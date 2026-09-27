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
				RaiseTopbarChanged();
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
				this.RaisePropertyChanged(nameof(RailToggleTooltip));
			}
		}

		public bool ShowRailLabels => !IsRailCollapsed;
		/// <summary>The rail's two widths: 216px expanded, 74px collapsed — the width
		/// the rail had before the mockup restyle, kept at the user's request.</summary>
		public GridLength RailColumnWidth => new(IsRailCollapsed ? 74 : 216);
		public string RailToggleTooltip => App.Lang[IsRailCollapsed ? "Rail.Expand" : "Rail.Collapse"];
		public ReactiveCommand<Unit, Unit> ToggleRailCommand => ReactiveCommand.Create(() => {
			IsRailCollapsed = !IsRailCollapsed;
		});

		// ---------- rail: navigation ----------

		/// <summary>"Scan setup" and "Results" both land on the scanner: which of the two
		/// screens it shows (setup or results) follows from whether a scan has produced
		/// anything, so a second entry could not point anywhere else.</summary>
		public ReactiveCommand<Unit, Unit> ShowRailScannerCommand => ReactiveCommand.Create(() => {
			ActiveShellView = ShellView.Main;
		});

		/// <summary>Lands on the settings page. The page carries the section list itself -
		/// it is the app's original settings nav, eleven sections wide and reachable from
		/// the keyboard - so the rail opens the page instead of repeating a shorter copy of
		/// that list beside it.</summary>
		public ReactiveCommand<Unit, Unit> ShowRailSettingsCommand => ReactiveCommand.Create(() => {
			ActiveShellView = ShellView.Settings;
		});

		string? _SettingsSection;
		/// <summary>The settings section on screen. The page is the writer: it is the one
		/// that knows which entry its nav has selected, and a search spanning every section
		/// clears this instead. The topbar reads it so the header names what is showing.</summary>
		public string? SettingsSection {
			get => _SettingsSection;
			set {
				this.RaiseAndSetIfChanged(ref _SettingsSection, value);
				RaiseTopbarChanged(); // the topbar names the section that is showing
			}
		}

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

		// ---------- topbar (mockup .topbar) ----------

		/// <summary>The mockup's page header: a 20px title with a quieter line under it
		/// saying what this screen is for. It follows the shell view — and, inside the
		/// scanner, the scan state — so the header never disagrees with what is shown.
		/// The settings page renames itself per section, as the mockup's setMeta does.</summary>
		public string TopbarTitle => ActiveShellView switch {
			ShellView.Settings => App.Lang[$"Topbar.Settings.{SettingsSectionKey}"],
			ShellView.Log => App.Lang["Topbar.Log"],
			_ => IsScanningState ? App.Lang["Topbar.Scanning"]
				: IsReviewState ? App.Lang["Topbar.Results"]
				: App.Lang["Topbar.Setup"],
		};

		public string TopbarSub => ActiveShellView switch {
			ShellView.Settings => App.Lang[$"Topbar.Settings.{SettingsSectionKey}.Sub"],
			ShellView.Log => App.Lang["Topbar.Log.Sub"],
			_ => IsScanningState ? App.Lang["Topbar.Scanning.Sub"]
				: IsReviewState ? App.Lang["Topbar.Results.Sub"]
				: App.Lang["Topbar.Setup.Sub"],
		};

		/// <summary>Which settings section the topbar names. During a search no single
		/// section is showing, so it falls back to the page's own generic title.</summary>
		string SettingsSectionKey => SettingsSection switch {
			"Scanning" => "Scanning",
			"Matching" => "Matching",
			"PartialClips" => "PartialClips",
			"Files" => "Files",
			"Database" => "Database",
			"Processing" => "Processing",
			"Appearance" => "Appearance",
			_ => "Root",
		};

		void RaiseTopbarChanged() {
			this.RaisePropertyChanged(nameof(TopbarTitle));
			this.RaisePropertyChanged(nameof(TopbarSub));
		}
	}
}
