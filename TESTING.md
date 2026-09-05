# KKS Runtime Testing

## 2026-09-05

Environment: Koikatsu Sunshine CharaStudio, Unity 2019.4.9, BepInEx 5.4.23.5, Forward rendering.

### Verified

- `PPE Extended 2.0.5` loads successfully in a fresh CharaStudio process.
- No PPE Extended exception, parameter-binding failure, or plugin crash was emitted during startup.
- The plugin waits safely when no Studio camera/profile exists: `Render path: No main camera (waiting for scene)`.
- Safe baseline was restored after the compatibility run: all master ownership switches, Auto Exposure and SSR are off.

### Not claimed as runtime-proven yet

The test process remained at the Studio start screen and no scene was loaded, so no active PostProcess profile or camera was available. Effect appearance and scene-specific compatibility must be tested after loading a scene. This avoids confusing a missing scene profile with an effect failure.

### Recommended compatibility matrix

| Effect | KKS Forward baseline |
|---|---|
| Color curves / mixer / custom tone | Supported, opt-in |
| Bloom | Supported, opt-in |
| DoF | Supported, scene/camera dependent |
| Grain | Supported |
| Vignette | Supported |
| Chromatic Aberration | Supported |
| Lens Distortion | Supported, use low intensity |
| Motion Blur | Supported, animation dependent |
| Fog / AA | Camera ownership required |
| Auto Exposure | Experimental, disabled by default |
| SSR | Disabled by default in Forward; use ReflectionProbe |

The final validation procedure is one scene load followed by enabling one effect at a time and checking `LogOutput.log` for `[PPE Ext]` binding diagnostics.
