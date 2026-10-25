using System.Collections.Generic;
using System.IO;
using UnCredibles.Players;
using UnCredibles.Players.Inputs;
using UnityEngine;

namespace UnCredibles.Minigames.GarageMice
{
    // Arena prototype. The host owns movement, hits and scores; clients draw its snapshots.
    // Jump is the broom swing on keyboard, gamepad and BatPad alike.
    public sealed class GarageMiceController : MinigameController
    {
        private const float MouseHeight = .16f; // mice run with their centre at this height
        private const byte CatchEvent = 1;

        [SerializeField] private GarageMiceSettings settings;
        [SerializeField, Tooltip("Floor and walls, built for the arena size in the settings.")] private Transform arenaPrefab;
        [SerializeField] private Transform shelfPrefab;
        [SerializeField, Tooltip("Half the footprint the shelf prefab is modelled at; it is stretched to each shelf in the settings.")]
        private Vector2 shelfPrefabHalfSize = new Vector2(1.2f, .55f);
        [SerializeField, Tooltip("Modelled for the back and front walls; turned for the side walls.")] private Transform holePrefab;
        [SerializeField] private Transform mousePrefab;
        [SerializeField] private Transform goldenMousePrefab;

        private sealed class Sweeper
        {
            public PlayerSlot Slot;
            public Transform Root;
            public GarageMicePlayer View;
            public float Cooldown;
            public float SwingVisual;
            public float Stun;
            public float Immunity;
        }

        private sealed class Mouse
        {
            public Transform Root;
            public Vector3 Direction;
            public float TurnTimer;
            public float Respawn;
            public bool Golden;
            public float Tunnel;        // Seconds left inside a wall hole.
            public int ExitHole;
            public float ShelterTime;   // Seconds spent hiding under the current shelf.
            public float HideCooldown;  // Shelves and holes are off limits while above zero.
        }

        private readonly List<Sweeper> sweepers = new List<Sweeper>(PlayerRegistry.MaxPlayers);
        private readonly List<Mouse> mice = new List<Mouse>();
        private GarageMiceSettings.Shelf[] shelves;
        private Vector3[] holes;
        private Transform arena;
        private Camera arenaCamera;
        private Vector3 cameraHome;
        private float cameraShake;
        private GarageMiceHud hud;
        private GarageMicePopups popups;

        protected override void OnInitialize(MinigameContext context)
        {
            shelves = settings.Shelves;
            holes = new Vector3[settings.Holes.Length];
            for (int i = 0; i < holes.Length; i++) holes[i] = new Vector3(settings.Holes[i].x, MouseHeight, settings.Holes[i].y);
            ConfigureCamera();
            BuildArena();
            foreach (var slot in Players) SpawnSweeper(slot);
            for (int i = 0; i < settings.MouseCount; i++) SpawnMouse(i, false);
            SpawnMouse(settings.MouseCount, true);
            hud = gameObject.AddComponent<GarageMiceHud>();
            hud.Setup(this);
            popups = gameObject.AddComponent<GarageMicePopups>();
            popups.Setup(arenaCamera != null ? arenaCamera.transform : null);
        }

        protected override void OnGameStarted() { }

        private void Update()
        {
            if (State < MinigameState.Waiting || State >= MinigameState.Exit) return;
            float deltaTime = Time.deltaTime;
            foreach (var sweeper in sweepers) UpdateBroom(sweeper, deltaTime);
            popups.Tick(deltaTime);
            if (IsReplica || !IsPlaying) return;

            TickMice(deltaTime);
            TickAI();
            foreach (var sweeper in sweepers) TickSweeper(sweeper, deltaTime);
        }

        private void TickSweeper(Sweeper sweeper, float deltaTime)
        {
            sweeper.Cooldown = Mathf.Max(0f, sweeper.Cooldown - deltaTime);
            sweeper.Stun = Mathf.Max(0f, sweeper.Stun - deltaTime);
            sweeper.Immunity = Mathf.Max(0f, sweeper.Immunity - deltaTime);
            if (sweeper.Stun > 0f) return;

            var input = sweeper.Slot.Input;
            Vector2 move = input != null ? Vector2.ClampMagnitude(input.Move, 1f) : Vector2.zero;
            if (move.sqrMagnitude > .02f)
            {
                var direction = new Vector3(move.x, 0f, move.y);
                sweeper.Root.rotation = Quaternion.RotateTowards(sweeper.Root.rotation,
                    Quaternion.LookRotation(direction), 900f * deltaTime);
                sweeper.Root.position = MoveAroundShelves(sweeper.Root.position,
                    direction * (settings.MoveSpeed * deltaTime), settings.PlayerRadius);
            }
            if (sweeper.Cooldown <= 0f && input != null && input.WasPressed(PlayerAction.Jump))
                Swing(sweeper);
        }

