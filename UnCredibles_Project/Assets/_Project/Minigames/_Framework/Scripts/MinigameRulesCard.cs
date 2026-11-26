using TMPro;
using UnCredibles.Localization;
using UnCredibles.Players;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.Video;

namespace UnCredibles.Minigames
{
    // "Mission briefing" shown before every minigame (art bible, p. 26): a tilted paper sheet with the
    // name, a disc per player that gets a READY stamp when they press Jump, and the START box.
    // Two layouts:
    //  - Mechanics (MinigameData.Mechanics not empty): one page per mechanic, its clip big on the left and,
    //    where the video used to be, what it does and its controls (gamepad glyph / keyboard key).
    //    Any player browses with the stick sideways; untouched, the pages turn by themselves.
    //  - Classic: the rules text, the gameplay video (or thumbnail) and the controls as ink buttons.
    // The countdown waits until every connected human is ready (see MinigameController.AwaitingReady);
    // this card only shows that state, so it works unchanged on online replicas (each screen browses on its own).
    // Lives in PF_MinigameRulesCard; MinigameController binds it. Texts come from the Minigames table:
    // "name.<id>", "rules.<id>", "controls.<id>" ("Key: action · Key: action"; falls back to "phone.<id>") and,
    // per mechanic, "mech.<id>.<key>", ".text" and ".controls" ("Move: ... · Jump: ..."). MinigameData is the fallback.
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

        // Mechanics pages.
        private const float PageSeconds = 6.5f;   // untouched, the next page comes after this
        private const float StickOn = .6f;        // a stick pushed this far sideways turns one page...
        private const float StickOff = .3f;       // ...and has to come back this close to the middle first
        private const float DotSpacing = 34f;
        private const float GlyphSize = 52f;
        private const float RowInset = 24f;
        private const float PanelWidth = 600f;
        private static readonly Color PadA = new Color(.36f, .62f, .25f);

        [SerializeField, Tooltip("Title font (Bangers); texts it cannot draw fall back to the default font.")]
        private TMP_FontAsset titleFont;
        [SerializeField] private CanvasGroup group;
        [SerializeField] private RectTransform sheet;
        [SerializeField] private TMP_Text title;
        [SerializeField] private GameObject missionBox;
        [SerializeField] private TMP_Text missionTab;
        [SerializeField] private TMP_Text rules;
        [SerializeField] private GameObject controlsBox;
        [SerializeField] private TMP_Text controlsTab;
        [SerializeField, Tooltip("One row per control: ink key button, its label and the action.")]
        private Image[] keys;
        [SerializeField] private TMP_Text[] keyLabels;
        [SerializeField] private TMP_Text[] actions;
        [SerializeField] private GameObject mediaBox;
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

        [Header("Mechanics pages")]
        [SerializeField] private GameObject mechanicFrame;
        [SerializeField] private RawImage mechanicVideo;
        [SerializeField] private VideoPlayer mechanicPlayer;
        [SerializeField] private GameObject mechanicPlaceholder;
        [SerializeField] private TMP_Text mechanicPlaceholderName;
        [SerializeField] private RectTransform previousArrow;
        [SerializeField] private RectTransform nextArrow;
        [SerializeField] private RectTransform pageDotsRoot;
        [SerializeField] private Image[] pageDots;
        [SerializeField] private TMP_Text browseHint;
        [SerializeField] private RectTransform mechanicPanel;
        [SerializeField] private TMP_Text mechanicTab;
        [SerializeField] private TMP_Text mechanicStep;
        [SerializeField] private TMP_Text mechanicText;
        [SerializeField, Tooltip("Per control row: gamepad glyph (disc), its letter and stick knob, '/', keyboard key pill, its label and the action.")]
        private Image[] padGlyphs;
        [SerializeField] private TMP_Text[] padLabels;
        [SerializeField] private GameObject[] padKnobs;
        [SerializeField] private TMP_Text[] slashes;
        [SerializeField] private Image[] keyPills;
        [SerializeField] private TMP_Text[] keyPillLabels;
        [SerializeField] private TMP_Text[] mechanicActions;

