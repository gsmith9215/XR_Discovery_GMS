using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using XRDiscovery.Vocabulary;

public sealed class PremadeSetEditorWindow : EditorWindow
{
    [SerializeField] private PremadeSetDocument draft;
    [SerializeField] private string path;
    [SerializeField] private string search = "";
    [SerializeField] private int categoryIndex;
    private VocabularyLibrary library;
    private string[] files = Array.Empty<string>();
    private Vector2 fileScroll, catalogScroll, selectionScroll;
    private string error;

    [MenuItem("Tools/Vocabulary/Premade Set Editor")]
    public static void Open() => GetWindow<PremadeSetEditorWindow>("Premade Vocabulary Sets");

    private void OnEnable()
    {
        minSize = new Vector2(850, 500);
        saveChangesMessage = "Save changes to this premade vocabulary set?";
        RefreshLibrary();
    }

    private void RefreshLibrary()
    {
        try { library = VocabularyLibrary.Load(); files = PremadeSetAuthoring.Files(); error = null; }
        catch (Exception e) { error = e.Message; }
    }

    private void OnGUI()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            if (GUILayout.Button("New set", EditorStyles.toolbarButton)) NewSet();
            using (new EditorGUI.DisabledScope(draft == null))
            {
                if (GUILayout.Button("Duplicate", EditorStyles.toolbarButton)) Duplicate();
                if (GUILayout.Button("Save", EditorStyles.toolbarButton)) Save();
            }
            if (GUILayout.Button("Refresh catalog", EditorStyles.toolbarButton)) RefreshLibrary();
            if (GUILayout.Button("Validate all", EditorStyles.toolbarButton))
            {
                try { PremadeSetAuthoring.Validate(); error = null; }
                catch (Exception e) { error = e.Message; }
            }
            GUILayout.FlexibleSpace();
        }
        if (!string.IsNullOrEmpty(error)) EditorGUILayout.HelpBox(error, MessageType.Error);
        using (new EditorGUILayout.HorizontalScope())
        {
            DrawFiles();
            using (new EditorGUILayout.VerticalScope())
            {
                if (draft == null) EditorGUILayout.HelpBox("Select a premade set or create one. Set membership is independent of prefab folders.", MessageType.Info);
                else if (library != null) DrawDraft();
            }
        }
    }

    private void DrawFiles()
    {
        using (new EditorGUILayout.VerticalScope(GUILayout.Width(210)))
        {
            EditorGUILayout.LabelField("Premade sets", EditorStyles.boldLabel);
            fileScroll = EditorGUILayout.BeginScrollView(fileScroll);
            foreach (string file in files)
            {
                string label;
                try { label = PremadeSetAuthoring.Read(file).name ?? Path.GetFileNameWithoutExtension(file); }
                catch { label = Path.GetFileName(file) + " (invalid)"; }
                if (GUILayout.Button(label, file == path ? EditorStyles.miniButtonMid : EditorStyles.miniButton)) Load(file);
            }
            EditorGUILayout.EndScrollView();
        }
    }

    private void DrawDraft()
    {
        using (new EditorGUI.DisabledScope(true)) EditorGUILayout.TextField("Stable ID", draft.id);
        EditorGUI.BeginChangeCheck();
        draft.name = EditorGUILayout.TextField("Name", draft.name);
        draft.description = EditorGUILayout.TextField("Description", draft.description);
        draft.sortOrder = EditorGUILayout.IntField("Menu order", draft.sortOrder);
        if (EditorGUI.EndChangeCheck()) hasUnsavedChanges = true;
        EditorGUILayout.LabelField("Study follows the selected item order. Tests shuffle targets.", EditorStyles.miniLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            DrawCatalog();
            DrawSelected();
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField(string.IsNullOrEmpty(path) ? "New set — save to create its JSON file." : path, EditorStyles.miniLabel);
            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(path)))
                if (GUILayout.Button("Delete set", GUILayout.Width(100))) Delete();
        }
    }

    private void DrawCatalog()
    {
        using (new EditorGUILayout.VerticalScope(GUILayout.Width(Mathf.Max(260, (position.width - 226) * .5f))))
        {
            EditorGUILayout.LabelField("Catalog", EditorStyles.boldLabel);
            float previousLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 65;
            search = EditorGUILayout.TextField("Search", search);
            var categories = new[] { "All categories" }.Concat(library.Categories).ToArray();
            categoryIndex = EditorGUILayout.Popup("Category", Mathf.Clamp(categoryIndex, 0, categories.Length - 1), categories);
            EditorGUIUtility.labelWidth = previousLabelWidth;
            var selected = new HashSet<string>(draft.itemIds ?? Array.Empty<string>());
            var matches = library.Entries.Where(e => (categoryIndex == 0 || e.Category == categories[categoryIndex])
                && (string.IsNullOrWhiteSpace(search) || (e.Name + " " + e.Japanese + " " + e.Reading).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)).ToArray();
            if (GUILayout.Button("Add matching (" + matches.Length + ")"))
                ChangeItems((draft.itemIds ?? Array.Empty<string>()).Concat(matches.Select(e => e.Id)).Distinct().ToArray());
            catalogScroll = EditorGUILayout.BeginScrollView(catalogScroll);
            foreach (var entry in matches)
            {
                bool included = selected.Contains(entry.Id);
                bool next = EditorGUILayout.ToggleLeft(entry.Name + " / " + entry.Japanese, included);
                if (next != included)
                    ChangeItems(next ? draft.itemIds.Concat(new[] { entry.Id }).ToArray() : draft.itemIds.Where(id => id != entry.Id).ToArray());
            }
            EditorGUILayout.EndScrollView();
        }
    }

    private void DrawSelected()
    {
        using (new EditorGUILayout.VerticalScope(GUILayout.Width(Mathf.Max(260, (position.width - 226) * .5f))))
        {
            EditorGUILayout.LabelField("Selected (" + draft.itemIds.Length + ")", EditorStyles.boldLabel);
            selectionScroll = EditorGUILayout.BeginScrollView(selectionScroll);
            for (int i = 0; i < draft.itemIds.Length; i++)
            {
                string id = draft.itemIds[i];
                int move = 0;
                bool remove;
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField((i + 1) + ". " + (library.Get(id)?.Name ?? "Missing: " + id),
                        GUILayout.Width(Mathf.Max(150, (position.width - 226) * .5f - 110)));
                    using (new EditorGUI.DisabledScope(i == 0)) if (GUILayout.Button("↑", GUILayout.Width(26))) move = -1;
                    using (new EditorGUI.DisabledScope(i == draft.itemIds.Length - 1)) if (GUILayout.Button("↓", GUILayout.Width(26))) move = 1;
                    remove = GUILayout.Button("×", GUILayout.Width(26));
                }
                if (remove) { ChangeItems(draft.itemIds.Where((_, index) => index != i).ToArray()); break; }
                if (move != 0)
                {
                    var members = (string[])draft.itemIds.Clone();
                    (members[i], members[i + move]) = (members[i + move], members[i]);
                    ChangeItems(members); break;
                }
            }
            EditorGUILayout.EndScrollView();
        }
    }

    private void ChangeItems(string[] items) { draft.itemIds = items; hasUnsavedChanges = true; }

    private bool FinishDraft()
    {
        if (!hasUnsavedChanges) return true;
        int choice = EditorUtility.DisplayDialogComplex("Unsaved premade set", "Save changes before switching sets?", "Save", "Cancel", "Discard");
        if (choice == 0) return Save();
        return choice == 2;
    }

    private void NewSet()
    {
        if (!FinishDraft()) return;
        draft = new PremadeSetDocument { version = 1, id = "premade:" + Guid.NewGuid().ToString("N"), name = "New vocabulary set", description = "", itemIds = Array.Empty<string>() };
        path = null; error = null; hasUnsavedChanges = true;
    }

    private void Duplicate()
    {
        // Duplicate the current draft, including unsaved membership edits.
        var copy = JsonUtility.FromJson<PremadeSetDocument>(JsonUtility.ToJson(draft));
        if (!FinishDraft()) return;
        copy.id = "premade:" + Guid.NewGuid().ToString("N");
        copy.name += " copy";
        draft = copy; path = null; error = null; hasUnsavedChanges = true;
    }

    private void Load(string file)
    {
        if (!FinishDraft()) return;
        try
        {
            var loaded = PremadeSetAuthoring.Read(file);
            loaded.itemIds ??= Array.Empty<string>();
            draft = loaded; path = file; error = null; hasUnsavedChanges = false;
        }
        catch (Exception e) { error = e.Message; }
    }

    private bool Save()
    {
        if (draft == null || library == null) return false;
        try
        {
            string destination = path ?? PremadeSetAuthoring.Folder + "/set-" + Guid.NewGuid().ToString("N") + ".json";
            PremadeSetAuthoring.Save(destination, draft, library);
            path = destination; hasUnsavedChanges = false; error = null;
            files = PremadeSetAuthoring.Files();
            return true;
        }
        catch (Exception e) { error = e.Message; return false; }
    }

    public override void SaveChanges() { if (Save()) base.SaveChanges(); }
    public override void DiscardChanges() { draft = null; path = null; base.DiscardChanges(); }

    private void Delete()
    {
        if (!EditorUtility.DisplayDialog("Delete premade set?", "Delete “" + draft.name + "” and its JSON definition? Vocabulary objects and player saves are kept.", "Delete", "Cancel")) return;
        if (!AssetDatabase.DeleteAsset(path)) { error = "Could not delete " + path; return; }
        draft = null; path = null; hasUnsavedChanges = false;
        files = PremadeSetAuthoring.Files();
    }
}
