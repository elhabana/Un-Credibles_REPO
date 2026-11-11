using System;
using TMPro;
using UnCredibles.Players;
using UnityEngine;
using UnityEngine.UI;

namespace UnCredibles.UI.Voting
{
    // One polaroid of the vote screen (PF_VoteCard): white frame, the thumbnail as the photo, the name on the
    // bottom margin, a disc per player and the "EN LA RUTA" stamp. MapVoteView fills it and moves the discs.
    public sealed class VoteCard : MonoBehaviour
    {
        // Polaroid, top to bottom: inset, photo (16:9), name, status, player discs, wider bottom margin.
        private const float PhotoInset = 14f;
        private const float NameHeight = 52f;
        private const float StatusHeight = 30f;
        private const float BottomMargin = 34f;   // room for the vote stamp under the discs
        internal const float BadgeSize = 56f;

        [Serializable]
        internal sealed class Badge
        {
            public Image disc;
            public TMP_Text label;
            public RectTransform stamp;
            public TMP_Text stampLabel;
            [NonSerialized] public bool voted;
            [NonSerialized] public float stampTime = -1f;
        }

        [SerializeField] internal Image background;
        [SerializeField] internal Button button;
        [SerializeField] internal Image thumbnail;
        [SerializeField] internal TMP_Text title;
        [SerializeField] internal TMP_Text footer;
        [SerializeField] internal Badge[] badges = new Badge[PlayerRegistry.MaxPlayers];
        [SerializeField] internal GameObject inRoute;
        [SerializeField] internal RectTransform strike;
        [SerializeField] internal RectTransform routeStamp;
        [SerializeField] internal TMP_Text routeLabel;

        // Centre of the disc row, from the card's top edge.
        internal float BadgeY { get; private set; }

        // The width follows the number of candidates; the height fits the content, so the rows hang from the
        // top edge (y = distance from it to each row's centre). Returns the card's height.
        internal float Layout(float width)
        {
            float photoWidth = width - PhotoInset * 2f;
            float photoHeight = photoWidth * 9f / 16f;
            float nameY = PhotoInset + photoHeight + NameHeight * .5f;
            float statusY = nameY + (NameHeight + StatusHeight) * .5f;
            BadgeY = statusY + StatusHeight * .5f + 4f + BadgeSize * .5f;
            float height = BadgeY + BadgeSize * .5f + BottomMargin;

            background.rectTransform.sizeDelta = new Vector2(width, height);
            FromTop(thumbnail.rectTransform, PhotoInset + photoHeight * .5f, photoWidth, photoHeight);
            FromTop(title.rectTransform, nameY, width - 30f, NameHeight);
            FromTop(footer.rectTransform, statusY, width - 30f, StatusHeight);
            for (int i = 0; i < badges.Length; i++) FromTop(badges[i].disc.rectTransform, BadgeY, BadgeSize, BadgeSize);
            strike.sizeDelta = new Vector2(Mathf.Sqrt(width * width + height * height) - 20f, 10f);
            strike.localEulerAngles = new Vector3(0f, 0f, Mathf.Atan2(height, width) * Mathf.Rad2Deg);
            routeStamp.sizeDelta = new Vector2(width - 20f, routeStamp.sizeDelta.y);
            return height;
        }

        // Anchored to the top edge, pivot in the middle (set up in the prefab).
        private static void FromTop(RectTransform rect, float y, float width, float height)
        {
            rect.anchoredPosition = new Vector2(0f, -y);
            rect.sizeDelta = new Vector2(width, height);
        }
    }
}
