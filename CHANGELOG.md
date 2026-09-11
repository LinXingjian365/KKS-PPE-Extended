# Changelog

## [2.1.1] - 2026-09-11

### Fixed
- SSR now applies independently of the global `EnableEffectOverrides` switch, so Bloom/DoF and other PPSv2 effects do not need to be taken over just to use SSR.
- SSR depth and motion-vector requests remain active while the SSR setting is enabled.

## [2.1.0] - 2026-09-11

### Added
- Opt-in `ForceDeferredForSSR` mode for testing PPSv2 Screen Space Reflections on KKS cameras that normally use Forward rendering.
- Camera-path restoration when SSR is disabled, the profile changes, or the plugin unloads.
- Depth and motion-vector requests plus active camera-path reporting in the SSR panel.
- A PPSv2 `Custom` SSR preset so custom thickness, fade, vignette, resolution, and iteration controls are no longer overwritten every frame.

### Fixed
- Custom SSR ranges now match Unity PPSv2 (thickness 1–64, fade/vignette 0–1, iterations 4–256).

## [2.0.6] - 2026-09-06

### Fixed
- **Curves tab now actually affects the image.** `ApplyCurves` assigned the curve but never set `overrideState`, and PPSv2 volume blending ignores parameters whose `overrideState` is false — so Curves had been a no-op in every earlier version. See DEBUG.md #25.
- **Turning a master switch OFF hands control back cleanly.** Only the parameters this extension wrote are released (once, on the falling edge) and the original PPE `Settings()` is invoked so its own values are re-applied immediately. The old "Release All Overrides" button (which cleared nothing — it looked for a NonPublic `overrideState` field that is public) is replaced by **Return Control to Original PPE**. See DEBUG.md #26.
- **Numeric fields next to sliders can be typed into.** The field kept re-formatting its text every frame. See DEBUG.md #27.
- Startup log said v2.0.0; the version is now a single constant used by the plugin attribute, window title and log.
- Curves are rebuilt only when a Curves setting changes or the bound ColorGrading changes, instead of allocating new `AnimationCurve`s every frame.
- SSR tab help text corrected (needs a Deferred G-buffer; no effect in KKS Forward).

### Changed
- Per-frame work runs from the extension's own `Update()` instead of a Harmony postfix on the original's `Update` (the postfix remains only as a diagnostic). All logging goes through the BepInEx logger, so everything is in `BepInEx/LogOutput.log` (DEBUG.md #29).
- Bloom Clamp range widened to 0..65472 to match the original PPE.
- The "Bound profile=..." diagnostic line is no longer written every 2 seconds. One `Rebound:` line is logged whenever the bound volume/profile/layer/camera changes. Set `[General] VerboseDiagnostics = true` to get the periodic line back.

### Added
- **HDR-safe curves.** The Curves tab now exposes the four secondary curves PPSv2 actually samples in HighDefinitionRange grading mode: Hue vs Saturation and Hue vs Hue (8 hue bands each), Luminance vs Saturation and Saturation vs Saturation (shadows/mid/highlights). Master/RGB curves stay available but are labelled LDR-only, because `ColorGradingRenderer.GetCurveTexture(hdr: true)` ignores them (DEBUG.md #28).
- **Ownership starts from the original's current values.** Enabling *Take Ownership of PPSv2 Effects* or *Camera AA/Fog* first copies the original PPE's live values into the panel (`[General] CopyOriginalOnOwnership`, default on), and a **Copy Current Original PPE Values** button does it on demand, so the picture does not change until you move something (DEBUG.md #30).
- **Status line** at the top of the panel: original PPE on/off, bound state, Color Grading on/off with grading mode and tonemapper.
- Color tabs warn when Color Grading is off in the original PPE and offer to enable it; enabling Color Overrides turns it on automatically. CustomTone gets **Set Original Tonemapper to Custom**.
- **Presets tab**: save / load / delete named presets of all extension settings (every section except `UI`) as `BepInEx/plugins/PPE_Extended_Presets/<name>.cfg`. Files use the BepInEx `[Section]` / `Key = value` layout and are human-editable; unknown keys are ignored.
- **Scene persistence**: extension settings are stored in Studio scene files (ExtensibleSaveFormat id `com.user.ppe_extended`) when a scene is saved and restored on Load and Import (not on Clear / new scene) — the same policy as Save_PostProcessingEffects uses for the original PPE.
- New hard dependencies: KKSAPI (`marco.kkapi` >= 1.35) and ExtensibleSaveFormat (`com.bepis.bepinex.extendedsave` >= 16.8.1). Both ship with the standard KKS BepisPlugins set.

### Notes
- v2.0.5 existed as source only and must not be deployed: its per-frame release disabled the original PPE's effects whenever a master switch was off (DEBUG.md #26). v2.0.6 is built on the v2.0.4 build that was actually in use.

## [2.0.5] - 2026-09-05 (source only, withdrawn)

- Fixed stale PPSv2 override states persisting after the master toggles are disabled.
- Auto Exposure is now opt-in behind the camera ownership switch and is released when that switch is off.
- Added full release paths for color curves/mixer/tone and all owned PPSv2 effect parameters.
- Disabled the unsafe persisted Auto Exposure/SSR baseline in the live KKS config; the previous config is archived.

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
