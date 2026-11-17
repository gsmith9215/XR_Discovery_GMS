using System.Collections.Generic;
using UnityEngine;

namespace XRDiscovery.Vocabulary
{
    internal sealed class StudyController
    {
        private static readonly int[] VoiceSpeedPercents = { 100, 75, 50 };
        private readonly VocabularyApp app;
        private readonly StudyScreen screen;
        private StudySession session;
        private string name;
        private System.Action back;
        private bool showJapaneseImmediately;
        private bool revealed;
        private readonly AudioSource audio;
        private AudioClip clip;
        private int voiceSpeedIndex;
        public int Index => session?.Index ?? 0;

        public StudyController(VocabularyApp app)
        {
            this.app = app;
            screen = new StudyScreen(app.View);
            audio = app.gameObject.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 0;
            audio.pitch = 1f;
        }

        public void StopAudio() => audio.Stop();

        private void LoadAudio()
        {
            StopAudio();
            var speedFolder = voiceSpeedIndex == 0 ? string.Empty : VoiceSpeedPercents[voiceSpeedIndex] + "/";
            clip = Resources.Load<AudioClip>("VocabularyAudio/" +
                (app.JapaneseMaleVoice ? "Male/" : "Female/") + speedFolder + session.CurrentItemId);
            audio.clip = clip;
        }

        private void PlayAudio()
        {
            if (app.Screen != VocabularyScreen.Study || clip == null) return;
            audio.Stop();
            audio.Play();
        }

        private void PlayAutomaticAudio()
        {
            if (app.JapaneseAudioEnabled) PlayAudio();
        }

        public void Start(string setName, IEnumerable<string> ids, bool showImmediately, System.Action returnToPreview = null)
        {
            var valid = app.ValidIds(ids);
            if (valid.Length == 0) return;
            name = setName;
            back = returnToPreview ?? app.ShowMainMenu;
            showJapaneseImmediately = showImmediately;
            revealed = showJapaneseImmediately;
            session = new StudySession(valid);
            app.Presenter.Recenter(app.View.PlacementOrigin, app.View.PlacementForward);
            Show();
        }

        public void Move(int delta)
        {
            if (app.Screen != VocabularyScreen.Study || session == null) return;
            session.Move(delta);
            revealed = showJapaneseImmediately;
            app.Presenter.ShowItems(new[] { app.Library.Get(session.CurrentItemId) }, false);
            LoadAudio();
            RefreshControls();
            PlayAutomaticAudio();
        }

        private void ToggleImmediate()
        {
            showJapaneseImmediately = !showJapaneseImmediately;
            revealed = showJapaneseImmediately;
            RefreshControls();
        }

        private void ToggleReveal()
        {
            revealed = !revealed;
            RefreshControls();
        }

        private void ToggleAudio()
        {
            app.JapaneseAudioEnabled = !app.JapaneseAudioEnabled;
            StopAudio();
            RefreshControls();
            PlayAutomaticAudio();
        }

        private void ToggleVoice()
        {
            app.JapaneseMaleVoice = !app.JapaneseMaleVoice;
            LoadAudio();
            RefreshControls();
            PlayAutomaticAudio();
        }

        private void ToggleVoiceSpeed()
        {
            voiceSpeedIndex = (voiceSpeedIndex + 1) % VoiceSpeedPercents.Length;
            LoadAudio();
            RefreshControls();
            PlayAutomaticAudio();
        }

        private void RefreshControls()
        {
            screen.Update(name, session.Index, session.ItemIds.Count, app.Library.Get(session.CurrentItemId),
                showJapaneseImmediately, revealed, app.JapaneseAudioEnabled, app.JapaneseMaleVoice,
                VoiceSpeedPercents[voiceSpeedIndex], clip != null);
        }

        private void Show()
        {
            app.NewPage(VocabularyScreen.Study);
            app.View.SetBackgroundBottomExtension(55);
            var entry = app.Library.Get(session.CurrentItemId);
            app.Presenter.ShowItems(new[] { entry }, false);
            LoadAudio();
            Draw();
            PlayAutomaticAudio();
        }

        private void Draw()
        {
            var entry = app.Library.Get(session.CurrentItemId);
            screen.Draw(name, session.Index, session.ItemIds.Count, entry, showJapaneseImmediately, revealed,
                app.JapaneseAudioEnabled, app.JapaneseMaleVoice, VoiceSpeedPercents[voiceSpeedIndex], clip != null,
                back, Move, app.Recenter, ToggleImmediate, ToggleReveal, ToggleAudio, ToggleVoice,
                ToggleVoiceSpeed, PlayAudio);
        }
    }
}
