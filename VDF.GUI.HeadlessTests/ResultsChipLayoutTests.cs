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
using Avalonia.Layout;
using Avalonia.VisualTree;
using VDF.GUI.Views;

namespace VDF.GUI.HeadlessTests;

/// <summary>
/// The group head of the result list holds the similarity chips, the AI tag and two buttons
/// in one horizontal StackPanel. A StackPanel stretches its children across the cross axis,
/// so the chips were pulled up to the buttons' height while their text stayed at the top of
/// the taller box - 6px of space above the text and 16px below, which reads as a chip that
/// has come apart. The chips now centre themselves in the row.
///
/// Two of those four chips are only built for a combined grayscale + pHash scan and one only
/// for an AI match, so a single screenshot on an ordinary result set cannot see them; this
/// covers every chip the row can produce, whatever the scan did.
/// </summary>
public class ResultsChipLayoutTests {

	[Fact]
	public Task ResultsView_GroupHeadChips_CentreAgainstTheButtonsBesideThem() => HeadlessUi.Run(() => {
		var vm = ResultsFixture.CreatePopulatedViewModel();
		var window = HeadlessUi.Show(new DuplicateResultsView { DataContext = vm });
		HeadlessUi.Pump();

		try {
			var chips = Chips(window, "simchip");
			Assert.NotEmpty(chips);
			foreach (var chip in chips)
				Assert.Equal(VerticalAlignment.Center, chip.VerticalAlignment);

			foreach (var tag in Chips(window, "aitag"))
				Assert.Equal(VerticalAlignment.Center, tag.VerticalAlignment);
		}
		finally {
			window.Close();
			HeadlessUi.Pump();
		}
	});

	/// <summary>The chips at their natural height, rather than stretched to the row's.</summary>
	[Fact]
	public Task ResultsView_GroupHeadChip_IsShorterThanTheButtonsItSitsWith() => HeadlessUi.Run(() => {
		var vm = ResultsFixture.CreatePopulatedViewModel();
		var window = HeadlessUi.Show(new DuplicateResultsView { DataContext = vm });
		HeadlessUi.Pump();

		try {
			var chip = Chips(window, "simchip").FirstOrDefault();
			Assert.True(chip != null, "the result list has no similarity chip to measure");

			var button = window.GetVisualDescendants().OfType<Button>()
				.FirstOrDefault(b => b.Classes.Contains("fileact") && b.Bounds.Height > 0);
			if (button == null)
				return;   // the group actions only appear on the row the pointer is over

			Assert.True(chip!.Bounds.Height < button.Bounds.Height,
				$"the chip ({chip.Bounds.Height}px) is stretched to the button's height ({button.Bounds.Height}px)");
		}
		finally {
			window.Close();
			HeadlessUi.Pump();
		}
	});

	static List<Border> Chips(Visual root, string cls) => root.GetVisualDescendants()
		.OfType<Border>()
		.Where(b => b.Classes.Contains(cls))
		.ToList();
}
