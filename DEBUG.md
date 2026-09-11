# DEBUG & Troubleshooting Log

All issues encountered during development, their root causes, and fixes. This document grows with every release.

---

## SSR on KKS (v2.1.1)

PPSv2 Screen Space Reflections requires the camera's Deferred G-buffer and
therefore reports unsupported on KKS's normal Forward path. Open the SSR tab,
enable **Force Deferred path while SSR is enabled**, then initialize SSR while
**Take Ownership of PPSv2 Effects** is enabled. The panel reports the active
camera path and the log records the transition. SSR no longer requires the
global PPSv2 effect ownership switch. Disabling the option or SSR
restores the previous camera path automatically.

---

## PPE_Extended Plugin Issues

### 30. Taking ownership made effects vanish and sliders seemed dead

**Symptom**: Ticking *Take Ownership of PPSv2 Effects* removed bloom; moving Bloom sliders afterwards appeared to do nothing.
**Root Cause**: Ownership started from the extension's own stored values (Bloom Intensity 0, Threshold later dragged to 3.1) instead of the original's (2.8 / 0.89). With a threshold above almost every HDR pixel, intensity changes are invisible.
**Fix (v2.0.6)**: On the rising edge of an ownership switch the original PPE's live values are copied into the extension (`[General] CopyOriginalOnOwnership`, default true); a **Copy Current Original PPE Values** button does it manually. Bloom Clamp range was widened to 0..65472 so the copy is exact.

### 29. Plugin `Debug.Log` output missing from Player.log; Harmony postfix looked dead

