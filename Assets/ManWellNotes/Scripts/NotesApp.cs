using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using TMPro;

namespace ManWellNotes
{
    public sealed class NotesApp : MonoBehaviour
    {
        // The palette lives in one place so the whole interface can be restyled easily.
        private static readonly Color Ink = new Color32(12, 12, 12, 255);
        private static readonly Color Paper = new Color32(250, 250, 250, 255);
        private static readonly Color White = Color.white;
        private static readonly Color Soft = new Color32(239, 239, 239, 255);
        private static readonly Color Line = new Color32(218, 218, 218, 255);
        private static readonly Color Muted = new Color32(103, 103, 103, 255);

        // Scene references are serialized so you can inspect and rearrange the real
        // UI GameObjects in the Editor. The scene builder assigns all of these once.
        [Header("Notes list")]
        [SerializeField] private RectTransform notesContent;
        [SerializeField] private TMP_Text notesHeading;
        [SerializeField] private TMP_Text emptyMessage;
        [SerializeField] private TMP_InputField searchField;

        [Header("Editor")]
        [SerializeField] private GameObject editorPanel;
        [SerializeField] private TMP_InputField titleField;
        [SerializeField] private TMP_Text createdText;
        [SerializeField] private TMP_InputField bodyField;
        [SerializeField] private Button pinButton;
        [SerializeField] private TMP_Text pinButtonText;

        [Header("Status")]
        [SerializeField] private TMP_Text statusText;

        [Header("Page navigation")]
        [SerializeField] private NotesPageNavigator pageNavigator;

        [Header("Typography")]
        [Tooltip("Sekaiwo is the primary face. Its saved TMP asset contains the Noto fallback chain.")]
        [SerializeField] private TMP_FontAsset font;

        private NoteStore store;
        private readonly List<NoteData> notes = new List<NoteData>();
        private readonly List<GameObject> noteCards = new List<GameObject>();

        private NoteData selectedNote;
        private bool suppressEditorEvents;
        private bool saveQueued;
        private float saveAt;

        private void Awake()
        {
            // Unity API properties such as persistentDataPath are safe from Awake onward.
            // Constructing NoteStore here avoids accessing them in a field initializer.
            store = new NoteStore();

            // A missing reference means the scene builder was not run or a required
            // object was deleted. We stop here instead of silently creating runtime UI.
            if (font == null || notesContent == null || searchField == null || titleField == null ||
                createdText == null || bodyField == null)
            {
                Debug.LogError("Man-Well Notes is not configured. Rebuild the notes interface from the Editor.");
                enabled = false;
                return;
            }

            notes.AddRange(store.Load().Where(note => note != null));
            if (RepairMissingCreationDates())
                store.Save(notes);
            BindEvents();
            RefreshList();

            NoteData initial = SortedNotes().FirstOrDefault();
            if (initial != null)
                SelectNote(initial, false);
            else
                ShowEmptyEditor();

            pageNavigator.ShowLibraryPage();
        }

        // UnityEvent listeners added with AddListener are runtime-only. Keeping all
        // bindings here makes it easy to see which user action calls which method.
        private void BindEvents()
        {
            searchField.onValueChanged.AddListener(_ => RefreshList());
            titleField.onValueChanged.AddListener(OnTitleChanged);
            bodyField.onValueChanged.AddListener(OnBodyChanged);
            pinButton.onClick.AddListener(TogglePin);

            FindButton("New note")?.onClick.AddListener(CreateNewNote);
            FindButton("All notes")?.onClick.AddListener(ShowAllNotes);
            FindButton("Pinned")?.onClick.AddListener(ShowPinnedNotes);
            FindButton("Delete")?.onClick.AddListener(DeleteSelectedNote);
        }

        private Button FindButton(string objectName)
        {
            return GetComponentsInChildren<Button>(true)
                .FirstOrDefault(button => button.gameObject.name == objectName);
        }

        private void Update()
        {
            if (!saveQueued || Time.unscaledTime < saveAt)
                return;

            saveQueued = false;
            store.Save(notes);
            SetStatus("Saved");
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
                SaveImmediately();
        }

        private void OnApplicationQuit() => SaveImmediately();

