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
        public readonly bool IsAI;

        public PodiumEntry(int placement, string name, int points, int slotIndex, bool isAI)
        {
            IsAI = isAI;
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
        [Tooltip("1st to 4th. Pivot on the floor.")]
        public PodiumPedestal[] pedestals = new PodiumPedestal[4];
        [Tooltip("Placed on the winner's head.")] public Transform crown;
    }

    // One podium place popping up from the floor (ease-out back) once its turn comes.
    internal sealed class PodiumPop : MonoBehaviour
    {
        private const float Seconds = .45f;
        public float Delay;
        private float time;

        private void Awake() => transform.localScale = Vector3.zero;

        private void Update()
        {
            time += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01((time - Delay) / Seconds);
            float k = t - 1f;
            transform.localScale = Vector3.one * (t <= 0f ? 0f : 1f + 2.70158f * k * k * k + 1.70158f * k * k);
            if (t >= 1f) enabled = false;
        }
    }

    // The final standings are represented by the same simple figures used in the garage lobby.
    internal sealed class ResultsPodium
    {
        private const float FloorTop = -2.5f;
        private const float FigureScale = .65f;
        private const float RevealStart = .4f;
        private const float RevealStep = .55f; // between two places: last place first, the winner last
        private static readonly Color[] MedalColors =
        {
            new Color(1f, .78f, .2f), new Color(.82f, .84f, .88f), new Color(.85f, .55f, .3f), Color.white,
        };
        private static readonly Color32 Outline = new Color32(12, 16, 24, 255);
        private readonly TMP_FontAsset titleFont;
        private bool reveal;
        private readonly Transform parent;
        private readonly PlayerFigureSet figures;
        private readonly PodiumPrefabs prefabs;
        private GameObject root;

        public ResultsPodium(Transform parent, PlayerFigureSet figures, PodiumPrefabs prefabs, TMP_FontAsset titleFont)
        {
            this.titleFont = titleFont;
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
            reveal = root == null; // a repeated render (language change) shows the podium already standing
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
            int firstChild = root.transform.childCount;
            // Ties share a placement, so it is always 1 to 4.
            var pedestal = UnityEngine.Object.Instantiate(prefabs.pedestals[Mathf.Clamp(entry.Placement, 1, 4) - 1],
                root.transform, false);
            pedestal.name = $"Place {entry.Placement} pedestal";
            pedestal.transform.localPosition = new Vector3(x, FloorTop, 0f);
            var spot = pedestal.FigureSpot;
            float height = spot.localPosition.y;
            float top = FloorTop + height;

            var figure = UnityEngine.Object.Instantiate(figures.For(entry.SlotIndex), root.transform, false);
            figure.name = $"Place {entry.Placement} figure";
            figure.transform.position = spot.position;
            figure.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); // face the camera
            figure.transform.localScale = Vector3.one * FigureScale;
            // Dressed as in the lobby; the crown sits on top of the helmet.
            float helmet = 0f;
            var outfit = figure.GetComponentInChildren<UnCredibles.Players.PlayerCharacterOutfit>();
            var players = UnCredibles.Core.CoreRoot.Instance != null ? UnCredibles.Core.CoreRoot.Instance.Players : null;
            if (outfit != null && players != null && entry.SlotIndex >= 0 && entry.SlotIndex < players.Slots.Count)
            {
                outfit.Apply(players.Slots[entry.SlotIndex].Loadout);
                helmet = outfit.ExtraHeadHeight;
            }
            if (entry.Placement == 1)
            {
                var crown = UnityEngine.Object.Instantiate(prefabs.crown, root.transform, false);
                crown.localPosition = new Vector3(x, top + 1.69f + helmet, -.08f);
            }

            // Place in its medal colour and the name in the player's, lightened to read on the dark backdrop.
            var medal = MedalColors[Mathf.Clamp(entry.Placement, 1, 4) - 1];
            var nameColor = Color.Lerp(PlayerIdentity.ColorFor(entry.SlotIndex, entry.IsAI), Color.white, .35f);
            Label($"<color=#{ColorUtility.ToHtmlStringRGB(medal)}>{Ordinal(entry.Placement)}</color>  " +
                  $"<color=#{ColorUtility.ToHtmlStringRGB(nameColor)}><noparse>{entry.Name}</noparse></color>",
                new Vector3(x, top + 2.02f + helmet, -.2f), 2.8f, 2.1f, null);
            Label(L10n.Format(L10n.UI, "podium.points", entry.Points), new Vector3(x, FloorTop + height * .48f, -.71f),
                3.2f, 1.7f, titleFont).color = medal;

            if (!reveal) return;
            // Pedestal, figure, crown and labels pop up together from the floor.
            var group = new GameObject($"Place {entry.Placement}").transform;
            group.SetParent(root.transform, false);
            group.localPosition = new Vector3(x, FloorTop, 0f);
            for (int i = group.GetSiblingIndex() - 1; i >= firstChild; i--)
                root.transform.GetChild(i).SetParent(group, true);
            group.gameObject.AddComponent<PodiumPop>().Delay = RevealStart + (count - 1 - index) * RevealStep;
        }

        // There are at most 4 players; anything else falls back to the bare number.
        private static string Ordinal(int place) =>
            place >= 1 && place <= 4 ? L10n.Get(L10n.UI, "podium.place_" + place) : place.ToString();

        // The title font only has ASCII: a text it cannot draw whole goes in the default font.
        private TextMeshPro Label(string value, Vector3 position, float fontSize, float width, TMP_FontAsset font)
        {
            var text = new GameObject("Label").AddComponent<TextMeshPro>();
            text.transform.SetParent(root.transform, false);
            text.transform.localPosition = position;
            text.text = value;
            text.font = font != null && font.HasCharacters(value) ? font : TMP_Settings.defaultFontAsset;
            if (text.font != font) text.fontStyle = FontStyles.Bold;
            text.outlineColor = Outline;
            text.outlineWidth = .2f;
            text.fontSize = fontSize;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.enableAutoSizing = true;
            text.fontSizeMin = 1.5f;
            text.fontSizeMax = fontSize;
            text.rectTransform.sizeDelta = new Vector2(width, .55f);
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
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
