using TMPro;
using UnCredibles.Localization;
using UnCredibles.Minigames;
using UnCredibles.Players;
using UnityEngine;
using UnityEngine.UI;

namespace UnCredibles.UI.Results
{
    // Standings between rounds as a taped paper sheet (art bible, PaperUI kit): the title, a "Round N / 3"
    // tab, one paper strip per player (place with a medal for the winner, slot disc, name, the round's
    // points as a stamp, big total) popping in one after another, and the hint in a paper pill.
    // Only shows what ResultsScreen hands it, so the online client sees the host's standings the same.
    public sealed class ResultsSheet : MonoBehaviour
    {
        private static readonly Color Gold = new Color(1f, .78f, .2f);
        private static readonly Color LeaderPaper = new Color(1f, .96f, .82f);
        private static readonly Vector2 Middle = new Vector2(.5f, .5f);
        private static readonly Vector2 Top = new Vector2(.5f, 1f);
        private static readonly Vector2 Left = new Vector2(0f, .5f);
        private static readonly Vector2 Right = new Vector2(1f, .5f);
        private static readonly Vector2 SheetSize = new Vector2(1300f, 860f);
        private static readonly Vector2 RowSize = new Vector2(1180f, 124f);
        private const float FirstRowY = -240f;
        private const float RowStep = 142f;
        private const float RowDelay = .12f;   // stagger between rows
        private const float PopSeconds = .3f;

        private TMP_FontAsset font;
        private GameObject root;
        private TMP_Text title;
        private TMP_Text round;
        private TMP_Text hint;
        private readonly Row[] rows = new Row[PlayerRegistry.MaxPlayers];
        private float animTime = -1f; // -1 = rows settled

        private sealed class Row
        {
            public Image panel;
            public Image medal;
            public TMP_Text place;
            public Image disc;
            public TMP_Text tag;
            public TMP_Text name;
            public GameObject stamp;
            public TMP_Text gained;
            public TMP_Text total;
        }

        public static ResultsSheet Create(Transform parent, TMP_FontAsset titleFont)
        {
            var canvas = PaperUI.Canvas("Results sheet", parent, 10); // over the scene canvas and its backdrop
            var sheet = canvas.gameObject.AddComponent<ResultsSheet>();
            sheet.font = titleFont;
            sheet.root = canvas.gameObject;
            sheet.Build();
            return sheet;
        }

        public void SetVisible(bool visible)
        {
            if (visible && !root.activeSelf)
            {
                // The rows pop in each time the sheet appears; until their turn they are not drawn.
                animTime = 0f;
                for (int i = 0; i < rows.Length; i++) rows[i].panel.rectTransform.localScale = Vector3.zero;
            }
            root.SetActive(visible);
        }

        public void SetHeader(string titleText, string roundText)
        {
            title.text = titleText;
            round.text = roundText;
        }

        public void SetHint(string text) => hint.text = text;

        public void SetRow(int index, int placement, string playerName, int slotIndex, bool isAI, int gained, int total)
        {
            var row = rows[index];
            bool leader = placement == 1;
            row.panel.color = leader ? LeaderPaper : Color.white;
            row.medal.gameObject.SetActive(leader);
            string key = "podium.place_" + placement;
            string place = L10n.Get(L10n.UI, key);
            row.place.text = place == key ? placement.ToString() : place;
            row.disc.color = PlayerIdentity.ColorFor(slotIndex, isAI);
            row.tag.text = PlayerIdentity.LabelFor(slotIndex, isAI);
            row.name.text = playerName;
            row.stamp.SetActive(gained > 0);
            row.gained.SetText("+{0}", gained);
            row.total.SetText("{0}", total);
            row.panel.gameObject.SetActive(true);
        }

        public void HideRow(int index) => rows[index].panel.gameObject.SetActive(false);