        private MinigameController controller;
        private RenderTexture videoTexture;
        private VideoPlayer[] pagePlayers = System.Array.Empty<VideoPlayer>();
        private RenderTexture[] pageTextures;
        private readonly bool[] wasReady = new bool[PlayerRegistry.MaxPlayers];
        private readonly float[] stampTime = new float[PlayerRegistry.MaxPlayers];
        private readonly bool[] stickHeld = new bool[PlayerRegistry.MaxPlayers];
        private float animTime;
        private bool shown;
        private bool leaving;
        private int page;
        private int shownPage = -1;
        private float pageTimer;
        private float pagePop;
        private float arrowPop;
        private int arrowSide;

        private int MechanicCount => controller != null && controller.Data != null ? controller.Data.Mechanics.Count : 0;

        public void Setup(MinigameController game)
        {
            controller = game;
            if (MechanicCount > 0) SetupMechanics();
            else SetupVideo(controller.Data != null ? controller.Data.Preview : null);

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
            videoPlayer.skipOnDrop = true;
            videoPlayer.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
            videoPlayer.clip = clip;
            videoPlayer.Prepare();
            video.gameObject.SetActive(true);
        }

        // One player and texture per page, all prepared now: turning a page only swaps the texture shown,
        // instead of loading a clip (which froze the frame for a moment). The prefab's player is page 0.
        private void SetupMechanics()
        {
            var mechanics = controller.Data.Mechanics;
            pagePlayers = new VideoPlayer[mechanics.Count];
            pageTextures = new RenderTexture[mechanics.Count];
            for (int i = 0; i < mechanics.Count; i++)
            {
                var clip = mechanics[i].clip;
                if (clip == null) continue;
                var player = i == 0 ? mechanicPlayer : mechanicPlayer.gameObject.AddComponent<VideoPlayer>();
                player.playOnAwake = false;
                player.isLooping = true;
                player.skipOnDrop = true;
                player.waitForFirstFrame = true;
                player.audioOutputMode = VideoAudioOutputMode.None;
                // Real time: the card also runs while something slows or pauses the game.
                player.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
                player.renderMode = VideoRenderMode.RenderTexture;
                pageTextures[i] = new RenderTexture((int)clip.width, (int)clip.height, 0);
                player.targetTexture = pageTextures[i];
                player.clip = clip;
                pagePlayers[i] = player; // prepared when the card opens (an inactive object cannot)
            }
        }

        private void OnDestroy()
        {
            Release(videoTexture);
            if (pageTextures != null)
                foreach (var texture in pageTextures) Release(texture);
            L10n.LanguageChanged -= Refresh;
            if (controller == null) return;
            controller.ReadyChanged -= Refresh;
            controller.StateChanged -= HandleStateChanged;
        }

        private static void Release(RenderTexture texture)
        {
            if (texture == null) return;
            texture.Release();
            Destroy(texture);
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
            SetDisplay(startLabel, L10n.Get(L10n.UI, "rules.start"));
            SetDisplay(pressLabel, L10n.Get(L10n.UI, "rules.press"));

            bool paged = MechanicCount > 0;
            if (paged && opening) PrepareClips();
            missionBox.SetActive(!paged);
            mediaBox.SetActive(!paged);
            if (paged) controlsBox.SetActive(false); // the classic one; ShowControls decides it otherwise
            mechanicFrame.SetActive(paged);
            mechanicPanel.gameObject.SetActive(paged);
            pageDotsRoot.gameObject.SetActive(paged);
            browseHint.gameObject.SetActive(paged && MechanicCount > 1);
            if (paged) ShowPage();
            else ShowClassic(data, id, gameName);

            RefreshPlayers(opening);
        }

        private void ShowClassic(MinigameData data, string id, string gameName)
        {
            SetDisplay(placeholderName, gameName);
            rules.text = Localized("rules." + id, data != null ? data.Rules : string.Empty);
            SetTab(missionTab, L10n.Get(L10n.UI, "rules.mission"));
            SetTab(controlsTab, L10n.Get(L10n.UI, "rules.controls"));
            ShowControls(Localized("controls." + id, Localized("phone." + id, string.Empty)));

            // The video wins over the thumbnail; without either, the placeholder fills the frame.
            bool hasVideo = videoTexture != null;
            if (hasVideo && !videoPlayer.isPlaying) videoPlayer.Play();
            bool hasThumbnail = !hasVideo && data != null && data.Thumbnail != null;
            thumbnail.gameObject.SetActive(hasThumbnail);
            if (hasThumbnail) thumbnail.sprite = data.Thumbnail;
            placeholder.SetActive(!hasVideo && !hasThumbnail);
        }

