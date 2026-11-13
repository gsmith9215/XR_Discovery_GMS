using System.Collections.Generic;

namespace XRDiscovery.Vocabulary
{
    internal sealed class StudyController
    {
        private readonly VocabularyApp app;
        private readonly StudyScreen screen;
        private StudySession session;
        private string name;
        public int Index => session?.Index ?? 0;

        public StudyController(VocabularyApp app)
        { this.app = app; screen = new StudyScreen(app.View, app.Screens); }

        public void Start(string setName, IEnumerable<string> ids)
        {
            var valid = app.ValidIds(ids);
            if (valid.Length == 0) return;
            name = setName;
            session = new StudySession(valid);
            app.Presenter.Recenter(app.View.PlacementOrigin, app.View.PlacementForward);
            Show();
        }

        public void Move(int delta)
        {
            if (app.Screen != VocabularyScreen.Study || session == null) return;
            session.Move(delta);
            Show();
        }

        private void Show()
        {
            app.NewPage(VocabularyScreen.Study, false);
            var entry = app.Library.Get(session.CurrentItemId);
            app.Presenter.ShowItems(new[] { entry }, false);
            screen.Draw(name, session.Index, session.ItemIds.Count, entry, app.ShowMainMenu, Move, app.Recenter);
        }
    }
}
