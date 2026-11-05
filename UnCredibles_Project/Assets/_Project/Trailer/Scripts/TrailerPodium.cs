using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnCredibles.UI.Results;
using UnityEngine;

namespace UnCredibles.Trailer
{
    // Previs shot 17 in SCN_Results: the final podium with made-up standings (no match needed),
    // filmed with a slow push-in. The results UI is hidden; the 3D name and point labels stay.
    public sealed class TrailerPodium : MonoBehaviour
    {
        public string[] names = { "Rojo", "Azul", "Verde", "Amarillo" };
        // Slot index per placement 1..4 and their points.
        public int[] slotByPlace = { 2, 0, 3, 1 };
        public int[] points = { 2450, 1980, 1720, 1150 };
        public float duration = 4f;

        private TrailerShots shots;

        private IEnumerator Start()
        {
            var screen = FindFirstObjectByType<ResultsScreen>();
            // Let the results screen finish its own setup (it waits for Core) before replacing the podium.
            yield return new WaitForSeconds(.2f);
            var podiumField = typeof(ResultsScreen).GetField("podium", BindingFlags.Instance | BindingFlags.NonPublic);
            var podium = (ResultsPodium)podiumField.GetValue(screen);
            var entries = new List<PodiumEntry>();
            for (int i = 0; i < slotByPlace.Length; i++)
                entries.Add(new PodiumEntry(i + 1, names[slotByPlace[i]], points[i], slotByPlace[i]));
            podium.Render(entries);
            podium.SetVisible(true);
            screen.enabled = false;
            // Render() destroys the previous podium at the end of the frame; wait so Find gets the new one.
            yield return null;

            var output = Camera.main;
            output.clearFlags = CameraClearFlags.SolidColor;
            output.backgroundColor = new Color(.035f, .05f, .08f);
            var look = TrailerLook.Apply(output, .2f, .8f);
            look.hideWorldText = false;

            // A warm spotlight on the winner's pedestal.
            var winner = GameObject.Find("Place 1 figure");
            if (winner != null)
            {
                var spot = new GameObject("Trailer winner spot").AddComponent<Light>();
                spot.type = LightType.Spot;
                spot.transform.SetPositionAndRotation(winner.transform.position + new Vector3(0f, 5f, -1.5f),
                    Quaternion.LookRotation(new Vector3(0f, -5f, 1.5f)));
                spot.spotAngle = 32f;
                spot.innerSpotAngle = 18f;
                spot.range = 12f;
                spot.intensity = 30f;
                spot.color = new Color(1f, .88f, .65f);
                spot.shadows = LightShadows.Soft;
            }

            MattePedestals();
            CollectRisers();

            var cam = output.transform;
            shots = TrailerShots.Create(output);
            shots.Add(new TrailerShot
            {
                Name = "17 Podium", Duration = duration, Fov = output.fieldOfView - 4f, ToFov = output.fieldOfView - 14f,
                FromPosition = cam.position + cam.forward * .6f, ToPosition = cam.position + cam.forward * 2.0f + Vector3.up * .2f,
                FromEuler = cam.eulerAngles + new Vector3(4f, 0f, 0f), ToEuler = cam.eulerAngles + new Vector3(7f, 0f, 0f)
            });
            Ready = true;
        }

        public bool Ready { get; private set; }
        public TrailerShots Shots => shots;

        [Tooltip("Seconds into the shot when the pedestals start rising, and how long they take.")]
        public float riseStart = .35f, riseTime = 1.8f;

        // One place on the podium: its pedestal grows from a common height to its own, carrying what is on top.
        private sealed class Riser
        {
            public Transform Pedestal;
            public float Height;
            public Vector3 PedestalScale;
            public readonly List<(Transform item, Vector3 position, float share)> Items = new();
        }

        private readonly List<Riser> risers = new();
        private float startHeight;

        // The medal materials are glossy and mirror the dark backdrop; give the trailer copies a satin finish.
        private static void MattePedestals()
        {
            var podium = GameObject.Find("Final podium");
            if (podium == null) return;
            foreach (var renderer in podium.GetComponentsInChildren<Renderer>())
            {
                if (renderer is not MeshRenderer) continue;
                foreach (var material in renderer.materials)
                {
                    if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
                    if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", .25f);
                    if (material.HasProperty("_EnvironmentReflections")) material.SetFloat("_EnvironmentReflections", 0f);
                    material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
                }
            }
        }

        private void CollectRisers()
        {
            var podium = GameObject.Find("Final podium");
            if (podium == null) return;
            var root = podium.transform;
            foreach (Transform child in root)
            {
                if (!child.name.EndsWith("pedestal")) continue;
                var spot = child.GetComponent<PodiumPedestal>();
                float height = spot != null ? spot.FigureSpot.localPosition.y * child.localScale.y : 1f;
                risers.Add(new Riser { Pedestal = child, Height = height, PedestalScale = child.localScale });
            }
            if (risers.Count == 0) return;
            startHeight = float.MaxValue;
            foreach (var riser in risers) startHeight = Mathf.Min(startHeight, riser.Height);
            // Everything standing at the pedestal's x rides up with it: figure, crown and name fully,
            // the points painted on its front by half (they sit at mid height).
            foreach (Transform child in root)
                foreach (var riser in risers)
                {
                    if (child == riser.Pedestal || Mathf.Abs(child.localPosition.x - riser.Pedestal.localPosition.x) > .05f) continue;
                    float share = child.localPosition.y > riser.Pedestal.localPosition.y + riser.Height * .8f ? 1f : .5f;
                    riser.Items.Add((child, child.localPosition, share));
                }
            Rise(0f);
        }

        private void Update()
        {
            if (shots == null || risers.Count == 0) return;
            float k = Mathf.Clamp01((shots.Elapsed - riseStart) / riseTime);
            // Ease out with a small overshoot, like the pedestals popping into place.
            float eased = 1f + 2.2f * Mathf.Pow(k - 1f, 3f) + 1.2f * Mathf.Pow(k - 1f, 2f);
            Rise(k >= 1f ? 1f : eased);
        }

        private void Rise(float k)
        {
            foreach (var riser in risers)
            {
                float height = Mathf.LerpUnclamped(startHeight, riser.Height, k);
                riser.Pedestal.localScale = new Vector3(riser.PedestalScale.x, riser.PedestalScale.y * height / riser.Height, riser.PedestalScale.z);
                float lift = height - riser.Height;
                foreach (var (item, position, share) in riser.Items)
                    item.localPosition = position + Vector3.up * (lift * share);
            }
        }
    }
}
