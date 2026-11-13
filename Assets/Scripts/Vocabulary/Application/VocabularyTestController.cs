using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace XRDiscovery.Vocabulary
{
    internal sealed class VocabularyTestController
    {
        private readonly VocabularyApp app;
        private readonly TestScreen screen;
        private Coroutine feedback;
        private bool answering;
        private string name;
        public TestSession Session { get; private set; }

        public VocabularyTestController(VocabularyApp app)
        { this.app = app; screen = new TestScreen(app.View, app.Screens); }

        public void Start(string setName, IEnumerable<string> ids)
        {
            var valid = app.ValidIds(ids);
            if (valid.Length == 0) return;
            Suspend();
            name = setName;
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
            screen.Question(name, Session, app.Prompt(app.Library.Get(question.TargetId)),
                () => app.Confirm("Leave this test?", "This round's progress will be discarded. Your custom sets are already saved.", app.ShowMainMenu, ShowQuestion), app.Recenter);
        }

        public void Answer(string id)
        {
            if (app.Screen != VocabularyScreen.Test || answering || Session == null || Session.IsRoundComplete) return;
            answering = true;
            app.Presenter.StopAcceptingHits();
            var question = Session.CurrentQuestion;
            var target = app.Library.Get(question.TargetId);
            bool correct = Session.SubmitAnswer(question.Sequence, id);
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
            screen.Results(Session, Session.MissedItemIds.Select(id => app.Library.Get(id).Name).ToArray(), NextMissedRound, Retest, app.ShowMainMenu);
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
