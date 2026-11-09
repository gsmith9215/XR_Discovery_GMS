using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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
        private RandomObjectSpawner spawner;
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
            spawner = host.AddComponent<RandomObjectSpawner>();
            spawner.enabled = false; // Initialize explicitly with disposable storage, before Start can run.
            typeof(RandomObjectSpawner).GetField("playerCamera", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(spawner, cameraObject.transform);
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
            Navigate("NextItem");
            Navigate("PreviousItem");
            Navigate("Back");
            Navigate("Test");
            Navigate("Set_premade:all");
            Navigate("StartSet");
            Navigate("Back");
            Navigate("Cancel");
            Navigate("Back");
            Navigate("Confirm");
            Navigate("CustomSets");
            Navigate("NewBlankSet");
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
        public IEnumerator CustomSetTemplateDuplicateRenameMembershipAndDeletePersistThroughUi()
        {
            string category = app.Library.Categories.First(); // Always on the first premade browser page.
            string[] template = app.Library.GetCategory(category).Select(entry => entry.Id).ToArray();
            Assert.That(template.Length, Is.GreaterThan(0));

            Click("CustomSets");
            Click("NewFromTemplate");
            Click("Set_premade:" + category);
            Input("SetName").text = category; // Same name as its premade template is explicitly allowed.
            Click("SaveSet");
            yield return null;
            VocabularySet original = app.CustomSets.Sets.Single();
            Assert.That(original.Name, Is.EqualTo(category));
            Assert.That(original.ItemIds, Is.EquivalentTo(template));
            Assert.That(app.Library.GetCategory(category).Select(entry => entry.Id), Is.EquivalentTo(template));

            Click("Set_" + original.Id);
            Click("DuplicateSet");
            Input("SetName").text = "私の練習セット";
            string removedId = template[0];
            Input("ItemSearch").text = app.Library.Get(removedId).Name;
            yield return null;
            Click("Item_" + removedId);
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
            Input("ItemSearch").text = app.Library.Get(addedId).Name;
            yield return null;
            Click("Item_" + addedId);
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
            Click("NewBlankSet");
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
            Click("NewBlankSet");
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
            Click("NewBlankSet");
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
