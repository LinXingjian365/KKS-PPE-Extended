using System;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

namespace PPE_Extended
{
    [BepInPlugin("com.user.ppe_extended", "PPE Extended (Full PPSv2)", "2.0.5")]
    [BepInDependency("org.bepinex.plugins.KKS_PostProcessingEffectsV3")]
    public class PPEExtended : BaseUnityPlugin
    {
        private static Harmony _harmony;
        private static Type _ppeType;
        private static PPEExtended _instance;

        private static MemberInfo _aoMode, _aoModeSel, _aoFold, _aoObj, _cgFold, _lift, _gamma, _gain, _cgObj, _toneMap, _ppVolume, _ppLayer;

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
        private static float _nextDiagnosticTime;

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
            SSRpreset = Config.Bind("SSR", "Preset", 2, "0=Lowest 1=Low 2=Medium 3=High 4=Ultra 5=Overkill");
            SSRthickness = CfgR("SSR", "Thickness", 0.1f, 0.01f, 1f);
            SSRmaxDist = CfgR("SSR", "MaxMarchDistance", 50f, 1f, 200f);
            SSRdistFade = CfgR("SSR", "DistanceFade", 1f, 0f, 10f);
            SSRvignette = CfgR("SSR", "Vignette", 0.5f, 0f, 2f);
            SSRiterations = CfgR("SSR", "MaxIterations", 16f, 4f, 64f);
            SSRresolution = Config.Bind("SSR", "Resolution", 1, "0=Quarter 1=Half 2=Full");

            // Bloom
            BloomEnable = CfgB("Bloom", "Enable", false);
            BloomIntensity = CfgR("Bloom", "Intensity", 0.5f, 0f, 10f);
            BloomThreshold = CfgR("Bloom", "Threshold", 1.1f, 0f, 4f);
            BloomSoftKnee = CfgR("Bloom", "SoftKnee", 0.5f, 0f, 1f);
            BloomClamp = CfgR("Bloom", "Clamp", 6.5f, 0f, 20f);
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

            _ppeType = AccessTools.TypeByName("PostProcessingEffectsV3.PostProcessingEffectsV3");
            if (_ppeType == null) { Logger.LogError("PPE type not found"); return; }

            _aoMode = GM(_ppeType, "AOmode"); _aoModeSel = GM(_ppeType, "AOmodesel");
            _aoFold = GM(_ppeType, "AOb"); _aoObj = GM(_ppeType, "AO");
            _cgFold = GM(_ppeType, "CGb"); _cgObj = GM(_ppeType, "CG");
            _lift = GM(_ppeType, "CGlift"); _gamma = GM(_ppeType, "CGgamma"); _gain = GM(_ppeType, "CGgain");
            _toneMap = GM(_ppeType, "CGtoneMapper"); _ppVolume = GM(_ppeType, "postProcessVolume"); _ppLayer = GM(_ppeType, "postProcessLayer");

            _harmony = new Harmony("com.user.ppe_extended");
            var upd = AccessTools.Method(_ppeType, "Update");
            if (upd != null) _harmony.Patch(upd, postfix: new HarmonyMethod(typeof(PPEExtended), nameof(UpdatePostfix)));
            var originalGui = AccessTools.Method(_ppeType, "OnGUI");
            if (originalGui != null) _harmony.Patch(originalGui, prefix: new HarmonyMethod(typeof(PPEExtended), nameof(OriginalOnGUIPrefix)));

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

            Logger.LogInfo("PPE Extended v2.0.0 loaded - Ctrl+P to open panel");
        }

        private void Update() { if (ToggleKey.Value.IsDown()) _showWindow = !_showWindow; }

        private void OnGUI()
        {
            if (!_showWindow) return;
            float s = UIScale.Value;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(s, s, 1f));
            _windowRect = GUILayout.Window(_windowId, _windowRect, DrawWindow, "PPE Full PPSv2 Color Panel v2.0.4  [Ctrl+P]", GUILayout.Width(360));
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
            GUILayout.Space(3);

