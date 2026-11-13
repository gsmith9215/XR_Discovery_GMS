using System;
using TMPro;

namespace XRDiscovery.Vocabulary
{
    internal sealed class StudyScreen
    {
        private readonly VocabularyView view;
        private readonly VocabularyScreens screens;
        public StudyScreen(VocabularyView view, VocabularyScreens screens) { this.view = view; this.screens = screens; }
        public void Draw(string setName, int index, int count, VocabularyEntry entry, Action back, Action<int> move, Action recenter)
        {
            view.Panel("StudyHeadingPanel", 0, 0, 1200, 115);
            view.Panel("StudyWordPanel", 0, 677, 1200, 223);
            screens.Header(setName + "  ·  " + (index + 1) + " / " + count, back);
            var name = view.Text("JapaneseWord", entry.Japanese, 48, 684, 1104, 72, 44);
            name.alignment = TextAlignmentOptions.Midline;
            var reading = view.Text("Reading", entry.Reading + "   ·   " + entry.Name, 48, 752, 1104, 52, 29, VocabularyView.Muted);
            reading.alignment = TextAlignmentOptions.Midline;
            view.Button("PreviousItem", "← Previous", 48, 820, 345, 60, () => move(-1));
            view.Button("Recenter", "Recenter", 427, 820, 345, 60, recenter);
            view.Button("NextItem", "Next →", 807, 820, 345, 60, () => move(1), true);
        }
    }
}
