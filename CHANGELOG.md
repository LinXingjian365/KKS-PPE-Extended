# Changelog

## v2.0.0 — 2026-09-04

### Added
- **Full PPSv2 coverage**: 13 tabs now include Bloom, Depth of Field, Film Grain, Lens Distortion, Chromatic Aberration, Motion Blur, and Vignette with complete parameter sets.
- `EnsureAllEffects()` automatically adds missing PPSv2 effects to the post-process profile on first run.
- Every new effect has an independent enable toggle (default OFF).
- `DEBUG.md` — comprehensive troubleshooting document covering all 19 known issues and fixes.

### Changed
- **Color Overrides master switch** (default OFF). Curves/Mixer/CustomTone no longer force-override PPE panel values unless explicitly enabled. This fixes the issue where the extension would darken the scene and prevent users from adjusting brightness through the original PPE panel.
- Panel tab layout expanded to 7 columns with 13 tabs.
- Version bump to 2.0.0 reflecting full PPSv2 coverage.

### Fixed
- Config file corruption from older versions with duplicate section keys (CTone/CustomTone, MSVO duplicates) — old configs should be deleted to regenerate clean defaults.
- DepthOfField `kernelSize` uses correct `KernelSize` enum (was incorrectly `maxBlurSize`).
- All new effects default to disabled to prevent unexpected visual changes on first run.

### Known Issues
- SSR has no visible effect in Forward rendering (requires Deferred + G-Buffer). Use reflection probe plugin instead.
- AutoExposure removed (crashes on this Unity version). See `DEBUG.md` issue #3.
- "Screen position out of view frustum" console spam is harmless (Unity warning from 2048x2048 cameras). See `DEBUG.md` issue #16.

## v1.5.0 — 2026-09-04

### Added
- `EnableColorOverrides` master switch to prevent forced parameter overrides.

### Fixed
- Removed AutoExposure (caused crashes on startup).
- Default config values reset to neutral.

## v1.4.2 — 2026-09-03

### Changed
- Full English UI (removed all Chinese strings).
- Panel scale slider for HiDPI.

## v1.0.0 — 2026-09-02

### Added
- Initial release with Trackballs, Curves, Mixer, CustomTone, SSR, MSVO tabs.
- Standalone floating window (Ctrl+P).
