using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UnCredibles.UI.Results
{
    // One line of the standings: position, player, points won this round and match total.
    public sealed class ResultRowView : MonoBehaviour
    {
        [SerializeField] private Image background;
        [SerializeField] private TMP_Text placementText;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text gainedText;
        [SerializeField] private TMP_Text totalText;
        [SerializeField] private Color normalColor = new Color(0.14f, 0.16f, 0.24f, 0.95f);
        [SerializeField] private Color leaderColor = new Color(0.75f, 0.58f, 0.12f, 1f);

        public void Render(int placement, string playerName, int gained, int total, bool highlight)
        {
            placementText.text = placement.ToString();
            nameText.text = playerName;
            gainedText.text = gained > 0 ? $"+{gained}" : string.Empty;
            totalText.text = total.ToString();
            background.color = highlight ? leaderColor : normalColor;
            gameObject.SetActive(true);
        }

        public void Hide() => gameObject.SetActive(false);
    }
}
