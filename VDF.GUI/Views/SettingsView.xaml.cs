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

using System.ComponentModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using VDF.GUI.Data;
using VDF.GUI.ViewModels;

namespace VDF.GUI.Views {
	/// <summary>
	/// The settings page (redesign stage 3): option rows with always-visible descriptions,
	/// phone-facing profiles and cross-section search. The section list is the rail's group
	/// (mockup .nav-parent + .nav-sub-item); the page shows one section at a time and is
	/// told which by <see cref="MainWindowVM.SettingsSection"/>. All filter DECISIONS live
	/// in <see cref="SettingsSearch"/>; this class only maps them onto control visibility.
	/// </summary>
	public partial class SettingsView : UserControl {

	sealed record SectionInfo(Control Panel, TextBlock? Caption, string Id, string Label);

	/// <summary>
	/// The page's sections, in the order the rail lists them (mockup .nav-sub-item). The
	/// page has no nav of its own — the rail's group is the list — so this table is where a
	/// section's id and label come from: the rail's entries write one of these ids into
	/// <see cref="MainWindowVM.SettingsSection"/> to ask for a section, and the page header
	/// and the search results show the label. The ids match the Tag on each section panel
	/// in the XAML.
	/// </summary>
	static readonly (string Id, string LangKey)[] SectionOrder = [
		("Scanning", "MainWindow.Settings.Scanning"),
		("Matching", "Settings.Nav.Matching"),
		("PartialClips", "Settings.Nav.PartialClips"),
		("Files", "Settings.Nav.FilesFilters"),
		("Database", "Settings.Sub.Results"),
		("Processing", "MainWindow.Settings.Processing"),
		("Appearance", "MainWindow.Settings.Appearance"),
	];

	readonly List<SectionInfo> sections = new();
	readonly List<TextBlock> subCaptions = new();
	readonly List<SettingsSearchSection> searchSections = new();
	readonly List<SettingsSearchRow> searchRows = new();
	// The seconds floor/cap rows of the duration group, folded behind "more".
	readonly HashSet<SettingRow> collapsedExtraRows = new();
	bool durationMoreExpanded;
	bool indexBuilt;
	string selectedSectionId = SectionOrder[0].Id;
	// A section that was asked for from outside before the index existed to select it from.
	string? pendingSectionRequest;
	MainWindowVM? vm;

		public SettingsView() {
			AvaloniaXamlLoader.Load(this);

			// The folder lists this used to wire up for drag & drop moved out with the
			// Directories section (mockup has no such section). Adding folders lives on
			// the scan page, which owns the list the scanner actually reads.

			DataContextChanged += (_, __) => HookViewModel();
			Loaded += (_, __) => {
				BuildIndex();
				UpdateVisibility();
				if (pendingSectionRequest is { } pending) {
					pendingSectionRequest = null;
					SelectSection(pending);
				}
			};
		}

		void HookViewModel() {
			if (vm != null)
				vm.PropertyChanged -= ViewModel_PropertyChanged;
			vm = DataContext as MainWindowVM;
			if (vm != null)
				vm.PropertyChanged += ViewModel_PropertyChanged;
		}

		void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e) {
			if (e.PropertyName == nameof(MainWindowVM.SettingsSearchQuery))
				UpdateVisibility();
			// A section asked for from outside ("open the settings on Appearance"): pick
			// the matching entry of the left nav. The page's own picks report through the
			// same property, which is why ShowRequestedSection ignores what is already on
			// screen instead of selecting it again.
			else if (e.PropertyName == nameof(MainWindowVM.SettingsSection))
				ShowRequestedSection(vm?.SettingsSection);
		}

		/// <summary>Selects the section that was asked for; a request that arrives before
		/// the index exists waits for Loaded, which is what builds it.</summary>
		void ShowRequestedSection(string? sectionId) {
			if (string.IsNullOrWhiteSpace(sectionId)) return;
			if (sectionId == selectedSectionId) return;
			if (!indexBuilt) {
				pendingSectionRequest = sectionId;
				return;
			}
			SelectSection(sectionId);
		}

		/// <summary>Picks a section the way the rail's entry does: drop a running
		/// search first, then let UpdateVisibility drive the cards.</summary>
		void SelectSection(string sectionId) {
			if (SectionOrder.All(s => s.Id != sectionId)) return;
			selectedSectionId = sectionId;
			if (vm != null && SettingsSearch.IsSearching(vm.SettingsSearchQuery))
				vm.SettingsSearchQuery = string.Empty;
			UpdateVisibility();
		}

