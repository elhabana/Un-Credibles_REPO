using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace UnCredibles.UI.Results
{
    internal readonly struct PodiumEntry
    {
        public readonly int Placement;
        public readonly string Name;
        public readonly int Points;
        public readonly Color CharacterColor;

        public PodiumEntry(int placement, string name, int points, Color characterColor)
        {
            Placement = placement;
            Name = name;
            Points = points;
            CharacterColor = characterColor;
        }
    }

    // The final standings are represented by the same simple figures used in the garage lobby.
    internal sealed class ResultsPodium
    {
        private const float FloorTop = -2.5f;
        private readonly Transform parent;
        private readonly List<Material> materials = new List<Material>();
        private GameObject root;

        public ResultsPodium(Transform parent) => this.parent = parent;

        public void SetVisible(bool visible)
        {
            if (root != null) root.SetActive(visible);
        }

        public void Render(IReadOnlyList<PodiumEntry> entries)
        {
            Clear();
            root = new GameObject("Final podium");
            root.transform.SetParent(parent, false);

            Box("Stage", new Vector3(0f, FloorTop - .15f, .35f), new Vector3(11f, .3f, 3.2f),
                new Color(.08f, .11f, .17f));
            Box("Stage edge", new Vector3(0f, FloorTop - .02f, -1.27f), new Vector3(11f, .09f, .08f),
                new Color(.94f, .57f, .16f));

            int count = Mathf.Min(entries.Count, 4);
            for (int i = 0; i < count; i++)
                CreatePlace(i, count, entries[i]);
        }

        public void Clear()
        {
            if (root != null) Object.Destroy(root);
            root = null;
            foreach (var material in materials) Object.Destroy(material);
            materials.Clear();
        }

        private void CreatePlace(int index, int count, PodiumEntry entry)
        {
            float x = Position(index, count);
            float height = Height(entry.Placement);
            float top = FloorTop + height;
            Color metal = MetalColor(entry.Placement);
            Box($"Place {entry.Placement} pedestal", new Vector3(x, FloorTop + height * .5f, 0f),
                new Vector3(1.85f, height, 1.35f), metal);
            Box("Pedestal rim", new Vector3(x, top - .06f, 0f), new Vector3(1.95f, .12f, 1.45f),
                Color.Lerp(metal, Color.white, .24f));

            var bodyMaterial = MakeMaterial(entry.CharacterColor);
            var eyeMaterial = MakeMaterial(new Color(.025f, .03f, .04f));
            Primitive("Body", PrimitiveType.Capsule, new Vector3(x, top + .58f, -.02f),
                new Vector3(.46f, .57f, .37f), bodyMaterial);
            Primitive("Head", PrimitiveType.Sphere, new Vector3(x, top + 1.34f, -.08f),
                new Vector3(.58f, .58f, .55f), bodyMaterial);
            Primitive("Left eye", PrimitiveType.Sphere, new Vector3(x - .13f, top + 1.4f, -.345f),
                Vector3.one * .065f, eyeMaterial);
            Primitive("Right eye", PrimitiveType.Sphere, new Vector3(x + .13f, top + 1.4f, -.345f),
                Vector3.one * .065f, eyeMaterial);
            if (entry.Placement == 1)
                Box("Winner crown", new Vector3(x, top + 1.69f, -.08f), new Vector3(.55f, .14f, .42f),
                    new Color(1f, .72f, .13f));

            Label($"{entry.Placement}º  {entry.Name}", new Vector3(x, top + 2.02f, -.2f), 2.8f, 2.1f);
            Label($"{entry.Points} PTS", new Vector3(x, FloorTop + height * .48f, -.71f), 3.2f, 1.7f);
        }

        private void Primitive(string name, PrimitiveType type, Vector3 position, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            Object.Destroy(go.GetComponent<Collider>());
        }

        private void Box(string name, Vector3 position, Vector3 scale, Color color) =>
            Primitive(name, PrimitiveType.Cube, position, scale, MakeMaterial(color));

        private void Label(string value, Vector3 position, float fontSize, float width)
        {
            var text = new GameObject(value).AddComponent<TextMeshPro>();
            text.transform.SetParent(root.transform, false);
            text.transform.localPosition = position;
            text.text = value;
            text.font = TMP_Settings.defaultFontAsset;
            text.fontSize = fontSize;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.enableAutoSizing = true;
            text.fontSizeMin = 1.5f;
            text.fontSizeMax = fontSize;
            text.rectTransform.sizeDelta = new Vector2(width, .55f);
            text.color = Color.white;
            text.raycastTarget = false;
        }

        private Material MakeMaterial(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            var material = new Material(shader) { color = color };
            materials.Add(material);
            return material;
        }

        private static float Position(int index, int count)
        {
            if (count == 1) return 0f;
            if (count == 2) return index == 0 ? 1.1f : -1.1f;
            if (count == 3) return index == 0 ? 0f : index == 1 ? -2.2f : 2.2f;
            return index == 0 ? -1.1f : index == 1 ? -3.3f : index == 2 ? 1.1f : 3.3f;
        }

        private static float Height(int placement) => placement == 1 ? 1.85f :
            placement == 2 ? 1.3f : placement == 3 ? .9f : .6f;

        private static Color MetalColor(int placement) => placement == 1 ? new Color(.82f, .55f, .12f) :
            placement == 2 ? new Color(.48f, .57f, .67f) :
            placement == 3 ? new Color(.56f, .31f, .18f) : new Color(.22f, .3f, .37f);
    }
}