            string[] tabs = { "Trackballs", "Curves", "Mixer", "CustomTone", "Bloom", "DoF", "Grain", "Lens", "CA", "Blur", "Vignette", "SSR", "MSVO", "AutoExp", "AA", "Fog" };
            _tab = GUILayout.SelectionGrid(_tab, tabs, 8, GUI.skin.button);

            if (_tab == 0) DrawTrackballs(ppe);
            else if (_tab == 1) DrawCurves();
            else if (_tab == 2) DrawMixer();
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

            GUILayout.Space(8);
            GUILayout.EndScrollView();
            GUI.DragWindow();
        }

        void DrawTrackballs(object ppe)
        {
            Section("Trackballs", "Lift=Shadows  Gamma=Midtones  Gain=Highlights");
            TB("Lift (Shadows)", _lift, ppe);
            TB("Gamma (Midtones)", _gamma, ppe);
            TB("Gain (Highlights)", _gain, ppe);
            GUILayout.Space(3);
            TwoBtn("Reset Neutral", () => { SV4(_lift, ppe, V4(1,1,1,0)); SV4(_gamma, ppe, V4(1,1,1,0)); SV4(_gain, ppe, V4(1,1,1,0)); },
                    "Cinematic", () => { SV4(_lift, ppe, V4(0.92f,0.96f,1.05f,-0.04f)); SV4(_gamma, ppe, V4(1.03f,1.01f,0.97f,0.03f)); SV4(_gain, ppe, V4(1.02f,1f,0.98f,-0.02f)); });
        }

        void DrawCurves()
        {
            Section("Color Curves", "Master curve controls overall contrast, RGB offset for white balance");
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

        void DrawMixer()
        {
            Section("Color Mixer", "Adjust each output channel RGB input, essential for skin tone / split toning");
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
            Section("Screen Space Reflections", "PPSv2 standard effect, requires Forward rendering path");
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
                string[] ps = { "Lowest", "Low", "Medium", "High", "Ultra", "Overkill" };
                SSRpreset.Value = GUILayout.SelectionGrid(SSRpreset.Value, ps, 3, GUI.skin.toggle);
                string[] rs = { "Downsampled", "Full Size", "Supersampled" };
                SSRresolution.Value = GUILayout.SelectionGrid(SSRresolution.Value, rs, 3, GUI.skin.toggle);
                Slider("Thickness", 0.01f, 1, SSRthickness);
                Slider("Max March Distance", 1, 200, SSRmaxDist);
                Slider("Distance Fade", 0, 10, SSRdistFade);
                Slider("Vignette", 0, 2, SSRvignette);
                Slider("Max Iterations", 4, 64, SSRiterations);
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
                Slider("Clamp", 0, 20, BloomClamp);
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

        static void UpdatePostfix(object __instance)
        {
            try
            {
                // KKS recreates its volume when a Studio scene changes. Rebind by
                // profile identity so values reach the active render path.
                if (!RefreshBinding(__instance)) return;

                if (EnableCameraOverrides.Value)
                {
                    try { ApplyAntiAliasing(); } catch (Exception e) { Debug.LogWarning("[PPE Ext] AntiAliasing: " + e.Message); }
                    try { ApplyFog(); } catch (Exception e) { Debug.LogWarning("[PPE Ext] Fog: " + e.Message); }
                }
                if (_aeAvailable)
                    try
                    {
                        // Exposure is opt-in. More importantly, release the PPSv2
                        // override when the camera master switch is off so a
                        // previous session cannot leave the scene nearly black.
                        if (EnableCameraOverrides.Value) ApplyAutoExposure();
                        else ReleaseAutoExposure();
                    }
                    catch (Exception e) { Debug.LogWarning("[PPE Ext] AutoExposure: " + e.Message); _aeAvailable = false; }

                // MSVO
                var ao = (AmbientOcclusion)GMV(_aoObj, __instance);
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
                var cg = (ColorGrading)GMV(_cgObj, __instance);
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
                else if (cg != null)
                {
                    ReleaseColorOverrides(cg);
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
                            catch (Exception e) { Debug.LogWarning("[PPE Ext] SSR apply error: " + e.Message); _ssrAvailable = false; }
                        }

                        // Only write effect parameters in explicit ownership mode.
                        try { ApplyBloom(); } catch (Exception e) { Debug.LogWarning("[PPE Ext] Bloom: " + e.Message); }
                        try { ApplyDoF(); } catch (Exception e) { Debug.LogWarning("[PPE Ext] DoF: " + e.Message); }
                        try { ApplyGrain(); } catch (Exception e) { Debug.LogWarning("[PPE Ext] Grain: " + e.Message); }
                        try { ApplyLensDistortion(); } catch (Exception e) { Debug.LogWarning("[PPE Ext] LensDist: " + e.Message); }
                        try { ApplyCA(); } catch (Exception e) { Debug.LogWarning("[PPE Ext] CA: " + e.Message); }
                        try { ApplyMotionBlur(); } catch (Exception e) { Debug.LogWarning("[PPE Ext] MotionBlur: " + e.Message); }
                        try { ApplyVignette(); } catch (Exception e) { Debug.LogWarning("[PPE Ext] Vignette: " + e.Message); }
                    }
                    else
                    {
                        ReleaseEffectOverrides();
                    }
                }
                EmitDiagnosticsIfDue();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PPE Ext] UpdatePostfix error: " + e.Message);
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
                if (volume == null || profile == null) return false;

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
                _effectsTried = false;
                _bloom = null; _dof = null; _grain = null; _lensDistortion = null;
                _chromaticAberration = null; _motionBlur = null; _vignette = null;
                _ssr = null; _ssrAvailable = false;
                EnsureAllEffects(profile);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PPE Ext] Binding refresh failed: " + e.Message);
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
                Debug.Log("[PPE Ext] Bound profile='" + (_boundProfile != null ? _boundProfile.name : "null") +
                          "' settings=" + (_boundProfile != null ? _boundProfile.settings.Count.ToString() : "0") +
                          " layer=" + layerState + " camera=" + cameraState +
                          " aa=" + (_boundLayer != null ? _boundLayer.antialiasingMode.ToString() : "missing") +
                          " fog=" + (FogEnable != null && FogEnable.Value ? "on" : "off") +
                          " bloom=" + (_bloom != null && _bloom.enabled != null ? _bloom.enabled.value.ToString() : "missing") +
                          " dof=" + (_dof != null && _dof.enabled != null ? _dof.enabled.value.ToString() : "missing") +
                          " grain=" + (_grain != null && _grain.enabled != null ? _grain.enabled.value.ToString() : "missing") +
                          " vignette=" + (_vignette != null && _vignette.enabled != null ? _vignette.enabled.value.ToString() : "missing"));
            }
            catch (Exception e) { Debug.LogWarning("[PPE Ext] Diagnostics failed: " + e.Message); }
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
                Debug.Log("[PPE Ext] AutoExposure initialized: " + _aeAvailable);
                return _aeAvailable;
            }
            catch (Exception e)
            {
                _aeAvailable = false;
                Debug.LogWarning("[PPE Ext] AutoExposure init failed: " + e.Message);
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

                // Key fix: SSR needs depth texture, PPE does not enable it by default
                var cam = Camera.main;
                if (cam != null)
                {
                    cam.depthTextureMode = DepthTextureMode.Depth;
                    Debug.Log("[PPE Ext] Camera depthTextureMode set to Depth for SSR");
                }

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
                Debug.Log("[PPE Ext] SSR initialized: " + _ssrAvailable);
                return _ssrAvailable;
            }
            catch (Exception e)
            {
                _ssrAvailable = false;
                Debug.LogWarning("[PPE Ext] SSR init failed: " + e.Message);
                return false;
            }
        }

        static void ClearOverride(ParameterOverride parameter)
        {
            if (parameter != null) parameter.overrideState = false;
        }

        static void ReleaseAutoExposure()
        {
            if (_autoExposure == null) return;
            ClearOverride(_autoExposure.enabled);
            ClearOverride(_autoExposure.eyeAdaptation);
            ClearOverride(_autoExposure.minLuminance);
            ClearOverride(_autoExposure.maxLuminance);
            ClearOverride(_autoExposure.keyValue);
            ClearOverride(_autoExposure.speedUp);
            ClearOverride(_autoExposure.speedDown);
            ClearOverride(_autoExposure.filtering);
        }

        static void ReleaseColorOverrides(ColorGrading cg)
        {
            ClearOverride(cg.masterCurve); ClearOverride(cg.redCurve);
            ClearOverride(cg.greenCurve); ClearOverride(cg.blueCurve);
            ClearOverride(cg.mixerRedOutRedIn); ClearOverride(cg.mixerRedOutGreenIn); ClearOverride(cg.mixerRedOutBlueIn);
            ClearOverride(cg.mixerGreenOutRedIn); ClearOverride(cg.mixerGreenOutGreenIn); ClearOverride(cg.mixerGreenOutBlueIn);
            ClearOverride(cg.mixerBlueOutRedIn); ClearOverride(cg.mixerBlueOutGreenIn); ClearOverride(cg.mixerBlueOutBlueIn);
            ClearOverride(cg.toneCurveToeStrength); ClearOverride(cg.toneCurveToeLength);
            ClearOverride(cg.toneCurveShoulderStrength); ClearOverride(cg.toneCurveShoulderLength);
            ClearOverride(cg.toneCurveShoulderAngle); ClearOverride(cg.toneCurveGamma);
        }

        static void ReleaseEffectOverrides()
        {
            if (_ssr != null)
            {
                ClearOverride(_ssr.enabled); ClearOverride(_ssr.preset); ClearOverride(_ssr.thickness);
                ClearOverride(_ssr.maximumMarchDistance); ClearOverride(_ssr.distanceFade);
                ClearOverride(_ssr.vignette); ClearOverride(_ssr.maximumIterationCount); ClearOverride(_ssr.resolution);
            }
            if (_bloom != null)
            {
                ClearOverride(_bloom.enabled); ClearOverride(_bloom.intensity); ClearOverride(_bloom.threshold);
                ClearOverride(_bloom.softKnee); ClearOverride(_bloom.clamp); ClearOverride(_bloom.diffusion);
                ClearOverride(_bloom.anamorphicRatio); ClearOverride(_bloom.fastMode);
                ClearOverride(_bloom.dirtIntensity); ClearOverride(_bloom.color);
            }
            if (_dof != null)
            {
                ClearOverride(_dof.enabled); ClearOverride(_dof.focusDistance); ClearOverride(_dof.aperture);
                ClearOverride(_dof.focalLength); ClearOverride(_dof.kernelSize);
            }
            if (_grain != null)
            {
                ClearOverride(_grain.enabled); ClearOverride(_grain.intensity); ClearOverride(_grain.colored);
                ClearOverride(_grain.size); ClearOverride(_grain.lumContrib);
            }
            if (_lensDistortion != null)
            {
                ClearOverride(_lensDistortion.enabled); ClearOverride(_lensDistortion.intensity);
                ClearOverride(_lensDistortion.centerX); ClearOverride(_lensDistortion.centerY); ClearOverride(_lensDistortion.scale);
            }
            if (_chromaticAberration != null)
            {
                ClearOverride(_chromaticAberration.enabled); ClearOverride(_chromaticAberration.intensity);
                ClearOverride(_chromaticAberration.fastMode);
            }
            if (_motionBlur != null)
            {
                ClearOverride(_motionBlur.enabled); ClearOverride(_motionBlur.shutterAngle); ClearOverride(_motionBlur.sampleCount);
            }
            if (_vignette != null)
            {
                ClearOverride(_vignette.enabled); ClearOverride(_vignette.mode); ClearOverride(_vignette.intensity);
                ClearOverride(_vignette.smoothness); ClearOverride(_vignette.roundness); ClearOverride(_vignette.center);
                ClearOverride(_vignette.rounded); ClearOverride(_vignette.opacity); ClearOverride(_vignette.color);
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
            _ssr.enabled.Override(SSRenable.Value);
            if (!SSRenable.Value) return;

            if (_ssr.preset != null) _ssr.preset.Override((ScreenSpaceReflectionPreset)SSRpreset.Value);
            if (_ssr.thickness != null) _ssr.thickness.Override(SSRthickness.Value);
            if (_ssr.maximumMarchDistance != null) _ssr.maximumMarchDistance.Override(SSRmaxDist.Value);
            if (_ssr.distanceFade != null) _ssr.distanceFade.Override(SSRdistFade.Value);
            if (_ssr.vignette != null) _ssr.vignette.Override(SSRvignette.Value);
            if (_ssr.maximumIterationCount != null) _ssr.maximumIterationCount.Override((int)SSRiterations.Value);
            if (_ssr.resolution != null) _ssr.resolution.Override((ScreenSpaceReflectionResolution)SSRresolution.Value);
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
            if (cg.masterCurve?.value != null)
                cg.masterCurve.value.curve = BuildMaster(CurvePreset.Value, CurveStrength.Value, CurveBlackLift.Value, CurveWhiteCrush.Value);
            if (cg.redCurve?.value != null) cg.redCurve.value.curve = BuildOff(CurveRedOff.Value);
            if (cg.greenCurve?.value != null) cg.greenCurve.value.curve = BuildOff(CurveGreenOff.Value);
            if (cg.blueCurve?.value != null) cg.blueCurve.value.curve = BuildOff(CurveBlueOff.Value);
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

                Debug.Log("[PPE Ext] All PPSv2 effects ensured in profile");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PPE Ext] EnsureAllEffects failed: " + e.Message);
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

        static void Section(string t, string d) { GUILayout.Space(5); GUILayout.Label(t, GUILayout.Width(320)); GUILayout.Label(d, GUILayout.Width(320)); GUILayout.Space(2); }
        static void SubSection(string t) { GUILayout.Space(3); GUILayout.Label("── " + t + " ──", GUILayout.Width(320)); }
        static void Slider(string label, float min, float max, ConfigEntry<float> cfg)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(140));
            cfg.Value = GUILayout.HorizontalSlider(cfg.Value, min, max, GUILayout.Width(120));
            GUILayout.Label(cfg.Value.ToString("F2"), GUILayout.Width(40));
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
            GUILayout.Label("R:",GUILayout.Width(20)); v.x=GUILayout.HorizontalSlider(v.x,0,2,GUILayout.Width(95)); GUILayout.Label(v.x.ToString("F2"),GUILayout.Width(35));
            GUILayout.Label("G:",GUILayout.Width(20)); v.y=GUILayout.HorizontalSlider(v.y,0,2,GUILayout.Width(95)); GUILayout.Label(v.y.ToString("F2"),GUILayout.Width(35));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("B:",GUILayout.Width(20)); v.z=GUILayout.HorizontalSlider(v.z,0,2,GUILayout.Width(95)); GUILayout.Label(v.z.ToString("F2"),GUILayout.Width(35));
            GUILayout.Label("W:",GUILayout.Width(20)); v.w=GUILayout.HorizontalSlider(v.w,-0.5f,0.5f,GUILayout.Width(95)); GUILayout.Label(v.w.ToString("F2"),GUILayout.Width(35));
            GUILayout.EndHorizontal();
            SV4(m,o,v);
        }

        private void OnDestroy() { _harmony?.UnpatchSelf(); }
    }
}
