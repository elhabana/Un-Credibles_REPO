#if UNITY_EDITOR
using TMPro;
using UnCredibles.Core;
using UnCredibles.Minigames;
using UnCredibles.Players;
using UnCredibles.UI.Garage;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace UnCredibles.UI.Voting.Editor
{
    // Temporary: bakes the vote screen MapVoteView used to build at runtime into PF_MapVote and PF_VoteCard
    // (same sizes, colours and fonts) and places PF_MapVote in SCN_MainMenu. Safe to run again: the prefabs are
    // rebuilt in place (same GUIDs) and an existing scene instance is kept. Delete this file once it has run.
    public static class VotingUiBake
    {
        private const string Folder = "Assets/_Project/UI/Voting/Prefabs";
        private const string CardPath = Folder + "/PF_VoteCard.prefab";
        private const string ScreenPath = Folder + "/PF_MapVote.prefab";
        private const string ScenePath = "Assets/_Project/Core/Scenes/SCN_MainMenu.unity";
        private const string SpriteFolder = "Assets/_Project/UI/Common/Resources/PaperUI/";
        private static readonly Color Backdrop = new Color(.114f, .114f, .106f, .85f); // see-through ink
        private static readonly Vector2 Middle = new Vector2(.5f, .5f);
        private static readonly Vector2 Top = new Vector2(.5f, 1f);
        private const float StopWidth = 420f;
        private const float StopStep = 480f;
        private const float MaxCardWidth = 365f; // the runtime width with few candidates

        [MenuItem("UnCredibles/UI Bake/Voting")]
        public static void Bake()
        {
            // PaperUI loads the shapes through Resources; they must be these assets or the prefabs lose them.
            if (!IsAsset(PaperUI.PanelSprite, "T_UI_Panel") || !IsAsset(PaperUI.PillSprite, "T_UI_Pill") ||
                !IsAsset(PaperUI.DiscSprite, "T_UI_Disc")) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var front = Object.FindFirstObjectByType<GarageFrontend>(FindObjectsInactive.Include);
            if (front == null || front.titleFont == null)
            {
                Debug.LogError("[Vote bake] " + ScenePath + " needs the GarageFrontend with its title font (Bangers SDF).");
                return;
            }
            var font = front.titleFont;
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/_Project/UI/Voting", "Prefabs");

            var card = BuildCard(font);
            var cardAsset = PrefabUtility.SaveAsPrefabAsset(card.gameObject, CardPath).GetComponent<VoteCard>();
            Object.DestroyImmediate(card.gameObject);

            var existing = Object.FindFirstObjectByType<MapVoteView>(FindObjectsInactive.Include);
            if (existing != null && existing.transform.parent != null)
            {
                Undo.DestroyObjectImmediate(existing.gameObject);
                existing = null;
            }
            var screen = BuildScreen(font, cardAsset);
            var screenAsset = PrefabUtility.SaveAsPrefabAsset(screen.gameObject, ScreenPath);
            Object.DestroyImmediate(screen.gameObject);

            if (existing == null)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(screenAsset, scene);
                Undo.RegisterCreatedObjectUndo(instance, "Bake vote screen");
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[Vote bake] " + ScreenPath + " and " + CardPath + " baked; PF_MapVote is in " + ScenePath + ".");
        }

        private static bool IsAsset(Sprite used, string name)
        {
            var asset = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteFolder + name + ".png");
            if (asset != null && asset == used) return true;
            Debug.LogError("[Vote bake] Missing sprite " + SpriteFolder + name + ".png (PaperUI must load that asset).");
            return false;
        }

        private static MapVoteView BuildScreen(TMP_FontAsset font, VoteCard cardPrefab)
        {
            var canvas = PaperUI.Canvas("PF_MapVote", null, 200);
            var view = canvas.gameObject.AddComponent<MapVoteView>();
            canvas.gameObject.AddComponent<GraphicRaycaster>(); // the cards take mouse clicks
            var root = (RectTransform)new GameObject("Mapa de votacion", typeof(RectTransform)).transform;
            root.SetParent(canvas.transform, false);
            PaperUI.Stretch(root);
            view.root = root.gameObject;

            var backdrop = Plain("Fondo", root, Backdrop);
            PaperUI.Stretch(backdrop.rectTransform);

            var label = PaperUI.Panel("Titulo", root, PaperUI.Paper, -1.5f, true);
            Place(label.rectTransform, 0f, 440f, 1150f, 110f);
            view.heading = Label("Texto", label.transform, font, 76f, 40f, PaperUI.Ink);
            PaperUI.Stretch(view.heading.rectTransform, 14f);

            view.timerPill = PaperUI.Pill("Tiempo", root, PaperUI.TimerTeal, 72f);
            Place(view.timerPill.rectTransform, 0f, 330f, 210f, 72f);
            view.timer = Label("Texto", view.timerPill.transform, font, 56f, 30f, PaperUI.Paper);
            PaperUI.Stretch(view.timer.rectTransform, 6f);

            // The road joins the 3 stops; drawn first so the cards sit on it.
            var road = Plain("Camino", root, PaperUI.Ink);
            Place(road.rectTransform, 0f, 222f, StopStep * 2f, 8f);
            view.stops = new MapVoteView.Stop[MatchVote.VotedRounds];
            for (int i = 0; i < view.stops.Length; i++) view.stops[i] = BuildStop(i, root, font);

            var footer = PaperUI.Pill("Instrucciones", root, PaperUI.Paper, 60f);
            Place(footer.rectTransform, 0f, -478f, 1560f, 60f);
            view.hint = Label("Texto", footer.transform, font, 30f, 16f, PaperUI.Ink);
            view.hint.rectTransform.anchorMin = Vector2.zero;
            view.hint.rectTransform.anchorMax = Vector2.one;
            view.hint.rectTransform.offsetMin = new Vector2(30f, 6f);
            view.hint.rectTransform.offsetMax = new Vector2(-30f, -6f);

            // Last, so the polaroids draw over everything else as they did when appended at runtime.
            var cards = new GameObject("Cartas", typeof(RectTransform));
            cards.transform.SetParent(root, false);
            view.cardContainer = (RectTransform)cards.transform;
            PaperUI.Stretch(view.cardContainer);
            view.cardPrefab = cardPrefab;

            view.root.SetActive(false);
            return view;
        }

        private static MapVoteView.Stop BuildStop(int index, Transform root, TMP_FontAsset font)
        {
            var stop = new MapVoteView.Stop();
            stop.panel = PaperUI.Panel("Parada " + (index + 1), root, PaperUI.Paper, index % 2 == 0 ? -1.5f : 1.5f);
            Place(stop.panel.rectTransform, (index - 1) * StopStep, 222f, StopWidth, 90f);
            stop.disc = PaperUI.Disc("Numero", stop.panel.transform, PaperUI.Ink);
            PaperUI.Place(stop.disc.rectTransform, new Vector2(0f, .5f), new Vector2(14f, 0f), new Vector2(62f, 62f));
            stop.number = Label("Texto", stop.disc.transform, font, 38f, 20f, PaperUI.Paper);
            PaperUI.Stretch(stop.number.rectTransform);
            stop.number.text = (index + 1).ToString();
            stop.label = Label("Nombre", stop.panel.transform, font, 34f, 18f, PaperUI.Ink);
            PaperUI.Stretch(stop.label.rectTransform, 10f);
            stop.label.rectTransform.offsetMin = new Vector2(88f, 10f);
            return stop;
        }

        // Polaroid at the widest size; MapVoteView re-lays it out for the number of candidates.
        private static VoteCard BuildCard(TMP_FontAsset font)
        {
            var background = PaperUI.Panel("PF_VoteCard", null, Color.white, 0f, true);
            background.raycastTarget = true;
            PaperUI.Place(background.rectTransform, Middle, Vector2.zero, Vector2.zero);
            var card = background.gameObject.AddComponent<VoteCard>();
            card.background = background;
            card.button = background.gameObject.AddComponent<Button>();

            card.thumbnail = Plain("Miniatura", background.transform, Color.white);
            card.thumbnail.preserveAspect = true;
            HangFromTop(card.thumbnail.rectTransform);
            card.title = Label("Nombre", background.transform, font, 46f, 24f, PaperUI.Ink);
            HangFromTop(card.title.rectTransform);
            card.footer = Label("Votos", background.transform, font, 28f, 16f, PaperUI.TimerTeal);
            HangFromTop(card.footer.rectTransform);

            card.badges = new VoteCard.Badge[PlayerRegistry.MaxPlayers];
            for (int slot = 0; slot < card.badges.Length; slot++)
                card.badges[slot] = BuildBadge(background.transform, slot, font);
            BuildInRoute(card, font);
            card.Layout(MaxCardWidth);
            return card;
        }

        // "EN LA RUTA": an ink line crossing the photo out and a red rubber stamp over it.
        private static void BuildInRoute(VoteCard card, TMP_FontAsset font)
        {
            var group = new GameObject("En la ruta", typeof(RectTransform));
            group.transform.SetParent(card.background.transform, false);
            PaperUI.Stretch((RectTransform)group.transform);
            var strike = Plain("Tachon", group.transform, PaperUI.Ink);
            Place(strike.rectTransform, 0f, 0f, 0f, 10f); // length and angle: VoteCard.Layout
            var stamp = PaperUI.Pill("Sello", group.transform, PaperUI.Alarm, 72f);
            Place(stamp.rectTransform, 0f, 30f, 0f, 72f); // width: VoteCard.Layout
            stamp.rectTransform.localEulerAngles = new Vector3(0f, 0f, -16f);
            card.routeLabel = Label("Texto", stamp.transform, font, 48f, 24f, PaperUI.Paper);
            PaperUI.Stretch(card.routeLabel.rectTransform, 8f);
            group.SetActive(false);
            card.inRoute = group;
            card.strike = strike.rectTransform;
            card.routeStamp = stamp.rectTransform;
        }

        // Player disc with the tag ("J1"); a confirmed vote also gets a small red stamp.
        private static VoteCard.Badge BuildBadge(Transform parent, int slotIndex, TMP_FontAsset font)
        {
            var badge = new VoteCard.Badge();
            badge.disc = PaperUI.Disc("Ficha P" + (slotIndex + 1), parent, PlayerIdentity.ColorFor(slotIndex));
            HangFromTop(badge.disc.rectTransform);
            badge.label = Label("Texto", badge.disc.transform, font, 30f, 16f, PaperUI.Ink);
            PaperUI.Stretch(badge.label.rectTransform, 4f);
            var stamp = PaperUI.Pill("Sello", badge.disc.transform, PaperUI.Alarm, 26f);
            badge.stamp = stamp.rectTransform;
            Place(badge.stamp, 0f, -30f, 96f, 26f);
            badge.stamp.localEulerAngles = new Vector3(0f, 0f, -12f);
            badge.stampLabel = Label("Texto", badge.stamp, font, 20f, 10f, PaperUI.Paper);
            PaperUI.Stretch(badge.stampLabel.rectTransform, 2f);
            badge.stamp.gameObject.SetActive(false);
            badge.disc.gameObject.SetActive(false);
            return badge;
        }

        private static Image Plain(string name, Transform parent, Color color)
        {
            var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.transform.SetParent(parent, false);
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        // Centred, auto-sized label of the paper kit.
        private static TMP_Text Label(string name, Transform parent, TMP_FontAsset font, float size, float min, Color color)
        {
            var label = PaperUI.Text(name, parent, font, size, color, TextAlignmentOptions.Center);
            label.enableAutoSizing = true;
            label.fontSizeMin = min;
            label.fontSizeMax = size;
            return label;
        }

        private static void Place(RectTransform rect, float x, float y, float width, float height) =>
            PaperUI.Place(rect, Middle, new Vector2(x, y), new Vector2(width, height));

        // Rows of the polaroid hang from its top edge, centred on their point (VoteCard.Layout sets where).
        private static void HangFromTop(RectTransform rect)
        {
            PaperUI.Place(rect, Top, Vector2.zero, Vector2.zero);
            rect.pivot = Middle;
        }
    }
}
#endif
