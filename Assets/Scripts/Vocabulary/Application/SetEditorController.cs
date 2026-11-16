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
        private bool filterOpen;
        private bool addBySet;
        private string setFilter = "", setSearch = "";
        private SetChoice previewSet;

        public SetEditorController(VocabularyApp app)
        { this.app = app; screen = new SetEditorScreen(app.View, app.Screens); }

        public void Open(string setId, string setName, IEnumerable<string> itemIds)
        {
            id = setId; name = setName; items = new HashSet<string>(itemIds);
            search = ""; category = ""; selectedOnly = false; filterOpen = false;
            addBySet = false; setFilter = ""; setSearch = ""; previewSet = null;
            DrawSet();
        }

        private void DrawSet()
        {
            filterOpen = false;
            previewSet = null;
            app.NewPage(VocabularyScreen.Editor);
            var selected = app.Library.Entries.Where(e => items.Contains(e.Id)).ToArray();
            screen.DrawSet(id == null, name, selected, items.Count - selected.Length,
                () => app.Confirm("Discard these edits?", "Changes are saved only when you select Save set.", app.ManageSets, DrawSet),
                value => name = value, DrawWords, Save);
        }

        private void DrawWords()
        {
            app.NewPage(VocabularyScreen.EditorWords);
            if (addBySet && previewSet != null)
            {
                var choice = previewSet;
                var available = choice.Items.Select(app.Library.Get).Where(entry => entry != null).ToArray();
                screen.SetPreview(choice, available, choice.Items.Count - available.Length, items,
                    () => { previewSet = null; DrawWords(); },
                    () =>
                    {
                        foreach (var entry in available) items.Add(entry.Id);
                        previewSet = null;
                        DrawWords();
                    },
                    () =>
                    {
                        foreach (var entry in available) items.Remove(entry.Id);
                        previewSet = null;
                        DrawWords();
                    });
                return;
            }
            string activeFilter = addBySet ? setFilter : category;
            screen.DrawWords(addBySet, addBySet ? setSearch : search, activeFilter, selectedOnly, items.Count, DrawSet,
                value =>
                {
                    if (addBySet) { setSearch = value; screen.ScrollToTop(); RefreshItems(); }
                    else { search = value; screen.ScrollToTop(); RefreshItems(); }
                },
                () => { selectedOnly = !selectedOnly; DrawWords(); },
                () => { addBySet = !addBySet; filterOpen = false; previewSet = null; DrawWords(); },
                () => { filterOpen = !filterOpen; DrawWords(); });
            RefreshItems();
            if (filterOpen)
                screen.ShowFilters(addBySet ? (IReadOnlyList<string>)new[] { "Premade Sets", "Custom Sets" } : app.Library.Categories,
                    activeFilter, addBySet ? "All Sets" : "All Words", value =>
                {
                    if (addBySet) setFilter = value;
                    else category = value;
                    filterOpen = false;
                    DrawWords();
                });
        }

        private void RefreshItems()
        {
            if (app.Screen != VocabularyScreen.EditorWords) return;
            if (addBySet)
            {
                var sets = app.PremadeSets.Sets.Select(s => new SetChoice(s.Set))
                    .Concat(app.StorageAvailable ? app.CustomSets.Sets
                        .Where(s => s.Id != id).Select(s => new SetChoice(s, true)) : Enumerable.Empty<SetChoice>())
                    .Where(s => (setFilter == "" || (setFilter == "Custom Sets") == s.Custom)
                        && (string.IsNullOrWhiteSpace(setSearch)
                            || s.Name.IndexOf(setSearch, StringComparison.OrdinalIgnoreCase) >= 0)
                        && (!selectedOnly || (app.ValidIds(s.Items).Length > 0
                            && app.ValidIds(s.Items).All(items.Contains))))
                    .ToArray();
                screen.Sets(sets, choice => { previewSet = choice; DrawWords(); });
                return;
            }
            var entries = app.Library.Entries.Where(e => (string.IsNullOrEmpty(category) || e.Category == category)
                && (!selectedOnly || items.Contains(e.Id)) && MatchesSearch(e)).ToArray();
            screen.Words(entries, items,
                item => { if (!items.Add(item)) items.Remove(item); RefreshItems(); },
                () => { foreach (var entry in entries) items.Add(entry.Id); RefreshItems(); },
                () => { foreach (var entry in entries) items.Remove(entry.Id); RefreshItems(); });
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
