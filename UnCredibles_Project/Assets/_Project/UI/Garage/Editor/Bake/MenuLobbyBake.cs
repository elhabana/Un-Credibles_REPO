#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnCredibles.Minigames;
using UnCredibles.Players;
using UnCredibles.UI.MainMenu;
using UnCredibles.UI.PartyLobby;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using UnityEngine.UI;

namespace UnCredibles.UI.Garage.Editor
{
    // Temporary, run once: saves into SCN_MainMenu (Main menu and Lobby under Garage UI, nothing else) and into
    // PF_MenuTab / PF_SignPill / PF_LobbySlot the look MainMenuController, PartyLobbyView and LobbySlotView used to
    // build at runtime, with the same sizes, colours and font. An already baked scene is left as it is.
    // If it throws halfway, reopen the scene without saving. Delete this folder once the baked scene is committed.
    public static class MenuLobbyBake
    {
        private const string ScenePath = "Assets/_Project/Core/Scenes/SCN_MainMenu.unity";
        private const string PaperFolder = "Assets/_Project/UI/Common/Resources/PaperUI/";
        private const string GaragePrefabs = "Assets/_Project/UI/Garage/Prefabs";
        private const string LobbyPrefabs = "Assets/_Project/UI/PartyLobby/Prefabs";
        private static readonly Color ShadowColor = new Color(.114f, .114f, .106f, .9f);
        private static readonly Vector2 ShadowOffset = new Vector2(6f, -6f);
        private static readonly Vector2 Middle = new Vector2(.5f, .5f);
        private const int PillShape = 64;            // texture height of T_UI_Pill
        private const float HomePanelWidth = 440f;   // to the bottom end of its diagonal edge
        private const float LevelButtonHeight = 48f;
        private const float LevelButtonStep = 55f;
        private const float LevelCancelSize = 40f;

        private static TMP_FontAsset font;
        private static Material shadowMaterial;
        private static Sprite panelSprite, pillSprite, discSprite, dashedSprite;
        private static Texture2D dotsTexture;
        private static readonly List<GameObject> instances = new List<GameObject>();

        [MenuItem("UnCredibles/UI Bake/Menu and Lobby")]
        public static void Bake()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var front = Object.FindFirstObjectByType<GarageFrontend>(FindObjectsInactive.Include);
            var lobby = front != null && front.lobbyRoot != null ? front.lobbyRoot.GetComponentInChildren<PartyLobbyView>(true) : null;
            if (front == null || front.menu == null || lobby == null || front.titleFont == null)
            {
                Debug.LogError("[UI Bake] " + ScenePath + " needs its Garage frontend with the menu, the lobby view and the title font.");
                return;
            }
            if (Get<Button>(front.menu, "quitButton") != null)
            {
                Debug.Log("[UI Bake] SCN_MainMenu is already baked; nothing changed.");
                return;
            }

            font = front.titleFont;
            panelSprite = Load<Sprite>("T_UI_Panel.png");
            pillSprite = Load<Sprite>("T_UI_Pill.png");
            discSprite = Load<Sprite>("T_UI_Disc.png");
            dashedSprite = Load<Sprite>("T_UI_Dashed.png");
            dotsTexture = Load<Texture2D>("T_UI_HalftoneDots.png");
            if (panelSprite == null || pillSprite == null || discSprite == null || dashedSprite == null || dotsTexture == null)
            {
                Debug.LogError("[UI Bake] Missing paper sprites or the halftone texture in " + PaperFolder);
                return;
            }
            shadowMaterial = ShadowMaterial();
            Folder(GaragePrefabs);
            Folder(LobbyPrefabs);
            instances.Clear();

            var settingsLabel = Get<Button>(front.menu, "settingsButton").GetComponentInChildren<TMP_Text>(true);
            var pill = SignPillPrefab(settingsLabel.fontSize);
            BakeHome(front.menu, pill);
            BakeOnlineSetup(front.menu);
            BakeLobby(lobby, pill);
            // Values set from code on prefab instances only stick as overrides once recorded.
            foreach (var instance in instances) Record(instance);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[UI Bake] Home, online sheet and lobby saved in SCN_MainMenu, with PF_MenuTab, PF_SignPill and PF_LobbySlot.");
        }

        // ---------- Home: comic panel, tab options, SALIR and its confirmation, title prompt ----------

