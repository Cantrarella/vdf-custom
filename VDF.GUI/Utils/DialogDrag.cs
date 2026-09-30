using System;
using System.Linq;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using VDF.GUI.Views;

namespace VDF.GUI.Utils {

	/// <summary>
	/// Lets a captionless window be moved by its own body, and sized by its own edges.
	///
	/// Every dialog in this app is its own top-level window, and none of them keeps a
	/// system caption - that is what makes them look like the mockup's cards. It is also
	/// what left them nailed to wherever WindowStartupLocation put them: a window with
	/// no non-client area has no strip the OS will drag it by, and there was nothing in
	/// the app taking the pointer's place.
	///
	/// So the body is the handle, and the edge is the grip. Press anywhere that is not a
	/// control with an opinion of its own and the window follows the pointer; press within
	/// a few pixels of the card's edge and that edge follows instead, corners included.
	///
	/// The move is done by setting Position rather than by Window.BeginMoveDrag, which
	/// hands the gesture to the platform and, on a transparent window with no caption,
	/// is not something the Win32 backend promises to start. This way it is the same on
	/// every backend and holds for a modal dialog too. The size is done by setting Width
	/// and Height for the same reason: there is no longer a frame for the OS to resize,
	/// so WM_NCHITTEST answers HTCLIENT the whole way round and dragging an edge against
	/// a window that says HTCLIENT moves a window that will not move.
	/// </summary>
	public static class DialogDrag {
		public static readonly AttachedProperty<bool> EnabledProperty =
			AvaloniaProperty.RegisterAttached<Window, bool>("Enabled", typeof(DialogDrag));

		public static bool GetEnabled(Window w) => w.GetValue(EnabledProperty);
		public static void SetEnabled(Window w, bool value) => w.SetValue(EnabledProperty, value);

		// How near the card's edge a press has to land to size instead of move. The card
		// is inset 28px inside its window so the shadow has somewhere to fall, so the
		// window's own edge is 28px of mostly-transparent margin away from anything the
		// eye calls an edge - measured from the window, this band would sit in dead space
		// and the visible border would do nothing.
		const double Edge = 6;

		// Nothing is pulled smaller than this unless the window asks for more (the
		// database viewer: 640x360). Layout pixels.
		const double FloorWidth = 160, FloorHeight = 96;

		[Flags]
		enum Edges {
			None = 0, Left = 1, Right = 2, Top = 4, Bottom = 8,
		}

		// Where the pointer grabbed, what it grabbed, and what the window measured at the
		// time. Kept per window because two modeless dialogs (the blacklist manager, the
		// database viewer) can be open at once, and a single static would let one of them
		// move the other.
		sealed class Drag {
			public IPointer Pointer = null!;
			public Point Anchor;
			public Edges Edges;
			public double Width, Height;   // layout pixels
			public PixelPoint Origin;      // device pixels
		}

		static readonly ConditionalWeakTable<Window, Drag> Live = new();

		// Same shape as DialogDim.Hooked, and for the same reason: closing a window
		// detaches its styles, which drives Enabled back to false. A handler that
		// unsubscribed on false would let go an instant before it was needed, so
		// subscribe once and never unsubscribe. The table keeps it from happening twice.
		static readonly ConditionalWeakTable<Window, object> Hooked = new();

		static DialogDrag() {
			EnabledProperty.Changed.Subscribe(e => {
				// MainWindow matches the Window selector too. It keeps its system caption
				// and must not be given a second way to move.
				if (e.Sender is not Window w || w is MainWindow) return;
				if (!(e.NewValue.HasValue && e.NewValue.Value)) return;
				if (Hooked.TryGetValue(w, out _)) return;
				Hooked.Add(w, new object());
				// The tunnel first, so an edge can be taken before anything under the
				// pointer has had the chance to swallow the press (see OnPressedTunnel).
				w.AddHandler(InputElement.PointerPressedEvent, OnPressedTunnel, RoutingStrategies.Tunnel);
				w.PointerPressed += OnPressed;
				w.PointerMoved += OnMoved;
				w.PointerReleased += OnReleased;
			});
		}

