using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using BepInEx;
using BepInEx.Configuration;
using ExtensibleSaveFormat;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

namespace PPE_Extended
{
    [BepInPlugin(GUID, "PPE Extended (Full PPSv2)", Version)]
    [BepInDependency("org.bepinex.plugins.KKS_PostProcessingEffectsV3")]
    [BepInDependency("marco.kkapi", "1.35")]
    [BepInDependency("com.bepis.bepinex.extendedsave", "16.8.1")]
    public class PPEExtended : BaseUnityPlugin
    {
        public const string GUID = "com.user.ppe_extended";
        public const string Version = "2.1.0";
        private const string PresetFolderName = "PPE_Extended_Presets";
        private const string SceneDataKey = "ppe_ext_cfg";

        private static Harmony _harmony;
        private static BepInEx.Logging.ManualLogSource _log;
        private static Type _ppeType;
        private static PPEExtended _instance;

        private static MemberInfo _aoMode, _aoModeSel, _aoFold, _aoObj, _cgFold, _lift, _gamma, _gain, _cgObj, _toneMap, _ppVolume, _ppLayer, _onoff, _cgEnable;

        // New effects
        private static AutoExposure _autoExposure;
        private static ScreenSpaceReflections _ssr;
        private static Bloom _bloom;
        private static DepthOfField _dof;
        private static Grain _grain;
        private static LensDistortion _lensDistortion;
        private static ChromaticAberration _chromaticAberration;
        private static MotionBlur _motionBlur;
        private static Vignette _vignette;
        private static bool _effectsTried;
        private static bool _aeAvailable;
        private static bool _ssrAvailable;
        private static bool _isForwardRendering = true;
        private static string _renderPathInfo = "Detecting...";
        private static object _boundPpe;
        private static PostProcessVolume _boundVolume;
        private static PostProcessProfile _boundProfile;
        private static PostProcessLayer _boundLayer;
        private static Camera _boundCamera;
        private static Camera _ssrPathCamera;
        private static RenderingPath _ssrOriginalPath;
        private static bool _ssrPathOverridden;
        private static float _nextDiagnosticTime;
        private static bool _postfixSeen;
        private static bool _tickSeen;
        private static float _nextWaitLog;

        // Ownership edge tracking: release our overrides once when a master switch turns off.
        private static bool _mastersTracked;
        private static bool _prevColorMaster, _prevEffectMaster, _prevCameraMaster;

        // Curves are rebuilt only when their config changes or the bound ColorGrading changes.
        private static bool _curvesDirty = true;
        private static ColorGrading _curvesAppliedTo;

        // Presets tab state
        private static string _presetName = "";
        private static string _presetStatus = "";
        private static string _presetPendingDelete;
        private static string[] _presetFiles = new string[0];
        private static bool _presetListLoaded;
        private static Dictionary<string, ConfigEntryBase> _entriesByKey;

        // MSVO
        public static ConfigEntry<float> MSVOthickness, MSVOdirectLight, MSVOnoiseTol, MSVOblurTol, MSVOupsampleTol;
        public static ConfigEntry<bool> MSVOambientOnly;

        // Curves
        public static ConfigEntry<int> CurvePreset;
        public static ConfigEntry<float> CurveStrength, CurveBlackLift, CurveWhiteCrush, CurveRedOff, CurveGreenOff, CurveBlueOff;

        // Mixer
        public static ConfigEntry<float> MixRR, MixRG, MixRB, MixGR, MixGG, MixGB, MixBR, MixBG, MixBB;

        // Custom Tone
        public static ConfigEntry<float> CTtoeS, CTtoeL, CTshS, CTshL, CTshA, CTgamma;

        // Auto Exposure
        public static ConfigEntry<bool> AEenable;
        public static ConfigEntry<int> AEmode;
        public static ConfigEntry<float> AEminLum, AEmaxLum, AEkey, AEspeedUp, AEspeedDown, AEfilterMin, AEfilterMax;

        // SSR
        public static ConfigEntry<bool> SSRenable;
        public static ConfigEntry<bool> SSRForceDeferred;
        public static ConfigEntry<int> SSRpreset;
        public static ConfigEntry<float> SSRthickness, SSRmaxDist, SSRdistFade, SSRvignette, SSRiterations;
        public static ConfigEntry<int> SSRresolution;

        // Bloom
        public static ConfigEntry<bool> BloomEnable;
        public static ConfigEntry<float> BloomIntensity, BloomThreshold, BloomSoftKnee, BloomClamp, BloomDiffusion, BloomAnamorphic, BloomDirtIntensity;
        public static ConfigEntry<bool> BloomFastMode;
        public static ConfigEntry<float> BloomColorR, BloomColorG, BloomColorB;

        // Depth of Field
        public static ConfigEntry<bool> DoFEnable;
        public static ConfigEntry<float> DoFFocusDistance, DoFAperture, DoFFocalLength;
        public static ConfigEntry<int> DoFMaxBlur; // 0=Small,1=Medium,2=Large,3=VeryLarge

        // Grain
        public static ConfigEntry<bool> GrainEnable;
        public static ConfigEntry<float> GrainIntensity, GrainSize, GrainLumContrib;
        public static ConfigEntry<bool> GrainColored;

        // Lens Distortion
        public static ConfigEntry<bool> LDEnable;
        public static ConfigEntry<float> LDIntensity, LDCenterX, LDCenterY, LDScale;

        // Chromatic Aberration
        public static ConfigEntry<bool> CAEnable;
        public static ConfigEntry<float> CAIntensity;
        public static ConfigEntry<bool> CAFastMode;

        // Motion Blur
        public static ConfigEntry<bool> MBEnable;
        public static ConfigEntry<float> MBShutterAngle;
        public static ConfigEntry<int> MBSampleCount;

        // Vignette (advanced)
        public static ConfigEntry<bool> VignetteEnable;
        public static ConfigEntry<int> VignetteMode; // 0=Classic,1=Masked
        public static ConfigEntry<float> VignetteIntensity, VignetteSmoothness, VignetteRoundness, VignetteCenterX, VignetteCenterY, VignetteOpacity;
        public static ConfigEntry<float> VignetteColorR, VignetteColorG, VignetteColorB;
        public static ConfigEntry<bool> VignetteRounded;

        // Built-in PPSv2 camera effects exposed by the original KKS PPE panel.
        public static ConfigEntry<int> AAMode, SMAAQuality;
        public static ConfigEntry<bool> FXAAFastMode, FXAAKeepAlpha;
        public static ConfigEntry<float> TAAJitterSpread, TAASharpness, TAAStationaryBlending, TAAMotionBlending;
        public static ConfigEntry<bool> FogEnable;
        public static ConfigEntry<FogMode> FogModeSelected;
        public static ConfigEntry<float> FogDensity, FogStart, FogEnd, FogHeight;
        public static ConfigEntry<float> FogColorR, FogColorG, FogColorB;

        // Master toggle for color overrides (default OFF = do not touch PPE panel values)
        public static ConfigEntry<bool> EnableColorOverrides;
        public static ConfigEntry<bool> EnableEffectOverrides;
        public static ConfigEntry<bool> EnableCameraOverrides;
        public static ConfigEntry<bool> UnifiedPanelMode;
        public static ConfigEntry<bool> VerboseDiagnostics;
        public static ConfigEntry<bool> CopyOriginalOnOwnership;

        // HDR-safe secondary curves: the only curves PPSv2 applies in HighDefinitionRange mode.
        // 8 hue bands for Hue-vs-Sat / Hue-vs-Hue, 3 points for Lum-vs-Sat / Sat-vs-Sat.
        const int HueBandCount = 8;
        static readonly string[] HueBandNames = { "Red", "Orange", "Yellow", "Green", "Cyan", "Blue", "Purple", "Magenta" };
        static readonly float[] HueBandPos = { 0f, 30f / 360f, 60f / 360f, 120f / 360f, 180f / 360f, 240f / 360f, 270f / 360f, 300f / 360f };
        public static ConfigEntry<float>[] HueSat, HueHue;
        public static ConfigEntry<float> LumSatShadows, LumSatMid, LumSatHigh, SatSatLow, SatSatMid, SatSatHigh;

        // UI
        public static ConfigEntry<float> UIScale;
        public static ConfigEntry<KeyboardShortcut> ToggleKey;

        private bool _showWindow;
        private Rect _windowRect = new Rect(20, 20, 360, 650);
        private int _windowId;
        private Vector2 _scroll;
        private int _tab;