        private static void BakeHome(MainMenuController menu, GameObject pill)
        {
            var home = Get<GameObject>(menu, "mainPanel").transform;
            var single = Get<Button>(menu, "singleplayerButton");
            var multi = Get<Button>(menu, "multiplayerButton");
            var settings = Get<Button>(menu, "settingsButton");
            var olds = new List<Button>();
            foreach (Transform child in home)
                if (child.TryGetComponent(out Button button)) olds.Add(button);

            // SALIR goes one step (the gap under AJUSTES) below the lowest option, with AJUSTES's size.
            var template = (RectTransform)settings.transform;
            float lowest = template.anchoredPosition.y, step = 0f;
            foreach (var old in olds)
            {
                float y = ((RectTransform)old.transform).anchoredPosition.y;
                if (y < lowest) lowest = y;
                if (y < template.anchoredPosition.y && (step == 0f || template.anchoredPosition.y - y < step))
                    step = template.anchoredPosition.y - y;
            }
            if (step == 0f) step = template.rect.height + 25f;
            float quitY = lowest - step;

            home.name = "Home";
            GetOrAdd<CanvasGroup>(home.gameObject); // the intro blocks clicks with it while the options slide in
            var slide = new List<RectTransform> { HomePanel(home) };
            var options = Root("Options", home);
            options.SetSiblingIndex(olds[0].transform.GetSiblingIndex());
            var tabPrefab = MenuTabPrefab(single);
            var map = new Dictionary<Button, Button>();
            foreach (var old in olds)
            {
                var label = old.GetComponentInChildren<TMP_Text>(true);
                var localized = old.GetComponentInChildren<LocalizeStringEvent>(true);
                var option = Option(tabPrefab, options, OptionName(old.name), (RectTransform)old.transform,
                    label != null ? label.text : old.name, localized != null ? localized.StringReference : null);
                CopyClicks(old, option);
                map[old] = option;
                slide.Add((RectTransform)option.transform);
            }
            var quit = Option(tabPrefab, options, "Option_Quit", template, "SALIR", new LocalizedString("UI", "menu.quit"));
            ((RectTransform)quit.transform).anchoredPosition = new Vector2(template.anchoredPosition.x, quitY);
            slide.Add((RectTransform)quit.transform);

            // ACEPTAR / CANCELAR occupy the same row as SALIR.
            var confirm = Root("QuitConfirm", home);
            float width = template.sizeDelta.x;
            float center = template.anchoredPosition.x;
            var size = new Vector2(width * .5f - 16f, template.sizeDelta.y - 16f); // a gap between them and the sheet's edge
            var acceptAt = new Vector2(center - width * .25f, quitY);
            var cancelAt = new Vector2(center + width * .25f, quitY);
            var accept = SignPill(pill, confirm, "Accept", template.anchorMin, acceptAt, size, SignButton.Brick);
            var cancel = SignPill(pill, confirm, "Cancel", template.anchorMin, cancelAt, size, PaperUI.Ink);
            accept.GetComponentInChildren<TMP_Text>(true).text = "ACEPTAR";
            cancel.GetComponentInChildren<TMP_Text>(true).text = "CANCELAR";
            Localize(accept.GetComponentInChildren<TMP_Text>(true), new LocalizedString("UI", "menu.quit_accept"));
            Localize(cancel.GetComponentInChildren<TMP_Text>(true), new LocalizedString("UI", "menu.quit_cancel"));
            foreach (var button in new[] { accept, cancel })
            {
                GetOrAdd<KeepSelectionSize>(button.gameObject); // side by side: selected, they must not overlap
                button.gameObject.SetActive(false);
            }
            var sheet = PaperUI.Panel("Sheet", confirm, PaperUI.Paper, 1.5f);
            sheet.transform.SetAsFirstSibling(); // under the two buttons
            PaperUI.Place(sheet.rectTransform, template.anchorMin, new Vector2(center, quitY), new Vector2(width + 8f, size.y + 56f));
            var tab = PaperUI.Pill("Tab", sheet.transform, PaperUI.Ink, 40f);
            PaperUI.Place(tab.rectTransform, new Vector2(0f, 1f), new Vector2(20f, 18f), new Vector2(160f, 40f));
            var quitTitle = PaperUI.Text("Label", tab.transform, font, 28f, PaperUI.Paper, TextAlignmentOptions.Center);
            PaperUI.Stretch(quitTitle.rectTransform);
            quitTitle.text = "SALIR";
            sheet.gameObject.SetActive(false);

            // Title screen prompt, centred 30% of the screen below the middle, in AJUSTES's colour with the hard shadow.
            var labelColor = settings.GetComponentInChildren<TMP_Text>(true).color;
            var press = PaperUI.Text("PressStart", home.parent, font, 60f, labelColor, TextAlignmentOptions.Center);
            Text(press, labelColor, true);
            var pressRect = press.rectTransform;
            pressRect.anchorMin = pressRect.anchorMax = new Vector2(.5f, .2f);
            pressRect.pivot = Middle; // pops from its centre
            pressRect.anchoredPosition = Vector2.zero;
            pressRect.sizeDelta = new Vector2(1000f, 90f);
            press.text = "PRESS START";
            press.gameObject.SetActive(false);

            Set(menu, "singleplayerButton", map[single], "multiplayerButton", map[multi], "settingsButton", map[settings],
                "quitButton", quit, "quitAccept", accept, "quitCancel", cancel, "quitSheet", sheet.gameObject,
                "quitTitle", quitTitle, "pressStart", press, "introSlide", slide.ToArray());
            foreach (var old in olds) Object.DestroyImmediate(old.gameObject);
        }

