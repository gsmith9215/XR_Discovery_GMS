using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace XRDiscovery.Vocabulary
{
    internal enum TestPromptMode { Words, Audio, Both }

    internal sealed class VocabularyTestController
    {
        private readonly VocabularyApp app;
        private readonly TestScreen screen;
        private Coroutine feedback;
        private bool answering;
        private string name;
        private System.Action back;
        private readonly AudioSource audio;
        private AudioClip clip;
        public TestSession Session { get; private set; }

        public VocabularyTestController(VocabularyApp app)
        {
            this.app = app;
            screen = new TestScreen(app.View, app.Screens);
            audio = app.gameObject.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 0;
            audio.pitch = 1f;
        }

        public void StopAudio() => audio.Stop();

        private void LoadAudio()
        {
            StopAudio();
            string speed = app.GameVoiceSpeedPercent == 100 ? "" : app.GameVoiceSpeedPercent + "/";
            clip = Resources.Load<AudioClip>("VocabularyAudio/" +
                (app.JapaneseMaleVoice ? "Male/" : "Female/") + speed + Session.CurrentQuestion.TargetId);
            audio.clip = clip;
        }

        private void PlayAudio()
        {
            if (app.Screen != VocabularyScreen.Test || answering || clip == null) return;
            audio.Stop();
            audio.Play();
        }

        private void PlayPromptAudio()
        {
            if (app.GamePromptMode != TestPromptMode.Words) PlayAudio();
        }

        private void RefreshPrompt()
        {
            var target = app.Library.Get(Session.CurrentQuestion.TargetId);
            screen.UpdatePrompt(app.Prompt(target), app.GamePromptMode, app.JapaneseMaleVoice,
                app.GameVoiceSpeedPercent, clip != null);
        }

        private void ToggleMode()
        {
            app.GamePromptMode = (TestPromptMode)(((int)app.GamePromptMode + 1) % 3);
            StopAudio();
            RefreshPrompt();
            PlayPromptAudio();
        }

        private void ToggleVoice()
        {
            app.JapaneseMaleVoice = !app.JapaneseMaleVoice;
            LoadAudio();
            RefreshPrompt();
            PlayPromptAudio();
        }

        private void ToggleSpeed()
        {
            app.GameVoiceSpeedPercent = app.GameVoiceSpeedPercent == 100 ? 75 : app.GameVoiceSpeedPercent == 75 ? 50 : 100;
            LoadAudio();
            RefreshPrompt();
            PlayPromptAudio();
        }

        public void Start(string setName, IEnumerable<string> ids, System.Action returnToPreview = null)
        {
            var valid = app.ValidIds(ids);
            if (valid.Length == 0) return;
            Suspend();
            name = setName;
            back = returnToPreview ?? app.ShowMainMenu;
            Session = new TestSession(valid, app.Library.AllItemIds, new System.Random(), id => app.Prompt(app.Library.Get(id)));
            app.Presenter.Recenter(app.View.PlacementOrigin, app.View.PlacementForward);
            ShowQuestion();
        }

        private void ShowQuestion()
        {
            if (Session.IsRoundComplete) { ShowResults(); return; }
            app.NewPage(VocabularyScreen.Test, false);
            var question = Session.CurrentQuestion;
            app.Presenter.ShowItems(question.ChoiceIds.Select(app.Library.Get).ToArray(), true);
            LoadAudio();
            screen.Question(name, Session, app.Prompt(app.Library.Get(question.TargetId)), app.GamePromptMode,
                app.JapaneseMaleVoice, app.GameVoiceSpeedPercent, clip != null,
                () => app.Confirm("Leave this test?", "This round's progress will be discarded. Your custom sets are already saved.", Leave, ShowQuestion),
                app.Recenter, ToggleMode, ToggleVoice, ToggleSpeed, PlayAudio);
            PlayPromptAudio();
        }

        public void Answer(string id)
        {
            if (app.Screen != VocabularyScreen.Test || answering || Session == null || Session.IsRoundComplete) return;
            answering = true;
            StopAudio();
            app.Presenter.StopAcceptingHits();
            var question = Session.CurrentQuestion;
            var target = app.Library.Get(question.TargetId);
            bool correct = Session.SubmitAnswer(question.Sequence, id);
            app.Presenter.ShowAnswerFeedback(id, correct);
            screen.Feedback(correct, target);
            feedback = app.StartCoroutine(AdvanceAfterFeedback());
        }

        private IEnumerator AdvanceAfterFeedback()
        {
            yield return new WaitForSecondsRealtime(1.1f);
            feedback = null;
            ShowQuestion();
        }

        private void ShowResults()
        {
            app.NewPage(VocabularyScreen.Results);
            screen.Results(Session, Session.MissedItemIds.Select(app.Library.Get).ToArray(), PlayResultAudio,
                NextMissedRound, Retest, app.ShowMainMenu, Leave);
        }

        private void Leave()
        {
            Reset();
            back();
        }

        private void PlayResultAudio(string id)
        {
            if (app.Screen != VocabularyScreen.Results || app.Library.Get(id) == null) return;
            var voice = app.JapaneseMaleVoice ? "Male/" : "Female/";
            audio.Stop();
            audio.clip = Resources.Load<AudioClip>("VocabularyAudio/" + voice + "75/" + id);
            if (audio.clip == null) return;
            audio.Play();
        }

        public void NextMissedRound()
        {
            if (app.Screen == VocabularyScreen.Results && Session.StartNextMissedRound()) ShowQuestion();
        }

        public void Retest()
        {
            if (Session == null) return;
            Suspend();
            Session.Retest();
            ShowQuestion();
        }

        public void Suspend()
        {
            if (feedback != null) { app.StopCoroutine(feedback); feedback = null; }
            answering = false;
            app.Presenter.StopAcceptingHits();
        }

        public void Reset() { Suspend(); Session = null; }
    }
}