        private void Awake()
        {
            _instance = this;
            _log = Logger;
            _windowId = new System.Random().Next(10000, 99999);

            MSVOthickness = Cfg("MSVO", "Thickness", 1.5f);
            MSVOdirectLight = Cfg("MSVO", "DirectLight", 0.2f);
            MSVOambientOnly = CfgB("MSVO", "AmbientOnly", false);
            MSVOnoiseTol = Cfg("MSVO", "NoiseTol", 0f);
            MSVOblurTol = Cfg("MSVO", "BlurTol", -4.6f);
            MSVOupsampleTol = Cfg("MSVO", "UpsampleTol", -12f);

            CurvePreset = Config.Bind("Curves", "Preset", 0);
            CurveStrength = CfgR("Curves", "Strength", 0.5f, 0f, 1f);
            CurveBlackLift = CfgR("Curves", "BlackLift", 0f, 0f, 0.3f);
            CurveWhiteCrush = CfgR("Curves", "WhiteCrush", 1f, 0.7f, 1f);
            CurveRedOff = CfgR("Curves", "RedOffset", 0f, -0.2f, 0.2f);
            CurveGreenOff = CfgR("Curves", "GreenOffset", 0f, -0.2f, 0.2f);
            CurveBlueOff = CfgR("Curves", "BlueOffset", 0f, -0.2f, 0.2f);

            HueSat = new ConfigEntry<float>[HueBandCount];
            HueHue = new ConfigEntry<float>[HueBandCount];
            for (int i = 0; i < HueBandCount; i++)
            {
                HueSat[i] = CfgR("HdrCurves", "HueVsSat_" + HueBandNames[i], 0f, -1f, 1f);
                HueHue[i] = CfgR("HdrCurves", "HueVsHue_" + HueBandNames[i], 0f, -60f, 60f);
            }
            LumSatShadows = CfgR("HdrCurves", "LumVsSat_Shadows", 0f, -1f, 1f);
            LumSatMid = CfgR("HdrCurves", "LumVsSat_Midtones", 0f, -1f, 1f);
            LumSatHigh = CfgR("HdrCurves", "LumVsSat_Highlights", 0f, -1f, 1f);
            SatSatLow = CfgR("HdrCurves", "SatVsSat_LowSat", 0f, -1f, 1f);
            SatSatMid = CfgR("HdrCurves", "SatVsSat_MidSat", 0f, -1f, 1f);
            SatSatHigh = CfgR("HdrCurves", "SatVsSat_HighSat", 0f, -1f, 1f);

            MixRR = CfgR("Mixer", "R_R", 1f, -2f, 2f); MixRG = CfgR("Mixer", "R_G", 0f, -2f, 2f); MixRB = CfgR("Mixer", "R_B", 0f, -2f, 2f);
            MixGR = CfgR("Mixer", "G_R", 0f, -2f, 2f); MixGG = CfgR("Mixer", "G_G", 1f, -2f, 2f); MixGB = CfgR("Mixer", "G_B", 0f, -2f, 2f);
            MixBR = CfgR("Mixer", "B_R", 0f, -2f, 2f); MixBG = CfgR("Mixer", "B_G", 0f, -2f, 2f); MixBB = CfgR("Mixer", "B_B", 1f, -2f, 2f);

            CTtoeS = CfgR("CTone", "ToeStrength", 0f, 0f, 1f);
            CTtoeL = CfgR("CTone", "ToeLength", 0f, 0f, 1f);
            CTshS = CfgR("CTone", "ShoulderStrength", 0f, 0f, 1f);
            CTshL = CfgR("CTone", "ShoulderLength", 0f, 0f, 1f);
            CTshA = CfgR("CTone", "ShoulderAngle", 0f, 0f, 1f);
            CTgamma = CfgR("CTone", "Gamma", 1f, 0.1f, 3f);

            AEenable = CfgB("AutoExposure", "Enable", false);
            AEmode = Config.Bind("AutoExposure", "Mode", 0, "0=Fixed, 1=Progressive");
            AEminLum = CfgR("AutoExposure", "MinLuminance", -2f, -10f, 0f);
            AEmaxLum = CfgR("AutoExposure", "MaxLuminance", 1f, 0f, 10f);
            AEkey = CfgR("AutoExposure", "KeyValue", 0.25f, 0.05f, 1f);
            AEspeedUp = CfgR("AutoExposure", "SpeedUp Light->Dark", 1f, 0f, 10f);
            AEspeedDown = CfgR("AutoExposure", "SpeedDown Dark->Light", 3f, 0f, 10f);
            AEfilterMin = CfgR("AutoExposure", "FilterMin", -5f, -10f, 0f);
            AEfilterMax = CfgR("AutoExposure", "FilterMax", 5f, 0f, 10f);

            SSRenable = CfgB("SSR", "Enable", false);
            SSRForceDeferred = CfgB("SSR", "ForceDeferredForSSR", false);
            SSRpreset = Config.Bind("SSR", "Preset", 2, "0=Lower 1=Low 2=Medium 3=High 4=Higher 5=Ultra 6=Overkill 7=Custom");
            SSRthickness = CfgR("SSR", "Thickness", 8f, 1f, 64f);
            SSRmaxDist = CfgR("SSR", "MaxMarchDistance", 50f, 1f, 200f);
            SSRdistFade = CfgR("SSR", "DistanceFade", 0.5f, 0f, 1f);
            SSRvignette = CfgR("SSR", "Vignette", 0.5f, 0f, 1f);
            SSRiterations = CfgR("SSR", "MaxIterations", 32f, 4f, 256f);
            SSRresolution = Config.Bind("SSR", "Resolution", 1, "0=Downsampled 1=FullSize 2=Supersampled");
            // Migrate values written by pre-2.1 builds into the PPSv2 ranges.
            if (SSRthickness.Value < 1f) SSRthickness.Value = 8f;
            SSRdistFade.Value = Mathf.Clamp01(SSRdistFade.Value);
            SSRvignette.Value = Mathf.Clamp01(SSRvignette.Value);
            SSRiterations.Value = Mathf.Clamp(SSRiterations.Value, 4f, 256f);

            // Bloom
            BloomEnable = CfgB("Bloom", "Enable", false);
            BloomIntensity = CfgR("Bloom", "Intensity", 0.5f, 0f, 10f);
            BloomThreshold = CfgR("Bloom", "Threshold", 1.1f, 0f, 4f);
            BloomSoftKnee = CfgR("Bloom", "SoftKnee", 0.5f, 0f, 1f);
            BloomClamp = CfgR("Bloom", "Clamp", 65472f, 0f, 65472f);
            BloomDiffusion = CfgR("Bloom", "Diffusion", 7f, 1f, 20f);
            BloomAnamorphic = CfgR("Bloom", "AnamorphicRatio", 0f, -1f, 1f);
            BloomFastMode = CfgB("Bloom", "FastMode", false);
            BloomDirtIntensity = CfgR("Bloom", "DirtIntensity", 0f, 0f, 10f);
            BloomColorR = CfgR("Bloom", "ColorR", 1f, 0f, 2f);
            BloomColorG = CfgR("Bloom", "ColorG", 1f, 0f, 2f);
            BloomColorB = CfgR("Bloom", "ColorB", 1f, 0f, 2f);

            // Depth of Field
            DoFEnable = CfgB("DoF", "Enable", false);
            DoFFocusDistance = CfgR("DoF", "FocusDistance", 10f, 0.1f, 100f);
            DoFAperture = CfgR("DoF", "Aperture", 5.6f, 0.1f, 32f);
            DoFFocalLength = CfgR("DoF", "FocalLength", 50f, 1f, 300f);
            DoFMaxBlur = Config.Bind("DoF", "MaxBlurSize", 1, "0=Small 1=Medium 2=Large 3=VeryLarge");

            // Grain
            GrainEnable = CfgB("Grain", "Enable", false);
            GrainIntensity = CfgR("Grain", "Intensity", 0.5f, 0f, 1f);
            GrainColored = CfgB("Grain", "Colored", true);
            GrainSize = CfgR("Grain", "Size", 1f, 0.5f, 3f);
            GrainLumContrib = CfgR("Grain", "LumContrib", 0.8f, 0f, 1f);

            // Lens Distortion
            LDEnable = CfgB("LensDistortion", "Enable", false);
            LDIntensity = CfgR("LensDistortion", "Intensity", 0f, -100f, 100f);
            LDCenterX = CfgR("LensDistortion", "CenterX", 0.5f, 0f, 1f);
            LDCenterY = CfgR("LensDistortion", "CenterY", 0.5f, 0f, 1f);
            LDScale = CfgR("LensDistortion", "Scale", 1f, 0.1f, 5f);

            // Chromatic Aberration
            CAEnable = CfgB("ChromaticAberration", "Enable", false);
            CAIntensity = CfgR("ChromaticAberration", "Intensity", 0f, 0f, 1f);
            CAFastMode = CfgB("ChromaticAberration", "FastMode", true);

            // Motion Blur
            MBEnable = CfgB("MotionBlur", "Enable", false);
            MBShutterAngle = CfgR("MotionBlur", "ShutterAngle", 270f, 0f, 360f);
            MBSampleCount = Config.Bind("MotionBlur", "SampleCount", 10, "4-32");

            // Vignette
            VignetteEnable = CfgB("Vignette", "Enable", false);
            VignetteMode = Config.Bind("Vignette", "Mode", 0, "0=Classic 1=Masked");
            VignetteIntensity = CfgR("Vignette", "Intensity", 0.4f, 0f, 1f);
            VignetteSmoothness = CfgR("Vignette", "Smoothness", 0.2f, 0f, 1f);
            VignetteRoundness = CfgR("Vignette", "Roundness", 1f, 0f, 1f);
            VignetteCenterX = CfgR("Vignette", "CenterX", 0.5f, 0f, 1f);
            VignetteCenterY = CfgR("Vignette", "CenterY", 0.5f, 0f, 1f);
            VignetteOpacity = CfgR("Vignette", "Opacity", 1f, 0f, 1f);
            VignetteRounded = CfgB("Vignette", "Rounded", false);
            VignetteColorR = CfgR("Vignette", "ColorR", 0f, 0f, 1f);
            VignetteColorG = CfgR("Vignette", "ColorG", 0f, 0f, 1f);
            VignetteColorB = CfgR("Vignette", "ColorB", 0f, 0f, 1f);

            AAMode = Config.Bind("AntiAliasing", "Mode", 0, "0=None 1=FXAA 2=SMAA 3=TAA");
            SMAAQuality = Config.Bind("AntiAliasing", "SMAAQuality", 1, "0=Low 1=Medium 2=High");
            FXAAFastMode = CfgB("AntiAliasing", "FXAAFastMode", false);
            FXAAKeepAlpha = CfgB("AntiAliasing", "FXAAKeepAlpha", false);
            TAAJitterSpread = CfgR("AntiAliasing", "TAAJitterSpread", 0.75f, 0.1f, 1f);
            TAASharpness = CfgR("AntiAliasing", "TAASharpness", 0.3f, 0f, 3f);
            TAAStationaryBlending = CfgR("AntiAliasing", "TAAStationaryBlending", 0.95f, 0f, 0.99f);
            TAAMotionBlending = CfgR("AntiAliasing", "TAAMotionBlending", 0.85f, 0f, 0.99f);

            FogEnable = CfgB("Fog", "Enable", false);
            FogModeSelected = Config.Bind("Fog", "Mode", FogMode.ExponentialSquared, "Unity fog mode");
            FogDensity = CfgR("Fog", "Density", 1f, 0f, 100f);
            FogStart = CfgR("Fog", "Start", 1f, 0f, 100f);
            FogEnd = CfgR("Fog", "End", 20f, 0f, 100f);
            FogHeight = CfgR("Fog", "Height", 20f, 0f, 100f);
            FogColorR = CfgR("Fog", "ColorR", 1f, 0f, 1f);
            FogColorG = CfgR("Fog", "ColorG", 1f, 0f, 1f);
            FogColorB = CfgR("Fog", "ColorB", 1f, 0f, 1f);

            UIScale = CfgR("UI", "Scale", 1f, 0.5f, 2f);
            ToggleKey = Config.Bind("UI", "ToggleWindow", new KeyboardShortcut(KeyCode.P, KeyCode.LeftControl));
            EnableColorOverrides = CfgB("General", "EnableColorOverrides", false);
            EnableEffectOverrides = CfgB("General", "EnableEffectOverrides", false);
            EnableCameraOverrides = CfgB("General", "EnableCameraOverrides", false);
            UnifiedPanelMode = CfgB("General", "UnifiedPanelMode", false);
            VerboseDiagnostics = CfgB("General", "VerboseDiagnostics", false);
            CopyOriginalOnOwnership = CfgB("General", "CopyOriginalOnOwnership", true);

            _entriesByKey = new Dictionary<string, ConfigEntryBase>();
            foreach (var kv in Config) _entriesByKey[kv.Key.Section + "." + kv.Key.Key] = kv.Value;
            Config.SettingChanged += (s, e) =>
            {
                if (e.ChangedSetting != null && (e.ChangedSetting.Definition.Section == "Curves" || e.ChangedSetting.Definition.Section == "HdrCurves")) _curvesDirty = true;
            };

            _ppeType = AccessTools.TypeByName("PostProcessingEffectsV3.PostProcessingEffectsV3");
            if (_ppeType == null) { Logger.LogError("PPE type not found"); return; }

            _aoMode = GM(_ppeType, "AOmode"); _aoModeSel = GM(_ppeType, "AOmodesel");
            _aoFold = GM(_ppeType, "AOb"); _aoObj = GM(_ppeType, "AO");
            _cgFold = GM(_ppeType, "CGb"); _cgObj = GM(_ppeType, "CG");
            _lift = GM(_ppeType, "CGlift"); _gamma = GM(_ppeType, "CGgamma"); _gain = GM(_ppeType, "CGgain");
            _toneMap = GM(_ppeType, "CGtoneMapper"); _ppVolume = GM(_ppeType, "postProcessVolume"); _ppLayer = GM(_ppeType, "postProcessLayer");
            _onoff = GM(_ppeType, "onoff");
            _cgEnable = GM(_ppeType, "CGenable");
            Logger.LogInfo("[PPE Ext] Members: volume=" + (_ppVolume != null) + " layer=" + (_ppLayer != null) + " cg=" + (_cgObj != null) + " ao=" + (_aoObj != null) + " onoff=" + (_onoff != null));

            _harmony = new Harmony("com.user.ppe_extended");
            var upd = AccessTools.Method(_ppeType, "Update");
            if (upd != null) _harmony.Patch(upd, postfix: new HarmonyMethod(typeof(PPEExtended), nameof(UpdatePostfix)));
            Logger.LogInfo("[PPE Ext] Update patched: " + (upd != null));
            var originalGui = AccessTools.Method(_ppeType, "OnGUI");
            if (originalGui != null) _harmony.Patch(originalGui, prefix: new HarmonyMethod(typeof(PPEExtended), nameof(OriginalOnGUIPrefix)));

            try
            {
                if (KKAPI.Studio.StudioAPI.InsideStudio)
                {
                    KKAPI.Studio.SaveLoad.StudioSaveLoadApi.RegisterExtraBehaviour<PpeExtSceneController>(GUID);
                    Logger.LogInfo("[PPE Ext] Scene save/load controller registered");
                }
            }
            catch (Exception e) { Logger.LogWarning("[PPE Ext] Scene controller registration failed: " + e.Message); }

            // Detect render path (logging only, does not disable anything)
            try
            {
                var cam = Camera.main;
                if (cam != null)
                {
                    _isForwardRendering = cam.renderingPath == RenderingPath.Forward || cam.renderingPath == RenderingPath.UsePlayerSettings;
                    _renderPathInfo = cam.renderingPath.ToString();
                }
                else
                {
                    _renderPathInfo = "No main camera (waiting for scene)";
                }
                Logger.LogInfo($"[PPE Ext] Render path: {_renderPathInfo}");
            }
            catch (Exception e)
            {
                Logger.LogWarning("[PPE Ext] Render path detection failed: " + e.Message);
            }

            Logger.LogInfo("PPE Extended v" + Version + " loaded - Ctrl+P to open panel");
        }

        private void Update()
        {
            if (ToggleKey.Value.IsDown()) _showWindow = !_showWindow;
            // Drive the per-frame work from our own Update instead of a Harmony postfix on the
            // original's Update: the original only writes on changes, so ordering is irrelevant,
            // and this keeps working even if patches on the original never fire.
            var bound = _boundPpe as UnityEngine.Object;
            object ppe = (bound != null) ? _boundPpe : GetPPE();
            if (ppe != null) Tick(ppe);
        }

        private void OnGUI()
        {
            if (!_showWindow) return;
            float s = UIScale.Value;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(s, s, 1f));
            _windowRect = GUILayout.Window(_windowId, _windowRect, DrawWindow, "PPE Extended v" + Version + "  [Ctrl+P]", GUILayout.Width(360));
            GUI.matrix = Matrix4x4.identity;
        }

