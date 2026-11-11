using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace UnCredibles.UI.Garage
{
    // Full-screen flash over everything: fades in, holds while the scene changes underneath, fades out.
    // Lives on its own object, so it finishes even when whoever started it is switched off meanwhile.
    public sealed class ScreenFlash : MonoBehaviour
    {
        private Image image;
        private Coroutine running;

        public static ScreenFlash Create(Color color)
        {
            var go = new GameObject("Screen flash", typeof(Canvas), typeof(CanvasGroup));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            go.GetComponent<CanvasGroup>().blocksRaycasts = false;
            var flash = go.AddComponent<ScreenFlash>();
            flash.image = new GameObject("Flash", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            flash.image.transform.SetParent(go.transform, false);
            var rect = flash.image.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;
            flash.image.color = new Color(color.r, color.g, color.b, 0f);
            flash.image.raycastTarget = false;
            go.SetActive(false);
            return flash;
        }

        public void Play(float fadeIn, float hold, float fadeOut)
        {
            gameObject.SetActive(true);
            if (running != null) StopCoroutine(running);
            running = StartCoroutine(Run(fadeIn, hold, fadeOut));
        }

        public void Stop()
        {
            if (running != null) StopCoroutine(running);
            running = null;
            SetAlpha(0f);
            gameObject.SetActive(false);
        }

        private IEnumerator Run(float fadeIn, float hold, float fadeOut)
        {
            float start = image.color.a;
            for (float t = 0f; t < fadeIn; t += Time.unscaledDeltaTime)
            {
                float x = t / fadeIn;
                SetAlpha(Mathf.Lerp(start, 1f, x * x)); // speeds up into full white
                yield return null;
            }
            SetAlpha(1f);
            yield return new WaitForSecondsRealtime(hold);
            for (float t = 0f; t < fadeOut; t += Time.unscaledDeltaTime)
            {
                SetAlpha(1f - Mathf.SmoothStep(0f, 1f, t / fadeOut));
                yield return null;
            }
            running = null;
            Stop();
        }

        private void SetAlpha(float alpha)
        {
            var color = image.color;
            color.a = alpha;
            image.color = color;
        }
    }
}
