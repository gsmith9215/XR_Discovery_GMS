using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace XRDiscovery.Vocabulary
{
    public enum VocabularyScreen { MainMenu, Sets, SetDetails, Editor, Study, Test, Results, Confirmation, Error }

    /// <summary>Scene entry point: connects services, owns navigation and coordinates screen exit.</summary>
    public sealed class VocabularyApp : MonoBehaviour
    {
        [SerializeField] private VocabularyObjectPresenter presenter;
        [SerializeField] private GameObject hammer;
        private bool initialized;
        private bool japanese = true;
        private Coroutine initialPositioning;
        private SetBrowserController browser;
        private SetEditorController editor;
        private StudyController study;
        private VocabularyTestController test;
        private VocabularyDesktopInput desktop;
        private VocabularyScreens screens;
        public VocabularyScreen Screen { get; private set; }
        public VocabularyLibrary Library { get; private set; }
        public PremadeSetCatalog PremadeSets { get; private set; }
        public CustomSetRepository CustomSets { get; private set; }
        public VocabularyView View { get; private set; }
        public TestSession Session => test?.Session;
        public int StudyIndex => study?.Index ?? 0;
        internal VocabularyObjectPresenter Presenter => presenter;
        internal bool StorageAvailable { get; private set; }
        internal string StorageNotice { get; private set; }
        internal VocabularyScreens Screens => screens;

        private void Start()
        {
            if (!initialized) Initialize(presenter != null ? presenter : GetComponent<VocabularyObjectPresenter>());
        }

        public void Initialize(VocabularyObjectPresenter objectPresenter, string customSetsPath = null)
        {
            if (initialized) return;
            initialized = true;
            presenter = objectPresenter;
            try
            {
                if (presenter == null) throw new InvalidOperationException("Assign a VocabularyObjectPresenter to VocabularyApp.");
                presenter.Initialize();
                View = gameObject.AddComponent<VocabularyView>();
                View.Initialize(presenter.PlayerCamera, presenter.RightHandAnchor);
                screens = new VocabularyScreens(View);
                japanese = PlayerPrefs.GetInt("Vocabulary.JapanesePrompts", 1) != 0;
                Library = VocabularyLibrary.Load();
                if (Library.Entries.Count == 0) throw new InvalidOperationException("The vocabulary library is empty.");
                PremadeSets = PremadeSetCatalog.Load(Library);
                CustomSets = new CustomSetRepository(customSetsPath ?? Path.Combine(Application.persistentDataPath, "Vocabulary", "custom-sets.json"));
                try
                {
                    CustomSets.Load();
                    StorageAvailable = true;
                    StorageNotice = CustomSets.RecoveryNotice;
                }
                catch (Exception e)
                {
                    StorageNotice = "Custom sets could not be loaded. Your saved file has been preserved. " + e.Message;
                    Debug.LogWarning(StorageNotice);
                }
                browser = new SetBrowserController(this);
                editor = new SetEditorController(this);
                study = new StudyController(this);
                test = new VocabularyTestController(this);
                presenter.BoxSelected += test.Answer;
                View.Keyboard.VisibilityChanged += editor.SetKeyboardVisible;
                desktop = gameObject.AddComponent<VocabularyDesktopInput>();
                desktop.Initialize(this, presenter);
                ShowMainMenu();
                initialPositioning = StartCoroutine(PositionAfterTrackingStarts());
            }
            catch (Exception e)
            {
                Screen = VocabularyScreen.Error;
                if (View != null)
                {
                    View.Clear();
                    View.Text("Error", "Vocabulary could not load", 48, 70, 1104, 80, 44);
                    View.Text("Details", e.Message, 48, 170, 1104, 400, 28);
                }
                Debug.LogException(e, this);
            }
        }

        // Every navigation exits presentation before drawing the destination.
        internal void NewPage(VocabularyScreen screen, bool solid = true)
        {
            StopInitialPositioning();
            test?.Suspend();
            presenter.ClearObjects();
            Screen = screen;
            View.Clear(solid);
            if (hammer != null) hammer.SetActive(screen == VocabularyScreen.Test);
        }

        public void ShowMainMenu()
        {
            test?.Reset();
            NewPage(VocabularyScreen.MainMenu);
            screens.MainMenu(japanese, StorageAvailable, StorageNotice,
                () => browser.Browse(BrowsePurpose.Study), () => browser.Browse(BrowsePurpose.Test),
                () => browser.Browse(BrowsePurpose.Manage), TogglePromptLanguage, Recenter);
        }

        private void TogglePromptLanguage()
        {
            japanese = !japanese;
            PlayerPrefs.SetInt("Vocabulary.JapanesePrompts", japanese ? 1 : 0);
            PlayerPrefs.Save();
            ShowMainMenu();
        }

        internal string Prompt(VocabularyEntry entry) => japanese && !string.IsNullOrEmpty(entry.Japanese) ? entry.Japanese : entry.Name;
        internal string[] ValidIds(IEnumerable<string> ids) => ids.Where(id => Library.Get(id) != null).Distinct().ToArray();
        internal void EditSet(string id, string name, IEnumerable<string> items) => editor.Open(id, name, items);
        internal void ManageSets() => browser.Browse(BrowsePurpose.Manage, false);
        public void StartStudy(string name, IEnumerable<string> ids) => study.Start(name, ids);
        public void CycleStudy(int delta) => study.Move(delta);
        public void StartTest(string name, IEnumerable<string> ids) => test.Start(name, ids);
        public void NextMissedRound() => test.NextMissedRound();
        public void Retest() => test.Retest();

        internal void Confirm(string title, string description, Action accept, Action cancel)
        {
            NewPage(VocabularyScreen.Confirmation);
            screens.Confirm(title, description, accept, cancel);
        }

        internal void ShowFailure(string title, Exception error, Action back)
        {
            NewPage(VocabularyScreen.Error);
            screens.Failure(title, error.Message, back);
        }

        public void Recenter()
        {
            StopInitialPositioning();
            View.Recenter();
            presenter.Recenter(View.PlacementOrigin, View.PlacementForward);
        }

        private IEnumerator PositionAfterTrackingStarts()
        {
            // The XR rig initializes its anchors after Start. Navigation cancels this correction.
            yield return null;
            yield return new WaitForEndOfFrame();
            if (Screen == VocabularyScreen.MainMenu) View.Recenter();
            while (enabled && Screen == VocabularyScreen.MainMenu)
            {
                var head = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.Head);
                if (head.isValid && head.TryGetFeatureValue(UnityEngine.XR.CommonUsages.isTracked, out bool tracked) && tracked)
                {
                    yield return new WaitForEndOfFrame();
                    if (Screen == VocabularyScreen.MainMenu) View.Recenter();
                    break;
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

        private void OnDisable()
        {
            StopInitialPositioning();
            test?.Suspend();
        }

        private void OnDestroy()
        {
            if (presenter != null && test != null) presenter.BoxSelected -= test.Answer;
            if (View != null && View.Keyboard != null && editor != null)
                View.Keyboard.VisibilityChanged -= editor.SetKeyboardVisible;
        }
    }
}