        private void Build()
        {
            var sheet = PaperUI.Panel("Sheet", root.transform, PaperUI.Paper, -1f, true);
            Center(sheet.rectTransform, Middle, new Vector2(0f, 30f), SheetSize);
            // The kit's tape is sized for small badges; a sheet this big needs a wider strip.
            if (sheet.transform.Find("Tape") is RectTransform tape) tape.sizeDelta = new Vector2(170f, 50f);

            title = Label("Title", sheet.transform, font, 110f, 50f, PaperUI.Ink, TextAlignmentOptions.Center);
            Center(title.rectTransform, Top, new Vector2(0f, -95f), new Vector2(1000f, 130f));

            var tab = PaperUI.Pill("Round", sheet.transform, PaperUI.Ink, 52f);
            Center(tab.rectTransform, Top, new Vector2(0f, -190f), new Vector2(320f, 52f));
            tab.rectTransform.localEulerAngles = new Vector3(0f, 0f, 1.5f);
            round = Label("Label", tab.transform, font, 36f, 18f, PaperUI.Paper, TextAlignmentOptions.Center);
            PaperUI.Stretch(round.rectTransform, 6f);

            for (int i = 0; i < rows.Length; i++) rows[i] = BuildRow(sheet.transform, i);

            var pill = PaperUI.Pill("Hint", root.transform, PaperUI.Paper, 64f);
            Center(pill.rectTransform, Middle, new Vector2(0f, -478f), new Vector2(1300f, 64f));
            hint = Label("Label", pill.transform, font, 34f, 16f, PaperUI.Ink, TextAlignmentOptions.Center);
            PaperUI.Stretch(hint.rectTransform, 8f);
            root.SetActive(false); // ResultsScreen shows it between rounds only
        }

        private Row BuildRow(Transform sheet, int index)
        {
            var row = new Row();
            row.panel = PaperUI.Panel("Row " + (index + 1), sheet, Color.white, index % 2 == 0 ? -.6f : .6f);
            Center(row.panel.rectTransform, Top, new Vector2(0f, FirstRowY - index * RowStep - RowSize.y * .5f), RowSize);
            var panel = row.panel.transform;

            row.medal = PaperUI.Disc("Medal", panel, Gold);
            Center(row.medal.rectTransform, Left, new Vector2(72f, 0f), new Vector2(108f, 108f));
            row.place = Label("Place", panel, font, 64f, 30f, PaperUI.Ink, TextAlignmentOptions.Center);
            Center(row.place.rectTransform, Left, new Vector2(72f, 0f), new Vector2(104f, 104f));

            row.disc = PaperUI.Disc("Disc", panel, Color.white);
            Center(row.disc.rectTransform, Left, new Vector2(196f, 0f), new Vector2(92f, 92f));
            row.tag = Label("Tag", row.disc.transform, font, 38f, 18f, PaperUI.Ink, TextAlignmentOptions.Center);
            PaperUI.Stretch(row.tag.rectTransform, 6f);

            // Typed player names can hold any character: the default font, not Bangers.
            row.name = Label("Name", panel, null, 44f, 24f, PaperUI.Ink, TextAlignmentOptions.Left);
            row.name.overflowMode = TextOverflowModes.Ellipsis;
            PaperUI.Place(row.name.rectTransform, Left, new Vector2(262f, 0f), new Vector2(480f, 90f));

            var stamp = PaperUI.Pill("Round points", panel, PaperUI.TimerTeal, 64f);
            Center(stamp.rectTransform, Left, new Vector2(840f, 0f), new Vector2(150f, 64f));
            stamp.rectTransform.localEulerAngles = new Vector3(0f, 0f, -8f);
            row.stamp = stamp.gameObject;
            row.gained = Label("Label", stamp.transform, font, 44f, 22f, PaperUI.Paper, TextAlignmentOptions.Center);
            PaperUI.Stretch(row.gained.rectTransform, 4f);

            row.total = Label("Total", panel, font, 96f, 40f, PaperUI.Ink, TextAlignmentOptions.Right);
            PaperUI.Place(row.total.rectTransform, Right, new Vector2(-30f, 0f), new Vector2(220f, 110f));
            row.panel.gameObject.SetActive(false);
            return row;
        }

        // Each row pops in a little after the one above: from nothing, past full size, and back (ease-out back).
        private void Update()
        {
            if (animTime < 0f) return;
            animTime += Time.unscaledDeltaTime;
            for (int i = 0; i < rows.Length; i++)
            {
                float t = Mathf.Clamp01((animTime - i * RowDelay) / PopSeconds);
                float k = t - 1f;
                float scale = t <= 0f ? 0f : 1f + 2.70158f * k * k * k + 1.70158f * k * k;
                rows[i].panel.rectTransform.localScale = new Vector3(scale, scale, 1f);
            }
            if (animTime >= (rows.Length - 1) * RowDelay + PopSeconds) animTime = -1f;
        }

        private static TMP_Text Label(string name, Transform parent, TMP_FontAsset font, float size, float min, Color color,
            TextAlignmentOptions alignment)
        {
            var label = PaperUI.Text(name, parent, font, size, color, alignment);
            label.enableAutoSizing = true;
            label.fontSizeMin = min;
            label.fontSizeMax = size;
            return label;
        }

        // Like PaperUI.Place but turning around its centre (tilt and pop).
        private static void Center(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            PaperUI.Place(rect, anchor, position, size);
            rect.pivot = Middle;
        }
    }
}
