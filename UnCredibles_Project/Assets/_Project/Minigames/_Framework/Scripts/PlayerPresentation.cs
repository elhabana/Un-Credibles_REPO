using TMPro;
using UnCredibles.Localization;
using UnCredibles.Players;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnCredibles.Minigames
{
    // Local label of the registered slot; the body colour comes from the slot's prefab.
    // No additional network object is needed.
    public sealed class PlayerPresentation : MonoBehaviour
    {
        private PlayerSlot slot;
        private Renderer[] bodies = new Renderer[0];
        private TextMeshPro label;
        private Material labelMaterial;
        private Camera viewCamera;
        private bool wasAI;
        private float headHeight = 1.5f;

        // Name tag over the head ("P1", "BOT"); lives beside the avatar, not under it.
        public TextMeshPro Label => label;

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
            presentation.RefreshLabel();
            return presentation;
        }

        private void EnsureLabel()
        {
            if (label != null) return;
            // Not a child of the avatar: its squash-and-stretch would shear the camera-facing text, and
            // undoing a near-zero scale made it huge. Same scene, so it unloads with the minigame.
            var go = new GameObject("Player label");
            SceneManager.MoveGameObjectToScene(go, gameObject.scene);
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
            L10n.LanguageChanged += RefreshLabel; // "P1" / "J1"
        }

        private void RefreshLabel()
        {
            wasAI = slot.IsAI;
            label.text = PlayerIdentity.LabelFor(slot.SlotIndex, slot.IsAI);
            label.color = PlayerIdentity.ColorFor(slot.SlotIndex, slot.IsAI);
        }

        private void LateUpdate()
        {
            if (slot == null || label == null) return;
            if (wasAI != slot.IsAI) RefreshLabel();
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
        }

        // The label is not under the avatar, so it has to follow it out of sight.
        private void OnDisable()
        {
            if (label != null) label.enabled = false;
        }

        private void OnDestroy()
        {
            L10n.LanguageChanged -= RefreshLabel;
            if (label != null) Destroy(label.gameObject);
            if (labelMaterial != null) Destroy(labelMaterial);
        }
    }
}