        private void DrawWindow(int id)
        {
            _scroll = GUILayout.BeginScrollView(_scroll);
            object ppe = GetPPE();
            if (ppe == null) { GUILayout.Label("PPE instance not found"); GUILayout.EndScrollView(); GUI.DragWindow(); return; }

            GUILayout.BeginHorizontal();
            GUILayout.Label("Panel Scale:", GUILayout.Width(60));
            UIScale.Value = GUILayout.HorizontalSlider(UIScale.Value, 0.5f, 2f, GUILayout.Width(150));
            GUILayout.Label(UIScale.Value.ToString("F1") + "x", GUILayout.Width(35));
            GUILayout.EndHorizontal();
            GUILayout.Label(StatusLine(ppe), GUILayout.Width(340));
            GUILayout.Space(3);

            bool co = GUILayout.Toggle(EnableColorOverrides.Value, "  Enable Color Overrides (Curves/Mixer/CustomTone)");
            if (co != EnableColorOverrides.Value) EnableColorOverrides.Value = co;
            if (!EnableColorOverrides.Value)
                GUILayout.Label("Color overrides OFF - PPE panel values are untouched", GUILayout.Width(340));
            bool eo = GUILayout.Toggle(EnableEffectOverrides.Value, "  Take Ownership of PPSv2 Effects");
            if (eo != EnableEffectOverrides.Value) EnableEffectOverrides.Value = eo;
            if (!EnableEffectOverrides.Value)
                GUILayout.Label("Compatibility mode - original PPE effect toggles are untouched", GUILayout.Width(340));
            bool cam = GUILayout.Toggle(EnableCameraOverrides.Value, "  Take Ownership of Camera AA/Fog");
            if (cam != EnableCameraOverrides.Value) EnableCameraOverrides.Value = cam;
            bool unified = GUILayout.Toggle(UnifiedPanelMode.Value, "  Unified Panel Mode (hide original PPE panel)");
            if (unified != UnifiedPanelMode.Value) UnifiedPanelMode.Value = unified;
            if (UnifiedPanelMode.Value)
            {
                EnableColorOverrides.Value = true;
                EnableEffectOverrides.Value = true;
                EnableCameraOverrides.Value = true;
                GUILayout.Label("Unified mode: PPE Extended is the single standard PPSv2 controller", GUILayout.Width(340));
            }
            bool copyOn = GUILayout.Toggle(CopyOriginalOnOwnership.Value, "  Start ownership from the original PPE's current values");
            if (copyOn != CopyOriginalOnOwnership.Value) CopyOriginalOnOwnership.Value = copyOn;
            if (GUILayout.Button("Copy Current Original PPE Values Into This Panel", GUILayout.Height(24)))
            {
                CopyEffectsFromOriginal();
                CopyCameraFromOriginal();
            }
            GUILayout.Space(2);
            if (GUILayout.Button("Return Control to Original PPE (release our overrides)", GUILayout.Height(28)))
                ReturnControlToOriginal(ppe);
            GUILayout.Space(3);

            string[] tabs = { "Trackballs", "Curves", "Mixer", "CustomTone", "Bloom", "DoF", "Grain", "Lens", "CA", "Blur", "Vignette", "SSR", "MSVO", "AutoExp", "AA", "Fog", "Presets" };
            _tab = GUILayout.SelectionGrid(_tab, tabs, 6, GUI.skin.button);

            if (_tab == 0) DrawTrackballs(ppe);
            else if (_tab == 1) DrawCurves(ppe);
            else if (_tab == 2) DrawMixer(ppe);
            else if (_tab == 3) DrawCustomTone(ppe);
            else if (_tab == 4) DrawBloom();
            else if (_tab == 5) DrawDoF();
            else if (_tab == 6) DrawGrain();
            else if (_tab == 7) DrawLensDistortion();
            else if (_tab == 8) DrawCA();
            else if (_tab == 9) DrawMotionBlur();
            else if (_tab == 10) DrawVignette();
            else if (_tab == 11) DrawSSR();
            else if (_tab == 12) DrawMSVO(ppe);
            else if (_tab == 13) DrawAutoExposure();
            else if (_tab == 14) DrawAntiAliasing();
            else if (_tab == 15) DrawFog();
            else if (_tab == 16) DrawPresets();

            GUILayout.Space(8);
            GUILayout.EndScrollView();
            GUI.DragWindow();
        }

        void DrawTrackballs(object ppe)
        {
            Section("Trackballs", "Lift=Shadows  Gamma=Midtones  Gain=Highlights");
            DrawColorGradingHint(ppe);
            TB("Lift (Shadows)", _lift, ppe);
            TB("Gamma (Midtones)", _gamma, ppe);
            TB("Gain (Highlights)", _gain, ppe);
            GUILayout.Space(3);
            TwoBtn("Reset Neutral", () => { SV4(_lift, ppe, V4(1,1,1,0)); SV4(_gamma, ppe, V4(1,1,1,0)); SV4(_gain, ppe, V4(1,1,1,0)); },
                    "Cinematic", () => { SV4(_lift, ppe, V4(0.92f,0.96f,1.05f,-0.04f)); SV4(_gamma, ppe, V4(1.03f,1.01f,0.97f,0.03f)); SV4(_gain, ppe, V4(1.02f,1f,0.98f,-0.02f)); });
        }

        void DrawCurves(object ppe)
        {
            var cg = _cgObj != null ? GMV(_cgObj, ppe) as ColorGrading : null;
            bool hdr = cg == null || cg.gradingMode == null || cg.gradingMode.value != GradingMode.LowDefinitionRange;
            Section("Curves", hdr
                ? "HDR grading: PPSv2 applies only the Hue/Sat/Lum curves below. Master/RGB curves are LDR-only."
                : "LDR grading: all curves below are active.");
            DrawColorGradingHint(ppe);
            SubSection("Hue vs Saturation  (-1 = gray, +1 = double)");
            for (int i = 0; i < HueBandCount; i++) Slider(HueBandNames[i], -1f, 1f, HueSat[i]);
            SubSection("Hue vs Hue  (shift in degrees)");
            for (int i = 0; i < HueBandCount; i++) Slider(HueBandNames[i], -60f, 60f, HueHue[i]);
            SubSection("Luminance vs Saturation");
            Slider("Shadows", -1f, 1f, LumSatShadows);
            Slider("Midtones", -1f, 1f, LumSatMid);
            Slider("Highlights", -1f, 1f, LumSatHigh);
            SubSection("Saturation vs Saturation");
            Slider("Low Sat", -1f, 1f, SatSatLow);
            Slider("Mid Sat", -1f, 1f, SatSatMid);
            Slider("High Sat", -1f, 1f, SatSatHigh);
            OneBtn("Reset Hue/Sat/Lum Curves", () =>
            {
                for (int i = 0; i < HueBandCount; i++) { HueSat[i].Value = 0f; HueHue[i].Value = 0f; }
                LumSatShadows.Value = 0f; LumSatMid.Value = 0f; LumSatHigh.Value = 0f;
                SatSatLow.Value = 0f; SatSatMid.Value = 0f; SatSatHigh.Value = 0f;
            });
            SubSection(hdr ? "Master / RGB curves (LDR mode only - no effect while HDR)" : "Master / RGB curves");
            string[] ps = { "Linear (Default)", "S-Curve (Contrast)", "Strong Contrast", "Film Shoulder", "Faded Matte" };
            CurvePreset.Value = GUILayout.SelectionGrid(CurvePreset.Value, ps, 1, GUI.skin.toggle);
            Slider("Curve Strength", 0, 1, CurveStrength);
            Slider("Black Lift", 0, 0.3f, CurveBlackLift);
            Slider("White Crush", 0.7f, 1, CurveWhiteCrush);
            SubSection("RGB Channel Offset");
            Slider("Red", -0.2f, 0.2f, CurveRedOff);
            Slider("Green", -0.2f, 0.2f, CurveGreenOff);
            Slider("Blue", -0.2f, 0.2f, CurveBlueOff);
            OneBtn("Reset to Linear", () => { CurvePreset.Value=0; CurveStrength.Value=0.5f; CurveBlackLift.Value=0; CurveWhiteCrush.Value=1; CurveRedOff.Value=0; CurveGreenOff.Value=0; CurveBlueOff.Value=0; });
        }

        void DrawMixer(object ppe)
        {
            Section("Color Mixer", "Adjust each output channel RGB input, essential for skin tone / split toning");
            DrawColorGradingHint(ppe);
            SubSection("Red Output");
            Slider("Red <- Red", -2, 2, MixRR); Slider("Red <- Green", -2, 2, MixRG); Slider("Red <- Blue", -2, 2, MixRB);
            SubSection("Green Output");
            Slider("Green <- Red", -2, 2, MixGR); Slider("Green <- Green", -2, 2, MixGG); Slider("Green <- Blue", -2, 2, MixGB);
            SubSection("Blue Output");
            Slider("Blue <- Red", -2, 2, MixBR); Slider("Blue <- Green", -2, 2, MixBG); Slider("Blue <- Blue", -2, 2, MixBB);
            OneBtn("Reset (Identity)", () => { MixRR.Value=1; MixRG.Value=0; MixRB.Value=0; MixGR.Value=0; MixGG.Value=1; MixGB.Value=0; MixBR.Value=0; MixBG.Value=0; MixBB.Value=1; });
        }

        void DrawCustomTone(object ppe)
        {
            Section("Custom Tonemapping", "Set Tonemapper to Custom in PPE panel to take effect");
            var tm = (Tonemapper)GCV(_toneMap, ppe);
            if (tm != Tonemapper.Custom) GUILayout.Label("Current Tonemapper: " + tm + " (set to Custom)", GUILayout.Width(320));
            DrawColorGradingHint(ppe);
            if (tm != Tonemapper.Custom && GUILayout.Button("Set Original Tonemapper to Custom", GUILayout.Height(24)))
                SCV(_toneMap, ppe, Tonemapper.Custom);
            Slider("Toe Strength", 0, 1, CTtoeS);
            Slider("Toe Length", 0, 1, CTtoeL);
            Slider("Shoulder Strength", 0, 1, CTshS);
            Slider("Shoulder Length", 0, 1, CTshL);
            Slider("Shoulder Angle", 0, 1, CTshA);
            Slider("Gamma", 0.1f, 3, CTgamma);
        }

        void DrawAutoExposure()
        {
            Section("Auto Exposure", "PPSv2 standard effect, simulates eye adaptation");
            if (!_aeAvailable)
            {
                GUILayout.Label("Not initialized, click button below (first time only)", GUILayout.Width(320));
                if (GUILayout.Button("Initialize AutoExposure", GUILayout.Height(30)))
                {
                    if (TryInitAutoExposure())
                        AEenable.Value = true;
                }
                return;
            }
            bool newVal = GUILayout.Toggle(AEenable.Value, "  Enable Auto Exposure");
            if (newVal != AEenable.Value) AEenable.Value = newVal;
            if (AEenable.Value)
            {
                string[] ms = { "Fixed", "Progressive" };
                AEmode.Value = GUILayout.SelectionGrid(AEmode.Value, ms, 2, GUI.skin.toggle);
                Slider("Min Luminance", -10, 0, AEminLum);
                Slider("Max Luminance", 0, 10, AEmaxLum);
                Slider("Key Value", 0.05f, 1, AEkey);
                SubSection("Histogram Filtering");
                Slider("Filter Min", -10, 0, AEfilterMin);
                Slider("Filter Max", 0, 10, AEfilterMax);
                if (AEmode.Value == 1)
                {
                    SubSection("Adaptation Speed (Progressive)");
                    Slider("Speed Up (Light->Dark)", 0, 10, AEspeedUp);
                    Slider("Speed Down (Dark->Light)", 0, 10, AEspeedDown);
                }
            }
        }

        void DrawSSR()
        {
            Section("Screen Space Reflections", "PPSv2 SSR requires a Deferred G-buffer; KKS normally uses Forward rendering");
            var activePath = _boundCamera != null ? _boundCamera.actualRenderingPath : RenderingPath.UsePlayerSettings;
            if (_boundCamera != null)
                GUILayout.Label("Camera path: " + activePath, GUILayout.Width(320));
            bool forceDeferred = GUILayout.Toggle(SSRForceDeferred.Value, "  Force Deferred path while SSR is enabled");
            if (forceDeferred != SSRForceDeferred.Value)
            {
                SSRForceDeferred.Value = forceDeferred;
                if (!forceDeferred) ReleaseSSRRenderingPath();
            }
            if (activePath != RenderingPath.DeferredShading && !SSRForceDeferred.Value)
                GUILayout.Label("SSR is unavailable in Forward mode. Enable the option above to test Deferred.", GUILayout.Width(320));
            if (!_ssrAvailable)
            {
                GUILayout.Label("Not initialized, click button below (first time only)", GUILayout.Width(320));
                if (GUILayout.Button("Initialize SSR", GUILayout.Height(30)))
                {
                    if (TryInitSSR())
                        SSRenable.Value = true;
                }
                return;
            }
            bool newVal = GUILayout.Toggle(SSRenable.Value, "  Enable SSR");
            if (newVal != SSRenable.Value) SSRenable.Value = newVal;
            if (SSRenable.Value)
            {
                string[] ps = { "Lower", "Low", "Medium", "High", "Higher", "Ultra", "Overkill", "Custom" };
                SSRpreset.Value = Mathf.Clamp(GUILayout.SelectionGrid(Mathf.Clamp(SSRpreset.Value, 0, 7), ps, 4, GUI.skin.toggle), 0, 7);
                string[] rs = { "Downsampled", "Full Size", "Supersampled" };
                SSRresolution.Value = Mathf.Clamp(GUILayout.SelectionGrid(Mathf.Clamp(SSRresolution.Value, 0, 2), rs, 3, GUI.skin.toggle), 0, 2);
                if (SSRpreset.Value == 7)
                    Slider("Thickness", 1f, 64f, SSRthickness);
                Slider("Max March Distance", 1, 200, SSRmaxDist);
                if (SSRpreset.Value == 7)
                {
                    Slider("Distance Fade", 0, 1, SSRdistFade);
                    Slider("Vignette", 0, 1, SSRvignette);
                    Slider("Max Iterations", 4, 256, SSRiterations);
                }
            }
        }

