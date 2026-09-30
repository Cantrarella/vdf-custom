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

using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using Avalonia.Threading;
using ReactiveUI;
using VDF.Core;
using VDF.Core.Utils;
using VDF.GUI.Data;
using VDF.GUI.Utils;
using VDF.GUI.Views;

namespace VDF.GUI.ViewModels {

	/// <summary>One folder row on the Setup screen (included or excluded).</summary>
	public sealed class SetupFolderVM : ReactiveObject {
		public SetupFolderVM(string path, bool isExcluded) {
			Path = path;
			IsExcluded = isExcluded;
			IsNetwork = FolderCountingService.IsNetworkPath(path);
			string? root = null;
			try { root = System.IO.Path.GetPathRoot(path); } catch (Exception) { }
			DriveLabel = IsNetwork ? "＼＼" : string.IsNullOrEmpty(root) ? "＊" : root.TrimEnd('\\', '/');
		}

		public string Path { get; }
		public bool IsExcluded { get; }
		public bool IsNetwork { get; }
		/// <summary>Drive chip: "D:", "＼＼" for UNC, "＊" for patterns.</summary>
		public string DriveLabel { get; }

		string _MetaText = string.Empty;
		public string MetaText {
			get => _MetaText;
			set => this.RaiseAndSetIfChanged(ref _MetaText, value);
		}
		bool _ShowCountNow;
		/// <summary>Network locations are not walked automatically — opt-in link.</summary>
		public bool ShowCountNow {
			get => _ShowCountNow;
			set => this.RaiseAndSetIfChanged(ref _ShowCountNow, value);
		}
		bool _IsCounting;
		public bool IsCounting {
			get => _IsCounting;
			set => this.RaiseAndSetIfChanged(ref _IsCounting, value);
		}
		/// <summary>Instant count from the fingerprint database, before any folder walk.</summary>
		internal int? DbKnownCount;

		/// <summary>Result of the last completed walk; drives the Setup screen's estimate.</summary>
		long? _FileCount;
		public long? FileCount {
			get => _FileCount;
			set => this.RaiseAndSetIfChanged(ref _FileCount, value);
		}
		long? _TotalBytes;
		public long? TotalBytes {
			get => _TotalBytes;
			set => this.RaiseAndSetIfChanged(ref _TotalBytes, value);
		}
		/// <summary>Switched off on the Setup screen: kept in the list, left out of the scan.</summary>
		bool _IsDisabled;
		public bool IsDisabled {
			get => _IsDisabled;
			set => this.RaiseAndSetIfChanged(ref _IsDisabled, value);
		}
	}

	public sealed class ScanProfileOptionVM : ReactiveObject {
		public ScanProfileOptionVM(ScanProfile value, string name, string description, string timeHint) {
			Value = value;
			Name = name;
			Description = description;
			TimeHint = timeHint;
		}
		public ScanProfile Value { get; }
		public string Name { get; }
		public string Description { get; }
		public string TimeHint { get; }
		bool _IsActive;
		public bool IsActive {
			get => _IsActive;
			set => this.RaiseAndSetIfChanged(ref _IsActive, value);
		}
	}

	// Setup + Scanning window states (redesign stage 2). Review state is the results view.
	public partial class MainWindowVM : ReactiveObject {

		// ---------- state switching ----------
		// The scanner has two screens and normally the data picks between them: no results
		// means the setup page. The rail has an entry per screen, and an entry that the
		// data overrules is an entry that appears to do nothing, so a pick from the rail
		// wins until the next scan starts (which drops it again, see IsScanning).
		public bool IsSetupState => !IsScanning && (Duplicates.Count == 0 || railScannerScreenIsSetup == true);
		public bool IsScanningState => IsScanning;
		public bool IsReviewState => !IsScanning && Duplicates.Count > 0 && railScannerScreenIsSetup != true;