        // Called by NotesSceneBuilder while editing the scene. It deliberately creates
        // ordinary GameObjects, components and serialized references—not runtime UI.
        public void BuildSceneInterface(TMP_FontAsset primaryFont)
        {
            // The asset itself stores its ordered fallback list, so every text object
            // only needs a reference to the Sekaiwo primary font.
            font = primaryFont;
            EnsureEventSystem();

            Canvas canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = false;

            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            // Work in phone-sized logical units. A 1080 x 1920 display then renders
            // these values at roughly 3 physical pixels per UI unit.
            scaler.referenceResolution = new Vector2(360f, 640f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            GameObject background = Panel("Background", transform, Paper);
            Stretch(background.GetComponent<RectTransform>());

            GameObject shell = Panel("App", background.transform, Paper);
            Stretch(shell.GetComponent<RectTransform>(), 18f, 18f, 18f, 18f);

            // Keep every generated page above Android's keyboard, including the
            // notes search page as well as the title and body editor.
            shell.AddComponent<KeyboardSafeArea>();

            HorizontalLayoutGroup shellLayout = shell.AddComponent<HorizontalLayoutGroup>();
            shellLayout.spacing = 1f;
            shellLayout.childControlWidth = true;
            shellLayout.childControlHeight = true;
            shellLayout.childForceExpandWidth = false;
            shellLayout.childForceExpandHeight = true;
            shellLayout.padding = new RectOffset(0, 0, 0, 0);

            BuildSidebar(shell.transform);
            BuildNotesPane(shell.transform);
            BuildEditor(shell.transform);
        }

        private void BuildSidebar(Transform parent)
        {
            GameObject sidebar = Panel("Sidebar", parent, Ink);
            LayoutElement size = sidebar.AddComponent<LayoutElement>();
            size.preferredWidth = 245f;
            size.minWidth = 210f;

            VerticalLayoutGroup layout = sidebar.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(24, 24, 25, 24);
            layout.spacing = 12f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;

            TMP_Text brand = Label("MAN-WELL", sidebar.transform, 28, FontStyles.Bold, White);
            brand.gameObject.AddComponent<LayoutElement>().preferredHeight = 35f;

            TMP_Text subtitle = Label("NOTES", sidebar.transform, 14, FontStyles.Bold, new Color32(190, 190, 190, 255));
            subtitle.gameObject.AddComponent<LayoutElement>().preferredHeight = 26f;

            Button newButton = CreateButton("+   New note", sidebar.transform, White, Ink, 19, FontStyles.Bold);
            newButton.gameObject.name = "New note";
            newButton.GetComponent<LayoutElement>().preferredHeight = 48f;

            Spacer(sidebar.transform, 15f);
            TMP_Text library = Label("LIBRARY", sidebar.transform, 14, FontStyles.Bold, new Color32(155, 155, 155, 255));
            library.gameObject.AddComponent<LayoutElement>().preferredHeight = 22f;

            Button allNotes = CreateButton("All notes", sidebar.transform, new Color32(38, 38, 38, 255), White, 18, FontStyles.Bold);
            allNotes.gameObject.name = "All notes";
            allNotes.GetComponent<LayoutElement>().preferredHeight = 44f;

            Button pinned = CreateButton("Pinned", sidebar.transform, Ink, White, 18, FontStyles.Normal);
            pinned.gameObject.name = "Pinned";
            pinned.GetComponent<LayoutElement>().preferredHeight = 44f;

            GameObject flexible = new GameObject("Flexible space", typeof(RectTransform), typeof(LayoutElement));
            flexible.transform.SetParent(sidebar.transform, false);
            flexible.GetComponent<LayoutElement>().flexibleHeight = 1f;

            statusText = Label("Ready", sidebar.transform, 16, FontStyles.Normal, new Color32(180, 180, 180, 255));
            statusText.gameObject.AddComponent<LayoutElement>().preferredHeight = 22f;

            TMP_Text storage = Label("Stored privately on this device", sidebar.transform, 14, FontStyles.Normal, new Color32(125, 125, 125, 255));
            storage.gameObject.AddComponent<LayoutElement>().preferredHeight = 28f;
        }

        private void BuildNotesPane(Transform parent)
        {
            GameObject pane = Panel("Notes list", parent, White);
            LayoutElement size = pane.AddComponent<LayoutElement>();
            size.preferredWidth = 410f;
            size.minWidth = 340f;

            VerticalLayoutGroup layout = pane.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(26, 26, 25, 22);
            layout.spacing = 14f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;

            notesHeading = Label("All notes", pane.transform, 30, FontStyles.Bold, Ink);
            notesHeading.gameObject.AddComponent<LayoutElement>().preferredHeight = 38f;

            searchField = CreateInput("Search notes", pane.transform, 18, false);
            searchField.GetComponent<LayoutElement>().preferredHeight = 44f;

            GameObject rule = Panel("Rule", pane.transform, Line);
            rule.AddComponent<LayoutElement>().preferredHeight = 1f;

            GameObject scrollObject = Panel("Scroll view", pane.transform, White);
            LayoutElement scrollSize = scrollObject.AddComponent<LayoutElement>();
            scrollSize.flexibleHeight = 1f;
            scrollSize.minHeight = 300f;

            ScrollRect scroll = scrollObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 35f;

            GameObject viewport = Panel("Viewport", scrollObject.transform, White);
            Stretch(viewport.GetComponent<RectTransform>());
            viewport.AddComponent<RectMask2D>();
            scroll.viewport = viewport.GetComponent<RectTransform>();

            GameObject content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);
            notesContent = content.GetComponent<RectTransform>();
            notesContent.anchorMin = new Vector2(0f, 1f);
            notesContent.anchorMax = new Vector2(1f, 1f);
            notesContent.pivot = new Vector2(0.5f, 1f);
            notesContent.offsetMin = Vector2.zero;
            notesContent.offsetMax = Vector2.zero;

            VerticalLayoutGroup contentLayout = content.AddComponent<VerticalLayoutGroup>();
            contentLayout.spacing = 9f;
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = false;
            contentLayout.childForceExpandWidth = true;
            ContentSizeFitter fitter = content.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = notesContent;

            emptyMessage = Label("No notes yet.\nCreate one to get started.", viewport.transform, 18, FontStyles.Normal, Muted);
            Stretch(emptyMessage.rectTransform, 20f, 20f, 20f, 20f);
            emptyMessage.alignment = TextAlignmentOptions.Center;
        }

