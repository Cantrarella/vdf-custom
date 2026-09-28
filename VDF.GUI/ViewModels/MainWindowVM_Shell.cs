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

		/// <summary>The rail has an entry per scanner screen, so the highlight has to say
		/// which of the two is showing - both are lit by "the scanner is showing" alone.</summary>
		public bool IsRailScanSetupActive => IsShellMainVisible && IsSetupState;
		public bool IsRailResultsActive => IsShellMainVisible && IsReviewState;

		void RaiseShellNavChanged() {
			this.RaisePropertyChanged(nameof(ShowNavNewScan));
			this.RaisePropertyChanged(nameof(ShowNavBackToResults));
			this.RaisePropertyChanged(nameof(ShowNavLog));
			this.RaisePropertyChanged(nameof(ShowNavSettings));
			// Also raised by the scanner state: which of its two screens is up is half of
			// what the rail's highlight depends on.
			this.RaisePropertyChanged(nameof(IsRailScanSetupActive));
			this.RaisePropertyChanged(nameof(IsRailResultsActive));
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
				this.RaisePropertyChanged(nameof(ShowRailCountBadge));
				this.RaisePropertyChanged(nameof(RailToggleTooltip));
				// The settings children fall away with the labels: they are labels too,
				// and left behind they would sit in the icon strip as a column of dots.
				this.RaisePropertyChanged(nameof(ShowRailSettingsChildren));
			}
		}

		public bool ShowRailLabels => !IsRailCollapsed;
		/// <summary>The group counter next to "查重结果". Collapsed, the rail shows icons
		/// only — the mockup hides the count with the labels (.nav-item .dot) — so it
		/// doubles as the "is there anything to count" test while expanded.</summary>
		public bool ShowRailCountBadge => ShowRailLabels && Duplicates.Count > 0;
		/// <summary>The rail's two widths: 216px expanded, 74px collapsed — the width
		/// the rail had before the mockup restyle, kept at the user's request.</summary>
		public GridLength RailColumnWidth => new(IsRailCollapsed ? 74 : 216);
		public string RailToggleTooltip => App.Lang[IsRailCollapsed ? "Rail.Expand" : "Rail.Collapse"];
		public ReactiveCommand<Unit, Unit> ToggleRailCommand => ReactiveCommand.Create(() => {
			IsRailCollapsed = !IsRailCollapsed;
		});

		// ---------- rail: navigation ----------

		/// <summary>The rail entry named after the scan folders: it opens the folder
		/// picker straight away, on top of the scanner screen. Landing on the scanner
		/// alone was invisible whenever the scanner was already showing, which made the
		/// entry read as a dead link.</summary>
		public ReactiveCommand<Unit, Unit> ShowRailScannerCommand => ReactiveCommand.CreateFromTask(async () => {
			ActiveShellView = ShellView.Main;
			await AddIncludesToScanListAsync();
		});

		/// <summary>Which scanner screen the rail last asked for: null while the data
		/// decides (no results means the setup page), true/false once the rail named one.
		/// Without this the rail's two scanner entries both landed on whichever screen the
		/// data happened to favour, which made them read as duplicates of each other.</summary>
		bool? railScannerScreenIsSetup;

		/// <summary>"Scan setup": the folder list and the scan options, whatever the last
		/// scan left behind.</summary>
		public ReactiveCommand<Unit, Unit> ShowRailScanSetupCommand => ReactiveCommand.Create(() => {
			railScannerScreenIsSetup = true;
			ActiveShellView = ShellView.Main;
			RaiseScannerStateChanged();
		});

		/// <summary>"Results": the groups the last scan found. With nothing to show there
		/// is no results screen to land on, so the setup page stays.</summary>
		public ReactiveCommand<Unit, Unit> ShowRailResultsCommand => ReactiveCommand.Create(() => {
			railScannerScreenIsSetup = false;
			ActiveShellView = ShellView.Main;
			RaiseScannerStateChanged();
		});

		/// <summary>A scan starts: the rail's pick is dropped so the results of that scan
		/// come up on their own when it finishes.</summary>
		internal void ClearRailScannerScreen() => railScannerScreenIsSetup = null;

		// ---------- rail: settings group ----------

		bool _IsRailSettingsExpanded;
		/// <summary>The settings group's fold state (mockup .nav-parent + .nav-sub).</summary>
		public bool IsRailSettingsExpanded {
			get => _IsRailSettingsExpanded;
			set {
				this.RaiseAndSetIfChanged(ref _IsRailSettingsExpanded, value);
				this.RaisePropertyChanged(nameof(ShowRailSettingsChildren));
			}
		}

		public bool ShowRailSettingsChildren => IsRailSettingsExpanded && !IsRailCollapsed;

		/// <summary>The mockup's .nav-parent: from a collapsed rail or from another page it
		/// opens the group and lands on the settings page; on the settings page itself it
		/// is the fold/unfold toggle it looks like.</summary>
		public ReactiveCommand<Unit, Unit> ToggleRailSettingsCommand => ReactiveCommand.Create(() => {
			bool wasCollapsed = IsRailCollapsed;
			IsRailCollapsed = false;
			bool onSettings = ActiveShellView == ShellView.Settings;
			IsRailSettingsExpanded = wasCollapsed || !onSettings || !IsRailSettingsExpanded;
			if (!onSettings)
				ActiveShellView = ShellView.Settings;
		});

		/// <summary>A section of the settings page, picked from the rail. The page is the
		/// one that knows how to show a section, so this only names it; the page listens
		/// on <see cref="SettingsSection"/> and switches.</summary>
		public ReactiveCommand<string, Unit> ShowSettingsSectionCommand => ReactiveCommand.Create<string>(section => {
			ActiveShellView = ShellView.Settings;
			SettingsSection = section;
		});

		string? _SettingsSection;
		/// <summary>The settings section on screen. The page is the usual writer: it is the
		/// one that knows which section it has selected, and a search spanning every section
		/// clears this instead. The rail writes it too, to name the section it wants. The
		/// topbar reads it so the header names what is showing.</summary>
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