		void RaiseScannerStateChanged() {
			this.RaisePropertyChanged(nameof(IsSetupState));
			this.RaisePropertyChanged(nameof(IsScanningState));
			this.RaisePropertyChanged(nameof(IsReviewState));
			RaiseShellNavChanged(); // "New scan" nav link follows the Review state
			RaiseTopbarChanged(); // the header names the screen the scanner moved to
		}

		/// <summary>
		/// Titlebar "New scan": discards the current results and returns to the Setup
		/// screen so folders/profile can be changed first — it does NOT start the scan
		/// (the Setup screen's own Scan button does). Quick re-runs with unchanged
		/// settings live in the ⋯ menu (Rescan). The saved-results backup file on disk
		/// is left untouched.
		/// </summary>
		public ReactiveCommand<Unit, Unit> NewScanCommand => ReactiveCommand.CreateFromTask(async () => {
			if (Duplicates.Count > 0 &&
				await MessageBoxService.Show(App.Lang["Message.NewScanDiscardPrompt"],
					MessageBoxButtons.Yes | MessageBoxButtons.No) != MessageBoxButtons.Yes)
				return;
			DiscardResultsToSetup();
		});

		internal void DiscardResultsToSetup() {
			Duplicates.Clear(); // Reset event → checked counters/undo stack clear + state raise
			ShowNoDuplicatesNotice = false;
			BuildActiveResultsView();
			RefreshGroupStats();
			// The fingerprint database may have grown since these results were produced.
			RebuildSetupFolders();
		}

		// ---------- welcome strip ----------
		public ReactiveCommand<Unit, Unit> DismissWelcomeStripCommand => ReactiveCommand.Create(() => {
			SettingsFile.Instance.WelcomeStripDismissed = true;
		});

		// ---------- "no duplicates found" notice ----------
		bool _ShowNoDuplicatesNotice;
		/// <summary>
		/// Raised after a completed scan that found nothing, so the Setup screen a returning
		/// user lands on can be told apart from the never-scanned state. Persists until the
		/// next scan starts (see <see cref="StartScanCommand"/>) or the user dismisses it.
		/// </summary>
		public bool ShowNoDuplicatesNotice {
			get => _ShowNoDuplicatesNotice;
			set => this.RaiseAndSetIfChanged(ref _ShowNoDuplicatesNotice, value);
		}

		public ReactiveCommand<Unit, Unit> DismissNoDuplicatesNoticeCommand => ReactiveCommand.Create(() => {
			ShowNoDuplicatesNotice = false;
		});

		// ---------- folder list ----------
		public ObservableCollection<SetupFolderVM> SetupFolders { get; } = new();
		readonly FolderCountingService folderCounting = new();
		// Completed walks per folder; survives list rebuilds within the session.
		readonly Dictionary<string, FolderCountProgress> folderCountCache = new(StringComparer.OrdinalIgnoreCase);

		string _SetupFootnote = string.Empty;
		public string SetupFootnote {
			get => _SetupFootnote;
			set => this.RaiseAndSetIfChanged(ref _SetupFootnote, value);
		}
		string _DatabaseInfoText = string.Empty;
		public string DatabaseInfoText {
			get => _DatabaseInfoText;
			set => this.RaiseAndSetIfChanged(ref _DatabaseInfoText, value);
		}

	internal void RebuildSetupFolders() {
		SetupFolders.Clear();
		foreach (var path in SettingsFile.Instance.Includes)
			SetupFolders.Add(new SetupFolderVM(path, isExcluded: false) {
				IsDisabled = SettingsFile.Instance.DisabledIncludes.Contains(path)
			});
		foreach (var path in SettingsFile.Instance.Blacklists)
			SetupFolders.Add(new SetupFolderVM(path, isExcluded: true) {
				MetaText = App.Lang["Setup.Excluded"]
			});
		RefreshSetupEstimate();

			SetupFootnote = string.Format(App.Lang["Setup.LocationsFootnote"], SettingsFile.Instance.Includes.Count);

			foreach (var folder in SetupFolders.Where(f => !f.IsExcluded))
				StartFolderStats(folder);

			RefreshDatabaseInfo();
		}

