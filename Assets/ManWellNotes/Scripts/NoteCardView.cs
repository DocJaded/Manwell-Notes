using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ManWellNotes
{
    // Inspector-authored view for one note card. NotesApp only supplies data and
    // event bindings; the visual hierarchy and baseline geometry live in the prefab.
    public sealed class NoteCardView : MonoBehaviour
    {
        [SerializeField] private Image background;
        [SerializeField] private Button openButton;
        [SerializeField] private LongPressButton longPressButton;
        [SerializeField] private Image pinnedIcon;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text previewText;
        [SerializeField] private TMP_Text createdText;

        public Button OpenButton => openButton;
        public LongPressButton LongPressButton => longPressButton;

        public void Present(
            string title,
            string preview,
            string created,
            bool pinned,
            bool active,
            Sprite pinnedIconBlack,
            Sprite pinnedIconWhite,
            Color activeBackground,
            Color inactiveBackground,
            Color activeText,
            Color inactiveText,
            Color activePreview,
            Color inactiveSecondary)
        {
            background.color = active ? activeBackground : inactiveBackground;

            ColorBlock colors = openButton.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = active
                ? new Color32(48, 48, 48, 255)
                : new Color32(225, 225, 225, 255);
            colors.pressedColor = new Color32(195, 195, 195, 255);
            openButton.colors = colors;

            pinnedIcon.gameObject.SetActive(pinned);
            pinnedIcon.sprite = active ? pinnedIconWhite : pinnedIconBlack;

            titleText.text = title;
            previewText.text = preview;
            createdText.text = created;
            titleText.color = active ? activeText : inactiveText;
            previewText.color = active ? activePreview : inactiveSecondary;
            createdText.color = active
                ? new Color32(190, 190, 190, 255)
                : inactiveSecondary;

            RectTransform titleRect = titleText.rectTransform;
            Vector2 offsetMin = titleRect.offsetMin;
            offsetMin.x = pinned ? 48f : 16f;
            titleRect.offsetMin = offsetMin;

            titleText.ForceMeshUpdate(true, true);
            previewText.ForceMeshUpdate(true, true);
            createdText.ForceMeshUpdate(true, true);
        }
    }
}
