using System.Collections.Generic;
using UnCredibles.Players;
using UnityEngine;

namespace UnCredibles.Trailer
{
    // Previs only: dresses the minigame placeholders (capsules, empty mowers) as the lobby heroes.
    // The hero model is parented to the placeholder's visual, so the game's own jumps, squashes, stun
    // shakes and spins still move it. Runs, stands or sits (riding the mower) depending on the mode.
    [DefaultExecutionOrder(2000)]
    public sealed class TrailerFigures : MonoBehaviour
    {
        public enum Pose { Stand, Run, Sit }

        [Tooltip("Component type name of the player avatar, e.g. ChurroPlayer.")]
        public string playerType;
        [Tooltip("Child of the avatar that the game animates; the hero is parented to it.")]
        public string visualName = "Visual";
        [Tooltip("Placeholder renderers to hide (object names).")]
        public string[] hide = { "Body", "Nose", "Head" };
        public PlayerFigureSet figures;
        public float scale = .5f;
        public Vector3 offset;
        public Pose pose = Pose.Stand;
        [Tooltip("Sit: height of the seat in the visual's space; the hips are placed on it (offset.y is ignored).")]
        public float seatHeight = .7f;
        [Tooltip("Run: speed (m/s) above which the hero plays the run loop.")]
        public float runThreshold = .4f;

        private sealed class Dressed
        {
            public Transform Root;
            public Transform Figure;
            public PlayerCharacterAnimator Character;
            public Vector3 LastPosition;
            public Transform[] Legs; // thigh, shin, foot per side (sit pose)
            public Transform[] Arms; // upper arm, forearm, hand per side (sit pose)
        }

        private readonly List<Dressed> dressed = new();
        private readonly HashSet<Transform> seen = new();

        private void LateUpdate()
        {
            DressNewPlayers();
            foreach (var hero in dressed)
            {
                if (hero.Root == null) continue;
                if (pose == Pose.Run && hero.Character != null)
                {
                    float speed = (hero.Root.position - hero.LastPosition).magnitude / Mathf.Max(Time.deltaTime, 1e-4f);
                    hero.Character.SetRunning(speed > runThreshold);
                }
                hero.LastPosition = hero.Root.position;
                if (pose == Pose.Sit) Sit(hero);
            }
        }

        private void DressNewPlayers()
        {
            foreach (var behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            {
                if (behaviour.GetType().Name != playerType || seen.Contains(behaviour.transform)) continue;
                var root = behaviour.transform;
                seen.Add(root);
                var visual = Find(root, visualName) ?? root;
                foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                    if (System.Array.IndexOf(hide, renderer.name) >= 0) renderer.enabled = false;

                // Avatars are named after their slot ("..._Red_0").
                int slot = Mathf.Clamp(root.name[^1] - '0', 0, 3);
                var figure = Instantiate(figures.For(slot), visual, false).transform;
                figure.name = "Trailer hero";
                figure.localPosition = offset;
                figure.localRotation = Quaternion.identity;
                figure.localScale = Vector3.one * scale;
                var hero = new Dressed
                {
                    Root = root, Figure = figure, LastPosition = root.position,
                    Character = figure.GetComponentInChildren<PlayerCharacterAnimator>()
                };
                if (pose == Pose.Sit)
                {
                    hero.Legs = new[] { Find(figure, "thigh.L"), Find(figure, "shin.L"), Find(figure, "foot.L"),
                        Find(figure, "thigh.R"), Find(figure, "shin.R"), Find(figure, "foot.R") };
                    hero.Arms = new[] { Find(figure, "upper_arm.L"), Find(figure, "forearm.L"), Find(figure, "hand.L"),
                        Find(figure, "upper_arm.R"), Find(figure, "forearm.R"), Find(figure, "hand.R") };
                    if (hero.Legs[0] != null)
                    {
                        float hips = figure.InverseTransformPoint(hero.Legs[0].position).y * scale;
                        figure.localPosition = new Vector3(offset.x, seatHeight - hips, offset.z);
                    }
                }
                dressed.Add(hero);
            }
        }

        // Riding pose over the idle animation: thighs forward, shins down, arms reaching for the wheel.
        private static void Sit(Dressed hero)
        {
            var forward = hero.Figure.forward;
            var down = -hero.Figure.up;
            for (int side = 0; side < 2; side++)
            {
                Aim(hero.Legs[side * 3], hero.Legs[side * 3 + 1], (forward * .95f + down * .3f).normalized);
                Aim(hero.Legs[side * 3 + 1], hero.Legs[side * 3 + 2], (down * .9f + forward * .25f).normalized);
                Aim(hero.Arms[side * 3], hero.Arms[side * 3 + 1], (forward * .8f + down * .6f).normalized);
                Aim(hero.Arms[side * 3 + 1], hero.Arms[side * 3 + 2], (forward * .95f + down * .1f).normalized);
            }
        }

        // Rotates a bone so the direction towards its child points along the wanted direction.
        private static void Aim(Transform bone, Transform child, Vector3 direction)
        {
            if (bone == null || child == null) return;
            var current = child.position - bone.position;
            if (current.sqrMagnitude < 1e-8f) return;
            bone.rotation = Quaternion.FromToRotation(current, direction) * bone.rotation;
        }

        private static Transform Find(Transform parent, string childName)
        {
            if (parent.name == childName) return parent;
            foreach (Transform child in parent)
            {
                var found = Find(child, childName);
                if (found != null) return found;
            }
            return null;
        }
    }
}