        void DrawMSVO(object ppe)
        {
            Section("AO / MSVO", "MSVO = Multi-Scale Volumetric Occlusion, PPSv2 high quality AO");
            bool useNew = (bool)GCV(_aoModeSel, ppe);
            if (useNew) { GUILayout.Label("Using New AO Mode (SSAOPro), disable in PPE panel first", GUILayout.Width(320)); }
            else
            {
                var mode = (AmbientOcclusionMode)GCV(_aoMode, ppe);
                GUILayout.BeginHorizontal();
                GUILayout.Label("AO Mode:", GUILayout.Width(60));
                int idx = GUILayout.SelectionGrid((int)mode, new string[] { "ScalableAO", "MSVO" }, 2, GUI.skin.toggle);
                if (idx != (int)mode) SCV(_aoMode, ppe, (AmbientOcclusionMode)idx);
                GUILayout.EndHorizontal();
                if (mode == AmbientOcclusionMode.MultiScaleVolumetricObscurance)
                {
                    Slider("Thickness", 1, 10, MSVOthickness);
                    Slider("Direct Light", 0, 1, MSVOdirectLight);
                    MSVOambientOnly.Value = GUILayout.Toggle(MSVOambientOnly.Value, "  Ambient Only (requires Deferred)");
                    Slider("Noise Tolerance", -8, 0, MSVOnoiseTol);
                    Slider("Blur Tolerance", -8, -1, MSVOblurTol);
                    Slider("Upsample Tolerance", -12, -1, MSVOupsampleTol);
                }
            }
        }

        void DrawAntiAliasing()
        {
            Section("Anti-Aliasing", "PPSv2 PostProcessLayer camera anti-aliasing");
            AAMode.Value = GUILayout.SelectionGrid(AAMode.Value, new[] { "None", "FXAA", "SMAA", "TAA" }, 4, GUI.skin.toggle);
            if (AAMode.Value == 1)
            {
                FXAAFastMode.Value = GUILayout.Toggle(FXAAFastMode.Value, "  FXAA Fast Mode");
                FXAAKeepAlpha.Value = GUILayout.Toggle(FXAAKeepAlpha.Value, "  FXAA Keep Alpha");
            }
            else if (AAMode.Value == 2)
            {
                SMAAQuality.Value = GUILayout.SelectionGrid(SMAAQuality.Value, new[] { "Low", "Medium", "High" }, 3, GUI.skin.toggle);
            }
            else if (AAMode.Value == 3)
            {
                Slider("Jitter Spread", 0.1f, 1f, TAAJitterSpread);
                Slider("Sharpness", 0f, 3f, TAASharpness);
                Slider("Stationary Blend", 0f, 0.99f, TAAStationaryBlending);
                Slider("Motion Blend", 0f, 0.99f, TAAMotionBlending);
            }
            if (_boundLayer == null) GUILayout.Label("Waiting for active PostProcessLayer");
        }

        void DrawFog()
        {
            Section("Fog", "Unity RenderSettings fog and PPSv2 deferred fog");
            FogEnable.Value = GUILayout.Toggle(FogEnable.Value, "  Enable Fog");
            FogModeSelected.Value = (FogMode)GUILayout.SelectionGrid((int)FogModeSelected.Value, new[] { "Linear", "Exp", "Exp2" }, 3, GUI.skin.toggle);
            Slider("Density", 0f, 100f, FogDensity);
            Slider("Start", 0f, 100f, FogStart);
            Slider("End", 0f, 100f, FogEnd);
            Slider("Height", 0f, 100f, FogHeight);
            Slider("Color R", 0f, 1f, FogColorR);
            Slider("Color G", 0f, 1f, FogColorG);
            Slider("Color B", 0f, 1f, FogColorB);
        }

        void DrawBloom()
        {
            Section("Bloom", "PPSv2 full bloom parameters");
            bool en = GUILayout.Toggle(BloomEnable.Value, "  Enable Bloom");
            if (en != BloomEnable.Value) BloomEnable.Value = en;
            if (BloomEnable.Value)
            {
                Slider("Intensity", 0, 10, BloomIntensity);
                Slider("Threshold", 0, 4, BloomThreshold);
                Slider("Soft Knee", 0, 1, BloomSoftKnee);
                Slider("Clamp", 0, 65472, BloomClamp);
                Slider("Diffusion", 1, 20, BloomDiffusion);
                Slider("Anamorphic Ratio", -1, 1, BloomAnamorphic);
                BloomFastMode.Value = GUILayout.Toggle(BloomFastMode.Value, "  Fast Mode (lower quality)");
                Slider("Dirt Intensity", 0, 10, BloomDirtIntensity);
                SubSection("Tint Color");
                Slider("R", 0, 2, BloomColorR);
                Slider("G", 0, 2, BloomColorG);
                Slider("B", 0, 2, BloomColorB);
            }
        }

        void DrawDoF()
        {
            Section("Depth of Field", "PPSv2 Gaussian DOF");
            bool en = GUILayout.Toggle(DoFEnable.Value, "  Enable Depth of Field");
            if (en != DoFEnable.Value) DoFEnable.Value = en;
            if (DoFEnable.Value)
            {
                Slider("Focus Distance", 0.1f, 100, DoFFocusDistance);
                Slider("Aperture (f-stop)", 0.1f, 32, DoFAperture);
                Slider("Focal Length (mm)", 1, 300, DoFFocalLength);
                string[] bs = { "Small", "Medium", "Large", "Very Large" };
                DoFMaxBlur.Value = GUILayout.SelectionGrid(DoFMaxBlur.Value, bs, 4, GUI.skin.toggle);
            }
        }

        void DrawGrain()
        {
            Section("Film Grain", "PPSv2 film grain effect");
            bool en = GUILayout.Toggle(GrainEnable.Value, "  Enable Grain");
            if (en != GrainEnable.Value) GrainEnable.Value = en;
            if (GrainEnable.Value)
            {
                Slider("Intensity", 0, 1, GrainIntensity);
                GrainColored.Value = GUILayout.Toggle(GrainColored.Value, "  Colored Grain");
                Slider("Size", 0.5f, 3, GrainSize);
                Slider("Luminance Contribution", 0, 1, GrainLumContrib);
            }
        }

        void DrawLensDistortion()
        {
            Section("Lens Distortion", "PPSv2 lens distortion");
            bool en = GUILayout.Toggle(LDEnable.Value, "  Enable Lens Distortion");
            if (en != LDEnable.Value) LDEnable.Value = en;
            if (LDEnable.Value)
            {
                Slider("Intensity", -100, 100, LDIntensity);
                Slider("Center X", 0, 1, LDCenterX);
                Slider("Center Y", 0, 1, LDCenterY);
                Slider("Scale", 0.1f, 5, LDScale);
            }
        }

        void DrawCA()
        {
            Section("Chromatic Aberration", "PPSv2 chromatic aberration");
            bool en = GUILayout.Toggle(CAEnable.Value, "  Enable Chromatic Aberration");
            if (en != CAEnable.Value) CAEnable.Value = en;
            if (CAEnable.Value)
            {
                Slider("Intensity", 0, 1, CAIntensity);
                CAFastMode.Value = GUILayout.Toggle(CAFastMode.Value, "  Fast Mode");
            }
        }

        void DrawMotionBlur()
        {
            Section("Motion Blur", "PPSv2 motion blur");
            bool en = GUILayout.Toggle(MBEnable.Value, "  Enable Motion Blur");
            if (en != MBEnable.Value) MBEnable.Value = en;
            if (MBEnable.Value)
            {
                Slider("Shutter Angle", 0, 360, MBShutterAngle);
                GUILayout.Label("Sample Count: " + MBSampleCount.Value);
                MBSampleCount.Value = (int)GUILayout.HorizontalSlider(MBSampleCount.Value, 4, 32);
            }
        }

        void DrawVignette()
        {
            Section("Vignette", "PPSv2 full vignette parameters");
            bool en = GUILayout.Toggle(VignetteEnable.Value, "  Enable Vignette");
            if (en != VignetteEnable.Value) VignetteEnable.Value = en;
            if (VignetteEnable.Value)
            {
                string[] ms = { "Classic", "Masked" };
                VignetteMode.Value = GUILayout.SelectionGrid(VignetteMode.Value, ms, 2, GUI.skin.toggle);
                Slider("Intensity", 0, 1, VignetteIntensity);
                Slider("Smoothness", 0, 1, VignetteSmoothness);
                Slider("Roundness", 0, 1, VignetteRoundness);
                Slider("Center X", 0, 1, VignetteCenterX);
                Slider("Center Y", 0, 1, VignetteCenterY);
                VignetteRounded.Value = GUILayout.Toggle(VignetteRounded.Value, "  Rounded");
                if (VignetteMode.Value == 1)
                    Slider("Mask Opacity", 0, 1, VignetteOpacity);
                SubSection("Vignette Color");
                Slider("R", 0, 1, VignetteColorR);
                Slider("G", 0, 1, VignetteColorG);
                Slider("B", 0, 1, VignetteColorB);
            }
        }

        // Diagnostic only: records whether Harmony patches on the original Update fire at all.
        static void UpdatePostfix(object __instance)
        {
            if (!_postfixSeen) { _postfixSeen = true; _log.LogInfo("[PPE Ext] Harmony postfix on original Update fired"); }
        }

        static void Tick(object ppe)
        {
            try
            {
                if (!_tickSeen) { _tickSeen = true; _log.LogInfo("[PPE Ext] Tick running from own Update"); }
                // KKS recreates its volume when a Studio scene changes. Rebind by
                // profile identity so values reach the active render path.
                if (!RefreshBinding(ppe)) return;
                HandleMasterEdges(ppe);

                if (EnableCameraOverrides.Value)
                {
                    try { ApplyAntiAliasing(); } catch (Exception e) { _log.LogWarning("[PPE Ext] AntiAliasing: " + e.Message); }
                    try { ApplyFog(); } catch (Exception e) { _log.LogWarning("[PPE Ext] Fog: " + e.Message); }
                }
                if (_aeAvailable)
                    try { ApplyAutoExposure(); } catch (Exception e) { _log.LogWarning("[PPE Ext] AutoExposure: " + e.Message); _aeAvailable = false; }

                // MSVO
                var ao = (AmbientOcclusion)GMV(_aoObj, ppe);
                if (ao != null && ao.enabled.value && ao.mode.value == AmbientOcclusionMode.MultiScaleVolumetricObscurance)
                {
                    ao.thicknessModifier.Override(MSVOthickness.Value);
                    ao.directLightingStrength.Override(MSVOdirectLight.Value);
                    ao.ambientOnly.Override(MSVOambientOnly.Value);
                    ao.noiseFilterTolerance.Override(MSVOnoiseTol.Value);
                    ao.blurTolerance.Override(MSVOblurTol.Value);
                    ao.upsampleTolerance.Override(MSVOupsampleTol.Value);
                }

                // ColorGrading — only override when user explicitly enables color overrides
                var cg = (ColorGrading)GMV(_cgObj, ppe);
                if (cg != null && cg.enabled.value && EnableColorOverrides.Value)
                {
                    ApplyCurves(cg);
                    cg.mixerRedOutRedIn.Override(MixRR.Value); cg.mixerRedOutGreenIn.Override(MixRG.Value); cg.mixerRedOutBlueIn.Override(MixRB.Value);
                    cg.mixerGreenOutRedIn.Override(MixGR.Value); cg.mixerGreenOutGreenIn.Override(MixGG.Value); cg.mixerGreenOutBlueIn.Override(MixGB.Value);
                    cg.mixerBlueOutRedIn.Override(MixBR.Value); cg.mixerBlueOutGreenIn.Override(MixBG.Value); cg.mixerBlueOutBlueIn.Override(MixBB.Value);
                    if (cg.tonemapper.value == Tonemapper.Custom)
                    {
                        cg.toneCurveToeStrength.Override(CTtoeS.Value); cg.toneCurveToeLength.Override(CTtoeL.Value);
                        cg.toneCurveShoulderStrength.Override(CTshS.Value); cg.toneCurveShoulderLength.Override(CTshL.Value);
                        cg.toneCurveShoulderAngle.Override(CTshA.Value); cg.toneCurveGamma.Override(CTgamma.Value);
                    }
                }

                // SSR (only init when user enables manually, avoids D3D crash on startup)
                var vol = _boundVolume;
                if (vol != null && vol.profile != null)
                {
                    EnsureAllEffects(vol.profile);

                    // Keep depth texture on while SSR active (scene load may reset it)
                    if (_ssrAvailable && _ssr != null && SSRenable.Value)
                    {
                        var cam = Camera.main;
                        if (cam != null && cam.depthTextureMode != DepthTextureMode.Depth)
                            cam.depthTextureMode = DepthTextureMode.Depth;
                    }
                    if (EnableEffectOverrides.Value)
                    {
                        if (_ssrAvailable && _ssr != null)
                        {
                            try { ApplySSR(); }
                            catch (Exception e) { _log.LogWarning("[PPE Ext] SSR apply error: " + e.Message); _ssrAvailable = false; }
                        }

                        // Only write effect parameters in explicit ownership mode.
                        try { ApplyBloom(); } catch (Exception e) { _log.LogWarning("[PPE Ext] Bloom: " + e.Message); }
                        try { ApplyDoF(); } catch (Exception e) { _log.LogWarning("[PPE Ext] DoF: " + e.Message); }
                        try { ApplyGrain(); } catch (Exception e) { _log.LogWarning("[PPE Ext] Grain: " + e.Message); }
                        try { ApplyLensDistortion(); } catch (Exception e) { _log.LogWarning("[PPE Ext] LensDist: " + e.Message); }
                        try { ApplyCA(); } catch (Exception e) { _log.LogWarning("[PPE Ext] CA: " + e.Message); }
                        try { ApplyMotionBlur(); } catch (Exception e) { _log.LogWarning("[PPE Ext] MotionBlur: " + e.Message); }
                        try { ApplyVignette(); } catch (Exception e) { _log.LogWarning("[PPE Ext] Vignette: " + e.Message); }
                    }
                }
                if (VerboseDiagnostics.Value) EmitDiagnosticsIfDue();
            }
            catch (Exception e)
            {
                _log.LogWarning("[PPE Ext] Tick error: " + e.Message);
            }
        }