        private void BuildEditor(Transform parent)
        {
            editorPanel = Panel("Editor", parent, Paper);
            LayoutElement size = editorPanel.AddComponent<LayoutElement>();
            size.flexibleWidth = 1f;
            size.minWidth = 480f;

            VerticalLayoutGroup layout = editorPanel.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(42, 42, 24, 28);
            layout.spacing = 12f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;

            GameObject toolbar = Panel("Toolbar", editorPanel.transform, Paper);
            toolbar.AddComponent<LayoutElement>().preferredHeight = 42f;
            HorizontalLayoutGroup toolbarLayout = toolbar.AddComponent<HorizontalLayoutGroup>();
            toolbarLayout.spacing = 8f;
            toolbarLayout.childControlWidth = false;
            toolbarLayout.childControlHeight = true;
            toolbarLayout.childForceExpandHeight = true;

            pinButton = CreateButton("Pin", toolbar.transform, Soft, Ink, 17, FontStyles.Bold);
            pinButton.GetComponent<LayoutElement>().preferredWidth = 82f;
            pinButtonText = pinButton.GetComponentInChildren<TMP_Text>();

            GameObject toolbarSpace = new GameObject("Space", typeof(RectTransform), typeof(LayoutElement));
            toolbarSpace.transform.SetParent(toolbar.transform, false);
            toolbarSpace.GetComponent<LayoutElement>().flexibleWidth = 1f;

            Button deleteButton = CreateButton("Delete", toolbar.transform, Ink, White, 17, FontStyles.Bold);
            deleteButton.gameObject.name = "Delete";
            deleteButton.GetComponent<LayoutElement>().preferredWidth = 92f;

            titleField = CreateInput("Title", editorPanel.transform, 34, false);
            titleField.textComponent.fontStyle = FontStyles.Bold;
            titleField.GetComponent<Image>().color = Paper;
            titleField.GetComponent<LayoutElement>().preferredHeight = 65f;

            createdText = Label("Created date and time", editorPanel.transform, 16, FontStyles.Normal, Muted);
            createdText.gameObject.name = "Created timestamp";
            createdText.gameObject.AddComponent<LayoutElement>().preferredHeight = 24f;

            GameObject divider = Panel("Divider", editorPanel.transform, Line);
            divider.AddComponent<LayoutElement>().preferredHeight = 1f;

            bodyField = CreateInput("Start writing…", editorPanel.transform, 20, true);
            bodyField.GetComponent<Image>().color = Paper;
            LayoutElement bodySize = bodyField.GetComponent<LayoutElement>();
            bodySize.flexibleHeight = 1f;
            bodySize.minHeight = 300f;

            // The editor uses a vertical layout, so placing the existing toolbar last
            // keeps Back, Pin and Delete at the bottom in newly generated scenes too.
            toolbar.transform.SetAsLastSibling();
        }

