using System;
using TMPro;

namespace XRDiscovery.Vocabulary
{
    internal sealed class StudyScreen
    {
        private readonly VocabularyView view;
        private TMP_Text title, status, english, japanese, reading;
        private UnityEngine.UI.Button wordToggle, audioToggle, voiceToggle, revealButton, sayButton;
        public StudyScreen(VocabularyView view) { this.view = view; }
        public void Draw(string setName, int index, int count, VocabularyEntry entry, bool showImmediately, bool revealed,
            bool audioEnabled, bool maleVoice, bool audioAvailable,
            Action back, Action<int> move, Action recenter, Action toggleImmediate, Action toggleReveal,
            Action toggleAudio, Action toggleVoice, Action playAudio)
        {
            // Keep the middle clear for the object, with one continuous canvas background.
            view.Button("Back", "← Back", 48, 35, 175, 60, back);
            VocabularyScreens.RecenterButton(view, recenter);
            title = view.Text("Title", "", 255, 28, 690, 78, 38);
            status = view.Text("AudioStatus", "No Recording", 920, 195, 232, 35, 24, VocabularyView.Muted);
            status.alignment = TextAlignmentOptions.MidlineRight;
            wordToggle = view.Button("ShowJapaneseImmediately", "Word Immediately: " + (showImmediately ? "On" : "Off"),
                48, 125, 356, 60, toggleImmediate, showImmediately);
            audioToggle = view.Button("JapaneseAudio", "Audio Immediately: " + (audioEnabled ? "On" : "Off"),
                422, 125, 356, 60, toggleAudio, audioEnabled);
            voiceToggle = view.Button("JapaneseVoice", "Voice: " + (maleVoice ? "Male" : "Female"),
                796, 125, 356, 60, toggleVoice);
            // Keep the words below the full-size object; actions sit in two side columns.
            english = view.Text("EnglishWord", "", 48, 784, 1104, 42, 29, VocabularyView.Muted);
            english.alignment = TextAlignmentOptions.Midline;
            english.enableAutoSizing = true;
            english.fontSizeMin = 20;
            english.fontSizeMax = 29;
            japanese = view.Text("JapaneseWord", "", 48, 826, 1104, 46, 34);
            japanese.alignment = TextAlignmentOptions.Midline;
            japanese.enableAutoSizing = true;
            japanese.fontSizeMin = 24;
            japanese.fontSizeMax = 34;
            reading = view.Text("Reading", "", 48, 870, 1104, 28, 20, VocabularyView.Muted);
            reading.alignment = TextAlignmentOptions.Midline;
            reading.enableAutoSizing = true;
            reading.fontSizeMin = 16;
            reading.fontSizeMax = 20;
            revealButton = view.Button("RevealJapaneseWord", "Show Word", 48, 417, 345, 60, toggleReveal);
            sayButton = view.Button("PlayJapaneseAudio", "Say Word", 807, 417, 345, 60, playAudio, enabled: audioAvailable);
            view.Button("PreviousItem", "← Previous", 48, 485, 345, 60, () => move(-1));
            view.Button("NextItem", "Next →", 807, 485, 345, 60, () => move(1), true);
            Update(setName, index, count, entry, showImmediately, revealed, audioEnabled, maleVoice, audioAvailable);
        }

        public void Update(string setName, int index, int count, VocabularyEntry entry, bool showImmediately,
            bool revealed, bool audioEnabled, bool maleVoice, bool audioAvailable)
        {
            title.text = setName + "  ·  " + (index + 1) + " / " + count;
            status.gameObject.SetActive(!audioAvailable);
            english.text = entry.Name;
            japanese.text = entry.Japanese;
            reading.text = entry.Reading;
            japanese.gameObject.SetActive(revealed);
            reading.gameObject.SetActive(revealed);
            view.UpdateButton(wordToggle, "Word Immediately: " + (showImmediately ? "On" : "Off"), showImmediately);
            view.UpdateButton(audioToggle, "Audio Immediately: " + (audioEnabled ? "On" : "Off"), audioEnabled);
            view.UpdateButton(voiceToggle, "Voice: " + (maleVoice ? "Male" : "Female"));
            view.UpdateButton(revealButton, revealed ? "Hide Word" : "Show Word");
            view.UpdateButton(sayButton, "Say Word", enabled: audioAvailable);
        }
    }
}