		/// <summary>
		/// The edge, taken on the way down.
		///
		/// Everything else waits for the press to bubble, so that a control under the
		/// pointer gets it first - which is the whole reason the body is a safe handle and
		// a text field is not. An edge cannot wait: the middle of the card is content, and
		// content answers the press. The database viewer's scroll view reaches the card's
		// right and left edge, and what it answers with is "handled", so a press there
		// never bubbled at all and the card had two dead edges along the sides that most
		// invite a pull.
		///
		/// Only an edge is taken here. Anything else falls through to the bubbling handler,
		/// and the named controls keep it even on an edge - a scrollbar riding the card's
		/// border beats the border, the same way it beats the body.
		/// </summary>
		static void OnPressedTunnel(object? sender, PointerPressedEventArgs e) {
			if (sender is not Window w) return;
			if (w.WindowState != WindowState.Normal) return;
			if (!w.CanResize) return;   // a window that says it cannot be resized keeps its word
			if (!e.GetCurrentPoint(w).Properties.IsLeftButtonPressed) return;
			if (OwnsTheEdge(e.Source)) return;

			var edges = HitEdges(e.GetPosition(w), CardRect(w));
			if (edges == Edges.None) return;

			TakeOverSize(w, edges);
			Begin(w, e, edges);

			// And the press goes no further. It is not enough to grab the edge before the
			// content sees the press: the content also takes the pointer, and a pointer it
			// holds is a pointer whose moves never reach this window - which leaves the
			// gesture started and dead. Taking the press away from it here is taking away
			// a press inside six pixels of the card's border, where nothing but the
			// border was ever going to answer it.
			e.Handled = true;
		}

		static void OnPressed(object? sender, PointerPressedEventArgs e) {
			if (sender is not Window w) return;
			if (w.WindowState != WindowState.Normal) return;   // never drag a maximised one
			if (!e.GetCurrentPoint(w).Properties.IsLeftButtonPressed) return;

			// The tunnel has already taken this one (an edge) and started the gesture;
			// starting it again here would throw away what it measured.
			if (Live.TryGetValue(w, out var held) && held.Pointer == e.Pointer) return;

			// A control that wants the press itself has usually marked the event handled,
			// and a handled event never reaches this window-level handler. The ones that
			// do not - a text field above all - would lose their own gesture to the
			// window sliding out from under them, so they are named here. A scrollbar
			// riding the card's edge beats the edge for the same reason.
			if (OwnsThePress(e.Source)) return;

			var here = e.GetPosition(w);

			// A window that says it cannot be resized keeps its word: five of the twelve
			// set CanResize="False" (the message box, the input box, About, the expression
			// builder, the quality order), and their edges move the window instead.
			var edges = w.CanResize ? HitEdges(here, CardRect(w)) : Edges.None;
			if (edges != Edges.None) TakeOverSize(w, edges);

			Begin(w, e, edges);
		}

		static void Begin(Window w, PointerPressedEventArgs e, Edges edges) {
			Live.Remove(w);
			Live.Add(w, new Drag {
				Pointer = e.Pointer,
				Anchor = e.GetPosition(w),
				Edges = edges,
				Width = w.Bounds.Width,
				Height = w.Bounds.Height,
				Origin = w.Position,
			});
			w.Cursor = CursorFor(edges);

			// Without the capture the window stops following as soon as the pointer
			// leaves it, which during a drag it does constantly.
			e.Pointer.Capture(w);
		}

		static void OnMoved(object? sender, PointerEventArgs e) {
			if (sender is not Window w) return;

			if (!Live.TryGetValue(w, out var drag)) {
				// No gesture in flight, so the only thing to do is say what a press here
				// would do. Without it an edge that can be dragged looks like any other
				// part of the card, and the grip is invisible until it is found by luck.
				if (w.WindowState == WindowState.Normal && w.CanResize)
					w.Cursor = CursorFor(HitEdges(e.GetPosition(w), CardRect(w)));
				return;
			}

			var here = e.GetPosition(w);

			// Measured from the anchor, not accumulated from the last move: the pointer
			// stays put on screen while the window travels under it, so "how far has the
			// pointer drifted from where it grabbed" IS the distance still to travel, and
			// it comes back to zero by itself once the window has caught up. Adding per
			// -move deltas instead would let the window's own motion cancel the pointer's
			// and the window would barely creep.
			var dx = here.X - drag.Anchor.X;
			var dy = here.Y - drag.Anchor.Y;

			if (drag.Edges == Edges.None) {
				// GetPosition is in layout pixels and Position is in device pixels, hence
				// the scaling - wrong here and the window lags the pointer on any display
				// that is not 100%.
				var mx = (int)Math.Round(dx * w.RenderScaling);
				var my = (int)Math.Round(dy * w.RenderScaling);
				if (mx == 0 && my == 0) return;
				w.Position = new PixelPoint(drag.Origin.X + mx, drag.Origin.Y + my);
				return;
			}

			Resize(w, drag, dx, dy);
		}

