using System;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.VisualTree;
using VDF.GUI.Views;

namespace VDF.GUI.Utils {

	/// <summary>
	/// Lets a captionless window be moved by its own body.
	///
	/// Every dialog in this app is its own top-level window, and none of them keeps a
	/// system caption - that is what makes them look like the mockup's cards. It is also
	/// what left them nailed to wherever WindowStartupLocation put them: a window with
	/// no non-client area has no strip the OS will drag it by, and there was nothing in
	/// the app taking the pointer's place.
	///
	/// So the body is the handle. Press anywhere that is not a control with an opinion
	/// of its own and the window follows the pointer, as far as you like.
	///
	/// The move is done by setting Position rather than by Window.BeginMoveDrag, which
	/// hands the gesture to the platform and, on a transparent window with no caption,
	/// is not something the Win32 backend promises to start. This way it is the same on
	/// every backend and holds for a modal dialog too.
	/// </summary>
	public static class DialogDrag {
		public static readonly AttachedProperty<bool> EnabledProperty =
			AvaloniaProperty.RegisterAttached<Window, bool>("Enabled", typeof(DialogDrag));

		public static bool GetEnabled(Window w) => w.GetValue(EnabledProperty);
		public static void SetEnabled(Window w, bool value) => w.SetValue(EnabledProperty, value);

		// Where the pointer grabbed, in the window's own coordinates. Kept per window
		// because two modeless dialogs (the blacklist manager, the database viewer) can
		// be open at once, and a single static would let one of them move the other.
		sealed class Drag { public Point Anchor; }

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
				w.PointerPressed += OnPressed;
				w.PointerMoved += OnMoved;
				w.PointerReleased += OnReleased;
			});
		}

		static void OnPressed(object? sender, PointerPressedEventArgs e) {
			if (sender is not Window w) return;
			if (w.WindowState != WindowState.Normal) return;   // never drag a maximised one
			if (!e.GetCurrentPoint(w).Properties.IsLeftButtonPressed) return;

			// A control that wants the press itself has usually marked the event handled,
			// and a handled event never reaches this window-level handler. The ones that
			// do not - a text field above all - would lose their own gesture to the
			// window sliding out from under them, so they are named here.
			if (OwnsThePress(e.Source)) return;

			Live.Remove(w);
			Live.Add(w, new Drag { Anchor = e.GetPosition(w) });

			// Without the capture the window stops following as soon as the pointer
			// leaves it, which during a drag it does constantly.
			e.Pointer.Capture(w);
		}

		static void OnMoved(object? sender, PointerEventArgs e) {
			if (sender is not Window w) return;
			if (!Live.TryGetValue(w, out var drag)) return;

			var here = e.GetPosition(w);

			// Measured from the anchor, not accumulated from the last move: the pointer
			// stays put on screen while the window travels under it, so "how far has the
			// pointer drifted from where it grabbed" IS the distance still to travel, and
			// it comes back to zero by itself once the window has caught up. Adding per
			// -move deltas instead would let the window's own motion cancel the pointer's
			// and the window would barely creep.
			//
			// GetPosition is in layout pixels and Position is in device pixels, hence the
			// scaling - wrong here and the window lags the pointer on any display that is
			// not 100%.
			var dx = (int)Math.Round((here.X - drag.Anchor.X) * w.RenderScaling);
			var dy = (int)Math.Round((here.Y - drag.Anchor.Y) * w.RenderScaling);
			if (dx == 0 && dy == 0) return;

			w.Position = new PixelPoint(w.Position.X + dx, w.Position.Y + dy);
		}

		static void OnReleased(object? sender, PointerReleasedEventArgs e) {
			if (sender is not Window w) return;
			Live.Remove(w);
			e.Pointer.Capture(null);
		}

		/// <summary>
		/// True when the press landed on something that needs it more than the window does.
		/// Walked up the visual tree, because what arrives here is the innermost element
		/// under the pointer - a TextPresenter inside a TextBox, say - and what matters is
		/// any ancestor that owns a gesture.
		/// </summary>
		static bool OwnsThePress(object? source) {
			for (var v = source as Visual; v != null; v = v.GetVisualParent()) {
				switch (v) {
					case TextBox:
					case NumericUpDown:
					case Slider:
					case ComboBox:
					case ListBox:
					case TreeView:
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
				}
			}
			return false;
		}
	}
}
