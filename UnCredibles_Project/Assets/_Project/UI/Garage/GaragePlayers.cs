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
        private const float FiguresScreenHeight = .55f; // helmets stay clear of the buttons at the top
        // Figures (and the clothes on them) at human size next to the 3.8 m garage door.
        private const float FigureScale = .65f;
        private const float FigureHeight = 2.55f * FigureScale;
        // Looking slightly down from a bit higher up.
        private const float LobbyPitch = 8f;
        // Figures closer than this touch each other (the head is about 1.1 wide at full size) and leave no
        // room for the outfit arrows between them: spread them out around the middle of the garage.
        private const float FigureSpacing = 1.95f; // also lines them up over their cards
        // How far past the door the players run when they leave (out of the garage, into the light).
        private const float ExitPastDoor = 3f;
        // Width of a figure at its scale, plus air: two figures side by side never closer than this.
        private const float FigureWidth = 1.1f * FigureScale + .25f;
        // When the door is too narrow for everyone side by side, they go through one after another, this far apart in time.
        private const float QueueDelay = .3f;
        private const float RunStart = .45f; // the door is already half up
        // The door panel of the garage model that rises when the players leave; found by name when left empty.
        private static readonly string[] GarageDoorNames = { "SM_GarageDoor.001", "GarageDoor.001", "garagedoor.001" };
        // The brick wall behind the door has no hole: as the door rises, a warm glow fills the opening and
        // lights the garage (GarageDoorLight), the players vanish into it and the camera pushes in on it
        // while the screen flashes white, and the vote comes up as the flash fades.
        private static readonly Color FlashColor = new Color(1f, .97f, .9f);
        private const float GlowInset = .15f;       // behind the door panel, in front of the wall
        private const float FlashIn = .55f;         // ends with the departure
        private const float FlashHold = .75f;       // covers the wait and the switch to the vote
        private const float FlashOut = .7f;
        private const float ZoomStart = 1.3f;       // seconds into the departure
        private const float ZoomTravel = .55f;      // part of the way to the door the camera covers
        private const float ZoomFov = 12f;          // degrees narrower at the end
        public Transform garageDoor;
        public PartyLobbyController lobby;
        // One spot per slot (with its accessory anchors); the slot's figure is placed inside it.
        public GameObject[] avatars;
        public PlayerFigureSet figures;
        private Bounds doorOpening; // where the real door is, how wide and how tall
        private bool hasGarageDoor;
        private Vector3 garageDoorStart;
        private float[] runDelays; // per figure: who goes through the door first
        private float[] exitLanes; // per figure: its Z while going through the door
        private Vector3[] avatarStarts;
        // Animated character inside each figure (Idle while waiting, Run when the door opens).
        private PlayerCharacterAnimator[] characters;
        // Helmet, outfit and boots of each figure, changed from the lobby cards.
        private PlayerCharacterOutfit[] outfits;
        private Quaternion[] avatarRotations;
        private Coroutine departure;
        private GarageDoorLight doorLight;
        private ScreenFlash flash;
        private Camera zoomCamera;
        private Vector3 cameraStart;
        private Quaternion cameraStartRotation;
        private float cameraStartFov;
        private bool zoomed;

        private void Awake()
        {
            FindGarageDoor();
            var positions = new Vector3[avatars.Length];
            characters = new PlayerCharacterAnimator[avatars.Length];
            outfits = new PlayerCharacterOutfit[avatars.Length];
            for (int i = 0; i < avatars.Length; i++)
            {
                positions[i] = avatars[i].transform.position;
                // Figures face +Z; turn them to the lobby camera, on the side of the mask anchor.
                var figure = Instantiate(figures.For(i), avatars[i].transform, false);
                figure.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                figure.transform.localScale *= FigureScale; // helmets, outfits and boots hang from its bones
                characters[i] = figure.GetComponentInChildren<PlayerCharacterAnimator>();
                outfits[i] = figure.GetComponentInChildren<PlayerCharacterOutfit>();
                if (outfits[i] != null && lobby.PartCatalog == null) lobby.PartCatalog = outfits[i].Catalog;
            }
            // The lobby camera faces west: increasing Z runs from screen left to right.
            System.Array.Sort(positions, (a, b) => a.z.CompareTo(b.z));
            SpreadOut(positions, GarageMiddleZ());
            avatarStarts = positions;
            avatarRotations = new Quaternion[avatars.Length];
            for (int i = 0; i < avatars.Length; i++)
            {
                avatars[i].transform.position = positions[i];
                avatarRotations[i] = avatars[i].transform.rotation;
            }
            ReframeLobbyCamera();
            PlanExit();
            BuildDoorGlow();
            gameObject.AddComponent<LobbyOutfitArrows>().Init(lobby, avatars, outfits);
        }

        // The garage model's door panel, and the box it fills (the opening the players run through).
        private void FindGarageDoor()
        {
            if (garageDoor == null)
                foreach (var candidate in FindObjectsByType<Transform>(FindObjectsSortMode.None))
                    if (System.Array.Exists(GarageDoorNames, n => string.Equals(n, candidate.name, System.StringComparison.OrdinalIgnoreCase)))
                    {
                        garageDoor = candidate;
                        break;
                    }
            if (garageDoor == null)
            {
                Debug.LogWarning("[GaragePlayers] No garage door (SM_GarageDoor.001) in the scene: the players run west.", this);
                return;
            }
            var renderers = garageDoor.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;
            doorOpening = renderers[0].bounds;
            foreach (var part in renderers) doorOpening.Encapsulate(part.bounds);
            garageDoorStart = garageDoor.position;
            hasGarageDoor = true;
        }

        private void BuildDoorGlow()
        {
            flash = ScreenFlash.Create(FlashColor);
            if (hasGarageDoor) doorLight = GarageDoorLight.Build(doorOpening, DoorX() - GlowInset);
        }

        private void OnDestroy()
        {
            if (doorLight != null) Destroy(doorLight.gameObject);
            if (flash != null) Destroy(flash.gameObject);
        }

        private void SetGlow(float amount)
        {
            if (doorLight != null) doorLight.Set(amount);
        }

        // Where the garage door stands (west of the players); without one, 4 m ahead of them.
        private float DoorX() => hasGarageDoor ? doorOpening.center.x : avatarStarts[0].x - 4f;

        // The middle of the garage door: the line is centred on it, and so is the lobby camera.
        private float GarageMiddleZ()
        {
            if (hasGarageDoor) return doorOpening.center.z;
            float z = 0f;
            foreach (var avatar in avatars) z += avatar.transform.position.z;
            return z / Mathf.Max(1, avatars.Length);
        }

        // Each figure goes through the door in its own lane. If the door is too narrow for the four side
        // by side, the lanes squeeze together and the ones in the middle go first, one after another,
        // so two figures are never level and close at the same time.
        private void PlanExit()
        {
            int count = avatarStarts.Length;
            runDelays = new float[count];
            exitLanes = new float[count];
            float middle = GarageMiddleZ();
            float room = hasGarageDoor ? doorOpening.size.z - FigureWidth : float.MaxValue;
            float spacing = count > 1 ? Mathf.Min(FigureSpacing, room / (count - 1)) : 0f;
            bool queue = spacing < FigureWidth;
            float first = -(count - 1) * .5f;
            for (int i = 0; i < count; i++)
            {
                exitLanes[i] = middle + (first + i) * Mathf.Max(0f, spacing);
                if (!queue) continue;
                // One turn each, middle figures first: count who is closer to the middle (left wins a tie).
                int turn = 0;
                for (int other = 0; other < count; other++)
                {
                    float mine = Mathf.Abs(first + i), theirs = Mathf.Abs(first + other);
                    if (theirs < mine || (theirs == mine && other < i)) turn++;
                }
                runDelays[i] = turn * QueueDelay;
            }
        }

        // Evenly spaced line, FigureSpacing apart, centred on the garage (the spots keep their depth and height).
        private static void SpreadOut(Vector3[] positions, float middleZ)
        {
            if (positions.Length == 0) return;
            float x = 0f;
            foreach (var position in positions) x += position.x;
            x /= positions.Length;
            float first = -(positions.Length - 1) * .5f;
            for (int i = 0; i < positions.Length; i++)
                positions[i] = new Vector3(x, positions[i].y, middleZ + (first + i) * FigureSpacing);
        }

        // Keeps the lobby viewpoint's rotation and moves it back along it, centred on the figures.
        private void ReframeLobbyCamera()
        {
            var rig = FindFirstObjectByType<GarageCameraRig>();
            if (rig == null || rig.output == null || rig.viewpoints.Length <= (int)GarageView.Lobby) return;
            var view = rig.viewpoints[(int)GarageView.Lobby];
            if (view == null) return;
            view.rotation = Quaternion.Euler(LobbyPitch, view.eulerAngles.y, 0f);
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
            lobby.CountdownCancelled += Refresh; // figures that went into the light come back
            ResetDeparture();
            Refresh();
        }
        private void OnDisable()
        {
            lobby.Initialized -= Refresh;
            lobby.SlotsChanged -= Refresh;
            lobby.CountdownTick -= HandleCountdown;
            lobby.CountdownCancelled -= ResetDeparture;
            lobby.CountdownCancelled -= Refresh;
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
            // Into their door lane before reaching the door, through it, and out the other side.
            float doorX = DoorX();
            float exitX = doorX - ExitPastDoor;
            float doorRise = doorOpening.size.y + .1f;
            var rig = FindFirstObjectByType<GarageCameraRig>();
            zoomCamera = rig != null ? rig.output : null;
            if (zoomCamera != null)
            {
                cameraStart = zoomCamera.transform.position;
                cameraStartRotation = zoomCamera.transform.rotation;
                cameraStartFov = zoomCamera.fieldOfView;
            }
            var glowCentre = new Vector3(doorX - GlowInset, hasGarageDoor ? doorOpening.center.y : 1.5f,
                hasGarageDoor ? doorOpening.center.z : GarageMiddleZ());
            bool flashed = false;
            while (elapsed < DepartureSeconds)
            {
                elapsed += Time.deltaTime;
                float open = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / .95f));
                if (hasGarageDoor) garageDoor.position = garageDoorStart + Vector3.up * (open * doorRise);
                SetGlow(Mathf.Clamp01(open * 3f)); // full glow before the bricks behind can show
                if (!flashed && elapsed >= DepartureSeconds - FlashIn)
                {
                    flashed = true;
                    flash.Play(FlashIn, FlashHold, FlashOut);
                }
                // Push in on the glow, faster and faster, narrowing the view: the cut into the vote.
                float zoom = Mathf.Clamp01((elapsed - ZoomStart) / (DepartureSeconds - ZoomStart));
                if (zoomCamera != null && zoom > 0f)
                {
                    zoomed = true;
                    float e = zoom * zoom;
                    zoomCamera.transform.SetPositionAndRotation(Vector3.Lerp(cameraStart, glowCentre, e * ZoomTravel),
                        Quaternion.Slerp(cameraStartRotation, Quaternion.LookRotation(glowCentre - cameraStart), e * .5f));
                    zoomCamera.fieldOfView = cameraStartFov - ZoomFov * e;
                }

                for (int i = 0; i < avatars.Length; i++)
                {
                    if (!avatars[i].activeSelf) continue;
                    var start = avatarStarts[i];
                    var character = characters[i];
                    float begin = RunStart + runDelays[i];
                    // Quick start, full speed through the door (no slowing down at the end).
                    float run = Mathf.Clamp01((elapsed - begin) / (DepartureSeconds - begin));
                    run = run * run * (2f - run);
                    if (character != null && elapsed >= begin) character.SetRunning(true);
                    float x = Mathf.Lerp(start.x, exitX, run);
                    if (hasGarageDoor && x < doorX - GlowInset - .1f)
                    {
                        avatars[i].SetActive(false); // gone into the light; Refresh brings it back
                        continue;
                    }
                    // Lined up with its lane by the time it reaches the door frame.
                    float toDoor = Mathf.Clamp01(Mathf.InverseLerp(start.x, doorX + FigureWidth, x));
                    float z = Mathf.Lerp(start.z, exitLanes[i], Mathf.SmoothStep(0f, 1f, toDoor));
                    // The animated run already bounces; the old figures fake it with a hop.
                    float hop = character != null ? start.y : Mathf.Sin(run * Mathf.PI * 12f) * .08f;
                    avatars[i].transform.position = new Vector3(x, hop, z);
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
            if (hasGarageDoor && garageDoor != null) garageDoor.position = garageDoorStart; // already destroyed when the scene closes
            SetGlow(0f);
            if (zoomed && zoomCamera != null)
            {
                zoomCamera.transform.SetPositionAndRotation(cameraStart, cameraStartRotation);
                zoomCamera.fieldOfView = cameraStartFov;
            }
            zoomed = false;
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