        private static bool OriginalOnGUIPrefix()
        {
            // Keep the original PPE renderer/update alive, but suppress only its
            // duplicate IMGUI when the user explicitly selects unified mode.
            return !UnifiedPanelMode.Value;
        }

        private static bool RefreshBinding(object ppe)
        {
            try
            {
                if (ppe == null) return false;
                var volume = (PostProcessVolume)GMV(_ppVolume, ppe);
                var layer = (PostProcessLayer)GMV(_ppLayer, ppe);
                var camera = Camera.main;
                var profile = volume != null ? volume.profile : null;
                if (volume == null || profile == null)
                {
                    if (Time.unscaledTime >= _nextWaitLog)
                    {
                        _nextWaitLog = Time.unscaledTime + 5f;
                        var on = _onoff != null ? GCV(_onoff, ppe) : null;
                        _log.LogInfo("[PPE Ext] Waiting for original PPE volume: volume=" + (volume == null ? "null" : "ok") + " ppeOn=" + (on ?? "?"));
                    }
                    return false;
                }

                bool changed = !ReferenceEquals(_boundPpe, ppe) ||
                               !ReferenceEquals(_boundVolume, volume) ||
                               !ReferenceEquals(_boundProfile, profile) ||
                               !ReferenceEquals(_boundLayer, layer) ||
                               !ReferenceEquals(_boundCamera, camera);
                if (!changed) return true;

                _boundPpe = ppe;
                _boundVolume = volume;
                _boundProfile = profile;
                _boundLayer = layer;
                _boundCamera = camera;
                ReleaseSSRRenderingPath();
                _effectsTried = false;
                _bloom = null; _dof = null; _grain = null; _lensDistortion = null;
                _chromaticAberration = null; _motionBlur = null; _vignette = null;
                _ssr = null; _ssrAvailable = false;
                _curvesDirty = true; _curvesAppliedTo = null;
                EnsureAllEffects(profile);
                _log.LogInfo("[PPE Ext] Rebound: profile='" + profile.name + "' settings=" + profile.settings.Count +
                          " layer=" + (layer == null ? "none" : (layer.enabled ? "enabled" : "disabled")) +
                          " camera=" + (camera == null ? "none" : camera.name + "/" + camera.renderingPath));
                return true;
            }
            catch (Exception e)
            {
                _log.LogWarning("[PPE Ext] Binding refresh failed: " + e.Message);
                return false;
            }
        }

        private static void EmitDiagnosticsIfDue()
        {
            if (Time.unscaledTime < _nextDiagnosticTime) return;
            _nextDiagnosticTime = Time.unscaledTime + 2f;
            try
            {
                var layerState = _boundLayer == null ? "none" : (_boundLayer.enabled ? "enabled" : "disabled");
                var cameraState = _boundCamera == null ? "none" : _boundCamera.name + "/" + _boundCamera.renderingPath;
                _log.LogInfo("[PPE Ext] Bound profile='" + (_boundProfile != null ? _boundProfile.name : "null") +
                          "' settings=" + (_boundProfile != null ? _boundProfile.settings.Count.ToString() : "0") +
                          " layer=" + layerState + " camera=" + cameraState +
                          " aa=" + (_boundLayer != null ? _boundLayer.antialiasingMode.ToString() : "missing") +
                          " fog=" + (FogEnable != null && FogEnable.Value ? "on" : "off") +
                          " bloom=" + (_bloom != null && _bloom.enabled != null ? _bloom.enabled.value.ToString() : "missing") +
                          " dof=" + (_dof != null && _dof.enabled != null ? _dof.enabled.value.ToString() : "missing") +
                          " grain=" + (_grain != null && _grain.enabled != null ? _grain.enabled.value.ToString() : "missing") +
                          " vignette=" + (_vignette != null && _vignette.enabled != null ? _vignette.enabled.value.ToString() : "missing"));
            }
            catch (Exception e) { _log.LogWarning("[PPE Ext] Diagnostics failed: " + e.Message); }
        }

        static void TryEnsureEffects(PostProcessProfile profile)
        {
            // No longer called automatically, only when user enables manually
        }

        // Called when user enables manually, safely init effects
        static bool TryInitAutoExposure()
        {
            if (_aeAvailable) return true;
            try
            {
                object ppe = GetPPE();
                if (ppe == null) return false;
                var vol = (PostProcessVolume)GMV(_ppVolume, ppe);
                if (vol == null || vol.profile == null) return false;

                if (vol.profile.HasSettings<AutoExposure>())
                {
                    vol.profile.TryGetSettings<AutoExposure>(out _autoExposure);
                }
                else
                {
                    _autoExposure = vol.profile.AddSettings<AutoExposure>();
                    if (_autoExposure != null && _autoExposure.enabled != null)
                        _autoExposure.enabled.Override(false);
                }
                _aeAvailable = _autoExposure != null;
                _log.LogInfo("[PPE Ext] AutoExposure initialized: " + _aeAvailable);
                return _aeAvailable;
            }
            catch (Exception e)
            {
                _aeAvailable = false;
                _log.LogWarning("[PPE Ext] AutoExposure init failed: " + e.Message);
                return false;
            }
        }

        static bool TryInitSSR()
        {
            if (_ssrAvailable) return true;
            try
            {
                object ppe = GetPPE();
                if (ppe == null) return false;
                var vol = (PostProcessVolume)GMV(_ppVolume, ppe);
                if (vol == null || vol.profile == null) return false;

                var cam = _boundCamera != null ? _boundCamera : Camera.main;
                if (cam == null) return false;
                if (cam.actualRenderingPath != RenderingPath.DeferredShading)
                {
                    if (!SSRForceDeferred.Value)
                    {
                        _log.LogWarning("[PPE Ext] SSR unavailable: camera rendering path is " + cam.actualRenderingPath + "; enable ForceDeferredForSSR to opt in.");
                        return false;
                    }
                    EnsureSSRRenderingPath(cam);
                    if (cam.actualRenderingPath != RenderingPath.DeferredShading)
                    {
                        _log.LogWarning("[PPE Ext] SSR force-deferred request was not accepted; actual path remains " + cam.actualRenderingPath + ".");
                        return false;
                    }
                }

                // Key fix: SSR needs depth texture, PPE does not enable it by default
                cam.depthTextureMode |= DepthTextureMode.Depth | DepthTextureMode.MotionVectors;
                _log.LogInfo("[PPE Ext] Camera depthTextureMode set to Depth+MotionVectors for SSR");

                if (vol.profile.HasSettings<ScreenSpaceReflections>())
                {
                    vol.profile.TryGetSettings<ScreenSpaceReflections>(out _ssr);
                }
                else
                {
                    _ssr = vol.profile.AddSettings<ScreenSpaceReflections>();
                    if (_ssr != null && _ssr.enabled != null)
                        _ssr.enabled.Override(false);
                }
                _ssrAvailable = _ssr != null;
                _log.LogInfo("[PPE Ext] SSR initialized: " + _ssrAvailable);
                return _ssrAvailable;
            }
            catch (Exception e)
            {
                _ssrAvailable = false;
                _log.LogWarning("[PPE Ext] SSR init failed: " + e.Message);
                return false;
            }
        }

        static void ApplyAutoExposure()
        {
            if (_autoExposure.enabled == null) return;
            _autoExposure.enabled.Override(AEenable.Value);
            if (!AEenable.Value) return;

            if (_autoExposure.eyeAdaptation != null) _autoExposure.eyeAdaptation.Override((EyeAdaptation)AEmode.Value);
            if (_autoExposure.minLuminance != null) _autoExposure.minLuminance.Override(AEminLum.Value);
            if (_autoExposure.maxLuminance != null) _autoExposure.maxLuminance.Override(AEmaxLum.Value);
            if (_autoExposure.keyValue != null) _autoExposure.keyValue.Override(AEkey.Value);
            if (_autoExposure.speedUp != null) _autoExposure.speedUp.Override(AEspeedUp.Value);
            if (_autoExposure.speedDown != null) _autoExposure.speedDown.Override(AEspeedDown.Value);
            if (_autoExposure.filtering != null) _autoExposure.filtering.Override(new Vector2(AEfilterMin.Value, AEfilterMax.Value));
        }

        static void ApplySSR()
        {
            if (_ssr.enabled == null) return;
            var cam = _boundCamera != null ? _boundCamera : Camera.main;
            if (SSRenable.Value && SSRForceDeferred.Value && cam != null)
                EnsureSSRRenderingPath(cam);
            _ssr.enabled.Override(SSRenable.Value);
            if (!SSRenable.Value)
            {
                ReleaseSSRRenderingPath();
                return;
            }

            int preset = Mathf.Clamp(SSRpreset.Value, 0, 7);
            if (_ssr.preset != null) _ssr.preset.Override((ScreenSpaceReflectionPreset)preset);
            if (preset == 7)
            {
                if (_ssr.thickness != null) _ssr.thickness.Override(Mathf.Clamp(SSRthickness.Value, 1f, 64f));
                if (_ssr.distanceFade != null) _ssr.distanceFade.Override(Mathf.Clamp01(SSRdistFade.Value));
                if (_ssr.vignette != null) _ssr.vignette.Override(Mathf.Clamp01(SSRvignette.Value));
                if (_ssr.maximumIterationCount != null) _ssr.maximumIterationCount.Override(Mathf.Clamp((int)SSRiterations.Value, 4, 256));
                if (_ssr.resolution != null) _ssr.resolution.Override((ScreenSpaceReflectionResolution)Mathf.Clamp(SSRresolution.Value, 0, 2));
            }
            if (_ssr.maximumMarchDistance != null) _ssr.maximumMarchDistance.Override(Mathf.Max(0f, SSRmaxDist.Value));
        }

        private static void EnsureSSRRenderingPath(Camera camera)
        {
            if (camera == null || camera.actualRenderingPath == RenderingPath.DeferredShading) return;
            if (!_ssrPathOverridden || _ssrPathCamera != camera)
            {
                _ssrPathCamera = camera;
                _ssrOriginalPath = camera.renderingPath;
                _ssrPathOverridden = true;
                _log.LogInfo("[PPE Ext] SSR forcing camera rendering path " + camera.actualRenderingPath + " -> DeferredShading");
            }
            camera.renderingPath = RenderingPath.DeferredShading;
            camera.depthTextureMode |= DepthTextureMode.Depth | DepthTextureMode.MotionVectors;
        }

