using System;
using System.Collections.Generic;
using System.Linq;

namespace XRDiscovery.Vocabulary
{
    internal sealed class SetEditorController
    {
        private readonly VocabularyApp app;
        private readonly SetEditorScreen screen;
        private string id, name, search = "", category = "";
        private HashSet<string> items;
        private bool selectedOnly;
        private int page;

        public SetEditorController(VocabularyApp app)
        { this.app = app; screen = new SetEditorScreen(app.View, app.Screens); }

        public void Open(string setId, string setName, IEnumerable<string> itemIds)
        {
            id = setId; name = setName; items = new HashSet<string>(itemIds);
            search = ""; category = ""; selectedOnly = false; page = 0;
            Draw();
        }

        private void Draw()
        {
            app.NewPage(VocabularyScreen.Editor);
            screen.Draw(id == null, name, search, category, selectedOnly,
                () => app.Confirm("Discard these edits?", "Changes are saved only when you select Save set.", app.ManageSets, Draw),
                value => name = value,
                value => { search = value; page = 0; RefreshItems(); },
                () => { selectedOnly = !selectedOnly; page = 0; Draw(); },
                () =>
                {
                    var categories = new[] { "" }.Concat(app.Library.Categories).ToArray();
                    category = categories[(Array.IndexOf(categories, category) + 1) % categories.Length];
                    page = 0; Draw();
                });
            RefreshItems();
        }

        private void RefreshItems()
        {
            if (app.Screen != VocabularyScreen.Editor) return;
            var entries = app.Library.Entries.Where(e => (string.IsNullOrEmpty(category) || e.Category == category)
                && (!selectedOnly || items.Contains(e.Id)) && MatchesSearch(e)).ToArray();
            int pages = Math.Max(1, (entries.Length + 5) / 6);
            page = Math.Clamp(page, 0, pages - 1);
            screen.Items(entries.Skip(page * 6).Take(6).ToArray(), items, page, pages, entries.Length,
                item => { if (!items.Add(item)) items.Remove(item); RefreshItems(); },
                value => { page = value; RefreshItems(); },
                () => { foreach (var entry in entries) items.Add(entry.Id); RefreshItems(); },
                () => { foreach (var entry in entries) items.Remove(entry.Id); RefreshItems(); }, Save);
        }

        private bool MatchesSearch(VocabularyEntry e) => string.IsNullOrWhiteSpace(search)
            || e.Name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0
            || e.Japanese.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0
            || e.Reading.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;

        private void Save()
        {
            try
            {
                var ordered = app.Library.Entries.Where(e => items.Contains(e.Id)).Select(e => e.Id)
                    .Concat(items.Where(item => app.Library.Get(item) == null));
                if (id == null) app.CustomSets.Create(name, ordered);
                else app.CustomSets.Update(id, name, ordered);
                app.ManageSets();
            }
            catch (Exception e) { screen.Status("Could not save: " + e.Message); }
        }

        public void SetKeyboardVisible(bool visible) => screen.SetKeyboardVisible(visible);
    }
}