        private void ShowAllNotes()
        {
            searchField.text = string.Empty;
            RefreshList();
            pageNavigator.ShowNotesPage();
        }

        private void ShowPinnedNotes()
        {
            searchField.text = "is:pinned";
            RefreshList();
            pageNavigator.ShowNotesPage();
        }

        private void CreateNewNote()
        {
            CommitEditor();
            NoteData note = NoteData.Create();
            notes.Add(note);
            searchField.text = string.Empty;
            SelectNote(note);
            RefreshList();
            titleField.ActivateInputField();
            QueueSave();
        }

        private void DeleteSelectedNote()
        {
            if (selectedNote == null)
                return;

            int oldIndex = SortedNotes().IndexOf(selectedNote);
            notes.Remove(selectedNote);
            selectedNote = null;
            RefreshList();

            List<NoteData> remaining = SortedNotes();
            if (remaining.Count > 0)
                SelectNote(remaining[Mathf.Clamp(oldIndex, 0, remaining.Count - 1)]);
            else
            {
                ShowEmptyEditor();
                pageNavigator.ShowNotesPage();
            }

            SaveImmediately();
        }

        private void TogglePin()
        {
            if (selectedNote == null)
                return;

            selectedNote.pinned = !selectedNote.pinned;
            selectedNote.modifiedUtcTicks = DateTime.UtcNow.Ticks;
            pinButtonText.text = selectedNote.pinned ? "Pinned" : "Pin";
            RefreshList();
            QueueSave();
            pageNavigator.ShowNotesPage();
        }

        private void SelectNote(NoteData note, bool navigateToEditor = true)
        {
            if (note == null)
                return;

            CommitEditor();
            selectedNote = note;
            suppressEditorEvents = true;
            titleField.text = note.title ?? string.Empty;
            bodyField.text = note.body ?? string.Empty;
            createdText.text = FormatCreated(note);
            pinButtonText.text = note.pinned ? "Pinned" : "Pin";
            suppressEditorEvents = false;
            editorPanel.SetActive(true);
            RefreshList();

            if (navigateToEditor)
                pageNavigator.ShowEditorPage();
        }

        private void ShowEmptyEditor()
        {
            suppressEditorEvents = true;
            titleField.text = string.Empty;
            bodyField.text = string.Empty;
            createdText.text = string.Empty;
            suppressEditorEvents = false;
            editorPanel.SetActive(false);
        }

        private void OnTitleChanged(string value)
        {
            if (suppressEditorEvents || selectedNote == null)
                return;

            selectedNote.title = value;
            TouchSelected();
        }

        private void OnBodyChanged(string value)
        {
            if (suppressEditorEvents || selectedNote == null)
                return;

            selectedNote.body = value;
            TouchSelected();
        }

        private void TouchSelected()
        {
            // Editing moves the note to the top of the list and starts the autosave timer.
            selectedNote.modifiedUtcTicks = DateTime.UtcNow.Ticks;
            RefreshList();
            QueueSave();
        }

        private void CommitEditor()
        {
            if (selectedNote == null || suppressEditorEvents)
                return;

            selectedNote.title = titleField.text;
            selectedNote.body = bodyField.text;
        }

