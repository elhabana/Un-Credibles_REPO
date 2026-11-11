using TMPro;
using UnCredibles.Localization;
using UnCredibles.Players;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace UnCredibles.Minigames
{
    // "Mission briefing" shown before every minigame (art bible, p. 26): a tilted paper sheet with the
    // name, the rules, the gameplay video (or thumbnail), the controls as ink buttons, a disc per player
    // that gets a READY stamp when they press Jump, and the START box.
    // The countdown waits until every connected human is ready (see MinigameController.AwaitingReady);
    // this card only shows that state, so it works unchanged on online replicas.
    // Built in code, so every minigame scene gets it without setup. Texts come from the Minigames
    // table: "name.<id>", "rules.<id>" and "phone.<id>" (controls, "Key: action · Key: action");
    // the MinigameData fields are the fallback.
    public sealed class MinigameRulesCard : MonoBehaviour
    {
        private static readonly Color Backdrop = new Color(.114f, .114f, .106f, .85f); // see-through ink
        private static readonly Color DotColor = new Color(.169f, .647f, .604f, .3f);
        private static readonly Vector2 Middle = new Vector2(.5f, .5f);
        private static readonly Vector2 TopLeft = new Vector2(0f, 1f);
        private static readonly Vector2 TopRight = new Vector2(1f, 1f);
        private static readonly Vector2 BottomLeft = new Vector2(0f, 0f);
        private static readonly Vector2 BottomRight = new Vector2(1f, 0f);
        private static readonly Vector2 SheetSize = new Vector2(1560f, 880f);
        private const float SheetTilt = -1.5f;
        private const float Margin = 50f;
        private const float LeftWidth = 860f;   // title, mission, controls
        private const float RightWidth = 600f;  // media, players, START
        // Gameplay video or thumbnail, inside the media frame (16:9).
        private const float MediaWidth = 560f;
        private const float MediaHeight = 315f;
        private const int MaxControls = 4;
        private const float KeyHeight = 46f;
        private const float RowStep = 54f;
        private const float KeyInset = 28f;
        private const float DiscSize = 90f;
        private const float DiscSpacing = 145f;
        private const float InSeconds = .45f;
        private const float OutSeconds = .3f;
        private const float StampSeconds = .15f;
        private const float SlideDistance = 700f;

        private MinigameController controller;
        private TMP_FontAsset titleFont;
        private GameObject root;
        private CanvasGroup group;
        private RectTransform sheet;
        private RectTransform startBox;
        private Image thumbnail;
        private GameObject placeholder;
        private RawImage video;
        private VideoPlayer videoPlayer;
        private RenderTexture videoTexture;
        private TMP_Text title;
        private TMP_Text placeholderName;
        private TMP_Text missionTab;
        private TMP_Text rules;
        private GameObject controlsBox;
        private TMP_Text controlsTab;
        private TMP_Text startLabel;
        private TMP_Text pressLabel;
        private readonly Image[] keys = new Image[MaxControls];
        private readonly TMP_Text[] keyLabels = new TMP_Text[MaxControls];
        private readonly TMP_Text[] actions = new TMP_Text[MaxControls];
        private readonly Image[] discs = new Image[PlayerRegistry.MaxPlayers];
        private readonly TMP_Text[] tags = new TMP_Text[PlayerRegistry.MaxPlayers];
        private readonly TMP_Text[] names = new TMP_Text[PlayerRegistry.MaxPlayers];
        private readonly RectTransform[] stamps = new RectTransform[PlayerRegistry.MaxPlayers];
        private readonly TMP_Text[] stampLabels = new TMP_Text[PlayerRegistry.MaxPlayers];
        private readonly bool[] wasReady = new bool[PlayerRegistry.MaxPlayers];
        private readonly float[] stampTime = new float[PlayerRegistry.MaxPlayers];
        private float animTime;
        private bool shown;
        private bool leaving;

        public void Setup(MinigameController game)
        {
            controller = game;
            var ui = game.GetComponent<MinigameUI>();
            if (ui == null) ui = FindAnyObjectByType<MinigameUI>();
            titleFont = ui != null ? ui.TitleFont : null;

            root = PaperUI.Canvas("Rules card", transform, 50).gameObject; // above the minigame HUD
            group = root.AddComponent<CanvasGroup>();
            var backdrop = new GameObject("Backdrop", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            backdrop.transform.SetParent(root.transform, false);
            backdrop.color = Backdrop;
            backdrop.raycastTarget = false;
            PaperUI.Stretch(backdrop.rectTransform);

            sheet = PaperUI.Panel("Sheet", root.transform, PaperUI.Paper, SheetTilt, true).rectTransform;
            Center(sheet, Middle, Vector2.zero, SheetSize);
            // The kit's tape is sized for small badges; a sheet this big needs a wider strip.
            if (sheet.Find("Tape") is RectTransform tape) tape.sizeDelta = new Vector2(170f, 50f);

            BuildLeftColumn();
            BuildMedia();
            BuildPlayers();
            BuildStart();

            for (int i = 0; i < stampTime.Length; i++) stampTime[i] = -1f;
            controller.ReadyChanged += Refresh;
            controller.StateChanged += HandleStateChanged;
            L10n.LanguageChanged += Refresh;
            Refresh();
        }

        private void BuildLeftColumn()
        {
            var titleBox = PaperUI.Panel("Title", sheet, Color.white, 1.5f);
            PaperUI.Place(titleBox.rectTransform, TopLeft, new Vector2(Margin, -40f), new Vector2(LeftWidth, 130f));
            title = PaperUI.Text("Name", titleBox.transform, titleFont, 84f, PaperUI.Ink, TextAlignmentOptions.Center);
            AutoSize(title, 40f, 84f);
            PaperUI.Stretch(title.rectTransform, 16f);

            var mission = PaperUI.Panel("Mission", sheet, Color.white);
            PaperUI.Place(mission.rectTransform, TopLeft, new Vector2(Margin, -215f), new Vector2(LeftWidth, 320f));
            missionTab = Tab(mission.transform);
            rules = PaperUI.Text("Rules", mission.transform, null, 32f, PaperUI.Ink, TextAlignmentOptions.TopLeft);
            rules.textWrappingMode = TextWrappingModes.Normal;
            AutoSize(rules, 20f, 32f);
            PaperUI.Stretch(rules.rectTransform, KeyInset);
            rules.rectTransform.offsetMax = new Vector2(-KeyInset, -40f); // below the tab

            var controls = PaperUI.Panel("Controls", sheet, Color.white);
            PaperUI.Place(controls.rectTransform, BottomLeft, new Vector2(Margin, 40f), new Vector2(LeftWidth, 270f));
            controlsBox = controls.gameObject;
            controlsTab = Tab(controls.transform);
            for (int i = 0; i < MaxControls; i++)
            {
                float y = -42f - i * RowStep;
                // Round ink button; a longer key ("JOYSTICK") stretches it into a pill.
                keys[i] = PaperUI.Pill("Key", controls.transform, PaperUI.Ink, KeyHeight);
                PaperUI.Place(keys[i].rectTransform, TopLeft, new Vector2(KeyInset, y), new Vector2(KeyHeight, KeyHeight));
                keyLabels[i] = PaperUI.Text("Label", keys[i].transform, titleFont, 28f, PaperUI.Paper, TextAlignmentOptions.Center);
                PaperUI.Stretch(keyLabels[i].rectTransform);
                actions[i] = PaperUI.Text("Action", controls.transform, null, 28f, PaperUI.Ink, TextAlignmentOptions.Left);
                AutoSize(actions[i], 18f, 28f);
                PaperUI.Place(actions[i].rectTransform, TopLeft, new Vector2(KeyInset, y), new Vector2(LeftWidth, KeyHeight));
            }
        }

        private void BuildMedia()
        {
            var frame = PaperUI.Panel("Media", sheet, Color.white, 2f);
            PaperUI.Place(frame.rectTransform, TopRight, new Vector2(-Margin, -40f), new Vector2(RightWidth, 355f));
            var mediaSize = new Vector2(MediaWidth, MediaHeight);

            thumbnail = new GameObject("Thumbnail", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            thumbnail.transform.SetParent(frame.transform, false);
            thumbnail.preserveAspect = true;
            thumbnail.raycastTarget = false;
            Center(thumbnail.rectTransform, Middle, Vector2.zero, mediaSize);

            // Without video or thumbnail: a dotted pattern with the name, never an empty grey box.
            var dots = new GameObject("Placeholder", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            dots.transform.SetParent(frame.transform, false);
            dots.sprite = PaperUI.DiscSprite;
            dots.type = Image.Type.Tiled;
            dots.pixelsPerUnitMultiplier = 3f;
            dots.color = DotColor;
            dots.raycastTarget = false;
            Center(dots.rectTransform, Middle, Vector2.zero, mediaSize);
            placeholder = dots.gameObject;
            placeholderName = PaperUI.Text("Name", dots.transform, titleFont, 64f, PaperUI.Ink, TextAlignmentOptions.Center);
            placeholderName.textWrappingMode = TextWrappingModes.Normal;
            AutoSize(placeholderName, 32f, 64f);
            PaperUI.Stretch(placeholderName.rectTransform, 24f);

            BuildVideo(frame.transform, controller.Data != null ? controller.Data.Preview : null);
        }

        // Gameplay loop: muted, looping, drawn into a texture the card shows. Only built when there is a clip.
        private void BuildVideo(Transform parent, VideoClip clip)
        {
            if (clip == null) return;
            videoTexture = new RenderTexture((int)clip.width, (int)clip.height, 0);
            video = new GameObject("Gameplay", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            video.transform.SetParent(parent, false);
            video.texture = videoTexture;
            video.raycastTarget = false;
            // Fit the clip inside the media box, keeping its proportions.
            float scale = Mathf.Min(MediaWidth / clip.width, MediaHeight / clip.height);
            Center(video.rectTransform, Middle, Vector2.zero, new Vector2(clip.width * scale, clip.height * scale));

            videoPlayer = video.gameObject.AddComponent<VideoPlayer>();
            videoPlayer.playOnAwake = false;
            videoPlayer.isLooping = true;
            videoPlayer.audioOutputMode = VideoAudioOutputMode.None;
            videoPlayer.renderMode = VideoRenderMode.RenderTexture;
            videoPlayer.targetTexture = videoTexture;
            videoPlayer.clip = clip;
        }

        private void BuildPlayers()
        {
            for (int i = 0; i < discs.Length; i++)
            {
                discs[i] = PaperUI.Disc("Player " + (i + 1), sheet, Color.white);
                tags[i] = PaperUI.Text("Tag", discs[i].transform, titleFont, 40f, PaperUI.Ink, TextAlignmentOptions.Center);
                PaperUI.Stretch(tags[i].rectTransform);
                names[i] = PaperUI.Text("Name", discs[i].transform, null, 22f, PaperUI.Ink, TextAlignmentOptions.Center);
                names[i].overflowMode = TextOverflowModes.Ellipsis;
                PaperUI.Place(names[i].rectTransform, new Vector2(.5f, 0f), new Vector2(0f, -36f), new Vector2(DiscSpacing, 30f));

                // Rubber stamp slapped over the disc when the player is ready.
                var stamp = PaperUI.Pill("Ready stamp", discs[i].transform, PaperUI.Alarm, 48f);
                stamps[i] = stamp.rectTransform;
                Center(stamps[i], Middle, new Vector2(0f, -4f), new Vector2(140f, 48f));
                stamps[i].localEulerAngles = new Vector3(0f, 0f, -14f);
                stampLabels[i] = PaperUI.Text("Label", stamps[i], titleFont, 32f, PaperUI.Paper, TextAlignmentOptions.Center);
                PaperUI.Stretch(stampLabels[i].rectTransform);
            }
        }

        private void BuildStart()
        {
            var box = PaperUI.Panel("Start", sheet, PaperUI.Ink, -2f);
            startBox = box.rectTransform;
            Center(startBox, BottomRight, new Vector2(-Margin - RightWidth * .5f, 40f + 115f), new Vector2(RightWidth, 230f));
            startLabel = PaperUI.Text("Start", startBox, titleFont, 110f, PaperUI.Paper, TextAlignmentOptions.Center);
            AutoSize(startLabel, 50f, 110f);
            PaperUI.Place(startLabel.rectTransform, new Vector2(.5f, 1f), new Vector2(0f, -20f), new Vector2(RightWidth - 40f, 130f));
            pressLabel = PaperUI.Text("Press", startBox, titleFont, 40f, PaperUI.TimerTeal, TextAlignmentOptions.Center);
            AutoSize(pressLabel, 24f, 40f);
            PaperUI.Place(pressLabel.rectTransform, new Vector2(.5f, 0f), new Vector2(0f, 24f), new Vector2(RightWidth - 40f, 56f));
        }

        private void OnDestroy()
        {
            if (videoTexture != null)
            {
                videoTexture.Release();
                Destroy(videoTexture);
            }
            L10n.LanguageChanged -= Refresh;
            if (controller == null) return;
            controller.ReadyChanged -= Refresh;
            controller.StateChanged -= HandleStateChanged;
        }

        private void HandleStateChanged(MinigameState state) => Refresh();

        private void Refresh()
        {
            // Waiting ends with the countdown, on the host and on online clients alike.
            if (controller.State != MinigameState.Waiting)
            {
                Hide();
                return;
            }
            bool opening = !shown;
            if (opening) Show();

            var data = controller.Data;
            string id = data != null ? data.Id : string.Empty;
            string gameName = Localized("name." + id, data != null ? data.DisplayName : string.Empty);
            SetDisplay(title, gameName);
            SetDisplay(placeholderName, gameName);
            rules.text = Localized("rules." + id, data != null ? data.Rules : string.Empty);
            SetTab(missionTab, L10n.Get(L10n.UI, "rules.mission"));
            SetTab(controlsTab, L10n.Get(L10n.UI, "rules.controls"));
            ShowControls(Localized("phone." + id, string.Empty));
            SetDisplay(startLabel, L10n.Get(L10n.UI, "rules.start"));
            SetDisplay(pressLabel, L10n.Get(L10n.UI, "rules.press"));

            // The video wins over the thumbnail; without either, the placeholder fills the frame.
            bool hasVideo = videoPlayer != null;
            if (hasVideo && !videoPlayer.isPlaying) videoPlayer.Play();
            bool hasThumbnail = !hasVideo && data != null && data.Thumbnail != null;
            thumbnail.gameObject.SetActive(hasThumbnail);
            if (hasThumbnail) thumbnail.sprite = data.Thumbnail;
            placeholder.SetActive(!hasVideo && !hasThumbnail);

            var players = controller.Players;
            int count = Mathf.Min(players.Count, discs.Length);
            string readyText = L10n.Get(L10n.UI, "lobby.ready");
            for (int i = 0; i < discs.Length; i++)
            {
                bool used = i < count;
                discs[i].gameObject.SetActive(used);
                if (!used)
                {
                    wasReady[i] = false;
                    continue;
                }
                var player = players[i];
                Center(discs[i].rectTransform, TopRight,
                    new Vector2(-Margin - RightWidth * .5f + (i - (count - 1) * .5f) * DiscSpacing, -470f),
                    new Vector2(DiscSize, DiscSize));
                var color = PlayerIdentity.ColorFor(player.SlotIndex, player.IsAI);
                if (!player.IsConnected) color.a = .35f; // left the game: nobody waits for them
                discs[i].color = color;
                SetDisplay(tags[i], PlayerIdentity.LabelFor(player.SlotIndex, player.IsAI));
                names[i].text = player.PlayerName;
                SetDisplay(stampLabels[i], readyText);

                bool ready = controller.IsReady(i);
                stamps[i].gameObject.SetActive(ready);
                if (ready && !wasReady[i])
                {
                    stampTime[i] = 0f;
                    stamps[i].localScale = new Vector3(2.2f, 2.2f, 1f);
                    // Only a human pressing Jump: bots come in ready and make no sound.
                    if (!opening && !player.IsAI) GameAudio.Play(GameSfx.UiReady);
                }
                wasReady[i] = ready;
            }
        }

        // "Joystick: move · A: turbo · Unload the bags" -> one row per part, the key in an ink button.
        private void ShowControls(string text)
        {
            int row = 0;
            if (!string.IsNullOrEmpty(text))
            {
                foreach (string part in text.Split('·'))
                {
                    if (row == MaxControls) break;
                    string entry = part.Trim();
                    if (entry.Length == 0) continue;
                    int colon = entry.IndexOf(':');
                    string key = colon > 0 ? entry.Substring(0, colon).Trim().ToUpperInvariant() : string.Empty;
                    string action = colon > 0 ? entry.Substring(colon + 1).Trim() : entry;

                    float width = 0f;
                    keys[row].gameObject.SetActive(key.Length > 0);
                    if (key.Length > 0)
                    {
                        SetDisplay(keyLabels[row], key);
                        width = Mathf.Max(KeyHeight, keyLabels[row].GetPreferredValues(key).x + 28f);
                        keys[row].rectTransform.sizeDelta = new Vector2(width, KeyHeight);
                    }
                    var rect = actions[row].rectTransform;
                    float x = KeyInset + (width > 0f ? width + 18f : 0f);
                    rect.anchoredPosition = new Vector2(x, rect.anchoredPosition.y);
                    rect.sizeDelta = new Vector2(LeftWidth - x - KeyInset, KeyHeight);
                    actions[row].text = action.Length > 0 ? char.ToUpper(action[0]) + action.Substring(1) : action;
                    actions[row].gameObject.SetActive(true);
                    row++;
                }
            }
            controlsBox.SetActive(row > 0);
            for (int i = row; i < MaxControls; i++)
            {
                keys[i].gameObject.SetActive(false);
                actions[i].gameObject.SetActive(false);
            }
        }

        private void Show()
        {
            shown = true;
            leaving = false;
            animTime = 0f;
            group.alpha = 0f;
            sheet.anchoredPosition = new Vector2(0f, -SlideDistance);
            root.SetActive(true);
        }

        private void Hide()
        {
            if (!shown)
            {
                if (!leaving) root.SetActive(false);
                return;
            }
            shown = false;
            leaving = true;
            animTime = 0f;
        }

        private void Update()
        {
            if (root == null || !root.activeSelf) return;
            // Unscaled: the card animates even if something pauses time.
            float dt = Time.unscaledDeltaTime;
            animTime += dt;
            if (leaving)
            {
                // Flies up and fades while the countdown starts.
                float t = Mathf.Clamp01(animTime / OutSeconds);
                group.alpha = 1f - t;
                sheet.anchoredPosition = new Vector2(0f, t * t * SlideDistance);
                if (t >= 1f)
                {
                    leaving = false;
                    root.SetActive(false);
                }
                return;
            }

            // Slides up from below and overshoots a little (ease-out back) while the backdrop fades in.
            float k = Mathf.Clamp01(animTime / InSeconds) - 1f;
            float back = 1f + 2.70158f * k * k * k + 1.70158f * k * k;
            group.alpha = Mathf.Clamp01(animTime / InSeconds * 2f);
            sheet.anchoredPosition = new Vector2(0f, (back - 1f) * SlideDistance);

            float pulse = controller.AwaitingReady ? 1f + .04f * Mathf.Sin(animTime * 5f) : 1f;
            startBox.localScale = new Vector3(pulse, pulse, 1f);

            for (int i = 0; i < stampTime.Length; i++)
            {
                if (stampTime[i] < 0f) continue;
                stampTime[i] += dt;
                float t = Mathf.Clamp01(stampTime[i] / StampSeconds);
                float scale = Mathf.Lerp(2.2f, 1f, t);
                stamps[i].localScale = new Vector3(scale, scale, 1f);
                if (t >= 1f) stampTime[i] = -1f;
            }
        }

        // Ink tab on the top edge of a box, like the labels of the art bible.
        private TMP_Text Tab(Transform box)
        {
            var pill = PaperUI.Pill("Tab", box, PaperUI.Ink, 44f);
            PaperUI.Place(pill.rectTransform, TopLeft, new Vector2(24f, 22f), new Vector2(200f, 44f));
            var label = PaperUI.Text("Label", pill.transform, titleFont, 30f, PaperUI.Paper, TextAlignmentOptions.Center);
            PaperUI.Stretch(label.rectTransform);
            return label;
        }

        private void SetTab(TMP_Text label, string text)
        {
            SetDisplay(label, text);
            var tab = (RectTransform)label.transform.parent;
            tab.sizeDelta = new Vector2(label.GetPreferredValues(text).x + 40f, tab.sizeDelta.y);
        }

        // Bangers only has ASCII (no accents, ñ, ¡ or ¿): a text it cannot draw whole goes in the default
        // font instead of mixing glyphs. Regenerating its atlas with Latin-1 makes these use Bangers too.
        private void SetDisplay(TMP_Text label, string text)
        {
            label.font = Supports(titleFont, text) ? titleFont : TMP_Settings.defaultFontAsset;
            label.text = text;
        }

        private static bool Supports(TMP_FontAsset font, string text)
        {
            if (font == null) return false;
            foreach (char c in text)
                if (!char.IsWhiteSpace(c) && !font.HasCharacter(c)) return false;
            return true;
        }

        // Missing translation (Get returns the key): use the fallback, if any.
        private static string Localized(string key, string fallback)
        {
            string text = L10n.Get(L10n.Minigames, key);
            return text == key ? fallback ?? string.Empty : text;
        }

        private static void AutoSize(TMP_Text label, float min, float max)
        {
            label.enableAutoSizing = true;
            label.fontSizeMin = min;
            label.fontSizeMax = max;
        }

        // Like PaperUI.Place but turning around its centre (tilt, pop and pulse).
        private static void Center(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            PaperUI.Place(rect, anchor, position, size);
            rect.pivot = Middle;
        }
    }
}
