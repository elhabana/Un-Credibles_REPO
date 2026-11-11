using TMPro;
using UnCredibles.Localization;
using UnCredibles.Minigames;
using UnCredibles.Players;
using UnityEngine;
using UnityEngine.UI;

namespace UnCredibles.UI.Results
{
    // One paper strip of the standings sheet (PF_ResultsRow): place on a gold, silver or bronze medal, slot
    // disc, name, the round's points as a stamp and the big total, which counts up from the previous one.
    public sealed class ResultsRow : MonoBehaviour
    {
        private static readonly Color LeaderPaper = new Color(1f, .96f, .82f);
        // The gold of the 1st is the colour baked into the prefab.
        private static readonly Color Silver = new Color(.78f, .8f, .84f);
        private static readonly Color Bronze = new Color(.85f, .55f, .3f);
        private const float CountDelay = .7f;   // every row has popped in by then
        private const float CountSeconds = .6f;

        [SerializeField] private Image panel;
        [SerializeField] private GameObject medal;
        [SerializeField] private TMP_Text place;
        [SerializeField] private Image disc;
        [SerializeField] private TMP_Text slotTag;
        [SerializeField] private TMP_Text playerName;
        [SerializeField] private GameObject stamp;
        [SerializeField] private TMP_Text gained;
        [SerializeField] private TMP_Text total;

        private Image medalImage;
        private RawImage picture;
        private Color gold;
        private int countFrom;
        private int countTo = -1;
        private float countTime = -1f; // -1 = total settled

        // portrait: the player's character for the disc; null = the slot tag ("J1" / "BOT").
        public void Set(int placement, string playerNameText, int slotIndex, bool isAI, int gainedPoints, int totalPoints,
            Texture portrait)
        {
            bool leader = placement == 1;
            var color = PlayerIdentity.ColorFor(slotIndex, isAI);
            panel.color = leader ? LeaderPaper : Color.Lerp(Color.white, color, .1f);
            if (medalImage == null)
            {
                medalImage = medal.GetComponent<Image>();
                gold = medalImage.color;
            }
            medal.SetActive(placement <= 3);
            medalImage.color = leader ? gold : placement == 2 ? Silver : Bronze;
            string key = "podium.place_" + placement;
            string placeText = L10n.Get(L10n.UI, key);
            place.text = placeText == key ? placement.ToString() : placeText;
            disc.color = color;
            slotTag.text = PlayerIdentity.LabelFor(slotIndex, isAI);
            slotTag.gameObject.SetActive(portrait == null);
            if (portrait != null && picture == null) picture = PlayerBadge.ShowPortrait(disc, portrait);
            if (picture != null)
            {
                picture.texture = portrait;
                picture.transform.parent.gameObject.SetActive(portrait != null); // the round cut holding it
            }
            playerName.text = playerNameText;
            stamp.SetActive(gainedPoints > 0);
            gained.SetText("+{0}", gainedPoints);
            // A new total counts up from what the player had; a repeated render (language change) does not.
            if (totalPoints != countTo)
            {
                countFrom = totalPoints - gainedPoints;
                countTo = totalPoints;
                countTime = gainedPoints > 0 ? 0f : -1f;
            }
            total.SetText("{0}", countTime < 0f ? countTo : countFrom);
            gameObject.SetActive(true);
        }

        private void Update()
        {
            if (countTime < 0f) return;
            countTime += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01((countTime - CountDelay) / CountSeconds);
            total.SetText("{0}", Mathf.RoundToInt(Mathf.Lerp(countFrom, countTo, t)));
            // The round's stamp punches while its points go into the total.
            float punch = 1f + .25f * Mathf.Sin(t * Mathf.PI);
            stamp.transform.localScale = new Vector3(punch, punch, 1f);
            if (t >= 1f) countTime = -1f;
        }

        public void Hide() => gameObject.SetActive(false);
    }
}