		void RefreshDatabaseInfo() {
			Task.Run(() => ScanEngine.DatabaseEntryCount).ContinueWith(t =>
				Dispatcher.UIThread.Post(() => {
					if (t.IsCompletedSuccessfully)
						DatabaseInfoText = string.Format(App.Lang["Setup.DatabaseInfo"], t.Result.ToString("N0"));
				}));
		}

		void StartFolderStats(SetupFolderVM folder) {
			if (folderCountCache.TryGetValue(folder.Path, out var cached)) {
				ApplyFinalCount(folder, cached);
				return;
			}

			// DB-known count is instant and never blocks anything.
			Task.Run(() => ScanEngine.CountDatabaseEntriesUnder(folder.Path)).ContinueWith(t =>
				Dispatcher.UIThread.Post(() => {
					if (!t.IsCompletedSuccessfully) return;
					folder.DbKnownCount = t.Result;
					// Only fill the meta line while nothing better is known yet.
					if (!folder.IsCounting && string.IsNullOrEmpty(folder.MetaText) && t.Result > 0)
						folder.MetaText = string.Format(App.Lang["Setup.MetaKnown"], t.Result.ToString("N0"));
				}));

			if (folder.IsNetwork) {
				folder.MetaText = App.Lang["Setup.NetworkNotCounted"];
				folder.ShowCountNow = true;
				return;
			}
			BeginFolderWalk(folder);
		}

		void BeginFolderWalk(SetupFolderVM folder) {
			folder.ShowCountNow = false;
			folder.IsCounting = true;
			folder.MetaText = string.Format(App.Lang["Setup.CountingSoFar"], 0);
			bool started = folderCounting.StartCounting(folder.Path, progress =>
				Dispatcher.UIThread.Post(() => {
					if (!progress.Completed) {
						folder.MetaText = string.Format(App.Lang["Setup.CountingSoFar"], progress.FileCount.ToString("N0"));
						return;
					}
					folderCountCache[folder.Path] = progress;
					// The rebuilt list may hold a NEW VM for this path by now.
					var target = SetupFolders.FirstOrDefault(f =>
						string.Equals(f.Path, folder.Path, StringComparison.OrdinalIgnoreCase)) ?? folder;
					target.IsCounting = false;
					ApplyFinalCount(target, progress);
				}));
			if (!started)
				folder.IsCounting = folderCounting.IsCounting(folder.Path);
		}

	void ApplyFinalCount(SetupFolderVM folder, FolderCountProgress result) {
		if (result.Failed) {
			folder.MetaText = App.Lang["Setup.CountFailed"];
			return;
		}
		folder.FileCount = result.FileCount;
		folder.TotalBytes = result.TotalBytes;
		string text = string.Format(App.Lang["Setup.MetaCounted"],
				result.FileCount.ToString("N0"), result.TotalBytes.BytesToString());
			if (folder.DbKnownCount is int known && result.FileCount - known > 0)
			text += " · " + string.Format(App.Lang["Setup.MetaNotScanned"], (result.FileCount - known).ToString("N0"));
		folder.MetaText = text;
		RefreshSetupEstimate();
	}

	/// <summary>
	/// The mockup's 匹配强度 slider. Percent is a float on the settings file, which a
	/// Slider cannot bind to directly, so this proxies it as a double.
	/// </summary>
	public double SimilarityThreshold {
		get => SettingsFile.Instance.Percent;
		set {
			float rounded = (float)Math.Round(value, 1);
			if (SettingsFile.Instance.Percent == rounded) return;
			SettingsFile.Instance.Percent = rounded;
			this.RaisePropertyChanged();
		}
	}