        private static void ReleaseSSRRenderingPath()
        {
            if (!_ssrPathOverridden) return;
            if (_ssrPathCamera != null)
            {
                _ssrPathCamera.renderingPath = _ssrOriginalPath;
                _log.LogInfo("[PPE Ext] SSR restored camera rendering path to " + _ssrOriginalPath);
            }
            _ssrPathCamera = null;
            _ssrPathOverridden = false;
        }

        static void ApplyAntiAliasing()
        {
            if (_boundLayer == null) return;
            _boundLayer.antialiasingMode = (PostProcessLayer.Antialiasing)Mathf.Clamp(AAMode.Value, 0, 3);
            _boundLayer.subpixelMorphologicalAntialiasing.quality =
                (SubpixelMorphologicalAntialiasing.Quality)Mathf.Clamp(SMAAQuality.Value, 0, 2);
            _boundLayer.fastApproximateAntialiasing.fastMode = FXAAFastMode.Value;
            _boundLayer.fastApproximateAntialiasing.keepAlpha = FXAAKeepAlpha.Value;
            _boundLayer.temporalAntialiasing.jitterSpread = TAAJitterSpread.Value;
            _boundLayer.temporalAntialiasing.sharpness = TAASharpness.Value;
            _boundLayer.temporalAntialiasing.stationaryBlending = TAAStationaryBlending.Value;
            _boundLayer.temporalAntialiasing.motionBlending = TAAMotionBlending.Value;
        }

        static void ApplyFog()
        {
            var color = new Color(FogColorR.Value, FogColorG.Value, FogColorB.Value, 1f);
            RenderSettings.fog = FogEnable.Value;
            RenderSettings.fogMode = FogModeSelected.Value;
            RenderSettings.fogDensity = FogDensity.Value;
            RenderSettings.fogStartDistance = FogStart.Value;
            RenderSettings.fogEndDistance = FogEnd.Value;
            RenderSettings.fogColor = color;
            if (_boundLayer != null && _boundLayer.fog != null)
            {
                _boundLayer.fog.enabled = FogEnable.Value;
                _boundLayer.fog.excludeSkybox = true;
            }
        }

        static void ApplyCurves(ColorGrading cg)
        {
            // PPSv2 only blends parameters whose overrideState is true; assigning the
            // AnimationCurve alone (as older versions did) never reached the renderer.
            if (_curvesDirty || !ReferenceEquals(_curvesAppliedTo, cg))
            {
                if (cg.masterCurve?.value != null)
                    cg.masterCurve.value.curve = BuildMaster(CurvePreset.Value, CurveStrength.Value, CurveBlackLift.Value, CurveWhiteCrush.Value);
                if (cg.redCurve?.value != null) cg.redCurve.value.curve = BuildOff(CurveRedOff.Value);
                if (cg.greenCurve?.value != null) cg.greenCurve.value.curve = BuildOff(CurveGreenOff.Value);
                if (cg.blueCurve?.value != null) cg.blueCurve.value.curve = BuildOff(CurveBlueOff.Value);
                if (cg.hueVsSatCurve?.value != null) cg.hueVsSatCurve.value.curve = BuildHueBandCurve(HueSat, 0.5f);
                if (cg.hueVsHueCurve?.value != null) cg.hueVsHueCurve.value.curve = BuildHueBandCurve(HueHue, 1f / 360f);
                if (cg.lumVsSatCurve?.value != null) cg.lumVsSatCurve.value.curve = BuildThreePointCurve(LumSatShadows.Value, LumSatMid.Value, LumSatHigh.Value);
                if (cg.satVsSatCurve?.value != null) cg.satVsSatCurve.value.curve = BuildThreePointCurve(SatSatLow.Value, SatSatMid.Value, SatSatHigh.Value);
                _curvesDirty = false;
                _curvesAppliedTo = cg;
            }
            if (cg.masterCurve != null) cg.masterCurve.overrideState = true;
            if (cg.redCurve != null) cg.redCurve.overrideState = true;
            if (cg.greenCurve != null) cg.greenCurve.overrideState = true;
            if (cg.blueCurve != null) cg.blueCurve.overrideState = true;
            if (cg.hueVsSatCurve != null) cg.hueVsSatCurve.overrideState = true;
            if (cg.hueVsHueCurve != null) cg.hueVsHueCurve.overrideState = true;
            if (cg.lumVsSatCurve != null) cg.lumVsSatCurve.overrideState = true;
            if (cg.satVsSatCurve != null) cg.satVsSatCurve.overrideState = true;
        }

        // The original PPE writes its overrides only from Settings() (setup / any of its
        // config values changing), so once we stop writing, our last values would stay
        // baked into the shared profile. On a falling edge of a master switch we clear
        // exactly the parameters we own and ask the original to re-apply its own.
        static void HandleMasterEdges(object ppe)
        {
            bool color = EnableColorOverrides.Value, effect = EnableEffectOverrides.Value, camera = EnableCameraOverrides.Value;
            if (!_mastersTracked)
            {
                _mastersTracked = true;
                _prevColorMaster = color; _prevEffectMaster = effect; _prevCameraMaster = camera;
                return;
            }
            bool released = false;
            if (_prevColorMaster && !color) { ReleaseColorGroup(ppe); released = true; }
            if (_prevEffectMaster && !effect) { ReleaseEffectGroup(); released = true; }
            if (_prevCameraMaster && !camera) released = true; // AA/Fog are plain fields; the original re-applies them
            if (!_prevColorMaster && color) { _curvesDirty = true; EnsureOriginalColorGrading(ppe); }
            if (!_prevEffectMaster && effect && CopyOriginalOnOwnership.Value) CopyEffectsFromOriginal();
            if (!_prevCameraMaster && camera && CopyOriginalOnOwnership.Value) CopyCameraFromOriginal();
            _prevColorMaster = color; _prevEffectMaster = effect; _prevCameraMaster = camera;
            if (released) InvokeOriginalSettings(ppe);
        }

        static void ReturnControlToOriginal(object ppe)
        {
            UnifiedPanelMode.Value = false;
            EnableColorOverrides.Value = false;
            EnableEffectOverrides.Value = false;
            EnableCameraOverrides.Value = false;
            HandleMasterEdges(ppe);
        }

        static void InvokeOriginalSettings(object ppe)
        {
            try
            {
                var m = AccessTools.Method(_ppeType, "Settings");
                if (m == null || ppe == null) return;
                m.Invoke(ppe, null);
                _log.LogInfo("[PPE Ext] Released our overrides; original PPE Settings() re-applied");
            }
            catch (Exception e) { _log.LogWarning("[PPE Ext] Original Settings() invoke failed: " + e.Message); }
        }

        static void Release(ParameterOverride p) { if (p != null) p.overrideState = false; }

        static void ReleaseColorGroup(object ppe)
        {
            var cg = (_cgObj != null && ppe != null) ? GMV(_cgObj, ppe) as ColorGrading : null;
            if (cg == null) return;
            Release(cg.masterCurve); Release(cg.redCurve); Release(cg.greenCurve); Release(cg.blueCurve);
            Release(cg.hueVsSatCurve); Release(cg.hueVsHueCurve); Release(cg.lumVsSatCurve); Release(cg.satVsSatCurve);
            Release(cg.mixerRedOutRedIn); Release(cg.mixerRedOutGreenIn); Release(cg.mixerRedOutBlueIn);
            Release(cg.mixerGreenOutRedIn); Release(cg.mixerGreenOutGreenIn); Release(cg.mixerGreenOutBlueIn);
            Release(cg.mixerBlueOutRedIn); Release(cg.mixerBlueOutGreenIn); Release(cg.mixerBlueOutBlueIn);
            Release(cg.toneCurveToeStrength); Release(cg.toneCurveToeLength);
            Release(cg.toneCurveShoulderStrength); Release(cg.toneCurveShoulderLength);
            Release(cg.toneCurveShoulderAngle); Release(cg.toneCurveGamma);
        }

        static void ReleaseEffectGroup()
        {
            if (_ssr != null)
            {
                Release(_ssr.enabled); Release(_ssr.preset); Release(_ssr.thickness); Release(_ssr.maximumMarchDistance);
                Release(_ssr.distanceFade); Release(_ssr.vignette); Release(_ssr.maximumIterationCount); Release(_ssr.resolution);
            }
            if (_bloom != null)
            {
                Release(_bloom.enabled); Release(_bloom.intensity); Release(_bloom.threshold); Release(_bloom.softKnee); Release(_bloom.clamp);
                Release(_bloom.diffusion); Release(_bloom.anamorphicRatio); Release(_bloom.fastMode); Release(_bloom.dirtIntensity); Release(_bloom.color);
            }
            if (_dof != null) { Release(_dof.enabled); Release(_dof.focusDistance); Release(_dof.aperture); Release(_dof.focalLength); Release(_dof.kernelSize); }
            if (_grain != null) { Release(_grain.enabled); Release(_grain.intensity); Release(_grain.colored); Release(_grain.size); Release(_grain.lumContrib); }
            if (_lensDistortion != null) { Release(_lensDistortion.enabled); Release(_lensDistortion.intensity); Release(_lensDistortion.centerX); Release(_lensDistortion.centerY); Release(_lensDistortion.scale); }
            if (_chromaticAberration != null) { Release(_chromaticAberration.enabled); Release(_chromaticAberration.intensity); Release(_chromaticAberration.fastMode); }
            if (_motionBlur != null) { Release(_motionBlur.enabled); Release(_motionBlur.shutterAngle); Release(_motionBlur.sampleCount); }
            if (_vignette != null)
            {
                Release(_vignette.enabled); Release(_vignette.mode); Release(_vignette.intensity); Release(_vignette.smoothness); Release(_vignette.roundness);
                Release(_vignette.center); Release(_vignette.rounded); Release(_vignette.opacity); Release(_vignette.color);
            }
        }

        static void EnsureAllEffects(PostProcessProfile profile)
        {
            if (_effectsTried) return;
            _effectsTried = true;
            try
            {
                if (!profile.HasSettings<Bloom>()) _bloom = profile.AddSettings<Bloom>();
                else profile.TryGetSettings<Bloom>(out _bloom);

                if (!profile.HasSettings<DepthOfField>()) _dof = profile.AddSettings<DepthOfField>();
                else profile.TryGetSettings<DepthOfField>(out _dof);

                if (!profile.HasSettings<Grain>()) _grain = profile.AddSettings<Grain>();
                else profile.TryGetSettings<Grain>(out _grain);

                if (!profile.HasSettings<LensDistortion>()) _lensDistortion = profile.AddSettings<LensDistortion>();
                else profile.TryGetSettings<LensDistortion>(out _lensDistortion);

                if (!profile.HasSettings<ChromaticAberration>()) _chromaticAberration = profile.AddSettings<ChromaticAberration>();
                else profile.TryGetSettings<ChromaticAberration>(out _chromaticAberration);

                if (!profile.HasSettings<MotionBlur>()) _motionBlur = profile.AddSettings<MotionBlur>();
                else profile.TryGetSettings<MotionBlur>(out _motionBlur);

                if (!profile.HasSettings<Vignette>()) _vignette = profile.AddSettings<Vignette>();
                else profile.TryGetSettings<Vignette>(out _vignette);

                _log.LogInfo("[PPE Ext] All PPSv2 effects ensured in profile");
            }
            catch (Exception e)
            {
                _log.LogWarning("[PPE Ext] EnsureAllEffects failed: " + e.Message);
            }
        }

        static void ApplyBloom()
        {
            if (_bloom == null || _bloom.enabled == null) return;
            _bloom.enabled.Override(BloomEnable.Value);
            if (!BloomEnable.Value) return;
            if (_bloom.intensity != null) _bloom.intensity.Override(BloomIntensity.Value);
            if (_bloom.threshold != null) _bloom.threshold.Override(BloomThreshold.Value);
            if (_bloom.softKnee != null) _bloom.softKnee.Override(BloomSoftKnee.Value);
            if (_bloom.clamp != null) _bloom.clamp.Override(BloomClamp.Value);
            if (_bloom.diffusion != null) _bloom.diffusion.Override(BloomDiffusion.Value);
            if (_bloom.anamorphicRatio != null) _bloom.anamorphicRatio.Override(BloomAnamorphic.Value);
            if (_bloom.fastMode != null) _bloom.fastMode.Override(BloomFastMode.Value);
            if (_bloom.dirtIntensity != null) _bloom.dirtIntensity.Override(BloomDirtIntensity.Value);
            if (_bloom.color != null) _bloom.color.Override(new Color(BloomColorR.Value, BloomColorG.Value, BloomColorB.Value, 1f));
        }

        static void ApplyDoF()
        {
            if (_dof == null || _dof.enabled == null) return;
            _dof.enabled.Override(DoFEnable.Value);
            if (!DoFEnable.Value) return;
            if (_dof.focusDistance != null) _dof.focusDistance.Override(DoFFocusDistance.Value);
            if (_dof.aperture != null) _dof.aperture.Override(DoFAperture.Value);
            if (_dof.focalLength != null) _dof.focalLength.Override(DoFFocalLength.Value);
            if (_dof.kernelSize != null) _dof.kernelSize.Override((KernelSize)DoFMaxBlur.Value);
        }

