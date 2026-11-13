using System;
using TMPro;

namespace XRDiscovery.Vocabulary
{
    internal sealed class VocabularyScreens
    {
        private readonly VocabularyView view;
        public VocabularyScreens(VocabularyView view) { this.view = view; }
        public void MainMenu(bool japanese, bool storageAvailable, string storageNotice, Action study, Action test, Action manage, Action toggleLanguage, Action recenter)
        {
            var heading = view.Text("Heading", "Vocabulary Learning Center", 60, 100, 1080, 100, 58);
            heading.alignment = TextAlignmentOptions.Center;
            view.MenuButton("Study", "Study", "Learn words with 3D objects.", 60, 310, 520, 220,
                study);
            view.MenuButton("Test", "Games", "Test you knowledge with vocabulary games.", 620, 310, 520, 220,
                test);
            view.Button("CustomSets", "My Custom Vocabulary Sets", 60, 550, 1080, 80, manage, enabled: storageAvailable);
            view.Button("PromptLanguage", "Test prompts: " + (japanese ? "Japanese" : "English"), 60, 665, 700, 65, toggleLanguage);
            view.Button("Recenter", "Recenter", 800, 665, 340, 65, recenter);
            var help = view.Text("Help", string.IsNullOrEmpty(storageNotice) ? "Point and press the trigger to select. Mouse and keyboard work in the Editor." : storageNotice,
                60, 810, 1080, 70, 23, VocabularyView.Muted);
            help.alignment = TextAlignmentOptions.Center;
        }
        public void Header(string title, Action back, bool centerTitle = false)
        {
            view.Button("Back", "← Back", 48, 35, 175, 60, back);
            var titleText = centerTitle
                ? view.Text("Title", title, 48, 28, 1104, 78, 38)
                : view.Text("Title", title, 255, 28, 897, 78, 38);
            if (centerTitle) titleText.alignment = TextAlignmentOptions.Center;
        }
        public void Pager(int page, int pages, Action<int> change, float y)
        {
            view.Button("PreviousPage", "← Previous", 48, y, 330, 60, () => change(page - 1), enabled: page > 0);
            var label = view.Text("PageCount", $"{page + 1} / {pages}", 430, y, 340, 60, 28);
            label.alignment = TextAlignmentOptions.Midline;
            view.Button("NextPage", "Next →", 822, y, 330, 60, () => change(page + 1), enabled: page + 1 < pages);
        }
        public void Confirm(string title, string description, Action accept, Action cancel)
        {
            view.Text("ConfirmTitle", title, 60, 180, 1080, 150, 44);
            view.Text("ConfirmDescription", description, 60, 360, 1080, 200, 30, VocabularyView.Muted);
            view.Button("Confirm", "Confirm", 60, 650, 520, 85, accept, true);
            view.Button("Cancel", "Go back", 620, 650, 520, 85, cancel);
        }
        public void Failure(string title, string message, Action back)
        {
            Header(title, back);
            view.Text("Error", message, 48, 180, 1104, 500, 30);
        }
    }
}
