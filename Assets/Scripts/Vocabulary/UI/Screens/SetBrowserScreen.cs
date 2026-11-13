using System;
using System.Collections.Generic;
using UnityEngine;

namespace XRDiscovery.Vocabulary
{
    internal sealed class SetBrowserScreen
    {
        private readonly VocabularyView view;
        private readonly VocabularyScreens screens;
        public SetBrowserScreen(VocabularyView view, VocabularyScreens screens) { this.view = view; this.screens = screens; }
        public void Browse(BrowsePurpose purpose, bool customTab, bool storageAvailable, IReadOnlyList<SetChoice> sets,
            int browserPage, int pages, Func<SetChoice, int> availableCount, Action back, Action create,
            Action template, Action<bool> tab, Action<SetChoice> select, Action<int> changePage)
        {
            string title = purpose == BrowsePurpose.Manage ? "My custom sets" : purpose == BrowsePurpose.Template ? "Choose a starting template" : "Choose a set to " + purpose.ToString().ToLowerInvariant();
            screens.Header(title, back);
            if (purpose == BrowsePurpose.Manage)
            {
                view.Button("NewBlankSet", "Create empty set", 48, 132, 530, 65, create, true);
                view.Button("NewFromTemplate", "Create from a template", 622, 132, 530, 65, template, true);
            }
            else
            {
                view.Button("PremadeTab", "Premade sets", 48, 132, 530, 65, () => tab(false), !customTab);
                view.Button("CustomTab", "My custom sets", 622, 132, 530, 65, () => tab(true), customTab, storageAvailable);
            }
            const int pageSize = 8;
            for (int i = 0; i < pageSize && browserPage * pageSize + i < sets.Count; i++)
            {
                var choice = sets[browserPage * pageSize + i];
                int available = availableCount(choice);
                view.Button("Set_" + choice.Id, choice.Name + "\n" + available + " items", 48 + (i % 2) * 574, 230 + (i / 2) * 126, 530, 106,
                    () => select(choice));
            }
            if (sets.Count == 0) view.Text("Empty", "No custom sets yet. Create one from the main menu, or start from a premade template.", 70, 300, 1060, 200, 34);
            screens.Pager(browserPage, pages, changePage, 770);
            view.Text("NamespaceHelp", customTab ? "Your sets are saved separately. Names can match any premade set." : "All items includes every available vocabulary box.", 48, 847, 1104, 35, 23, VocabularyView.Muted);
        }
        public void Details(SetChoice choice, BrowsePurpose purpose, string[] itemNames, int missing,
            Action back, Action edit, Action duplicate, Action delete, Action start)
        {
            screens.Header(choice.Name, back, centerTitle: true);
            view.Text("SetType", (choice.Custom ? "MY CUSTOM SET" : "PREMADE SET") + "  ·  " + itemNames.Length + " items", 48, 125, 1104, 35, 29, VocabularyView.Muted);
            view.ScrollArea("Preview", 48, 145, 1104, 470, out var content);
            const float columnWidth = 500;
            const float rowHeight = 48;
            const float columnGap = 24;
            const float horizontalPadding = 22;
            const float verticalPadding = 12;
            int rows = Mathf.Max(1, (itemNames.Length + 1) / 2);
            content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, verticalPadding * 2 + rows * rowHeight);
            for (int i = 0; i < itemNames.Length; i++)
            {
                int column = i % 2;
                int row = i / 2;
                var item = view.Text("PreviewItem_" + i, itemNames[i],
                    horizontalPadding + column * (columnWidth + columnGap),
                    verticalPadding + row * rowHeight,
                    columnWidth, rowHeight - 4, 27, parent: content);
                item.alignment = TMPro.TextAlignmentOptions.MidlineLeft;
            }
            if (missing > 0) view.Text("Missing", $"{missing} unavailable items will be skipped. They remain in your saved set.", 48, 145, 1104, 30, 20, VocabularyView.Muted);

            if (purpose == BrowsePurpose.Manage)
            {
                view.Button("EditSet", "Edit items / rename", 48, 610, 530, 75, edit, true);
                view.Button("DuplicateSet", "Duplicate as a new set", 622, 610, 530, 75, duplicate);
                view.Button("DeleteSet", "Delete set", 48, 725, 530, 65, delete);
            }
            else
            {
                view.Button("StartSet", purpose == BrowsePurpose.Study ? "Begin studying" : "Begin test", 48, 650, 1104, 100,
                    start, true, itemNames.Length > 0);
                view.Text("Rules", string.IsNullOrWhiteSpace(choice.Description)
                    ? (purpose == BrowsePurpose.Study ? "Use Previous and Next to cycle through every item." : "Every word is asked once. After the round, retest everything or practice only the words you missed.")
                    : choice.Description, 48, 770, 1104, 95, 26, VocabularyView.Muted);
            }
        }
    }
}
