using System;
using System.Linq;
using TMPro;
using UnityEngine;

namespace XRDiscovery.Vocabulary
{
    internal sealed class TestScreen
    {
        private readonly VocabularyView view;
        private readonly VocabularyScreens screens;
        private TMP_Text feedbackLabel;
        public TestScreen(VocabularyView view, VocabularyScreens screens) { this.view = view; this.screens = screens; }
        public void Question(string name, TestSession session, string promptText, Action back, Action recenter)
        {
            view.Panel("TestHeadingPanel", 0, 0, 1200, 450);
            screens.Header(name + "  ·  Round " + session.RoundNumber, back);
            view.Text("TestProgress", $"Word {session.AnsweredCount + 1} of {session.RoundItemIds.Count}    ·    Correct {session.CorrectCount}", 48, 115, 1104, 55, 27, VocabularyView.Muted);
            var prompt = view.Text("TargetWord", "Find: " + promptText, 48, 185, 1104, 105, 52);
            prompt.alignment = TextAlignmentOptions.Midline;
            feedbackLabel = view.Text("TestHelp", "Hit the matching box with your hammer.", 48, 290, 1104, 70, 29);
            view.Button("Recenter", "Recenter", 850, 370, 302, 65, recenter);
            view.Text("SmallSetHelp", session.OriginalItemIds.Count < 3 ? "Extra choices come from the library. Only your selected words are scored." : "Each word counts once per round.", 48, 370, 760, 75, 24, VocabularyView.Muted);
        }
        public void Feedback(bool correct, VocabularyEntry target)
        {
            feedbackLabel.text = correct ? "Correct!  " + target.Name : "Correct answer: " + target.Japanese + "  ·  " + target.Name;
            feedbackLabel.color = correct ? new Color32(32, 105, 69, 255) : new Color32(152, 75, 35, 255);
        }
        public void Results(TestSession session, string[] missedNames, Action nextMissed, Action retest, Action mainMenu)
        {
            screens.Header("Round " + session.RoundNumber + " complete", mainMenu);
            bool mastered = session.MissedItemIds.Count == 0;
            view.Text("ResultHeading", mastered ? "Every word correct!" : "Keep building your memory.", 48, 160, 1104, 100, 49);
            view.Text("Score", $"{session.CorrectCount} / {session.RoundItemIds.Count} correct this round", 48, 275, 1104, 65, 38);
            view.Text("Missed", mastered ? "You have answered every item in this set correctly." : session.MissedItemIds.Count + " to practice: " + string.Join(" · ", missedNames.Take(12)) + (session.MissedItemIds.Count > 12 ? " …" : ""),
                48, 365, 1104, 200, 31, VocabularyView.Muted);
            view.Button("NextMissedRound", "Round " + (session.RoundNumber + 1) + " — missed words only (" + session.MissedItemIds.Count + ")", 48, 600, 1104, 85,
                nextMissed, true, !mastered);
            view.Button("Retest", "Retest the full set", 48, 715, 530, 75, retest);
            view.Button("MainMenu", "Main menu", 622, 715, 530, 75, mainMenu);
        }
    }
}