        static void ApplyGrain()
        {
            if (_grain == null || _grain.enabled == null) return;
            _grain.enabled.Override(GrainEnable.Value);
            if (!GrainEnable.Value) return;
            if (_grain.intensity != null) _grain.intensity.Override(GrainIntensity.Value);
            if (_grain.colored != null) _grain.colored.Override(GrainColored.Value);
            if (_grain.size != null) _grain.size.Override(GrainSize.Value);
            if (_grain.lumContrib != null) _grain.lumContrib.Override(GrainLumContrib.Value);
        }

        static void ApplyLensDistortion()
        {
            if (_lensDistortion == null || _lensDistortion.enabled == null) return;
            _lensDistortion.enabled.Override(LDEnable.Value);
            if (!LDEnable.Value) return;
            if (_lensDistortion.intensity != null) _lensDistortion.intensity.Override(LDIntensity.Value);
            if (_lensDistortion.centerX != null) _lensDistortion.centerX.Override(LDCenterX.Value);
            if (_lensDistortion.centerY != null) _lensDistortion.centerY.Override(LDCenterY.Value);
            if (_lensDistortion.scale != null) _lensDistortion.scale.Override(LDScale.Value);
        }

        static void ApplyCA()
        {
            if (_chromaticAberration == null || _chromaticAberration.enabled == null) return;
            _chromaticAberration.enabled.Override(CAEnable.Value);
            if (!CAEnable.Value) return;
            if (_chromaticAberration.intensity != null) _chromaticAberration.intensity.Override(CAIntensity.Value);
            if (_chromaticAberration.fastMode != null) _chromaticAberration.fastMode.Override(CAFastMode.Value);
        }

        static void ApplyMotionBlur()
        {
            if (_motionBlur == null || _motionBlur.enabled == null) return;
            _motionBlur.enabled.Override(MBEnable.Value);
            if (!MBEnable.Value) return;
            if (_motionBlur.shutterAngle != null) _motionBlur.shutterAngle.Override(MBShutterAngle.Value);
            if (_motionBlur.sampleCount != null) _motionBlur.sampleCount.Override(MBSampleCount.Value);
        }

        static void ApplyVignette()
        {
            if (_vignette == null || _vignette.enabled == null) return;
            _vignette.enabled.Override(VignetteEnable.Value);
            if (!VignetteEnable.Value) return;
            if (_vignette.mode != null) _vignette.mode.Override((VignetteMode)VignetteMode.Value);
            if (_vignette.intensity != null) _vignette.intensity.Override(VignetteIntensity.Value);
            if (_vignette.smoothness != null) _vignette.smoothness.Override(VignetteSmoothness.Value);
            if (_vignette.roundness != null) _vignette.roundness.Override(VignetteRoundness.Value);
            if (_vignette.center != null) _vignette.center.Override(new Vector2(VignetteCenterX.Value, VignetteCenterY.Value));
            if (_vignette.rounded != null) _vignette.rounded.Override(VignetteRounded.Value);
            if (_vignette.opacity != null) _vignette.opacity.Override(VignetteOpacity.Value);
            if (_vignette.color != null) _vignette.color.Override(new Color(VignetteColorR.Value, VignetteColorG.Value, VignetteColorB.Value, 1f));
        }

        // Secondary curves are sampled with 0.5 = neutral: 1.0 doubles saturation (or +180 deg hue), 0 removes it.
        static AnimationCurve BuildHueBandCurve(ConfigEntry<float>[] bands, float scale)
        {
            var c = new AnimationCurve();
            for (int i = 0; i < HueBandCount; i++) c.AddKey(HueBandPos[i], Mathf.Clamp01(0.5f + bands[i].Value * scale));
            c.AddKey(1f, Mathf.Clamp01(0.5f + bands[0].Value * scale)); // wraps Magenta back into Red
            Flatten(c);
            return c;
        }

        static AnimationCurve BuildThreePointCurve(float low, float mid, float high)
        {
            var c = new AnimationCurve();
            c.AddKey(0f, Mathf.Clamp01(0.5f + low * 0.5f));
            c.AddKey(0.5f, Mathf.Clamp01(0.5f + mid * 0.5f));
            c.AddKey(1f, Mathf.Clamp01(0.5f + high * 0.5f));
            Flatten(c);
            return c;
        }

        // Flat tangents: smooth eased bumps between bands without overshoot.
        static void Flatten(AnimationCurve c)
        {
            for (int i = 0; i < c.length; i++) { var k = c.keys[i]; k.inTangent = 0f; k.outTangent = 0f; c.MoveKey(i, k); }
        }

        void DrawColorGradingHint(object ppe)
        {
            var cg = _cgObj != null ? GMV(_cgObj, ppe) as ColorGrading : null;
            if (cg == null || cg.enabled == null || cg.enabled.value) return;
            GUILayout.Label("Color Grading is OFF in the original PPE, so nothing on the color tabs can show.", GUILayout.Width(320));
            if (GUILayout.Button("Enable Color Grading in Original PPE", GUILayout.Height(24))) EnsureOriginalColorGrading(ppe);
        }

        // Turning on color overrides implies color grading; flip the original's switch so the tabs are not dead.
        static void EnsureOriginalColorGrading(object ppe)
        {
            try
            {
                var cg = _cgObj != null && ppe != null ? GMV(_cgObj, ppe) as ColorGrading : null;
                if (cg == null || cg.enabled == null || cg.enabled.value || _cgEnable == null) return;
                SCV(_cgEnable, ppe, true); // the original re-runs Settings() on this config change
                _log.LogInfo("[PPE Ext] Enabled Color Grading in the original PPE (required by the color tabs)");
            }
            catch (Exception e) { _log.LogWarning("[PPE Ext] Could not enable original Color Grading: " + e.Message); }
        }

        // Seeds our effect values from what the original PPE currently renders, so taking
        // ownership does not change the picture until the user moves something.
        static void CopyEffectsFromOriginal()
        {
            var cfg = _instance.Config;
            bool prev = cfg.SaveOnConfigSet;
            cfg.SaveOnConfigSet = false;
            try
            {
                if (_bloom != null)
                {
                    BloomEnable.Value = _bloom.enabled.value; BloomIntensity.Value = _bloom.intensity.value; BloomThreshold.Value = _bloom.threshold.value;
                    BloomSoftKnee.Value = _bloom.softKnee.value; BloomClamp.Value = _bloom.clamp.value; BloomDiffusion.Value = _bloom.diffusion.value;
                    BloomAnamorphic.Value = _bloom.anamorphicRatio.value; BloomFastMode.Value = _bloom.fastMode.value; BloomDirtIntensity.Value = _bloom.dirtIntensity.value;
                    var bc = _bloom.color.value; BloomColorR.Value = bc.r; BloomColorG.Value = bc.g; BloomColorB.Value = bc.b;
                }
                if (_dof != null)
                {
                    DoFEnable.Value = _dof.enabled.value; DoFFocusDistance.Value = _dof.focusDistance.value; DoFAperture.Value = _dof.aperture.value;
                    DoFFocalLength.Value = _dof.focalLength.value; DoFMaxBlur.Value = (int)_dof.kernelSize.value;
                }
                if (_grain != null)
                {
                    GrainEnable.Value = _grain.enabled.value; GrainIntensity.Value = _grain.intensity.value; GrainColored.Value = _grain.colored.value;
                    GrainSize.Value = _grain.size.value; GrainLumContrib.Value = _grain.lumContrib.value;
                }
                if (_lensDistortion != null)
                {
                    LDEnable.Value = _lensDistortion.enabled.value; LDIntensity.Value = _lensDistortion.intensity.value;
                    LDCenterX.Value = _lensDistortion.centerX.value; LDCenterY.Value = _lensDistortion.centerY.value; LDScale.Value = _lensDistortion.scale.value;
                }
                if (_chromaticAberration != null)
                {
                    CAEnable.Value = _chromaticAberration.enabled.value; CAIntensity.Value = _chromaticAberration.intensity.value; CAFastMode.Value = _chromaticAberration.fastMode.value;
                }
                if (_motionBlur != null)
                {
                    MBEnable.Value = _motionBlur.enabled.value; MBShutterAngle.Value = _motionBlur.shutterAngle.value; MBSampleCount.Value = _motionBlur.sampleCount.value;
                }
                if (_vignette != null)
                {
                    VignetteEnable.Value = _vignette.enabled.value; VignetteMode.Value = (int)_vignette.mode.value; VignetteIntensity.Value = _vignette.intensity.value;
                    VignetteSmoothness.Value = _vignette.smoothness.value; VignetteRoundness.Value = _vignette.roundness.value;
                    VignetteCenterX.Value = _vignette.center.value.x; VignetteCenterY.Value = _vignette.center.value.y;
                    VignetteRounded.Value = _vignette.rounded.value; VignetteOpacity.Value = _vignette.opacity.value;
                    var vc = _vignette.color.value; VignetteColorR.Value = vc.r; VignetteColorG.Value = vc.g; VignetteColorB.Value = vc.b;
                }
                _log.LogInfo("[PPE Ext] Copied current effect values from the original PPE");
            }
            catch (Exception e) { _log.LogWarning("[PPE Ext] Copy from original failed: " + e.Message); }
            finally { cfg.SaveOnConfigSet = prev; if (prev) cfg.Save(); }
        }

        static void CopyCameraFromOriginal()
        {
            var cfg = _instance.Config;
            bool prev = cfg.SaveOnConfigSet;
            cfg.SaveOnConfigSet = false;
            try
            {
                if (_boundLayer != null)
                {
                    AAMode.Value = (int)_boundLayer.antialiasingMode;
                    SMAAQuality.Value = (int)_boundLayer.subpixelMorphologicalAntialiasing.quality;
                    FXAAFastMode.Value = _boundLayer.fastApproximateAntialiasing.fastMode;
                    FXAAKeepAlpha.Value = _boundLayer.fastApproximateAntialiasing.keepAlpha;
                    TAAJitterSpread.Value = _boundLayer.temporalAntialiasing.jitterSpread;
                    TAASharpness.Value = _boundLayer.temporalAntialiasing.sharpness;
                    TAAStationaryBlending.Value = _boundLayer.temporalAntialiasing.stationaryBlending;
                    TAAMotionBlending.Value = _boundLayer.temporalAntialiasing.motionBlending;
                }
                FogEnable.Value = RenderSettings.fog;
                FogModeSelected.Value = RenderSettings.fogMode;
                FogDensity.Value = RenderSettings.fogDensity;
                FogStart.Value = RenderSettings.fogStartDistance;
                FogEnd.Value = RenderSettings.fogEndDistance;
                var fc = RenderSettings.fogColor; FogColorR.Value = fc.r; FogColorG.Value = fc.g; FogColorB.Value = fc.b;
                _log.LogInfo("[PPE Ext] Copied current AA/Fog values from the original PPE");
            }
            catch (Exception e) { _log.LogWarning("[PPE Ext] Copy camera values failed: " + e.Message); }
            finally { cfg.SaveOnConfigSet = prev; if (prev) cfg.Save(); }
        }

        static AnimationCurve BuildMaster(int preset, float str, float bl, float wc)
        {
            var c = new AnimationCurve();
            switch (preset)
            {
                case 1: c.AddKey(0,bl); c.AddKey(0.25f,0.25f-str*0.06f); c.AddKey(0.5f,0.5f); c.AddKey(0.75f,0.75f+str*0.06f); c.AddKey(1,wc); break;
                case 2: c.AddKey(0,bl); c.AddKey(0.2f,0.15f-str*0.05f); c.AddKey(0.5f,0.5f); c.AddKey(0.8f,0.85f+str*0.05f); c.AddKey(1,wc); break;
                case 3: c.AddKey(0,bl); c.AddKey(0.4f,0.42f+str*0.03f); c.AddKey(0.7f,0.72f); c.AddKey(0.9f,0.88f-str*0.02f); c.AddKey(1,wc-str*0.05f); break;
                case 4: c.AddKey(0,0.08f+bl); c.AddKey(0.5f,0.5f-str*0.02f); c.AddKey(1,0.92f-(1-wc)*0.1f); break;
                default: c.AddKey(0,bl); c.AddKey(1,wc); break;
            }
            for (int i=0;i<c.length;i++){var k=c.keys[i];k.tangentMode=1;c.MoveKey(i,k);}
            return c;
        }
        static AnimationCurve BuildOff(float o) { var c=new AnimationCurve(); c.AddKey(0,Mathf.Clamp01(o)); c.AddKey(1,Mathf.Clamp01(1+o)); return c; }