        private void RefreshList()
        {
            if (notesContent == null)
                return;

            // Cards are lightweight view objects. The NoteData objects remain in memory;
            // rebuilding the visible cards is what applies search and sorting.
            foreach (GameObject card in noteCards)
                Destroy(card);
            noteCards.Clear();

            string query = searchField != null ? searchField.text.Trim() : string.Empty;
            bool pinnedOnly = string.Equals(query, "is:pinned", StringComparison.OrdinalIgnoreCase);
            IEnumerable<NoteData> filtered = SortedNotes();

            if (pinnedOnly)
                filtered = filtered.Where(note => note.pinned);
            else if (!string.IsNullOrEmpty(query))
                filtered = filtered.Where(note =>
                    (note.title ?? string.Empty).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    (note.body ?? string.Empty).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0);

            List<NoteData> visible = filtered.ToList();
            notesHeading.text = pinnedOnly ? $"Pinned  {visible.Count}" : $"All notes  {visible.Count}";
            emptyMessage.gameObject.SetActive(visible.Count == 0);

            foreach (NoteData note in visible)
                BuildNoteCard(note);
        }

        private void BuildNoteCard(NoteData note)
        {
            bool active = note == selectedNote;
            GameObject cardObject = Panel("Note", notesContent, active ? Ink : Soft);
            noteCards.Add(cardObject);
            cardObject.AddComponent<LayoutElement>().preferredHeight = 124f;

            Button button = cardObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = active ? new Color32(48, 48, 48, 255) : new Color32(225, 225, 225, 255);
            colors.pressedColor = new Color32(195, 195, 195, 255);
            button.colors = colors;
            button.targetGraphic = cardObject.GetComponent<Image>();
            button.onClick.AddListener(() => SelectNote(note));

            // Runtime cards need explicit label bounds so TMP can render them on the
            // first frame, without waiting for a nested layout-group rebuild.
            GameObject contentObject = new GameObject("Card content", typeof(RectTransform));
            contentObject.transform.SetParent(cardObject.transform, false);
            Stretch(contentObject.GetComponent<RectTransform>(), 16f, 16f, 12f, 10f);

            string title = string.IsNullOrWhiteSpace(note.title) ? "Untitled note" : note.title.Trim();
            if (note.pinned)
                title = "PINNED  ·  " + title;
            TMP_Text titleText = Label(title, contentObject.transform, 19, FontStyles.Bold, active ? White : Ink);
            SetCardTextRect(titleText, 0f, 26f);

            string preview = string.IsNullOrWhiteSpace(note.body)
                ? "No additional text"
                : note.body.Replace("\r", " ").Replace("\n", " ").Trim();
            TMP_Text previewText = Label(preview, contentObject.transform, 17, FontStyles.Normal, active ? new Color32(210, 210, 210, 255) : Muted);
            SetCardTextRect(previewText, 31f, 24f);

            TMP_Text dateText = Label(FormatCreated(note), contentObject.transform, 14, FontStyles.Normal, active ? new Color32(190, 190, 190, 255) : Muted);
            SetCardTextRect(dateText, 62f, 22f);
        }

        private List<NoteData> SortedNotes()
        {
            return notes
                .OrderByDescending(note => note.pinned)
                .ThenByDescending(note => note.modifiedUtcTicks)
                .ToList();
        }

        private bool RepairMissingCreationDates()
        {
            bool repaired = false;
            long now = DateTime.UtcNow.Ticks;

            foreach (NoteData note in notes)
            {
                if (note.createdUtcTicks > DateTime.MinValue.Ticks &&
                    note.createdUtcTicks <= DateTime.MaxValue.Ticks)
                    continue;

                note.createdUtcTicks =
                    note.modifiedUtcTicks > DateTime.MinValue.Ticks &&
                    note.modifiedUtcTicks <= DateTime.MaxValue.Ticks
                        ? note.modifiedUtcTicks
                        : now;
                repaired = true;
            }

            return repaired;
        }

        private static string FormatCreated(NoteData note)
        {
            long ticks = note.createdUtcTicks > DateTime.MinValue.Ticks &&
                         note.createdUtcTicks <= DateTime.MaxValue.Ticks
                ? note.createdUtcTicks
                : DateTime.UtcNow.Ticks;
            DateTime created = new DateTime(ticks, DateTimeKind.Utc).ToLocalTime();
            return $"Created {created:MMM d, yyyy} · {created:h:mm tt}";
        }

