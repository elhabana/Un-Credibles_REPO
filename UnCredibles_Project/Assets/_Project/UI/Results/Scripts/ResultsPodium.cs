using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnCredibles.Localization;
using UnCredibles.Players;

namespace UnCredibles.UI.Results
{
    internal readonly struct PodiumEntry
    {
        public readonly int Placement;
        public readonly string Name;
        public readonly int Points;
        public readonly int SlotIndex; // picks the figure in the slot's colour

        public PodiumEntry(int placement, string name, int points, int slotIndex)
        {
            Placement = placement;
            Name = name;
            Points = points;
            SlotIndex = slotIndex;
        }
    }

    [Serializable]
    public sealed class PodiumPrefabs
    {
        [Tooltip("Floor under the pedestals; its pivot is at the top of the floor.")] public Transform stage;
        [Tooltip("1st to 4th. Pivot on the floor, with a child \"Figure spot\" where the figure stands.")]
        public Transform[] pedestals = new Transform[4];
        [Tooltip("Placed on the winner's head.")] public Transform crown;
    }

    // The final standings are represented by the same simple figures used in the garage lobby.
    internal sealed class ResultsPodium
    {
        private const float FloorTop = -2.5f;
        private const float FigureScale = .65f;
        private readonly Transform parent;
        private readonly PlayerFigureSet figures;
        private readonly PodiumPrefabs prefabs;
        private GameObject root;

        public ResultsPodium(Transform parent, PlayerFigureSet figures, PodiumPrefabs prefabs)
        {
            this.parent = parent;
            this.figures = figures;
            this.prefabs = prefabs;
        }

        public void SetVisible(bool visible)
        {
            if (root != null) root.SetActive(visible);
        }

        public void Render(IReadOnlyList<PodiumEntry> entries)
        {
            Clear();
            root = new GameObject("Final podium");
            root.transform.SetParent(parent, false);

            var stage = UnityEngine.Object.Instantiate(prefabs.stage, root.transform, false);
            stage.localPosition = new Vector3(0f, FloorTop, 0f);

            int count = Mathf.Min(entries.Count, 4);
            for (int i = 0; i < count; i++)
                CreatePlace(i, count, entries[i]);
        }

        public void Clear()
        {
            if (root != null) UnityEngine.Object.Destroy(root);
            root = null;
        }

        private void CreatePlace(int index, int count, PodiumEntry entry)
        {
            float x = Position(index, count);
            // Ties share a placement, so it is always 1 to 4.
            var pedestal = UnityEngine.Object.Instantiate(prefabs.pedestals[Mathf.Clamp(entry.Placement, 1, 4) - 1],
                root.transform, false);
            pedestal.name = $"Place {entry.Placement} pedestal";
            pedestal.localPosition = new Vector3(x, FloorTop, 0f);
            var spot = pedestal.Find("Figure spot");
            float height = spot.localPosition.y;
            float top = FloorTop + height;

            var figure = UnityEngine.Object.Instantiate(figures.For(entry.SlotIndex), root.transform, false);
            figure.name = $"Place {entry.Placement} figure";
            figure.transform.position = spot.position;
            figure.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); // face the camera
            figure.transform.localScale = Vector3.one * FigureScale;
            if (entry.Placement == 1)
            {
                var crown = UnityEngine.Object.Instantiate(prefabs.crown, root.transform, false);
                crown.localPosition = new Vector3(x, top + 1.69f, -.08f);
            }

            Label($"{Ordinal(entry.Placement)}  {entry.Name}", new Vector3(x, top + 2.02f, -.2f), 2.8f, 2.1f);
            Label(L10n.Format(L10n.UI, "podium.points", entry.Points), new Vector3(x, FloorTop + height * .48f, -.71f), 3.2f, 1.7f);
        }

        // There are at most 4 players; anything else falls back to the bare number.
        private static string Ordinal(int place) =>
            place >= 1 && place <= 4 ? L10n.Get(L10n.UI, "podium.place_" + place) : place.ToString();

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

        private static float Position(int index, int count)
        {
            if (count == 1) return 0f;
            if (count == 2) return index == 0 ? 1.1f : -1.1f;
            if (count == 3) return index == 0 ? 0f : index == 1 ? -2.2f : 2.2f;
            return index == 0 ? -1.1f : index == 1 ? -3.3f : index == 2 ? 1.1f : 3.3f;
        }
    }
}
