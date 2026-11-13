using System;
using System.Collections.Generic;
using System.Linq;

namespace XRDiscovery.Vocabulary
{
    internal enum BrowsePurpose { Study, Test, Manage, Template }

    internal sealed class SetChoice
    {
        public VocabularySet Set { get; }
        public bool Custom { get; }
        public string Description { get; }
        public string Id => Set.Id;
        public string Name => Set.Name;
        public IReadOnlyList<string> Items => Set.ItemIds;
        public SetChoice(VocabularySet set, bool custom = false, string description = "")
        { Set = set; Custom = custom; Description = description; }
    }

    internal sealed class SetBrowserController
    {
        private readonly VocabularyApp app;
        private readonly SetBrowserScreen screen;
        private BrowsePurpose purpose;
        private bool customTab;
        private int page;

        public SetBrowserController(VocabularyApp app)
        { this.app = app; screen = new SetBrowserScreen(app.View, app.Screens); }

        public void Browse(BrowsePurpose nextPurpose, bool reset = true)
        {
            purpose = nextPurpose;
            if (reset) { customTab = purpose == BrowsePurpose.Manage; page = 0; }
            // Returning from a premade template must always show the player's saved copy.
            if (purpose == BrowsePurpose.Manage) customTab = true;
            app.NewPage(VocabularyScreen.Sets);
            var sets = AvailableSets();
            int pages = Math.Max(1, (sets.Count + 7) / 8);
            page = Math.Clamp(page, 0, pages - 1);
            screen.Browse(purpose, customTab, app.StorageAvailable, sets, page, pages,
                choice => app.ValidIds(choice.Items).Length, app.ShowMainMenu,
                () => app.EditSet(null, "My vocabulary", Array.Empty<string>()),
                () => Browse(BrowsePurpose.Template),
                custom => { customTab = custom; page = 0; Browse(purpose, false); },
                choice => { if (purpose == BrowsePurpose.Template) app.EditSet(null, choice.Name + " copy", choice.Items); else ShowSet(choice); },
                value => { page = value; Browse(purpose, false); });
        }

        private List<SetChoice> AvailableSets()
        {
            if (customTab) return app.StorageAvailable ? app.CustomSets.Sets.Select(s => new SetChoice(s, true)).ToList() : new List<SetChoice>();
            var sets = new List<SetChoice> { new(new VocabularySet("premade:all", "All items", app.Library.AllItemIds)) };
            sets.AddRange(app.PremadeSets.Sets.Select(s => new SetChoice(s.Set, description: s.Description)));
            return sets;
        }

        private void ShowSet(SetChoice choice)
        {
            app.NewPage(VocabularyScreen.SetDetails);
            var valid = app.ValidIds(choice.Items);
            screen.Details(choice, purpose, valid.Select(id => app.Library.Get(id).Name).ToArray(), choice.Items.Count - valid.Length,
                () => Browse(purpose, false),
                () => app.EditSet(choice.Id, choice.Name, choice.Items),
                () => app.EditSet(null, choice.Name + " copy", choice.Items),
                () => app.Confirm("Delete “" + choice.Name + "”?", "Only this custom set will be removed. Its objects and other sets are kept.", () =>
                {
                    try { app.CustomSets.Delete(choice.Id); Browse(BrowsePurpose.Manage, false); }
                    catch (Exception e) { app.ShowFailure("Could not delete the set", e, () => ShowSet(choice)); }
                }, () => ShowSet(choice)),
                () => { if (purpose == BrowsePurpose.Study) app.StartStudy(choice.Name, valid); else app.StartTest(choice.Name, valid); });
        }
    }
}
