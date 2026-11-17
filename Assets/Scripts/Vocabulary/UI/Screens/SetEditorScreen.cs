using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace XRDiscovery.Vocabulary
{
    internal sealed class SetEditorScreen
    {
        private readonly VocabularyView view;
        private readonly VocabularyScreens screens;
        private RectTransform itemArea;
        private RectTransform itemContent;
        private RectTransform filterMenu;
        private UnityEngine.UI.ScrollRect itemScroll;
        private TMP_Text count, status;
        private readonly Dictionary<string, UnityEngine.UI.Button> wordButtons = new();
        private readonly Dictionary<string, UnityEngine.UI.Button> setButtons = new();
        private TMP_Text emptyWords, emptySets;
        private UnityEngine.UI.Button addFiltered, removeFiltered;
        private Action addVisibleWords, removeVisibleWords;
        public SetEditorScreen(VocabularyView view, VocabularyScreens screens) { this.view = view; this.screens = screens; }

        public void DrawSet(bool creating, string name, IReadOnlyList<VocabularyEntry> selected, int unavailable,
            Action back, Action<string> rename, Action addWords, Action save)
        {
            filterMenu = null;
            screens.Header(creating ? "Create a custom set" : "Edit your set", back);
            view.Text("NameLabel", $"SET NAME (MAX {VocabularySet.MaxNameLength} CHARACTERS)", 48, 118, 1104, 35, 22, VocabularyView.Muted);
            view.Input("SetName", name, "Name your set", 48, 155, 1104, rename, VocabularySet.MaxNameLength);
            view.Button("AddWords", "Add Words", 48, 240, 1104, 65, addWords, true);
            view.Text("SelectedCount", selected.Count + unavailable + " words in this set", 48, 325, 1104, 45, 28);
            itemArea = view.Area("SelectedWords", 0, 385, 1200, 515);
            view.ScrollArea("SelectedWords", 48, 0, 1104, 385, out var content, itemArea);
            const float rowHeight = 52;
            content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
                24 + Mathf.Max(1, selected.Count + (unavailable > 0 ? 1 : 0)) * rowHeight);
            for (int i = 0; i < selected.Count; i++)
            {
                var entry = selected[i];
                view.Text("Selected_" + entry.Id, entry.Name + "  /  " + entry.Japanese,
                    22, 12 + i * rowHeight, 1000, 46, 27, parent: content);
            }
            if (selected.Count + unavailable == 0)
                view.Text("EmptySet", "No words yet. Select Add Words to choose some.", 22, 12, 1000, 65, 30, parent: content);
            else if (unavailable > 0)
                view.Text("Unavailable", unavailable + " unavailable words are retained in this set.",
                    22, 12 + selected.Count * rowHeight, 1000, 46, 23, VocabularyView.Muted, content);
            status = view.Text("EditStatus", "", 48, 390, 1104, 30, 23, VocabularyView.Muted, itemArea);
            view.Button("SaveSet", "Save set", 48, 425, 1104, 70, save, true, parent: itemArea);
            SetKeyboardVisible(view.Keyboard.IsVisible);
        }

        public void DrawWords(bool addBySet, string search, string filter, bool selectedOnly, int selectedCount,
            Action back, Action<string> searchChanged, Action toggleSelection, Action toggleMode, Action toggleFilters)
        {
            filterMenu = null;
            // A new picker page owns a new content transform; do not retain destroyed controls.
            wordButtons.Clear();
            setButtons.Clear();
            emptyWords = null;
            emptySets = null;
            addFiltered = null;
            removeFiltered = null;
            status = null;
            screens.Header(addBySet ? "Add Sets" : "Add Words", back);
            count = view.Text("SelectedCount", selectedCount + " selected", 820, 98, 332, 32, 26);
            view.Input("ItemSearch", search, addBySet ? "Search sets" : "Search English or Japanese", 48, 135, 745, searchChanged);
            view.Button("SelectedOnly", selectedOnly ? (addBySet ? "Showing: Added" : "Showing: Selected") : "Showing: All",
                820, 135, 332, 60, toggleSelection);
            view.Button("CategoryFilter", "Filter: " + (string.IsNullOrEmpty(filter) ? (addBySet ? "All Sets" : "All Words") : filter),
                48, 215, 530, 60, toggleFilters);
            view.Button("AddBy", addBySet ? "Add/Remove By: Set" : "Add/Remove By: Word", 622, 215, 530, 60, toggleMode);
            itemArea = view.Area("ItemSelection", 0, 295, 1200, 575);
            itemScroll = view.ScrollArea("ItemSelection", 48, 0, 1104, 440, out itemContent, itemArea);
        }

        public void ShowFilters(IReadOnlyList<string> categories, string current, string allLabel, Action<string> select)
        {
            var scroll = view.ScrollArea("FilterOptions", 48, 295, 1104, 440, out var content);
            filterMenu = (RectTransform)scroll.transform;
            filterMenu.GetComponent<UnityEngine.UI.Image>().color = new Color32(255, 249, 228, 255);
            content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 16 + (categories.Count + 1) * 65);
            view.Button("Filter_All", allLabel, 18, 8, 1000, 56,
                () => select(""), string.IsNullOrEmpty(current), parent: content);
            for (int i = 0; i < categories.Count; i++)
            {
                var category = categories[i];
                view.Button("Filter_" + i, category, 18, 8 + (i + 1) * 65, 1000, 56,
                    () => select(category), category == current, parent: content);
            }
            filterMenu.gameObject.SetActive(!view.Keyboard.IsVisible);
        }

        public void Sets(IReadOnlyList<SetChoice> sets, Action<SetChoice> add)
        {
            float scrollOffset = itemContent.anchoredPosition.y;
            itemScroll.StopMovement();
            var visibleIds = new HashSet<string>();
            const float rowHeight = 112;
            itemContent.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
                24 + Mathf.Max(1, (sets.Count + 1) / 2) * rowHeight);
            for (int i = 0; i < sets.Count; i++)
            {
                var choice = sets[i];
                visibleIds.Add(choice.Id);
                if (!setButtons.TryGetValue(choice.Id, out var button))
                {
                    button = view.SetButton("SourceSet_" + choice.Id,
                        choice.Name, (choice.Custom ? "CUSTOM" : "PREMADE") + " · " + choice.Items.Count + " words",
                        22 + i % 2 * 504, 12 + i / 2 * rowHeight, 480, 100,
                        () => add(choice), parent: itemContent);
                    setButtons.Add(choice.Id, button);
                }
                SetRowVisible(button, true, 22 + i % 2 * 504, 12 + i / 2 * rowHeight);
            }
            foreach (var row in setButtons)
                if (!visibleIds.Contains(row.Key)) SetVisible(row.Value.gameObject, false);
            if (emptySets == null)
                emptySets = view.Text("NoSets", "No sets match this filter.", 22, 12, 1000, 65, 30, parent: itemContent);
            SetVisible(emptySets.gameObject, sets.Count == 0);
            itemContent.anchoredPosition = new Vector2(0,
                Mathf.Clamp(scrollOffset, 0, Mathf.Max(0, itemContent.rect.height - 440)));
            if (status == null)
                status = view.Text("PickerStatus", "Select a set to add or remove it.",
                    48, 455, 1104, 65, 27, VocabularyView.Muted, itemArea);
            SetKeyboardVisible(view.Keyboard.IsVisible);
        }

        public void SetPreview(SetChoice choice, VocabularyEntry[] available, int unavailable,
            HashSet<string> selected, Action back, Action add, Action remove)
        {
            itemArea = null;
            filterMenu = null;
            screens.Header(choice.Name, back);
            int alreadyAdded = 0;
            foreach (var entry in available)
                if (selected.Contains(entry.Id)) alreadyAdded++;
            int newWords = available.Length - alreadyAdded;
            view.Text("PreviewSummary",
                (choice.Custom ? "CUSTOM SET" : "PREMADE SET") + "  ·  " + available.Length +
                " words  ·  " + alreadyAdded + " already added", 48, 125, 1104, 45, 27, VocabularyView.Muted);
            view.ScrollArea("SetWords", 48, 185, 1104, 515, out var content);
            const float rowHeight = 52;
            content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
                24 + Mathf.Max(1, available.Length) * rowHeight);
            for (int i = 0; i < available.Length; i++)
            {
                var entry = available[i];
                view.Text("PreviewWord_" + entry.Id,
                    (selected.Contains(entry.Id) ? "✓  " : "+  ") + entry.Name + "  /  " + entry.Japanese,
                    22, 12 + i * rowHeight, 1000, 46, 27, parent: content);
            }
            if (available.Length == 0)
                view.Text("NoPreviewWords", "This set has no available words.", 22, 12, 1000, 65, 30, parent: content);
            if (unavailable > 0)
                view.Text("UnavailableWords", unavailable + " unavailable words will be skipped.",
                    48, 715, 1104, 45, 23, VocabularyView.Muted);
            view.Button("AddSetWords", newWords > 0 ? "Add " + newWords + " New Words" : "All Words Already Added",
                48, 785, 530, 80, add, true, newWords > 0);
            view.Button("RemoveSetWords", alreadyAdded > 0 ? "Remove " + alreadyAdded + " Words" : "No Words to Remove",
                622, 785, 530, 80, remove, enabled: alreadyAdded > 0);
        }

        public void Words(VocabularyEntry[] visible, HashSet<string> selected,
            Action<string> toggle, Action addMatching, Action removeMatching)
        {
            float scrollOffset = itemContent.anchoredPosition.y;
            itemScroll.StopMovement();
            var visibleIds = new HashSet<string>();
            addVisibleWords = addMatching;
            removeVisibleWords = removeMatching;
            count.text = selected.Count + " selected";
            const float rowHeight = 78;
            itemContent.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
                24 + Mathf.Max(1, visible.Length) * rowHeight);
            for (int i = 0; i < visible.Length; i++)
            {
                var entry = visible[i];
                visibleIds.Add(entry.Id);
                bool included = selected.Contains(entry.Id);
                string label = (included ? "✓  " : "+  ") + entry.Name + "  /  " + entry.Japanese;
                if (!wordButtons.TryGetValue(entry.Id, out var button))
                {
                    button = view.Button("Item_" + entry.Id, label, 22, 12 + i * rowHeight, 1000, 65,
                        () => toggle(entry.Id), included, parent: itemContent);
                    wordButtons.Add(entry.Id, button);
                }
                else
                    view.UpdateButton(button, label, included);
                SetRowVisible(button, true, 22, 12 + i * rowHeight);
            }
            foreach (var row in wordButtons)
                if (!visibleIds.Contains(row.Key)) SetVisible(row.Value.gameObject, false);
            if (emptyWords == null)
                emptyWords = view.Text("NoMatches", "No items match this filter.", 22, 12, 1000, 65, 32, parent: itemContent);
            SetVisible(emptyWords.gameObject, visible.Length == 0);
            itemContent.anchoredPosition = new Vector2(0,
                Mathf.Clamp(scrollOffset, 0, Mathf.Max(0, itemContent.rect.height - 440)));
            if (addFiltered == null)
                addFiltered = view.Button("AddFiltered", "Add All Displayed", 48, 455, 530, 65,
                    () => addVisibleWords?.Invoke(), parent: itemArea);
            if (removeFiltered == null)
                removeFiltered = view.Button("RemoveFiltered", "Remove All Displayed", 622, 455, 530, 65,
                    () => removeVisibleWords?.Invoke(), parent: itemArea);
            SetKeyboardVisible(view.Keyboard.IsVisible);
        }

        private static void SetVisible(GameObject target, bool visible)
        {
            if (target.activeSelf != visible) target.SetActive(visible);
        }

        private static void SetRowVisible(UnityEngine.UI.Button button, bool visible, float x, float y)
        {
            var rect = (RectTransform)button.transform;
            var position = new Vector2(x, -y);
            if (rect.anchoredPosition != position) rect.anchoredPosition = position;
            SetVisible(button.gameObject, visible);
        }

        public void Status(string message) { if (status != null) status.text = message; }
        public void ScrollToTop()
        {
            if (itemScroll != null) itemScroll.StopMovement();
            if (itemContent != null) itemContent.anchoredPosition = Vector2.zero;
        }
        public void SetKeyboardVisible(bool visible)
        {
            if (itemArea != null) itemArea.gameObject.SetActive(!visible);
            if (filterMenu != null) filterMenu.gameObject.SetActive(!visible);
        }
    }
}
