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
        private TMP_Text count, status;
        public SetEditorScreen(VocabularyView view, VocabularyScreens screens) { this.view = view; this.screens = screens; }

        public void Draw(bool creating, string name, string search, string category, bool selectedOnly,
            Action back, Action<string> rename, Action<string> searchChanged, Action toggleSelection, Action nextCategory)
        {
            screens.Header(creating ? "Create a custom set" : "Edit your set", back);
            view.Text("NameLabel", "SET NAME", 48, 118, 1104, 35, 22, VocabularyView.Muted);
            view.Input("SetName", name, "Name your set", 48, 155, 1104, rename);
            view.Input("ItemSearch", search, "Search English, Japanese or reading", 48, 240, 745, searchChanged);
            view.Button("SelectedOnly", selectedOnly ? "Showing: selected" : "Showing: all", 820, 240, 332, 60, toggleSelection);
            view.Button("CategoryFilter", "Category: " + (string.IsNullOrEmpty(category) ? "All items" : category) + "  →", 48, 320, 745, 60, nextCategory);
            count = view.Text("SelectedCount", "", 820, 320, 332, 60, 28);
            itemArea = view.Area("ItemSelection", 0, 405, 1200, 485);
        }

        public void Items(VocabularyEntry[] visible, HashSet<string> selected, int page, int pages, int matchCount,
            Action<string> toggle, Action<int> changePage, Action addMatching, Action removeMatching, Action save)
        {
            for (int i = itemArea.childCount - 1; i >= 0; i--)
            {
                var child = itemArea.GetChild(i).gameObject;
                child.SetActive(false);
                UnityEngine.Object.Destroy(child);
            }
            count.text = selected.Count + " selected";
            for (int i = 0; i < visible.Length; i++)
            {
                var entry = visible[i];
                bool included = selected.Contains(entry.Id);
                view.Button("Item_" + entry.Id, (included ? "✓  " : "+  ") + entry.Name + "  /  " + entry.Japanese,
                    48 + i % 2 * 574, i / 2 * 78, 530, 65, () => toggle(entry.Id), included, parent: itemArea);
            }
            if (visible.Length == 0) view.Text("NoMatches", "No items match this filter.", 48, 20, 1104, 160, 32, parent: itemArea);
            view.Button("PreviousPage", "←", 48, 245, 100, 55, () => changePage(page - 1), enabled: page > 0, parent: itemArea);
            view.Text("PageCount", $"{page + 1} / {pages}  ·  {matchCount} matching", 175, 245, 500, 55, 26, parent: itemArea);
            view.Button("NextPage", "→", 680, 245, 100, 55, () => changePage(page + 1), enabled: page + 1 < pages, parent: itemArea);
            view.Button("AddFiltered", "Add matching", 810, 245, 342, 55, addMatching, parent: itemArea);
            view.Button("RemoveFiltered", "Remove matching", 810, 312, 342, 55, removeMatching, parent: itemArea);
            status = view.Text("EditStatus", "Templates and other sets stay unchanged.", 48, 315, 735, 55, 23, VocabularyView.Muted, itemArea);
            view.Button("SaveSet", "Save set", 48, 392, 1104, 75, save, true, parent: itemArea);
            SetKeyboardVisible(view.Keyboard.IsVisible);
        }

        public void Status(string message) { if (status != null) status.text = message; }
        public void SetKeyboardVisible(bool visible) { if (itemArea != null) itemArea.gameObject.SetActive(!visible); }
    }
}
