# DEBUG & Troubleshooting Log

All issues encountered during development, their root causes, and fixes. This document grows with every release.

---

## PPE_Extended Plugin Issues

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
