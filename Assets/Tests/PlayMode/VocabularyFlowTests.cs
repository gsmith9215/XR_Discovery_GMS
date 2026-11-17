using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace XRDiscovery.Vocabulary.Tests
{
    /// <summary>
    /// Exercises the real generated Canvas, its button callbacks, resource prefabs and
    /// hammer collider entry point. It uses a disposable save file, never player data.
    /// </summary>
    public sealed class VocabularyFlowTests
    {
        private GameObject host;
        private GameObject cameraObject;
        private VocabularyObjectPresenter spawner;
        private VocabularyApp app;
        private string testDirectory;
        private string savePath;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Assert.That(Object.FindObjectsByType<VocabularyApp>(FindObjectsSortMode.None), Is.Empty,
                "Run these tests in the Test Runner's isolated scene, not alongside a live game.");
            testDirectory = Path.Combine(Path.GetTempPath(), "XRDiscovery-FlowTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(testDirectory);
            savePath = Path.Combine(testDirectory, "custom-sets.json");

            cameraObject = new GameObject("Vocabulary test camera", typeof(Camera));
            cameraObject.transform.position = new Vector3(0, 1.6f, 0);
            host = new GameObject("Vocabulary test app");
            spawner = host.AddComponent<VocabularyObjectPresenter>();
            spawner.Initialize(cameraObject.transform);
            app = host.AddComponent<VocabularyApp>();
            app.enabled = false; // Physical desktop input must not interfere with deterministic UI callbacks.
            app.Initialize(spawner, savePath);
            int firstFrame = Time.frameCount;
            yield return null;
            yield return null;
            Assert.That(Time.frameCount, Is.GreaterThan(firstFrame), "Play mode must actually advance frames.");
            Assert.That(app.Screen, Is.EqualTo(VocabularyScreen.MainMenu));
            Assert.That(app.Library.Entries.Count, Is.GreaterThanOrEqualTo(4));
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (host != null) Object.Destroy(host);
            if (cameraObject != null) Object.Destroy(cameraObject);
            yield return null;
            yield return null;
            if (!string.IsNullOrEmpty(testDirectory) && Directory.Exists(testDirectory))
                Directory.Delete(testDirectory, true);
        }

        [UnityTest]
        public IEnumerator ExplicitInitializationAndStartDoNotCreateDuplicateServices()
        {
            var originalView = app.View;
            var originalRepository = app.CustomSets;
            app.enabled = true;
            yield return null;
            app.enabled = false;
            Assert.That(app.View, Is.SameAs(originalView));
            Assert.That(app.CustomSets, Is.SameAs(originalRepository));
            Assert.That(app.CustomSets.FilePath, Is.EqualTo(savePath));
            Assert.That(host.GetComponents<VocabularyApp>().Length, Is.EqualTo(1));
            Assert.That(host.GetComponents<VocabularyView>().Length, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator EveryPremadeSetCanStartStudyInItsAuthoredOrder()
        {
            foreach (var definition in app.PremadeSets.Sets)
            {
                var set = definition.Set;
                app.ShowMainMenu();
                Click("Study");
                while (!app.View.Page.GetComponentsInChildren<UnityEngine.UI.Button>().Any(b => b.name == "Set_" + set.Id))
                    Click("NextPage");
                Click("Set_" + set.Id);
                Click("StartSet");
                Assert.That(app.Screen, Is.EqualTo(VocabularyScreen.Study));
                Assert.That(spawner.ActiveObjects.Single().name, Is.EqualTo(app.Library.Get(set.ItemIds[0]).Name));
                if (set.ItemIds.Count > 1)
                {
                    Click("NextItem");
                    Assert.That(spawner.ActiveObjects.Single().name, Is.EqualTo(app.Library.Get(set.ItemIds[1]).Name));
                }
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator MainMenuLabelsRenderFullyAndButtonTextAlignsAfterNavigation()
        {
            AssertMainMenuLayout();
            Click("Study");
            Assert.That(app.Screen, Is.EqualTo(VocabularyScreen.Sets));
            Click("Back");
            yield return null;
            AssertMainMenuLayout();
            Click("Test");
            Assert.That(app.Screen, Is.EqualTo(VocabularyScreen.Sets));
            Click("Back");
            yield return null;
            AssertMainMenuLayout();
        }

        [UnityTest]
        public IEnumerator RoundedButtonsKeepTheirLabelsVisibleAcrossScreens()
        {
            void CheckButtons()
            {
                Canvas.ForceUpdateCanvases();
                foreach (var button in app.View.Page.GetComponentsInChildren<UnityEngine.UI.Button>())
                {
                    var image = button.GetComponent<UnityEngine.UI.Image>();
                    Assert.That(image, Is.Not.Null, button.name);
                    Assert.That(image.sprite, Is.Not.Null, button.name);
                    Assert.That(image.type, Is.EqualTo(UnityEngine.UI.Image.Type.Sliced), button.name);
                    foreach (var label in button.GetComponentsInChildren<TMP_Text>())
                        if (!string.IsNullOrEmpty(label.text)) AssertFullyRendered(label);
                }
            }

            CheckButtons();
            Click("Study");
            CheckButtons();
            Click("Set_premade:all");
            CheckButtons();
            Click("StartSet");
            CheckButtons();
            Click("Back");
            Click("CustomSets");
            CheckButtons();
            Click("CreateCustomSet");
            CheckButtons();
            app.View.Keyboard.Show(Input("SetName"));
            Canvas.ForceUpdateCanvases();
            foreach (var key in app.View.Keyboard.GetComponentsInChildren<UnityEngine.UI.Button>())
            {
                var image = key.GetComponent<UnityEngine.UI.Image>();
                Assert.That(image.sprite, Is.Not.Null, key.name);
                Assert.That(image.type, Is.EqualTo(UnityEngine.UI.Image.Type.Sliced), key.name);
                foreach (var label in key.GetComponentsInChildren<TMP_Text>())
                    AssertFullyRendered(label);
            }
            yield return null;
        }

        private void AssertMainMenuLayout()
        {
            Canvas.ForceUpdateCanvases();
            var titles = new TMP_Text[2];
            var descriptions = new TMP_Text[2];
            string[] names = { "Study", "Test" };
            string[] expectedTitles = { "Study", "Games" };
            for (int i = 0; i < names.Length; i++)
            {
                var button = Button(names[i]);
                var rect = (RectTransform)button.transform;
                titles[i] = button.transform.Find("Title").GetComponent<TMP_Text>();
                descriptions[i] = button.transform.Find("Description").GetComponent<TMP_Text>();
                Assert.That(titles[i].text, Is.EqualTo(expectedTitles[i]));
                foreach (var label in new[] { titles[i], descriptions[i] })
                {
                    AssertFullyRendered(label);
                    foreach (var character in label.textInfo.characterInfo.Take(label.textInfo.characterCount)
                                 .Where(character => character.isVisible))
                    {
                        Vector3 bottom = rect.InverseTransformPoint(label.transform.TransformPoint(character.bottomLeft));
                        Vector3 top = rect.InverseTransformPoint(label.transform.TransformPoint(character.topRight));
                        Assert.That(bottom.x, Is.GreaterThanOrEqualTo(rect.rect.xMin), label.name);
                        Assert.That(bottom.y, Is.GreaterThanOrEqualTo(rect.rect.yMin), label.name);
                        Assert.That(top.x, Is.LessThanOrEqualTo(rect.rect.xMax), label.name);
                        Assert.That(top.y, Is.LessThanOrEqualTo(rect.rect.yMax), label.name);
                    }
                }
                Assert.That(titles[i].textInfo.lineCount, Is.EqualTo(1));
                var titleBottom = MenuTextY(titles[i], titles[i].textBounds.min.y);
                var descriptionTop = MenuTextY(descriptions[i], descriptions[i].textBounds.max.y);
                Assert.That(titleBottom - descriptionTop, Is.GreaterThan(20f), "Leave a visible gap below each title.");
            }
            Assert.That(MenuTextY(titles[0], titles[0].textInfo.lineInfo[0].baseline),
                Is.EqualTo(MenuTextY(titles[1], titles[1].textInfo.lineInfo[0].baseline)).Within(.1f));
            Assert.That(MenuTextY(descriptions[0], descriptions[0].textInfo.lineInfo[0].baseline),
                Is.EqualTo(MenuTextY(descriptions[1], descriptions[1].textInfo.lineInfo[0].baseline)).Within(.1f));
            Assert.That(Text("Heading").text, Is.EqualTo("Vocabulary Learning Center"));
            foreach (var label in app.View.Page.GetComponentsInChildren<TMP_Text>()
                         .Where(label => !string.IsNullOrEmpty(label.text)))
                AssertFullyRendered(label);
        }

        private float MenuTextY(TMP_Text text, float localY) => app.View.Page.InverseTransformPoint(
            text.transform.TransformPoint(new Vector3(0, localY, 0))).y;

        private static void AssertFullyRendered(TMP_Text label)
        {
            label.ForceMeshUpdate();
            string visible = new string(label.textInfo.characterInfo.Take(label.textInfo.characterCount)
                .Where(character => character.isVisible).Select(character => character.character).ToArray());
            string expected = new string(label.text.Where(character => !char.IsWhiteSpace(character)).ToArray());
            Assert.That(visible, Is.EqualTo(expected), label.transform.parent.name + "/" + label.name + " must render every letter.");
            Assert.That(label.isTextOverflowing, Is.False, label.name + " must fit its rectangle.");
        }

        [UnityTest]
        public IEnumerator NavigationKeepsMenuPoseUntilRecenterIsClicked()
        {
            Transform menu = app.View.Canvas.transform;
            Vector3 position = menu.position;
            Quaternion rotation = menu.rotation;

            void Navigate(string button)
            {
                cameraObject.transform.position += new Vector3(.1f, .02f, .05f);
                cameraObject.transform.rotation *= Quaternion.Euler(0, 7, 0);
                Click(button);
                Assert.That(Vector3.Distance(menu.position, position), Is.LessThan(.0001f), button + " moved the menu.");
                Assert.That(Quaternion.Angle(menu.rotation, rotation), Is.LessThan(.001f), button + " rotated the menu.");
            }

            Navigate("Study");
            Navigate("Back");
            Navigate("Study");
            Navigate("Set_premade:all");
            Navigate("StartSet");
            Vector3 studyCenter = spawner.ActiveObjects[0].transform.parent.position;
            Assert.That(Vector3.Distance(studyCenter, position - menu.forward * .2f - Vector3.up * .04f),
                Is.LessThan(.001f), "Study objects should use the menu's placement pose.");
            Navigate("NextItem");
            Navigate("PreviousItem");
            Navigate("Back");
            Navigate("Test");
            Navigate("Set_premade:all");
            Navigate("StartSet");
            Vector3 testCenter = spawner.ActiveObjects[0].transform.parent.position;
            Assert.That(Vector3.Distance(testCenter, position - menu.forward * .7f - Vector3.up * .19f),
                Is.LessThan(.001f), "Test objects should stay centered below the scoreboard.");
            Navigate("Back");
            Navigate("Cancel");
            Navigate("Back");
            Navigate("Confirm");
            Navigate("CustomSets");
            Navigate("CreateCustomSet");
            Navigate("Back");
            Navigate("Confirm");
            Navigate("Back");

            int promptLanguage = PlayerPrefs.GetInt("Vocabulary.JapanesePrompts", 1);
            bool hadPromptLanguage = PlayerPrefs.HasKey("Vocabulary.JapanesePrompts");
            try
            {
                Navigate("PromptLanguage");
                Navigate("PromptLanguage");
            }
            finally
            {
                if (hadPromptLanguage) PlayerPrefs.SetInt("Vocabulary.JapanesePrompts", promptLanguage);
                else PlayerPrefs.DeleteKey("Vocabulary.JapanesePrompts");
                PlayerPrefs.Save();
            }

            Click("Recenter");
            Assert.That(Vector3.Distance(menu.position, position), Is.LessThan(.0001f), "The menu moved before the countdown ended.");
            yield return new WaitForSecondsRealtime(3.1f);
            Assert.That(Vector3.Distance(menu.position, position), Is.GreaterThan(.1f));
            Assert.That(Quaternion.Angle(menu.rotation, rotation), Is.GreaterThan(1f));
            Assert.That(Vector3.Angle(menu.forward, Vector3.ProjectOnPlane(cameraObject.transform.forward, Vector3.up)), Is.LessThan(.001f));
            position = menu.position;
            rotation = menu.rotation;
            Navigate("Study");
            Navigate("Back");
            yield return null;
            Assert.That(Vector3.Distance(menu.position, position), Is.LessThan(.0001f));
            Assert.That(Quaternion.Angle(menu.rotation, rotation), Is.LessThan(.001f));
        }

        [UnityTest]
        public IEnumerator MainMenuAndStudyUseRealPremadeSetAndWrapInBothDirections()
        {
            Assert.That(spawner.ActiveObjects, Is.Empty);
            Assert.That(Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            Assert.That(app.View.Canvas.renderMode, Is.EqualTo(RenderMode.WorldSpace));
            Assert.That(app.View.Canvas.GetComponent<UnityEngine.UI.GraphicRaycaster>(), Is.Not.Null);

            Click("Study");
            Click("Set_premade:all");
            Click("ShowJapaneseImmediately"); // This presentation test opts in; Study now defaults to hidden words.
            Click("StartSet");
            yield return null;
            Assert.That(app.Screen, Is.EqualTo(VocabularyScreen.Study));
            Assert.That(app.StudyIndex, Is.Zero);
            Assert.That(spawner.ActiveObjects.Count, Is.EqualTo(1));
            Assert.That(spawner.ActiveObjects[0].name, Is.EqualTo(app.Library.Entries[0].Name));
            Assert.That(Text("JapaneseWord").text, Is.EqualTo(app.Library.Entries[0].Japanese));
            AssertStudyPresentationVisible();

            Click("PreviousItem");
            yield return null;
            Assert.That(app.StudyIndex, Is.EqualTo(app.Library.Entries.Count - 1));
            Assert.That(spawner.ActiveObjects[0].name, Is.EqualTo(app.Library.Entries.Last().Name));
            AssertStudyPresentationVisible();
            Click("NextItem");
            yield return null;
            Assert.That(app.StudyIndex, Is.Zero);
            AssertStudyPresentationVisible();

            // The same collider callback used by the hammer must not answer in study mode.
            yield return new WaitForSecondsRealtime(.4f);
            spawner.SelectBox(spawner.ActiveObjects[0].GetComponent<Collider>());
            Assert.That(app.Screen, Is.EqualTo(VocabularyScreen.Study));
            Assert.That(app.Session, Is.Null);
            Click("Back");
            yield return null;
            Assert.That(app.Screen, Is.EqualTo(VocabularyScreen.MainMenu));
            Assert.That(spawner.ActiveObjects, Is.Empty);
        }

        [UnityTest]
        public IEnumerator StudySettingsAndRevealKeepControlsAndBackdropStable()
        {
            Click("Study");
            Click("Set_premade:all");
            var previewWord = Button("ShowJapaneseImmediately");
            var previewAudio = Button("JapaneseAudio");
            Assert.That(previewWord.GetComponentInChildren<TMP_Text>().text, Is.EqualTo("Word Immediately: Off"));
            Assert.That(previewAudio.GetComponentInChildren<TMP_Text>().text, Is.EqualTo("Audio Immediately: Off"));
            Click("ShowJapaneseImmediately");
            Click("JapaneseAudio");
            Click("ShowJapaneseImmediately");
            Click("JapaneseAudio");
            Assert.That(Button("ShowJapaneseImmediately"), Is.SameAs(previewWord));
            Assert.That(Button("JapaneseAudio"), Is.SameAs(previewAudio));

            Click("StartSet");
            var controls = app.View.Page.GetComponentsInChildren<UnityEngine.UI.Button>()
                .ToDictionary(button => button.name);
            var backdrop = app.View.Canvas.transform.Find("MenuBackground");
            Assert.That(backdrop, Is.Not.Null);
            var backgroundCanvas = backdrop.GetComponent<Canvas>();
            var backgroundImage = backdrop.GetComponent<UnityEngine.UI.Image>();
            Assert.That(backgroundCanvas.sortingOrder, Is.LessThan(app.View.Canvas.sortingOrder));
            Assert.That(backgroundImage.color.a, Is.EqualTo(1f));
            var japanese = app.View.Page.Find("JapaneseWord").gameObject;
            Assert.That(japanese.activeSelf, Is.False);
            Assert.That(Button("RevealJapaneseWord").GetComponentInChildren<TMP_Text>().text, Is.EqualTo("Show Word"));
            Click("RevealJapaneseWord");
            Assert.That(japanese.activeSelf, Is.True);
            Assert.That(Button("RevealJapaneseWord").GetComponentInChildren<TMP_Text>().text, Is.EqualTo("Hide Word"));
            Click("RevealJapaneseWord");
            Assert.That(japanese.activeSelf, Is.False);
            Click("ShowJapaneseImmediately");
            Assert.That(japanese.activeSelf, Is.True);
            Click("JapaneseAudio");
            Click("JapaneseVoice");
            Click("NextItem");
            yield return null;

            foreach (var control in controls)
                Assert.That(Button(control.Key), Is.SameAs(control.Value), "Study must retain " + control.Key);
            Assert.That(app.View.Canvas.transform.Find("MenuBackground"), Is.SameAs(backdrop));
            Assert.That(backgroundImage.enabled, Is.True);
            Assert.That(backgroundCanvas.enabled, Is.True);
            Click("Back");
            Click("Study");
            Click("Set_premade:all");
            Assert.That(Button("ShowJapaneseImmediately").GetComponentInChildren<TMP_Text>().text, Does.EndWith(": Off"));
            Assert.That(Button("JapaneseAudio").GetComponentInChildren<TMP_Text>().text, Does.EndWith(": Off"));
        }

        [UnityTest]
        public IEnumerator CustomWordSelectionReusesRowsAndPreservesSearchAndScroll()
        {
            Click("CustomSets");
            Click("CreateCustomSet");
            Click("AddWords");
            var rows = app.View.Page.GetComponentsInChildren<UnityEngine.UI.Button>()
                .Where(button => button.name.StartsWith("Item_", StringComparison.Ordinal))
                .ToDictionary(button => button.name);
            var add = Button("AddFiltered");
            var remove = Button("RemoveFiltered");
            var search = Input("ItemSearch");
            var scroll = app.View.Page.GetComponentsInChildren<UnityEngine.UI.ScrollRect>().Single();
            Canvas.ForceUpdateCanvases();
            scroll.content.anchoredPosition = new Vector2(0, 160);
            string firstId = app.Library.Entries[0].Id;
            Click("Item_" + firstId);
            Assert.That(Text("SelectedCount").text, Is.EqualTo("1 selected"));
            Assert.That(Button("Item_" + firstId).GetComponentInChildren<TMP_Text>().text, Does.StartWith("✓"));
            Assert.That(scroll.content.anchoredPosition.y, Is.EqualTo(160).Within(.01f));
            Click("AddFiltered");
            Assert.That(Text("SelectedCount").text, Is.EqualTo(app.Library.Entries.Count + " selected"));
            Click("RemoveFiltered");
            Assert.That(Text("SelectedCount").text, Is.EqualTo("0 selected"));
            Assert.That(scroll.content.anchoredPosition.y, Is.EqualTo(160).Within(.01f));
            foreach (var row in rows)
                Assert.That(Button(row.Key), Is.SameAs(row.Value), "Membership edits must retain " + row.Key);
            Assert.That(Button("AddFiltered"), Is.SameAs(add));
            Assert.That(Button("RemoveFiltered"), Is.SameAs(remove));

            EventSystem.current.SetSelectedGameObject(search.gameObject);
            search.text = app.Library.Entries[0].Name;
            Assert.That(Input("ItemSearch"), Is.SameAs(search));
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(search.gameObject));
            Assert.That(app.View.Keyboard.Target, Is.SameAs(search));
            search.text = "no-vocabulary-matches-this-filter";
            search.text = "";
            Assert.That(scroll.content.anchoredPosition.y, Is.EqualTo(0).Within(.01f));
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(search.gameObject));
            app.View.Keyboard.Hide();
            foreach (var row in rows)
                Assert.That(Button(row.Key), Is.SameAs(row.Value), "Search must reuse " + row.Key);
            Assert.That(Button("AddFiltered"), Is.SameAs(add));
            Assert.That(Button("RemoveFiltered"), Is.SameAs(remove));
            yield return null;
        }

        [UnityTest]
        public IEnumerator HammerTestScoresFourTargetsThenMissedOnlyRoundAndFullRetest()
        {
            string[] targets = FourDistinctTargets();
            app.StartTest("Four-item integration test", targets);
            string missedTarget = app.Session.CurrentQuestion.TargetId;
            var asked = new List<string>();
            while (app.Screen == VocabularyScreen.Test)
            {
                TestQuestion question = app.Session.CurrentQuestion;
                asked.Add(question.TargetId);
                yield return Answer(question.TargetId != missedTarget);
            }
            Assert.That(app.Screen, Is.EqualTo(VocabularyScreen.Results));
            Assert.That(asked, Is.EquivalentTo(targets));
            Assert.That(app.Session.AnsweredCount, Is.EqualTo(4));
            Assert.That(app.Session.CorrectCount, Is.EqualTo(3));
            Assert.That(app.Session.MissedItemIds, Is.EqualTo(new[] { missedTarget }));
            Assert.That(spawner.ActiveObjects, Is.Empty);

            Click("NextMissedRound");
            Assert.That(app.Session.RoundNumber, Is.EqualTo(2));
            Assert.That(app.Session.RoundItemIds, Is.EqualTo(new[] { missedTarget }));
            Assert.That(spawner.ActiveObjects.Count, Is.EqualTo(3));
            yield return Answer(true);
            Assert.That(app.Screen, Is.EqualTo(VocabularyScreen.Results));
            Assert.That(app.Session.CorrectCount, Is.EqualTo(1));
            Assert.That(app.Session.MissedItemIds, Is.Empty);
            Assert.That(Button("NextMissedRound").interactable, Is.False);

            Click("Retest");
            Assert.That(app.Screen, Is.EqualTo(VocabularyScreen.Test));
            Assert.That(app.Session.RoundNumber, Is.EqualTo(1));
            Assert.That(app.Session.RoundItemIds, Is.EquivalentTo(targets));
            Assert.That(app.Session.AnsweredCount, Is.Zero);
            Assert.That(app.Session.CorrectCount, Is.Zero);
            yield return null;
        }

        [UnityTest]
        public IEnumerator LeavingTestSuspendsHitsAndFeedbackCannotRestartAfterReturningToMenu()
        {
            app.StartTest("Navigation test", FourDistinctTargets());
            yield return new WaitForSecondsRealtime(.4f);
            Collider oldBox = spawner.ActiveObjects[0].GetComponent<Collider>();
            Click("Back");
            Assert.That(app.Screen, Is.EqualTo(VocabularyScreen.Confirmation));
            Assert.That(spawner.ActiveObjects.All(box => !box.activeInHierarchy), Is.True);
            spawner.SelectBox(oldBox);
            Assert.That(app.Session.AnsweredCount, Is.Zero);
            Click("Cancel");
            Assert.That(app.Screen, Is.EqualTo(VocabularyScreen.Test));
            Assert.That(app.Session.AnsweredCount, Is.Zero);

            yield return new WaitForSecondsRealtime(.4f);
            TestQuestion question = app.Session.CurrentQuestion;
            int choice = question.ChoiceIds.ToList().IndexOf(question.TargetId);
            Collider answer = spawner.ActiveObjects[choice].GetComponent<Collider>();
            spawner.SelectBox(answer);
            spawner.SelectBox(answer);
            Assert.That(app.Session.AnsweredCount, Is.EqualTo(1), "Repeated trigger entries cannot score twice.");
            Click("Back"); // Leave while the 1.1-second answer feedback coroutine is running.
            Click("Confirm");
            Assert.That(app.Screen, Is.EqualTo(VocabularyScreen.MainMenu));
            yield return new WaitForSecondsRealtime(1.3f);
            Assert.That(app.Screen, Is.EqualTo(VocabularyScreen.MainMenu));
            Assert.That(app.Session, Is.Null);
            Assert.That(spawner.ActiveObjects, Is.Empty);
        }

        [UnityTest]
        public IEnumerator CustomSetCreateDuplicateRenameMembershipAndDeletePersistThroughUi()
        {
            var premade = app.PremadeSets.Sets.First().Set;
            string category = premade.Name;
            string[] template = premade.ItemIds.ToArray();
            Assert.That(template.Length, Is.GreaterThan(0));

            Click("CustomSets");
            Assert.That(app.View.Page.Find("NewFromTemplate"), Is.Null);
            Click("CreateCustomSet");
            Assert.That(app.Screen, Is.EqualTo(VocabularyScreen.Editor));
            Assert.That(app.View.Page.Find("SelectedCount").GetComponent<TMP_Text>().text, Is.EqualTo("0 words in this set"));
            Click("AddWords");
            foreach (string itemId in template) Click("Item_" + itemId);
            Click("Back");
            Input("SetName").text = category; // Same name as its premade template is explicitly allowed.
            Click("SaveSet");
            yield return null;
            VocabularySet original = app.CustomSets.Sets.Single();
            Assert.That(original.Name, Is.EqualTo(category));
            Assert.That(original.ItemIds, Is.EquivalentTo(template));
            Assert.That(app.PremadeSets.Sets.Single(s => s.Set.Id == premade.Id).Set.ItemIds, Is.EquivalentTo(template));

            Click("Set_" + original.Id);
            Click("DuplicateSet");
            Input("SetName").text = "私の練習セット";
            string removedId = template[0];
            Click("AddWords");
            Input("ItemSearch").text = app.Library.Get(removedId).Name;
            yield return null;
            Click("Item_" + removedId);
            Click("Back");
            Click("SaveSet");
            yield return null;
            VocabularySet duplicate = app.CustomSets.Sets.Single(set => set.Id != original.Id);
            Assert.That(duplicate.Name, Is.EqualTo("私の練習セット"));
            Assert.That(duplicate.ItemIds, Is.EquivalentTo(template.Where(id => id != removedId)));
            Assert.That(app.CustomSets.Sets.Single(set => set.Id == original.Id).ItemIds, Is.EquivalentTo(template));

            Click("Set_" + duplicate.Id);
            Click("EditSet");
            Input("SetName").text = category;
            string addedId = app.Library.AllItemIds.First(id => !template.Contains(id));
            Click("AddWords");
            Input("ItemSearch").text = app.Library.Get(addedId).Name;
            yield return null;
            Click("Item_" + addedId);
            Click("Back");
            Click("SaveSet");
            yield return null;
            IReadOnlyList<VocabularySet> reloaded = new CustomSetRepository(savePath).Load();
            Assert.That(reloaded.Count, Is.EqualTo(2));
            Assert.That(reloaded.All(set => set.Name == category), Is.True);
            Assert.That(reloaded.Single(set => set.Id == duplicate.Id).ItemIds,
                Is.EquivalentTo(template.Where(id => id != removedId).Concat(new[] { addedId })));

            // Custom sets are selectable separately for Study and Test.
            app.ShowMainMenu();
            Click("Study");
            Click("CustomTab");
            Click("Set_" + duplicate.Id);
            Click("StartSet");
            Assert.That(app.Screen, Is.EqualTo(VocabularyScreen.Study));
            Assert.That(spawner.ActiveObjects.Count, Is.EqualTo(1));
            Click("Back");
            Click("Test");
            Click("CustomTab");
            Click("Set_" + duplicate.Id);
            Click("StartSet");
            Assert.That(app.Screen, Is.EqualTo(VocabularyScreen.Test));
            Assert.That(app.Session.OriginalItemIds, Is.EquivalentTo(reloaded.Single(set => set.Id == duplicate.Id).ItemIds));

            app.ShowMainMenu();
            Click("CustomSets");
            Click("Set_" + duplicate.Id);
            Click("DeleteSet");
            Click("Confirm");
            yield return null;
            IReadOnlyList<VocabularySet> afterDelete = new CustomSetRepository(savePath).Load();
            Assert.That(afterDelete.Count, Is.EqualTo(1));
            Assert.That(afterDelete[0].Id, Is.EqualTo(original.Id));
            Assert.That(afterDelete[0].ItemIds, Is.EquivalentTo(template));
        }

        [UnityTest]
        public IEnumerator EmptyAndInvalidDraftsAreHandledWithoutDiscardingSavedItems()
        {
            Click("CustomSets");
            Click("CreateCustomSet");
            Input("SetName").text = "  ";
            Click("SaveSet");
            Assert.That(app.Screen, Is.EqualTo(VocabularyScreen.Editor));
            Assert.That(Text("EditStatus").text, Does.StartWith("Could not save:"));
            Assert.That(app.CustomSets.Sets, Is.Empty);

            Input("SetName").text = "For later";
            Click("SaveSet");
            VocabularySet empty = app.CustomSets.Sets.Single();
            Assert.That(empty.ItemIds, Is.Empty);
            app.ShowMainMenu();
            Click("Test");
            Click("CustomTab");
            Click("Set_" + empty.Id);
            Assert.That(Button("StartSet").interactable, Is.False);

            app.ShowMainMenu();
            Click("CustomSets");
            Click("Set_" + empty.Id);
            Click("EditSet");
            Input("SetName").text = "Unsaved changes";
            Click("Back");
            Click("Confirm");
            Assert.That(new CustomSetRepository(savePath).Load().Single().Name, Is.EqualTo("For later"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator WorldKeyboardPreservesSelectionAndDeletesWholeUnicodeTextElements()
        {
            Click("CustomSets");
            Click("CreateCustomSet");
            TMP_InputField field = Input("SetName");
            VocabularyKeyboard keyboard = app.View.Keyboard;
            var visibility = new List<bool>();
            keyboard.VisibilityChanged += visibility.Add;
            keyboard.Bind(field); // Binding an already-bound field must remain harmless.
            EventSystem.current.SetSelectedGameObject(field.gameObject);
            yield return null;
            Assert.That(keyboard.IsVisible, Is.True);
            Assert.That(keyboard.Target, Is.SameAs(field));
            keyboard.Show(field);

            field.text = "abcdef";
            field.selectionStringAnchorPosition = 2;
            field.selectionStringFocusPosition = 4;
            keyboard.Insert("XY");
            Assert.That(field.text, Is.EqualTo("abXYef"));
            field.selectionStringAnchorPosition = 4;
            field.selectionStringFocusPosition = 2;
            keyboard.Insert("Z");
            Assert.That(field.text, Is.EqualTo("abZef"), "Reverse selections must also replace only the selected substring.");

            field.text = "A\U0001F642か\u3099";
            field.stringPosition = field.text.Length;
            keyboard.Backspace();
            Assert.That(field.text, Is.EqualTo("A\U0001F642"), "The combining dakuten must leave with its base kana.");
            keyboard.Backspace();
            Assert.That(field.text, Is.EqualTo("A"), "A surrogate-pair emoji must be removed as one text element.");

            keyboard.GetComponentsInChildren<UnityEngine.UI.Button>().Single(button => button.name == "DoneKey").onClick.Invoke();
            Assert.That(keyboard.IsVisible, Is.False);
            Assert.That(keyboard.Target, Is.Null);
            Assert.That(visibility, Is.EqualTo(new[] { true, false }));
            Assert.That(Button("SaveSet").gameObject.activeInHierarchy, Is.True);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ChangingScreenClosesKeyboardAndClearsItsInputTarget()
        {
            Click("CustomSets");
            Click("CreateCustomSet");
            TMP_InputField field = Input("SetName");
            app.View.Keyboard.Show(field);
            Assert.That(app.View.Keyboard.IsVisible, Is.True);
            Assert.That(app.View.Page.GetComponentsInChildren<UnityEngine.UI.Button>()
                .Any(button => button.name == "SaveSet"), Is.False, "The keyboard hides the underlying item controls.");

            app.ShowMainMenu();
            yield return null;
            Assert.That(app.View.Keyboard.IsVisible, Is.False);
            Assert.That(app.View.Keyboard.Target, Is.Null);
            Assert.That(app.Screen, Is.EqualTo(VocabularyScreen.MainMenu));
            Assert.That(Button("Study").interactable, Is.True);
        }

        [UnityTest]
        public IEnumerator ControllerPointerFiltersOnlyRuntimeXrBindingsAndRestoresDesktopActions()
        {
            var pointer = host.GetComponent<VocabularyPointerInput>();
            var module = EventSystem.current.GetComponent<InputSystemUIInputModule>();
            Assert.That(pointer, Is.Not.Null);
            Assert.That(module, Is.Not.Null);
            pointer.enabled = false;
            InputActionAsset original = module.actionsAsset;
            string originalJson = original.ToJson();
            string originalOverrides = original.SaveBindingOverridesAsJson();
            string[] mousePaths = original.actionMaps.SelectMany(map => map.bindings)
                .Select(binding => binding.effectivePath).Where(path => path != null && path.Contains("<Mouse>"))
                .ToArray();
            Assert.That(mousePaths, Is.Not.Empty);
            Assert.That(original.actionMaps.SelectMany(map => map.bindings).Any(IsXrBinding), Is.True);

            pointer.enabled = true;
            pointer.Configure(app.View.Canvas, null, null, cameraObject.GetComponent<Camera>());
            InputActionAsset runtime = module.actionsAsset;
            Assert.That(runtime, Is.Not.SameAs(original));
            foreach (InputBinding binding in runtime.actionMaps.SelectMany(map => map.bindings))
                if (IsXrBinding(binding))
                    Assert.That(binding.effectivePath, Is.Null.Or.Empty, "The desktop module must not also consume controller triggers.");
            string[] runtimeMousePaths = runtime.actionMaps.SelectMany(map => map.bindings)
                .Select(binding => binding.effectivePath).Where(path => path != null && path.Contains("<Mouse>"))
                .ToArray();
            Assert.That(runtimeMousePaths, Is.EquivalentTo(mousePaths));
            Assert.That(original.ToJson(), Is.EqualTo(originalJson));
            Assert.That(original.SaveBindingOverridesAsJson(), Is.EqualTo(originalOverrides));

            pointer.enabled = false;
            Assert.That(module.actionsAsset, Is.SameAs(original));
            yield return null;
            Assert.That(runtime == null, Is.True, "The temporary action asset must be destroyed when the pointer is disabled.");

            pointer.enabled = true;
            pointer.Configure(app.View.Canvas, null, null, cameraObject.GetComponent<Camera>());
            Object.Destroy(pointer);
            yield return null;
            Assert.That(module.actionsAsset, Is.SameAs(original));
        }

        private static bool IsXrBinding(InputBinding binding)
        {
            string groups = ";" + binding.groups + ";";
            string path = binding.path ?? "";
            return groups.IndexOf(";XR;", StringComparison.OrdinalIgnoreCase) >= 0 ||
                path.IndexOf("XRController", StringComparison.OrdinalIgnoreCase) >= 0 ||
                path.IndexOf("TrackedDevice", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        [UnityTest]
        public IEnumerator EveryStudyObjectClearsTheWordArea()
        {
            app.StartStudy("Layout check", app.Library.Entries.Select(entry => entry.Id), true);
            Camera camera = cameraObject.GetComponent<Camera>();
            float minimumClearance = float.PositiveInfinity;
            string closestObject = "";
            for (int i = 0; i < app.Library.Entries.Count; i++)
            {
                yield return null;
                Canvas.ForceUpdateCanvases();
                var corners = new Vector3[4];
                Text("EnglishWord").rectTransform.GetWorldCorners(corners);
                float textTop = corners.Max(point => camera.WorldToViewportPoint(point).y);
                foreach (var renderer in spawner.ActiveObjects[0].GetComponentsInChildren<Renderer>())
                {
                    if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                    Bounds bounds = renderer.bounds;
                    for (int corner = 0; corner < 8; corner++)
                    {
                        Vector3 point = bounds.center + Vector3.Scale(bounds.extents, new Vector3(
                            (corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
                        float clearance = camera.WorldToViewportPoint(point).y - textTop;
                        if (clearance < minimumClearance)
                        {
                            minimumClearance = clearance;
                            closestObject = spawner.ActiveObjects[0].name + "/" + renderer.name;
                        }
                    }
                }
                foreach (string label in new[] { "EnglishWord", "JapaneseWord", "Reading" })
                    AssertFullyRendered(Text(label));
                Vector3 englishPosition = Text("EnglishWord").transform.position;
                Click("RevealJapaneseWord");
                Click("RevealJapaneseWord");
                Assert.That(Text("EnglishWord").transform.position, Is.EqualTo(englishPosition));
                app.CycleStudy(1);
            }
            Assert.That(minimumClearance, Is.GreaterThan(.005f), closestObject + " is too close to the word area.");
        }

        private void AssertStudyPresentationVisible()
        {
            Canvas.ForceUpdateCanvases();
            TMP_Text japanese = Text("JapaneseWord");
            japanese.ForceMeshUpdate();
            Assert.That(japanese.textInfo.characterInfo.Take(japanese.textInfo.characterCount)
                    .Any(character => character.isVisible && character.character != '\u2026'), Is.True,
                "The Japanese word must render readable glyphs, not disappear because its font line height exceeds the label.");

            Camera camera = cameraObject.GetComponent<Camera>();
            Renderer model = spawner.ActiveObjects[0].GetComponentsInChildren<Renderer>()
                .Where(renderer => renderer.enabled && renderer.gameObject.activeInHierarchy &&
                    renderer.name != "BoxVisual" && renderer.bounds.size.sqrMagnitude > 0)
                .OrderByDescending(renderer => renderer.bounds.size.sqrMagnitude).FirstOrDefault();
            Assert.That(model, Is.Not.Null, "A study box must contain a visible object model.");
            Assert.That(GeometryUtility.TestPlanesAABB(GeometryUtility.CalculateFrustumPlanes(camera), model.bounds),
                Is.True, "The object model must lie in the player's camera frustum.");
            Vector3 viewport = camera.WorldToViewportPoint(model.bounds.center);
            Assert.That(viewport.z, Is.GreaterThan(camera.nearClipPlane));
            Assert.That(viewport.x, Is.InRange(0f, 1f));
            Assert.That(viewport.y, Is.InRange(0f, 1f), "The object must not spawn above the player's field of view.");
        }

        private string[] FourDistinctTargets() => app.Library.Entries
            .GroupBy(entry => entry.Prompt).Select(group => group.First().Id).Take(4).ToArray();

        private IEnumerator Answer(bool correct)
        {
            TestQuestion question = app.Session.CurrentQuestion;
            Assert.That(spawner.ActiveObjects.Count, Is.EqualTo(question.ChoiceIds.Count));
            Assert.That(question.ChoiceIds.Count, Is.EqualTo(3));
            int choice = question.ChoiceIds.ToList().FindIndex(id => correct ? id == question.TargetId : id != question.TargetId);
            Assert.That(choice, Is.GreaterThanOrEqualTo(0));
            Collider collider = spawner.ActiveObjects[choice].GetComponent<Collider>();
            Assert.That(collider, Is.Not.Null);
            int before = app.Session.AnsweredCount;
            yield return new WaitForSecondsRealtime(.4f);
            spawner.SelectBox(collider);
            spawner.SelectBox(collider);
            Assert.That(app.Session.AnsweredCount, Is.EqualTo(before + 1));
            yield return new WaitForSecondsRealtime(1.2f);
            yield return null;
        }

        private UnityEngine.UI.Button Button(string name)
        {
            var matches = app.View.Page.GetComponentsInChildren<UnityEngine.UI.Button>()
                .Where(button => button.name == name && button.gameObject.activeInHierarchy).ToArray();
            Assert.That(matches.Length, Is.EqualTo(1), "Expected one active button named " + name + " on " + app.Screen);
            return matches[0];
        }

        private void Click(string name)
        {
            UnityEngine.UI.Button button = Button(name);
            Assert.That(button.interactable, Is.True, "Button is disabled: " + name);
            button.onClick.Invoke();
        }

        private TMP_InputField Input(string name) => app.View.Page.GetComponentsInChildren<TMP_InputField>()
            .Single(field => field.name == name && field.gameObject.activeInHierarchy);

        private TMP_Text Text(string name) => app.View.Page.GetComponentsInChildren<TMP_Text>()
            .Single(text => text.name == name && text.gameObject.activeInHierarchy);
    }
}
