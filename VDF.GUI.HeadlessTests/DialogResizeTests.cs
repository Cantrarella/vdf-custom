// /*
//     Copyright (C) 2026 0x90d
//     This file is part of VideoDuplicateFinder
//     VideoDuplicateFinder is free software: you can redistribute it and/or modify
//     it under the terms of the GNU Affero General Public License as published by
//     the Free Software Foundation, either version 3 of the License, or
//     (at your option) any later version.
//     VideoDuplicateFinder is distributed in the hope that it will be useful,
//     but WITHOUT ANY WARRANTY; without even the implied warranty of
//     MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//     GNU Affero General Public License for more details.
//     You should have received a copy of the GNU Affero General Public License
//     along with VideoDuplicateFinder.  If not, see <http://www.gnu.org/licenses/>.
// */
//

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace VDF.GUI.HeadlessTests;

/// <summary>
/// The dialog edges, driven through the real input pipeline like the drag is.
///
/// These windows have no frame at all, which is what makes them look like the mockup's
/// cards and is also what took their edges away: with no WS_THICKFRAME the OS answers
/// HTCLIENT the whole way round, so nothing resizes a window that does not own a single
/// non-client pixel. What answers here is the app, measuring the press against the card
/// it draws and setting the size itself.
///
/// What cannot be checked here is the feel - whether 6px is a comfortable grip, and
/// whether the edge tracks the pointer on a scaled display. Headless renders at 1:1 and
/// has no opinion about comfort; both are for a real pointer to answer.
/// </summary>
public class DialogResizeTests {

	// The grip is the card's own edge, six pixels either side of it, so a press taken
	// three pixels inside is a press on the edge and not on anything the card holds.
	const double Inside = 3;

	static Window Open(string name) {
		// The shell first: a dialog's constructor reaches ApplicationHelpers.MainWindow
		// for its owner and icon, and that getter throws when there is nothing there yet.
		var shell = HeadlessUi.Shell().Window;
		var dlg = DialogCatalog.Dialogs[name]();
		dlg.Show(shell);
		HeadlessUi.Pump();
		return dlg;
	}

	/// <summary>
	/// The card's rectangle in the window's coordinates. The dialogs carry it as
	/// Border.dlgframe, inset 28px so its shadow has somewhere to fall - which is why the
	/// edge to aim at is this one and not the window's.
	/// </summary>
	static Rect CardOf(Window dlg) {
		var card = dlg.GetVisualDescendants().OfType<Border>()
			.First(b => b.Classes.Contains("dlgframe"));
		var at = card.TranslatePoint(default, dlg);
		Assert.NotNull(at);
		return new Rect(at!.Value, card.Bounds.Size);
	}

	static Point RightOf(Rect c) => new(c.Right - Inside, c.Top + c.Height / 2);
	static Point LeftOf(Rect c) => new(c.Left + Inside, c.Top + c.Height / 2);
	static Point BottomOf(Rect c) => new(c.Left + c.Width / 2, c.Bottom - Inside);
	static Point TopOf(Rect c) => new(c.Left + c.Width / 2, c.Top + Inside);
	static Point BottomRightOf(Rect c) => new(c.Right - Inside, c.Bottom - Inside);

	static void Pull(Window w, Point from, Point to) {
		w.MouseDown(from, MouseButton.Left);
		w.MouseMove(to, RawInputModifiers.LeftMouseButton);
		HeadlessUi.Pump();
	}

	static void LetGo(Window w, Point at) {
		w.MouseUp(at, MouseButton.Left);
		HeadlessUi.Pump();
	}

	[Fact]
	public Task The_right_edge_pulls_out_and_leaves_the_corner_alone() => HeadlessUi.Run(() => {
		var dlg = Open("DatabaseEditor");
		try {
			var card = CardOf(dlg);
			var before = dlg.Bounds;
			var at = RightOf(card);

			Pull(dlg, at, new Point(at.X + 60, at.Y));
			LetGo(dlg, new Point(at.X + 60, at.Y));

			// The far edge is pinned, so the window does not budge and only the width
			// changes - the one thing that separates this from a move.
			Assert.Equal(60, dlg.Bounds.Width - before.Width);
			Assert.Equal(before.Height, dlg.Bounds.Height);
			Assert.Equal(before.Position, dlg.Bounds.Position);
		}
		finally {
			dlg.Close();
			HeadlessUi.Pump();
		}
	});