        // Mustard halftone panel down the left of the screen, behind the logo and the options.
        private static RectTransform HomePanel(Transform home)
        {
            var panel = new GameObject("Panel_Halftone", typeof(RectTransform), typeof(CanvasRenderer), typeof(HalftonePanel))
                .GetComponent<HalftonePanel>();
            var rect = panel.rectTransform;
            rect.SetParent(home, false);
            rect.SetAsFirstSibling();
            panel.color = SignButton.Mustard;
            panel.raycastTarget = false;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, .5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(HomePanelWidth, 0f);
            Set(panel, "dots", dotsTexture);
            return rect;
        }

        // Ink text over the panel with an invisible hit area; the cream tab MenuTab slides out is folded at idle.
        private static GameObject MenuTabPrefab(Button source)
        {
            var button = Object.Instantiate(source, source.transform.parent);
            button.onClick = new Button.ButtonClickedEvent();
            button.transition = Selectable.Transition.None; // the tab replaces the pink tint
            button.navigation = new Navigation { mode = Navigation.Mode.Automatic };
            GetOrAdd<KeepSelectionSize>(button.gameObject); // nor does it grow
            if (button.targetGraphic != null) button.targetGraphic.color = Color.clear;

            var label = button.GetComponentInChildren<TMP_Text>(true);
            label.name = "Label";
            Text(label, PaperUI.Ink, false);
            label.enableAutoSizing = false;
            label.fontSize = MenuTab.FontSize;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            var text = label.rectTransform;
            text.anchorMin = Vector2.zero;
            text.anchorMax = Vector2.one;
            text.pivot = new Vector2(0f, .5f);
            text.offsetMin = new Vector2(MenuTab.TextInset, 0f);
            text.offsetMax = Vector2.zero;

            var face = PaperUI.Panel("Tab", button.transform, PaperUI.Paper);
            face.pixelsPerUnitMultiplier = MenuTab.TabBorder;
            var depth = face.GetComponent<Shadow>();
            depth.effectColor = MenuTab.Side;
            depth.effectDistance = MenuTab.Depth;
            var tab = face.rectTransform;
            tab.SetAsFirstSibling(); // under the text
            tab.anchorMin = Vector2.zero;
            tab.anchorMax = Vector2.one;
            tab.pivot = new Vector2(0f, .5f);
            tab.offsetMin = Vector2.zero;
            tab.offsetMax = new Vector2(MenuTab.Reach, 0f);
            tab.localScale = new Vector3(0f, 1f, 1f); // MenuTab's idle pose; scale x to 1 to see it while editing

            Set(GetOrAdd<MenuTab>(button.gameObject), "target", button, "tab", tab, "label", label);
            return Save(button.gameObject, GaragePrefabs + "/PF_MenuTab.prefab");
        }

        private static Button Option(GameObject prefab, Transform parent, string name, RectTransform from, string text, LocalizedString key)
        {
            var go = Instance(prefab, parent, name);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = from.anchorMin;
            rect.anchorMax = from.anchorMax;
            rect.pivot = from.pivot;
            rect.anchoredPosition = from.anchoredPosition;
            rect.sizeDelta = from.sizeDelta;
            go.SetActive(from.gameObject.activeSelf);
            var label = go.GetComponentInChildren<TMP_Text>(true);
            label.text = text;
            Localize(label, key);
            return go.GetComponent<Button>();
        }

        private static string OptionName(string sceneName) => sceneName switch
        {
            "JUGAR LOCAL" => "Option_PlayLocal",
            "MULTIPLAYER" => "Option_Online",
            "AJUSTES" => "Option_Settings",
            "CREDITOS" => "Option_Credits",
            "GALERIA" => "Option_Gallery",
            _ => "Option_" + sceneName.Replace(" ", ""),
        };

        // ---------- Online setup: restyled in place as a paper sheet ----------

