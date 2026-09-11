# KKS PPE Extended

Full Unity PPSv2 (Post Processing Stack v2) parameter panel for Koikatsu Sunshine CharaStudio.

Extends the original KKS PostProcessingEffects plugin with a standalone floating window exposing **all** PPSv2 effect parameters that the original panel hides, plus presets and per-scene persistence for those extra settings.

## Features

17 tabs covering the Unity 2019 PPSv2/KKS PPE surface:

| Tab | Effect | Key Parameters |
|-----|--------|---------------|
| Trackballs | Color Grading Lift/Gamma/Gain | RGB + offset per trackball |
| Curves | Color Grading Curves | HDR-safe Hue vs Sat / Hue vs Hue (8 hue bands), Lum vs Sat, Sat vs Sat; Master/RGB presets (LDR only) |
| Mixer | Channel Mixer | 9-channel matrix (R/G/B output x R/G/B input) |
| CustomTone | Custom Tonemapping | Toe/Shoulder/Gamma (requires Tonemapper=Custom) |
| Bloom | Bloom | Intensity, threshold, soft knee, clamp, diffusion, anamorphic, dirt, tint |
| DoF | Depth of Field | Focus distance, aperture, focal length, kernel size |
| Grain | Film Grain | Intensity, colored, size, luminance contribution |
| Lens | Lens Distortion | Intensity, center X/Y, scale |
| CA | Chromatic Aberration | Intensity, fast mode |
| Blur | Motion Blur | Shutter angle, sample count |
| Vignette | Vignette | Classic/Masked mode, intensity, smoothness, roundness, center, color |
| SSR | Screen Space Reflections | Optional Deferred path, quality presets, custom thickness/distance/fade/iterations |
| MSVO | Ambient Occlusion | ScalableAO/MSVO switch, thickness, direct light, tolerances |
| AutoExp | Auto Exposure | Fixed/progressive adaptation, luminance bounds, key value |
| AA | PostProcessLayer AA | None, FXAA, SMAA, TAA and their parameters |
| Fog | Unity/PPSv2 Fog | Mode, density, distance, height, color |
| Presets | Extension presets | Save / load / delete named presets of all extension settings |

## Design Principles

- **Color Overrides master switch (default OFF)**: Curves/Mixer/CustomTone do NOT touch PPE panel values until explicitly enabled. This prevents the extension from fighting with your existing PPE settings.
- **Compatibility mode is default**: the original KKS PPE owns effect enable states and values. The extension does not write them unless **Take Ownership of PPSv2 Effects** is enabled.
- Camera AA/Fog are similarly untouched unless **Take Ownership of Camera AA/Fog** is enabled.
- **Ownership starts from the original's current values**: enabling an ownership switch copies the original PPE's live values into the panel first (`CopyOriginalOnOwnership`, default on), and **Copy Current Original PPE Values** does it any time.
- **Status line** shows original PPE on/off, bound state and Color Grading mode; color tabs offer to enable Color Grading in the original when it is off.
- **Clean hand-back**: when a master switch is turned OFF, only the parameters this extension wrote are released and the original PPE re-applies its own values at once. **Return Control to Original PPE** does this for all three switches in one click.
- **Unified Panel Mode** hides the duplicate original PPE IMGUI while keeping the original PPE runtime initialization and Unity 2019.4 PPSv2 resources.
- KKS-specific extras remain implemented by the original PPE runtime; standard Unity 2019.4 PPSv2 effects are controlled from the unified tabs.
- **Presets** for the extension's own settings live in `BepInEx/plugins/PPE_Extended_Presets/*.cfg` (BepInEx-style `[Section]` / `Key = value`, human-editable). Original PPE presets (Save_PostProcessingEffects) are separate.
- **Scene persistence**: extension settings are saved into Studio scene files and restored on Load/Import, so a scene card carries the complete look.
- **Pure English UI** — no translated strings.
- **Standalone floating window** (Ctrl+P) — does not modify the original PPE panel layout.
- **Panel scale slider** (0.5x–2x) for HiDPI displays; number boxes next to sliders accept typed values.
- Rebinds automatically when KKS replaces the active post-process volume/profile during a scene load and logs one `[PPE Ext] Rebound:` line when it does (`[General] VerboseDiagnostics = true` re-enables the periodic status line).

## Requirements

- Koikatsu Sunshine (KKS) CharaStudio
- BepInEx 5.x
- [KKS_PostProcessingEffectsV3](https://github.com/RikkiBalboa/Koikatsu-Plugins) v4.5 (original PPE plugin, must be installed)
- KKSAPI (`marco.kkapi` >= 1.35) and ExtensibleSaveFormat (`com.bepis.bepinex.extendedsave` >= 16.8.1) — both are part of the standard KKS BepisPlugins set
- Unity 2019.4 (PPSv2 built-in)

## Installation

1. Install the original KKS PostProcessingEffects plugin if not already present.
2. Copy `PPE_Extended.dll` to `BepInEx/plugins/`.
3. Launch CharaStudio.
4. Press **Ctrl+P** to open the extended panel.

## Usage

1. Open the original PPE panel and enable the effects you want (Color Grading, Bloom, etc.). In the default compatibility mode, those original controls remain authoritative.
2. Press **Ctrl+P** to open the extended panel.
3. For Curves/Mixer/CustomTone: tick **"Enable Color Overrides"** at the top of the panel.
4. To let this extension control Bloom/DoF/Grain/Lens/CA/Blur/Vignette/SSR, explicitly enable **"Take Ownership of PPSv2 Effects"** first. Otherwise their original PPE values are preserved.
   PPSv2 SSR requires Deferred GBuffer data; KKS runs Forward and the extension blocks runtime path switching for safety. Use `KKS_ReflectionProbe.dll` for reflections in the normal KKS renderer.
5. To let this extension control FXAA/SMAA/TAA/Fog, explicitly enable **"Take Ownership of Camera AA/Fog"** first.
6. Adjust sliders or type into the number boxes — changes apply in real-time.
7. Turn a master switch off (or press **Return Control to Original PPE**) to give the original plugin its values back immediately.
8. **Presets** tab: type a name and press **Save**; click a preset to load it; **Delete** asks for confirmation.
9. Saving a Studio scene stores the extension settings in the scene file; loading or importing that scene restores them.

### Hotkeys

| Key | Action |
|-----|--------|
| Ctrl+P | Toggle extended panel |

## Building

```bash
dotnet build -c Release
```

Output: `bin/Release/net471/PPE_Extended.dll`

Requires the .NET SDK with .NET Framework 4.7.1 targeting. Reference paths are configured in the `.csproj` to point at the game's managed assemblies (`KoikatsuSunshine_Data/Managed`, `CharaStudio_Data/Managed/Assembly-CSharp.dll`, `BepInEx/core`, `BepInEx/plugins/KKSAPI.dll`, `BepInEx/plugins/KKS_BepisPlugins/KKS_ExtensibleSaveFormat.dll`).

Deploy only while CharaStudio is closed (the DLL is locked while the game runs and BepInEx rewrites the config on exit).

## Version History

See [CHANGELOG.md](CHANGELOG.md). Troubleshooting and root causes are in [DEBUG.md](DEBUG.md).

## License

MIT