        private void Swing(Sweeper attacker)
        {
            attacker.Cooldown = settings.SwingCooldown;
            attacker.SwingVisual = settings.SwingVisualSeconds;
            Vector3 origin = attacker.Root.position;
            Vector3 facing = attacker.Root.forward;
            float reach = settings.SwingReach + settings.MouseRadius;
            int caught = 0;
            int points = 0;
            bool golden = false;
            foreach (var mouse in mice)
            {
                if (!IsCatchable(mouse) || !InSwing(origin, facing, mouse.Root.position, reach)) continue;
                caught++;
                points += mouse.Golden ? settings.GoldenPoints : 1;
                golden |= mouse.Golden;
                mouse.Respawn = mouse.Golden ? settings.GoldenRespawnSeconds : settings.MouseRespawnSeconds;
                mouse.Root.gameObject.SetActive(false);
            }
            // Combo: every mouse in the same swing multiplies the whole catch.
            if (caught > 0) AwardCatch(attacker, points * caught, caught, golden);
            foreach (var rival in sweepers)
            {
                if (rival == attacker || rival.Immunity > 0f ||
                    !InSwing(origin, facing, rival.Root.position, settings.SwingReach + settings.PlayerRadius)) continue;
                rival.Stun = settings.StunSeconds;
                rival.Immunity = settings.StunImmunitySeconds;
                cameraShake = Mathf.Max(cameraShake, .17f);
            }
        }

        private void AwardCatch(Sweeper attacker, int points, int combo, bool golden)
        {
            Score.AddScore(attacker.Slot.PlayerId, points);
            popups.Show(attacker.Root.position, points, combo, golden);
            var message = BeginEvent(CatchEvent);
            message.Write((byte)sweepers.IndexOf(attacker));
            message.Write((short)points);
            message.Write((byte)combo);
            message.Write(golden);
            SendEvent();
        }

        protected override void OnNetworkEvent(byte eventId, BinaryReader reader)
        {
            if (eventId != CatchEvent) return;
            int index = reader.ReadByte();
            int points = reader.ReadInt16();
            int combo = reader.ReadByte();
            bool golden = reader.ReadBoolean();
            if (index < sweepers.Count) popups.Show(sweepers[index].Root.position, points, combo, golden);
        }

        // Mice in a hole or hiding under a shelf cannot be hit.
        private bool IsCatchable(Mouse mouse) =>
            mouse.Respawn <= 0f && mouse.Tunnel <= 0f && !IsHidden(mouse.Root.position);

        // Under a shelf a mouse is out of reach, so it is not drawn either (its tail used to stick out).
        // Clients work it out from the position, so the snapshot needs nothing extra.
        private bool IsHidden(Vector3 position) => InShelf(position, 0f);

        private bool InSwing(Vector3 origin, Vector3 facing, Vector3 target, float reach)
        {
            Vector3 offset = target - origin;
            offset.y = 0f;
            return offset.sqrMagnitude <= reach * reach &&
                   (offset.sqrMagnitude < .01f || Vector3.Angle(facing, offset) <= settings.SwingHalfAngle);
        }

