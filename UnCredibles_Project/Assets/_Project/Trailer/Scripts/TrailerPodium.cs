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
    }
}