        private static void BakeOnlineSetup(MainMenuController menu)
        {
            var root = Get<GameObject>(menu, "multiplayerSetupPanel").transform;
            var panel = root.Find("Panel");
            if (panel != null && panel.TryGetComponent(out Image background))
            {
                background.sprite = panelSprite;
                background.type = Image.Type.Sliced;
                background.pixelsPerUnitMultiplier = 1f;
                background.color = PaperUI.Paper;
                background.rectTransform.localEulerAngles = new Vector3(0f, 0f, -1.5f);
                AddShadow(background);
            }
            var title = root.Find("Text - MULTIPLAYER");
            if (title != null && title.TryGetComponent(out TMP_Text titleText))
            {
                var label = PaperUI.Panel("Title label", root, Color.white, -2f, true);
                label.transform.SetSiblingIndex(title.GetSiblingIndex());
                PaperUI.Place(label.rectTransform, titleText.rectTransform.anchorMin, titleText.rectTransform.anchoredPosition, new Vector2(440f, 90f));
                titleText.rectTransform.localEulerAngles = new Vector3(0f, 0f, -2f);
                Text(titleText, PaperUI.Ink, false);
            }
            var caption = root.Find("Text - CODIGO DE SALA");
            if (caption != null && caption.TryGetComponent(out TMP_Text captionText)) Text(captionText, PaperUI.Ink, false);

            // The code field as a pasted note.
            var input = Get<TMP_InputField>(menu, "roomCodeInput");
            if (input.targetGraphic is Image field)
            {
                field.sprite = panelSprite;
                field.type = Image.Type.Sliced;
                field.pixelsPerUnitMultiplier = 1.6f; // smaller corners on a short field
                field.color = Color.white;
                field.rectTransform.localEulerAngles = new Vector3(0f, 0f, 1f);
                AddShadow(field);
            }
            input.fontAsset = font;
            input.textComponent.color = PaperUI.Ink;
            if (input.placeholder is TMP_Text hint) hint.color = SignButton.SoftInk;
            input.customCaretColor = true;
            input.caretColor = PaperUI.Ink;

            Pill(Get<Button>(menu, "createRoomButton"), PaperUI.Ink, PaperUI.Paper, -1f);
            Pill(Get<Button>(menu, "joinRoomButton"), SignButton.SofaBlue, PaperUI.Paper, 1f);
            Pill(Get<Button>(menu, "backButton"), SignButton.Brick, PaperUI.Paper, -1f);
            Text(Get<TMP_Text>(menu, "connectionStatus"), PaperUI.Ink, false);
        }

        // ---------- Lobby: paper cards, taped title with the room code pill, controls footer, phone QR ----------