	[Fact]
	public Task The_left_edge_pulls_in_and_takes_the_window_with_it() => HeadlessUi.Run(() => {
		var dlg = Open("DatabaseEditor");
		try {
			var card = CardOf(dlg);
			var before = dlg.Bounds;
			var was = dlg.Position;
			var at = LeftOf(card);

			Pull(dlg, at, new Point(at.X + 60, at.Y));
			LetGo(dlg, new Point(at.X + 60, at.Y));

			// Dragging the left edge right makes the card narrower, which only reads as
			// "narrower" if the right edge stays where it was: the window moves as far
			// as the edge did.
			Assert.Equal(-60, dlg.Bounds.Width - before.Width);
			Assert.Equal(new PixelPoint(was.X + 60, was.Y), dlg.Position);
		}
		finally {
			dlg.Close();
			HeadlessUi.Pump();
		}
	});

	[Fact]
	public Task The_bottom_edge_pulls_down() => HeadlessUi.Run(() => {
		var dlg = Open("DatabaseEditor");
		try {
			var card = CardOf(dlg);
			var before = dlg.Bounds;
			var at = BottomOf(card);

			Pull(dlg, at, new Point(at.X, at.Y + 60));
			LetGo(dlg, new Point(at.X, at.Y + 60));

			Assert.Equal(60, dlg.Bounds.Height - before.Height);
			Assert.Equal(before.Width, dlg.Bounds.Width);
		}
		finally {
			dlg.Close();
			HeadlessUi.Pump();
		}
	});

	[Fact]
	public Task The_top_edge_pulls_down_and_takes_the_window_with_it() => HeadlessUi.Run(() => {
		var dlg = Open("DatabaseEditor");
		try {
			var card = CardOf(dlg);
			var before = dlg.Bounds;
			var was = dlg.Position;
			var at = TopOf(card);

			Pull(dlg, at, new Point(at.X, at.Y + 60));
			LetGo(dlg, new Point(at.X, at.Y + 60));

			Assert.Equal(-60, dlg.Bounds.Height - before.Height);
			Assert.Equal(new PixelPoint(was.X, was.Y + 60), dlg.Position);
		}
		finally {
			dlg.Close();
			HeadlessUi.Pump();
		}
	});

	[Fact]
	public Task A_corner_pulls_both_ways_at_once() => HeadlessUi.Run(() => {
		var dlg = Open("DatabaseEditor");
		try {
			var card = CardOf(dlg);
			var before = dlg.Bounds;
			var at = BottomRightOf(card);

			Pull(dlg, at, new Point(at.X + 40, at.Y + 30));
			LetGo(dlg, new Point(at.X + 40, at.Y + 30));

			Assert.Equal(40, dlg.Bounds.Width - before.Width);
			Assert.Equal(30, dlg.Bounds.Height - before.Height);
		}
		finally {
			dlg.Close();
			HeadlessUi.Pump();
		}
	});

	[Fact]
	public Task An_edge_stops_at_the_window_own_minimum() => HeadlessUi.Run(() => {
		var dlg = Open("DatabaseEditor");
		try {
			var card = CardOf(dlg);
			var at = LeftOf(card);

			// Far past the point where there is nothing left to give: the database viewer
			// names 640x360 itself, and a card pulled to nothing is not a card.
			Pull(dlg, at, new Point(at.X + 4000, at.Y));
			LetGo(dlg, new Point(at.X + 4000, at.Y));

			Assert.Equal(640, dlg.Bounds.Width);
		}
		finally {
			dlg.Close();
			HeadlessUi.Pump();
		}
	});

	[Fact]
	public Task A_window_that_says_it_cannot_be_resized_keeps_its_word() => HeadlessUi.Run(() => {
		// The message box is CanResize="False": five of the twelve are, and their edges
		// were never a grip. Pressing one moves the window, which is what the body does.
		var dlg = Open("MessageBox");
		try {
			var card = CardOf(dlg);
			var before = dlg.Bounds;
			var was = dlg.Position;
			var at = RightOf(card);

			Pull(dlg, at, new Point(at.X + 60, at.Y));
			LetGo(dlg, new Point(at.X + 60, at.Y));

			Assert.Equal(before.Width, dlg.Bounds.Width);
			Assert.Equal(new PixelPoint(was.X + 60, was.Y), dlg.Position);
		}
		finally {
			dlg.Close();
			HeadlessUi.Pump();
		}
	});

	[Fact]
	public Task A_dialog_measured_to_its_content_can_still_be_pulled() => HeadlessUi.Run(() => {
		// The custom selection is SizeToContent="WidthAndHeight". A window measured to its
		// content answers a new width by measuring again, so without taking the axis off
		// SizeToContent the drag would look like nothing at all happened.
		var dlg = Open("CustomSelection");
		try {
			var card = CardOf(dlg);
			var before = dlg.Bounds.Width;
			var at = RightOf(card);

			Pull(dlg, at, new Point(at.X + 80, at.Y));
			LetGo(dlg, new Point(at.X + 80, at.Y));

			Assert.Equal(80, dlg.Bounds.Width - before);

			// Only the axis that was pulled lets go: the width is the user's from here on,
			// the height still answers the content.
			Assert.Equal(SizeToContent.Height, dlg.SizeToContent);
		}
		finally {
			dlg.Close();
			HeadlessUi.Pump();
		}
	});

