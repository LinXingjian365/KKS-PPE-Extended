# KKS PPE Extended

Full Unity PPSv2 (Post Processing Stack v2) parameter panel for Koikatsu Sunshine CharaStudio.

Extends the original KKS PostProcessingEffects plugin with a standalone floating window exposing **all** PPSv2 effect parameters that the original panel hides.

## Features

16 tabs covering the Unity 2019 PPSv2/KKS PPE surface:

| Tab | Effect | Key Parameters |
|-----|--------|---------------|
| Trackballs | Color Grading Lift/Gamma/Gain | RGB + offset per trackball |
| Curves | Color Grading Curves | 5 presets, master/RGB curves, strength |
| Mixer | Channel Mixer | 9-channel matrix (R/G/B output x R/G/B input) |
| CustomTone | Custom Tonemapping | Toe/Shoulder/Gamma (requires Tonemapper=Custom) |
| Bloom | Bloom | Intensity, threshold, soft knee, clamp, diffusion, anamorphic, dirt, tint |
| DoF | Depth of Field | Focus distance, aperture, focal length, kernel size |
| Grain | Film Grain | Intensity, colored, size, luminance contribution |
| Lens | Lens Distortion | Intensity, center X/Y, scale |
| CA | Chromatic Aberration | Intensity, fast mode |
| Blur | Motion Blur | Shutter angle, sample count |
| Vignette | Vignette | Classic/Masked mode, intensity, smoothness, roundness, center, color |
| SSR | Screen Space Reflections | Preset, thickness, march distance, fade, vignette, iterations |
| MSVO | Ambient Occlusion | ScalableAO/MSVO switch, thickness, direct light, tolerances |
| AutoExp | Auto Exposure | Fixed/progressive adaptation, luminance bounds, key value |
| AA | PostProcessLayer AA | None, FXAA, SMAA, TAA and their parameters |
| Fog | Unity/PPSv2 Fog | Mode, density, distance, height, color |

## Design Principles

- **Color Overrides master switch (default OFF)**: Curves/Mixer/CustomTone do NOT touch PPE panel values until explicitly enabled. This prevents the extension from fighting with your existing PPE settings.
- **All other effects have independent enable toggles**, default OFF.
- **Pure English UI** — no translated strings.
- **Standalone floating window** (Ctrl+P) — does not modify the original PPE panel layout.
- **Panel scale slider** (0.5x–2x) for HiDPI displays.
- Rebinds automatically when KKS replaces the active post-process volume/profile during a scene load.
- Emits throttled runtime diagnostics for the bound profile, layer, camera path, and effect state.
- Applies the original KKS camera AA and fog controls through the active PostProcessLayer.

## Requirements

- Koikatsu Sunshine (KKS) CharaStudio
- BepInEx 5.x
- [KKS_PostProcessingEffectsV3](https://github.com/DeathWeasel1337/KK_Plugins) (original PPE plugin, must be installed)
- Unity 2019.4 (PPSv2 built-in)

## Installation

1. Install the original KKS PostProcessingEffects plugin if not already present.
2. Copy `PPE_Extended.dll` to `BepInEx/plugins/`.
3. Launch CharaStudio.
4. Press **Ctrl+P** to open the extended panel.

## Usage

1. Open the original PPE panel and enable the effects you want (Color Grading, Bloom, etc.).
2. Press **Ctrl+P** to open the extended panel.
3. For Curves/Mixer/CustomTone: tick **"Enable Color Overrides"** at the top of the panel.
4. For other effects: tick the **"Enable X"** toggle inside each tab.
5. Adjust sliders — changes apply in real-time.

### Hotkeys

| Key | Action |
|-----|--------|
| Ctrl+P | Toggle extended panel |

## Building

```bash
dotnet build -c Release
```

Output: `bin/Release/net471/PPE_Extended.dll`

Requires .NET Framework 4.7.1 targeting pack. Reference paths are configured in the `.csproj` to point at the game's managed assemblies.

## Version History

See [CHANGELOG.md](CHANGELOG.md).

## License

MIT
