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
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using VDF.GUI.Data;
using VDF.GUI.ViewModels;

namespace VDF.GUI.Views {
	public partial class SetupView : UserControl {
		public SetupView() {
			AvaloniaXamlLoader.Load(this);
			// Dropping folders anywhere on the setup screen includes them;
			// holding Alt while dropping excludes them instead.
			DragDrop.SetAllowDrop(this, true);
			AddHandler(DragDrop.DropEvent, OnDrop);
			AddHandler(DragDrop.DragOverEvent, (_, e) => e.DragEffects = DragDropEffects.Copy);
		}

		MainWindowVM? ViewModel => DataContext as MainWindowVM;

		void OnDrop(object? sender, DragEventArgs e) {
			if (!e.DataTransfer.Contains(DataFormat.File)) return;
			bool exclude = e.KeyModifiers.HasFlag(KeyModifiers.Alt);
			foreach (var item in e.DataTransfer.GetItems(DataFormat.File) ?? Array.Empty<IDataTransferItem>()) {
				string? path = item.TryGetFile()?.TryGetLocalPath();
				if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) continue;
				var target = exclude ? SettingsFile.Instance.Blacklists : SettingsFile.Instance.Includes;
				if (!target.Contains(path))
					target.Add(path);
			}
		}

		// The radio inside a profile card is out of hit testing, so every click anywhere
		// on the card lands here: the card is what the pointer gets to aim at, and it
		// picks the same profile the radio's Space key does.
		void OnProfileCardPressed(object? sender, PointerPressedEventArgs e) {
			if (ViewModel is null || sender is not Border { DataContext: ScanProfileOptionVM option }) return;
			e.Handled = true;
			ViewModel.SelectScanProfileCommand.Execute(option).Subscribe();
		}
	}
}
