using TMPro;
using UnCredibles.Players;
using UnCredibles.UI.PartyLobby;
using UnityEngine;
using UnityEngine.UI;

namespace UnCredibles.UI.Garage
{
    // White arrows beside each lobby figure, at the part they change: the head (helmet), the
    // body (outfit) and the feet (boots). They follow the figure on screen; the row a player
    // has picked with the stick pulses and glows.
    public sealed class LobbyOutfitArrows : MonoBehaviour
    {
        private static readonly CharacterPartSlot[] Parts = { CharacterPartSlot.Helmet, CharacterPartSlot.Outfit, CharacterPartSlot.Boots };
        // Distance from the middle of each part to its arrows (character 1 unit tall): just
        // outside the head, the arms and the feet. Plus a few pixels of air.
        private static readonly float[] Offsets = { .25f, .24f, .14f }; // the head one clears helmet brims too
        private const float Gap = 12f;
        private const float ArrowSize = 44f;
        private static readonly Color Idle = new Color(1f, 1f, 1f, .5f);
        private static readonly Color Lit = new Color(1f, .93f, .62f, 1f);
        private static readonly Color GlowColor = new Color(1f, .78f, .3f, .9f);

        private PartyLobbyController lobby;
        private GameObject[] avatars;
        private PlayerCharacterOutfit[] outfits;
        private Transform[,] anchorsA;
        private Transform[,] anchorsB;
        private RectTransform[,,] arrows; // figure, part, side (0 left, 1 right)
        private TextMeshProUGUI[,,] labels;
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
            arrows = new RectTransform[avatars.Length, Parts.Length, 2];
            labels = new TextMeshProUGUI[avatars.Length, Parts.Length, 2];
            for (int i = 0; i < avatars.Length; i++)
            {
                if (outfits[i] != null)
                {
                    var bones = outfits[i].GetComponentsInChildren<Transform>(true);
                    // Head: halfway between the base of the skull and the top of the head.
                    anchorsA[i, 0] = Bone(bones, "spine.006"); anchorsB[i, 0] = Bone(bones, "Socket_Cabeza");
                    anchorsA[i, 1] = Bone(bones, "spine.002"); anchorsB[i, 1] = anchorsA[i, 1];
                    anchorsA[i, 2] = Bone(bones, "foot.L"); anchorsB[i, 2] = Bone(bones, "foot.R");
                }
                for (int p = 0; p < Parts.Length; p++)
                    for (int side = 0; side < 2; side++)
                    {
                        int figure = i, part = p, step = side == 0 ? -1 : 1;
                        arrows[i, p, side] = Arrow(side == 0 ? "<" : ">", () => lobby.StepPart(figure, Parts[part], step), out labels[i, p, side]);
                    }
            }
        }

        private static Transform Bone(Transform[] bones, string name)
        {
            foreach (var bone in bones)
                if (bone.name == name) return bone;
            return null;
        }

        // Clickable, but only the white arrow shows: the button image is fully transparent.
        private RectTransform Arrow(string text, System.Action onClick, out TextMeshProUGUI label)
        {
            var rect = new GameObject("Outfit arrow " + text, typeof(RectTransform), typeof(Image), typeof(Button)).GetComponent<RectTransform>();
            rect.SetParent(area, false);
            rect.sizeDelta = new Vector2(ArrowSize, ArrowSize);
            rect.GetComponent<Image>().color = Color.clear;
            var button = rect.GetComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.navigation = new Navigation { mode = Navigation.Mode.None }; // players use their own pads
            button.onClick.AddListener(() => onClick());
            label = new GameObject("Text", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            label.rectTransform.SetParent(rect, false);
            label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
            label.font = TMP_Settings.defaultFontAsset;
            label.text = text;
            label.fontSize = 46f;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            // Own material: a thin dark outline so it reads on any background, and a glow that
            // only lights up on the selected row.
            var material = label.fontMaterial;
            material.EnableKeyword("OUTLINE_ON");
            material.SetFloat(ShaderUtilities.ID_OutlineWidth, .12f);
            material.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0f, 0f, 0f, .6f));
            material.EnableKeyword("GLOW_ON");
            material.SetColor(ShaderUtilities.ID_GlowColor, GlowColor);
            material.SetFloat(ShaderUtilities.ID_GlowOuter, .6f);
            material.SetFloat(ShaderUtilities.ID_GlowPower, 0f);
            rect.gameObject.SetActive(false);
            return rect;
        }

        private void LateUpdate()
        {
            if (lobby == null) return;
            var view = Camera.main;
            float pulse = .5f + .5f * Mathf.Sin(Time.unscaledTime * 6f);
            for (int i = 0; i < avatars.Length; i++)
            {
                bool show = view != null && lobby.Players != null && avatars[i] != null && avatars[i].activeInHierarchy
                    && outfits[i] != null && lobby.CanCustomize(i);
                int selected = show ? lobby.SelectedPartRow(i) : -1;
                for (int p = 0; p < Parts.Length; p++)
                {
                    bool lit = p == selected;
                    bool placed = show && Place(view, i, p, lit ? pulse * 6f : 0f);
                    for (int side = 0; side < 2; side++)
                    {
                        var arrow = arrows[i, p, side];
                        if (arrow.gameObject.activeSelf != placed) arrow.gameObject.SetActive(placed);
                        if (!placed) continue;
                        var label = labels[i, p, side];
                        label.color = lit ? Color.Lerp(Color.white, Lit, pulse) : Idle;
                        label.fontMaterial.SetFloat(ShaderUtilities.ID_GlowPower, lit ? .35f + .35f * pulse : 0f);
                        arrow.localScale = Vector3.one * (lit ? 1.05f + .12f * pulse : .85f);
                    }
                }
            }
        }

        // Puts both arrows of a part beside it on screen (pushed `nudge` pixels further out);
        // false when it cannot be seen.
        private bool Place(Camera view, int figure, int part, float nudge)
        {
            var a = anchorsA[figure, part];
            var b = anchorsB[figure, part];
            if (a == null || b == null) return false;
            var point = (a.position + b.position) * .5f;
            float halfWidth = Offsets[part] * outfits[figure].transform.lossyScale.y;

            var centre = view.WorldToScreenPoint(point);
            var edge = view.WorldToScreenPoint(point + view.transform.right * halfWidth);
            if (centre.z <= 0f) return false;
            var uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(area, centre, uiCamera, out var local);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(area, edge, uiCamera, out var localEdge);
            float offset = Mathf.Abs(localEdge.x - local.x) + Gap + nudge;
            arrows[figure, part, 0].anchoredPosition = AnchoredFromLocal(local + Vector2.left * offset);
            arrows[figure, part, 1].anchoredPosition = AnchoredFromLocal(local + Vector2.right * offset);
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
