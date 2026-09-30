// /*
//     Copyright (C) 2026 0x90d
//     This file is part of VideoDuplicateFinder
//     VideoDuplicateFinder is free software: you can redistribute it and/or modify
//     it under the terms of the GNU Affero General Public License as published by
//     the Free Software Foundation, either version 3 of the License, or
//     (at your option) any later version.
//     VideoDuplicateFinder is distributed in the hope that it will be useful,
//     but WITHOUT ANY WARRANTY without even the implied warranty of
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
using VDF.GUI.Utils;

namespace VDF.GUI.HeadlessTests;

/// <summary>
/// The dialog drag, driven through the real input pipeline rather than by calling the
/// handler: the whole point of the feature is that a press reaches the window when it
/// should and reaches the control when it should, and only a real press answers that.
///
/// What cannot be checked here is the feel - the pointer following the window on a
/// scaled display. That is RenderScaling arithmetic, and headless renders at 1:1.
/// </summary>
public class DialogDragTests {

	// Every dialog is inset 28px for its shadow, so a point inside that band is on the
	// window itself and on nothing else - the plainest possible handle.
	static readonly Point OnTheBareFrame = new(6, 6);

	static Window Open(string name) {
		// The shell first: a dialog's constructor reaches ApplicationHelpers.MainWindow
		// for its owner and icon, and that getter throws when there is nothing there yet.
		var shell = HeadlessUi.Shell().Window;
		var dlg = DialogCatalog.Dialogs[name]();
		dlg.Show(shell);
		HeadlessUi.Pump();
		return dlg;
	}

	static Point CentreOf(Visual v, Visual inWindow) {
		var p = v.TranslatePoint(new Point(v.Bounds.Width / 2, v.Bounds.Height / 2), inWindow);
		Assert.NotNull(p);
		return p!.Value;
	}

	/// <summary>
	/// The first control of its kind that is actually on screen. A message box builds all
	/// four buttons and collapses the ones this result set does not use - the collapsed
	/// one still enumerates first, and its centre lands on empty space, where the window
	/// takes the press and the assertion below would pass for the wrong reason.
	/// </summary>
	static T Visible<T>(Window dialog) where T : Visual =>
		dialog.GetVisualDescendants().OfType<T>().First(v => v.Bounds.Width > 0 && v.Bounds.Height > 0);

	static void Drag(Window w, Point from, Point to) {
		w.MouseDown(from, MouseButton.Left);
		w.MouseMove(to, RawInputModifiers.LeftMouseButton);
		HeadlessUi.Pump();
	}

	[Fact]
	public Task Drags_by_its_body_and_stops_when_the_button_comes_up() => HeadlessUi.Run(() => {
		var dlg = Open("MessageBox");
		try {
			var before = dlg.Position;
			var from = OnTheBareFrame;
			var to = new Point(from.X + 50, from.Y + 30);

			Drag(dlg, from, to);
			Assert.Equal(new PixelPoint(before.X + 50, before.Y + 30), dlg.Position);

			dlg.MouseUp(to, MouseButton.Left);
			HeadlessUi.Pump();
			var released = dlg.Position;

			// Without the release the window would keep following the pointer around
			// the desktop for the rest of the session.
			dlg.MouseMove(new Point(to.X + 120, to.Y + 90), RawInputModifiers.LeftMouseButton);
			HeadlessUi.Pump();
			Assert.Equal(released, dlg.Position);
		}
		finally {
			dlg.Close();
			HeadlessUi.Pump();
		}
	});

	[Fact]
	public Task Measures_from_the_grab_point_not_from_the_last_move() => HeadlessUi.Run(() => {
		var dlg = Open("MessageBox");
		try {
			var before = dlg.Position;

			// A headless pointer is addressed in the window's own coordinates, so a window
			// that has just moved 40px right leaves the pointer back where it grabbed -
			// which is exactly the situation the real one is in once the window has caught
			// up with it. Measuring from the anchor, this second move is therefore nothing
			// to do; accumulating per move, it would read as 40px back to the left and the
			// window would twitch under a pointer that never moved.
			Drag(dlg, OnTheBareFrame, new Point(OnTheBareFrame.X + 40, OnTheBareFrame.Y));
			dlg.MouseMove(OnTheBareFrame, RawInputModifiers.LeftMouseButton);
			HeadlessUi.Pump();

			Assert.Equal(before.X + 40, dlg.Position.X);
			Assert.Equal(before.Y, dlg.Position.Y);
		}
		finally {
			dlg.MouseUp(OnTheBareFrame, MouseButton.Left);
			dlg.Close();
			HeadlessUi.Pump();
		}
	});

	[Fact]
	public Task A_text_box_keeps_its_own_press() => HeadlessUi.Run(() => {
		var dlg = Open("InputBox");
		try {
			var box = Visible<TextBox>(dlg);
			var before = dlg.Position;

			Drag(dlg, CentreOf(box, dlg), new Point(OnTheBareFrame.X + 200, OnTheBareFrame.Y + 200));

			// This is the one that goes wrong quietly: the window slides out from under
			// the pointer and the caret lands wherever the drag ended.
			Assert.Equal(before, dlg.Position);
		}
		finally {
			dlg.Close();
			HeadlessUi.Pump();
		}
	});

	[Fact]
	public Task A_button_keeps_its_own_press() => HeadlessUi.Run(() => {
		var dlg = Open("MessageBox");
		try {
			var button = Visible<Button>(dlg);
			var before = dlg.Position;

			Drag(dlg, CentreOf(button, dlg), new Point(OnTheBareFrame.X + 200, OnTheBareFrame.Y + 200));

			Assert.Equal(before, dlg.Position);
		}
		finally {
			dlg.Close();
			HeadlessUi.Pump();
		}
	});

	[Fact]
	public Task A_list_keeps_its_own_press() => HeadlessUi.Run(() => {
		var dlg = Open("BlacklistManager");
		try {
			var list = Visible<ListBox>(dlg);
			var before = dlg.Position;

			Drag(dlg, CentreOf(list, dlg), new Point(OnTheBareFrame.X + 200, OnTheBareFrame.Y + 200));

			// Selecting a row must not move the window; the space around the list still
			// does, which is why only the named controls are excluded and not containers.
			Assert.Equal(before, dlg.Position);
		}
		finally {
			dlg.Close();
			HeadlessUi.Pump();
		}
	});

	[Fact]
	public Task The_shell_keeps_its_system_caption() => HeadlessUi.Run(() => {
		var shell = HeadlessUi.Shell().Window;
		var before = shell.Position;

		// MainWindow matches the Window selector that carries the drag, and must not
		// pick it up: it has a caption, and a second way to move would fight the first.
		Drag(shell, OnTheBareFrame, new Point(OnTheBareFrame.X + 200, OnTheBareFrame.Y + 200));
		shell.MouseUp(new Point(200, 200), MouseButton.Left);
		HeadlessUi.Pump();

		Assert.Equal(before, shell.Position);
	});

	[Theory]
	[MemberData(nameof(DialogShellTests.AllDialogs), MemberType = typeof(DialogShellTests))]
	public Task Every_dialog_carries_the_drag(string name) => HeadlessUi.Run(() => {
		var dlg = Open(name);
		try {
			Assert.True(DialogDrag.GetEnabled(dlg),
				$"'{name}' did not pick up DialogDrag.Enabled from the Window style, so it cannot be moved");
		}
		finally {
			dlg.Close();
			HeadlessUi.Pump();
		}
	});
}