        private void TickAI()
        {
            foreach (var sweeper in sweepers)
            {
                if (!(sweeper.Slot.Input is AIInput input)) continue;
                Mouse nearest = null;
                float best = float.MaxValue;
                foreach (var mouse in mice)
                {
                    if (!IsCatchable(mouse)) continue;
                    float distance = (mouse.Root.position - sweeper.Root.position).sqrMagnitude;
                    if (mouse.Golden) distance *= .25f; // Bots go for the golden mouse first.
                    if (distance >= best) continue;
                    best = distance;
                    nearest = mouse;
                }
                if (nearest == null || sweeper.Stun > 0f)
                {
                    input.SetMove(Vector2.zero);
                    continue;
                }
                Vector3 offset = nearest.Root.position - sweeper.Root.position;
                Vector2 desired = new Vector2(offset.x, offset.z).normalized;
                Vector3 test = sweeper.Root.position + new Vector3(desired.x, 0f, desired.y) * .8f;
                if (IsBlocked(test, settings.PlayerRadius))
                {
                    Vector2 left = new Vector2(-desired.y, desired.x);
                    Vector2 right = -left;
                    var first = sweeper.Slot.SlotIndex % 2 == 0 ? left : right;
                    var second = -first;
                    Vector3 firstTest = sweeper.Root.position + new Vector3(first.x, 0f, first.y) * .8f;
                    desired = !IsBlocked(firstTest, settings.PlayerRadius) ? first : second;
                }
                input.SetMove(desired);
                offset.y = 0f;
                if (offset.sqrMagnitude < settings.SwingReach * settings.SwingReach && sweeper.Cooldown <= 0f &&
                    Vector3.Angle(sweeper.Root.forward, offset) < settings.SwingHalfAngle)
                    input.Press(PlayerAction.Jump);
            }
        }

        private void TickMice(float deltaTime)
        {
            foreach (var mouse in mice)
            {
                if (mouse.Respawn > 0f)
                {
                    mouse.Respawn -= deltaTime;
                    if (mouse.Respawn <= 0f) PlaceMouse(mouse);
                    continue;
                }
                if (mouse.Tunnel > 0f)
                {
                    mouse.Tunnel -= deltaTime;
                    if (mouse.Tunnel <= 0f) LeaveHole(mouse);
                    continue;
                }
                mouse.HideCooldown = Mathf.Max(0f, mouse.HideCooldown - deltaTime);
                if (mouse.HideCooldown <= 0f && InShelf(mouse.Root.position, 0f))
                {
                    // Hiding under a shelf: it stays still, then has to come out for a while.
                    mouse.ShelterTime += deltaTime;
                    if (mouse.ShelterTime < settings.ShelterSeconds) continue;
                    mouse.ShelterTime = 0f;
                    mouse.HideCooldown = settings.ShelterCooldownSeconds;
                    mouse.Direction = OutOfShelf(mouse.Root.position);
                }
                bool fleeing = TryFlee(mouse);
                if (fleeing && mouse.HideCooldown <= 0f && TryEnterHole(mouse)) continue;
                mouse.TurnTimer -= deltaTime;
                if (!fleeing && mouse.TurnTimer <= 0f)
                {
                    mouse.TurnTimer = Random.Range(.4f, 1.3f);
                    mouse.Direction = Quaternion.Euler(0f, Random.Range(-70f, 70f), 0f) * mouse.Direction;
                }
                float speed = mouse.Golden ? (fleeing ? settings.GoldenFleeSpeed : settings.GoldenSpeed) :
                    fleeing ? settings.MouseFleeSpeed : settings.MouseSpeed;
                Vector3 next = mouse.Root.position + mouse.Direction * (speed * deltaTime);
                if (IsBlockedForMouse(mouse, next, fleeing))
                {
                    mouse.Direction = Quaternion.Euler(0f, Random.Range(110f, 250f), 0f) * mouse.Direction;
                    mouse.TurnTimer = .25f;
                }
                else mouse.Root.position = next;
                mouse.Root.rotation = Quaternion.LookRotation(mouse.Direction);
            }
            SeparateMice();
            foreach (var mouse in mice)
                if (mouse.Respawn <= 0f && mouse.Tunnel <= 0f) mouse.Root.gameObject.SetActive(!IsHidden(mouse.Root.position));
        }

        private bool TryEnterHole(Mouse mouse)
        {
            for (int i = 0; i < holes.Length; i++)
            {
                Vector3 offset = holes[i] - mouse.Root.position;
                offset.y = 0f;
                if (offset.sqrMagnitude > settings.HoleEnterDistance * settings.HoleEnterDistance) continue;
                mouse.Tunnel = Random.Range(2.5f, 4f);
                mouse.ExitHole = (i + Random.Range(1, holes.Length)) % holes.Length;
                mouse.Root.gameObject.SetActive(false);
                return true;
            }
            return false;
        }

