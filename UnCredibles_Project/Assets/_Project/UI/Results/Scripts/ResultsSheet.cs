using TMPro;
using UnityEngine;

namespace UnCredibles.UI.Results
{
    // Standings between rounds as a taped paper sheet (art bible, PaperUI kit): the title, a "Round N / 3"
    // tab, one PF_ResultsRow per player popping in one after another, and the hint in a paper pill.
    // Only shows what ResultsScreen hands it, so the online client sees the host's standings the same.
    // Lives in SCN_Results and starts inactive: ResultsScreen shows it between rounds only.
    public sealed class ResultsSheet : MonoBehaviour
    {
        private const float RowDelay = .12f;   // stagger between rows
        private const float PopSeconds = .3f;

        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text round;
        [SerializeField] private TMP_Text hint;
        [SerializeField] private ResultsRow[] rows;
        private float animTime = -1f; // -1 = rows settled

        public int RowCount => rows.Length;

        public void SetVisible(bool visible)
        {
            if (visible && !gameObject.activeSelf)
            {
                // The rows pop in each time the sheet appears; until their turn they are not drawn.
                animTime = 0f;
                for (int i = 0; i < rows.Length; i++) rows[i].transform.localScale = Vector3.zero;
            }
            gameObject.SetActive(visible);
        }

        public void SetHeader(string titleText, string roundText)
        {
            title.text = titleText;
            round.text = roundText;
        }

        public void SetHint(string text) => hint.text = text;

        public void SetRow(int index, int placement, string playerName, int slotIndex, bool isAI, int gained, int total) =>
            rows[index].Set(placement, playerName, slotIndex, isAI, gained, total);

        public void HideRow(int index) => rows[index].Hide();

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
                rows[i].transform.localScale = new Vector3(scale, scale, 1f);
            }
            if (animTime >= (rows.Length - 1) * RowDelay + PopSeconds) animTime = -1f;
        }
    }
}
