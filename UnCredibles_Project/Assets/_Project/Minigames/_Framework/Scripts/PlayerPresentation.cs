using TMPro;
using UnCredibles.Players;
using UnityEngine;

namespace UnCredibles.Minigames
{
    // Local presentation of the registered slot; no additional network object is needed.
    public sealed class PlayerPresentation : MonoBehaviour
    {
        private PlayerSlot slot;
        private Renderer[] bodies = new Renderer[0];
        private MaterialPropertyBlock block;
        private TextMeshPro label;
        private Material labelMaterial;
        private Camera viewCamera;
        private bool wasAI;
        private float headHeight = 1.5f;
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorProperty = Shader.PropertyToID("_Color");

        public static PlayerPresentation Attach(Component avatar, PlayerSlot player, Renderer[] bodyRenderers = null)
        {
            var presentation = avatar.GetComponent<PlayerPresentation>();
            if (presentation == null) presentation = avatar.gameObject.AddComponent<PlayerPresentation>();
            presentation.slot = player;
            if (bodyRenderers != null)
            {
                presentation.bodies = bodyRenderers;
                float top = avatar.transform.position.y;
                foreach (var body in bodyRenderers)
                    if (body != null) top = Mathf.Max(top, body.bounds.max.y);
                presentation.headHeight = Mathf.Max(.5f, top - avatar.transform.position.y) + .25f;
            }
            presentation.EnsureLabel();
            presentation.RefreshColor();
            return presentation;
        }

        private void EnsureLabel()
        {
            if (label != null) return;
            block = new MaterialPropertyBlock();
            var go = new GameObject("Player label");
            go.transform.SetParent(transform, false);
            label = go.AddComponent<TextMeshPro>();
            label.font = TMP_Settings.defaultFontAsset;
            label.fontSize = 3.6f;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.rectTransform.sizeDelta = new Vector2(2f, 1f);
            label.outlineColor = Color.white;
            label.outlineWidth = .22f;
            // TMP creates a private material for these outline settings.
            labelMaterial = label.fontSharedMaterial;
            if (labelMaterial != null) labelMaterial.EnableKeyword("OUTLINE_ON");
            label.raycastTarget = false;
            label.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private void RefreshColor()
        {
            wasAI = slot.IsAI;
            Color color = PlayerIdentity.ColorFor(slot.SlotIndex, slot.IsAI);
            label.text = PlayerIdentity.LabelFor(slot.SlotIndex, slot.IsAI);
            label.color = color;
            foreach (var body in bodies)
            {
                if (body == null) continue;
                body.GetPropertyBlock(block);
                block.SetColor(BaseColor, color);
                block.SetColor(ColorProperty, color);
                body.SetPropertyBlock(block);
            }
        }

        private void LateUpdate()
        {
            if (slot == null || label == null) return;
            if (wasAI != slot.IsAI) RefreshColor();
            if (viewCamera == null || !viewCamera.isActiveAndEnabled) viewCamera = Camera.main;
            if (viewCamera == null) return;
            float top = transform.position.y;
            bool visible = bodies.Length == 0;
            foreach (var body in bodies)
            {
                if (body == null || !body.enabled || !body.gameObject.activeInHierarchy) continue;
                visible = true;
                top = Mathf.Max(top, body.bounds.max.y);
            }
            label.enabled = visible;
            float height = bodies.Length == 0 ? headHeight : top - transform.position.y + .25f;
            // Ignore avatar rotation/scale so text stays upright and readable while moving.
            label.transform.position = transform.position + Vector3.up * height + viewCamera.transform.up * .2f;
            label.transform.rotation = viewCamera.transform.rotation;
            var scale = transform.lossyScale;
            label.transform.localScale = new Vector3(1f / Mathf.Max(.001f, Mathf.Abs(scale.x)),
                1f / Mathf.Max(.001f, Mathf.Abs(scale.y)), 1f / Mathf.Max(.001f, Mathf.Abs(scale.z)));
        }

        private void OnDestroy()
        {
            if (labelMaterial != null) Destroy(labelMaterial);
        }
    }
}
