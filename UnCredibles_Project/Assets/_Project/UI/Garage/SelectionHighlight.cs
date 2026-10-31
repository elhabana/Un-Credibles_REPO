using System.Collections.Generic;
using TMPro;
using UnCredibles.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UnCredibles.UI.Garage
{
    // Marks where the player is in the garage menus: the selected button turns pink and grows a little,
    // and the mouse hover gets a lighter pink. The TV settings are left out, they outline their own rows.
    // Hovering a button with the mouse selects it, and moving the selection plays a sound (TV included).
    public sealed class SelectionHighlight : MonoBehaviour
    {
        private static readonly Color Selected = new Color(1f, .45f, .85f);
        private static readonly Color Hover = new Color(1f, .75f, .93f);
        private const float SelectedScale = 1.08f;
        private const float ScaleSpeed = 14f;

        private readonly Dictionary<Transform, Vector3> baseScales = new Dictionary<Transform, Vector3>();
        private readonly HashSet<Selectable> tinted = new HashSet<Selectable>();
        private Transform excluded;
        private AudioClip selectSound;
        private Transform current;
        private GameObject lastSelected;

        public void Init(Transform exclude, AudioClip sound)
        {
            excluded = exclude;
            selectSound = sound;
            foreach (var selectable in GetComponentsInChildren<Selectable>(true))
            {
                Tint(selectable);
                SelectOnHover.Add(selectable);
            }
            if (exclude != null)
                foreach (var selectable in exclude.GetComponentsInChildren<Selectable>(true))
                    SelectOnHover.Add(selectable);
        }

        private void Update()
        {
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            PlaySoundOnMove(selected);
            var selectable = selected != null ? selected.GetComponent<Selectable>() : null;
            var target = selectable != null && Marks(selectable) ? selectable.transform : null;
            if (target != null) Tint(selectable);
            if (target != current)
            {
                Restore(current);
                current = target;
                if (current != null && !baseScales.ContainsKey(current)) baseScales[current] = current.localScale;
            }
            if (current != null && current.GetComponent<KeepSize>() == null)
                current.localScale = Vector3.Lerp(current.localScale, baseScales[current] * SelectedScale,
                    1f - Mathf.Exp(-ScaleSpeed * Time.unscaledDeltaTime));
        }

        // Only a move inside the same menu sounds: opening a menu hides the previous selection's panel,
        // and a click on empty space (selection lost and given back) leaves it on the same object.
        private void PlaySoundOnMove(GameObject selected)
        {
            if (selected == lastSelected) return;
            bool moved = selected != null && lastSelected != null && lastSelected.activeInHierarchy;
            if (selected != null) lastSelected = selected;
            if (moved && selectSound != null && CoreRoot.Instance != null) CoreRoot.Instance.Audio.PlaySfx(selectSound);
        }

        private void OnDisable()
        {
            Restore(current);
            current = null;
        }

        private bool Marks(Selectable selectable) =>
            selectable.transform.IsChildOf(transform) && (excluded == null || !selectable.transform.IsChildOf(excluded));

        private void Tint(Selectable selectable)
        {
            if (!Marks(selectable) || !tinted.Add(selectable)) return;
            var colors = selectable.colors;
            colors.selectedColor = Selected;
            colors.highlightedColor = Hover;
            selectable.colors = colors;
        }

        private void Restore(Transform target)
        {
            if (target != null && baseScales.TryGetValue(target, out var scale)) target.localScale = scale;
        }

        // On buttons joined to others (side by side, no gap): selected, they only turn pink, so they don't overlap.
        public sealed class KeepSize : MonoBehaviour
        {
        }

        private sealed class SelectOnHover : MonoBehaviour, IPointerEnterHandler
        {
            // Looked up on its own object when needed: a script recompiled during Play, or a copy of the
            // button made at runtime, comes back with this field empty.
            private Selectable selectable;

            // Text fields stay click-only: selecting one starts typing.
            public static void Add(Selectable selectable)
            {
                if (selectable is TMP_InputField || selectable.GetComponent<SelectOnHover>() != null) return;
                selectable.gameObject.AddComponent<SelectOnHover>();
            }

            public void OnPointerEnter(PointerEventData eventData)
            {
                if (selectable == null) selectable = GetComponent<Selectable>();
                var events = EventSystem.current;
                if (events == null || selectable == null || !selectable.IsInteractable() ||
                    events.currentSelectedGameObject == gameObject) return;
                // Not while dragging a slider: the knob would lose it.
                if (eventData.dragging) return;
                events.SetSelectedGameObject(gameObject);
            }
        }
    }
}
