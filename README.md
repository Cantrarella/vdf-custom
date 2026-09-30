# Video Duplicate Finder
Video Duplicate Finder is a cross-platform software to find duplicated video (and image) files on hard disk based on similarity. Unlike other duplicate finders this one also finds duplicates which have a different resolution, frame rate and even watermarked.

# Features
- Cross-platform
- Fast scanning speed
- Ultra fast rescan
- Optional calling ffmpeg functions natively for even more speed
- Finds duplicate videos / images based on similarity (optional scan against pHash at zero cost)
- Partial clip detection — finds when a shorter video is a partial clip of a longer one (audio fingerprinting)
- Optional AI matching — neural image embeddings find cropped, mirrored, zoomed and heavily edited copies the classic methods miss, and locate trimmed clips inside longer recordings without needing audio. Runs 100% locally.
- Desktop GUI (Windows, Linux, macOS)

# Partial Clip Detection

VDF can detect when a shorter video is a partial clip of a longer one — for example, a scene ripped from a movie, or a clip saved from a longer recording. Candidates are found by audio fingerprinting, so it catches clips the normal visual scan misses; by default each audio match is then visually confirmed by comparing frames at the matched offset.

It runs as an **optional second phase** after the normal visual duplicate scan, using an audio fingerprinting pipeline (Chromaprint-style chroma extraction + sliding-window Hamming similarity matching). Matched pairs appear in the duplicate list with a **Clip Offset** column showing where in the source the clip starts.

### Enabling it

In **Settings → Partial Clip Detection**, check **Enable Partial Clip Detection** and adjust:

| Setting | Default | Description |
|---------|---------|-------------|
| Min clip / source ratio (%) | 10 | Minimum clip duration as a percentage of the source duration. Clips shorter than this are ignored. |
| Min audio similarity (%) | 80 | Minimum average Hamming similarity for the sliding-window fingerprint match to be accepted. |
| Require visual confirmation | on | Reject audio matches whose frames at the matched offset don't also look similar. |
| Min visual similarity (%) | 85 | Minimum frame similarity for the visual confirmation step. |

> **Note:** Partial clip detection requires audio tracks in both files. Videos without audio are skipped — for those, see the visual variant under **AI Matching** below.

---

# AI Matching (optional)

