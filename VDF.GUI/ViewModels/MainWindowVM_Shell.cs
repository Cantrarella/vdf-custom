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
using System.Reactive;
using Avalonia.Controls;
using ReactiveUI;
using VDF.GUI.Data;

namespace VDF.GUI.ViewModels {
	// One-window shell (redesign stage 6): the tab strip is replaced by titlebar nav
	// links; Settings and Log are secondary views layered over the scanner. Stage 8
	// turns the rail into the mockup's grouped sidebar: section headers, a results
	// badge, collapsible settings children, a theme segment, and collapsing.
	public partial class MainWindowVM {

		ShellView _ActiveShellView = ShellView.Main;
		public ShellView ActiveShellView {
			get => _ActiveShellView;
			set {
				if (value == _ActiveShellView) return;
				this.RaiseAndSetIfChanged(ref _ActiveShellView, value);
				this.RaisePropertyChanged(nameof(IsShellMainVisible));
				this.RaisePropertyChanged(nameof(IsShellSettingsVisible));
				this.RaisePropertyChanged(nameof(IsShellLogVisible));
				RaiseShellNavChanged();
				// The settings page owns the same theme choice; returning to the rail has
				// to show whatever it changed while we were away.
				RaiseThemeChanged();
			}
		}

		public bool IsShellMainVisible => ActiveShellView == ShellView.Main;
		public bool IsShellSettingsVisible => ActiveShellView == ShellView.Settings;
		public bool IsShellLogVisible => ActiveShellView == ShellView.Log;

		ShellNavLinks NavLinks => ShellNav.For(ActiveShellView, IsReviewState);
		public bool ShowNavNewScan => NavLinks.NewScan;
		public bool ShowNavBackToResults => NavLinks.BackToResults;
		public bool ShowNavLog => NavLinks.Log;
		public bool ShowNavSettings => NavLinks.Settings;

		void RaiseShellNavChanged() {
			this.RaisePropertyChanged(nameof(ShowNavNewScan));
			this.RaisePropertyChanged(nameof(ShowNavBackToResults));
			this.RaisePropertyChanged(nameof(ShowNavLog));
			this.RaisePropertyChanged(nameof(ShowNavSettings));
		}

		public ReactiveCommand<string, Unit> ShowShellViewCommand => ReactiveCommand.Create<string>(view => {
			ActiveShellView = Enum.Parse<ShellView>(view);
		});

		// ---------- rail: collapsing ----------

		bool _IsRailCollapsed;
		/// <summary>Icons only while collapsed. The rail never leaves the screen - it is
		/// the way back - so the column narrows rather than disappearing.</summary>
		public bool IsRailCollapsed {
			get => _IsRailCollapsed;
			set {
				this.RaiseAndSetIfChanged(ref _IsRailCollapsed, value);
				this.RaisePropertyChanged(nameof(RailColumnWidth));
				this.RaisePropertyChanged(nameof(ShowRailLabels));
				this.RaisePropertyChanged(nameof(ShowRailSettingsChildren));
				this.RaisePropertyChanged(nameof(RailToggleTooltip));
			}
		}

		public bool ShowRailLabels => !IsRailCollapsed;
		public GridLength RailColumnWidth => new(IsRailCollapsed ? 62 : 216);
		public string RailToggleTooltip => App.Lang[IsRailCollapsed ? "Rail.Expand" : "Rail.Collapse"];
		public ReactiveCommand<Unit, Unit> ToggleRailCommand => ReactiveCommand.Create(() => {
			IsRailCollapsed = !IsRailCollapsed;
		});

		// ---------- rail: settings children ----------

		bool _IsRailSettingsExpanded;
		public bool IsRailSettingsExpanded {
			get => _IsRailSettingsExpanded;
			set {
				this.RaiseAndSetIfChanged(ref _IsRailSettingsExpanded, value);
				this.RaisePropertyChanged(nameof(ShowRailSettingsChildren));
			}
		}

		public bool ShowRailSettingsChildren => IsRailSettingsExpanded && !IsRailCollapsed;

		/// <summary>Opens the section list and lands on the settings view; picking the
		/// entry again folds the list away instead.</summary>
		public ReactiveCommand<Unit, Unit> ToggleRailSettingsCommand => ReactiveCommand.Create(() => {
			IsRailSettingsExpanded = !IsRailSettingsExpanded;
			if (IsRailSettingsExpanded)
				ActiveShellView = ShellView.Settings;
		});

		// ---------- rail: navigation ----------

		/// <summary>"Scan setup" and "Results" both land on the scanner: which of the two
		/// screens it shows (setup or results) follows from whether a scan has produced
		/// anything, so a second entry could not point anywhere else.</summary>
		public ReactiveCommand<Unit, Unit> ShowRailScannerCommand => ReactiveCommand.Create(() => {
			ActiveShellView = ShellView.Main;
		});

		string? _RequestedSettingsSection;
		/// <summary>The section the rail asked the settings view to show. The view listens
		/// and picks the matching entry of its own left nav. Re-picking the entry that is
		/// already showing clears the value first, so the change is raised every time.</summary>
		public string? RequestedSettingsSection {
			get => _RequestedSettingsSection;
			set => this.RaiseAndSetIfChanged(ref _RequestedSettingsSection, value);
		}

		public ReactiveCommand<string, Unit> ShowSettingsSectionCommand => ReactiveCommand.Create<string>(section => {
			ActiveShellView = ShellView.Settings;
			if (RequestedSettingsSection == section)
				RequestedSettingsSection = null;
			RequestedSettingsSection = section;
		});

		// ---------- rail: theme segment (mockup .seg) ----------

		public bool IsLightTheme => SettingsFile.Instance.ThemeMode == ThemeMode.Light;
		public bool IsDarkTheme => SettingsFile.Instance.ThemeMode == ThemeMode.Dark;

		/// <summary>Sets light or dark outright. "Follow the system" stays available on the
		/// settings page; the rail offers the pair the mockup shows, and neither lights up
		/// while the choice is to follow the system.</summary>
		public ReactiveCommand<string, Unit> SetThemeModeCommand => ReactiveCommand.Create<string>(mode => {
			if (!Enum.TryParse<ThemeMode>(mode, out var parsed) || SettingsFile.Instance.ThemeMode == parsed)
				return;
			SettingsFile.Instance.ThemeMode = parsed; // Appearance listens and re-applies
			RaiseThemeChanged();
		});

		void RaiseThemeChanged() {
			this.RaisePropertyChanged(nameof(IsLightTheme));
			this.RaisePropertyChanged(nameof(IsDarkTheme));
			this.RaisePropertyChanged(nameof(SelectedThemeModeOption));
		}
	}
}