        private void LeaveHole(Mouse mouse)
        {
            Vector3 hole = holes[mouse.ExitHole];
            mouse.Tunnel = 0f;
            mouse.HideCooldown = settings.HoleCooldownSeconds;
            mouse.Root.position = hole;
            mouse.Direction = new Vector3(-hole.x, 0f, -hole.z).normalized;
            mouse.TurnTimer = Random.Range(.4f, 1.3f);
            mouse.Root.gameObject.SetActive(true);
        }

        private Vector3 OutOfShelf(Vector3 position)
        {
            foreach (var shelf in shelves)
            {
                var offset = new Vector3(position.x - shelf.center.x, 0f, position.z - shelf.center.y);
                if (Mathf.Abs(offset.x) < shelf.halfSize.x && Mathf.Abs(offset.z) < shelf.halfSize.y)
                    return offset.sqrMagnitude > .0001f ? offset.normalized : Vector3.forward;
            }
            return Vector3.forward;
        }

        // Pushes overlapping mice apart, half each, unless that would put one inside a wall or shelf.
        private void SeparateMice()
        {
            float minDistance = settings.MouseRadius * 2f;
            for (int i = 0; i < mice.Count; i++)
            {
                var a = mice[i];
                if (a.Respawn > 0f || a.Tunnel > 0f) continue;
                for (int j = i + 1; j < mice.Count; j++)
                {
                    var b = mice[j];
                    if (b.Respawn > 0f || b.Tunnel > 0f) continue;
                    Vector3 offset = b.Root.position - a.Root.position;
                    offset.y = 0f;
                    float distance = offset.magnitude;
                    if (distance >= minDistance) continue;
                    Vector3 push = (distance > .0001f ? offset / distance : a.Direction) * ((minDistance - distance) * .5f);
                    if (!IsBlocked(a.Root.position - push, settings.MouseRadius)) a.Root.position -= push;
                    if (!IsBlocked(b.Root.position + push, settings.MouseRadius)) b.Root.position += push;
                }
            }
        }

        // Runs away from the closest sweeper in range, heading for a nearby shelf or hole when one lies
        // away from the threat. When the way is blocked it slides sideways instead of freezing.
        private bool TryFlee(Mouse mouse)
        {
            Vector3 position = mouse.Root.position;
            Vector3 away = Vector3.zero;
            float closest = settings.MouseFleeRadius * settings.MouseFleeRadius;
            foreach (var sweeper in sweepers)
            {
                Vector3 offset = position - sweeper.Root.position;
                offset.y = 0f;
                float distance = offset.sqrMagnitude;
                if (distance >= closest) continue;
                closest = distance;
                away = distance < .0001f ? mouse.Direction : offset.normalized;
            }
            if (away == Vector3.zero) return false;
            Vector3 desired = away;
            if (mouse.HideCooldown <= 0f)
            {
                float best = settings.ShelterSeekRadius * settings.ShelterSeekRadius;
                foreach (var hole in holes) ConsiderShelter(position, away, hole, ref best, ref desired);
                foreach (var shelf in shelves)
                    ConsiderShelter(position, away, new Vector3(shelf.center.x, 0f, shelf.center.y), ref best, ref desired);
            }
            float probe = settings.MouseFleeSpeed * .25f;
            // Keep sliding the way it already heads, so it does not jitter along a wall.
            float side = Vector3.Cross(desired, mouse.Direction).y >= 0f ? 1f : -1f;
            foreach (float angle in FleeAngles)
            {
                Vector3 candidate = Quaternion.Euler(0f, angle * side, 0f) * desired;
                if (IsBlockedForMouse(mouse, position + candidate * probe, true)) continue;
                mouse.Direction = candidate;
                return true;
            }
            return true;
        }

        private static void ConsiderShelter(Vector3 position, Vector3 away, Vector3 target, ref float best, ref Vector3 desired)
        {
            Vector3 offset = target - position;
            offset.y = 0f;
            float distance = offset.sqrMagnitude;
            if (distance >= best || Vector3.Dot(offset, away) <= 0f) return;
            best = distance;
            desired = distance > .0001f ? offset / Mathf.Sqrt(distance) : away;
        }

        private static readonly float[] FleeAngles = { 0f, 35f, -35f, 70f, -70f, 105f, -105f };