        private static void SetCardTextRect(TMP_Text text, float top, float height)
        {
            RectTransform rect = text.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -top);
            rect.sizeDelta = new Vector2(0f, height);

            text.enableAutoSizing = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
        }

        private void QueueSave()
        {
            // Waiting briefly prevents a disk write for every individual keystroke.
            saveQueued = true;
            saveAt = Time.unscaledTime + 0.45f;
            SetStatus("Saving…");
        }

        private void SaveImmediately()
        {
            if (titleField != null)
                CommitEditor();
            saveQueued = false;
            store.Save(notes);
            SetStatus("Saved");
        }

        private void SetStatus(string value)
        {
            if (statusText != null)
                statusText.text = value;
        }

        private void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null)
                return;

            GameObject eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<InputSystemUIInputModule>();
        }

        private GameObject Panel(string name, Transform parent, Color color)
        {
            GameObject panel = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panel.transform.SetParent(parent, false);
            panel.GetComponent<Image>().color = color;
            return panel;
        }

        private TMP_Text Label(string value, Transform parent, int size, FontStyles style, Color color)
        {
            GameObject label = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            label.transform.SetParent(parent, false);
            TMP_Text text = label.GetComponent<TMP_Text>();
            text.font = font;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.text = value;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Truncate;
            text.raycastTarget = false;
            return text;
        }

        private Button CreateButton(string label, Transform parent, Color background, Color foreground, int size, FontStyles style)
        {
            GameObject buttonObject = Panel(label, parent, background);
            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = buttonObject.GetComponent<Image>();

            ColorBlock colors = button.colors;
            colors.highlightedColor = background == Ink ? new Color32(45, 45, 45, 255) : new Color32(225, 225, 225, 255);
            colors.pressedColor = new Color32(185, 185, 185, 255);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;

            TMP_Text text = Label(label, buttonObject.transform, size, style, foreground);
            Stretch(text.rectTransform, 14f, 14f, 0f, 0f);
            text.alignment = TextAlignmentOptions.MidlineLeft;

            buttonObject.AddComponent<LayoutElement>();
            return button;
        }

        private TMP_InputField CreateInput(string placeholderValue, Transform parent, int size, bool multiline)
        {
            GameObject inputObject = Panel(placeholderValue, parent, Soft);
            TMP_InputField input = inputObject.AddComponent<TMP_InputField>();
            input.lineType = multiline ? TMP_InputField.LineType.MultiLineNewline : TMP_InputField.LineType.SingleLine;
            input.contentType = TMP_InputField.ContentType.Standard;
            input.textViewport = inputObject.GetComponent<RectTransform>();
            input.caretColor = Ink;
            input.selectionColor = new Color32(170, 170, 170, 120);
            input.customCaretColor = true;

            TMP_Text text = Label(string.Empty, inputObject.transform, size, FontStyles.Normal, Ink);
            Stretch(text.rectTransform, multiline ? 4f : 14f, 10f, multiline ? 8f : 5f, 5f);
            text.alignment = multiline ? TextAlignmentOptions.TopLeft : TextAlignmentOptions.MidlineLeft;
            text.overflowMode = multiline ? TextOverflowModes.Overflow : TextOverflowModes.Truncate;
            text.richText = false;

            TMP_Text placeholder = Label(placeholderValue, inputObject.transform, size, FontStyles.Normal, new Color32(145, 145, 145, 255));
            Stretch(placeholder.rectTransform, multiline ? 4f : 14f, 10f, multiline ? 8f : 5f, 5f);
            placeholder.alignment = multiline ? TextAlignmentOptions.TopLeft : TextAlignmentOptions.MidlineLeft;
            placeholder.fontStyle = placeholderValue == "Title" ? FontStyles.Bold : FontStyles.Normal;

            input.textComponent = text;
            input.placeholder = placeholder;
            inputObject.AddComponent<LayoutElement>();
            return input;
        }

        private static void Spacer(Transform parent, float height)
        {
            GameObject spacer = new GameObject("Spacer", typeof(RectTransform), typeof(LayoutElement));
            spacer.transform.SetParent(parent, false);
            spacer.GetComponent<LayoutElement>().preferredHeight = height;
        }

        private static void Stretch(RectTransform rect, float left = 0f, float right = 0f, float top = 0f, float bottom = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }
    }
}
