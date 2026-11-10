using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace XRDiscovery.Vocabulary
{
    /// <summary>An in-world keyboard for standalone XR, with normal TMP/IME input retained.</summary>
    public sealed class VocabularyKeyboard : MonoBehaviour
    {
        private enum KeyboardMode { Latin, Hiragana, Katakana }

        private sealed class Binding
        {
            public TMP_InputField Field;
            public UnityAction<string> Select;
        }

        private sealed class CharacterKey
        {
            public string Value;
            public TMP_Text Label;
            public bool Kana;
        }

        private readonly List<Binding> bindings = new List<Binding>();
        private readonly List<CharacterKey> characterKeys = new List<CharacterKey>();
        private TMP_InputField target;
        private RectTransform latinRows;
        private RectTransform kanaRows;
        private TMP_Text shiftLabel;
        private UnityEngine.UI.Button dakutenButton;
        private UnityEngine.UI.Button handakutenButton;
        private KeyboardMode mode;
        private bool shifted;
        private bool built;
        private TMP_FontAsset font;

        public bool IsVisible => built && gameObject.activeInHierarchy;
        public TMP_InputField Target => target;
        public event Action<bool> VisibilityChanged;

        /// <summary>Build once in a parent roughly 300 UI units high; the keyboard fills it.</summary>
        public static VocabularyKeyboard Build(RectTransform parent, TMP_FontAsset font = null)
        {
            if (parent == null)
                throw new ArgumentNullException(nameof(parent));
            var root = new GameObject("VocabularyKeyboard", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            var rect = (RectTransform)root.transform;
            rect.SetParent(parent, false);
            Stretch(rect);
            var keyboard = root.AddComponent<VocabularyKeyboard>();
            keyboard.font = font != null ? font : TMP_Settings.defaultFontAsset;
            keyboard.BuildContents();
            keyboard.built = true;
            root.SetActive(false);
            return keyboard;
        }

        public void Bind(TMP_InputField field)
        {
            if (field == null)
                throw new ArgumentNullException(nameof(field));
            bindings.RemoveAll(binding => binding.Field == null);
            if (bindings.Exists(binding => binding.Field == field))
                return;
            field.richText = false;
            field.shouldHideSoftKeyboard = true;
            field.shouldHideMobileInput = true;
            field.resetOnDeActivation = false;
            field.onFocusSelectAll = false;
            UnityAction<string> callback = ignored => Show(field);
            field.onSelect.AddListener(callback);
            bindings.Add(new Binding { Field = field, Select = callback });
        }

        public void Show(TMP_InputField field)
        {
            if (field == null || !field.isActiveAndEnabled || !field.interactable || field.readOnly)
                return;
            bool wasVisible = IsVisible;
            target = field;
            target.shouldHideSoftKeyboard = true;
            if (!gameObject.activeSelf)
                gameObject.SetActive(true);
            if (!wasVisible)
                VisibilityChanged?.Invoke(true);
        }

        public void Hide()
        {
            bool wasVisible = IsVisible;
            TMP_InputField previous = target;
            target = null;
            if (previous != null)
            {
                previous.shouldHideSoftKeyboard = true;
                previous.DeactivateInputField();
                if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == previous.gameObject)
                    EventSystem.current.SetSelectedGameObject(null);
            }
            gameObject.SetActive(false);
            if (wasVisible)
                VisibilityChanged?.Invoke(false);
        }

        private void Update()
        {
            if (target == null || !target.isActiveAndEnabled)
                Hide();
        }

        private void BuildContents()
        {
            var background = GetComponent<UnityEngine.UI.Image>();
            RoundedUiSprite.Apply(background, true);
            background.color = new Color(0.055f, 0.085f, 0.12f, 0.98f);
            background.raycastTarget = false;
            var layout = gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.spacing = 4f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            RectTransform header = Row("Modes", transform, 36f);
            Button(header, "ABC", () => SetMode(KeyboardMode.Latin));
            Button(header, "ひらがな", () => SetMode(KeyboardMode.Hiragana));
            Button(header, "カタカナ", () => SetMode(KeyboardMode.Katakana));
            shiftLabel = Button(header, "Shift", Shift).GetComponentInChildren<TMP_Text>();
            dakutenButton = Button(header, "゛", () => ModifyKana(false));
            handakutenButton = Button(header, "゜", () => ModifyKana(true));
            var native = Button(header, "System", OpenSystemKeyboard);
            native.interactable = TouchScreenKeyboard.isSupported;

            RectTransform keyArea = Rect("Characters", transform);
            var keyAreaSize = keyArea.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            keyAreaSize.flexibleHeight = 1f;
            keyAreaSize.minHeight = 150f;
            latinRows = CharacterRows(keyArea, "Latin", new[] { "1234567890", "qwertyuiop", "asdfghjkl", "zxcvbnm-_'" }, false);
            kanaRows = CharacterRows(keyArea, "Kana", new[] { "あいうえおかきくけこ", "さしすせそたちつてと", "なにぬねのはひふへほ", "まみむめもやゆよわを", "らりるれろんー、。？" }, true);

            RectTransform footer = Row("Editing", transform, 44f);
            Button(footer, "Backspace", Backspace);
            Button(footer, "Space", () => Insert(" "));
            Button(footer, "Clear", Clear);
            Button(footer, "Done", Hide, true);
            SetMode(KeyboardMode.Latin);
        }

        private RectTransform CharacterRows(RectTransform parent, string name, string[] rows, bool kana)
        {
            RectTransform root = Rect(name, parent);
            Stretch(root);
            var layout = root.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            layout.spacing = 4f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            for (int rowIndex = 0; rowIndex < rows.Length; rowIndex++)
            {
                RectTransform row = Row("Row" + rowIndex, root, -1f);
                foreach (char character in rows[rowIndex])
                {
                    var key = new CharacterKey { Value = character.ToString(), Kana = kana };
                    var button = Button(row, key.Value, () => Insert(DisplayCharacter(key)));
                    key.Label = button.GetComponentInChildren<TMP_Text>();
                    characterKeys.Add(key);
                }
            }
            return root;
        }

        private void SetMode(KeyboardMode value)
        {
            mode = value;
            latinRows.gameObject.SetActive(mode == KeyboardMode.Latin);
            kanaRows.gameObject.SetActive(mode != KeyboardMode.Latin);
            shiftLabel.text = mode == KeyboardMode.Latin ? (shifted ? "SHIFT" : "Shift") : "小 / 大";
            dakutenButton.interactable = mode != KeyboardMode.Latin;
            handakutenButton.interactable = mode != KeyboardMode.Latin;
            foreach (CharacterKey key in characterKeys)
                key.Label.text = DisplayCharacter(key);
        }

        private string DisplayCharacter(CharacterKey key)
        {
            if (!key.Kana)
                return shifted ? key.Value.ToUpperInvariant() : key.Value;
            char value = key.Value[0];
            return mode == KeyboardMode.Katakana && value >= 'ぁ' && value <= 'ゖ'
                ? ((char)(value + 0x60)).ToString() : key.Value;
        }

        private void Shift()
        {
            if (mode == KeyboardMode.Latin)
            {
                shifted = !shifted;
                SetMode(mode);
                return;
            }
            ModifyPreviousCharacter(character =>
            {
                const string large = "あいうえおつやゆよわかけアイウエオツヤユヨワカケ";
                const string small = "ぁぃぅぇぉっゃゅょゎゕゖァィゥェォッャュョヮヵヶ";
                int index = large.IndexOf(character);
                if (index >= 0)
                    return small[index];
                index = small.IndexOf(character);
                return index >= 0 ? large[index] : character;
            });
        }

        private void ModifyKana(bool handakuten)
        {
            ModifyPreviousCharacter(character =>
            {
                const string plain = "かきくけこさしすせそたちつてとはひふへほうカキクケコサシスセソタチツテトハヒフヘホウ";
                const string voiced = "がぎぐげござじずぜぞだぢづでどばびぶべぼゔガギグゲゴザジズゼゾダヂヅデドバビブベボヴ";
                const string hPlain = "はひふへほハヒフヘホ";
                const string hVoiced = "ばびぶべぼバビブベボ";
                const string hSemi = "ぱぴぷぺぽパピプペポ";
                if (handakuten)
                {
                    int at = hPlain.IndexOf(character);
                    if (at < 0)
                        at = hVoiced.IndexOf(character);
                    if (at >= 0)
                        return hSemi[at];
                    at = hSemi.IndexOf(character);
                    return at >= 0 ? hPlain[at] : character;
                }
                int index = plain.IndexOf(character);
                if (index >= 0)
                    return voiced[index];
                index = voiced.IndexOf(character);
                if (index >= 0)
                    return plain[index];
                index = hSemi.IndexOf(character);
                return index >= 0 ? hVoiced[index] : character;
            });
        }

        private void ModifyPreviousCharacter(Func<char, char> transformCharacter)
        {
            if (!CanEdit())
                return;
            int position = Mathf.Clamp(target.selectionStringFocusPosition, 0, target.text.Length);
            if (position == 0)
                return;
            char previous = target.text[position - 1];
            char replacement = transformCharacter(previous);
            if (replacement != previous)
            {
                target.text = target.text.Substring(0, position - 1) + replacement + target.text.Substring(position);
                RestoreFocus(position);
            }
        }

        public void Insert(string value)
        {
            if (!CanEdit() || string.IsNullOrEmpty(value))
                return;
            string text = target.text;
            int start = Mathf.Clamp(Math.Min(target.selectionStringAnchorPosition, target.selectionStringFocusPosition), 0, text.Length);
            int end = Mathf.Clamp(Math.Max(target.selectionStringAnchorPosition, target.selectionStringFocusPosition), start, text.Length);
            int allowed = target.characterLimit > 0 ? target.characterLimit - (text.Length - end + start) : int.MaxValue;
            if (value.Length > allowed)
                return;
            target.text = text.Substring(0, start) + value + text.Substring(end);
            RestoreFocus(start + value.Length);
        }

        public void Backspace()
        {
            if (!CanEdit())
                return;
            string text = target.text;
            int start = Mathf.Clamp(Math.Min(target.selectionStringAnchorPosition, target.selectionStringFocusPosition), 0, text.Length);
            int end = Mathf.Clamp(Math.Max(target.selectionStringAnchorPosition, target.selectionStringFocusPosition), start, text.Length);
            if (start == end && start > 0)
            {
                // Remove a text element, preserving surrogate pairs and combining kana marks.
                int previous = 0;
                foreach (int index in StringInfo.ParseCombiningCharacters(text))
                {
                    if (index >= start)
                        break;
                    previous = index;
                }
                start = previous;
            }
            if (end > start)
                target.text = text.Remove(start, end - start);
            RestoreFocus(start);
        }

        public void Clear()
        {
            if (!CanEdit())
                return;
            target.text = string.Empty;
            RestoreFocus(0);
        }

        private bool CanEdit() => target != null && target.isActiveAndEnabled && target.interactable && !target.readOnly;

        private void RestoreFocus(int position)
        {
            if (!CanEdit())
                return;
            target.stringPosition = Mathf.Clamp(position, 0, target.text.Length);
            target.ForceLabelUpdate();
            // Keyboard buttons use Navigation.None, so controller clicks preserve the
            // field's selection. Desktop clicks may deselect it; restore that focus.
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != target.gameObject)
                EventSystem.current.SetSelectedGameObject(target.gameObject);
            target.ActivateInputField();
        }

        private void OpenSystemKeyboard()
        {
            if (!CanEdit() || !TouchScreenKeyboard.isSupported)
                return;
            TMP_InputField field = target;
            field.DeactivateInputField();
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != field.gameObject)
                EventSystem.current.SetSelectedGameObject(field.gameObject);
            field.shouldHideSoftKeyboard = false;
            // Keep the in-world keyboard available when a platform keyboard is absent.
            field.ActivateInputField();
        }

        private UnityEngine.UI.Button Button(Transform parent, string label, UnityAction callback, bool accent = false)
        {
            RectTransform rect = Rect(label + "Key", parent);
            var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            RoundedUiSprite.Apply(image);
            image.color = accent ? new Color(0.12f, 0.46f, 0.40f) : new Color(0.14f, 0.20f, 0.27f);
            image.raycastTarget = true;
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            button.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.None };
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.68f, 1f, 0.90f);
            colors.pressedColor = new Color(0.42f, 0.75f, 0.66f);
            colors.disabledColor = new Color(0.42f, 0.47f, 0.50f, 0.5f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            button.onClick.AddListener(callback);
            var size = rect.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            size.flexibleWidth = 1f;
            size.minWidth = 24f;
            RectTransform labelRect = Rect("Label", rect);
            Stretch(labelRect);
            labelRect.offsetMin = new Vector2(3f, 0f);
            labelRect.offsetMax = new Vector2(-3f, 0f);
            var text = labelRect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.text = label;
            text.fontSize = 23f;
            text.enableAutoSizing = true;
            text.fontSizeMin = 15f;
            text.fontSizeMax = 23f;
            text.alignment = TextAlignmentOptions.Center;
            text.color = new Color(0.94f, 0.97f, 1f);
            text.raycastTarget = false;
            text.richText = false;
            return button;
        }

        private static RectTransform Row(string name, Transform parent, float height)
        {
            RectTransform rect = Rect(name, parent);
            var layout = rect.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            layout.spacing = 4f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            var size = rect.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            if (height >= 0f)
            {
                size.minHeight = height;
                size.preferredHeight = height;
                size.flexibleHeight = 0f;
            }
            else
                size.flexibleHeight = 1f;
            return rect;
        }

        private static RectTransform Rect(string name, Transform parent)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            return rect;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private void OnDestroy()
        {
            foreach (Binding binding in bindings)
                if (binding.Field != null)
                    binding.Field.onSelect.RemoveListener(binding.Select);
        }
    }
}