		static void Resize(Window w, Drag drag, double dx, double dy) {
			var scale = w.RenderScaling;
			var width = drag.Width;
			var height = drag.Height;
			var x = drag.Origin.X;
			var y = drag.Origin.Y;

			// Same anchor arithmetic as the move, and for the same reason: the edge being
			// pulled travels with the pointer, so the pointer's distance from where it
			// grabbed is the distance the edge still owes, and it settles at zero.
			//
			// Left and top are the two that also move the window: the far edge is pinned
			// and the near one follows, which is what every window manager does and the
			// only reading under which "drag the left edge right" means "get narrower".
			if (drag.Edges.HasFlag(Edges.Left)) {
				var take = Math.Min(dx, width - MinWidthOf(w));
				x += (int)Math.Round(take * scale);
				width -= take;
			}
			else if (drag.Edges.HasFlag(Edges.Right)) {
				// Never past the floor, however far the pointer goes the wrong way.
				width += Math.Max(dx, MinWidthOf(w) - width);
			}

			if (drag.Edges.HasFlag(Edges.Top)) {
				var take = Math.Min(dy, height - MinHeightOf(w));
				y += (int)Math.Round(take * scale);
				height -= take;
			}
			else if (drag.Edges.HasFlag(Edges.Bottom)) {
				height += Math.Max(dy, MinHeightOf(w) - height);
			}

			w.Position = new PixelPoint(x, y);
			w.Width = width;
			w.Height = height;
		}

		static void OnReleased(object? sender, PointerReleasedEventArgs e) {
			if (sender is not Window w) return;
			Live.Remove(w);
			e.Pointer.Capture(null);
			if (w.WindowState == WindowState.Normal && w.CanResize)
				w.Cursor = CursorFor(HitEdges(e.GetPosition(w), CardRect(w)));
		}

		/// <summary>
		/// Which edges the point is near, if any. Measured on the card rather than on the
		/// window: the card is what the eye calls the edge, and the 28px between it and
		/// the window is transparent margin the shadow falls into.
		/// </summary>
		static Edges HitEdges(Point p, Rect card) {
			if (card.Width <= 0 || card.Height <= 0) return Edges.None;

			var edges = Edges.None;
			if (Math.Abs(p.X - card.Left) <= Edge) edges |= Edges.Left;
			else if (Math.Abs(p.X - card.Right) <= Edge) edges |= Edges.Right;
			if (Math.Abs(p.Y - card.Top) <= Edge) edges |= Edges.Top;
			else if (Math.Abs(p.Y - card.Bottom) <= Edge) edges |= Edges.Bottom;
			return edges;
		}

		/// <summary>
		/// The card's rectangle in the window's own coordinates. The dialogs all carry it
		/// as Border.dlgframe, which is also the one thing they have in common as shapes -
		/// what is inside differs too much to name.
		///
		/// Found once and remembered: the pointer moves over a card hundreds of times a
		/// second and each move needs the rectangle to answer "would a press here size",
		/// so walking the whole tree per move is not free.
		/// </summary>
		static Rect CardRect(Window w) {
			var entry = Cards.GetValue(w, static _ => new Card());
			if (entry.Found is not { } found || !found.IsAttachedToVisualTree()) {
				entry.Found = found = w.GetVisualDescendants().OfType<Border>()
					.FirstOrDefault(b => b.Classes.Contains("dlgframe"));
			}
			if (found is null) return new Rect(0, 0, w.Bounds.Width, w.Bounds.Height);

			// TranslatePoint rather than Bounds: the border's own bounds are in its
			// parent's coordinates, and the card is not always the window's direct child.
			var at = found.TranslatePoint(default, w);
			return at is null
				? new Rect(0, 0, w.Bounds.Width, w.Bounds.Height)
				: new Rect(at.Value, found.Bounds.Size);
		}

		sealed class Card { public Border? Found; }

		static readonly ConditionalWeakTable<Window, Card> Cards = new();

