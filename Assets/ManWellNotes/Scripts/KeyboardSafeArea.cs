using UnityEngine;

namespace ManWellNotes
{
    /// <summary>
    /// Keeps an existing UI panel above the device safe area and on-screen keyboard.
    /// This component only resizes its saved RectTransform; it never creates UI at runtime.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class KeyboardSafeArea : MonoBehaviour
    {
        [Tooltip("Extra breathing room between the app and the top edge of the keyboard.")]
        [SerializeField, Min(0f)] private float keyboardPadding = 12f;

        private RectTransform panel;
        private Canvas parentCanvas;
        private Vector2 restingOffsetMin;
        private float lastBottomInset = -1f;

        private void Awake()
        {
            panel = GetComponent<RectTransform>();
            parentCanvas = GetComponentInParent<Canvas>();
            restingOffsetMin = panel.offsetMin;
        }

        private void OnEnable()
        {
            // Awake runs before OnEnable in a player build, but this also makes the
            // component safe when it is enabled manually in the Unity Editor.
            if (panel == null)
                Awake();

            ApplyBottomInset(force: true);
        }

        private void LateUpdate()
        {
            // Android can report the keyboard rectangle a frame or two after it
            // becomes visible, so check during LateUpdate rather than only on focus.
            ApplyBottomInset(force: false);
        }

        private void OnDisable()
        {
            if (panel != null)
                panel.offsetMin = restingOffsetMin;

            lastBottomInset = -1f;
        }

        private void ApplyBottomInset(bool force)
        {
            float obscuredPixels = Screen.safeArea.yMin;

            if (TouchScreenKeyboard.visible)
                obscuredPixels = Mathf.Max(obscuredPixels, TouchScreenKeyboard.area.height);

            float scaleFactor = parentCanvas != null
                ? Mathf.Max(parentCanvas.rootCanvas.scaleFactor, 0.0001f)
                : 1f;

            float inset = obscuredPixels / scaleFactor;
            if (TouchScreenKeyboard.visible && inset > 0f)
                inset += keyboardPadding;

            if (!force && Mathf.Approximately(inset, lastBottomInset))
                return;

            Vector2 offset = restingOffsetMin;
            offset.y += inset;
            panel.offsetMin = offset;
            lastBottomInset = inset;
        }
    }
}
