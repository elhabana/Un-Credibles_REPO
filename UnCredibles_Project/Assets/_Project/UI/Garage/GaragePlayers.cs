using System.Collections;
using UnCredibles.UI.PartyLobby;
using UnCredibles.Players;
using UnityEngine;

namespace UnCredibles.UI.Garage
{
    public sealed class GaragePlayers : MonoBehaviour
    {
        public const float DepartureSeconds = 2.6f;
        // The lobby camera steps back so the whole figures (feet and boots included) stand above
        // the player cards: how much further, where their middle lands on screen, figure height.
        private const float LobbyZoomOut = 1.4f;
        private const float FiguresScreenHeight = .6f; // helmets stay clear of the buttons at the top
        private const float FigureHeight = 2.55f;
        // Figures closer than this touch each other (the head is about 1.1 wide) and leave no
        // room for the outfit arrows between them: spread them out around the same middle.
        private const float FigureSpacing = 1.95f; // also lines them up over their cards
        public PartyLobbyController lobby;
        // One spot per slot (with its accessory anchors); the slot's figure is placed inside it.
        public GameObject[] avatars;
        public PlayerFigureSet figures;
        // The garage door and its slats: they rise together when the players leave.
        public Transform[] doorPieces;
        private Vector3[] avatarStarts;
        // Animated character inside each figure (Idle while waiting, Run when the door opens).
        private PlayerCharacterAnimator[] characters;
        // Helmet, outfit and boots of each figure, changed from the lobby cards.
        private PlayerCharacterOutfit[] outfits;
        private Quaternion[] avatarRotations;
        private Vector3[] doorStarts;
        private Coroutine departure;

        private void Awake()
        {
            var positions = new Vector3[avatars.Length];
            characters = new PlayerCharacterAnimator[avatars.Length];
            outfits = new PlayerCharacterOutfit[avatars.Length];
            for (int i = 0; i < avatars.Length; i++)
            {
                positions[i] = avatars[i].transform.position;
                // Figures face +Z; turn them to the lobby camera, on the side of the mask anchor.
                var figure = Instantiate(figures.For(i), avatars[i].transform, false);
                figure.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                characters[i] = figure.GetComponentInChildren<PlayerCharacterAnimator>();
                outfits[i] = figure.GetComponentInChildren<PlayerCharacterOutfit>();
                if (outfits[i] != null && lobby.PartCatalog == null) lobby.PartCatalog = outfits[i].Catalog;
            }
            // The lobby camera faces west: increasing Z runs from screen left to right.
            System.Array.Sort(positions, (a, b) => a.z.CompareTo(b.z));
            SpreadOut(positions);
            avatarStarts = positions;
            avatarRotations = new Quaternion[avatars.Length];
            for (int i = 0; i < avatars.Length; i++)
            {
                avatars[i].transform.position = positions[i];
                avatarRotations[i] = avatars[i].transform.rotation;
            }
            ReframeLobbyCamera();
            gameObject.AddComponent<LobbyOutfitArrows>().Init(lobby, avatars, outfits);

            doorStarts = new Vector3[doorPieces.Length];
            for (int i = 0; i < doorPieces.Length; i++) doorStarts[i] = doorPieces[i].localPosition;
        }

        private static void SpreadOut(Vector3[] positions)
        {
            if (positions.Length < 2) return;
            float spacing = float.MaxValue;
            var centre = Vector3.zero;
            for (int i = 0; i < positions.Length; i++)
            {
                centre += positions[i];
                if (i > 0) spacing = Mathf.Min(spacing, Vector3.Distance(positions[i], positions[i - 1]));
            }
            if (spacing >= FigureSpacing || spacing <= 0f) return;
            centre /= positions.Length;
            for (int i = 0; i < positions.Length; i++)
                positions[i] = centre + (positions[i] - centre) * (FigureSpacing / spacing);
        }

        // Keeps the lobby viewpoint's rotation and moves it back along it, centred on the figures.
        private void ReframeLobbyCamera()
        {
            var rig = FindFirstObjectByType<GarageCameraRig>();
            if (rig == null || rig.output == null || rig.viewpoints.Length <= (int)GarageView.Lobby) return;
            var view = rig.viewpoints[(int)GarageView.Lobby];
            if (view == null) return;
            var centre = Vector3.zero;
            foreach (var start in avatarStarts) centre += start;
            centre = centre / avatarStarts.Length + Vector3.up * (FigureHeight * .5f);
            float distance = Vector3.Dot(centre - view.position, view.forward) * LobbyZoomOut;
            float halfHeight = Mathf.Tan(rig.output.fieldOfView * .5f * Mathf.Deg2Rad) * distance;
            view.position = centre - view.forward * distance - view.up * ((FiguresScreenHeight - .5f) * 2f * halfHeight);
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
                    var character = characters[i];
                    if (character != null && elapsed >= .45f) character.SetRunning(true);
                    // The animated run already bounces; the old figures fake it with a hop.
                    float hop = character != null ? start.y : Mathf.Sin(run * Mathf.PI * 12f) * .08f;
                    avatars[i].transform.position = new Vector3(start.x - 1.9f * run, hop, start.z);
                    // The face is on the spot's +X (mask anchor); turn it to the door, west.
                    avatars[i].transform.rotation = Quaternion.Slerp(avatarRotations[i],
                        Quaternion.Euler(0f, 180f, 0f), Mathf.Clamp01(run * 4f));
                }
                yield return null;
            }
            departure = null;
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
                if (characters != null && characters[i] != null) characters[i].SetRunning(false);
            }
        }
        private void Refresh()
        {
            for (int i = 0; i < avatars.Length; i++)
            {
                bool occupied = lobby.Players != null && lobby.Slots[i].Occupied;
                avatars[i].SetActive(occupied);
                var loadout = lobby.Players != null ? lobby.Slots[i].Loadout : default;
                if (outfits[i] != null && !outfits[i].Loadout.Equals(loadout)) outfits[i].Apply(loadout);
            }
        }
    }
}
