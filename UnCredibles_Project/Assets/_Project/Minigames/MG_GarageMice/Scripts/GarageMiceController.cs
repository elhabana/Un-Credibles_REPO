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
        private const float HalfWidth = 10f;
        private const float HalfDepth = 6f;
        private const float PlayerRadius = .38f;
        private const float MouseRadius = .2f;
        private const float MoveSpeed = 4.4f;
        private const float MouseSpeed = 2.1f;
        private const float SwingReach = 1.5f;
        private const float SwingHalfAngle = 65f;
        private const float SwingCooldown = .55f;
        private const float SwingVisualSeconds = .22f;
        private const float StunSeconds = 1.25f;
        private const float StunImmunitySeconds = 1.8f;
        private const float MouseRespawnSeconds = 2f;
        private const int MouseCount = 26;

        private struct Shelf
        {
            public Vector2 Center;
            public Vector2 HalfSize;
            public Shelf(float x, float z, float halfX, float halfZ)
            {
                Center = new Vector2(x, z);
                HalfSize = new Vector2(halfX, halfZ);
            }
        }

        private sealed class Sweeper
        {
            public PlayerSlot Slot;
            public Transform Root;
            public Transform Visual;
            public Transform Broom;
            public Transform Stars;
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
        }

        private readonly List<Sweeper> sweepers = new List<Sweeper>(PlayerRegistry.MaxPlayers);
        private readonly List<Mouse> mice = new List<Mouse>(MouseCount);
        private readonly List<Material> materials = new List<Material>();
        private readonly Shelf[] shelves =
        {
            new Shelf(-6.6f, -2.1f, 1.2f, .55f), new Shelf(-6.6f, 2.1f, 1.2f, .55f),
            new Shelf(6.6f, -2.1f, 1.2f, .55f), new Shelf(6.6f, 2.1f, 1.2f, .55f),
            new Shelf(-3.9f, .5f, 1.05f, .55f), new Shelf(3.9f, .5f, 1.05f, .55f)
        };
        private Transform arena;
        private Camera arenaCamera;
        private Vector3 cameraHome;
        private float cameraShake;
        private GarageMiceHud hud;

        protected override void OnInitialize(MinigameContext context)
        {
            ConfigureCamera();
            BuildArena();
            foreach (var slot in Players) BuildSweeper(slot);
            for (int i = 0; i < MouseCount; i++) BuildMouse(i);
            hud = gameObject.AddComponent<GarageMiceHud>();
            hud.Setup(this);
        }

        protected override void OnGameStarted() { }

        private void Update()
        {
            if (State < MinigameState.Waiting || State >= MinigameState.Exit) return;
            float deltaTime = Time.deltaTime;
            foreach (var sweeper in sweepers) UpdateBroom(sweeper, deltaTime);
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
                    direction * (MoveSpeed * deltaTime), PlayerRadius);
            }
            if (sweeper.Cooldown <= 0f && input != null && input.WasPressed(PlayerAction.Jump))
                Swing(sweeper);
        }

        private void Swing(Sweeper attacker)
        {
            attacker.Cooldown = SwingCooldown;
            attacker.SwingVisual = SwingVisualSeconds;
            Vector3 origin = attacker.Root.position;
            Vector3 facing = attacker.Root.forward;
            float reach = SwingReach + MouseRadius;
            foreach (var mouse in mice)
            {
                if (mouse.Respawn > 0f || !InSwing(origin, facing, mouse.Root.position, reach)) continue;
                mouse.Respawn = MouseRespawnSeconds;
                mouse.Root.gameObject.SetActive(false);
                Score.AddScore(attacker.Slot.PlayerId, 1);
            }
            foreach (var rival in sweepers)
            {
                if (rival == attacker || rival.Immunity > 0f ||
                    !InSwing(origin, facing, rival.Root.position, SwingReach + PlayerRadius)) continue;
                rival.Stun = StunSeconds;
                rival.Immunity = StunImmunitySeconds;
                cameraShake = Mathf.Max(cameraShake, .17f);
            }
        }

        private static bool InSwing(Vector3 origin, Vector3 facing, Vector3 target, float reach)
        {
            Vector3 offset = target - origin;
            offset.y = 0f;
            return offset.sqrMagnitude <= reach * reach &&
                   (offset.sqrMagnitude < .01f || Vector3.Angle(facing, offset) <= SwingHalfAngle);
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
                    if (mouse.Respawn > 0f) continue;
                    float distance = (mouse.Root.position - sweeper.Root.position).sqrMagnitude;
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
                if (IsBlocked(test, PlayerRadius))
                {
                    Vector2 left = new Vector2(-desired.y, desired.x);
                    Vector2 right = -left;
                    var first = sweeper.Slot.SlotIndex % 2 == 0 ? left : right;
                    var second = -first;
                    Vector3 firstTest = sweeper.Root.position + new Vector3(first.x, 0f, first.y) * .8f;
                    desired = !IsBlocked(firstTest, PlayerRadius) ? first : second;
                }
                input.SetMove(desired);
                if (best < SwingReach * SwingReach && sweeper.Cooldown <= 0f &&
                    Vector3.Angle(sweeper.Root.forward, offset) < SwingHalfAngle)
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
                mouse.TurnTimer -= deltaTime;
                if (mouse.TurnTimer <= 0f)
                {
                    mouse.TurnTimer = Random.Range(.4f, 1.3f);
                    mouse.Direction = Quaternion.Euler(0f, Random.Range(-70f, 70f), 0f) * mouse.Direction;
                }
                Vector3 next = mouse.Root.position + mouse.Direction * (MouseSpeed * deltaTime);
                if (IsBlocked(next, MouseRadius))
                {
                    mouse.Direction = Quaternion.Euler(0f, Random.Range(110f, 250f), 0f) * mouse.Direction;
                    mouse.TurnTimer = .25f;
                }
                else mouse.Root.position = next;
                mouse.Root.rotation = Quaternion.LookRotation(mouse.Direction);
            }
        }

        private static Vector3 Clamp(Vector3 position, float radius) => new Vector3(
            Mathf.Clamp(position.x, -HalfWidth + radius, HalfWidth - radius),
            position.y,
            Mathf.Clamp(position.z, -HalfDepth + radius, HalfDepth - radius));

        private bool IsBlocked(Vector3 position, float radius)
        {
            if (position.x < -HalfWidth + radius || position.x > HalfWidth - radius ||
                position.z < -HalfDepth + radius || position.z > HalfDepth - radius) return true;
            foreach (var shelf in shelves)
                if (Mathf.Abs(position.x - shelf.Center.x) < shelf.HalfSize.x + radius &&
                    Mathf.Abs(position.z - shelf.Center.y) < shelf.HalfSize.y + radius) return true;
            return false;
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
            Vector3 candidate = Vector3.zero;
            for (int attempt = 0; attempt < 48; attempt++)
            {
                candidate = new Vector3(Random.Range(-HalfWidth + .5f, HalfWidth - .5f),
                    .16f, Random.Range(-HalfDepth + .5f, HalfDepth - .5f));
                bool clear = !IsBlocked(candidate, MouseRadius + .08f);
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
            float t = sweeper.SwingVisual / SwingVisualSeconds;
            sweeper.Broom.localRotation = Quaternion.Euler(0f, t > 0f ? -85f * Mathf.Sin(t * Mathf.PI) : 0f, -20f);
            bool stunned = sweeper.Stun > 0f;
            sweeper.Stars.gameObject.SetActive(stunned);
            if (stunned)
            {
                sweeper.Stars.Rotate(Vector3.up, 290f * deltaTime, Space.Self);
                sweeper.Visual.localPosition = new Vector3(Mathf.Sin(Time.time * 38f) * .055f, 0f, 0f);
            }
            else sweeper.Visual.localPosition = Vector3.zero;
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
                writer.Write(mouse.Respawn);
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
                mouse.Root.SetPositionAndRotation(new Vector3(x, .16f, z), Quaternion.Euler(0f, yaw, 0f));
                mouse.Respawn = respawn;
                mouse.Root.gameObject.SetActive(respawn <= 0f);
            }
        }

        private void BuildArena()
        {
            arena = new GameObject("Garage mice arena").transform;
            arena.SetParent(transform, false);
            Material concrete = MakeMaterial(new Color(.24f, .28f, .31f));
            Material wall = MakeMaterial(new Color(.13f, .17f, .2f));
            Material metal = MakeMaterial(new Color(.34f, .4f, .44f));
            Material wood = MakeMaterial(new Color(.48f, .32f, .18f));
            float visibleWidth = arenaCamera != null ? arenaCamera.orthographicSize * arenaCamera.aspect * 2f + 2f : 24f;
            float visibleDepth = arenaCamera != null ? arenaCamera.orthographicSize * 2f + 2f : 16f;
            Box("Concrete floor", new Vector3(0f, -.16f, 0f),
                new Vector3(Mathf.Max(visibleWidth, HalfWidth * 2f + 1f), .3f,
                    Mathf.Max(visibleDepth, HalfDepth * 2f + 1f)), concrete);
            Box("Back wall", new Vector3(0f, 1.3f, HalfDepth + .15f), new Vector3(HalfWidth * 2f + .6f, 2.6f, .3f), wall);
            Box("Left wall", new Vector3(-HalfWidth - .15f, 1.3f, 0f), new Vector3(.3f, 2.6f, HalfDepth * 2f + .6f), wall);
            Box("Right wall", new Vector3(HalfWidth + .15f, 1.3f, 0f), new Vector3(.3f, 2.6f, HalfDepth * 2f + .6f), wall);
            Box("Front wall", new Vector3(0f, 1.3f, -HalfDepth - .15f),
                new Vector3(HalfWidth * 2f + .6f, 2.6f, .3f), wall);
            for (int i = 0; i < shelves.Length; i++) BuildShelf(i, shelves[i], metal, wood);
        }

        private void BuildShelf(int index, Shelf shelf, Material metal, Material wood)
        {
            float x = shelf.Center.x, z = shelf.Center.y;
            float width = shelf.HalfSize.x * 2f, depth = shelf.HalfSize.y * 2f;
            for (int side = -1; side <= 1; side += 2)
                for (int end = -1; end <= 1; end += 2)
                    Box($"Shelf {index} post", new Vector3(x + side * shelf.HalfSize.x,
                        .68f, z + end * shelf.HalfSize.y), new Vector3(.11f, 1.35f, .11f), metal);
            Box($"Shelf {index} lower board", new Vector3(x, .3f, z), new Vector3(width, .1f, depth), wood);
            Box($"Shelf {index} top board", new Vector3(x, 1.35f, z), new Vector3(width, .13f, depth), wood);
            Box($"Shelf {index} supplies", new Vector3(x, 1.54f, z),
                new Vector3(width * .63f, .25f, depth * .6f), wood);
        }

        private void ConfigureCamera()
        {
            arenaCamera = Camera.main;
            if (arenaCamera == null) return;
            arenaCamera.orthographic = true;
            arenaCamera.transform.SetPositionAndRotation(new Vector3(0f, 20f, -4.25f), Quaternion.Euler(78f, 0f, 0f));
            arenaCamera.orthographicSize = Mathf.Max(HalfDepth + 1.2f,
                (HalfWidth + .6f) / Mathf.Max(.5f, arenaCamera.aspect));
            cameraHome = arenaCamera.transform.position;
        }

        private void BuildSweeper(PlayerSlot slot)
        {
            var root = new GameObject($"Sweeper_{slot.SlotIndex}").transform;
            root.SetParent(arena, false);
            root.position = new Vector3((slot.SlotIndex - 1.5f) * 1.2f, 0f, 3.4f);
            var visual = new GameObject("Stun wobble").transform;
            visual.SetParent(root, false);
            var body = Primitive("Body", PrimitiveType.Capsule, visual, new Vector3(0f, .8f, 0f),
                new Vector3(.55f, .8f, .55f), MakeMaterial(PlayerIdentity.ColorFor(slot.SlotIndex, slot.IsAI)));
            var head = Primitive("Head", PrimitiveType.Sphere, visual, new Vector3(0f, 1.68f, 0f),
                Vector3.one * .48f, MakeMaterial(new Color(.92f, .76f, .61f)));
            var broom = new GameObject("Broom swing").transform;
            broom.SetParent(visual, false);
            broom.localPosition = new Vector3(.35f, 1f, .25f);
            Primitive("Handle", PrimitiveType.Cylinder, broom, new Vector3(0f, -.18f, .55f),
                new Vector3(.055f, .65f, .055f), MakeMaterial(new Color(.58f, .32f, .13f)));
            Primitive("Brush", PrimitiveType.Cube, broom, new Vector3(0f, -.76f, .65f),
                new Vector3(.65f, .15f, .22f), MakeMaterial(new Color(.83f, .7f, .42f)));
            var stars = new GameObject("Stun stars").transform;
            stars.SetParent(visual, false);
            stars.localPosition = new Vector3(0f, 2.05f, 0f);
            Material starMaterial = MakeMaterial(new Color(1f, .85f, .15f));
            for (int i = 0; i < 4; i++)
            {
                float angle = i * Mathf.PI * .5f;
                var star = Primitive("Star", PrimitiveType.Cube, stars,
                    new Vector3(Mathf.Cos(angle) * .43f, .08f * (i % 2), Mathf.Sin(angle) * .43f),
                    new Vector3(.2f, .06f, .2f), starMaterial);
                star.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
            }
            stars.gameObject.SetActive(false);
            PlayerPresentation.Attach(root.gameObject.AddComponent<GarageMicePlayerView>(), slot,
                new[] { body.GetComponent<Renderer>(), head.GetComponent<Renderer>() });
            sweepers.Add(new Sweeper { Slot = slot, Root = root, Visual = visual, Broom = broom, Stars = stars });
        }

        private void BuildMouse(int index)
        {
            var root = new GameObject($"Mouse_{index}").transform;
            root.SetParent(arena, false);
            Material fur = MakeMaterial(new Color(.12f + index % 3 * .05f, .1f, .09f));
            Primitive("Mouse body", PrimitiveType.Sphere, root, Vector3.zero, new Vector3(.42f, .25f, .28f), fur);
            Primitive("Head", PrimitiveType.Sphere, root, new Vector3(0f, .03f, .22f), Vector3.one * .2f, fur);
            Primitive("Left ear", PrimitiveType.Sphere, root, new Vector3(-.1f, .14f, .23f), Vector3.one * .11f, fur);
            Primitive("Right ear", PrimitiveType.Sphere, root, new Vector3(.1f, .14f, .23f), Vector3.one * .11f, fur);
            Material eyes = MakeMaterial(new Color(.94f, .84f, .68f));
            Primitive("Left eye", PrimitiveType.Sphere, root, new Vector3(-.07f, .08f, .37f), Vector3.one * .045f, eyes);
            Primitive("Right eye", PrimitiveType.Sphere, root, new Vector3(.07f, .08f, .37f), Vector3.one * .045f, eyes);
            var tail = Primitive("Tail", PrimitiveType.Cylinder, root, new Vector3(0f, -.02f, -.43f),
                new Vector3(.025f, .27f, .025f), MakeMaterial(new Color(.54f, .29f, .32f)));
            tail.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var mouse = new Mouse { Root = root };
            mice.Add(mouse);
            if (!IsReplica) PlaceMouse(mouse);
            else root.gameObject.SetActive(false);
        }

        private GameObject Box(string name, Vector3 position, Vector3 size, Material material) =>
            Primitive(name, PrimitiveType.Cube, arena, position, size, material);

        private static GameObject Primitive(string name, PrimitiveType type, Transform parent,
            Vector3 position, Vector3 size, Material material)
        {
            var instance = GameObject.CreatePrimitive(type);
            instance.name = name;
            instance.transform.SetParent(parent, false);
            instance.transform.localPosition = position;
            instance.transform.localScale = size;
            instance.GetComponent<Renderer>().sharedMaterial = material;
            Destroy(instance.GetComponent<Collider>());
            return instance;
        }

        private Material MakeMaterial(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            var material = new Material(shader) { color = color };
            materials.Add(material);
            return material;
        }

        protected override void OnExit()
        {
            if (arenaCamera != null) arenaCamera.transform.position = cameraHome;
            if (hud != null) Destroy(hud);
            if (arena != null) Destroy(arena.gameObject);
            foreach (var material in materials) Destroy(material);
            materials.Clear();
            mice.Clear();
            sweepers.Clear();
        }
    }
}
