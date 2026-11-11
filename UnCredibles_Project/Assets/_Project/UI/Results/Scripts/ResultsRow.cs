using TMPro;
using UnCredibles.Localization;
using UnCredibles.Players;
using UnityEngine;
using UnityEngine.UI;

namespace UnCredibles.UI.Results
{
    // One paper strip of the standings sheet (PF_ResultsRow): place with a medal for the leader, slot disc,
    // name, the round's points as a stamp and the big total.
    public sealed class ResultsRow : MonoBehaviour
    {
        private static readonly Color LeaderPaper = new Color(1f, .96f, .82f);

        [SerializeField] private Image panel;
        [SerializeField] private GameObject medal;
        [SerializeField] private TMP_Text place;
        [SerializeField] private Image disc;
        [SerializeField] private TMP_Text slotTag;
        [SerializeField] private TMP_Text playerName;
        [SerializeField] private GameObject stamp;
        [SerializeField] private TMP_Text gained;
        [SerializeField] private TMP_Text total;

        public void Set(int placement, string playerNameText, int slotIndex, bool isAI, int gainedPoints, int totalPoints)
        {
            bool leader = placement == 1;
            panel.color = leader ? LeaderPaper : Color.white;
            medal.SetActive(leader);
            string key = "podium.place_" + placement;
            string placeText = L10n.Get(L10n.UI, key);
            place.text = placeText == key ? placement.ToString() : placeText;
            disc.color = PlayerIdentity.ColorFor(slotIndex, isAI);
            slotTag.text = PlayerIdentity.LabelFor(slotIndex, isAI);
            playerName.text = playerNameText;
            stamp.SetActive(gainedPoints > 0);
            gained.SetText("+{0}", gainedPoints);
            total.SetText("{0}", totalPoints);
            gameObject.SetActive(true);
        }

        public void Hide() => gameObject.SetActive(false);
    }
}