VDF can additionally compare videos with neural image embeddings (a [DINOv2](https://github.com/facebookresearch/dinov2) vision model running via [ONNX Runtime](https://onnxruntime.ai/)). The classic comparison stays authoritative — the AI pass only **adds** pairs it is confident about, so enabling it never hides results you would otherwise get. It is good at exactly the cases pixel-based methods miss:

- **Transformed copies** — cropped, mirrored, zoomed, letterboxed, color-graded or otherwise heavily edited versions of the same video. Pairs found this way are marked with an **AI** chip in the results.
- **Visual partial detection** — finds trimmed cuts and clips contained in longer recordings by matching sampled keyframes with a consistent time offset. Unlike the audio-based partial clip detection above, this also works on silent, muted and re-dubbed videos. Matches appear with the same **Clip Offset** column.

### Enabling it

Both switches are independent and off by default:

| Setting | Where | Default | Description |
|---------|-------|---------|-------------|
| AI matching (additional pass) | Settings → Matching | off | Enables the embedding comparison on top of the selected classic mode. |
| AI similarity threshold (%) | Settings → Matching | 94 | How similar two files' embeddings must be. Lower to ~92 to find more aggressively edited copies at a slightly higher false-positive risk. |
| Detect partial duplicates visually (AI) | Settings → Partial Clip Detection | off | Dense keyframe matching for trimmed/embedded clips, no audio needed. |
| AI frame hit threshold (%) | Settings → Partial Clip Detection | 89 | Per-keyframe similarity needed for a hit; at least 4 hits must agree on one time offset before two videos are paired. Raise it if unrelated videos get paired. |

### Components, privacy & footprint

- On first use VDF downloads two components (**~100 MB** once): the ONNX Runtime library from the [official Microsoft release](https://github.com/microsoft/onnxruntime/releases) and the embedding model (integrity-checked against a pinned SHA256). They are stored next to the scan database. The GUI asks before downloading.
- **Everything runs locally on your CPU.** No cloud services, no accounts, nothing is uploaded — the model analyzes your frames on your machine, full stop.
- Cost: roughly 50 ms per file during hashing; embeddings are cached in the scan database (~2 KB per file), so rescans stay fast. Visual partial detection keeps its keyframe cache in a separate `DenseEmbeddings.db` sidecar (~25 KB per video) that cleans itself up.
- Supported on all release platforms (Windows, Linux x64/ARM64, macOS Intel & Apple Silicon).

---

# Downloads

[Daily build](https://github.com/0x90d/videoduplicatefinder/releases/tag/4.1.x) — attachments are automatically rebuilt and replaced on every commit.

[Versioned releases](https://github.com/0x90d/videoduplicatefinder/releases) (tags like `v4.1.1`) are published now and then, and their files never change afterwards. Package managers and anyone who needs a fixed download should use those.

> **Prefer the classic interface?** 4.1 introduces a redesigned interface. The final classic-UI build stays available on the [4.0.x release](https://github.com/0x90d/videoduplicatefinder/releases/tag/4.0.x) — databases and settings are compatible both ways.

> **Upgrading from 3.x:** your scan database is migrated automatically on first load. Cached image hashes are recomputed on the next scan (image processing moved from ImageSharp to FFmpeg); video hashes are unaffected. Downgrading back to 3.x after the migration is not recommended. The last 3.x build remains available on the [3.0.x release](https://github.com/0x90d/videoduplicatefinder/releases/tag/3.0.x).

Available packages per platform:
- `GUI-<platform>` — desktop application

---

# Desktop GUI

### Requirements

FFmpeg and FFprobe are required. On first launch VDF attempts to download them automatically.
Native FFmpeg binding requires FFmpeg 8.x shared libraries (not the master branch).

#### Windows
Download the latest FFmpeg GPL shared package from https://ffmpeg.org/download.html
Extract `ffmpeg.exe` and `ffprobe.exe` into the same folder as `VDF.GUI.exe`, a subfolder named `bin`, or ensure they are on your `PATH`.

#### Linux
```bash
sudo apt-get update && sudo apt-get install ffmpeg
```
Then run:
```bash
chmod +x VDF.GUI
./VDF.GUI
```

**Optional: add to your application menu**

The Linux archive includes `videoduplicatefinder.desktop` and `icon.png`. To register the app with your desktop environment (GNOME, KDE, XFCE, etc.):

```bash
# Edit the Exec= and Icon= paths to match where you extracted the archive, e.g.:
sed -i "s|/opt/videoduplicatefinder|$(pwd)|g" videoduplicatefinder.desktop

# Install for the current user
mkdir -p ~/.local/share/applications
cp videoduplicatefinder.desktop ~/.local/share/applications/
```

The app will then appear in your application launcher with its icon.

#### macOS
```bash
brew install ffmpeg
```
Extract the archive — it contains `Video Duplicate Finder.app`. Double-click it to launch.

If macOS blocks the app with "cannot be opened because the developer cannot be verified", right-click the `.app` and choose **Open**, then confirm. You only need to do this once.

If macOS still refuses to launch the bundle (e.g. "library load disallowed by system policy" on macOS 14+ / Tahoe), clear the quarantine flag and re-sign every binary in the bundle ad-hoc:
```bash
xattr -cr "Video Duplicate Finder.app"
codesign --force --deep --sign - "Video Duplicate Finder.app"
```

---

# Screenshots (outdated)
<img src="https://user-images.githubusercontent.com/46010672/129763067-8855a538-4a4f-4831-ac42-938eae9343bd.png" width="510">

# License
Video Duplicate Finder is licensed under AGPLv3.

The optional AI components are downloaded separately on first use and carry their own licenses: ONNX Runtime (MIT) and the DINOv2-small embedding model (Apache-2.0). Neither is bundled with or linked into the release binaries.

# Credits / Third Party
- [Avalonia](https://github.com/AvaloniaUI/Avalonia)
- [ActiPro Avalonia Controls (Free Edition)](https://github.com/Actipro/Avalonia-Controls)
- [FFmpeg.AutoGen](https://github.com/Ruslan-B/FFmpeg.AutoGen)
- [MemoryPack](https://github.com/Cysharp/MemoryPack)

- [AcoustID.NET by wo80](https://github.com/wo80/AcoustID.NET) — the audio fingerprinting pipeline (Chromaprint-style chroma extraction, FIR smoothing, and fingerprint encoding) used for partial clip detection is derived from this library, licensed under LGPL 2.1

- [ONNX Runtime](https://github.com/microsoft/onnxruntime) (Microsoft, MIT) — inference engine for the optional AI matching feature
- [DINOv2](https://github.com/facebookresearch/dinov2) (Meta AI, Apache-2.0) — the image embedding model behind AI matching, used as the int8-quantized ONNX export from [Xenova/dinov2-small](https://huggingface.co/Xenova/dinov2-small) (mirrored on this repo's [ai-models-v1 release](https://github.com/0x90d/videoduplicatefinder/releases/tag/ai-models-v1))

# Building
- .NET 10.x
- Visual Studio 2022 or later is recommended

# Contributing
- Create a pull request for each addition or fix — do not merge them into one PR
- Unless it refers to an existing issue, write into your pull request what it does
- For larger PRs, open an issue for discussion first