        // === Utilities ===
        ConfigEntry<float> Cfg(string s, string n, float v) => Config.Bind(s, n, v);
        ConfigEntry<float> CfgR(string s, string n, float v, float min, float max) => Config.Bind(s, n, v, new ConfigDescription("", new AcceptableValueRange<float>(min, max)));
        ConfigEntry<bool> CfgB(string s, string n, bool v) => Config.Bind(s, n, v);

        static object GetPPE() { if (_ppeType==null) return null; var a=UnityEngine.Object.FindObjectsOfType(_ppeType); return a!=null&&a.Length>0?a[0]:null; }
        static MemberInfo GM(Type t, string n) { var f=t.GetField(n,BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Instance); if(f!=null)return f; return t.GetProperty(n,BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Instance); }
        static object GMV(MemberInfo m, object o) { if(m is FieldInfo f)return f.GetValue(o); if(m is PropertyInfo p)return p.GetValue(o); return null; }
        static object GCV(MemberInfo m, object o) { var e=GMV(m,o); return e?.GetType().GetProperty("Value").GetValue(e); }
        static void SCV(MemberInfo m, object o, object v) { var e=GMV(m,o); e?.GetType().GetProperty("Value").SetValue(e,v); }
        static Vector4 GV4(MemberInfo m, object o) { var v=GCV(m,o); return v!=null?(Vector4)v:V4(1,1,1,0); }
        static void SV4(MemberInfo m, object o, Vector4 v) { SCV(m,o,v); }
        static Vector4 V4(float x,float y,float z,float w)=>new Vector4(x,y,z,w);

        static string StatusLine(object ppe)
        {
            try
            {
                var on = _onoff != null ? GCV(_onoff, ppe) as bool? : null;
                var sb = new StringBuilder();
                sb.Append("Original PPE: ").Append(on == null ? "?" : (on.Value ? "ON" : "OFF (its master hotkey: Keypad /)"));
                sb.Append(" | Bound: ").Append(_boundVolume != null ? "yes" : "no");
                var cg = _cgObj != null ? GMV(_cgObj, ppe) as ColorGrading : null;
                if (cg != null && cg.enabled != null)
                    sb.Append(" | ColorGrading: ").Append(cg.enabled.value ? "ON " : "OFF ").Append(cg.gradingMode.value).Append('/').Append(cg.tonemapper.value);
                return sb.ToString();
            }
            catch (Exception e) { return "Status unavailable: " + e.Message; }
        }

        static void Section(string t, string d) { GUILayout.Space(5); GUILayout.Label(t, GUILayout.Width(320)); GUILayout.Label(d, GUILayout.Width(320)); GUILayout.Space(2); }
        static void SubSection(string t) { GUILayout.Space(3); GUILayout.Label("── " + t + " ──", GUILayout.Width(320)); }
        static void Slider(string label, float min, float max, ConfigEntry<float> cfg)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(140));
            cfg.Value = GUILayout.HorizontalSlider(cfg.Value, min, max, GUILayout.Width(100));
            cfg.Value = FloatField(cfg.Definition.Section + "." + cfg.Definition.Key, cfg.Value, min, max, GUILayout.Width(50));
            GUILayout.EndHorizontal();
        }
        static void OneBtn(string label, System.Action a) { if (GUILayout.Button(label, GUILayout.Height(25))) a(); }
        static void TwoBtn(string l1, System.Action a1, string l2, System.Action a2)
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(l1, GUILayout.Height(25))) a1();
            if (GUILayout.Button(l2, GUILayout.Height(25))) a2();
            GUILayout.EndHorizontal();
        }
        static void TB(string name, MemberInfo m, object o)
        {
            GUILayout.Label(name, GUILayout.Width(320));
            var v=GV4(m,o);
            GUILayout.BeginHorizontal();
            GUILayout.Label("R:",GUILayout.Width(20)); v.x=GUILayout.HorizontalSlider(v.x,0,5,GUILayout.Width(80)); v.x=FloatField(name+".r",v.x,0,5,GUILayout.Width(50));
            GUILayout.Label("G:",GUILayout.Width(20)); v.y=GUILayout.HorizontalSlider(v.y,0,5,GUILayout.Width(80)); v.y=FloatField(name+".g",v.y,0,5,GUILayout.Width(50));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("B:",GUILayout.Width(20)); v.z=GUILayout.HorizontalSlider(v.z,0,5,GUILayout.Width(80)); v.z=FloatField(name+".b",v.z,0,5,GUILayout.Width(50));
            GUILayout.Label("W:",GUILayout.Width(20)); v.w=GUILayout.HorizontalSlider(v.w,-2f,2f,GUILayout.Width(80)); v.w=FloatField(name+".w",v.w,-2f,2f,GUILayout.Width(50));
            GUILayout.EndHorizontal();
            SV4(m,o,v);
        }

        // Focus-aware numeric field. While the field has keyboard focus the raw text being
        // typed is shown (re-formatting it every frame made typing impossible); the value is
        // committed live whenever the text parses and snaps back to F2 when focus leaves.
        static string _editKey, _editBuf;
        static float FloatField(string key, float val, float min, float max, params GUILayoutOption[] opts)
        {
            GUI.SetNextControlName(key);
            bool focused = GUI.GetNameOfFocusedControl() == key;
            string shown = (focused && _editKey == key) ? _editBuf : val.ToString("F2", CultureInfo.InvariantCulture);
            string typed = GUILayout.TextField(shown, opts);
            if (!focused)
            {
                if (_editKey == key) _editKey = null;
                return val;
            }
            if (_editKey != key) { _editKey = key; _editBuf = shown; }
            _editBuf = typed;
            var ev = Event.current;
            if (ev.type == EventType.KeyDown && (ev.keyCode == KeyCode.Return || ev.keyCode == KeyCode.KeypadEnter || ev.keyCode == KeyCode.Escape))
            {
                GUI.FocusControl(null);
                _editKey = null;
            }
            float f;
            if (float.TryParse(typed, NumberStyles.Float, CultureInfo.InvariantCulture, out f))
                return Mathf.Clamp(f, min, max);
            return val;
        }

        // === Presets & Studio scene persistence ===
        static string PresetFolder { get { return Path.Combine(Paths.PluginPath, PresetFolderName); } }

        // Everything except the UI section (panel scale / hotkey) is part of a "look".
        static bool IsPersisted(ConfigDefinition def) { return def.Section != "UI"; }

        // "[Section]" + "Key = value" lines, same shape as the BepInEx cfg so presets stay human-editable.
        static string SerializeConfig()
        {
            var entries = new List<ConfigEntryBase>();
            foreach (var kv in _instance.Config) if (IsPersisted(kv.Key)) entries.Add(kv.Value);
            entries.Sort((a, b) =>
            {
                int c = string.CompareOrdinal(a.Definition.Section, b.Definition.Section);
                return c != 0 ? c : string.CompareOrdinal(a.Definition.Key, b.Definition.Key);
            });
            var sb = new StringBuilder();
            sb.Append("# PPE Extended preset v").Append(Version).Append('\n');
            string section = null;
            foreach (var e in entries)
            {
                if (e.Definition.Section != section)
                {
                    section = e.Definition.Section;
                    sb.Append('\n').Append('[').Append(section).Append("]\n");
                }
                sb.Append(e.Definition.Key).Append(" = ").Append(e.GetSerializedValue()).Append('\n');
            }
            return sb.ToString();
        }

        // Unknown keys are ignored; values BepInEx rejects are logged and skipped.
        static int ApplySerializedConfig(string text)
        {
            if (string.IsNullOrEmpty(text) || _entriesByKey == null) return 0;
            int applied = 0;
            string section = "";
            foreach (var raw in text.Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                if (line[0] == '[' && line[line.Length - 1] == ']') { section = line.Substring(1, line.Length - 2).Trim(); continue; }
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                var key = line.Substring(0, eq).Trim();
                var value = line.Substring(eq + 1).Trim();
                ConfigEntryBase entry;
                if (!_entriesByKey.TryGetValue(section + "." + key, out entry) || !IsPersisted(entry.Definition)) continue;
                try { entry.SetSerializedValue(value); applied++; }
                catch (Exception e) { _log.LogWarning("[PPE Ext] Rejected " + section + "." + key + " = " + value + ": " + e.Message); }
            }
            _curvesDirty = true;
            return applied;
        }

        static void RefreshPresetList()
        {
            try
            {
                if (!Directory.Exists(PresetFolder)) Directory.CreateDirectory(PresetFolder);
                _presetFiles = Directory.GetFiles(PresetFolder, "*.cfg");
                Array.Sort(_presetFiles, StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception e) { _presetFiles = new string[0]; _presetStatus = "List failed: " + e.Message; }
            _presetListLoaded = true;
        }

        static void SavePreset(string name)
        {
            try
            {
                foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
                name = name.Trim();
                if (name.Length == 0) { _presetStatus = "Enter a preset name first"; return; }
                if (!Directory.Exists(PresetFolder)) Directory.CreateDirectory(PresetFolder);
                File.WriteAllText(Path.Combine(PresetFolder, name + ".cfg"), SerializeConfig());
                _presetName = name;
                _presetStatus = "Saved " + name;
                RefreshPresetList();
            }
            catch (Exception e) { _presetStatus = "Save failed: " + e.Message; _log.LogWarning("[PPE Ext] " + _presetStatus); }
        }

        static void LoadPreset(string path)
        {
            try
            {
                int n = ApplySerializedConfig(File.ReadAllText(path));
                _presetName = Path.GetFileNameWithoutExtension(path);
                _presetStatus = "Loaded " + _presetName + " (" + n + " values)";
            }
            catch (Exception e) { _presetStatus = "Load failed: " + e.Message; _log.LogWarning("[PPE Ext] " + _presetStatus); }
        }

        static void DeletePreset(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
                _presetStatus = "Deleted " + Path.GetFileNameWithoutExtension(path);
                RefreshPresetList();
            }
            catch (Exception e) { _presetStatus = "Delete failed: " + e.Message; _log.LogWarning("[PPE Ext] " + _presetStatus); }
        }

        void DrawPresets()
        {
            Section("Presets", "Extension settings only (all tabs + master switches). Original PPE presets are separate.");
            if (!_presetListLoaded) RefreshPresetList();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Name:", GUILayout.Width(45));
            _presetName = GUILayout.TextField(_presetName, GUILayout.Width(170));
            if (GUILayout.Button("Save", GUILayout.Width(55))) SavePreset(_presetName);
            if (GUILayout.Button("Refresh", GUILayout.Width(65))) RefreshPresetList();
            GUILayout.EndHorizontal();
            GUILayout.Label("Folder: BepInEx/plugins/" + PresetFolderName, GUILayout.Width(320));
            GUILayout.Label("These settings are also stored in Studio scene files and restored on load.", GUILayout.Width(320));
            if (!string.IsNullOrEmpty(_presetStatus)) GUILayout.Label(_presetStatus, GUILayout.Width(320));
            GUILayout.Space(4);
            foreach (var path in _presetFiles)
            {
                var name = Path.GetFileNameWithoutExtension(path);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button(name, GUILayout.Width(190))) LoadPreset(path);
                if (_presetPendingDelete == path)
                {
                    if (GUILayout.Button("Confirm", GUILayout.Width(65))) { DeletePreset(path); _presetPendingDelete = null; }
                    if (GUILayout.Button("Cancel", GUILayout.Width(60))) _presetPendingDelete = null;
                }
                else if (GUILayout.Button("Delete", GUILayout.Width(65))) _presetPendingDelete = path;
                GUILayout.EndHorizontal();
            }
        }

        internal sealed class PpeExtSceneController : KKAPI.Studio.SaveLoad.SceneCustomFunctionController
        {
            protected override void OnSceneSave()
            {
                try
                {
                    var data = new PluginData();
                    data.version = 1;
                    data.data[SceneDataKey] = SerializeConfig();
                    SetExtendedData(data);
                }
                catch (Exception e) { _log.LogWarning("[PPE Ext] Scene save failed: " + e.Message); }
            }

            // Same policy as Save_PostProcessingEffects: apply on Load and Import, never on Clear (new scene).
            protected override void OnSceneLoad(KKAPI.Studio.SaveLoad.SceneOperationKind operation, KKAPI.Utilities.ReadOnlyDictionary<int, global::Studio.ObjectCtrlInfo> loadedItems)
            {
                if (operation == KKAPI.Studio.SaveLoad.SceneOperationKind.Clear) return;
                try
                {
                    var data = GetExtendedData();
                    object text;
                    if (data == null || data.data == null || !data.data.TryGetValue(SceneDataKey, out text) || !(text is string)) return;
                    int n = ApplySerializedConfig((string)text);
                    _log.LogInfo("[PPE Ext] Scene " + operation + ": restored " + n + " extension values");
                }
                catch (Exception e) { _log.LogWarning("[PPE Ext] Scene load failed: " + e.Message); }
            }
        }

        private void OnDestroy()
        {
            ReleaseSSRRenderingPath();
            _harmony?.UnpatchSelf();
        }
    }
}