		/// <summary>
		/// Takes the size off SizeToContent for the axes about to be pulled.
		///
		/// Several dialogs are measured to their content, and a window that is measured to
		/// its content answers a new Width by measuring again and coming back at the old
		/// size - the drag would look like nothing happened. So the size is pinned to what
		/// the window measures right now and SizeToContent gives up that axis, which means
		/// a dialog the user has sized by hand stops re-measuring itself afterwards. That
		/// is the right answer for a window the user has just taken hold of: the last thing
		/// they asked for is the size they asked for.
		/// </summary>
		static void TakeOverSize(Window w, Edges edges) {
			var mode = w.SizeToContent;
			// "Left or Right", not "Left and Right": HasFlag on a compound value asks for
			// both of them, and no one gesture ever pulls two opposite edges.
			var takeWidth = mode.HasFlag(SizeToContent.Width) && (edges & (Edges.Left | Edges.Right)) != 0;
			var takeHeight = mode.HasFlag(SizeToContent.Height) && (edges & (Edges.Top | Edges.Bottom)) != 0;
			if (!takeWidth && !takeHeight) return;

			// Width/Height first: SizeToContent is what has been supplying them, so the
			// value being written is the value already on screen and nothing jumps.
			if (takeWidth) w.Width = w.Bounds.Width;
			if (takeHeight) w.Height = w.Bounds.Height;

			if (takeWidth) mode &= ~SizeToContent.Width;
			if (takeHeight) mode &= ~SizeToContent.Height;
			w.SizeToContent = mode;
		}

		static double MinWidthOf(Window w) => Math.Max(w.MinWidth, FloorWidth);
		static double MinHeightOf(Window w) => Math.Max(w.MinHeight, FloorHeight);

		static readonly Cursor SizeWestEast = new(StandardCursorType.SizeWestEast);
		static readonly Cursor SizeNorthSouth = new(StandardCursorType.SizeNorthSouth);
		static readonly Cursor SizeNorthWestSouthEast = new(StandardCursorType.TopLeftCorner);
		static readonly Cursor SizeNorthEastSouthWest = new(StandardCursorType.TopRightCorner);

		/// <summary>
		/// The cursor that says what a press here would do. Corners get the two diagonals,
		/// so the card can be told apart from the desktop without pressing anything.
		/// </summary>
		static Cursor CursorFor(Edges edges) => edges switch {
			Edges.Left or Edges.Right => SizeWestEast,
			Edges.Top or Edges.Bottom => SizeNorthSouth,
			Edges.Left | Edges.Top or Edges.Right | Edges.Bottom => SizeNorthWestSouthEast,
			Edges.Left | Edges.Bottom or Edges.Right | Edges.Top => SizeNorthEastSouthWest,
			_ => Cursor.Default,
		};

		/// <summary>
		/// True when the press landed on something that needs it more than the window does.
		/// Walked up the visual tree, because what arrives here is the innermost element
		/// under the pointer - a TextPresenter inside a TextBox, say - and what matters is
		/// any ancestor that owns a gesture.
		/// </summary>
		static bool OwnsThePress(object? source) => Owns(source, lists: true);

		/// <summary>
		/// The same question asked of an edge, and answered with a shorter list: a list
		/// keeps its press in the middle of the card, where its rows are, and not along
		/// the six pixels next to the border.
		///
		/// A list is the case that forced the two apart. The database viewer's list reaches
		/// the card's edge, so every press near the left and right border landed in it - and
		/// a card whose two longest edges answer to their contents is a card that cannot be
		/// pulled. Rows are chosen in the middle, and the border belongs to the window: with
		/// a real frame those six pixels are not the list's to answer either.
		///
		/// The controls that keep it even here are the ones whose whole gesture starts in
		/// the press - a scrollbar to be dragged, a thumb to be slid, a caret to be set.
		/// </summary>
		static bool OwnsTheEdge(object? source) => Owns(source, lists: false);

		static bool Owns(object? source, bool lists) {
			for (var v = source as Visual; v != null; v = v.GetVisualParent()) {
				switch (v) {
					case TextBox:
					case NumericUpDown:
					case Slider:
					case ComboBox:
					case ScrollBar:
					// ToggleButton before Button, because it derives from it: a case for
					// Button already matches one, and the compiler says so. CheckBox and
					// RadioButton derive from ToggleButton in turn, so naming it covers
					// all three without naming them.
					case ToggleButton:
					case Button:
					case MenuItem:
					case DatePicker:
					case TimePicker:
						return true;
					case ListBox:
					case TreeView:
						if (lists) return true;
						break;
				}
			}
			return false;
		}
	}
}
