using UnityEngine;
using UnityEngine.InputSystem;

namespace XRDiscovery.Vocabulary
{
    public sealed class VocabularyDesktopInput : MonoBehaviour
    {
        private VocabularyApp app;
        private VocabularyObjectPresenter presenter;
        public void Initialize(VocabularyApp owner, VocabularyObjectPresenter objectPresenter)
        { app = owner; presenter = objectPresenter; }

        private void Update()
        {
            if (app == null || !app.enabled || UnityEngine.XR.XRSettings.isDeviceActive) return;
            var keyboard = Keyboard.current;
            if (app.Screen == VocabularyScreen.Study && !app.View.Keyboard.IsVisible)
            {
                if (keyboard?.rightArrowKey.wasPressedThisFrame == true) app.CycleStudy(1);
                else if (keyboard?.leftArrowKey.wasPressedThisFrame == true) app.CycleStudy(-1);
            }
            if (app.Screen != VocabularyScreen.Test) return;
            int index = keyboard?.digit1Key.wasPressedThisFrame == true ? 0 : keyboard?.digit2Key.wasPressedThisFrame == true ? 1 : keyboard?.digit3Key.wasPressedThisFrame == true ? 2 : -1;
            if (index >= 0 && index < presenter.ActiveObjects.Count) presenter.SelectBox(presenter.ActiveObjects[index].GetComponent<Collider>());
            if (Mouse.current?.leftButton.wasPressedThisFrame == true)
            {
                var camera = presenter.PlayerCamera.GetComponent<Camera>();
                if (Physics.Raycast(camera.ScreenPointToRay(Mouse.current.position.ReadValue()), out var hit, 10)) presenter.SelectBox(hit.collider);
            }
        }
    }
}