	// ---------- estimate line on the scan bar (mockup .scanbar) ----------
	string _SetupEstimatedFilesText = string.Empty;
	/// <summary>"预计扫描 12,480 个文件", or a counting placeholder while walks run.</summary>
	public string SetupEstimatedFilesText {
		get => _SetupEstimatedFilesText;
		set => this.RaiseAndSetIfChanged(ref _SetupEstimatedFilesText, value);
	}
	string _SetupEstimatedHintText = string.Empty;
	/// <summary>"上次同样规模用时 4 分 12 秒（有缓存会更快）".</summary>
	public string SetupEstimatedHintText {
		get => _SetupEstimatedHintText;
		set => this.RaiseAndSetIfChanged(ref _SetupEstimatedHintText, value);
	}

	/// <summary>
	/// Sums the folders that will actually take part in the scan. Folders still being
	/// walked contribute nothing yet, so the number climbs while it settles instead of
	/// starting at a wrong zero.
	/// </summary>
	internal void RefreshSetupEstimate() {
		long files = 0;
		int known = 0, due = 0;
		foreach (var folder in SetupFolders.Where(f => !f.IsExcluded && !f.IsDisabled)) {
			due++;
			if (folder.FileCount is long counted) {
				files += counted;
				known++;
			}
			else if (folder.DbKnownCount is int dbCount && dbCount > 0)
				files += dbCount; // fingerprint database: a floor, not the whole story
		}

		if (due == 0 || files == 0)
			SetupEstimatedFilesText = App.Lang["Setup.Estimate.Counting"];
		else
			SetupEstimatedFilesText = string.Format(App.Lang["Setup.Estimate.Files"], files.ToString("N0"));

		double? seconds = SettingsFile.Instance.LastScanDurationSeconds;
		if (seconds is double s && s >= 1)
			SetupEstimatedHintText = string.Format(App.Lang["Setup.Estimate.Hint"], FormatDuration(s));
		else
			SetupEstimatedHintText = App.Lang["Setup.Estimate.HintFirst"];
	}

	/// <summary>Chinese reads "4 分 12 秒"; the shared TimeSpan.Format() prints "4m, 12s".</summary>
	static string FormatDuration(double seconds) {
		TimeSpan t = TimeSpan.FromSeconds(seconds);
		return t.TotalHours >= 1
			? string.Format(App.Lang["Setup.Estimate.Hours"], (int)t.TotalHours, t.Minutes)
			: t.TotalMinutes >= 1
				? string.Format(App.Lang["Setup.Estimate.Minutes"], (int)t.TotalMinutes, t.Seconds)
				: string.Format(App.Lang["Setup.Estimate.Seconds"], t.Seconds);
	}

		public ReactiveCommand<SetupFolderVM, Unit> CountFolderNowCommand => ReactiveCommand.Create<SetupFolderVM>(folder => {
			if (folder != null && !folder.IsCounting)
				BeginFolderWalk(folder);
		});

		public ReactiveCommand<SetupFolderVM, Unit> RemoveSetupFolderCommand => ReactiveCommand.Create<SetupFolderVM>(folder => {
			if (folder == null) return;
			if (folder.IsExcluded)
				SettingsFile.Instance.Blacklists.Remove(folder.Path);
		else {
			folderCounting.Cancel(folder.Path);
			SettingsFile.Instance.Includes.Remove(folder.Path);
			SettingsFile.Instance.DisabledIncludes.Remove(folder.Path);
		}
		RefreshSetupEstimate();
	});

	/// <summary>
	/// Mockup .dir-row "停用": the folder stays in the list and out of the next scan.
	/// Excluded (blacklisted) folders have no on/off state of their own.
	/// </summary>
	public ReactiveCommand<SetupFolderVM, Unit> ToggleSetupFolderCommand => ReactiveCommand.Create<SetupFolderVM>(folder => {
		if (folder == null || folder.IsExcluded) return;
		bool nowDisabled = !folder.IsDisabled;
		var disabled = SettingsFile.Instance.DisabledIncludes;
		if (nowDisabled) {
			folderCounting.Cancel(folder.Path); // no point walking what will not be scanned
			if (!disabled.Contains(folder.Path)) disabled.Add(folder.Path);
		}
		else
			disabled.Remove(folder.Path);
		folder.IsDisabled = nowDisabled;
		RefreshSetupEstimate();
	});