	[Fact]
	public Task The_cursor_says_what_the_edge_will_do() => HeadlessUi.Run(() => {
		var dlg = Open("DatabaseEditor");
		try {
			var card = CardOf(dlg);

			dlg.MouseMove(RightOf(card));
			HeadlessUi.Pump();
			var onTheEdge = dlg.Cursor;

			dlg.MouseMove(BottomOf(card));
			HeadlessUi.Pump();
			var onTheFloor = dlg.Cursor;

			dlg.MouseMove(new Point(card.Left + card.Width / 2, card.Top + card.Height / 2));
			HeadlessUi.Pump();
			var inTheMiddle = dlg.Cursor;

			// Not the same cursor on a vertical edge as on a horizontal one, and no
			// sizing cursor at all in the middle - which is the whole of the hint: the
			// grip is invisible without it.
			Assert.NotNull(onTheEdge);
			Assert.NotSame(onTheEdge, onTheFloor);
			Assert.NotSame(onTheEdge, inTheMiddle);
		}
		finally {
			dlg.Close();
			HeadlessUi.Pump();
		}
	});

	/// <summary>
	/// Every dialog whose window says it can be resized has to answer somewhere along its
	/// edge - not at one agreed point, because that point belongs to whatever the dialog
	/// puts there: a button, a scrollbar, a list. What is guarded is the edge as a whole,
	/// and the six pixels next to it belonging to the window rather than to the contents
	/// (a list that reaches the border still yields the border).
	///
	/// Any edge answers it, in either direction, because a window is free to say it will
	/// not go further: the algorithm dialog sets MaxWidth="720" and is measured to exactly
	/// that, so pulling its right edge is a pull against a limit the window set itself and
	/// nothing happens - correctly. Pulling its left edge still narrows it.
	/// </summary>
	[Theory]
	[MemberData(nameof(DialogShellTests.AllDialogs), MemberType = typeof(DialogShellTests))]
	public Task Every_dialog_that_can_be_resized_answers_its_edge(string name) => HeadlessUi.Run(() => {
		var dlg = Open(name);
		try {
			if (!dlg.CanResize) return;   // the five that say they cannot: their edges are body

			var pulled = false;
			for (var i = 0; i < 6 && !pulled; i++) {
				var card = CardOf(dlg);
				var x = card.Left + 12 + i * (card.Width - 24) / 5;
				var y = card.Top + 12 + i * (card.Height - 24) / 5;

				pulled =
					Moves(dlg, new Point(card.Right - Inside, y), new Point(card.Right - Inside + 40, y), horizontal: true) ||
					Moves(dlg, new Point(card.Left + Inside, y), new Point(card.Left + Inside + 40, y), horizontal: true) ||
					Moves(dlg, new Point(x, card.Bottom - Inside), new Point(x, card.Bottom - Inside + 40), horizontal: false) ||
					Moves(dlg, new Point(x, card.Top + Inside), new Point(x, card.Top + Inside + 40), horizontal: false);
			}

			Assert.True(pulled, $"'{name}' did not answer a pull on any of its four edges");
		}
		finally {
			dlg.Close();
			HeadlessUi.Pump();
		}
	});

	/// <summary>
	/// True when the pull changed the side being pulled by more than a rounding error.
	/// </summary>
	static bool Moves(Window dlg, Point from, Point to, bool horizontal) {
		var before = horizontal ? dlg.Bounds.Width : dlg.Bounds.Height;
		Pull(dlg, from, to);
		LetGo(dlg, to);
		var after = horizontal ? dlg.Bounds.Width : dlg.Bounds.Height;
		return Math.Abs(after - before) > 20;
	}

	[Fact]
	public Task The_edge_lets_go_when_the_button_comes_up() => HeadlessUi.Run(() => {
		var dlg = Open("DatabaseEditor");
		try {
			var card = CardOf(dlg);
			var at = RightOf(card);
			Pull(dlg, at, new Point(at.X + 60, at.Y));
			LetGo(dlg, new Point(at.X + 60, at.Y));
			var sized = dlg.Bounds.Width;

			// Same as the drag: a window whose edge is still held keeps following the
			// pointer for the rest of the session.
			dlg.MouseMove(new Point(at.X + 260, at.Y), RawInputModifiers.LeftMouseButton);
			HeadlessUi.Pump();

			Assert.Equal(sized, dlg.Bounds.Width);
		}
		finally {
			dlg.Close();
			HeadlessUi.Pump();
		}
	});
}
