# Accepted UI implementation

Reference: generated visual exec-6ef69c68-91e2-4891-878f-f0e4853d8b20.png, approved by user October 1.

Keep the present sidebar width and existing gradient identity. Sidebar blends with the main surface. The rounded brand capsule itself toggles collapse; collapsed mode displays only the brand icon. Navigation uses 18px icons with 1.5px strokes. Expanded active navigation uses blue text and icons; collapsed active navigation uses a pale blue square. Hover uses neutral gray in both themes. Preserve all commands, keyboard navigation, theme support, scan/selection algorithms and virtualized results.

Tasks:
1. MainWindow.xaml and shell resources: clickable brand capsule, coherent background, neutral thin icons, blue active labels, settings sliders icon, subdued theme segment. Verify existing shell/keyboard/headless tests and render light/dark states.
2. DuplicateResultsView.xaml and local styles: tighten summary/hint space and group rows; dark filenames and readable neutral metadata, align essential metrics where feasible at ~700px pane width. Preserve comparison, best selection, filters, thumbnail resizing, collapsing and bulk actions. Verify results/contrast/keyboard tests and render actual five-file sample.
3. Build with local CET compiler wrapper, run headless suite and GUI tests; publish isolated runnable Windows x64 build under D:/Codex. Preserve Downloads app and its settings. Review all diffs before delivery.

No changes to recognition/recommendation rules or database format, and no duplicate generated-image controls. Settings page uses existing functional groups for this first accepted visual implementation; avoid unapproved removal of explanations. The additional AI request adds bundled component discovery and release packaging only.

The owner uses only Chinese and English. Existing missing keys in other locales are deliberately outside this change; the locale parity test reports this pre-existing limitation.
