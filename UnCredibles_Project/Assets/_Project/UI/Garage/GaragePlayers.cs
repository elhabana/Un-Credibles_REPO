using System.Collections;
using System.Collections.Generic;
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
        public GameObject[] avatars;
        private Renderer[][] avatarRenderers;
        private Vector3[] avatarStarts;
        private Quaternion[] avatarRotations;
        private readonly List<Transform> doorPieces = new List<Transform>();
        private readonly List<Vector3> doorStarts = new List<Vector3>();
        private Coroutine departure;
        private GameObject outsideGlow;
        private Material outsideMaterial;
        private MaterialPropertyBlock colors;
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorProperty = Shader.PropertyToID("_Color");

        private void Awake()
        {
            colors = new MaterialPropertyBlock();
            avatarRenderers = new Renderer[avatars.Length][];
            var positions = new Vector3[avatars.Length];
            for (int i = 0; i < avatars.Length; i++)
            {
                positions[i] = avatars[i].transform.position;
                avatarRenderers[i] = avatars[i].GetComponentsInChildren<Renderer>(true);
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
            var environment = GameObject.Find("Garage - provisional geometry");
            if (environment == null) return;
            SplitWallBehindDoor(environment.transform);
            foreach (Transform child in environment.transform)
            {
                if (child.name != "Garage door" && child.name != "Door slat") continue;
                doorPieces.Add(child);
                doorStarts.Add(child.localPosition);
            }
        }

        private static void SplitWallBehindDoor(Transform environment)
        {
            var wall = environment.Find("West wall");
            if (wall == null) return; // Newer authored scenes already contain the opening.
            var source = wall.GetComponent<Renderer>();
            var material = source.sharedMaterial;
            source.enabled = false;
            var collider = wall.GetComponent<Collider>();
            if (collider != null) collider.enabled = false;
            WallSection("Garage door left jamb", new Vector3(-7f, 2.5f, -5.95f),
                new Vector3(.3f, 5f, 2.1f), material, environment);
            WallSection("Garage door right jamb", new Vector3(-7f, 2.5f, 5.95f),
                new Vector3(.3f, 5f, 2.1f), material, environment);
            WallSection("Garage door header", new Vector3(-7f, 4.5f, 0f),
                new Vector3(.3f, 1f, 9.8f), material, environment);
        }

        private static void WallSection(string name, Vector3 position, Vector3 size, Material material, Transform parent)
        {
            var section = GameObject.CreatePrimitive(PrimitiveType.Cube);
            section.name = name;
            section.transform.SetParent(parent, false);
            section.transform.localPosition = position;
            section.transform.localScale = size;
            section.GetComponent<Renderer>().sharedMaterial = material;
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
                for (int i = 0; i < doorPieces.Count; i++)
                    doorPieces[i].localPosition = doorStarts[i] + Vector3.up * (open * 4.2f);

                float run = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((elapsed - .45f) / 1.8f));
                for (int i = 0; i < avatars.Length; i++)
                {
                    if (!avatars[i].activeSelf) continue;
                    var start = avatarStarts[i];
                    avatars[i].transform.position = new Vector3(start.x - 1.9f * run,
                        Mathf.Sin(run * Mathf.PI * 12f) * .08f, start.z);
                    avatars[i].transform.rotation = Quaternion.Slerp(avatarRotations[i],
                        Quaternion.Euler(0f, -90f, 0f), Mathf.Clamp01(run * 4f));
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
            for (int i = 0; i < doorPieces.Count; i++) doorPieces[i].localPosition = doorStarts[i];
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
            {
                bool occupied = lobby.Players != null && lobby.Slots[i].Occupied;
                avatars[i].SetActive(occupied);
                if (!occupied) continue;
                Color color = PlayerIdentity.ColorFor(i, lobby.Slots[i].IsAI);
                foreach (var renderer in avatarRenderers[i])
                {
                    // Tint each figure without changing the shared garage materials.
                    renderer.GetPropertyBlock(colors);
                    colors.SetColor(BaseColor, color);
                    colors.SetColor(ColorProperty, color);
                    renderer.SetPropertyBlock(colors);
                }
            }
        }
    }
}