        private static void BakeLobby(PartyLobbyView view, GameObject pill)
        {
            var modeText = Get<TMP_Text>(view, "modeText");
            var hintText = Get<TMP_Text>(view, "hintText");
            var batPadText = Get<TMP_Text>(view, "batPadText");
            var qr = (RectTransform)Get<RawImage>(view, "batPadQr").transform;
            var parent = modeText.transform.parent;

            var oldSlots = new SerializedObject(view).FindProperty("slotViews");
            var cards = new LobbySlotView[oldSlots.arraySize];
            for (int i = 0; i < cards.Length; i++) cards[i] = (LobbySlotView)oldSlots.GetArrayElementAtIndex(i).objectReferenceValue;
            var slotPrefab = LobbySlotPrefab(cards[0]);
            var slots = new LobbySlotView[cards.Length];
            for (int i = 0; i < cards.Length; i++)
            {
                var from = (RectTransform)cards[i].transform;
                var go = Instance(slotPrefab, from.parent, "Slot_P" + (i + 1));
                go.transform.SetSiblingIndex(from.GetSiblingIndex());
                var rect = (RectTransform)go.transform;
                rect.anchorMin = from.anchorMin;
                rect.anchorMax = from.anchorMax;
                rect.pivot = from.pivot;
                rect.anchoredPosition = from.anchoredPosition;
                rect.sizeDelta = from.sizeDelta;
                rect.localEulerAngles = new Vector3(0f, 0f, i % 2 == 0 ? -1.5f : 1.5f);
                slots[i] = go.GetComponent<LobbySlotView>();
                var color = PlayerIdentity.ColorFor(i);
                color.a = .35f; // as RenderPaper draws a free slot
                Get<Image>(slots[i], "disc").color = color;
                Get<TMP_Text>(slots[i], "discTag").text = PlayerIdentity.LabelFor(i);
                Object.DestroyImmediate(from.gameObject);
            }

            var phonePosition = qr.anchoredPosition + new Vector2(-50f, 0f);
            // Copies of the QR caption, made before the caption gets its shadow. The callout: three short lines
            // (question, gamepad, QR) read the same in every language and stay on screen next to the QR.
            var callout = Object.Instantiate(batPadText, qr.parent);
            callout.name = "Add Player Callout";
            var calloutRect = callout.rectTransform;
            calloutRect.anchorMin = calloutRect.anchorMax = qr.anchorMin;
            calloutRect.sizeDelta = new Vector2(340f, 120f);
            calloutRect.anchoredPosition = phonePosition + new Vector2(0f, (qr.sizeDelta.y + calloutRect.sizeDelta.y) * .5f + 8f);
            callout.fontSize = 32f;
            callout.lineSpacing = -10f;
            callout.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
            callout.alignment = TextAlignmentOptions.Center;
            Text(callout, SignButton.Mustard, true);
            callout.gameObject.SetActive(false);

            // A "connect with your phone" button where the small QR was, and a dark panel with the QR big enough to scan from the sofa.
            var phone = SignPill(pill, qr.parent, "Phone Button", qr.anchorMin, phonePosition,
                new Vector2(330f, 76f), SignButton.SofaBlue, 1.5f); // shifted so its text fits on screen
            var phoneLabel = PlainLabel(phone, 24f);
            phone.gameObject.SetActive(false);

            var overlay = new GameObject("Phone QR Panel", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            overlay.rectTransform.SetParent(qr.parent, false);
            PaperUI.Stretch(overlay.rectTransform);
            overlay.color = new Color(0f, 0f, 0f, .9f); // also blocks clicks behind
            var bigQr = new GameObject("Big QR", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            bigQr.rectTransform.SetParent(overlay.rectTransform, false);
            bigQr.rectTransform.sizeDelta = new Vector2(560f, 560f);
            bigQr.rectTransform.anchoredPosition = new Vector2(0f, 40f);
            var scan = Object.Instantiate(batPadText, overlay.rectTransform);
            scan.name = "Scan Text";
            scan.gameObject.SetActive(true);
            scan.rectTransform.anchorMin = scan.rectTransform.anchorMax = Middle;
            scan.rectTransform.sizeDelta = new Vector2(1500f, 60f);
            scan.rectTransform.anchoredPosition = new Vector2(0f, 370f);
            scan.fontSize = 32f;
            scan.textWrappingMode = TextWrappingModes.NoWrap;
            scan.alignment = TextAlignmentOptions.Center;
            Text(scan, PaperUI.Paper, false);
            var close = SignPill(pill, overlay.rectTransform, "Close", Middle, new Vector2(0f, -300f), new Vector2(280f, 70f), PaperUI.Ink);
            var closeLabel = PlainLabel(close, 28f);
            overlay.gameObject.SetActive(false);

            // Title on a taped label; PartyLobbyView fits it and puts the room code pill right after it.
            var titleCenter = modeText.rectTransform.anchoredPosition;
            var titleLabel = PaperUI.Panel("Title label", parent, Color.white, -1.5f, true);
            titleLabel.transform.SetSiblingIndex(modeText.transform.GetSiblingIndex());
            PaperUI.Place(titleLabel.rectTransform, modeText.rectTransform.anchorMin, titleCenter, new Vector2(500f, 84f));
            Text(modeText, PaperUI.Ink, false);
            modeText.enableAutoSizing = true;
            modeText.fontSizeMin = 24f;
            modeText.fontSizeMax = 52f;
            modeText.rectTransform.localEulerAngles = new Vector3(0f, 0f, -1.5f);

            var codePill = PaperUI.Pill("Room code", parent, PaperUI.TimerTeal, 68f);
            PaperUI.Place(codePill.rectTransform, titleLabel.rectTransform.anchorMin, titleCenter, new Vector2(300f, 68f));
            codePill.rectTransform.localEulerAngles = new Vector3(0f, 0f, 2f);
            var codeText = PaperUI.Text("Code", codePill.transform, font, 44f, PaperUI.Paper, TextAlignmentOptions.Center);
            PaperUI.Stretch(codeText.rectTransform, 6f);
            codePill.gameObject.SetActive(false);

            // Controls footer on a paper pill, one line per hint.
            var hintRect = hintText.rectTransform;
            var footer = PaperUI.Pill("Controls", parent, PaperUI.Paper, 84f);
            footer.transform.SetSiblingIndex(hintText.transform.GetSiblingIndex());
            PaperUI.Place(footer.rectTransform, hintRect.anchorMin, hintRect.anchoredPosition, new Vector2(hintRect.sizeDelta.x + 40f, 84f));
            Text(hintText, PaperUI.Ink, false);
            hintText.fontSizeMax = hintText.fontSize;
            hintText.fontSizeMin = 14f;
            hintText.enableAutoSizing = true;
            hintRect.sizeDelta = new Vector2(hintRect.sizeDelta.x - 40f, 74f);

            Text(Get<TMP_Text>(view, "countdownText"), PaperUI.Paper, true);
            Text(batPadText, PaperUI.Paper, true);
            Pill(Get<Button>(view, "backButton"), SignButton.Brick, PaperUI.Paper, -1.5f);

            Set(view, "slotViews", slots, "addPlayerCallout", callout, "phoneButton", phone, "phoneButtonLabel", phoneLabel,
                "phoneOverlay", overlay.gameObject, "bigQr", bigQr, "phoneScanText", scan, "phoneClose", close,
                "phoneCloseLabel", closeLabel, "titleLabel", titleLabel.rectTransform, "codePill", codePill, "codeText", codeText);
        }

        // The scene card restyled as a paper sheet: tape, the player's disc, READY stamp, pills and the bot level buttons.
        private static GameObject LobbySlotPrefab(LobbySlotView source)
        {
            var view = Object.Instantiate(source, source.transform.parent);
            var card = view.transform;
            var background = Get<Image>(view, "background");
            var nameText = Get<TMP_Text>(view, "nameText");
            var inputText = Get<TMP_Text>(view, "inputText");
            var statusText = Get<TMP_Text>(view, "statusText");
            var crown = Get<GameObject>(view, "crown");
            var addAIButton = Get<Button>(view, "addAIButton");
            var removeButton = Get<Button>(view, "removeButton");
            background.pixelsPerUnitMultiplier = 1f;
            AddShadow(background);

            var tape = new GameObject("Tape", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            tape.transform.SetParent(card, false);
            tape.color = new Color(1f, .97f, .85f, .55f); // PaperUI's tape
            tape.raycastTarget = false;
            Place(tape.rectTransform, new Vector2(-160f, 118f), new Vector2(110f, 32f));
            tape.rectTransform.localEulerAngles = new Vector3(0f, 0f, 40f);

            var disc = PaperUI.Disc("Disc", card, Color.white);
            disc.sprite = discSprite;
            Place(disc.rectTransform, new Vector2(-132f, 78f), new Vector2(68f, 68f));
            var tag = PaperUI.Text("Tag", disc.transform, font, 30f, PaperUI.Ink, TextAlignmentOptions.Center);
            PaperUI.Stretch(tag.rectTransform);

            // Behind the status text, which then reads in paper over the red ink.
            var stamp = PaperUI.Pill("Ready stamp", card, PaperUI.Alarm, 56f).rectTransform;
            stamp.SetSiblingIndex(statusText.transform.GetSiblingIndex());
            Place(stamp, new Vector2(0f, 2f), new Vector2(210f, 56f));
            stamp.localEulerAngles = new Vector3(0f, 0f, -8f);
            stamp.gameObject.SetActive(false);

            SlotLabel(nameText, new Vector2(6f, 88f), new Vector2(192f, 44f), 38f, TextAlignmentOptions.Left);
            SlotLabel(inputText, new Vector2(40f, 50f), new Vector2(264f, 30f), 24f, TextAlignmentOptions.Left);
            SlotLabel(statusText, new Vector2(0f, 2f), new Vector2(330f, 44f), 30f, TextAlignmentOptions.Center);
            inputText.color = SignButton.SoftInk;
            if (crown.TryGetComponent(out TMP_Text crownText))
            {
                Text(crownText, PaperUI.Alarm, false);
                Place(crownText.rectTransform, new Vector2(128f, 116f), new Vector2(100f, 28f));
                crownText.rectTransform.localEulerAngles = new Vector3(0f, 0f, 8f);
            }
            Place((RectTransform)removeButton.transform, new Vector2(0f, -92f), new Vector2(170f, 44f));
            Pill(removeButton, SignButton.Brick, PaperUI.Paper);
            Pill(addAIButton, PaperUI.Ink, PaperUI.Paper);

            // "+ BOT" opens one full-width button per difficulty, Easy on top and the lowest lined up with it,
            // and an X in the name's corner to go back. Copies of the styled "+ BOT", as they were at runtime.
            var template = (RectTransform)addAIButton.transform;
            float bottom = template.anchoredPosition.y - (template.sizeDelta.y - LevelButtonHeight) * .5f;
            string[] levelNames = { "Level_Easy", "Level_Medium", "Level_Hard" }; // BotDifficulty order
            var levels = new Button[levelNames.Length];
            for (int i = 0; i < levels.Length; i++)
                levels[i] = LevelButton(addAIButton, levelNames[i],
                    new Vector2(template.anchoredPosition.x, bottom + (levels.Length - 1 - i) * LevelButtonStep),
                    new Vector2(template.sizeDelta.x, LevelButtonHeight));
            var cancel = LevelButton(addAIButton, "Level_Cancel",
                new Vector2(template.anchoredPosition.x + (template.sizeDelta.x - LevelCancelSize) * .5f, nameText.rectTransform.anchoredPosition.y),
                Vector2.one * LevelCancelSize);
            cancel.GetComponentInChildren<TMP_Text>(true).text = "X";

            // Saved as a free slot, the way RenderPaper draws it until the lobby has players.
            var shadow = background.GetComponent<Shadow>();
            background.sprite = dashedSprite;
            background.type = Image.Type.Tiled;
            background.color = LobbySlotView.FadedPaper;
            shadow.enabled = false;
            tape.gameObject.SetActive(false);
            nameText.color = LobbySlotView.FadedInk;
            tag.color = LobbySlotView.FadedInk;

            Set(view, "levelButtons", levels, "levelCancel", cancel, "shadow", shadow, "tape", tape.gameObject,
                "disc", disc, "discTag", tag, "stamp", stamp, "paperSprite", panelSprite, "dashedSprite", dashedSprite);
            return Save(view.gameObject, LobbyPrefabs + "/PF_LobbySlot.prefab");
        }

        private static Button LevelButton(Button addAI, string name, Vector2 position, Vector2 size)
        {
            var rect = Object.Instantiate((RectTransform)addAI.transform, addAI.transform.parent);
            rect.name = name;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var localized = rect.GetComponentInChildren<LocalizeStringEvent>(true);
            if (localized != null) Object.DestroyImmediate(localized); // LobbySlotView writes its text
            var button = rect.GetComponent<Button>();
            Pill(button, PaperUI.Ink, PaperUI.Paper); // refits the round caps to the new height
            rect.gameObject.SetActive(false);
            return button;
        }

        private static void SlotLabel(TMP_Text label, Vector2 position, Vector2 size, float fontSize, TextAlignmentOptions alignment)
        {
            Text(label, PaperUI.Ink, false);
            Place(label.rectTransform, position, size);
            label.alignment = alignment;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.enableAutoSizing = true;
            label.fontSizeMin = 16f;
            label.fontSizeMax = fontSize;
        }

        private static void Place(RectTransform rect, Vector2 position, Vector2 size) => PaperUI.Place(rect, Middle, position, size);

        // ---------- Pills (SignButton) ----------

        // Pill button with a centred label; each instance gets its fill, tilt and size.
        private static GameObject SignPillPrefab(float fontSize)
        {
            var face = PaperUI.Pill("PF_SignPill", null, PaperUI.Ink, PillShape);
            face.raycastTarget = true;
            face.rectTransform.sizeDelta = new Vector2(280f, PillShape);
            var button = face.gameObject.AddComponent<Button>();
            button.targetGraphic = face;
            var label = PaperUI.Text("Label", face.transform, font, fontSize, PaperUI.Paper, TextAlignmentOptions.Midline);
            PaperUI.Stretch(label.rectTransform);
            label.rectTransform.offsetMin = new Vector2(5f, 0f);
            label.rectTransform.offsetMax = new Vector2(-5f, 0f);
            label.enableAutoSizing = true;
            label.fontSizeMin = 14f;
            label.fontSizeMax = fontSize;
            Pill(button, PaperUI.Ink, PaperUI.Paper);
            return Save(face.gameObject, GaragePrefabs + "/PF_SignPill.prefab");
        }

        private static Button SignPill(GameObject prefab, Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size,
            Color fill, float tilt = 0f)
        {
            var go = Instance(prefab, parent, name);
            PaperUI.Place((RectTransform)go.transform, anchor, position, size);
            var button = go.GetComponent<Button>();
            Pill(button, fill, PaperUI.Paper, tilt);
            return button;
        }

        // Label over the whole pill at a fixed size, for texts PartyLobbyView writes.
        private static TMP_Text PlainLabel(Button button, float fontSize)
        {
            var label = button.GetComponentInChildren<TMP_Text>(true);
            PaperUI.Stretch(label.rectTransform);
            label.enableAutoSizing = false;
            label.fontSize = fontSize;
            return label;
        }

        // Paper-kit pill of the given fill; text in paper (or ink on a light fill).
        private static void Pill(Selectable button, Color fill, Color text, float tilt = 0f)
        {
            var image = button.targetGraphic as Image;
            if (image == null) image = button.GetComponent<Image>();
            image.sprite = pillSprite;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = PillShape / Mathf.Max(1f, ((RectTransform)button.transform).sizeDelta.y);
            image.color = fill; // SignButton paints it so at runtime; here the editor shows it too
            button.transition = Selectable.Transition.None; // SignButton replaces the pink tint
            AddShadow(image);
            var label = button.GetComponentInChildren<TMP_Text>(true);
            Set(GetOrAdd<SignButton>(button.gameObject), "target", button, "face", image, "label", label,
                "fill", fill, "textColor", text, "tilt", tilt);
            button.transform.localEulerAngles = new Vector3(0f, 0f, tilt);
            if (label != null) Text(label, text, false);
        }

        // ---------- Shared helpers ----------

        // Bangers in the given colour; shadowed = hard ink drop shadow (TMP underlay material asset).
        private static void Text(TMP_Text label, Color color, bool shadowed)
        {
            label.font = font;
            label.fontStyle &= ~FontStyles.Bold; // Bangers is already heavy
            label.color = color;
            if (!shadowed) return;
            label.fontSharedMaterial = shadowMaterial;
            label.extraPadding = true; // room for the offset underlay
        }

        private static void AddShadow(Graphic graphic)
        {
            var shadow = GetOrAdd<Shadow>(graphic.gameObject);
            shadow.effectColor = ShadowColor;
            shadow.effectDistance = ShadowOffset;
        }

        // Next to the font, so the scene can reference it (the runtime used a throwaway copy).
        private static Material ShadowMaterial()
        {
            string path = Path.GetDirectoryName(AssetDatabase.GetAssetPath(font)).Replace('\\', '/') + "/" + font.name + " - Hard Shadow.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(font.material);
            material.EnableKeyword(ShaderUtilities.Keyword_Underlay);
            material.SetColor(ShaderUtilities.ID_UnderlayColor, PaperUI.Ink);
            material.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, .8f);
            material.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -.8f);
            material.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0f);
            material.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0f);
            ShaderUtilities.UpdateShaderRatios(material);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        // The label follows the language like the scene's own labels: a LocalizeStringEvent writing into its text.
        private static void Localize(TMP_Text label, LocalizedString key)
        {
            var localized = label.GetComponent<LocalizeStringEvent>();
            if (key == null)
            {
                if (localized != null) localized.enabled = false; // a plain label: nothing may write over it
                return;
            }
            if (localized == null) localized = label.gameObject.AddComponent<LocalizeStringEvent>();
            localized.StringReference.SetReference(key.TableReference, key.TableEntryReference);
            if (localized.OnUpdateString.GetPersistentEventCount() > 0) return;
            var setText = typeof(TMP_Text).GetProperty(nameof(TMP_Text.text)).GetSetMethod();
            UnityEventTools.AddPersistentListener(localized.OnUpdateString,
                (UnityAction<string>)System.Delegate.CreateDelegate(typeof(UnityAction<string>), label, setText));
        }

