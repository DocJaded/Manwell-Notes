using UnityEngine;
using UnityEngine.InputSystem;

namespace ManWellNotes
{
    // Attached only to the saved full-image viewer. No runtime UI construction.
    public sealed class NotePhotoZoom : MonoBehaviour
    {
        [SerializeField] private RectTransform viewport;
        [SerializeField] private RectTransform image;
        [SerializeField, Range(1, 10)] private float maximumZoom = 4;

        private float zoom = 1;
        private Vector2 offset;
        private int firstId = -1, secondId = -1;
        private Vector2 previousFirst, previousSecond;

        private void OnEnable() => ResetView();
        private void OnDisable() => ResetView();

        public void ResetView()
        {
            zoom = 1;
            offset = Vector2.zero;
            firstId = secondId = -1;
            if (image == null) return;
            image.localScale = Vector3.one;
            image.anchoredPosition = Vector2.zero;
        }

        private void LateUpdate()
        {
            if (viewport == null || image == null) return;
            var screen = Touchscreen.current;
            int count = 0, a = -1, b = -1;
            Vector2 p = default, q = default;
            var canvas = viewport.GetComponentInParent<Canvas>();
            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            if (screen != null)
                foreach (var touch in screen.touches)
                {
                    if (!touch.press.isPressed) continue;
                    Vector2 position = touch.position.ReadValue();
                    if (!RectTransformUtility.RectangleContainsScreenPoint(viewport, position, camera)) continue;
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport, position, camera, out var local);
                    if (count == 0) { a = touch.touchId.ReadValue(); p = local; }
                    else if (count == 1) { b = touch.touchId.ReadValue(); q = local; }
                    count++;
                }
            if (count == 2 && a == firstId && b == secondId)
            {
                float oldDistance = Vector2.Distance(previousFirst, previousSecond);
                if (oldDistance > 1)
                {
                    float nextZoom = Mathf.Clamp(zoom * Vector2.Distance(p, q) / oldDistance, 1, maximumZoom);
                    offset = PinchOffset(offset, (previousFirst + previousSecond) * 0.5f,
                        (p + q) * 0.5f, nextZoom / zoom);
                    zoom = nextZoom;
                }
            }
            else if (count == 1 && a == firstId && secondId == -1 && zoom > 1)
                offset += p - previousFirst;
            firstId = count is 1 or 2 ? a : -1;
            secondId = count == 2 ? b : -1;
            previousFirst = p;
            previousSecond = q;
            offset = ClampOffset(offset, image.rect.size * zoom, viewport.rect.size);
            image.localScale = new Vector3(zoom, zoom, 1);
            image.anchoredPosition = offset;
        }

        // Keep the point under the pinch midpoint stationary while scaling and panning.
        public static Vector2 PinchOffset(Vector2 offset, Vector2 oldMidpoint, Vector2 newMidpoint, float ratio)
            => newMidpoint - (oldMidpoint - offset) * ratio;

        public static Vector2 ClampOffset(Vector2 offset, Vector2 imageSize, Vector2 viewportSize)
        {
            Vector2 limit = Vector2.Max(Vector2.zero, (imageSize - viewportSize) * 0.5f);
            return new Vector2(Mathf.Clamp(offset.x, -limit.x, limit.x), Mathf.Clamp(offset.y, -limit.y, limit.y));
        }
    }
}