**Symptom**: In 2.0.6 sessions not a single `Debug.Log` from this plugin reached `Player.log` (other plugins' lines were present; 2.0.4 sessions had logged 800+ lines the same way), which made the Harmony postfix on the original `Update` look like it never ran.
**Facts**: Routing the same diagnostics through the BepInEx `ManualLogSource` showed the postfix does fire and binding works (`Rebound: profile='' settings=9 layer=enabled camera=Main Camera/Forward`). Why Unity swallowed this plugin's `Debug.Log` was not identified.
**Fix (v2.0.6)**: All logging uses the BepInEx logger, and the per-frame work is driven from the extension's own `Update()`, so neither Unity log capture nor patching the original's `Update` is load-bearing. The postfix is kept only to log once whether it fired.

### 28. Curves still had no effect in HDR grading mode after #25

**Symptom**: With `overrideState` fixed (#25), Master/RGB curve presets still changed nothing.
**Root Cause**: Decompiled `ColorGradingRenderer.GetCurveTexture(bool hdr)` only writes the master/R/G/B (YRGB) curves into the curve texture when `hdr` is false; both HDR pipelines (`RenderHDRPipeline2D/3D`) call it with `hdr: true`. KKS PPE runs `GradingMode = HighDefinitionRange`, so YRGB curves are structurally inert. Only `hueVsHueCurve`, `hueVsSatCurve`, `satVsSatCurve`, `lumVsSatCurve` are sampled in HDR (0.5 = neutral; 1.0 doubles saturation / +180 deg hue; 0 removes it).
**Fix (v2.0.6)**: The Curves tab exposes those four curves as band sliders (8 hue bands, 3 luminance/saturation points, flat-tangent AnimationCurves, looping for the hue curves) and labels the YRGB controls as LDR-only.

### 27. Numeric text fields could not be typed into (v2.0.4)

**Symptom**: Clicking the number box next to a slider and typing produced garbage or the cursor jumped; only the slider was usable.
**Root Cause**: `FloatField` rebuilt the TextField text from `val.ToString("F2")` every frame, so each keystroke was immediately re-parsed and re-formatted.
**Fix (v2.0.6)**: Focus-aware field. While the control has keyboard focus the raw typed text is kept in a buffer and shown as-is; the value is committed live whenever the text parses, and the text snaps back to F2 when focus leaves (Enter / Escape also blur the field). Field names are `Section.Key` so the same label on two tabs cannot share an edit buffer.

### 26. v2.0.5 per-frame "release" disabled the original PPE's effects (never deployed)

**Symptom**: With the extension's master switches OFF (the default), original PPE Bloom / DoF / Vignette / Grain / CA / Lens Distortion / Motion Blur stopped rendering.
**Root Cause**: v2.0.5 cleared `overrideState` on every effect parameter every frame while a master switch was off. The original PPE writes its overrides only in `Settings()` (called from `Setup()` and its config `SettingChanged` handler), not per frame, and this extension's Harmony postfix runs after the original `Update`. PPSv2 volume blending (`PostProcessLayer.OverrideSettings`) only applies parameters whose `overrideState` is true, so the original effects vanished.
**Fix (v2.0.6)**: Release happens once, on the falling edge of a master switch, only for the parameters this extension writes (curves / mixer / tone curve for the color group; bloom / DoF / grain / lens / CA / motion blur / vignette / SSR for the effect group). Immediately afterwards the original `Settings()` is invoked via reflection so its own values are re-applied in the same frame. The old "Release All Overrides" button — which searched for a NonPublic `overrideState` field although the field is public, and therefore never released anything — is replaced by **Return Control to Original PPE**, which turns all three switches off and runs the same edge logic.
**Lesson**: Never clear `overrideState` on parameters another plugin owns unless you also make that plugin re-apply them.

### 25. Curves tab had no effect in every version before 2.0.6

**Symptom**: Curves preset / strength / black lift / white crush / RGB offsets changed nothing on screen.
**Root Cause**: `ApplyCurves` assigned `cg.masterCurve.value.curve` (and the R/G/B curves) but never set `overrideState = true`. Decompiled `PostProcessLayer.OverrideSettings` skips every parameter whose `overrideState` is false, and `PostProcessProfile.AddSettings` only sets `enabled.value`, so the curves never reached the renderer. (This also means the "extreme Curves values" blamed in issue #1 were inert; the black screen came from the Custom Tone / MSVO values.)
**Fix (v2.0.6)**: `overrideState` is set on the master/R/G/B curves while Color Overrides is on and cleared again on the falling edge (see #26). Curves are rebuilt only when a `[Curves]` config value changes or the bound `ColorGrading` instance changes; previously new `AnimationCurve`s were allocated every frame.


### 24. Unified panel architecture

The original KKS PPE plugin remains the runtime owner of PPSv2 resources and initialization. Unified Panel Mode only suppresses its duplicate IMGUI and lets PPE Extended write the standard Unity 2019.4 PPSv2 settings. KKS-specific extras that have no PPSv2 equivalent remain part of the original runtime rather than being reimplemented with incompatible shaders.

### 23. Original PPE and PPE Extended fought over the same settings

Both plugins operate on the same PPSv2 profile and camera layer. The old extension wrote effect `enabled` values every frame, so an extension default of `false` could silently disable an effect selected in the original panel.

**Fix**: v2.0.3 adds two explicit ownership gates. `EnableEffectOverrides=false` preserves the original PPE volume effects. `EnableCameraOverrides=false` preserves original AA/Fog. The extension only writes those settings after the user enables the corresponding ownership option. The extension no longer disables effects during profile discovery.

### 22. Original PPE camera features missing from the extended panel

The previous release focused on volume effects and omitted the original KKS PPE camera controls. v2.0.2 adds FXAA, SMAA, TAA, Fog, and the missing Auto Exposure UI/application path. SSR remains a renderer limitation in Forward mode, not a missing parameter.

### 21. Effects appeared in the panel but did not affect the image after changing scenes

**Root Cause**: KKS Studio can replace the `PostProcessVolume` or its profile during scene transitions. The old extension cached the first profile forever, so later writes could target a stale profile. Its startup message also incorrectly reported v1.4.2 even though the DLL metadata was v2.0.0, making deployment verification misleading.

**Fix**: The current build refreshes the binding by PPE instance, volume, profile, layer, and main camera identity. On change it reacquires all PPSv2 settings and emits a throttled diagnostic line. Verify `Bound profile=... layer=enabled camera=...` in `LogOutput.log` before judging an effect.

**Known limits**: SSR still requires a compatible deferred/GBuffer path and is not expected to work in KKS's normal Forward path. AutoExposure remains disabled because it can crash KKS. These are renderer limits, not missing UI parameters.

### 1. PPE toggle makes screen completely black
**Symptom**: Enabling PPE in CharaStudio makes the scene pitch black. Disabling PPE makes it super bright.
**Root Cause**: `UpdatePostfix` Harmony patch unconditionally overwrites `ColorGrading` parameters every frame. The BepInEx config file contained extreme values from earlier debugging (Curves Preset=4, BlackLift=0.232, CTone Gamma=3, MSVO Thickness=4.84). Even after code defaults were changed to safe values, BepInEx reads the existing config file and prefers stored values over code defaults.
**Fix**: 
- Added `EnableColorOverrides` master toggle (default `false`) — Curves/Mixer/CustomTone only override PPE panel values when user explicitly enables it.
- Delete `BepInEx/config/com.user.ppe_extended.cfg` to force regeneration with clean defaults.
- **Lesson**: Never assume code defaults apply when a config file already exists. Always delete the config after changing defaults.

### 2. Config file keeps regenerating with extreme values
**Symptom**: After deleting the config, restarting the game recreates it with the same bad values.
**Root Cause**: The game was still running when the config was deleted. BepInEx writes the in-memory config back to disk on exit, recreating the file with old values.
**Fix**: Fully close CharaStudio before deleting the config file. Verify the file is gone after deletion.

### 3. AutoExposure causes crash / freeze on enable
**Symptom**: Toggling AutoExposure on crashes or freezes the game.
**Root Cause**: PPSv2 AutoExposure requires compute shader support and HDR camera target. KKS CharaStudio's forward renderer may not provide the expected texture format, causing null reference or compute dispatch failure.
**Fix**: AutoExposure removed from active feature set. Code retained (`TryInitAutoExposure`, `ApplyAutoExposure`) but not wired into the update loop. May be reimplemented safely in a future version.

### 4. SSR (Screen Space Reflections) has no visible effect
**Symptom**: Toggling SSR on/off produces no visible change in reflections.
**Root Cause**: PPSv2 ScreenSpaceReflections requires G-Buffer, which is only available in Deferred rendering path. KKS CharaStudio uses Forward rendering. The effect silently does nothing.
**Fix**: SSR tab retained for completeness but documented as non-functional in Forward mode. Alternative: use real-time reflection probe plugin (`KKS_ReflectionProbe.dll`, Ctrl+R toggle) for actual reflections.

### 5. DepthOfField compile error — wrong property name
**Symptom**: Compilation fails with `maxBlurSize` not found.
**Root Cause**: PPSv2 DepthOfField uses `kernelSize` property (enum `KernelSize`), not `maxBlurSize`.
**Fix**: Changed to `kernelSize` with `KernelSize` enum cast.

### 6. DLL deployment fails — file in use
**Symptom**: `Copy-Item` or `copy` fails with "user-mapped section open" or permission denied.
**Root Cause**: CharaStudio is running and has the DLL loaded. .NET assemblies cannot be overwritten while loaded.
**Fix**: Close CharaStudio before deploying. Use `cmd /c copy` instead of PowerShell `Copy-Item` (more reliable for this path).

---

## DLSS Integration Issues

### 7. NGX API — all init attempts return PlatformError
**Symptom**: `NVSDK_NGX_Init` returns `PlatformError` (or numeric 3134193666) for every combination of appId, dataPath, and sdk version.
**Root Cause**: NVIDIA NGX SDK on RTX 3060 Laptop with driver 531.41 has compatibility issues when initialized from within a Unity Mono/BepInEx context. The D3D11 device obtained via reflection is valid but NGX rejects the platform configuration. C++ native test confirmed same failure.
**Fix**: Abandoned native NGX approach. Switched to RenoDX renodx-dlss add-on (super-resolution variant) which wraps DLSS at the D3D11 level without NGX API calls.

### 8. EntryPointNotFoundException: NVSDK_NGX_AllocParameters
**Symptom**: DLSS wrapper throws `EntryPointNotFoundException` for `NVSDK_NGX_AllocParameters`.
**Root Cause**: Mismatched `nvngx_dlss.dll` version — the DLL exports don't match the P/Invoke signatures in the wrapper.
**Fix**: Updated `nvngx_dlss.dll` to a compatible version. Ultimately superseded by renodx approach.

### 9. GetDevice returns invalid pointer (0x3)
**Symptom**: `ID3D11Device::GetDevice` via vtable returns `0x3` (invalid pointer).
**Root Cause**: Incorrect vtable offset or calling convention when accessing D3D11 device through Unity's COM wrapper. The function pointer was correct but the `this` pointer or parameter layout was wrong.
**Fix**: Switched to native helper DLL that creates its own D3D11 device and shares it with NGX. Multiple approaches tried (reflection, vtable patching, native proxy) before settling on renodx.

### 10. Streamline d3d11 proxy — recursive self-load crash
**Symptom**: Replacing `d3d11.dll` with NVIDIA Streamline proxy causes infinite recursion / crash on startup.
**Root Cause**: Streamline's `d3d11.dll` proxy tries to load the real `d3d11.dll` from System32, but Unity's loading path causes the proxy to load itself recursively.
**Fix**: Abandoned Streamline approach. Not compatible with Unity 2019.4's D3D11 loading order.

### 11. renodx-dlss5 neural rendering variant — wrong for this use case
**Symptom**: renodx-dlss5 (neural rendering version) initializes but returns `0xBAD000B` or no upscaling.
**Root Cause**: The neural rendering variant requires DX12 and is designed for frame generation, not simple D3D11 super-resolution.
**Fix**: Used renodx-dlss **super-resolution** variant (`renodx-dlss.addon64`, ~2.4MB) instead. Works with D3D11 games.

### 12. ReShade — double proxy conflict ("Failed to initialize player")
**Symptom**: After installing ReShade, game shows "Failed to initialize player" graphics error on launch.
**Root Cause**: Both `d3d11.dll` and `dxgi.dll` exist in the game directory — two ReShade proxy DLLs conflicting. ReShade should only use one proxy (typically `dxgi.dll` for D3D11 games).
**Fix**: Remove the extra `d3d11.dll`. Keep only `dxgi.dll` as the ReShade proxy. renodx-dlss add-on goes in the `addons/` folder, not as a proxy DLL.

### 13. ReShade addons folder empty — can't find renodx-dlss
**Symptom**: ReShade add-on list shows nothing even though `renodx-dlss.addon64` is in `addons/`.
**Root Cause**: ReShade must be the **Add-on enabled** build (not standard). Standard ReShade doesn't load add-ons. Also the addons folder path must be correct (game root `addons/`).
**Fix**: Use ReShade 6.x Add-on version. Verify `renodx-dlss.addon64` is directly in `<game>/addons/` (not in a subfolder).

---

## Rendering / Color Issues

### 14. Scene too dark even with lights at max
**Symptom**: Lights cranked to maximum but scene remains dark. PPE off = super bright, PPE on = dark.
**Root Cause**: PPE's ColorGrading post-processing compresses the HDR range. With default tonemapping (ACES) and exposure settings, bright scenes get pulled down. Additionally, PPE_Extended was overriding ColorGrading with extreme values (see issue #1).
**Fix**: 
- After fixing EnableColorOverrides, PPE no longer force-overrides brightness.
- Adjust via PPE panel: Exposure > 0, or set Tonemapper to None for raw output.
- Do NOT switch to LDR color space — makes everything invisible (see #15).

### 15. Switching to LDR color space = everything invisible
**Symptom**: Changing color space from HDR to LDR makes the entire scene black/unreadable.
**Root Cause**: KKS materials and lighting are authored for HDR. LDR mode clamps all values to [0,1] and the post-processing chain expects HDR input.
**Fix**: Keep color space in HDR. Never switch to LDR in this game.

### 16. "Screen position out of view frustum" error spam
**Symptom**: Console spams `Screen position out of view frustum (screen pos 0.0, 0.0, 200.0) (Camera rect 0 0 2048 2048)` hundreds of times.
**Root Cause**: A camera with 2048x2048 render target (likely reflection probe or shadow camera) has objects outside its frustum being screen-projected. Common in KKS with multiple cameras.
**Fix**: This is a Unity warning, not a crash. Can be suppressed via `UnityLogFilter` plugin or ignored. Does not affect rendering quality.

---

## GitHub / Tooling Issues

### 17. Local git blocked by antivirus
**Symptom**: `git add` / `git commit` fails with "Permission denied" on `.git/objects/` files.
**Root Cause**: Tencent PC Manager (腾讯电脑管家) real-time protection intercepts writes to `.git/objects/` directory, treating git object writes as suspicious.
**Fix**: All git operations done via GitHub web UI (upload files, create commits via browser). Local git remains non-functional unless antivirus is disabled.

### 18. GitHub CodeMirror 6 editor — cannot programmatically paste
**Symptom**: Cannot inject source code into GitHub's "Create new file" editor via JavaScript.
**Root Cause**: GitHub uses CodeMirror 6 wrapped in React. The editor instance is not accessible via global variables or DOM properties. `navigator.clipboard.readText()` requires document focus and user gesture. `document.execCommand('paste')` returns false (blocked without user gesture).
**Fix**: Use GitHub's "Upload files" button instead of "Create new file" — supports direct file upload via file picker. For large source files, this is the only reliable web-based method.

### 19. bu.js limitations (browser automation)
**Symptom**: Various browser automation failures.
**Root Cause & Workarounds**:
- `bu.fill_input` does not accept ref format (e.g., `d35:e54`) — use CSS selectors (`#id` or `.class`).
- `bu.js` does not support async/await Promise return ("Promise was collected") — use synchronous code only.
- `bu.press_key` does not support "Control+v" combo format — use individual key calls or avoid paste.
- `bu.upload_file` requires CSS selector for file input element.

### 20. VideoExport recording fails — "Error while generating the video"
**Symptom**: CharaStudio built-in video recording (VideoExport plugin) shows "Error while generating the video, please check your output_log.txt file." BepInEx log shows `ffmpeg failed during the main encode pass (exit code -40)` and `h264_nvenc: Error while opening encoder - maybe incorrect parameters such as bit_rate, rate, width or height.`
**Root Cause**: VideoExport uses `h264_nvenc` (NVIDIA hardware encoding) with parameters `-tune hq -preset slow -qp 16`. On older NVIDIA drivers (e.g., 531.41), the nvenc encoder rejects these parameters and fails to initialize.
**Fix**: 
- **Option A (recommended)**: Update NVIDIA driver to latest version. After driver update, nvenc works correctly with the same parameters.
- **Option B (fallback)**: Set `mp4HwAccel = false` in `BepInEx/config/com.joan6694.illusionplugins.videoexport.cfg` to use `libx264` (CPU encoding, 100% compatible, better quality, slower).
- Config location: `BepInEx/config/com.joan6694.illusionplugins.videoexport.cfg`, key `mp4HwAccel`.
- **Note**: If the game is running, BepInEx may overwrite config changes on exit. Close CharaStudio before editing the config.

---

## Environment Reference

| Item | Value |
|------|-------|
| Game | Koikatsu Sunshine CharaStudio |
| Unity version | 2019.4.9 |
| Rendering | Forward, D3D11 |
| BepInEx | 5.4.23.5 |
| GPU | RTX 3060 Laptop |
| Driver | 531.41 |
| OS | Windows 10/11 x64 |
| PPE original | KKS_PostProcessingEffect v4.5 |
| .NET target | net471 |

---

## Quick Recovery Checklist

If something breaks:

1. **Black screen on PPE enable** → Delete `BepInEx/config/com.user.ppe_extended.cfg`, restart.
2. **Game won't start** → Check for duplicate proxy DLLs (`d3d11.dll` + `dxgi.dll`), remove one.
3. **DLL won't update** → Close CharaStudio first, then `cmd /c copy`.
4. **DLSS not working** → Verify ReShade is Add-on build, `renodx-dlss.addon64` in `addons/`, only `dxgi.dll` proxy.
5. **Console spam** → "Screen position out of view frustum" is harmless, ignore or filter.
6. **Video recording fails** → Update NVIDIA driver, or set `mp4HwAccel = false` in VideoExport config (see issue #20).
