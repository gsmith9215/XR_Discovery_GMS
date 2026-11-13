using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.XR;
using InputActionAsset = UnityEngine.InputSystem.InputActionAsset;
using InputSystemUIInputModule = UnityEngine.InputSystem.UI.InputSystemUIInputModule;

namespace XRDiscovery.Vocabulary
{
    /// <summary>
    /// A controller pointer for the flat, world-space vocabulary canvas. It shares the
    /// existing EventSystem with desktop input without replacing its input module.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class VocabularyPointerInput : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float maximumDistance = 8f;
        [SerializeField, Min(0.001f)] private float beamWidth = 0.003f;
        [SerializeField, Min(0f)] private float scrollSpeed = 40f;

        private Canvas canvas;
        private UnityEngine.UI.GraphicRaycaster raycaster;
        private Transform rightHand;
        private Transform leftHand;
        private Camera viewCamera;
        private EventSystem eventSystem;
        private PointerEventData pointer;
        private readonly List<RaycastResult> raycastResults = new List<RaycastResult>();
        private readonly List<Transform> hovered = new List<Transform>();
        private readonly List<Transform> nextHovered = new List<Transform>();
        private LineRenderer beam;
        private GameObject tip;
        private Material pointerMaterial;
        private bool wasPressed;
        private bool requireRelease = true;
        private InputDevice activeDevice;
        private InputSystemUIInputModule desktopInput;
        private InputActionAsset originalActions;
        private InputActionAsset desktopActions;

        public void Configure(Canvas targetCanvas, Transform rightController, Transform leftController, Camera camera)
        {
            CancelPointer();
            canvas = targetCanvas;
            raycaster = canvas != null ? canvas.GetComponent<UnityEngine.UI.GraphicRaycaster>() : null;
            rightHand = rightController;
            leftHand = leftController;
            viewCamera = camera;
            if (canvas != null && camera != null)
                canvas.worldCamera = camera;
            ConfigureDesktopInput();
            requireRelease = true;
        }

        private void LateUpdate()
        {
            if (canvas == null || !canvas.isActiveAndEnabled || raycaster == null ||
                !raycaster.isActiveAndEnabled || viewCamera == null || EventSystem.current == null ||
                (viewCamera.cullingMask & (1 << canvas.gameObject.layer)) == 0)
            {
                Suspend();
                return;
            }

            if (!TryGetController(XRNode.RightHand, rightHand, out var device, out var hand) &&
                !TryGetController(XRNode.LeftHand, leftHand, out device, out hand))
            {
                Suspend();
                return;
            }

            ConfigureDesktopInput();

            if (eventSystem != EventSystem.current || pointer == null)
            {
                CancelPointer();
                eventSystem = EventSystem.current;
                pointer = new PointerEventData(eventSystem)
                {
                    pointerId = -120,
                    button = PointerEventData.InputButton.Left,
                    useDragThreshold = true
                };
                requireRelease = true;
            }

            if (!activeDevice.Equals(device))
            {
                CancelPointer();
                activeDevice = device;
                requireRelease = true;
            }

            bool pressed = ReadTrigger(device);
            if (!pressed)
                requireRelease = false;

            Ray ray = new Ray(hand.position, hand.forward);
            Vector3 endpoint = ray.GetPoint(maximumDistance);
            var plane = new Plane(canvas.transform.forward, canvas.transform.position);
            bool hitsPlane = plane.Raycast(ray, out float distance) && distance <= maximumDistance;
            GameObject hitObject = null;
            RaycastResult hitResult = default;
            pointer.delta = Vector2.zero;

            if (hitsPlane)
            {
                endpoint = ray.GetPoint(distance);
                Vector3 screenPoint = viewCamera.WorldToScreenPoint(endpoint);
                if (screenPoint.z > viewCamera.nearClipPlane)
                {
                    Vector2 position = screenPoint;
                    pointer.delta = position - pointer.position;
                    pointer.position = position;
                    pointer.displayIndex = viewCamera.targetDisplay;
                    raycastResults.Clear();
                    raycaster.Raycast(pointer, raycastResults);
                    if (raycastResults.Count > 0)
                    {
                        hitResult = raycastResults[0];
                        hitObject = hitResult.gameObject;
                    }
                }
            }

            pointer.pointerCurrentRaycast = hitResult;
            UpdateHover(hitObject);
            if (pointer.pointerPress != null &&
                ExecuteEvents.GetEventHandler<IPointerClickHandler>(hitObject) != pointer.pointerPress)
                pointer.eligibleForClick = false;

            if (!requireRelease && pressed && !wasPressed)
                Press(hitObject);
            else if (pressed && wasPressed)
                Drag();
            else if (!pressed && wasPressed)
                Release(hitObject);

            if (hitObject != null && !pressed && device.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 axis))
            {
                if (Mathf.Abs(axis.y) > 0.2f)
                {
                    pointer.scrollDelta = Vector2.up * (axis.y * scrollSpeed * Time.unscaledDeltaTime);
                    ExecuteEvents.ExecuteHierarchy(hitObject, pointer, ExecuteEvents.scrollHandler);
                }
                else
                    pointer.scrollDelta = Vector2.zero;
            }

