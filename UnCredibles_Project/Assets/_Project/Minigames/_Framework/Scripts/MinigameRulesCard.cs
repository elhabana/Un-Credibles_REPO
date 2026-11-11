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
    // Lives in PF_MinigameRulesCard; MinigameController binds it. Texts come from the Minigames
    // table: "name.<id>", "rules.<id>" and "phone.<id>" (controls, "Key: action · Key: action");
    // the MinigameData fields are the fallback.
    public sealed class MinigameRulesCard : MonoBehaviour
    {
        private const float Margin = 50f;
        private const float LeftWidth = 860f;   // title, mission, controls
        private const float RightWidth = 600f;  // media, players, START
        // Gameplay video or thumbnail, inside the media frame (16:9).
        private const float MediaWidth = 560f;
        private const float MediaHeight = 315f;
        private const float KeyHeight = 46f;
        private const float KeyInset = 28f;
        private const float DiscSpacing = 145f;
        private const float InSeconds = .45f;
        private const float OutSeconds = .3f;
        private const float StampSeconds = .15f;
        private const float SlideDistance = 700f;

        [SerializeField, Tooltip("Title font (Bangers); texts it cannot draw fall back to the default font.")]
        private TMP_FontAsset titleFont;
        [SerializeField] private CanvasGroup group;
        [SerializeField] private RectTransform sheet;
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text missionTab;
        [SerializeField] private TMP_Text rules;
        [SerializeField] private GameObject controlsBox;
        [SerializeField] private TMP_Text controlsTab;
        [SerializeField, Tooltip("One row per control: ink key button, its label and the action.")]
        private Image[] keys;
        [SerializeField] private TMP_Text[] keyLabels;
        [SerializeField] private TMP_Text[] actions;
        [SerializeField] private Image thumbnail;
        [SerializeField] private GameObject placeholder;
        [SerializeField] private TMP_Text placeholderName;
        [SerializeField, Tooltip("Inactive until the minigame has a preview clip.")]
        private RawImage video;
        [SerializeField] private VideoPlayer videoPlayer;
        [SerializeField, Tooltip("One disc per player slot, with its tag, name and READY stamp.")]
        private Image[] discs;
        [SerializeField] private TMP_Text[] tags;
        [SerializeField] private TMP_Text[] names;
        [SerializeField] private RectTransform[] stamps;
        [SerializeField] private TMP_Text[] stampLabels;
        [SerializeField] private RectTransform startBox;
        [SerializeField] private TMP_Text startLabel;
        [SerializeField] private TMP_Text pressLabel;

        private MinigameController controller;
        private RenderTexture videoTexture;
        private readonly bool[] wasReady = new bool[PlayerRegistry.MaxPlayers];
        private readonly float[] stampTime = new float[PlayerRegistry.MaxPlayers];
        private float animTime;
        private bool shown;
        private bool leaving;

        public void Setup(MinigameController game)
        {
            controller = game;
            SetupVideo(controller.Data != null ? controller.Data.Preview : null);

            for (int i = 0; i < stampTime.Length; i++) stampTime[i] = -1f;
            controller.ReadyChanged += Refresh;
            controller.StateChanged += HandleStateChanged;
            L10n.LanguageChanged += Refresh;
            Refresh();
        }

        // Gameplay loop: muted, looping, drawn into a texture the card shows. Only when there is a clip.
        private void SetupVideo(VideoClip clip)
        {
            if (clip == null) return;
            videoTexture = new RenderTexture((int)clip.width, (int)clip.height, 0);
            video.texture = videoTexture;
            // Fit the clip inside the media box, keeping its proportions.
            float scale = Mathf.Min(MediaWidth / clip.width, MediaHeight / clip.height);
            video.rectTransform.sizeDelta = new Vector2(clip.width * scale, clip.height * scale);
            videoPlayer.targetTexture = videoTexture;
            videoPlayer.clip = clip;
            video.gameObject.SetActive(true);
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
            bool hasVideo = videoTexture != null;
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
                discs[i].rectTransform.anchoredPosition =
                    new Vector2(-Margin - RightWidth * .5f + (i - (count - 1) * .5f) * DiscSpacing, -470f);
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
                    if (row == keys.Length) break;
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
            for (int i = row; i < keys.Length; i++)
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
            gameObject.SetActive(true);
        }

        private void Hide()
        {
            if (!shown)
            {
                if (!leaving) gameObject.SetActive(false);
                return;
            }
            shown = false;
            leaving = true;
            animTime = 0f;
        }

        private void Update()
        {
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
                    gameObject.SetActive(false);
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

        // The ink tab on the top edge of a box grows with its label.
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
    }
}
