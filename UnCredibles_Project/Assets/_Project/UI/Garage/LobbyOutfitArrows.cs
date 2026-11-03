using TMPro;
using UnCredibles.Players;
using UnCredibles.UI.PartyLobby;
using UnityEngine;
using UnityEngine.UI;

namespace UnCredibles.UI.Garage
{
    // Arrows next to each lobby figure, at the part they change: the head (helmet), the chest
    // (outfit) and the feet (boots). They follow the figure on screen; the row a player has
    // picked with the stick is highlighted.
    public sealed class LobbyOutfitArrows : MonoBehaviour
    {
        private static readonly Color Ink = new Color(.06f, .08f, .12f, .88f);
        private static readonly Color Highlight = new Color(.95f, .65f, .2f, 1f);
        private static readonly CharacterPartSlot[] Parts = { CharacterPartSlot.Helmet, CharacterPartSlot.Outfit, CharacterPartSlot.Boots };
        // Distance from the middle of each part to its arrows (character 1 unit tall) and extra
        // pixels. The figures stand close together, so the head arrows sit on the head itself and
        // the others right at the edge of the chest and the feet: each pair stays on its figure.
        private static readonly float[] Offsets = { .13f, .11f, .1f };
        private static readonly float[] Gaps = { 0f, 8f, 8f };
        private const float HeadArrowHeight = .7f; // from the base of the skull to the top, where helmets are
        private const float ArrowSize = 38f;

        private PartyLobbyController lobby;
        private GameObject[] avatars;
        private PlayerCharacterOutfit[] outfits;
        private Transform[,] anchorsA;
        private Transform[,] anchorsB;
        private Button[,,] arrows; // figure, part, side (0 left, 1 right)
        private RectTransform area;
        private Canvas canvas;

        public void Init(PartyLobbyController lobbyController, GameObject[] figures, PlayerCharacterOutfit[] figureOutfits)
        {
            lobby = lobbyController;
            avatars = figures;
            outfits = figureOutfits;
            area = (RectTransform)transform;
            canvas = GetComponentInParent<Canvas>().rootCanvas;
            anchorsA = new Transform[avatars.Length, Parts.Length];
            anchorsB = new Transform[avatars.Length, Parts.Length];
            arrows = new Button[avatars.Length, Parts.Length, 2];
            for (int i = 0; i < avatars.Length; i++)
            {
                if (outfits[i] != null)
                {
                    var bones = outfits[i].GetComponentsInChildren<Transform>(true);
                    // Head: between the base of the skull and the top of the head.
                    anchorsA[i, 0] = Bone(bones, "spine.006"); anchorsB[i, 0] = Bone(bones, "Socket_Cabeza");
                    anchorsA[i, 1] = Bone(bones, "spine.002"); anchorsB[i, 1] = anchorsA[i, 1];
                    anchorsA[i, 2] = Bone(bones, "foot.L"); anchorsB[i, 2] = Bone(bones, "foot.R");
                }
                for (int p = 0; p < Parts.Length; p++)
                    for (int side = 0; side < 2; side++)
                    {
                        int figure = i, part = p, step = side == 0 ? -1 : 1;
                        arrows[i, p, side] = Arrow(side == 0 ? "<" : ">", () => lobby.StepPart(figure, Parts[part], step));
                    }
            }
        }

        private static Transform Bone(Transform[] bones, string name)
        {
            foreach (var bone in bones)
                if (bone.name == name) return bone;
            return null;
        }

        private Button Arrow(string text, System.Action onClick)
        {
            var rect = new GameObject("Outfit arrow " + text, typeof(RectTransform), typeof(Image), typeof(Button)).GetComponent<RectTransform>();
            rect.SetParent(area, false);
            rect.sizeDelta = new Vector2(ArrowSize, ArrowSize);
            rect.GetComponent<Image>().color = Ink;
            var button = rect.GetComponent<Button>();
            button.navigation = new Navigation { mode = Navigation.Mode.None }; // players use their own pads
            button.onClick.AddListener(() => onClick());
            var label = new GameObject("Text", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            label.rectTransform.SetParent(rect, false);
            label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
            label.font = TMP_Settings.defaultFontAsset;
            label.text = text;
            label.fontSize = 26f;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.raycastTarget = false;
            rect.gameObject.SetActive(false);
            return button;
        }

        private void LateUpdate()
        {
            if (lobby == null) return;
            var view = Camera.main;
            for (int i = 0; i < avatars.Length; i++)
            {
                bool show = view != null && lobby.Players != null && avatars[i] != null && avatars[i].activeInHierarchy
                    && outfits[i] != null && lobby.CanCustomize(i);
                int selected = show ? lobby.SelectedPartRow(i) : -1;
                for (int p = 0; p < Parts.Length; p++)
                {
                    bool placed = show && Place(view, i, p);
                    for (int side = 0; side < 2; side++)
                    {
                        var arrow = arrows[i, p, side];
                        if (arrow.gameObject.activeSelf != placed) arrow.gameObject.SetActive(placed);
                        if (placed) arrow.image.color = p == selected ? Highlight : Ink;
                    }
                }
            }
        }

        // Puts both arrows of a part beside it on screen; false when it cannot be seen.
        private bool Place(Camera view, int figure, int part)
        {
            var a = anchorsA[figure, part];
            var b = anchorsB[figure, part];
            if (a == null || b == null) return false;
            var point = part == 0 ? Vector3.Lerp(a.position, b.position, HeadArrowHeight) : (a.position + b.position) * .5f;
            float halfWidth = Offsets[part] * outfits[figure].transform.lossyScale.y;

            var centre = view.WorldToScreenPoint(point);
            var edge = view.WorldToScreenPoint(point + view.transform.right * halfWidth);
            if (centre.z <= 0f) return false;
            var uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(area, centre, uiCamera, out var local);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(area, edge, uiCamera, out var localEdge);
            float offset = Mathf.Abs(localEdge.x - local.x) + Gaps[part];
            ((RectTransform)arrows[figure, part, 0].transform).anchoredPosition = AnchoredFromLocal(local + Vector2.left * offset);
            ((RectTransform)arrows[figure, part, 1].transform).anchoredPosition = AnchoredFromLocal(local + Vector2.right * offset);
            return true;
        }

        // Arrows are anchored at the centre of the area; local points are relative to its pivot.
        private Vector2 AnchoredFromLocal(Vector2 local)
        {
            var rect = area.rect;
            var pivotToCentre = new Vector2((.5f - area.pivot.x) * rect.width, (.5f - area.pivot.y) * rect.height);
            return local - pivotToCentre;
        }
    }
}
