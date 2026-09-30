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
using VDF.Core.ViewModels;
using VDF.GUI.ViewModels;
using VDF.GUI.Views;

namespace VDF.GUI.HeadlessTests;

/// <summary>
/// Every dialog window the app opens, built the same way for any test that has to look
/// at all of them - the contrast guard and the window-chrome guard both need this list,
/// and a dialog that is added to one and forgotten in the other is a hole in both.
/// </summary>
internal static class DialogCatalog {
	internal static readonly Dictionary<string, Func<Window>> Dialogs = new() {
		["MessageBox"] = () => new MessageBoxView("Delete 3 files?",
			Data.MessageBoxButtons.Yes | Data.MessageBoxButtons.No | Data.MessageBoxButtons.Cancel,
			"Confirm", Data.MessageBoxButtons.No),
		["InputBox"] = () => new InputBoxView("New name:", "clip.mp4", "file name"),
		["About"] = () => new AboutWindow(),
		["ChooseAlgorithm"] = () => new ChooseAlgoView(),
		["BlacklistManager"] = () => new BlacklistManagerView(),
		["CustomSelection"] = () => new CustomSelectionView(string.Empty),
		["ExpressionBuilder"] = () => new ExpressionBuilder(),
		["QualityOrder"] = () => new QualityOrderDialog(),
		["RelocateFiles"] = () => new RelocateFilesDialog(),
		["DatabaseEditor"] = () => new DatabaseViewer(),
		["Comparer"] = () => {
			var group = Guid.NewGuid();
			// No thumbnail timestamps: loading yields nothing without ever starting FFmpeg.
			var items = new[] { "beach_2019_final.mp4", "beach_2019_final (1).mp4" }
				.Select(name => new LargeThumbnailDuplicateItem(new DuplicateItemVM(new DuplicateItem {
					Path = $@"Z:\does\not\exist\{name}", GroupId = group, Similarity = 98.4f,
					SizeLong = 700_000_000, FrameSize = "1280x720", Duration = TimeSpan.FromSeconds(754),
				})))
				.ToList();
			return new ThumbnailComparer(items);
		},
		["MetadataCompare"] = () => MetadataCompareSample.Window(),
	};
}