        private bool IsBlocked(Vector3 position, float radius) =>
            IsOutsideArena(position, radius) || InShelf(position, radius);

        private bool IsOutsideArena(Vector3 position, float radius) =>
            position.x < -settings.HalfWidth + radius || position.x > settings.HalfWidth - radius ||
            position.z < -settings.HalfDepth + radius || position.z > settings.HalfDepth - radius;

        private bool InShelf(Vector3 position, float radius)
        {
            foreach (var shelf in shelves)
                if (Mathf.Abs(position.x - shelf.center.x) < shelf.halfSize.x + radius &&
                    Mathf.Abs(position.z - shelf.center.y) < shelf.halfSize.y + radius) return true;
            return false;
        }

        // Mice can slip under shelves (players cannot), but only to hide while scared.
        private bool IsBlockedForMouse(Mouse mouse, Vector3 position, bool fleeing)
        {
            if (IsOutsideArena(position, settings.MouseRadius)) return true;
            if (InShelf(mouse.Root.position, settings.MouseRadius)) return false; // Already under one: free to come out.
            if (fleeing && mouse.HideCooldown <= 0f) return false;
            return InShelf(position, settings.MouseRadius);
        }

        private Vector3 MoveAroundShelves(Vector3 position, Vector3 motion, float radius)
        {
            Vector3 x = position + new Vector3(motion.x, 0f, 0f);
            if (!IsBlocked(x, radius)) position = x;
            Vector3 z = position + new Vector3(0f, 0f, motion.z);
            if (!IsBlocked(z, radius)) position = z;
            return position;
        }

        private void PlaceMouse(Mouse mouse)
        {
            mouse.Respawn = 0f;
            mouse.Tunnel = 0f;
            mouse.ShelterTime = 0f;
            mouse.HideCooldown = 0f;
            Vector3 candidate = Vector3.zero;
            for (int attempt = 0; attempt < 48; attempt++)
            {
                candidate = new Vector3(Random.Range(-settings.HalfWidth + .5f, settings.HalfWidth - .5f),
                    MouseHeight, Random.Range(-settings.HalfDepth + .5f, settings.HalfDepth - .5f));
                bool clear = !IsBlocked(candidate, settings.MouseRadius + .08f);
                foreach (var sweeper in sweepers)
                    if ((sweeper.Root.position - candidate).sqrMagnitude < 2.25f) { clear = false; break; }
                if (clear) break;
            }
            mouse.Root.position = candidate;
            mouse.Direction = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward;
            mouse.TurnTimer = Random.Range(.4f, 1.3f);
            mouse.Root.gameObject.SetActive(true);
        }

        private void UpdateBroom(Sweeper sweeper, float deltaTime)
        {
            sweeper.SwingVisual = Mathf.Max(0f, sweeper.SwingVisual - deltaTime);
            sweeper.View.Animate(sweeper.SwingVisual / settings.SwingVisualSeconds, sweeper.Stun > 0f, deltaTime);
        }

        protected override void LateUpdate()
        {
            base.LateUpdate();
            if (arenaCamera == null) return;
            cameraShake = Mathf.Max(0f, cameraShake - Time.deltaTime);
            float strength = cameraShake * .35f;
            arenaCamera.transform.position = cameraHome +
                new Vector3(Random.Range(-strength, strength), 0f, Random.Range(-strength, strength));
        }

        protected override void WriteSnapshot(BinaryWriter writer)
        {
            writer.Write((byte)sweepers.Count);
            foreach (var sweeper in sweepers)
            {
                writer.Write(sweeper.Root.position.x);
                writer.Write(sweeper.Root.position.z);
                writer.Write(sweeper.Root.eulerAngles.y);
                writer.Write(sweeper.Stun);
                writer.Write(sweeper.SwingVisual);
            }
            writer.Write((byte)mice.Count);
            foreach (var mouse in mice)
            {
                writer.Write(mouse.Root.position.x);
                writer.Write(mouse.Root.position.z);
                writer.Write(mouse.Root.eulerAngles.y);
                writer.Write(Mathf.Max(mouse.Respawn, mouse.Tunnel));
            }
        }

