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

using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.VisualTree;
using VDF.GUI.ViewModels;
using VDF.GUI.Views;
using Xunit;

namespace VDF.GUI.HeadlessTests;

/// <summary>
/// The similarity slider spent a while changing its number while its knob stood still in
/// the middle of the track - which is the one thing on this page a person is supposed to
/// aim at. Three parts of the control template were missing, and each one of them fails
/// silently: no TargetType, unnamed track buttons, no orientation on the track. Nothing
/// throws, nothing logs, the number just moves on its own. These assertions are what
/// silence looks like when it is spelled out.
/// </summary>
public class SliderTemplateTests {
	[Fact]
	public Task TrackIsWiredToTheSlider() => HeadlessUi.Run(() => {
		var window = HeadlessUi.Show(new SetupView { DataContext = new MainWindowVM() });
		var slider = window.GetVisualDescendants().OfType<Slider>().First(s => s.Classes.Contains("mock"));
		var track = slider.GetVisualDescendants().OfType<Track>().Single();

		// Orientation first: without it the track lays its parts out vertically, and every
		// later assertion here still passes while the knob sits in the middle of the lane.
		Assert.Equal(slider.Orientation, track.Orientation);
		Assert.Equal(slider.Minimum, track.Minimum);
		Assert.Equal(slider.Maximum, track.Maximum);
		Assert.Equal(slider.Value, track.Value);

		// The two names are the whole click-to-seek feature. The slider looks them up by
		// name and wires pressed handlers on them; unnamed buttons look identical and do
		// nothing.
		Assert.Equal("PART_DecreaseButton", track.DecreaseButton!.Name);
		Assert.Equal("PART_IncreaseButton", track.IncreaseButton!.Name);
		window.Close();
	});

	[Fact]
	public Task KnobSitsWhereTheValueSays() => HeadlessUi.Run(() => {
		var window = HeadlessUi.Show(new SetupView { DataContext = new MainWindowVM() });
		var slider = window.GetVisualDescendants().OfType<Slider>().First(s => s.Classes.Contains("mock"));
		var track = slider.GetVisualDescendants().OfType<Track>().Single();
		var thumb = track.Thumb!;

		void AssertPlacement(string because) {
			var travel = track.Bounds.Width - thumb.Bounds.Width;
			var fraction = (slider.Value - slider.Minimum) / (slider.Maximum - slider.Minimum);
			var expected = fraction * travel;
			Assert.True(Math.Abs(thumb.Bounds.X - expected) < 0.5,
				$"{because}: knob at x={thumb.Bounds.X:0.##}, the value puts it at {expected:0.##}");
			// the fill is the part of the track behind the knob, so it ends exactly there
			Assert.True(Math.Abs(track.DecreaseButton!.Bounds.Width - thumb.Bounds.X) < 0.5,
				$"{because}: fill ends at {track.DecreaseButton.Bounds.Width:0.##}, knob starts at {thumb.Bounds.X:0.##}");
		}

		AssertPlacement("as opened");
		foreach (double value in new[] { slider.Minimum, 85d, slider.Maximum }) {
			slider.Value = value;
			HeadlessUi.Pump();
			AssertPlacement($"at value {value}");
		}
		window.Close();
	});
}
