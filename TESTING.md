# KKS Runtime Testing

## 2026-09-06 — v2.0.6

Environment: Koikatsu Sunshine CharaStudio, Unity 2019.4.9, BepInEx 5.4.23.5, Forward rendering, KKSAPI 1.47, ExtensibleSaveFormat 21.1.3, original PPE 4.5 in HighDefinitionRange grading mode.

### Verified (headless launches, Studio start scene, no scene card)

- Build: 0 warnings, 0 errors.
- `LogOutput.log`: `Loading [PPE Extended (Full PPSv2) 2.0.6]`, `Members: volume=True layer=True cg=True ao=True onoff=True`, `Update patched: True`, `Scene save/load controller registered`.
- After the Studio scene loads: `Tick running from own Update`, a few `Waiting for original PPE volume: volume=null ppeOn=True` lines (pre-camera), then `All PPSv2 effects ensured in profile` and `Rebound: profile='' settings=9 layer=enabled camera=Main Camera/Forward`. No `[Warning]` from the plugin.
- Presets tab: saving creates `BepInEx/plugins/PPE_Extended_Presets/<name>.cfg` (confirmed by user with `8888.cfg`).
- Typing into number boxes works (confirmed by user).
- Scene save/load round trip: no errors (confirmed by user; log lines for it now go to LogOutput.log).

### User checklist for the final build

1. Panel top shows `Original PPE: ON | Bound: yes | ColorGrading: ON HighDefinitionRange/Neutral`. If it says OFF or `Bound: no`, nothing below can work — turn the original PPE on (Keypad /).
2. Tick **Enable Color Overrides** → Curves tab → drag **Hue vs Saturation / Red** to -1: red objects (lips, red cloth) go gray; +1 doubles their saturation. Drag **Lum vs Sat / Shadows** to -1: dark areas desaturate. These are the curves that work in HDR; the Master/RGB presets at the bottom are LDR-only and will not change anything in KKS.
3. Trackballs tab: drag Lift R to 3 — shadows turn red. If not, the status line must say ColorGrading ON; the tab shows an **Enable Color Grading in Original PPE** button when it is off.
4. Mixer: with all values at identity (R_R=G_G=B_B=1, others 0) the picture is unchanged; the 2/2/2 values left over from testing must be reset with **Reset (Identity)** first.
5. Tick **Take Ownership of PPSv2 Effects**: the picture must not change (values were copied from the original). Then drag Bloom Intensity — bloom follows. Untick — original bloom returns and LogOutput shows `Released our overrides; original PPE Settings() re-applied`.
6. Presets: Save / Load / Delete as before; presets now also carry the `[HdrCurves]` section.
7. Save the scene, load another, load it back: LogOutput shows `Scene Load: restored N extension values`.

## 2026-09-05 — v2.0.5 (withdrawn)

Loaded without errors at the start screen; withdrawn because its per-frame release disabled the original PPE's effects whenever a master switch was off (DEBUG.md #26).

### Compatibility matrix (still valid)

| Effect | KKS Forward baseline |
|---|---|
| Color curves / mixer / custom tone | Supported, opt-in (curves: HDR-safe secondary curves only) |
| Bloom | Supported, opt-in |
| DoF | Supported, scene/camera dependent |
| Grain | Supported |
| Vignette | Supported |
| Chromatic Aberration | Supported |
| Lens Distortion | Supported, use low intensity |
| Motion Blur | Supported, animation dependent |
| Fog / AA | Camera ownership required |
| Auto Exposure | Experimental, disabled by default |
| SSR | Requires Deferred; v2.1.1 provides an opt-in camera path switch and independent SSR ownership; use ReflectionProbe if Deferred is incompatible with a scene |
