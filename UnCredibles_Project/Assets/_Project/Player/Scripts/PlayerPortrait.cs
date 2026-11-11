using UnityEngine;

namespace UnCredibles.Players
{
    // A player's character filmed for the UI (HUD badges, results): the slot's lobby figure, dressed the
    // same, stands on its own stage far below the scene in front of its own camera, and Texture is what that
    // camera sees. An expression or reaction only has to change Figure.
    // Destroy the GameObject to free it; it also goes away with its scene.
    public sealed class PlayerPortrait : MonoBehaviour
    {
        private const float StageDepth = -2000f;
        private const float StageSpacing = 20f; // one spot per slot
        private const int Pixels = 256;
        private const float HeadDrop = .2f;  // camera centre below the top of the figure, in figure heights
        private const float HeadSpan = .22f; // half the framed height, in figure heights
        // Where the scene's sun comes from, seen by the figure: front, a bit above and to one side.
        private static readonly Quaternion KeyLight = Quaternion.Euler(-25f, 30f, 0f);

        private RenderTexture texture;

        public Texture Texture => texture;
        public GameObject Figure { get; private set; }

        // Null when there is no figure for the slot. background: what the picture shows behind the figure.
        public static PlayerPortrait Create(PlayerFigureSet figures, int slotIndex, CharacterLoadout loadout, Color background)
        {
            var prefab = figures != null ? figures.For(slotIndex) : null;
            if (prefab == null) return null;
            var portrait = new GameObject("Portrait stage " + slotIndex).AddComponent<PlayerPortrait>();
            portrait.Build(prefab, slotIndex, loadout, background);
            return portrait;
        }

        private void Build(GameObject prefab, int slotIndex, CharacterLoadout loadout, Color background)
        {
            transform.position = new Vector3(slotIndex * StageSpacing, StageDepth, 0f);
            Figure = Instantiate(prefab, transform, false);
            var outfit = Figure.GetComponentInChildren<PlayerCharacterOutfit>();
            if (outfit != null) outfit.Apply(loadout);

            var bounds = new Bounds(transform.position, Vector3.zero);
            bool any = false;
            foreach (var part in Figure.GetComponentsInChildren<Renderer>())
            {
                if (any) bounds.Encapsulate(part.bounds);
                else bounds = part.bounds;
                any = true;
            }
            float height = Mathf.Max(bounds.size.y, .01f);
            var head = new Vector3(bounds.center.x, bounds.max.y - height * HeadDrop, bounds.center.z);
            var forward = Figure.transform.forward;

            texture = new RenderTexture(Pixels, Pixels, 16) { antiAliasing = 4 };
            var view = new GameObject("Camera").AddComponent<Camera>();
            view.transform.SetParent(transform, false);
            view.transform.SetPositionAndRotation(head + forward * 10f, Quaternion.LookRotation(-forward));
            view.orthographic = true;
            view.orthographicSize = height * HeadSpan;
            view.aspect = 1f;
            view.nearClipPlane = .1f;
            view.farClipPlane = 30f;
            view.clearFlags = CameraClearFlags.SolidColor;
            view.backgroundColor = background;
            view.targetTexture = texture;

            // No light of its own (it would also light the scene): the whole stage turns so the scene's sun
            // hits the face instead of the top of the head.
            foreach (var sun in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (sun.type != LightType.Directional) continue;
                transform.rotation = Quaternion.FromToRotation(KeyLight * forward, -sun.transform.forward);
                break;
            }
        }

        private void OnDestroy()
        {
            if (texture == null) return;
            texture.Release();
            Destroy(texture);
        }
    }
}