		// ---------- scan profiles ----------
		public ScanProfileOptionVM[] ScanProfileOptions { get; } = {
			new(ScanProfile.ExactAndNear, App.Lang["Profile.Exact.Name"], App.Lang["Profile.Exact.Desc"], App.Lang["Profile.Exact.Time"]),
			new(ScanProfile.EditedAndAltered, App.Lang["Profile.Edited.Name"], App.Lang["Profile.Edited.Desc"], App.Lang["Profile.Edited.Time"]),
			new(ScanProfile.AiScan, App.Lang["Profile.Ai.Name"], App.Lang["Profile.Ai.Desc"], App.Lang["Profile.Ai.Time"]),
			new(ScanProfile.DeepClean, App.Lang["Profile.Deep.Name"], App.Lang["Profile.Deep.Desc"], App.Lang["Profile.Deep.Time"]),
			new(ScanProfile.Custom, App.Lang["Profile.Custom.Name"], App.Lang["Profile.Custom.Desc"], string.Empty),
		};

	internal void RefreshScanProfileSelection() {
		var active = ScanProfileMapper.Detect(SettingsFile.Instance);
		foreach (var option in ScanProfileOptions)
			option.IsActive = option.Value == active;
		ActiveScanProfileIsManaged = active != ScanProfile.Custom;
		ActiveScanProfileName = ScanProfileOptions.First(o => o.Value == active).Name;
	}

		bool _ActiveScanProfileIsManaged;
		/// <summary>True while the managed knobs match a profile bundle — drives the
		/// settings page banner (managed notice vs. the Custom auto-switch notice).</summary>
		public bool ActiveScanProfileIsManaged {
			get => _ActiveScanProfileIsManaged;
			set => this.RaiseAndSetIfChanged(ref _ActiveScanProfileIsManaged, value);
		}
		string _ActiveScanProfileName = string.Empty;
		public string ActiveScanProfileName {
			get => _ActiveScanProfileName;
			set => this.RaiseAndSetIfChanged(ref _ActiveScanProfileName, value);
		}

		public ReactiveCommand<ScanProfileOptionVM, Unit> SelectScanProfileCommand => ReactiveCommand.Create<ScanProfileOptionVM>(option => {
			if (option == null) return;
			ScanProfileMapper.Apply(option.Value, SettingsFile.Instance);
			RefreshScanProfileSelection();
		});

		// ---------- scanning state ----------
		string _ScanStageText = string.Empty;
		/// <summary>Localizable-free stage chip text, e.g. "Scanning files 27/819".</summary>
		public string ScanStageText {
			get => _ScanStageText;
			set => this.RaiseAndSetIfChanged(ref _ScanStageText, value);
		}
		string _ScanCurrentFile = string.Empty;
		public string ScanCurrentFile {
			get => _ScanCurrentFile;
			set => this.RaiseAndSetIfChanged(ref _ScanCurrentFile, value);
		}

		/// <summary>Last few log lines, shown under the scan card.</summary>
		public ObservableCollection<LogTailRow> LogTail { get; } = new();
		internal const int LogTailLength = 4;

		string _ScanNotice = string.Empty;
		/// <summary>
		/// Why the last scan came back with nothing in it, when the filters threw the
		/// files away rather than there being no duplicates. Empty the rest of the time,
		/// so nothing about the normal case gets a line of its own.
		/// </summary>
		public string ScanNotice {
			get => _ScanNotice;
			set => this.RaiseAndSetIfChanged(ref _ScanNotice, value);
		}
		internal void AppendLogTail(LogTailRow row) {
			LogTail.Add(row);
			while (LogTail.Count > LogTailLength)
				LogTail.RemoveAt(0);
		}
	}
}