        private void RefreshPlayers(bool opening)
        {
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

        // ---------- Mechanics pages ----------

        private void ShowPage()
        {
            var data = controller.Data;
            int count = MechanicCount;
            page = ((page % count) + count) % count;
            var mechanic = data.Mechanics[page];
            string key = "mech." + data.Id + "." + mechanic.key;
            string pageTitle = Localized(key, mechanic.key);

            SetTab(mechanicTab, pageTitle);
            SetDisplay(mechanicStep, count > 1 ? $"{page + 1}/{count}" : string.Empty);
            mechanicText.text = Localized(key + ".text", string.Empty);
            ShowMechanicControls(Localized(key + ".controls", string.Empty));
            SetDisplay(browseHint, L10n.Get(L10n.UI, "rules.browse"));

            previousArrow.gameObject.SetActive(count > 1);
            nextArrow.gameObject.SetActive(count > 1);
            for (int i = 0; i < pageDots.Length; i++)
            {
                bool used = i < count && count > 1;
                pageDots[i].gameObject.SetActive(used);
                if (!used) continue;
                pageDots[i].rectTransform.anchoredPosition = new Vector2((i - (count - 1) * .5f) * DotSpacing, 0f);
                bool current = i == page;
                pageDots[i].color = current ? PaperUI.Ink : new Color(PaperUI.Ink.r, PaperUI.Ink.g, PaperUI.Ink.b, .25f);
                pageDots[i].rectTransform.localScale = Vector3.one * (current ? 1.4f : 1f);
            }

            if (shownPage == page) return;
            shownPage = page;
            pagePop = 1f;
            SetDisplay(mechanicPlaceholderName, pageTitle);
            // Only the shown page plays, from its start; the others wait paused and ready.
            for (int i = 0; i < pagePlayers.Length; i++)
            {
                var player = pagePlayers[i];
                if (player == null) continue;
                if (i != page)
                {
                    if (player.isPlaying) player.Pause();
                    continue;
                }
                player.time = 0;
                player.Play();
                mechanicVideo.texture = pageTextures[i];
            }
            ShowClipWhenReady();
        }

        // The dotted placeholder stays until the page's clip has its first frame (or when it has no clip).
        // Only the image hides: the players live on its object, which must stay active to prepare and play.
        private void ShowClipWhenReady()
        {
            var player = page < pagePlayers.Length ? pagePlayers[page] : null;
            bool ready = player != null && player.isPrepared && player.frame >= 0;
            mechanicVideo.gameObject.SetActive(true);
            mechanicVideo.enabled = ready;
            mechanicPlaceholder.SetActive(!ready);
        }

        // Every page decodes ahead, so turning to it is instant.
        private void PrepareClips()
        {
            mechanicVideo.gameObject.SetActive(true);
            mechanicVideo.enabled = false;
            foreach (var player in pagePlayers)
                if (player != null && !player.isPrepared) player.Prepare();
        }

        private void Turn(int step)
        {
            page += step;
            pageTimer = 0f;
            ShowPage();
        }

        // Any player's stick sideways (one page per push). Online clients' slots only hold placeholders,
        // so a replica also listens to this machine's own keyboard and gamepads.
        private int ReadBrowse()
        {
            int step = 0;
            var players = controller.Players;
            for (int i = 0; i < players.Count && i < stickHeld.Length; i++)
            {
                var input = players[i].Input;
                if (players[i].IsAI || input == null) continue;
                float x = input.Move.x;
                if (!stickHeld[i] && Mathf.Abs(x) >= StickOn)
                {
                    stickHeld[i] = true;
                    step = x > 0f ? 1 : -1;
                }
                else if (Mathf.Abs(x) <= StickOff) stickHeld[i] = false;
            }
            if (step != 0 || !controller.IsReplica) return step;

            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.aKey.wasPressedThisFrame || keyboard.leftArrowKey.wasPressedThisFrame) return -1;
                if (keyboard.dKey.wasPressedThisFrame || keyboard.rightArrowKey.wasPressedThisFrame) return 1;
            }
            foreach (var pad in Gamepad.all)
            {
                if (pad.dpad.left.wasPressedThisFrame || pad.leftStick.left.wasPressedThisFrame) return -1;
                if (pad.dpad.right.wasPressedThisFrame || pad.leftStick.right.wasPressedThisFrame) return 1;
            }
            return 0;
        }

        // "Move: drive · Jump: turbo" -> one row each: gamepad glyph, "/", keyboard key and the action.
        private void ShowMechanicControls(string text)
        {
            int row = 0;
            if (!string.IsNullOrEmpty(text))
            {
                foreach (string part in text.Split('·'))
                {
                    if (row == padGlyphs.Length) break;
                    if (!SplitControl(part, out string glyph, out string action)) continue;

                    string pad = glyph switch { "jump" => "A", "crouch" => "B", _ => string.Empty };
                    bool known = glyph == "move" || pad.Length > 0;
                    padGlyphs[row].gameObject.SetActive(known);
                    slashes[row].gameObject.SetActive(known);
                    keyPills[row].gameObject.SetActive(known);
                    float x = RowInset;
                    if (known)
                    {
                        padGlyphs[row].color = glyph == "jump" ? PadA : glyph == "crouch" ? PaperUI.Alarm : PaperUI.Ink;
                        padKnobs[row].SetActive(glyph == "move");
                        SetDisplay(padLabels[row], pad);
                        string key = L10n.Get(L10n.UI, "rules.key." + glyph);
                        SetDisplay(keyPillLabels[row], key);
                        float width = Mathf.Max(KeyHeight, keyPillLabels[row].GetPreferredValues(key).x + 28f);
                        keyPills[row].rectTransform.sizeDelta = new Vector2(width, KeyHeight);
                        x = ((RectTransform)keyPills[row].transform).anchoredPosition.x + width + 16f;
                    }
                    var rect = mechanicActions[row].rectTransform;
                    rect.anchoredPosition = new Vector2(x, rect.anchoredPosition.y);
                    rect.sizeDelta = new Vector2(PanelWidth - x - RowInset, GlyphSize);
                    mechanicActions[row].text = action;
                    mechanicActions[row].gameObject.SetActive(true);
                    row++;
                }
            }
            for (int i = row; i < padGlyphs.Length; i++)
            {
                padGlyphs[i].gameObject.SetActive(false);
                slashes[i].gameObject.SetActive(false);
                keyPills[i].gameObject.SetActive(false);
                mechanicActions[i].gameObject.SetActive(false);
            }
        }

        // "Jump: Shove" -> glyph "jump", action "Shove". A part without a colon is all action. False if empty.
        public static bool SplitControl(string part, out string glyph, out string action)
        {
            string entry = part != null ? part.Trim() : string.Empty;
            int colon = entry.IndexOf(':');
            glyph = colon > 0 ? entry.Substring(0, colon).Trim().ToLowerInvariant() : string.Empty;
            action = colon > 0 ? entry.Substring(colon + 1).Trim() : entry;
            if (action.Length > 0) action = char.ToUpper(action[0]) + action.Substring(1);
            return entry.Length > 0;
        }

        // ---------- Classic ----------

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
            controlsBox.SetActive(row > 0 && MechanicCount == 0);
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
            foreach (var player in pagePlayers)
                if (player != null && player.isPlaying) player.Pause();
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

            if (shown && MechanicCount > 0) ShowClipWhenReady();
            if (shown && MechanicCount > 1) TickPages(dt);
        }

        private void TickPages(float dt)
        {
            int step = ReadBrowse();
            if (step != 0)
            {
                arrowSide = step;
                arrowPop = 1f;
                GameAudio.Play(GameSfx.UiMove);
                Turn(step);
            }
            // Capped: the first frame after a scene load can be seconds long and would skip a page at once.
            else if ((pageTimer += Mathf.Min(dt, .1f)) >= PageSeconds) Turn(1);

            // The text pops on a new page; the arrow that was pushed kicks outwards; both arrows breathe.
            pagePop = Mathf.Max(0f, pagePop - dt * 6f);
            mechanicPanel.localScale = Vector3.one * (1f + .05f * pagePop);
            arrowPop = Mathf.Max(0f, arrowPop - dt * 5f);
            float breathe = 1f + .06f * Mathf.Sin(animTime * 4f);
            previousArrow.localScale = Vector3.one * (breathe + (arrowSide < 0 ? .35f * arrowPop : 0f));
            nextArrow.localScale = Vector3.one * (breathe + (arrowSide > 0 ? .35f * arrowPop : 0f));
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
