using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace UnCredibles.UI.CharacterEditor
{
    // Full-screen surface behind the panels: drag turns the character, wheel zooms.
    public sealed class DragSurface : MonoBehaviour, IDragHandler, IScrollHandler
    {
        public event Action<Vector2> Dragged;
        public event Action<float> Scrolled;

        public void OnDrag(PointerEventData eventData) => Dragged?.Invoke(eventData.delta);
        public void OnScroll(PointerEventData eventData) => Scrolled?.Invoke(Mathf.Sign(eventData.scrollDelta.y));
    }
}
