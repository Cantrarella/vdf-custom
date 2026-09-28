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

using ActiproSoftware.UI.Avalonia.Media;
using ActiproSoftware.UI.Avalonia.Themes.Generation;

namespace VDF.GUI.Utils {
	/// <summary>
	/// The default ActiPro color palette plus VDF's accent ramp. The ramp is generated
	/// from a midtone, so the dark theme lands on the lighter accent and the light theme
	/// on its darker counterpart.
	///
	/// Under the final colour scheme the midtone is the document's Primary (#5AA9E6). It
	/// is the same weight the ramp used to be keyed on: the previous midtone (#4A97CD) sat
	/// at the geometric middle of its pale surface (#CDE9FF) and its accent ink (#17587F),
	/// and #5AA9E6 sits at the geometric middle of this palette's pair — Primary Soft
	/// (#EAF5FF) and Primary Text (#2474A6) — within a couple of percent of luminance.
	/// Everything the theme paints from the accent ramp (the checked tick, the switch, the
	/// progress bar, focus rings) therefore moves with the palette instead of staying on
	/// the old blue.
	/// </summary>
	public sealed class VdfColorPaletteFactory : DefaultColorPaletteFactory {
		public const string AccentRampName = "VdfAccent";

		public override ColorPalette Create() {
			ColorPalette palette = base.Create();
			palette.Ramps.Add(CreateColorRamp(AccentRampName, isNeutral: false, UIColor.Parse("#5AA9E6")));
			return palette;
		}
	}
}