		void BuildIndex() {
			if (indexBuilt) return;
			indexBuilt = true;

			collapsedExtraRows.Add(this.FindControl<SettingRow>("RowDurationMin")!);
			collapsedExtraRows.Add(this.FindControl<SettingRow>("RowDurationMax")!);

			foreach (var child in this.FindControl<StackPanel>("SectionsHost")!.Children) {
				// A section is a card (Border.setcard) holding the panel that carries the
				// tag and the rows. Hiding the outermost element hides the card with it.
				StackPanel? panel = child as StackPanel ?? (child as Border)?.Child as StackPanel;
				if (panel?.Tag is not string id) continue;
				var caption = panel.Children.OfType<TextBlock>().FirstOrDefault(t => t.Classes.Contains("sectioncaption"));
				string label = SectionOrder.FirstOrDefault(s => s.Id == id) is { LangKey: string key }
					? App.Lang[key]
					: id;
				sections.Add(new SectionInfo(child, caption, id, label));
				// The section itself is found by its own label only; rows and tagged
				// blocks carry their own text.
				searchSections.Add(new SettingsSearchSection(id, label));

				foreach (var descendant in panel.GetLogicalDescendants().OfType<Control>()) {
					if (descendant is TextBlock tb && tb.Classes.Contains("subcaption"))
						subCaptions.Add(tb);
					if (descendant is SettingRow row)
						searchRows.Add(new SettingsSearchRow(row, id, row.BuildSearchText()));
					else if (SettingsSearchMeta.GetText(descendant) is string meta)
						searchRows.Add(new SettingsSearchRow(descendant, id, BuildBlockSearchText(descendant, meta)));
				}
			}
		}

		/// <summary>A tagged block is searched by its keywords plus every static text inside it.</summary>
		static string BuildBlockSearchText(Control block, string meta) =>
			string.Join(' ', block.GetLogicalDescendants().OfType<TextBlock>()
				.Select(t => t.Text)
				.Where(t => !string.IsNullOrWhiteSpace(t))
				.Prepend(meta));

		void UpdateVisibility() {
			if (!indexBuilt) return;
			string? query = vm?.SettingsSearchQuery;
			var result = SettingsSearch.Apply(query, selectedSectionId, searchSections, searchRows);
			bool searching = result.IsSearchMode;

			foreach (var section in sections) {
				bool visible = result.VisibleSections.Contains(section.Id);
				section.Panel.IsVisible = visible;
				if (section.Caption != null)
					section.Caption.IsVisible = searching && visible;
			}

			foreach (var row in searchRows) {
				bool visible = result.VisibleRows.Contains(row.Handle);
				var control = (Control)row.Handle;
				if (control is SettingRow settingRow && collapsedExtraRows.Contains(settingRow))
					visible &= searching || durationMoreExpanded;
				control.IsVisible = visible;
			}

			// Group captions only make sense on the full section page.
			foreach (var caption in subCaptions)
				caption.IsVisible = !searching;

			// Hairline under every visible row except the last of its section (mockup).
			foreach (var group in searchRows.Where(r => r.Handle is SettingRow row && row.IsVisible)
					.GroupBy(r => r.SectionId)) {
				SettingRow? last = null;
				foreach (var entry in group) {
					var row = (SettingRow)entry.Handle;
					row.ShowSeparator = true;
					last = row;
				}
				if (last != null)
					last.ShowSeparator = false;
			}

			this.FindControl<TextBlock>("NoResultsText")!.IsVisible = searching && result.VisibleSections.Count == 0;
			// The section's name lives in the topbar, so the page only titles itself
			// during a search, which has no single section to name.
			var headerTitle = this.FindControl<TextBlock>("HeaderTitle")!;
			headerTitle.Text = searching
				? App.Lang["Settings.SearchResults"]
				: sections.FirstOrDefault(s => s.Id == selectedSectionId)?.Label;
			headerTitle.IsVisible = searching;

			// The topbar titles the page, so the page reports which section it is on. A
			// search spans every section, so during one there is no single section to name.
			// The rail reads it back to keep its own entry in step.
			if (vm != null)
				vm.SettingsSection = searching ? null : selectedSectionId;
		}

		void OnDurationMoreClick(object? sender, RoutedEventArgs e) {
			durationMoreExpanded = !durationMoreExpanded;
			this.FindControl<Button>("DurationMoreLink")!.Content =
				App.Lang[durationMoreExpanded ? "Settings.Less" : "Settings.More"];
			UpdateVisibility();
		}

		void Thumbnails_ValueChanged(object? sender, NumericUpDownValueChangedEventArgs e) {
			if (ApplicationHelpers.MainWindow != null && ApplicationHelpers.MainWindowDataContext != null)
				ApplicationHelpers.MainWindowDataContext.Thumbnails_ValueChanged(sender, e);
		}

		static void OnDragOver(object? sender, DragEventArgs e) {
			e.DragEffects &= DragDropEffects.Copy | DragDropEffects.Link;
			if (!e.DataTransfer.Contains(DataFormat.File))
				e.DragEffects = DragDropEffects.None;
		}

		static void DropFolders(DragEventArgs e, System.Collections.ObjectModel.ObservableCollection<string> target) {
			if (!e.DataTransfer.Contains(DataFormat.File)) return;
			foreach (var item in e.DataTransfer.GetItems(DataFormat.File) ?? Array.Empty<IDataTransferItem>()) {
				string? path = item.TryGetFile()?.TryGetLocalPath();
				if (!string.IsNullOrEmpty(path) && !target.Contains(path))
					target.Add(path);
			}
		}
	}
}