        // CREDITOS and GALERIA call GarageFrontend from the scene.
        private static void CopyClicks(Button from, Button to)
        {
            for (int i = 0; i < from.onClick.GetPersistentEventCount(); i++)
            {
                var target = from.onClick.GetPersistentTarget(i);
                string method = from.onClick.GetPersistentMethodName(i);
                if (target == null || string.IsNullOrEmpty(method)) continue;
                UnityEventTools.AddPersistentListener(to.onClick,
                    (UnityAction)System.Delegate.CreateDelegate(typeof(UnityAction), target, method));
            }
        }

        private static GameObject Instance(GameObject prefab, Transform parent, string name)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = name;
            instances.Add(go);
            return go;
        }

        private static GameObject Save(GameObject template, string path)
        {
            var prefab = PrefabUtility.SaveAsPrefabAsset(template, path);
            Object.DestroyImmediate(template);
            return prefab;
        }

        private static void Record(GameObject root)
        {
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
            {
                if (PrefabUtility.GetCorrespondingObjectFromSource(child.gameObject) != null)
                    PrefabUtility.RecordPrefabInstancePropertyModifications(child.gameObject);
                foreach (var component in child.GetComponents<Component>())
                    if (component != null && PrefabUtility.GetCorrespondingObjectFromSource(component) != null)
                        PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            }
        }

        private static Transform Root(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            PaperUI.Stretch(rect);
            return rect;
        }

        private static T Load<T>(string file) where T : Object => AssetDatabase.LoadAssetAtPath<T>(PaperFolder + file);

        private static T Get<T>(Object target, string field) where T : Object =>
            new SerializedObject(target).FindProperty(field).objectReferenceValue as T;

        private static T GetOrAdd<T>(GameObject go) where T : Component => go.TryGetComponent(out T found) ? found : go.AddComponent<T>();

        // Serialized fields by name (most are private): object references, colours, floats and object arrays.
        private static void Set(Object target, params object[] pairs)
        {
            var serialized = new SerializedObject(target);
            for (int i = 0; i < pairs.Length; i += 2)
            {
                var property = serialized.FindProperty((string)pairs[i]);
                if (property == null) throw new System.ArgumentException(target.GetType().Name + " has no serialized field " + pairs[i]);
                switch (pairs[i + 1])
                {
                    case Color color: property.colorValue = color; break;
                    case float value: property.floatValue = value; break;
                    case Object[] list:
                        property.arraySize = list.Length;
                        for (int j = 0; j < list.Length; j++) property.GetArrayElementAtIndex(j).objectReferenceValue = list[j];
                        break;
                    default: property.objectReferenceValue = (Object)pairs[i + 1]; break;
                }
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Folder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\', '/'), Path.GetFileName(path));
        }
    }
}
#endif
