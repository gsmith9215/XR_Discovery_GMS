using System;
using TMPro;
using UnityEngine;

namespace XRDiscovery.Vocabulary
{
    internal sealed class TestScreen
    {
        private readonly VocabularyView view;
        private readonly VocabularyScreens screens;
        private TMP_Text feedbackLabel;
        private TMP_Text targetLabel, audioStatus;
        private UnityEngine.UI.Button modeButton, voiceButton, speedButton, sayButton;
        private static Texture2D speakerIcon;
        public TestScreen(VocabularyView view, VocabularyScreens screens) { this.view = view; this.screens = screens; }
        public void Question(string name, TestSession session, string promptText, TestPromptMode mode,
            bool maleVoice, int speed, bool audioAvailable, Action back, Action recenter,
            Action toggleMode, Action toggleVoice, Action toggleSpeed, Action playAudio)
        {
            view.Panel("TestHeadingPanel", 0, 0, 1200, 450);
            view.Button("Back", "← Back", 48, 35, 175, 60, back);
            VocabularyScreens.RecenterButton(view, recenter);
            var setName = view.Text("GameSetName", "", 255, 28, 180, 70, 32);
            VocabularyView.FitSingleLine(setName, name + ":");
            var round = view.Text("RoundCounter", "Round " + session.RoundNumber,
                435, 28, 165, 70, 32);
            round.textWrappingMode = TextWrappingModes.NoWrap;
            if (round.GetPreferredValues(round.text, float.PositiveInfinity, float.PositiveInfinity).x > 165)
                round.text = "R " + session.RoundNumber;
            var progress = view.Text("TestProgress", $"Word {session.AnsweredCount + 1} of {session.RoundItemIds.Count} - Correct {session.CorrectCount}",
                605, 28, 340, 70, 27, VocabularyView.Muted);
            progress.alignment = TextAlignmentOptions.MidlineRight;
            progress.enableAutoSizing = true;
            progress.textWrappingMode = TextWrappingModes.NoWrap;
            progress.fontSizeMin = 20;
            progress.fontSizeMax = 27;
            feedbackLabel = view.Text("TestHelp", "Hit the matching box with your hammer.", 48, 105, 1104, 50, 27);
            feedbackLabel.alignment = TextAlignmentOptions.Midline;
            targetLabel = view.Text("TargetWord", "", 48, 165, 1104, 115, 46);
            targetLabel.alignment = TextAlignmentOptions.Midline;
            sayButton = view.Button("PlayJapaneseAudio", "Say Word", 422, 300, 356, 65, playAudio,
                enabled: audioAvailable);
            audioStatus = view.Text("AudioStatus", "No Recording", 796, 300, 356, 65, 21, VocabularyView.Muted);
            audioStatus.alignment = TextAlignmentOptions.MidlineRight;
            modeButton = view.Button("GamePromptMode", "", 48, 385, 356, 60, toggleMode);
            voiceButton = view.Button("JapaneseVoice", "", 422, 385, 356, 60, toggleVoice);
            speedButton = view.Button("GameVoiceSpeed", "", 796, 385, 356, 60, toggleSpeed);
            UpdatePrompt(promptText, mode, maleVoice, speed, audioAvailable);
        }
        public void UpdatePrompt(string promptText, TestPromptMode mode, bool maleVoice, int speed, bool audioAvailable)
        {
            targetLabel.text = mode == TestPromptMode.Audio && audioAvailable
                ? "Listen and find the matching box." : "Find: " + promptText;
            audioStatus.gameObject.SetActive(!audioAvailable);
            view.UpdateButton(modeButton, "Prompt: " + mode);
            view.UpdateButton(voiceButton, "Voice: " + (maleVoice ? "Male" : "Female"));
            view.UpdateButton(speedButton, "Voice Speed: " + speed + "%");
            view.UpdateButton(sayButton, "Say Word", enabled: audioAvailable);
        }
        public void Feedback(bool correct, VocabularyEntry target)
        {
            feedbackLabel.text = correct ? "Correct!  " + target.Name : "Correct answer: " + target.Japanese + "  ·  " + target.Name;
            feedbackLabel.color = correct ? new Color32(32, 105, 69, 255) : new Color32(152, 75, 35, 255);
        }
        public void Results(TestSession session, VocabularyEntry[] missed, Action<string> playAudio,
            Action nextMissed, Action retest, Action mainMenu, Action back)
        {
            var score = screens.Header($"Score: {session.CorrectCount}/{session.RoundItemIds.Count}", back, centerTitle: true);
            score.fontStyle = FontStyles.Bold;
            bool mastered = missed.Length == 0;
            var heading = view.Text("MissedWordsHeading", "", 48, 160, 1104, 60, 27);
            heading.richText = true;
            heading.text = "<b>Missed Words:</b> " +
                (mastered ? "You answered every word correctly." : "Play another round to retry with missed words.");
            heading.enableAutoSizing = true;
            heading.fontSizeMin = 21;
            heading.fontSizeMax = 27;
            heading.textWrappingMode = TextWrappingModes.NoWrap;
            view.ScrollArea("MissedWords", 48, 230, 1104, 350, out var content);
            const float rowHeight = 70;
            content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 24 + Mathf.Max(1, missed.Length) * rowHeight);
            if (mastered)
                view.Text("NoMissedWords", "No missed words.", 22, 12, 950, rowHeight - 4, 27, VocabularyView.Muted, content);
            for (int i = 0; i < missed.Length; i++)
            {
                var entry = missed[i];
                var word = view.Text("MissedWord_" + entry.Id, entry.Name + "  /  " + entry.Japanese,
                    22, 12 + i * rowHeight, 860, rowHeight - 4, 27, parent: content);
                word.textWrappingMode = TextWrappingModes.NoWrap;
                word.enableAutoSizing = true;
                word.fontSizeMin = 20;
                word.fontSizeMax = 27;
                var replay = view.Button("MissedAudio_" + entry.Id, "", 910, 20 + i * rowHeight, 74, 50,
                    () => playAudio(entry.Id), parent: content);
                var icon = view.Area("SpeakerIcon", 20, 8, 34, 34, replay.transform)
                    .gameObject.AddComponent<UnityEngine.UI.RawImage>();
                icon.texture = SpeakerIcon();
                icon.color = VocabularyView.ButtonInk;
                icon.raycastTarget = false;
            }
            view.Button("NextMissedRound", "Round " + (session.RoundNumber + 1) + " — missed words only (" + missed.Length + ")", 48, 610, 1104, 85,
                nextMissed, true, !mastered);
            view.Button("Retest", "Retest the full set", 48, 730, 530, 75, retest);
            view.Button("MainMenu", "Main menu", 622, 730, 530, 75, mainMenu);
        }

        private static Texture2D SpeakerIcon()
        {
            if (speakerIcon != null) return speakerIcon;
            const int size = 48;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float radius = Mathf.Sqrt((x - 23) * (x - 23) + (y - 24) * (y - 24));
                bool body = x >= 5 && x <= 14 && y >= 19 && y <= 29;
                bool cone = x >= 14 && x <= 25 && Mathf.Abs(y - 24) <= 4 + (x - 14) * .8f;
                bool waves = x >= 28 && (Mathf.Abs(radius - 11) < 1.5f || Mathf.Abs(radius - 18) < 1.5f);
                pixels[y * size + x] = body || cone || waves ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
            }
            speakerIcon = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            speakerIcon.SetPixels32(pixels);
            speakerIcon.Apply(false, true);
            return speakerIcon;
        }
    }
}
