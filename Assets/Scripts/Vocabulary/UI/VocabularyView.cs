using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace XRDiscovery.Vocabulary
{
    // A single world-space canvas, shared by mouse input in the Editor and controller rays on Quest.
    public sealed class VocabularyView : MonoBehaviour
    {
        public static readonly Color Ink = new(.91f, .95f, .97f);
        public static readonly Color Muted = new(.61f, .72f, .76f);
        public static readonly Color Accent = new(.13f, .52f, .47f);
        private TMP_FontAsset font;
        private RectTransform page;
        private UnityEngine.UI.Image background;
        private Transform cameraTransform;
        private RectTransform keyboardHost;
        public Canvas Canvas { get; private set; }
        public VocabularyKeyboard Keyboard { get; private set; }
        public RectTransform Page => page;
        // Reuse the menu's last placement pose when presenting study and test objects.
        public Vector3 PlacementOrigin { get; private set; }
        public Vector3 PlacementForward { get; private set; }

        public void Initialize(Transform cameraAnchor, Transform rightHand)
        {
            cameraTransform = cameraAnchor;
            font = Resources.Load<TMP_FontAsset>(VocabularyLibrary.FontResourcePath) ?? TMP_Settings.defaultFontAsset;
            var root = new GameObject("Vocabulary menu", typeof(RectTransform), typeof(Canvas),
                typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            root.transform.SetParent(transform, false);
            Canvas = root.GetComponent<Canvas>();
            Canvas.renderMode = RenderMode.WorldSpace;
            Canvas.worldCamera = cameraAnchor.GetComponent<Camera>();
            Canvas.sortingOrder = 20;
            var rect = (RectTransform)root.transform;
            rect.sizeDelta = new Vector2(1200, 900);
            rect.localScale = Vector3.one * .0013f;
            root.GetComponent<UnityEngine.UI.CanvasScaler>().dynamicPixelsPerUnit = 2;
            background = root.AddComponent<UnityEngine.UI.Image>();
            RoundedUiSprite.Apply(background, true);
            background.color = new Color(.035f, .065f, .085f, .98f);
            background.raycastTarget = false;
            page = Rect("Page", rect, Vector2.zero, Vector2.one);
            keyboardHost = Rect("Keyboard", rect, new Vector2(0, 0), new Vector2(1, 0));
            keyboardHost.pivot = new Vector2(.5f, 0);
            keyboardHost.sizeDelta = new Vector2(-40, 300);
            keyboardHost.anchoredPosition = new Vector2(0, 12);
            Keyboard = VocabularyKeyboard.Build(keyboardHost, font);
            Keyboard.Hide();

            var system = EventSystem.current;
            if (system == null)
            {
                var eventObject = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                eventObject.transform.SetParent(transform, false);
                eventObject.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            var left = FindAnchor(cameraAnchor.root, "LeftHandAnchor");
            var pointer = gameObject.AddComponent<VocabularyPointerInput>();
            pointer.Configure(Canvas, rightHand, left, Canvas.worldCamera);
            Recenter();
        }

        private static Transform FindAnchor(Transform root, string name)
        {
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == name) return child;
            return null;
        }

        public void Recenter()
        {
            var forward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < .001f) forward = Vector3.forward;
            PlacementOrigin = cameraTransform.position;
            PlacementForward = forward;
            Canvas.transform.SetPositionAndRotation(PlacementOrigin + forward * 1.7f - Vector3.up * .06f,
                Quaternion.LookRotation(forward, Vector3.up));
        }

        public void Clear(bool solidBackground = true)
        {
            Keyboard.Hide();
            for (int i = page.childCount - 1; i >= 0; i--)
            {
                var child = page.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }
            background.enabled = solidBackground;
        }

        // Positions are measured from the top left in the 1200 x 900 design canvas.
        public RectTransform Area(string name, float x, float y, float width, float height, Transform parent = null)
        {
            var rect = Rect(name, parent ?? page, new Vector2(0, 1), new Vector2(0, 1));
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }

        public TMP_Text Text(string name, string value, float x, float y, float width, float height,
            float size = 30, Color? color = null, Transform parent = null)
        {
            return Label(Area(name, x, y, width, height, parent), value, size, color ?? Ink);
        }

        public void Panel(string name, float x, float y, float width, float height)
        {
            var image = Area(name, x, y, width, height).gameObject.AddComponent<UnityEngine.UI.Image>();
            RoundedUiSprite.Apply(image, true);
            image.color = new Color(.035f, .065f, .085f, .96f);
            image.raycastTarget = false;
        }

        public UnityEngine.UI.ScrollRect ScrollArea(string name, float x, float y, float width, float height,
            out RectTransform content)
        {
            var viewport = Area(name + "Viewport", x, y, width, height);
            var image = viewport.gameObject.AddComponent<UnityEngine.UI.Image>();
            RoundedUiSprite.Apply(image, true);
            image.color = new Color(.025f, .05f, .065f, .72f);
            image.raycastTarget = true;
            viewport.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();

            content = Rect("Content", viewport, new Vector2(0, 1), new Vector2(1, 1));
            content.pivot = new Vector2(.5f, 1);
            content.sizeDelta = new Vector2(0, 0);
            content.anchoredPosition = Vector2.zero;

            var scroll = viewport.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40;

            var scrollbarRect = Rect("Scrollbar", viewport, new Vector2(1, 0), new Vector2(1, 1));
            scrollbarRect.pivot = new Vector2(1, .5f);
            scrollbarRect.sizeDelta = new Vector2(36, -64);
            scrollbarRect.anchoredPosition = new Vector2(-8, 0);
            var scrollbarImage = scrollbarRect.gameObject.AddComponent<UnityEngine.UI.Image>();
            RoundedUiSprite.ApplyCapsule(scrollbarImage);
            scrollbarImage.color = new Color(.11f, .19f, .22f, .9f);
            scrollbarImage.raycastTarget = false;

            var handleRect = Rect("Handle", scrollbarRect, Vector2.zero, Vector2.one);
            handleRect.offsetMin = new Vector2(2, 0);
            handleRect.offsetMax = new Vector2(-2, 0);
            var handleHitArea = handleRect.gameObject.AddComponent<UnityEngine.UI.Image>();
            handleHitArea.color = Color.clear;
            handleHitArea.raycastTarget = true;

            var thumbRect = Rect("Thumb", handleRect, new Vector2(0, 0), new Vector2(1, .7f));
            var thumbImage = thumbRect.gameObject.AddComponent<UnityEngine.UI.Image>();
            RoundedUiSprite.ApplyCapsule(thumbImage);
            thumbImage.color = new Color(.35f, .78f, .7f, .95f);
            thumbImage.raycastTarget = false;

            var scrollbar = scrollbarRect.gameObject.AddComponent<UnityEngine.UI.Scrollbar>();
            scrollbar.direction = UnityEngine.UI.Scrollbar.Direction.BottomToTop;
            scrollbar.targetGraphic = thumbImage;
            scrollbar.handleRect = handleRect;
            scrollbar.onValueChanged.AddListener(value =>
            {
                thumbRect.anchorMin = new Vector2(0, .3f * value);
                thumbRect.anchorMax = new Vector2(1, .7f + .3f * value);
            });
            scroll.verticalScrollbar = scrollbar;
            scroll.verticalScrollbarVisibility = UnityEngine.UI.ScrollRect.ScrollbarVisibility.AutoHide;
            return scroll;
        }

        public TMP_Text Label(RectTransform rect, string value, float size = 30, Color? color = null)
        {
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = size;
            text.color = color ?? Ink;
            text.text = value;
            text.richText = false;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            return text;
        }

        public UnityEngine.UI.Button Button(string name, string label, float x, float y, float width, float height,
            Action clicked, bool primary = false, bool enabled = true, Transform parent = null)
        {
            var rect = Area(name, x, y, width, height, parent);
            var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            RoundedUiSprite.Apply(image);
            image.color = primary ? Accent : new Color(.105f, .17f, .21f);
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.highlightedColor = new Color(1.25f, 1.25f, 1.25f);
            colors.pressedColor = new Color(.7f, .9f, .85f);
            colors.disabledColor = new Color(.4f, .4f, .4f, .65f);
            button.colors = colors;
            button.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.None };
            button.interactable = enabled;
            button.onClick.AddListener(() => clicked?.Invoke());
            var labelRect = Rect("Label", rect, Vector2.zero, Vector2.one);
            labelRect.offsetMin = new Vector2(18, 5);
            labelRect.offsetMax = new Vector2(-18, -5);
            var text = Label(labelRect, label, 28);
            text.alignment = TextAlignmentOptions.Midline;
            text.enableAutoSizing = true;
            text.fontSizeMin = 20;
            text.fontSizeMax = 28;
            return button;
        }

        public UnityEngine.UI.Button MenuButton(string name, string title, string description,
            float x, float y, float width, float height, Action clicked)
        {
            var button = Button(name, "", x, y, width, height, clicked, true);
            // Noto Sans JP needs more than 60 units of line height at this size.
            var titleText = Text("Title", title, 20, 20, width - 40, 80, 42, parent: button.transform);
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.textWrappingMode = TextWrappingModes.NoWrap;
            var descriptionText = Text("Description", description, 20, 120, width - 40, 75, 24,
                parent: button.transform);
            descriptionText.alignment = TextAlignmentOptions.Top;
            return button;
        }

        public TMP_InputField Input(string name, string value, string placeholder, float x, float y,
            float width, Action<string> changed)
        {
            var rect = Area(name, x, y, width, 60);
            var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            RoundedUiSprite.Apply(image);
            image.color = new Color(.08f, .13f, .16f);
            var field = rect.gameObject.AddComponent<TMP_InputField>();
            field.targetGraphic = image;
            var viewport = Rect("Text viewport", rect, Vector2.zero, Vector2.one);
            viewport.offsetMin = new Vector2(18, 8);
            viewport.offsetMax = new Vector2(-18, -8);
            viewport.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            var textRect = Rect("Value", viewport, Vector2.zero, Vector2.one);
            var text = (TextMeshProUGUI)Label(textRect, "", 29);
            text.textWrappingMode = TextWrappingModes.NoWrap;
            var placeholderText = Label(Rect("Placeholder", viewport, Vector2.zero, Vector2.one), placeholder, 27, Muted);
            field.textViewport = viewport;
            field.textComponent = text;
            field.placeholder = placeholderText;
            field.fontAsset = font;
            field.lineType = TMP_InputField.LineType.SingleLine;
            field.characterLimit = 80;
            field.richText = false;
            field.text = value;
            field.onValueChanged.AddListener(s => changed?.Invoke(s));
            Keyboard.Bind(field);
            return field;
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }
    }
}