        protected override void ReadSnapshot(BinaryReader reader)
        {
            int playerCount = reader.ReadByte();
            for (int i = 0; i < playerCount; i++)
            {
                float x = reader.ReadSingle(), z = reader.ReadSingle(), yaw = reader.ReadSingle();
                float stun = reader.ReadSingle(), swing = reader.ReadSingle();
                if (i >= sweepers.Count) continue;
                var sweeper = sweepers[i];
                sweeper.Root.SetPositionAndRotation(new Vector3(x, 0f, z), Quaternion.Euler(0f, yaw, 0f));
                if (sweeper.Stun <= 0f && stun > 0f) cameraShake = Mathf.Max(cameraShake, .17f);
                sweeper.Stun = stun;
                sweeper.SwingVisual = swing;
            }
            int mouseCount = reader.ReadByte();
            for (int i = 0; i < mouseCount; i++)
            {
                float x = reader.ReadSingle(), z = reader.ReadSingle(), yaw = reader.ReadSingle();
                float respawn = reader.ReadSingle();
                if (i >= mice.Count) continue;
                var mouse = mice[i];
                mouse.Root.SetPositionAndRotation(new Vector3(x, MouseHeight, z), Quaternion.Euler(0f, yaw, 0f));
                mouse.Respawn = respawn;
                mouse.Root.gameObject.SetActive(respawn <= 0f && !IsHidden(mouse.Root.position));
            }
        }

        private void BuildArena()
        {
            arena = Instantiate(arenaPrefab, transform, false);
            arena.name = "Garage mice arena";
            for (int i = 0; i < shelves.Length; i++)
            {
                var shelf = Instantiate(shelfPrefab, arena, false);
                shelf.name = $"Shelf {i}";
                shelf.localPosition = new Vector3(shelves[i].center.x, 0f, shelves[i].center.y);
                shelf.localScale = new Vector3(shelves[i].halfSize.x / shelfPrefabHalfSize.x, 1f,
                    shelves[i].halfSize.y / shelfPrefabHalfSize.y);
            }
            foreach (var point in holes)
            {
                bool sideWall = Mathf.Abs(point.x) > settings.HalfWidth - 1f;
                var onWall = sideWall ? new Vector3(Mathf.Sign(point.x) * (settings.HalfWidth - .03f), 0f, point.z)
                    : new Vector3(point.x, 0f, Mathf.Sign(point.z) * (settings.HalfDepth - .03f));
                var hole = Instantiate(holePrefab, arena, false);
                hole.name = "Mouse hole";
                hole.localPosition = onWall;
                hole.localRotation = sideWall ? Quaternion.Euler(0f, 90f, 0f) : Quaternion.identity;
            }
        }

        private void ConfigureCamera()
        {
            arenaCamera = Camera.main;
            if (arenaCamera == null) return;
            arenaCamera.orthographic = true;
            arenaCamera.transform.SetPositionAndRotation(new Vector3(0f, 20f, -4.25f), Quaternion.Euler(78f, 0f, 0f));
            arenaCamera.orthographicSize = Mathf.Max(settings.HalfDepth + 1.2f,
                (settings.HalfWidth + .6f) / Mathf.Max(.5f, arenaCamera.aspect));
            cameraHome = arenaCamera.transform.position;
        }

        private void SpawnSweeper(PlayerSlot slot)
        {
            var view = Spawns.Spawn(settings.PlayerPrefabFor(slot.SlotIndex), slot, arena);
            view.Setup(slot);
            sweepers.Add(new Sweeper { Slot = slot, Root = view.transform, View = view });
        }

        private void SpawnMouse(int index, bool golden)
        {
            var root = Instantiate(golden ? goldenMousePrefab : mousePrefab, arena, false);
            root.name = golden ? "Golden mouse" : $"Mouse_{index}";
            var mouse = new Mouse { Root = root, Golden = golden };
            mice.Add(mouse);
            if (IsReplica) root.gameObject.SetActive(false);
            else if (golden)
            {
                mouse.Respawn = settings.GoldenRespawnSeconds;
                root.gameObject.SetActive(false);
            }
            else PlaceMouse(mouse);
        }

        protected override void OnExit()
        {
            if (arenaCamera != null) arenaCamera.transform.position = cameraHome;
            if (hud != null) Destroy(hud);
            if (arena != null) Destroy(arena.gameObject);
            mice.Clear();
            sweepers.Clear();
        }
    }
}
