using System;
using System.Linq;
using System.Reflection;
using ManWellNotes;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace ManWellNotesEditor
{
    public static class InteractionValidation
    {
        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Single);
            NotesApp app = UnityEngine.Object.FindFirstObjectByType<NotesApp>(FindObjectsInactive.Include);
            if (app == null)
                throw new InvalidOperationException("NotesApp was not found.");

            app.UpgradeInteractionInterface();
            SerializedObject data = new SerializedObject(app);
            data.Update();
            RequireReference(data, "deleteDialog");
            RequireReference(data, "deleteDialogMessage");
            RequireReference(data, "confirmDeleteButton");
            RequireReference(data, "cancelDeleteButton");
            RequireReference(data, "noteCardPrefab");
            RequireReference(data, "pinnedIconBlack");
            RequireReference(data, "pinnedIconWhite");
            TMP_Text limitMessage =
                data.FindProperty("inputLimitMessage").objectReferenceValue as TMP_Text;
            if (limitMessage == null || limitMessage.color.r <= limitMessage.color.g)
                throw new InvalidOperationException("The red input-limit message is not configured.");

            TMP_InputField title = data.FindProperty("titleField").objectReferenceValue as TMP_InputField;
            if (title == null || !title.textComponent.enableAutoSizing ||
                title.textComponent.fontSizeMin != 18f ||
                !NativeMobileEditingIsSerialized(title))
                throw new InvalidOperationException("Accessible title autosizing is not configured.");

            TMP_InputField body = data.FindProperty("bodyField").objectReferenceValue as TMP_InputField;
            if (body == null || !NativeMobileEditingIsSerialized(body))
                throw new InvalidOperationException("Native mobile text editing is not enabled.");

            GameObject editor = data.FindProperty("editorPanel").objectReferenceValue as GameObject;
            if (editor == null)
                throw new InvalidOperationException("The editor page is not configured.");
            editor.SetActive(true);
            Canvas.ForceUpdateCanvases();

            Scrollbar bodyScrollbar =
                data.FindProperty("bodyScrollbar").objectReferenceValue as Scrollbar;
            if (bodyScrollbar == null ||
                bodyScrollbar.direction != Scrollbar.Direction.TopToBottom ||
                body.GetComponent<RectMask2D>() == null ||
                body.textComponent.rectTransform.offsetMax.x > -30f)
                throw new InvalidOperationException("The long-note scrollbar is not configured.");

            string originalBody = body.text;
            body.SetTextWithoutNotify(string.Join("\n", Enumerable.Repeat("Long note line", 80)));
            MethodInfo refreshBodyScroll = typeof(NotesApp).GetMethod(
                "RefreshBodyScrollState",
                BindingFlags.Instance | BindingFlags.NonPublic);
            refreshBodyScroll?.Invoke(app, new object[] { true });
            if (body.verticalScrollbar != bodyScrollbar || !bodyScrollbar.gameObject.activeSelf)
                throw new InvalidOperationException("The scrollbar does not activate for a long note.");
            body.SetTextWithoutNotify(originalBody);
            refreshBodyScroll?.Invoke(app, new object[] { true });

            VerticalLayoutGroup editorLayout = editor != null
                ? editor.GetComponent<VerticalLayoutGroup>()
                : null;
            LayoutElement deleteSize = editor != null
                ? editor.GetComponentsInChildren<LayoutElement>(true)
                    .FirstOrDefault(item => item.gameObject.name == "Delete")
                : null;
            Transform toolbar = editor != null ? editor.transform.Find("Toolbar") : null;
            HorizontalLayoutGroup toolbarLayout = toolbar != null
                ? toolbar.GetComponent<HorizontalLayoutGroup>()
                : null;
            if (editorLayout == null || editorLayout.padding.left > 20 ||
                deleteSize == null || deleteSize.preferredWidth > 84f ||
                toolbarLayout == null || !toolbarLayout.childControlWidth ||
                toolbarLayout.childForceExpandWidth)
                throw new InvalidOperationException("The editor toolbar can place Delete off-screen.");

            string[] toolbarButtonNames = { "Back to notes", "Save", "Delete" };
            if (toolbarButtonNames.Any(buttonName =>
            {
                Transform button = toolbar != null ? toolbar.Find(buttonName) : null;
                Image image = button != null ? button.GetComponent<Image>() : null;
                return image == null || !image.preserveAspect;
            }))
                throw new InvalidOperationException("An editor toolbar icon does not preserve its aspect ratio.");

            if (UnityEngine.Object.FindObjectsByType<Transform>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None).Any(item => item.name == "Editing tools"))
                throw new InvalidOperationException("The obsolete custom clipboard toolbar still exists.");

            NoteData note = NoteData.Create();
            note.pinned = true;
            note.title = "A deliberately long visible note title for card validation";
            note.body = "Visible preview text";

            MethodInfo buildCard = typeof(NotesApp).GetMethod(
                "BuildNoteCard",
                BindingFlags.Instance | BindingFlags.NonPublic);
            buildCard?.Invoke(app, new object[] { note });
            Canvas.ForceUpdateCanvases();

            RectTransform content =
                data.FindProperty("notesContent").objectReferenceValue as RectTransform;
            Transform card = content != null
                ? content.Cast<Transform>().LastOrDefault(child => child.name == "Note")
                : null;
            if (card == null || card.GetComponent<LongPressButton>() == null)
                throw new InvalidOperationException("Runtime card or long-press control was not created.");

            if (card.Find("Starred icon")?.GetComponent<Image>()?.sprite == null)
                throw new InvalidOperationException("The starred-note icon is not configured on note cards.");

            TMP_Text[] labels = card.GetComponentsInChildren<TMP_Text>(true);
            if (labels.Length != 3 || labels.Any(label =>
                    label.rectTransform.rect.width <= 0f ||
                    label.rectTransform.rect.height <= 0f ||
                    label.color.a <= 0f ||
                    string.IsNullOrWhiteSpace(label.text)))
                throw new InvalidOperationException("A runtime note-card label has invalid visible bounds.");

            UnityEngine.Object.DestroyImmediate(card.gameObject);
            Debug.Log("Interaction validation passed: long-note scrolling, card labels, native mobile editing, title sizing, long press, and delete dialog are configured.");
        }

        private static void RequireReference(SerializedObject data, string propertyName)
        {
            if (data.FindProperty(propertyName)?.objectReferenceValue == null)
                throw new InvalidOperationException($"Missing NotesApp reference: {propertyName}");
        }

        private static bool NativeMobileEditingIsSerialized(TMP_InputField input)
        {
            SerializedObject data = new SerializedObject(input);
            return !data.FindProperty("m_HideMobileInput").boolValue &&
                   !data.FindProperty("m_HideSoftKeyboard").boolValue &&
                   !data.FindProperty("m_OnFocusSelectAll").boolValue;
        }
    }
}
