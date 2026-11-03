using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace UnCredibles.Trailer
{
    // Makes a gameplay scene presentable for the previs recording: soft shadows, a trailer grade,
    // anti-aliasing, and no HUD, player labels or score popups on screen.
    public sealed class TrailerLook : MonoBehaviour
    {
        // Overlay canvases whose name contains one of these stay visible (e.g. the pool splash).
        public readonly List<string> keepCanvases = new();
        public bool hideOverlays = true;
        public bool hideWorldText = true;

        public static TrailerLook Apply(Camera output, float exposure = 0f, float bloom = .6f)
        {
            var data = output.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
            output.allowMSAA = false;

            foreach (var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (light.type == LightType.Directional)
                {
                    light.shadows = LightShadows.Soft;
                    light.shadowStrength = .75f;
                }

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            var tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.ACES);
            var colour = profile.Add<ColorAdjustments>(true);
            colour.postExposure.Override(exposure);
            colour.contrast.Override(12f);
            colour.saturation.Override(14f);
            var glow = profile.Add<Bloom>(true);
            glow.intensity.Override(bloom);
            glow.threshold.Override(1f);
            glow.scatter.Override(.6f);
            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(.22f);
            vignette.smoothness.Override(.45f);
            var motion = profile.Add<MotionBlur>(true);
            motion.intensity.Override(0f);
            var volume = new GameObject("Trailer volume").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 100f;
            volume.sharedProfile = profile;

            var look = new GameObject("Trailer look").AddComponent<TrailerLook>();
            return look;
        }

        private void LateUpdate()
        {
            if (hideOverlays)
                foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                {
                    if (!canvas.isRootCanvas || canvas.renderMode == RenderMode.WorldSpace) continue;
                    if (Kept(canvas.name)) continue;
                    canvas.enabled = false;
                }
            if (hideWorldText)
            {
                // Player labels re-enable their text every frame: stop them first.
                foreach (var presentation in FindObjectsByType<Minigames.PlayerPresentation>(FindObjectsSortMode.None))
                    presentation.enabled = false;
                foreach (var text in FindObjectsByType<TMPro.TextMeshPro>(FindObjectsSortMode.None))
                    if (text.enabled) text.enabled = false;
            }
        }

        private bool Kept(string canvasName)
        {
            foreach (var keep in keepCanvases)
                if (canvasName.Contains(keep)) return true;
            return false;
        }
    }
}
