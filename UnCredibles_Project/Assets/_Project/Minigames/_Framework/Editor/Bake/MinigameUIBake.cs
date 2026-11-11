#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnCredibles.Minigames.Churro;
using UnCredibles.Minigames.GarageMice;
using UnCredibles.Players;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

namespace UnCredibles.Minigames.Editor
{
    // Temporary authoring command: turns the minigame UI that used to be built in code (PaperUI) into prefabs,
    // puts the paper HUD in every minigame scene and wires the references. Running it again replaces what it made.
    // The measures, colours and fonts are the ones the runtime builders used.
    public static class MinigameUIBake
    {
        private const string PrefabFolder = "Assets/_Project/Minigames/_Framework/Prefabs/UI";
        private const string MiceFolder = "Assets/_Project/Minigames/MG_GarageMice/Prefabs/UI";
        private const string SceneFolder = "Assets/_Project/Minigames";
        private const string SpriteFolder = "Assets/_Project/UI/Common/Resources/PaperUI/";
        private const string TitleFontGuid = "0f4999180db1cd14f8de3280d6998060"; // Bangers, MinigameUI.titleFont in every scene
        private const string HudName = "Paper HUD";
        private const string RoundPillName = "Round pill";
        private const int ShapeSize = 64;

        // PaperUI look.
        private static readonly Color ShadowColor = new Color(.114f, .114f, .106f, .9f);
        private static readonly Color TapeColor = new Color(1f, .97f, .85f, .55f);
        private static readonly Vector2 ShadowOffset = new Vector2(6f, -6f);

        private static readonly Vector2 Middle = new Vector2(.5f, .5f);
        private static readonly Vector2 TopLeft = new Vector2(0f, 1f);
        private static readonly Vector2 TopRight = new Vector2(1f, 1f);
        private static readonly Vector2 BottomLeft = new Vector2(0f, 0f);
        private static readonly Vector2 BottomRight = new Vector2(1f, 0f);

        // Player badge.
        private static readonly Vector2 BadgeSize = new Vector2(300f, 110f);
        private const float BadgeDisc = 88f;
        private const float BadgeIcon = 46f;

        // Timer pill.
        private const float PillHeight = 84f;

        // Rules card.
        private static readonly Color Backdrop = new Color(.114f, .114f, .106f, .85f); // see-through ink
        private static readonly Color DotColor = new Color(.169f, .647f, .604f, .3f);
        private static readonly Vector2 SheetSize = new Vector2(1560f, 880f);
        private const float SheetTilt = -1.5f;
        private const float Margin = 50f;
        private const float LeftWidth = 860f;
        private const float RightWidth = 600f;
        private const float MediaWidth = 560f;
        private const float MediaHeight = 315f;
        private const int MaxControls = 4;
        private const float KeyHeight = 46f;
        private const float RowStep = 54f;
        private const float KeyInset = 28f;
        private const float DiscSize = 90f;
        private const float DiscSpacing = 145f;

        private static Sprite panelSprite;
        private static Sprite pillSprite;
        private static Sprite discSprite;
        private static TMP_FontAsset titleFont;

