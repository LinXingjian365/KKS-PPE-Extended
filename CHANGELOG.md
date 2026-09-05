# Changelog

## v2.0.4 — 2026-09-05

### Added
- Added opt-in Unified Panel Mode to hide the duplicate original PPE UI while preserving its runtime initialization.
- Unified mode automatically enables PPE Extended ownership for the standard Unity 2019.4 PPSv2 surface.

## v2.0.3 — 2026-09-05

### Fixed
- Added compatibility ownership gates so the extension no longer fights the original KKS PPE plugin.
- Removed first-bind writes that forced newly discovered effects disabled.
- Original PPE remains authoritative by default; extension ownership is opt-in for volume effects and camera AA/Fog.

## v2.0.2 — 2026-09-05

### Added
- Added the missing KKS PPE camera controls: FXAA, SMAA, TAA, and Fog.
- Added the Auto Exposure tab and safe runtime application path.
- Runtime diagnostics now report active anti-aliasing and fog state.

## v2.0.1 — 2026-09-05

### Fixed
- Rebind PPSv2 settings when KKS replaces the active volume/profile/camera after a scene load.
- Added throttled runtime binding diagnostics.
- Corrected the startup log version from 1.4.2 to 2.0.0.

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
