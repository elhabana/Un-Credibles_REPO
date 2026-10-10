using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnCredibles.Core;
using UnCredibles.Minigames;
using UnCredibles.Players;
using UnityEngine;
using UnityEngine.UI;

namespace UnCredibles.UI.Voting
{
    // A screen-space route map over the garage. The Core owns the ballot and its result.
    public sealed class MapVoteView : MonoBehaviour
    {
        private CoreRoot core;
        private GameObject root;
        private TMP_Text heading;
        private TMP_Text timer;
        private TMP_Text hint;
        private readonly TMP_Text[] stops = new TMP_Text[3];
        private readonly List<Card> cards = new List<Card>();
        private int renderedCandidateCount = -1;

        private sealed class Card
        {
            public Image background;
            public TMP_Text title;
            public TMP_Text footer;
            public Image thumbnail;
            public Button button;
        }

        private IEnumerator Start()
        {
            while (CoreRoot.Instance == null) yield return null;
            core = CoreRoot.Instance;
            Build();
            core.GameFlow.StateChanged += OnStateChanged;
            core.VoteChanged += Render;
            OnStateChanged(core.GameFlow.State, core.GameFlow.State);
        }

        private void OnDestroy()
        {
            if (core != null)
            {
                core.GameFlow.StateChanged -= OnStateChanged;
                core.VoteChanged -= Render;
            }
        }

        private void Update()
        {
            if (root != null && root.activeSelf && core != null)
                timer.text = $"{Mathf.CeilToInt(core.VoteSecondsLeft)} s";
        }

        private void OnStateChanged(GameState previous, GameState current)
        {
            if (root == null) return;
            root.SetActive(current == GameState.Voting);
            if (current == GameState.Voting) Render();
        }

