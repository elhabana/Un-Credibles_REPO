using System.Collections;
using UnCredibles.UI.PartyLobby;
using UnCredibles.Players;
using UnityEngine;
using UnityEngine.Rendering;

namespace UnCredibles.UI.Garage
{
    public sealed class GaragePlayers : MonoBehaviour
    {
        public const float DepartureSeconds = 2.6f;
        public PartyLobbyController lobby;
        // One spot per slot (with its accessory anchors); the slot's figure is placed inside it.
        public GameObject[] avatars;
        public PlayerFigureSet figures;
        // The garage door and its slats: they rise together when the players leave.
        public Transform[] doorPieces;
        private Vector3[] avatarStarts;
        private Quaternion[] avatarRotations;
        private Vector3[] doorStarts;
        private Coroutine departure;
        private GameObject outsideGlow;
        private Material outsideMaterial;

        private void Awake()
        {
            var positions = new Vector3[avatars.Length];
            for (int i = 0; i < avatars.Length; i++)
            {
                positions[i] = avatars[i].transform.position;
                // Figures face +Z; turn them to the lobby camera, on the side of the mask anchor.
                var figure = Instantiate(figures.For(i), avatars[i].transform, false);
                figure.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            }
            // The lobby camera faces west: increasing Z runs from screen left to right.
            System.Array.Sort(positions, (a, b) => a.z.CompareTo(b.z));
            avatarStarts = positions;
            avatarRotations = new Quaternion[avatars.Length];
            for (int i = 0; i < avatars.Length; i++)
            {
                avatars[i].transform.position = positions[i];
                avatarRotations[i] = avatars[i].transform.rotation;
            }
            doorStarts = new Vector3[doorPieces.Length];
            for (int i = 0; i < doorPieces.Length; i++) doorStarts[i] = doorPieces[i].localPosition;
        }

        private void OnEnable()
        {
            lobby.Initialized += Refresh;
            lobby.SlotsChanged += Refresh;
            lobby.CountdownTick += HandleCountdown;
            lobby.CountdownCancelled += ResetDeparture;
            ResetDeparture();
            Refresh();
        }
        private void OnDisable()
        {
            lobby.Initialized -= Refresh;
            lobby.SlotsChanged -= Refresh;
            lobby.CountdownTick -= HandleCountdown;
            lobby.CountdownCancelled -= ResetDeparture;
            ResetDeparture();
            foreach (var avatar in avatars)
                if (avatar != null) avatar.SetActive(false);
        }

        private void HandleCountdown(int seconds)
        {
            if (seconds == 0 && departure == null)
                departure = StartCoroutine(PlayDeparture());
        }

        private IEnumerator PlayDeparture()
        {
            CreateOutsideGlow();
            float elapsed = 0f;
            while (elapsed < DepartureSeconds)
            {
                elapsed += Time.deltaTime;
                float open = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / .95f));
                for (int i = 0; i < doorPieces.Length; i++)
                    doorPieces[i].localPosition = doorStarts[i] + Vector3.up * (open * 4.2f);

                float run = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((elapsed - .45f) / 1.8f));
                for (int i = 0; i < avatars.Length; i++)
                {
                    if (!avatars[i].activeSelf) continue;
                    var start = avatarStarts[i];
                    avatars[i].transform.position = new Vector3(start.x - 1.9f * run,
                        Mathf.Sin(run * Mathf.PI * 12f) * .08f, start.z);
                    // The face is on the spot's +X (mask anchor); turn it to the door, west.
                    avatars[i].transform.rotation = Quaternion.Slerp(avatarRotations[i],
                        Quaternion.Euler(0f, 180f, 0f), Mathf.Clamp01(run * 4f));
                }
                yield return null;
            }
            departure = null;
        }

        private void CreateOutsideGlow()
        {
            if (outsideGlow != null) return;
            outsideGlow = GameObject.CreatePrimitive(PrimitiveType.Cube);
            outsideGlow.name = "Bright exterior beyond garage door";
            outsideGlow.transform.position = new Vector3(-7.18f, 2f, 0f);
            outsideGlow.transform.localScale = new Vector3(.12f, 4.3f, 9.8f);
            Destroy(outsideGlow.GetComponent<Collider>());
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Standard");
            outsideMaterial = new Material(shader) { color = new Color(1f, .97f, .78f) };
            outsideMaterial.EnableKeyword("_EMISSION");
            outsideMaterial.SetColor("_EmissionColor", new Color(2.5f, 2.2f, 1.5f));
            var renderer = outsideGlow.GetComponent<Renderer>();
            renderer.sharedMaterial = outsideMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            var light = new GameObject("Entrance light").AddComponent<Light>();
            light.transform.SetParent(outsideGlow.transform, false);
            light.type = LightType.Point;
            light.range = 10f;
            light.intensity = 4f;
            light.color = new Color(1f, .94f, .72f);
        }

        private void ResetDeparture()
        {
            if (departure != null) StopCoroutine(departure);
            departure = null;
            for (int i = 0; i < doorPieces.Length; i++) doorPieces[i].localPosition = doorStarts[i];
            for (int i = 0; i < avatars.Length; i++)
            {
                if (avatars[i] == null) continue;
                avatars[i].transform.position = avatarStarts[i];
                avatars[i].transform.rotation = avatarRotations[i];
            }
            if (outsideGlow != null) Destroy(outsideGlow);
            if (outsideMaterial != null) Destroy(outsideMaterial);
            outsideGlow = null;
            outsideMaterial = null;
        }
        private void Refresh()
        {
            for (int i = 0; i < avatars.Length; i++)
                avatars[i].SetActive(lobby.Players != null && lobby.Slots[i].Occupied);
        }
    }
}
