# Validation of UI and bundled AI changes

October 1, 2026, Windows x64, .NET SDK 10.0.301.

- Core: 705 persistent tests passed, including four new complete/incomplete bundle discovery cases.
- GUI headless: 273 tests passed, including light/dark contrast, selection, keyboard navigation, group collapse and brand button state backgrounds.
- GUI view models: 501 tests passed. One pre-existing locale parity test fails because non-English/non-Chinese locales lack UI keys; the owner explicitly maintains only English and Chinese.
- Integration: 43 passed, 36 skipped because native media dependencies are unavailable. Skipped tests are not claimed as passed.
- Production model smoke: the downloaded, SHA256-verified DINOv2 model produced a finite 384-dimensional normalized embedding. An isolated process loaded the bundled ONNX Runtime from its own ai/ folder, reported Ready, and passed real inference.
- Windows self-contained publish succeeded. MainWindow and results commands/event bindings match the starting commit. The only Core source modification is AI component folder discovery, not recognition or file-operation logic.
- Release PowerShell script parsed successfully. Full release packaging and Native AOT build are verified by the GitHub release workflow after push; local publish uses self-contained .NET.

This is a regression check, not a proof that the entire application has no undiscovered bugs.
