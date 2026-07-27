using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

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
        [SerializeField] private Text notesHeading;
        [SerializeField] private Text emptyMessage;
        [SerializeField] private InputField searchField;

        [Header("Editor")]
        [SerializeField] private GameObject editorPanel;
        [SerializeField] private InputField titleField;
        [SerializeField] private InputField bodyField;
        [SerializeField] private Button pinButton;
        [SerializeField] private Text pinButtonText;

        [Header("Status")]
        [SerializeField] private Text statusText;

        private NoteStore store;
        private readonly List<NoteData> notes = new List<NoteData>();
        private readonly List<GameObject> noteCards = new List<GameObject>();

        private Font font;
        private NoteData selectedNote;
        private bool suppressEditorEvents;
        private bool saveQueued;
        private float saveAt;

        private void Awake()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            // Unity API properties such as persistentDataPath are safe from Awake onward.
            // Constructing NoteStore here avoids accessing them in a field initializer.
            store = new NoteStore();

            // A missing reference means the scene builder was not run or a required
            // object was deleted. We stop here instead of silently creating runtime UI.
            if (notesContent == null || searchField == null || titleField == null || bodyField == null)
            {
                Debug.LogError("Man-Well Notes is not configured. Rebuild the notes interface from the Editor.");
                enabled = false;
                return;
            }

            notes.AddRange(store.Load().Where(note => note != null));
            BindEvents();
            RefreshList();

            NoteData initial = SortedNotes().FirstOrDefault();
            if (initial != null)
                SelectNote(initial);
            else
                ShowEmptyEditor();
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
        public void BuildSceneInterface()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            EnsureEventSystem();

            Canvas canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = false;

            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1440f, 900f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            GameObject background = Panel("Background", transform, Paper);
            Stretch(background.GetComponent<RectTransform>());

            GameObject shell = Panel("App", background.transform, Paper);
            Stretch(shell.GetComponent<RectTransform>(), 18f, 18f, 18f, 18f);
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

            Text brand = Label("MAN-WELL", sidebar.transform, 24, FontStyle.Bold, White);
            brand.gameObject.AddComponent<LayoutElement>().preferredHeight = 35f;

            Text subtitle = Label("NOTES", sidebar.transform, 11, FontStyle.Bold, new Color32(190, 190, 190, 255));
            subtitle.gameObject.AddComponent<LayoutElement>().preferredHeight = 26f;

            Button newButton = CreateButton("+   New note", sidebar.transform, White, Ink, 15, FontStyle.Bold);
            newButton.gameObject.name = "New note";
            newButton.GetComponent<LayoutElement>().preferredHeight = 48f;

            Spacer(sidebar.transform, 15f);
            Text library = Label("LIBRARY", sidebar.transform, 11, FontStyle.Bold, new Color32(155, 155, 155, 255));
            library.gameObject.AddComponent<LayoutElement>().preferredHeight = 22f;

            Button allNotes = CreateButton("All notes", sidebar.transform, new Color32(38, 38, 38, 255), White, 15, FontStyle.Bold);
            allNotes.gameObject.name = "All notes";
            allNotes.GetComponent<LayoutElement>().preferredHeight = 44f;

            Button pinned = CreateButton("Pinned", sidebar.transform, Ink, White, 15, FontStyle.Normal);
            pinned.gameObject.name = "Pinned";
            pinned.GetComponent<LayoutElement>().preferredHeight = 44f;

            GameObject flexible = new GameObject("Flexible space", typeof(RectTransform), typeof(LayoutElement));
            flexible.transform.SetParent(sidebar.transform, false);
            flexible.GetComponent<LayoutElement>().flexibleHeight = 1f;

            statusText = Label("Ready", sidebar.transform, 12, FontStyle.Normal, new Color32(180, 180, 180, 255));
            statusText.gameObject.AddComponent<LayoutElement>().preferredHeight = 22f;

            Text storage = Label("Stored privately on this device", sidebar.transform, 10, FontStyle.Normal, new Color32(125, 125, 125, 255));
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

            notesHeading = Label("All notes", pane.transform, 27, FontStyle.Bold, Ink);
            notesHeading.gameObject.AddComponent<LayoutElement>().preferredHeight = 38f;

            searchField = CreateInput("Search notes", pane.transform, 14, false);
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

            emptyMessage = Label("No notes yet.\nCreate one to get started.", viewport.transform, 15, FontStyle.Normal, Muted);
            Stretch(emptyMessage.rectTransform, 20f, 20f, 20f, 20f);
            emptyMessage.alignment = TextAnchor.MiddleCenter;
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

            pinButton = CreateButton("Pin", toolbar.transform, Soft, Ink, 13, FontStyle.Bold);
            pinButton.GetComponent<LayoutElement>().preferredWidth = 82f;
            pinButtonText = pinButton.GetComponentInChildren<Text>();

            GameObject toolbarSpace = new GameObject("Space", typeof(RectTransform), typeof(LayoutElement));
            toolbarSpace.transform.SetParent(toolbar.transform, false);
            toolbarSpace.GetComponent<LayoutElement>().flexibleWidth = 1f;

            Button deleteButton = CreateButton("Delete", toolbar.transform, Ink, White, 13, FontStyle.Bold);
            deleteButton.gameObject.name = "Delete";
            deleteButton.GetComponent<LayoutElement>().preferredWidth = 92f;

            titleField = CreateInput("Title", editorPanel.transform, 31, false);
            titleField.textComponent.fontStyle = FontStyle.Bold;
            titleField.GetComponent<Image>().color = Paper;
            titleField.GetComponent<LayoutElement>().preferredHeight = 65f;

            GameObject divider = Panel("Divider", editorPanel.transform, Line);
            divider.AddComponent<LayoutElement>().preferredHeight = 1f;

            bodyField = CreateInput("Start writing…", editorPanel.transform, 17, true);
            bodyField.GetComponent<Image>().color = Paper;
            LayoutElement bodySize = bodyField.GetComponent<LayoutElement>();
            bodySize.flexibleHeight = 1f;
            bodySize.minHeight = 400f;
        }

        private void ShowAllNotes()
        {
            searchField.text = string.Empty;
            RefreshList();
        }

        private void ShowPinnedNotes()
        {
            searchField.text = "is:pinned";
            RefreshList();
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
                ShowEmptyEditor();

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
        }

        private void SelectNote(NoteData note)
        {
            if (note == null)
                return;

            CommitEditor();
            selectedNote = note;
            suppressEditorEvents = true;
            titleField.text = note.title ?? string.Empty;
            bodyField.text = note.body ?? string.Empty;
            pinButtonText.text = note.pinned ? "Pinned" : "Pin";
            suppressEditorEvents = false;
            editorPanel.SetActive(true);
            RefreshList();
        }

        private void ShowEmptyEditor()
        {
            suppressEditorEvents = true;
            titleField.text = string.Empty;
            bodyField.text = string.Empty;
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
            cardObject.AddComponent<LayoutElement>().preferredHeight = 112f;

            Button button = cardObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = active ? new Color32(48, 48, 48, 255) : new Color32(225, 225, 225, 255);
            colors.pressedColor = new Color32(195, 195, 195, 255);
            button.colors = colors;
            button.targetGraphic = cardObject.GetComponent<Image>();
            button.onClick.AddListener(() => SelectNote(note));

            VerticalLayoutGroup layout = cardObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(16, 16, 13, 11);
            layout.spacing = 5f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;

            string title = string.IsNullOrWhiteSpace(note.title) ? "Untitled note" : note.title.Trim();
            if (note.pinned)
                title = "PINNED  ·  " + title;
            Text titleText = Label(title, cardObject.transform, 15, FontStyle.Bold, active ? White : Ink);
            titleText.gameObject.AddComponent<LayoutElement>().preferredHeight = 23f;

            string preview = string.IsNullOrWhiteSpace(note.body)
                ? "No additional text"
                : note.body.Replace("\r", " ").Replace("\n", " ").Trim();
            Text previewText = Label(preview, cardObject.transform, 12, FontStyle.Normal, active ? new Color32(210, 210, 210, 255) : Muted);
            previewText.gameObject.AddComponent<LayoutElement>().preferredHeight = 21f;

            DateTime modified = new DateTime(Math.Max(note.modifiedUtcTicks, DateTime.MinValue.Ticks), DateTimeKind.Utc).ToLocalTime();
            Text dateText = Label(FormatDate(modified), cardObject.transform, 10, FontStyle.Normal, active ? new Color32(160, 160, 160, 255) : Muted);
            dateText.gameObject.AddComponent<LayoutElement>().preferredHeight = 18f;
        }

        private List<NoteData> SortedNotes()
        {
            return notes
                .OrderByDescending(note => note.pinned)
                .ThenByDescending(note => note.modifiedUtcTicks)
                .ToList();
        }

        private static string FormatDate(DateTime date)
        {
            if (date.Date == DateTime.Today)
                return $"Today, {date:h:mm tt}";
            if (date.Date == DateTime.Today.AddDays(-1))
                return $"Yesterday, {date:h:mm tt}";
            return date.ToString("MMM d, yyyy");
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

        private Text Label(string value, Transform parent, int size, FontStyle style, Color color)
        {
            GameObject label = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            label.transform.SetParent(parent, false);
            Text text = label.GetComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.text = value;
            text.alignment = TextAnchor.MiddleLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }

        private Button CreateButton(string label, Transform parent, Color background, Color foreground, int size, FontStyle style)
        {
            GameObject buttonObject = Panel(label, parent, background);
            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = buttonObject.GetComponent<Image>();

            ColorBlock colors = button.colors;
            colors.highlightedColor = background == Ink ? new Color32(45, 45, 45, 255) : new Color32(225, 225, 225, 255);
            colors.pressedColor = new Color32(185, 185, 185, 255);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;

            Text text = Label(label, buttonObject.transform, size, style, foreground);
            Stretch(text.rectTransform, 14f, 14f, 0f, 0f);
            text.alignment = TextAnchor.MiddleLeft;

            buttonObject.AddComponent<LayoutElement>();
            return button;
        }

        private InputField CreateInput(string placeholderValue, Transform parent, int size, bool multiline)
        {
            GameObject inputObject = Panel(placeholderValue, parent, Soft);
            InputField input = inputObject.AddComponent<InputField>();
            input.lineType = multiline ? InputField.LineType.MultiLineNewline : InputField.LineType.SingleLine;
            input.contentType = multiline ? InputField.ContentType.Standard : InputField.ContentType.Standard;
            input.caretColor = Ink;
            input.selectionColor = new Color32(170, 170, 170, 120);
            input.customCaretColor = true;

            Text text = Label(string.Empty, inputObject.transform, size, FontStyle.Normal, Ink);
            Stretch(text.rectTransform, multiline ? 4f : 14f, 10f, multiline ? 8f : 5f, 5f);
            text.alignment = multiline ? TextAnchor.UpperLeft : TextAnchor.MiddleLeft;
            text.verticalOverflow = multiline ? VerticalWrapMode.Overflow : VerticalWrapMode.Truncate;
            text.supportRichText = false;

            Text placeholder = Label(placeholderValue, inputObject.transform, size, FontStyle.Normal, new Color32(145, 145, 145, 255));
            Stretch(placeholder.rectTransform, multiline ? 4f : 14f, 10f, multiline ? 8f : 5f, 5f);
            placeholder.alignment = multiline ? TextAnchor.UpperLeft : TextAnchor.MiddleLeft;
            placeholder.fontStyle = placeholderValue == "Title" ? FontStyle.Bold : FontStyle.Normal;

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
