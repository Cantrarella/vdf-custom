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
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using VDF.GUI.Views;

namespace VDF.GUI.HeadlessTests;

/// <summary>
/// The window-chrome guard for the dialogs: their surface, their shadow, and the frame
/// Windows needs to resize them.
///
/// The dialog look is a card floating over whatever is behind it, which takes three things
/// that live in three different places (the window, the style layer, the theme dictionary)
/// and that a screenshot can only confirm one dialog at a time: the window paints nothing
/// and allows transparency so the shadow has somewhere to land, the card keeps its own
/// opaque fill so the transparent window cannot show through it, and the frame keeps the
/// non-client frame when - and only when - the dialog can be resized.
/// </summary>
public class DialogShellTests {

	/// <summary>The room the content leaves around itself so the shadow has somewhere to go.</summary>
	const double ShadowMargin = 28;

	public static TheoryData<string> AllDialogs() {
		var data = new TheoryData<string>();
		foreach (string dialog in DialogCatalog.Dialogs.Keys)
			data.Add(dialog);
		return data;
	}

	static Window Open(string name, ThemeVariant? theme = null) {
		HeadlessUi.Shell(); // dialogs take their owner and icon from the main window
		var dialog = DialogCatalog.Dialogs[name]();
		if (theme != null)
			dialog.RequestedThemeVariant = theme;
		dialog.Show();
		HeadlessUi.Pump();
		return dialog;
	}

	/// <summary>The card itself: the border that carries the dialog's shadow.</summary>
	static Border FrameOf(Window dialog) {
		var frame = dialog.GetVisualDescendants().OfType<Border>()
			.FirstOrDefault(b => b.Classes.Contains("dlgframe"));
		Assert.NotNull(frame);
		return frame!;
	}

	[Theory]
	[MemberData(nameof(AllDialogs))]
	public Task The_window_is_only_a_pane_for_the_card(string name) => HeadlessUi.Run(() => {
		var dialog = Open(name);
		try {
			var background = dialog.Background as ISolidColorBrush;
			Assert.True(background == null || background.Color.A == 0,
				"the window itself must paint nothing: it is the transparent pane the shadow "
				+ $"lands on, and {background?.Color} in it would fill the whole margin");
			Assert.True(dialog.TransparencyLevelHint?.Contains(WindowTransparencyLevel.Transparent) == true,
				"without a transparent top level the margin is opaque again and the shadow is cut off at the card");
		}
		finally {
			dialog.Hide();
			HeadlessUi.Pump();
		}
	});

	[Theory]
	[MemberData(nameof(AllDialogs))]
	public Task The_card_keeps_its_own_surface(string name) => HeadlessUi.Run(() => {
		var dialog = Open(name);
		try {
			var frame = FrameOf(dialog);
			Assert.Equal(new Thickness(ShadowMargin), frame.Margin);
			// The fill moved off the window and onto the card when the window went
			// transparent; without it the card is see-through over the desktop.
			Assert.Equal(255, (frame.Background as ISolidColorBrush)?.Color.A ?? -1);
			// The mockup's --r-xl. It was 0 while the window still drew a rectangular
			// non-client frame, because a round card inside a square frame is two edges;
			// the window paints its own shadow now, so there is only the card's edge.
			Assert.Equal(new CornerRadius(26), frame.CornerRadius);
		}
		finally {
			dialog.Hide();
			HeadlessUi.Pump();
		}
	});

	[Theory]
	[MemberData(nameof(AllDialogs))]
	public Task A_resizable_dialog_keeps_the_frame_it_resizes_with(string name) => HeadlessUi.Run(() => {
		var dialog = Open(name);
		try {
			// Measured on the live windows with WM_NCHITTEST: a BorderOnly dialog answers
			// HTLEFT/HTRIGHT/HTTOP/HTBOTTOM/HTBOTTOMRIGHT along its edges, and the same
			// dialog as None answers HTCLIENT at every one of those points - Windows gives
			// resize borders to a thick frame, and nothing else. So None is only allowed
			// where there was never anything to resize.
			Assert.Equal(dialog.CanResize ? WindowDecorations.BorderOnly : WindowDecorations.None,
				dialog.WindowDecorations);
		}
		finally {
			dialog.Hide();
			HeadlessUi.Pump();
		}
	});

	[Theory]
	[MemberData(nameof(AllDialogs))]
	public Task The_shadow_is_asked_for_and_comes_from_the_theme(string name) => HeadlessUi.Run(() => {
		var light = Open(name, ThemeVariant.Light);
		BoxShadows lightShadow;
		try {
			lightShadow = FrameOf(light).BoxShadow;
			AssertShadowsDownward(lightShadow, "light");
		}
		finally {
			light.Hide();
			HeadlessUi.Pump();
		}

		var dark = Open(name, ThemeVariant.Dark);
		try {
			var darkShadow = FrameOf(dark).BoxShadow;
			AssertShadowsDownward(darkShadow, "dark");
			// The two themes do not share one shadow: the light one is a soft blue-grey
			// under a white card, the dark one is heavier because a black card on a black
			// page has nothing else to separate it.
			Assert.NotEqual(lightShadow, darkShadow);
		}
		finally {
			dark.Hide();
			HeadlessUi.Pump();
		}
	});

	[Fact]
	public Task The_shell_has_a_scrim_to_darken_while_a_dialog_is_open() => HeadlessUi.Run(() => {
		// The dimming itself only runs where there is a real shell behind the dialog
		// (see Utils/DialogDim), which a headless session does not have. What can be
		// guarded here is the layer it paints: present in the shell, opaque enough to
		// darken, and different per theme - dimming a dark page with the light theme's
		// grey would barely register.
		// The shell is shared by the whole session, so the theme it was found with has to
		// go back: AppearanceTests asserts the theme it set afterwards.
		var (shell, _) = HeadlessUi.Shell();
		var was = shell.RequestedThemeVariant;
		try {
			shell.RequestedThemeVariant = ThemeVariant.Light;
			HeadlessUi.Pump();
			var light = ScrimIn(shell, "light");

			shell.RequestedThemeVariant = ThemeVariant.Dark;
			HeadlessUi.Pump();
			Assert.NotEqual(light, ScrimIn(shell, "dark"));
		}
		finally {
			shell.RequestedThemeVariant = was;
			HeadlessUi.Pump();
		}
	});

	static Color ScrimIn(Window shell, string theme) {
		var scrim = shell.FindControl<Border>("DialogScrim");
		Assert.True(scrim != null, "the shell lost the layer that dims it behind a dialog");
		var brush = scrim!.Background as ISolidColorBrush;
		Assert.True(brush != null, $"the {theme} scrim resolved to nothing, so it darkens nothing");
		Assert.True(brush!.Color.A > 0, $"the {theme} scrim is fully transparent and dims nothing");
		return brush.Color;
	}

	static void AssertShadowsDownward(BoxShadows shadows, string theme) {
		Assert.True(shadows.Count == 1, $"the {theme} card wants exactly one shadow, the theme gave {shadows.Count}");
		var shadow = shadows[0];
		Assert.True(shadow.OffsetY > 0, $"the {theme} shadow must fall below the card, not be drawn around it");
		Assert.True(shadow.Blur > 0, $"the {theme} shadow needs blur to read as depth rather than as a second border");
		Assert.True(shadow.Color.A > 0, $"the {theme} shadow is invisible");
	}
}