            wasPressed = pressed;
            if (canvas != null && canvas.isActiveAndEnabled && isActiveAndEnabled)
                ShowBeam(ray.origin, endpoint, hitObject != null, pressed);
            else
                Suspend();
        }

        private static bool TryGetController(XRNode node, Transform anchor, out InputDevice device, out Transform hand)
        {
            hand = anchor;
            device = InputDevices.GetDeviceAtXRNode(node);
            if (anchor == null || !anchor.gameObject.activeInHierarchy || !device.isValid)
                return false;
            return !device.TryGetFeatureValue(CommonUsages.isTracked, out bool tracked) || tracked;
        }

        private static bool ReadTrigger(InputDevice device)
        {
            if (device.TryGetFeatureValue(CommonUsages.triggerButton, out bool pressed))
                return pressed;
            return device.TryGetFeatureValue(CommonUsages.trigger, out float trigger) && trigger >= 0.65f;
        }

        private void ConfigureDesktopInput()
        {
            var module = EventSystem.current != null ? EventSystem.current.GetComponent<InputSystemUIInputModule>() : null;
            if (module != null && module == desktopInput && desktopActions != null && module.actionsAsset == desktopActions)
                return;
            RestoreDesktopInput();
            if (module == null || module.actionsAsset == null)
                return;

            desktopInput = module;
            originalActions = module.actionsAsset;
            desktopActions = Instantiate(originalActions);
            desktopActions.name = originalActions.name + " (Vocabulary desktop input)";
            desktopActions.hideFlags = HideFlags.DontSave;
            UnityEngine.InputSystem.InputActionRebindingExtensions.LoadBindingOverridesFromJson(desktopActions,
                UnityEngine.InputSystem.InputActionRebindingExtensions.SaveBindingOverridesAsJson(originalActions));

            // The scene's default input asset also binds XR triggers. Letting both
            // modules consume them can deselect text fields or deliver a second click
            // using the legacy module's screen pointer. Filter only this runtime copy.
            foreach (var map in desktopActions.actionMaps)
                foreach (var action in map.actions)
                    for (int index = 0; index < action.bindings.Count; index++)
                    {
                        var binding = action.bindings[index];
                        string path = binding.effectivePath ?? string.Empty;
                        string groups = ";" + binding.groups + ";";
                        if (groups.IndexOf(";XR;", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                            path.IndexOf("XRController", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                            path.IndexOf("TrackedDevice", System.StringComparison.OrdinalIgnoreCase) >= 0)
                            UnityEngine.InputSystem.InputActionRebindingExtensions.ApplyBindingOverride(action, index, string.Empty);
                    }
            module.actionsAsset = desktopActions;
        }

        private void RestoreDesktopInput()
        {
            if (desktopInput != null && desktopInput.actionsAsset == desktopActions)
                desktopInput.actionsAsset = originalActions;
            if (desktopActions != null)
                Destroy(desktopActions);
            desktopInput = null;
            originalActions = null;
            desktopActions = null;
        }

        private void Press(GameObject target)
        {
            pointer.eligibleForClick = target != null;
            pointer.pressPosition = pointer.position;
            pointer.pointerPressRaycast = pointer.pointerCurrentRaycast;
            pointer.clickCount = 1;
            pointer.clickTime = Time.unscaledTime;
            pointer.rawPointerPress = target;
            pointer.pointerPress = ExecuteEvents.ExecuteHierarchy(target, pointer, ExecuteEvents.pointerDownHandler);
            if (pointer.pointerPress == null)
                pointer.pointerPress = ExecuteEvents.GetEventHandler<IPointerClickHandler>(target);
            pointer.pointerDrag = ExecuteEvents.GetEventHandler<IDragHandler>(target);
            ExecuteEvents.Execute(pointer.pointerDrag, pointer, ExecuteEvents.initializePotentialDrag);
        }

        private void Drag()
        {
            if (pointer.pointerDrag == null || pointer.delta.sqrMagnitude < .001f) return;
            if (!pointer.dragging)
            {
                if ((pointer.position - pointer.pressPosition).sqrMagnitude <
                    EventSystem.current.pixelDragThreshold * EventSystem.current.pixelDragThreshold) return;
                ExecuteEvents.Execute(pointer.pointerDrag, pointer, ExecuteEvents.beginDragHandler);
                pointer.dragging = true;
                pointer.eligibleForClick = false;
            }
            ExecuteEvents.Execute(pointer.pointerDrag, pointer, ExecuteEvents.dragHandler);
        }

        private void Release(GameObject target)
        {
            GameObject pressedObject = pointer.pointerPress;
            bool click = pointer.eligibleForClick && pressedObject != null &&
                pressedObject == ExecuteEvents.GetEventHandler<IPointerClickHandler>(target);
            ExecuteEvents.Execute(pressedObject, pointer, ExecuteEvents.pointerUpHandler);
            if (pointer.dragging)
                ExecuteEvents.Execute(pointer.pointerDrag, pointer, ExecuteEvents.endDragHandler);
            // Clear before invoking callbacks: a click may hide or destroy this canvas.
            pointer.pointerPress = null;
            pointer.rawPointerPress = null;
            pointer.pointerDrag = null;
            pointer.dragging = false;
            pointer.eligibleForClick = false;
            if (click)
                ExecuteEvents.Execute(pressedObject, pointer, ExecuteEvents.pointerClickHandler);
        }

        private void UpdateHover(GameObject target)
        {
            if (pointer == null)
                return;
            nextHovered.Clear();
            for (Transform current = target != null ? target.transform : null; current != null; current = current.parent)
                nextHovered.Add(current);

            foreach (Transform previous in hovered)
                if (previous != null && !nextHovered.Contains(previous))
                    ExecuteEvents.Execute(previous.gameObject, pointer, ExecuteEvents.pointerExitHandler);

            foreach (Transform current in nextHovered)
                if (!hovered.Contains(current))
                    ExecuteEvents.Execute(current.gameObject, pointer, ExecuteEvents.pointerEnterHandler);

            hovered.Clear();
            hovered.AddRange(nextHovered);
            pointer.hovered.Clear();
            foreach (Transform current in hovered)
                if (current != null)
                    pointer.hovered.Add(current.gameObject);
            pointer.pointerEnter = target;
        }

        private void ShowBeam(Vector3 start, Vector3 end, bool hit, bool pressed)
        {
            if (beam == null)
            {
                var beamObject = new GameObject("VocabularyControllerRay");
                beamObject.transform.SetParent(transform, false);
                beam = beamObject.AddComponent<LineRenderer>();
                beam.useWorldSpace = true;
                beam.positionCount = 2;
                beam.startWidth = beamWidth;
                beam.endWidth = beamWidth * 0.55f;
                beam.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                beam.receiveShadows = false;
                // Reuse the uGUI shader already included for this canvas. A Shader.Find
                // of an otherwise unused URP shader can disappear in stripped builds.
                pointerMaterial = new Material(UnityEngine.UI.Graphic.defaultGraphicMaterial)
                {
                    name = "Vocabulary Pointer (Runtime)"
                };
                beam.sharedMaterial = pointerMaterial;

                tip = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                tip.name = "VocabularyPointerTip";
                tip.transform.SetParent(transform, false);
                tip.transform.localScale = Vector3.one * 0.012f;
                var collider = tip.GetComponent<Collider>();
                collider.enabled = false;
                Destroy(collider);
                var tipRenderer = tip.GetComponent<Renderer>();
                tipRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                tipRenderer.receiveShadows = false;
                if (pointerMaterial != null)
                    tipRenderer.sharedMaterial = pointerMaterial;
            }

            Color color = hit ? (pressed ? VocabularyView.Accent : VocabularyView.Highlight) :
                VocabularyView.Secondary;
            if (pointerMaterial != null)
            {
                pointerMaterial.color = color;
                if (pointerMaterial.HasProperty("_BaseColor"))
                    pointerMaterial.SetColor("_BaseColor", color);
            }
            beam.enabled = true;
            beam.startColor = color;
            beam.endColor = color;
            beam.SetPosition(0, start);
            beam.SetPosition(1, end);
            tip.SetActive(hit);
            tip.transform.position = end - (end - start).normalized * 0.004f;
        }

        private void Suspend()
        {
            CancelPointer();
            requireRelease = true;
            activeDevice = default;
            if (beam != null)
                beam.enabled = false;
            if (tip != null)
                tip.SetActive(false);
        }

        private void CancelPointer()
        {
            if (pointer != null)
            {
                ExecuteEvents.Execute(pointer.pointerPress, pointer, ExecuteEvents.pointerUpHandler);
                if (pointer.dragging)
                    ExecuteEvents.Execute(pointer.pointerDrag, pointer, ExecuteEvents.endDragHandler);
                pointer.pointerPress = null;
                pointer.rawPointerPress = null;
                pointer.pointerDrag = null;
                pointer.dragging = false;
                pointer.eligibleForClick = false;
                UpdateHover(null);
            }
            wasPressed = false;
        }

        private void OnDisable()
        {
            Suspend();
            RestoreDesktopInput();
        }

        private void OnDestroy()
        {
            RestoreDesktopInput();
            if (pointerMaterial != null)
                Destroy(pointerMaterial);
        }
    }
}
