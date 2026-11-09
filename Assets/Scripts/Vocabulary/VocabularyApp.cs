using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace XRDiscovery.Vocabulary
{
    public enum VocabularyScreen { MainMenu, Sets, SetDetails, Editor, Study, Test, Results, Confirmation, Error }

    public sealed class VocabularyApp : MonoBehaviour
    {
        private enum BrowsePurpose { Study, Test, Manage, Template }
        private sealed class SetChoice
        {
            public string Id;
            public string Name;
            public bool Custom;
            public string[] Items;
        }

        private RandomObjectSpawner spawner;
        private VocabularyView view;
        private VocabularyLibrary library;
        private CustomSetRepository repository;
        private string storageNotice;
        private bool storageAvailable;
        private BrowsePurpose purpose;
        private bool customTab;
        private int browserPage;
        private SetChoice selected;
        private TestSession session;
        private string[] studyItems;
        private int studyIndex;
        private bool answering;
        private bool initialized;
        private bool japanese = true;
        private Coroutine feedback;
        private Coroutine initialPositioning;
        private string draftId;
        private string draftName;
        private HashSet<string> draftItems;
        private string draftSearch = "";
        private string draftCategory = "";
        private bool selectedOnly;
        private int draftPage;
        private RectTransform itemArea;
        private TMP_Text draftCount;
        private TMP_Text draftStatus;
        private GameObject hammer;
        public VocabularyScreen Screen { get; private set; }
        public VocabularyLibrary Library => library;
        public TestSession Session => session;
        public int StudyIndex => studyIndex;
        public VocabularyView View => view;
        public CustomSetRepository CustomSets => repository;

        public void Initialize(RandomObjectSpawner presenter, string customSetsPath = null)
        {
            if (initialized) return;
            initialized = true;
            spawner = presenter;
            spawner.BoxSelected += Answer;
            var logger = FindFirstObjectByType<HammerHitLogger>(FindObjectsInactive.Include);
            if (logger != null) hammer = logger.GetComponentInParent<Rigidbody>()?.gameObject ?? logger.gameObject;
            view = gameObject.AddComponent<VocabularyView>();
            view.Initialize(spawner.PlayerCamera, spawner.RightHandAnchor);
            view.Keyboard.VisibilityChanged += OnKeyboardVisibility;
            japanese = PlayerPrefs.GetInt("Vocabulary.JapanesePrompts", 1) != 0;
            try
            {
                library = VocabularyLibrary.Load();
                if (library.Entries.Count == 0) throw new InvalidOperationException("The vocabulary library is empty.");
            }
            catch (Exception e)
            {
                Screen = VocabularyScreen.Error;
                view.Text("Error", "Vocabulary could not load", 48, 70, 1104, 80, 44);
                view.Text("Details", e.Message, 48, 170, 1104, 400, 28);
                Debug.LogException(e);
                return;
            }
            repository = new CustomSetRepository(customSetsPath ?? Path.Combine(Application.persistentDataPath, "Vocabulary", "custom-sets.json"));
            try
            {
                repository.Load();
                storageAvailable = true;
                storageNotice = repository.RecoveryNotice;
            }
            catch (Exception e)
            {
                storageAvailable = false;
                storageNotice = "Custom sets could not be loaded. Your saved file has been preserved. " + e.Message;
                Debug.LogWarning(storageNotice);
            }
            ShowMainMenu();
            initialPositioning = StartCoroutine(PositionAfterTrackingStarts());
        }

        private IEnumerator PositionAfterTrackingStarts()
        {
            // OVRCameraRig initializes its anchors after MonoBehaviour.Start.
            yield return null;
            yield return new WaitForEndOfFrame();
            if (Screen == VocabularyScreen.MainMenu) view.Recenter();
            while (enabled && Screen == VocabularyScreen.MainMenu)
            {
                var head = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.Head);
                if (head.isValid && head.TryGetFeatureValue(UnityEngine.XR.CommonUsages.isTracked, out bool tracked) && tracked)
                {
                    yield return new WaitForEndOfFrame();
                    if (Screen == VocabularyScreen.MainMenu) view.Recenter();
                    initialPositioning = null;
                    yield break;
                }
                yield return null;
            }
            initialPositioning = null;
        }

        private void StopInitialPositioning()
        {
            if (initialPositioning == null) return;
            StopCoroutine(initialPositioning);
            initialPositioning = null;
        }

        private void OnKeyboardVisibility(bool visible)
        {
            if (itemArea != null) itemArea.gameObject.SetActive(!visible);
        }

        private void NewPage(VocabularyScreen screen, bool solid = true)
        {
            StopInitialPositioning();
            Screen = screen;
            itemArea = null;
            view.Clear(solid);
            if (hammer != null) hammer.SetActive(screen == VocabularyScreen.Test);
        }

        private void StopPresentation()
        {
            if (feedback != null) { StopCoroutine(feedback); feedback = null; }
            answering = false;
            spawner.ClearObjects();
        }

        public void ShowMainMenu()
        {
            StopPresentation();
            session = null;
            NewPage(VocabularyScreen.MainMenu);
            var heading = view.Text("Heading", "Vocabulary Learning Center", 60, 100, 1080, 100, 58);
            heading.alignment = TextAlignmentOptions.Center;
            view.Button("Study", "Study\nLearn words with 3D objects.", 60, 360, 520, 160, () => Browse(BrowsePurpose.Study), true);
            view.Button("Test", "Games\nTest you knowledge with vocabulary games.", 620, 360, 520, 160, () => Browse(BrowsePurpose.Test), true);
            view.Button("CustomSets", "My custom sets", 60, 550, 1080, 80, () => Browse(BrowsePurpose.Manage), enabled: storageAvailable);
            view.Button("PromptLanguage", "Test prompts: " + (japanese ? "Japanese" : "English"), 60, 665, 700, 65, () =>
            {
                japanese = !japanese;
                PlayerPrefs.SetInt("Vocabulary.JapanesePrompts", japanese ? 1 : 0);
                PlayerPrefs.Save();
                ShowMainMenu();
            });
            view.Text("LibraryCount", $"{library.Entries.Count} items · {library.Categories.Count} premade sets", 60, 755, 1080, 48, 26, VocabularyView.Muted);
            view.Button("Recenter", "Recenter", 800, 665, 340, 65, Recenter);
            view.Text("Help", string.IsNullOrEmpty(storageNotice) ? "Point and press the trigger to select. Mouse and keyboard work in the Editor." : storageNotice,
                60, 810, 1080, 70, 23, VocabularyView.Muted);
        }

        private void Browse(BrowsePurpose nextPurpose, bool reset = true)
        {
            StopPresentation();
            purpose = nextPurpose;
            if (reset) { customTab = nextPurpose == BrowsePurpose.Manage; browserPage = 0; }
            NewPage(VocabularyScreen.Sets);
            string title = purpose == BrowsePurpose.Manage ? "My custom sets" : purpose == BrowsePurpose.Template ? "Choose a starting template" : "Choose a set to " + purpose.ToString().ToLowerInvariant();
            Header(title, ShowMainMenu);
            if (purpose == BrowsePurpose.Manage)
            {
                view.Button("NewBlankSet", "Create empty set", 48, 132, 530, 65, () => OpenEditor(null, "My vocabulary", Array.Empty<string>()), true);
                view.Button("NewFromTemplate", "Create from a template", 622, 132, 530, 65, () => Browse(BrowsePurpose.Template), true);
            }
            else
            {
                view.Button("PremadeTab", "Premade sets", 48, 132, 530, 65, () => { customTab = false; browserPage = 0; Browse(purpose, false); }, !customTab);
                view.Button("CustomTab", "My custom sets", 622, 132, 530, 65, () => { customTab = true; browserPage = 0; Browse(purpose, false); }, customTab, storageAvailable);
            }
            var sets = AvailableSets(customTab);
            const int pageSize = 8;
            int pages = Math.Max(1, (sets.Count + pageSize - 1) / pageSize);
            browserPage = Mathf.Clamp(browserPage, 0, pages - 1);
            for (int i = 0; i < pageSize && browserPage * pageSize + i < sets.Count; i++)
            {
                var choice = sets[browserPage * pageSize + i];
                int available = ValidIds(choice.Items).Length;
                view.Button("Set_" + choice.Id, choice.Name + "\n" + available + " items", 48 + (i % 2) * 574, 230 + (i / 2) * 126, 530, 106,
                    () => { if (purpose == BrowsePurpose.Template) OpenEditor(null, choice.Name + " copy", choice.Items); else ShowSet(choice); });
            }
            if (sets.Count == 0) view.Text("Empty", "No custom sets yet. Create one from the main menu, or start from a premade template.", 70, 300, 1060, 200, 34);
            Pager(browserPage, pages, p => { browserPage = p; Browse(purpose, false); }, 770);
            view.Text("NamespaceHelp", customTab ? "Your sets are saved separately. Names can match any premade set." : "All items includes every available vocabulary box.", 48, 847, 1104, 35, 23, VocabularyView.Muted);
        }

        private List<SetChoice> AvailableSets(bool custom)
        {
            if (custom)
                return !storageAvailable ? new List<SetChoice>() : repository.Sets.Select(s => new SetChoice { Id = s.Id, Name = s.Name, Custom = true, Items = s.ItemIds.ToArray() }).ToList();
            var sets = new List<SetChoice> { new() { Id = "premade:all", Name = "All items", Items = library.AllItemIds.ToArray() } };
            sets.AddRange(library.Categories.Select(c => new SetChoice { Id = "premade:" + c, Name = c, Items = library.GetCategory(c).Select(e => e.Id).ToArray() }));
            return sets;
        }

        private string[] ValidIds(IEnumerable<string> ids) => ids.Where(id => library.Get(id) != null).Distinct().ToArray();

        private void ShowSet(SetChoice choice)
        {
            selected = choice;
            NewPage(VocabularyScreen.SetDetails);
            Header(choice.Name, () => Browse(purpose, false));
            var valid = ValidIds(choice.Items);
            view.Text("SetType", (choice.Custom ? "MY CUSTOM SET" : "PREMADE SET") + "  ·  " + valid.Length + " items", 48, 145, 1104, 60, 29, VocabularyView.Muted);
            view.Text("Preview", string.Join("  ·  ", valid.Take(18).Select(id => library.Get(id).Name)) + (valid.Length > 18 ? "  …" : ""), 48, 235, 1104, 260, 32);
            int missing = choice.Items.Length - valid.Length;
            if (missing > 0) view.Text("Missing", $"{missing} unavailable items will be skipped. They remain in your saved set.", 48, 500, 1104, 70, 26, VocabularyView.Muted);
            if (purpose == BrowsePurpose.Manage)
            {
                view.Button("EditSet", "Edit items / rename", 48, 610, 530, 75, () => OpenEditor(choice.Id, choice.Name, choice.Items), true);
                view.Button("DuplicateSet", "Duplicate as a new set", 622, 610, 530, 75, () => OpenEditor(null, choice.Name + " copy", choice.Items));
                view.Button("DeleteSet", "Delete set", 48, 725, 530, 65, () => Confirm("Delete “" + choice.Name + "”?", "Only this custom set will be removed. Its objects and other sets are kept.", () =>
                {
                    try { repository.Delete(choice.Id); Browse(BrowsePurpose.Manage, false); }
                    catch (Exception e) { ShowFailure("Could not delete the set", e, () => ShowSet(choice)); }
                }, () => ShowSet(choice)));
            }
            else
            {
                view.Button("StartSet", purpose == BrowsePurpose.Study ? "Begin studying" : "Begin test", 48, 640, 1104, 100,
                    () => { if (purpose == BrowsePurpose.Study) StartStudy(choice.Name, valid); else StartTest(choice.Name, valid); }, true, valid.Length > 0);
                view.Text("Rules", purpose == BrowsePurpose.Study ? "Use Previous and Next to cycle through every item." : "Every word is asked once. After the round, retest everything or practice only the words you missed.",
                    48, 770, 1104, 95, 26, VocabularyView.Muted);
            }
        }

        public void StartStudy(string name, IEnumerable<string> ids)
        {
            StopPresentation();
            studyItems = ValidIds(ids);
            if (studyItems.Length == 0) return;
            selected = new SetChoice { Name = name, Items = studyItems };
            studyIndex = 0;
            spawner.Recenter();
            ShowStudyItem();
        }

        public void CycleStudy(int delta)
        {
            if (Screen != VocabularyScreen.Study || studyItems.Length == 0) return;
            studyIndex = (studyIndex + delta % studyItems.Length + studyItems.Length) % studyItems.Length;
            ShowStudyItem();
        }

        private void ShowStudyItem()
        {
            NewPage(VocabularyScreen.Study, false);
            view.Panel("StudyHeadingPanel", 0, 0, 1200, 115);
            view.Panel("StudyWordPanel", 0, 677, 1200, 223);
            Header(selected.Name + "  ·  " + (studyIndex + 1) + " / " + studyItems.Length, ShowMainMenu);
            var entry = library.Get(studyItems[studyIndex]);
            spawner.ShowItems(new[] { entry }, false);
            var name = view.Text("JapaneseWord", entry.Japanese, 48, 684, 1104, 72, 44);
            name.alignment = TextAlignmentOptions.Midline;
            var reading = view.Text("Reading", entry.Reading + "   ·   " + entry.Name, 48, 752, 1104, 52, 29, VocabularyView.Muted);
            reading.alignment = TextAlignmentOptions.Midline;
            view.Button("PreviousItem", "← Previous", 48, 820, 345, 60, () => CycleStudy(-1));
            view.Button("Recenter", "Recenter", 427, 820, 345, 60, Recenter);
            view.Button("NextItem", "Next →", 807, 820, 345, 60, () => CycleStudy(1), true);
        }

        public void StartTest(string name, IEnumerable<string> ids)
        {
            StopPresentation();
            var valid = ValidIds(ids);
            if (valid.Length == 0) return;
            selected = new SetChoice { Name = name, Items = valid };
            session = new TestSession(valid, library.AllItemIds, new System.Random(), id => Prompt(library.Get(id)));
            spawner.Recenter();
            ShowQuestion();
        }

        private string Prompt(VocabularyEntry entry) => japanese && !string.IsNullOrEmpty(entry.Japanese) ? entry.Japanese : entry.Name;

        private void ShowQuestion()
        {
            answering = false;
            if (session.IsRoundComplete) { ShowResults(); return; }
            NewPage(VocabularyScreen.Test, false);
            view.Panel("TestHeadingPanel", 0, 0, 1200, 450);
            Header(selected.Name + "  ·  Round " + session.RoundNumber, () => Confirm("Leave this test?", "This round's progress will be discarded. Your custom sets are already saved.", ShowMainMenu, ShowQuestion));
            var question = session.CurrentQuestion;
            view.Text("TestProgress", $"Word {session.AnsweredCount + 1} of {session.RoundItemIds.Count}    ·    Correct {session.CorrectCount}", 48, 115, 1104, 55, 27, VocabularyView.Muted);
            var prompt = view.Text("TargetWord", "Find: " + Prompt(library.Get(question.TargetId)), 48, 185, 1104, 105, 52);
            prompt.alignment = TextAlignmentOptions.Midline;
            spawner.ShowItems(question.ChoiceIds.Select(id => library.Get(id)).ToArray(), true);
            view.Text("TestHelp", "Hit the matching box with your hammer.", 48, 290, 1104, 70, 29);
            view.Button("Recenter", "Recenter", 850, 370, 302, 65, Recenter);
            view.Text("SmallSetHelp", selected.Items.Length < 3 ? "Extra choices come from the library. Only your selected words are scored." : "Each word counts once per round.", 48, 370, 760, 75, 24, VocabularyView.Muted);
        }

        private void Answer(string id)
        {
            if (Screen != VocabularyScreen.Test || answering || session == null || session.IsRoundComplete) return;
            answering = true;
            spawner.StopAcceptingHits();
            var question = session.CurrentQuestion;
            var target = library.Get(question.TargetId);
            bool correct = session.SubmitAnswer(question.Sequence, id);
            var feedbackLabel = view.Page.Find("TestHelp").GetComponent<TMP_Text>();
            feedbackLabel.text = correct ? "Correct!  " + target.Name : "Correct answer: " + target.Japanese + "  ·  " + target.Name;
            feedbackLabel.color = correct ? new Color(.4f, .95f, .7f) : new Color(1f, .75f, .4f);
            feedback = StartCoroutine(AdvanceAfterFeedback());
        }

        private IEnumerator AdvanceAfterFeedback()
        {
            yield return new WaitForSecondsRealtime(1.1f);
            feedback = null;
            ShowQuestion();
        }

        private void ShowResults()
        {
            StopPresentation();
            NewPage(VocabularyScreen.Results);
            Header("Round " + session.RoundNumber + " complete", ShowMainMenu);
            bool mastered = session.MissedItemIds.Count == 0;
            view.Text("ResultHeading", mastered ? "Every word correct!" : "Keep building your memory.", 48, 160, 1104, 100, 49);
            view.Text("Score", $"{session.CorrectCount} / {session.RoundItemIds.Count} correct this round", 48, 275, 1104, 65, 38);
            view.Text("Missed", mastered ? "You have answered every item in this set correctly." : session.MissedItemIds.Count + " to practice: " + string.Join(" · ", session.MissedItemIds.Take(12).Select(id => library.Get(id).Name)) + (session.MissedItemIds.Count > 12 ? " …" : ""),
                48, 365, 1104, 200, 31, VocabularyView.Muted);
            view.Button("NextMissedRound", "Round " + (session.RoundNumber + 1) + " — missed words only (" + session.MissedItemIds.Count + ")", 48, 600, 1104, 85,
                NextMissedRound, true, !mastered);
            view.Button("Retest", "Retest the full set", 48, 715, 530, 75, Retest);
            view.Button("MainMenu", "Main menu", 622, 715, 530, 75, ShowMainMenu);
        }

        public void NextMissedRound()
        {
            if (Screen != VocabularyScreen.Results || !session.StartNextMissedRound()) return;
            ShowQuestion();
        }

        public void Retest()
        {
            if (session == null) return;
            StopPresentation();
            session.Retest();
            ShowQuestion();
        }

        private void Recenter()
        {
            StopInitialPositioning();
            view.Recenter();
            spawner.Recenter();
        }

        private void OpenEditor(string id, string name, IEnumerable<string> items)
        {
            draftId = id;
            draftName = name;
            draftItems = new HashSet<string>(items);
            draftSearch = "";
            draftCategory = "";
            selectedOnly = false;
            draftPage = 0;
            DrawEditor();
        }

        private void DrawEditor()
        {
            NewPage(VocabularyScreen.Editor);
            Header(draftId == null ? "Create a custom set" : "Edit your set", () => Confirm("Discard these edits?", "Changes are saved only when you select Save set.", () => Browse(BrowsePurpose.Manage), DrawEditor));
            view.Text("NameLabel", "SET NAME", 48, 118, 1104, 35, 22, VocabularyView.Muted);
            view.Input("SetName", draftName, "Name your set", 48, 155, 1104, value => draftName = value);
            view.Input("ItemSearch", draftSearch, "Search English, Japanese or reading", 48, 240, 745, value => { draftSearch = value; draftPage = 0; RefreshEditorItems(); });
            view.Button("SelectedOnly", selectedOnly ? "Showing: selected" : "Showing: all", 820, 240, 332, 60, () => { selectedOnly = !selectedOnly; draftPage = 0; DrawEditor(); });
            view.Button("CategoryFilter", "Category: " + (string.IsNullOrEmpty(draftCategory) ? "All items" : draftCategory) + "  →", 48, 320, 745, 60, () =>
            {
                var categories = new[] { "" }.Concat(library.Categories).ToArray();
                draftCategory = categories[(Array.IndexOf(categories, draftCategory) + 1) % categories.Length];
                draftPage = 0;
                DrawEditor();
            });
            draftCount = view.Text("SelectedCount", "", 820, 320, 332, 60, 28);
            itemArea = view.Area("ItemSelection", 0, 405, 1200, 485);
            RefreshEditorItems();
        }

        private void RefreshEditorItems()
        {
            if (Screen != VocabularyScreen.Editor || itemArea == null) return;
            for (int i = itemArea.childCount - 1; i >= 0; i--) { itemArea.GetChild(i).gameObject.SetActive(false); Destroy(itemArea.GetChild(i).gameObject); }
            draftCount.text = draftItems.Count + " selected";
            var entries = library.Entries.Where(e => (string.IsNullOrEmpty(draftCategory) || e.Category == draftCategory)
                && (!selectedOnly || draftItems.Contains(e.Id)) && MatchesSearch(e)).ToArray();
            const int pageSize = 6;
            int pages = Math.Max(1, (entries.Length + pageSize - 1) / pageSize);
            draftPage = Mathf.Clamp(draftPage, 0, pages - 1);
            var visible = entries.Skip(draftPage * pageSize).Take(pageSize).ToArray();
            for (int i = 0; i < visible.Length; i++)
            {
                var entry = visible[i];
                bool included = draftItems.Contains(entry.Id);
                view.Button("Item_" + entry.Id, (included ? "✓  " : "+  ") + entry.Name + "  /  " + entry.Japanese,
                    48 + i % 2 * 574, i / 2 * 78, 530, 65, () => { if (!draftItems.Add(entry.Id)) draftItems.Remove(entry.Id); RefreshEditorItems(); }, included, parent: itemArea);
            }
            if (visible.Length == 0) view.Text("NoMatches", "No items match this filter.", 48, 20, 1104, 160, 32, parent: itemArea);
            view.Button("PreviousPage", "←", 48, 245, 100, 55, () => { draftPage--; RefreshEditorItems(); }, enabled: draftPage > 0, parent: itemArea);
            view.Text("PageCount", $"{draftPage + 1} / {pages}  ·  {entries.Length} matching", 175, 245, 500, 55, 26, parent: itemArea);
            view.Button("NextPage", "→", 680, 245, 100, 55, () => { draftPage++; RefreshEditorItems(); }, enabled: draftPage + 1 < pages, parent: itemArea);
            view.Button("AddFiltered", "Add matching", 810, 245, 342, 55, () => { foreach (var entry in entries) draftItems.Add(entry.Id); RefreshEditorItems(); }, parent: itemArea);
            view.Button("RemoveFiltered", "Remove matching", 810, 312, 342, 55, () => { foreach (var entry in entries) draftItems.Remove(entry.Id); RefreshEditorItems(); }, parent: itemArea);
            draftStatus = view.Text("EditStatus", "Templates and other sets stay unchanged.", 48, 315, 735, 55, 23, VocabularyView.Muted, itemArea);
            view.Button("SaveSet", "Save set", 48, 392, 1104, 75, SaveDraft, true, parent: itemArea);
            itemArea.gameObject.SetActive(!view.Keyboard.IsVisible);
        }

        private bool MatchesSearch(VocabularyEntry e) => string.IsNullOrWhiteSpace(draftSearch)
            || e.Name.IndexOf(draftSearch, StringComparison.OrdinalIgnoreCase) >= 0
            || (e.Japanese ?? "").IndexOf(draftSearch, StringComparison.OrdinalIgnoreCase) >= 0
            || (e.Reading ?? "").IndexOf(draftSearch, StringComparison.OrdinalIgnoreCase) >= 0;

        private void SaveDraft()
        {
            try
            {
                var ordered = library.Entries.Where(e => draftItems.Contains(e.Id)).Select(e => e.Id)
                    .Concat(draftItems.Where(id => library.Get(id) == null));
                if (draftId == null) repository.Create(draftName, ordered);
                else repository.Update(draftId, draftName, ordered);
                Browse(BrowsePurpose.Manage);
            }
            catch (Exception e) { draftStatus.text = "Could not save: " + e.Message; }
        }

        private void Header(string title, Action back)
        {
            view.Button("Back", "← Back", 48, 35, 175, 60, back);
            view.Text("Title", title, 255, 28, 897, 78, 38);
        }

        private void Pager(int page, int pages, Action<int> change, float y)
        {
            view.Button("PreviousPage", "← Previous", 48, y, 330, 60, () => change(page - 1), enabled: page > 0);
            var label = view.Text("PageCount", $"{page + 1} / {pages}", 430, y, 340, 60, 28);
            label.alignment = TextAlignmentOptions.Midline;
            view.Button("NextPage", "Next →", 822, y, 330, 60, () => change(page + 1), enabled: page + 1 < pages);
        }

        private void Confirm(string title, string description, Action accept, Action cancel)
        {
            if (feedback != null) { StopCoroutine(feedback); feedback = null; }
            spawner.StopAcceptingHits();
            foreach (var box in spawner.ActiveObjects) if (box != null) box.SetActive(false);
            NewPage(VocabularyScreen.Confirmation);
            view.Text("ConfirmTitle", title, 60, 180, 1080, 150, 44);
            view.Text("ConfirmDescription", description, 60, 360, 1080, 200, 30, VocabularyView.Muted);
            view.Button("Confirm", "Confirm", 60, 650, 520, 85, accept, true);
            view.Button("Cancel", "Go back", 620, 650, 520, 85, cancel);
        }

        private void ShowFailure(string title, Exception error, Action back)
        {
            NewPage(VocabularyScreen.Error);
            Header(title, back);
            view.Text("Error", error.Message, 48, 180, 1104, 500, 30);
        }

        private void Update()
        {
            if (!initialized || library == null || UnityEngine.XR.XRSettings.isDeviceActive) return;
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (Screen == VocabularyScreen.Study && !view.Keyboard.IsVisible)
            {
                if (keyboard?.rightArrowKey.wasPressedThisFrame == true) CycleStudy(1);
                else if (keyboard?.leftArrowKey.wasPressedThisFrame == true) CycleStudy(-1);
            }
            if (Screen != VocabularyScreen.Test || answering) return;
            int index = keyboard?.digit1Key.wasPressedThisFrame == true ? 0 : keyboard?.digit2Key.wasPressedThisFrame == true ? 1 : keyboard?.digit3Key.wasPressedThisFrame == true ? 2 : -1;
            if (index >= 0 && index < spawner.ActiveObjects.Count) spawner.SelectBox(spawner.ActiveObjects[index].GetComponent<Collider>());
            if (Mouse.current?.leftButton.wasPressedThisFrame == true)
            {
                var camera = spawner.PlayerCamera.GetComponent<Camera>();
                if (Physics.Raycast(camera.ScreenPointToRay(Mouse.current.position.ReadValue()), out var hit, 10)) spawner.SelectBox(hit.collider);
            }
        }

        private void OnDestroy()
        {
            if (spawner != null) spawner.BoxSelected -= Answer;
            if (view != null && view.Keyboard != null) view.Keyboard.VisibilityChanged -= OnKeyboardVisibility;
        }
    }
}