        private void Build()
        {
            root = new GameObject("Mapa de votacion", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(transform, false);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = .5f;

            var background = Panel("Fondo", root.transform, new Color(.035f, .065f, .10f, .98f));
            Stretch(background.rectTransform, Vector2.zero, Vector2.zero);

            heading = Label("Titulo", root.transform, "EL MAPA DE LA PARTIDA", 68, TextAlignmentOptions.Center);
            Place(heading.rectTransform, 0, 407, 1700, 90);
            heading.fontStyle = FontStyles.Bold;
            heading.color = new Color(1f, .79f, .35f);
            timer = Label("Tiempo", root.transform, "12 s", 46, TextAlignmentOptions.Center);
            Place(timer.rectTransform, 0, 323, 380, 65);

            var route = Panel("Ruta", root.transform, new Color(.09f, .15f, .20f));
            Place(route.rectTransform, 0, 195, 1580, 130);
            var road = Panel("Camino", route.transform, new Color(.36f, .49f, .50f));
            Place(road.rectTransform, 0, 0, 1020, 4);
            for (int i = 0; i < 3; i++)
            {
                stops[i] = Label("Parada " + (i + 1), route.transform, "?", 29, TextAlignmentOptions.Center);
                Place(stops[i].rectTransform, (i - 1) * 500, 0, 480, 100);
            }
            hint = Label("Instrucciones", root.transform, "IZQUIERDA / DERECHA: ELEGIR     A / ESPACIO: VOTAR", 27,
                TextAlignmentOptions.Center);
            Place(hint.rectTransform, 0, -436, 1700, 70);
            hint.color = new Color(.76f, .84f, .9f);
            root.SetActive(false);
        }

        private void Render()
        {
            if (root == null || core == null || core.GameFlow.State != GameState.Voting) return;
            var candidates = core.VoteCandidates;
            if (renderedCandidateCount != candidates.Count)
            {
                foreach (var card in cards) Destroy(card.background.gameObject);
                cards.Clear();
                renderedCandidateCount = candidates.Count;
                for (int i = 0; i < candidates.Count; i++) cards.Add(CreateCard(i, candidates[i], candidates.Count));
            }

            int round = core.VotedMinigames.Count;
            heading.text = round >= 3 ? "RUTA DECIDIDA" : $"VOTAD EL MINIJUEGO {round + 1} DE 3";
            for (int i = 0; i < stops.Length; i++)
            {
                string name = i < round ? core.VotedMinigames[i].DisplayName : i == round ? "VOTANDO..." : "POR DECIDIR";
                stops[i].text = $"{i + 1:00}   {name}";
                stops[i].color = i < round ? new Color(.45f, .95f, .69f) : i == round ?
                    new Color(1f, .79f, .35f) : new Color(.58f, .67f, .73f);
            }

            for (int i = 0; i < cards.Count; i++)
            {
                var card = cards[i];
                bool picked = Contains(core.VotedMinigames, candidates[i]);
                int votes = 0;
                var selectors = new List<string>();
                foreach (var slot in core.Players.Slots)
                {
                    if (!slot.IsOccupied || slot.IsAI) continue;
                    if (core.Votes.TryGetValue(slot.PlayerId, out int choice) && choice == i) votes++;
                    else if (!core.Votes.ContainsKey(slot.PlayerId) &&
                             (core.VoteCursor.TryGetValue(slot.PlayerId, out int cursor) ? cursor == i : i == FirstAvailable()))
                        selectors.Add(slot.PlayerName);
                }
                card.background.color = picked ? new Color(.10f, .15f, .17f) : selectors.Count > 0 ?
                    new Color(.24f, .39f, .43f) : new Color(.13f, .22f, .28f);
                card.title.color = picked ? new Color(.52f, .59f, .61f) : Color.white;
                card.footer.text = picked ? "EN LA RUTA" :
                    selectors.Count > 0 ? string.Join(", ", selectors) + (votes > 0 ? $"  ·  {votes} votos" : "") :
                    votes > 0 ? $"{votes} votos" : "DISPONIBLE";
                card.button.interactable = !picked &&
                    (core.GameFlow.Session == SessionMode.Local || core.Online.IsHost);
            }
            hint.text = core.GameFlow.Session == SessionMode.Online && !core.Online.IsHost
                ? "IZQUIERDA / DERECHA: ELEGIR     A / ESPACIO: VOTAR     Esperando al anfitrion"
                : "IZQUIERDA / DERECHA: ELEGIR     A / ESPACIO: VOTAR";
        }

        private int FirstAvailable()
        {
            for (int i = 0; i < core.VoteCandidates.Count; i++)
                if (!Contains(core.VotedMinigames, core.VoteCandidates[i])) return i;
            return 0;
        }

        private Card CreateCard(int index, MinigameData data, int count)
        {
            var card = new Card();
            card.background = Panel("Destino " + data.DisplayName, root.transform, new Color(.13f, .22f, .28f));
            float width = Mathf.Min(365f, 1540f / Mathf.Max(1, count) - 14f);
            Place(card.background.rectTransform, (index - (count - 1) * .5f) * (width + 22f), -119, width, 395);
            card.button = card.background.gameObject.AddComponent<Button>();
            int selectedIndex = index;
            card.button.onClick.AddListener(() =>
            {
                foreach (var slot in core.Players.Slots)
                    if (slot.IsOccupied && !slot.IsAI && !core.Votes.ContainsKey(slot.PlayerId) &&
                        !(slot.Input is UnCredibles.Networking.NetworkInput))
                    { core.CastVote(slot.PlayerId, selectedIndex); break; }
            });
            card.thumbnail = Panel("Miniatura", card.background.transform, new Color(.18f, .28f, .32f));
            Place(card.thumbnail.rectTransform, 0, 73, width - 28, 208);
            if (data.Thumbnail != null) { card.thumbnail.sprite = data.Thumbnail; card.thumbnail.color = Color.white; }
            card.title = Label("Nombre", card.background.transform, data.DisplayName, 31, TextAlignmentOptions.Center);
            Place(card.title.rectTransform, 0, -85, width - 24, 73);
            card.title.fontStyle = FontStyles.Bold;
            card.footer = Label("Votos", card.background.transform, "DISPONIBLE", 19, TextAlignmentOptions.Center);
            Place(card.footer.rectTransform, 0, -158, width - 24, 40);
            card.footer.color = new Color(1f, .79f, .35f);
            return card;
        }

        private static Image Panel(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private static TMP_Text Label(string name, Transform parent, string content, float size, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<TextMeshProUGUI>();
            text.font = TMP_Settings.defaultFontAsset;
            text.text = content;
            text.fontSize = size;
            text.alignment = align;
            text.color = Color.white;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.raycastTarget = false;
            return text;
        }

        private static void Place(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
        }

        private static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = min;
            rect.offsetMax = max;
        }

        private static bool Contains(IReadOnlyList<MinigameData> list, MinigameData data)
        {
            foreach (var item in list) if (item == data) return true;
            return false;
        }
    }
}
