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

using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using VDF.GUI.Utils;

namespace VDF.GUI.HeadlessTests;

/// <summary>
/// #906: a switch that is on looked like one that is off, apart from the knob's side.
/// On is now filled with the accent, in every theme, and its knob stays visible on the fill.
/// </summary>
public class SwitchStateTests {
	public static TheoryData<string> Themes() => new() { "Light", "Dark" };

	static ThemeVariant Variant(string name) => name == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;

	// Both templates are read, on purpose. The names below are template internals, and there
	// are two switch templates now: the theme's own (parts "border" and "glyph") and the plain
	// one this app draws for the settings rows (VdfSwitchTrack, with the knob an Ellipse inside
	// PART_SwitchKnob). Asking for one spelling only is exactly how this stopped guarding #906
	// - it threw "no matching element" and read as a broken switch rather than a moved part.
	static (Color Track, Color Knob) Colors(ToggleSwitch toggle) {
		var all = toggle.GetVisualDescendants().ToList();
		var track = all.OfType<Border>().FirstOrDefault(b => b.Name == "VdfSwitchTrack")
				 ?? all.OfType<Border>().First(b => b.Name == "border");
		Color trackColor = ((ISolidColorBrush)track.Background!).Color;

		// Ours draws the knob as an Ellipse and colours its fill; the theme's draws it as a
		// glyph and colours its foreground. Which shape is present is what tells them apart -
		// NOT the Canvas they sit in, which both templates name PART_SwitchKnob.
		var ellipse = all.OfType<Ellipse>().FirstOrDefault();
		if (ellipse is not null)
			return (trackColor, ((ISolidColorBrush)ellipse.Fill!).Color);
		var glyph = all.OfType<ContentPresenter>().First(c => c.Name == "glyph");
		return (trackColor, ((ISolidColorBrush)glyph.Foreground!).Color);
	}

	[Theory]
	[MemberData(nameof(Themes))]
	public Task OnIsFilled_AndItsKnobStandsOut(string theme) => HeadlessUi.Run(() => {
		HeadlessUi.Shell();
		var on = new ToggleSwitch { IsChecked = true, Classes = { "plain" } };
		var off = new ToggleSwitch { IsChecked = false, Classes = { "plain" } };
		var window = new Window { Width = 300, Height = 200, RequestedThemeVariant = Variant(theme), Content = new StackPanel { Children = { on, off } } };
		window.Show();
		HeadlessUi.Pump();
		try {
			var (onTrack, onKnob) = Colors(on);
			var (offTrack, _) = Colors(off);

			Assert.NotEqual(offTrack, onTrack);
			// WCAG 1.4.11: the knob is what shows the state, 3:1 against what it sits on.
			double knobContrast = ContrastTests.Ratio(onKnob, onTrack);
			Assert.True(knobContrast >= 3.0, $"knob {onKnob} on track {onTrack}: {knobContrast:0.00}:1 in {theme}");
		}
		finally {
			window.Close();
		}
	});

	[Fact]
	public Task DisabledSwitches_KeepTheThemesLook() => HeadlessUi.Run(() => {
		HeadlessUi.Shell();
		var disabledOn = new ToggleSwitch { IsChecked = true, IsEnabled = false };
		var disabledOff = new ToggleSwitch { IsChecked = false, IsEnabled = false };
		var window = new Window { Width = 300, Height = 200, Content = new StackPanel { Children = { disabledOn, disabledOff } } };
		window.Show();
		HeadlessUi.Pump();
		try {
			Assert.Equal(Colors(disabledOff).Track, Colors(disabledOn).Track);
		}
		finally {
			window.Close();
		}
	});
}
