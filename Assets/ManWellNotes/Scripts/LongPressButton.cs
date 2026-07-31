using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ManWellNotes
{
    public sealed class LongPressButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [SerializeField] private float holdSeconds = 0.65f;

        public event Action LongPressed;

        private bool pointerDown;
        private bool longPressTriggered;
        private bool suppressNextClick;
        private float pressedAt;

        public void OnPointerDown(PointerEventData eventData)
        {
            pointerDown = true;
            longPressTriggered = false;
            suppressNextClick = false;
            pressedAt = Time.unscaledTime;
        }

        public void OnPointerUp(PointerEventData eventData) => pointerDown = false;

        public void OnPointerExit(PointerEventData eventData) => pointerDown = false;

        private void Update()
        {
            if (!pointerDown || longPressTriggered ||
                Time.unscaledTime - pressedAt < holdSeconds)
                return;

            longPressTriggered = true;
            suppressNextClick = true;
            LongPressed?.Invoke();
        }

        public bool ConsumeSuppressedClick()
        {
            bool result = suppressNextClick;
            suppressNextClick = false;
            return result;
        }
    }
}