        [MenuItem("UnCredibles/UI Bake/Minigames")]
        public static void Bake()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            panelSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteFolder + "T_UI_Panel.png");
            pillSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteFolder + "T_UI_Pill.png");
            discSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteFolder + "T_UI_Disc.png");
            titleFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(TitleFontGuid));
            if (panelSprite == null || pillSprite == null || discSprite == null || titleFont == null)
            {
                Debug.LogError("[UI Bake] Missing the PaperUI sprites in " + SpriteFolder + " or the title font (" + TitleFontGuid + ").");
                return;
            }

            EnsureFolder(PrefabFolder);
            EnsureFolder(MiceFolder);
            const string badgePath = PrefabFolder + "/PF_PlayerBadge.prefab";
            const string hudPath = PrefabFolder + "/PF_MinigameHud.prefab";
            const string cardPath = PrefabFolder + "/PF_MinigameRulesCard.prefab";
            const string micePath = MiceFolder + "/PF_GarageMiceHud.prefab";
            Save(BuildBadge(), badgePath);
            Save(BuildHud(), hudPath);
            Save(BuildRulesCard(), cardPath);
            Save(BuildMiceHud(), micePath);

            var setup = EditorSceneManager.GetSceneManagerSetup();
            foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { SceneFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                var badge = AssetDatabase.LoadAssetAtPath<GameObject>(badgePath).GetComponent<PlayerBadge>();
                var hud = AssetDatabase.LoadAssetAtPath<GameObject>(hudPath);
                var card = AssetDatabase.LoadAssetAtPath<GameObject>(cardPath).GetComponent<MinigameRulesCard>();
                var miceHud = AssetDatabase.LoadAssetAtPath<GameObject>(micePath).GetComponent<GarageMiceHud>();
                if (!WireScene(scene, badge, hud, card, miceHud)) continue;
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene))
                    throw new IOException($"[UI Bake] Could not save {path}.");
                Debug.Log("[UI Bake] Wired " + path);
            }
            if (setup.Length > 0 && !string.IsNullOrEmpty(setup[0].path)) EditorSceneManager.RestoreSceneManagerSetup(setup);
            AssetDatabase.SaveAssets();
            Debug.Log("[UI Bake] Minigame UI prefabs saved in " + PrefabFolder + " and " + MiceFolder + ".");
        }

        private static bool WireScene(Scene scene, PlayerBadge badge, GameObject hud, MinigameRulesCard card, GarageMiceHud miceHud)
        {
            bool changed = false;
            foreach (var ui in Find<MinigameUI>())
            {
                // Overlay canvases need to be at the root of their scene.
                var instance = SceneCanvas(scene, hud, HudName);
                instance.name = HudName;
                var pill = instance.transform.Find("Timer");
                Assign(ui, "hud", instance.transform);
                Assign(ui, "timerPill", pill.GetComponent<Image>());
                Assign(ui, "timerLabel", pill.Find("Seconds").GetComponent<TMP_Text>());
                Assign(ui, "badgePrefab", badge);
                changed = true;
            }
            var controllers = Find<MinigameController>();
            if (controllers.Length > 0)
            {
                var instance = SceneCanvas(scene, card.gameObject, "Rules card");
                instance.SetActive(false);
                foreach (var controller in controllers)
                    Assign(controller, "rulesCard", instance.GetComponent<MinigameRulesCard>());
                changed = true;
            }
            foreach (var controller in Find<GarageMiceController>())
            {
                var instance = SceneCanvas(scene, miceHud.gameObject, "Garage mice HUD");
                instance.SetActive(false);
                Assign(controller, "hud", instance.GetComponent<GarageMiceHud>());
                changed = true;
            }
            foreach (var banner in Find<ChurroRoundBanner>())
                changed |= WrapRoundText(banner);
            return changed;
        }

        private static GameObject SceneCanvas(Scene scene, GameObject prefab, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
                if (PrefabUtility.GetCorrespondingObjectFromSource(root) == prefab) return root;
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.name = name;
            return instance;
        }

        // Churro: the round counter goes in a paper pill, top centre, where other minigames show the time.
        private static bool WrapRoundText(ChurroRoundBanner banner)
        {
            var so = new SerializedObject(banner);
            var text = so.FindProperty("roundText").objectReferenceValue as TMP_Text;
            if (text == null) return false;
            var holder = text.transform.parent;
            if (holder.name == RoundPillName)
            {
                // Re-run: take the text out of the old pill before replacing it.
                text.transform.SetParent(holder.parent, false);
                Object.DestroyImmediate(holder.gameObject);
                holder = text.transform.parent;
            }
            var pill = NewPill(RoundPillName, holder, PaperUI.Paper, 76f);
            Place(pill.rectTransform, new Vector2(.5f, 1f), new Vector2(0f, -24f), new Vector2(340f, 76f));
            text.transform.SetParent(pill.transform, false);
            Stretch(text.rectTransform);
            text.color = PaperUI.Ink;
            text.fontSize = 46f;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.text = string.Empty;
            EditorUtility.SetDirty(text);
            pill.gameObject.SetActive(false);
            so.FindProperty("roundPill").objectReferenceValue = pill.gameObject;
            so.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        // Laid out for the top-left corner; PlayerBadge.Setup mirrors it for the others.
        private static GameObject BuildBadge()
        {
            var frame = NewPanel("PF_PlayerBadge", null, PaperUI.Paper, 1.5f, true);
            Place(frame.rectTransform, TopLeft, Vector2.zero, BadgeSize);
            var edge = new Vector2(0f, .5f);

            var disc = NewDisc("Disc", frame.transform, Color.white);
            Place(disc.rectTransform, edge, new Vector2(14f, 0f), new Vector2(BadgeDisc, BadgeDisc));
            var portrait = NewImage("Portrait", disc.transform);
            portrait.preserveAspect = true;
            Stretch(portrait.rectTransform, 6f);
            portrait.gameObject.SetActive(false);
            var tag = NewText("Tag", disc.transform, titleFont, 40f, PaperUI.Ink, TextAlignmentOptions.Center);
            Stretch(tag.rectTransform);

            float start = 14f + BadgeDisc + 8f;
            var icon = NewImage("Icon", frame.transform);
            icon.preserveAspect = true;
            Place(icon.rectTransform, edge, new Vector2(start, 0f), new Vector2(BadgeIcon, BadgeIcon));
            icon.gameObject.SetActive(false);
            // Stretched from the icon to the inner edge, so without an icon it can grow over its place.
            var score = NewText("Score", frame.transform, titleFont, 72f, PaperUI.Ink, TextAlignmentOptions.Center);
            Stretch(score.rectTransform);
            score.rectTransform.offsetMin = new Vector2(start + BadgeIcon + 6f, 0f);
            score.rectTransform.offsetMax = new Vector2(-14f, 0f);
            score.text = "0";

            // Over the bottom edge, towards the middle of the screen.
            var popup = NewPill("Popup", frame.transform, PaperUI.Paper, 60f);
            Place(popup.rectTransform, new Vector2(.5f, 0f), new Vector2(40f, 0f), new Vector2(120f, 60f));
            popup.rectTransform.pivot = Middle;
            var popupGroup = popup.gameObject.AddComponent<CanvasGroup>();
            var amount = NewText("Amount", popup.transform, titleFont, 44f, PaperUI.Ink, TextAlignmentOptions.Center);
            Stretch(amount.rectTransform);
            popup.gameObject.SetActive(false);

            var badge = frame.gameObject.AddComponent<PlayerBadge>();
            Assign(badge, "tape", frame.transform.Find("Tape").gameObject);
            Assign(badge, "disc", disc);
            Assign(badge, "portrait", portrait);
            Assign(badge, "tagText", tag);
            Assign(badge, "icon", icon);
            Assign(badge, "score", score);
            Assign(badge, "popup", popup.rectTransform);
            Assign(badge, "popupGroup", popupGroup);
            Assign(badge, "popupText", amount);
            return frame.gameObject;
        }

        // Timer pill, top centre; the badges are added next to it at runtime.
        private static GameObject BuildHud()
        {
            var canvas = NewCanvas("PF_MinigameHud", 10); // under the rules card (50)
            var pill = NewPill("Timer", canvas.transform, PaperUI.TimerTeal, PillHeight);
            Place(pill.rectTransform, new Vector2(.5f, 1f), new Vector2(0f, -24f), new Vector2(180f, PillHeight));
            pill.rectTransform.pivot = Middle; // beats around its centre
            pill.rectTransform.anchoredPosition = new Vector2(0f, -24f - PillHeight * .5f);
            var label = NewText("Seconds", pill.transform, titleFont, 62f, PaperUI.Paper, TextAlignmentOptions.Center);
            Stretch(label.rectTransform);
            pill.gameObject.SetActive(false);
            return canvas.gameObject;
        }

        private static GameObject BuildRulesCard()
        {
            var canvas = NewCanvas("PF_MinigameRulesCard", 50); // above the minigame HUD
            var group = canvas.gameObject.AddComponent<CanvasGroup>();
            var backdrop = NewImage("Backdrop", canvas.transform);
            backdrop.color = Backdrop;
            Stretch(backdrop.rectTransform);

            var sheet = NewPanel("Sheet", canvas.transform, PaperUI.Paper, SheetTilt, true).rectTransform;
            Center(sheet, Middle, Vector2.zero, SheetSize);
            // The kit's tape is sized for small badges; a sheet this big needs a wider strip.
            ((RectTransform)sheet.Find("Tape")).sizeDelta = new Vector2(170f, 50f);

            // Left column: title, mission, controls.
            var titleBox = NewPanel("Title", sheet, Color.white, 1.5f);
            Place(titleBox.rectTransform, TopLeft, new Vector2(Margin, -40f), new Vector2(LeftWidth, 130f));
            var title = NewText("Name", titleBox.transform, titleFont, 84f, PaperUI.Ink, TextAlignmentOptions.Center);
            AutoSize(title, 40f, 84f);
            Stretch(title.rectTransform, 16f);

            var mission = NewPanel("Mission", sheet, Color.white);
            Place(mission.rectTransform, TopLeft, new Vector2(Margin, -215f), new Vector2(LeftWidth, 320f));
            var missionTab = NewTab(mission.transform);
            var rules = NewText("Rules", mission.transform, null, 32f, PaperUI.Ink, TextAlignmentOptions.TopLeft);
            rules.textWrappingMode = TextWrappingModes.Normal;
            AutoSize(rules, 20f, 32f);
            Stretch(rules.rectTransform, KeyInset);
            rules.rectTransform.offsetMax = new Vector2(-KeyInset, -40f); // below the tab

            var controls = NewPanel("Controls", sheet, Color.white);
            Place(controls.rectTransform, BottomLeft, new Vector2(Margin, 40f), new Vector2(LeftWidth, 270f));
            var controlsTab = NewTab(controls.transform);
            var keys = new Image[MaxControls];
            var keyLabels = new TMP_Text[MaxControls];
            var actions = new TMP_Text[MaxControls];
            for (int i = 0; i < MaxControls; i++)
            {
                float y = -42f - i * RowStep;
                // Round ink button; a longer key ("JOYSTICK") stretches it into a pill.
                keys[i] = NewPill("Key", controls.transform, PaperUI.Ink, KeyHeight);
                Place(keys[i].rectTransform, TopLeft, new Vector2(KeyInset, y), new Vector2(KeyHeight, KeyHeight));
                keyLabels[i] = NewText("Label", keys[i].transform, titleFont, 28f, PaperUI.Paper, TextAlignmentOptions.Center);
                Stretch(keyLabels[i].rectTransform);
                actions[i] = NewText("Action", controls.transform, null, 28f, PaperUI.Ink, TextAlignmentOptions.Left);
                AutoSize(actions[i], 18f, 28f);
                Place(actions[i].rectTransform, TopLeft, new Vector2(KeyInset, y), new Vector2(LeftWidth, KeyHeight));
            }

            // Media: gameplay video, thumbnail or the dotted placeholder with the name.
            var frame = NewPanel("Media", sheet, Color.white, 2f);
            Place(frame.rectTransform, TopRight, new Vector2(-Margin, -40f), new Vector2(RightWidth, 355f));
            var mediaSize = new Vector2(MediaWidth, MediaHeight);
            var thumbnail = NewImage("Thumbnail", frame.transform);
            thumbnail.preserveAspect = true;
            Center(thumbnail.rectTransform, Middle, Vector2.zero, mediaSize);
            var dots = NewImage("Placeholder", frame.transform);
            dots.sprite = discSprite;
            dots.type = Image.Type.Tiled;
            dots.pixelsPerUnitMultiplier = 3f;
            dots.color = DotColor;
            Center(dots.rectTransform, Middle, Vector2.zero, mediaSize);
            var placeholderName = NewText("Name", dots.transform, titleFont, 64f, PaperUI.Ink, TextAlignmentOptions.Center);
            placeholderName.textWrappingMode = TextWrappingModes.Normal;
            AutoSize(placeholderName, 32f, 64f);
            Stretch(placeholderName.rectTransform, 24f);

            var video = new GameObject("Gameplay", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            video.transform.SetParent(frame.transform, false);
            video.raycastTarget = false;
            Center(video.rectTransform, Middle, Vector2.zero, mediaSize); // fitted to the clip at runtime
            var videoPlayer = video.gameObject.AddComponent<VideoPlayer>();
            videoPlayer.playOnAwake = false;
            videoPlayer.isLooping = true;
            videoPlayer.audioOutputMode = VideoAudioOutputMode.None;
            videoPlayer.renderMode = VideoRenderMode.RenderTexture;
            video.gameObject.SetActive(false);

            // A disc per player slot; the card centres the used ones at runtime (here laid out for four).
            int slots = PlayerRegistry.MaxPlayers;
            var discs = new Image[slots];
            var tags = new TMP_Text[slots];
            var names = new TMP_Text[slots];
            var stamps = new RectTransform[slots];
            var stampLabels = new TMP_Text[slots];
            for (int i = 0; i < slots; i++)
            {
                discs[i] = NewDisc("Player " + (i + 1), sheet, Color.white);
                Center(discs[i].rectTransform, TopRight,
                    new Vector2(-Margin - RightWidth * .5f + (i - (slots - 1) * .5f) * DiscSpacing, -470f),
                    new Vector2(DiscSize, DiscSize));
                tags[i] = NewText("Tag", discs[i].transform, titleFont, 40f, PaperUI.Ink, TextAlignmentOptions.Center);
                Stretch(tags[i].rectTransform);
                names[i] = NewText("Name", discs[i].transform, null, 22f, PaperUI.Ink, TextAlignmentOptions.Center);
                names[i].overflowMode = TextOverflowModes.Ellipsis;
                Place(names[i].rectTransform, new Vector2(.5f, 0f), new Vector2(0f, -36f), new Vector2(DiscSpacing, 30f));

                // Rubber stamp slapped over the disc when the player is ready.
                stamps[i] = NewPill("Ready stamp", discs[i].transform, PaperUI.Alarm, 48f).rectTransform;
                Center(stamps[i], Middle, new Vector2(0f, -4f), new Vector2(140f, 48f));
                stamps[i].localEulerAngles = new Vector3(0f, 0f, -14f);
                stampLabels[i] = NewText("Label", stamps[i], titleFont, 32f, PaperUI.Paper, TextAlignmentOptions.Center);
                Stretch(stampLabels[i].rectTransform);
            }

            var startBox = NewPanel("Start", sheet, PaperUI.Ink, -2f).rectTransform;
            Center(startBox, BottomRight, new Vector2(-Margin - RightWidth * .5f, 40f + 115f), new Vector2(RightWidth, 230f));
            var startLabel = NewText("Start", startBox, titleFont, 110f, PaperUI.Paper, TextAlignmentOptions.Center);
            AutoSize(startLabel, 50f, 110f);
            Place(startLabel.rectTransform, new Vector2(.5f, 1f), new Vector2(0f, -20f), new Vector2(RightWidth - 40f, 130f));
            var pressLabel = NewText("Press", startBox, titleFont, 40f, PaperUI.TimerTeal, TextAlignmentOptions.Center);
            AutoSize(pressLabel, 24f, 40f);
            Place(pressLabel.rectTransform, new Vector2(.5f, 0f), new Vector2(0f, 24f), new Vector2(RightWidth - 40f, 56f));

            var card = canvas.gameObject.AddComponent<MinigameRulesCard>();
            Assign(card, "titleFont", titleFont);
            Assign(card, "group", group);
            Assign(card, "sheet", sheet);
            Assign(card, "title", title);
            Assign(card, "missionTab", missionTab);
            Assign(card, "rules", rules);
            Assign(card, "controlsBox", controls.gameObject);
            Assign(card, "controlsTab", controlsTab);
            AssignArray(card, "keys", keys);
            AssignArray(card, "keyLabels", keyLabels);
            AssignArray(card, "actions", actions);
            Assign(card, "thumbnail", thumbnail);
            Assign(card, "placeholder", dots.gameObject);
            Assign(card, "placeholderName", placeholderName);
            Assign(card, "video", video);
            Assign(card, "videoPlayer", videoPlayer);
            AssignArray(card, "discs", discs);
            AssignArray(card, "tags", tags);
            AssignArray(card, "names", names);
            AssignArray(card, "stamps", stamps);
            AssignArray(card, "stampLabels", stampLabels);
            Assign(card, "startBox", startBox);
            Assign(card, "startLabel", startLabel);
            Assign(card, "pressLabel", pressLabel);
            return canvas.gameObject;
        }

        // Garage mice extras: controls pill at the bottom, countdown / results in the middle.
        private static GameObject BuildMiceHud()
        {
            var canvas = NewCanvas("PF_GarageMiceHud", 0);
            var controls = NewPill("Controls", canvas.transform, PaperUI.Paper, 56f);
            Place(controls.rectTransform, new Vector2(.5f, 0f), new Vector2(0f, 26f), new Vector2(760f, 56f));
            var instructions = NewText("Instructions", controls.transform, titleFont, 24f, PaperUI.Ink, TextAlignmentOptions.Center);
            Stretch(instructions.rectTransform);
            var center = NewText("Countdown and results", canvas.transform, titleFont, 70f, Color.white, TextAlignmentOptions.Center);
            center.textWrappingMode = TextWrappingModes.Normal;
            Place(center.rectTransform, Middle, Vector2.zero, new Vector2(900f, 430f));
            center.gameObject.SetActive(false);

            var hud = canvas.gameObject.AddComponent<GarageMiceHud>();
            Assign(hud, "center", center);
            Assign(hud, "instructions", instructions);
            return canvas.gameObject;
        }

        // Ink tab on the top edge of a box, like the labels of the art bible.
        private static TMP_Text NewTab(Transform box)
        {
            var pill = NewPill("Tab", box, PaperUI.Ink, 44f);
            Place(pill.rectTransform, TopLeft, new Vector2(24f, 22f), new Vector2(200f, 44f));
            var label = NewText("Label", pill.transform, titleFont, 30f, PaperUI.Paper, TextAlignmentOptions.Center);
            Stretch(label.rectTransform);
            return label;
        }

        // The PaperUI builders, unchanged.
        private static Canvas NewCanvas(string name, int sortingOrder)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = .5f;
            return canvas;
        }

        private static Image NewPanel(string name, Transform parent, Color fill, float tilt = 0f, bool tape = false)
        {
            var image = NewShape(name, parent, panelSprite, fill, Image.Type.Sliced);
            image.rectTransform.localEulerAngles = new Vector3(0f, 0f, tilt);
            if (tape)
            {
                var strip = NewShape("Tape", image.transform, null, TapeColor, Image.Type.Simple);
                Place(strip.rectTransform, TopLeft, new Vector2(8f, -6f), new Vector2(76f, 24f));
                strip.rectTransform.pivot = Middle;
                strip.rectTransform.localEulerAngles = new Vector3(0f, 0f, 40f);
            }
            return image;
        }

        private static Image NewPill(string name, Transform parent, Color fill, float height)
        {
            var image = NewShape(name, parent, pillSprite, fill, Image.Type.Sliced);
            image.pixelsPerUnitMultiplier = ShapeSize / height;
            return image;
        }

        private static Image NewDisc(string name, Transform parent, Color fill) =>
            NewShape(name, parent, discSprite, fill, Image.Type.Simple);

        private static Image NewShape(string name, Transform parent, Sprite sprite, Color fill, Image.Type type)
        {
            var image = NewImage(name, parent);
            image.sprite = sprite;
            image.type = type;
            image.color = fill;
            if (sprite != null)
            {
                var shadow = image.gameObject.AddComponent<Shadow>();
                shadow.effectColor = ShadowColor;
                shadow.effectDistance = ShadowOffset;
            }
            return image;
        }

        private static Image NewImage(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            if (parent != null) go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.raycastTarget = false;
            return image;
        }

        private static TMP_Text NewText(string name, Transform parent, TMP_FontAsset font, float size, Color color,
            TextAlignmentOptions alignment)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var label = go.GetComponent<TextMeshProUGUI>();
            label.font = font != null ? font : TMP_Settings.defaultFontAsset;
            label.fontSize = size;
            label.color = color;
            label.alignment = alignment;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false;
            return label;
        }

        private static void AutoSize(TMP_Text label, float min, float max)
        {
            label.enableAutoSizing = true;
            label.fontSizeMin = min;
            label.fontSizeMax = max;
        }

        // Anchor and pivot on the same point, then offset from it.
        private static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        // Like Place but turning around its centre (tilt, pop and pulse).
        private static void Center(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            Place(rect, anchor, position, size);
            rect.pivot = Middle;
        }

        private static void Stretch(RectTransform rect, float inset = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        private static T[] Find<T>() where T : Object =>
            Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        private static void Assign(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var property = so.FindProperty(field);
            if (property == null || value == null)
                throw new System.InvalidOperationException($"[UI Bake] Cannot wire {target.GetType().Name}.{field}.");
            property.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssignArray(Object target, string field, Object[] values)
        {
            var so = new SerializedObject(target);
            var property = so.FindProperty(field);
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // SaveAsPrefabAsset overwrites an existing prefab in place, keeping its GUID.
        private static GameObject Save(GameObject root, string path)
        {
            var asset = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return asset;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
#endif
