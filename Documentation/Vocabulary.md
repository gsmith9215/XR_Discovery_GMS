# Vocabulary study and tests

Open `Assets/Scenes/SampleScene.unity` and press Play. The existing `RandomObjectSpawner` starts the vocabulary application and keeps its scene camera, hammer and spacing references. No additional scene setup is required.

## Player flow

- **Study:** select All items, a premade category, or a custom set. Previous/Next cycles through every object, with Japanese, kana reading and English shown together.
- **Test:** select a set and hit the matching box with the hammer. Each selected item is the target exactly once per round. The results offer a full retest or another round containing only missed targets. Continue until a round has no misses.
- **My custom sets:** create an empty set, copy a premade template, or duplicate a custom set. Name it, search/filter items, add/remove items and save. Open an existing set to rename/edit/delete it. Leaving the editor prompts before discarding a draft.
- Premade and custom sets occupy separate tabs. Names are labels, not IDs; matching names are allowed, including between two custom sets.
- Japanese test prompts are the default. The main menu can switch to English prompts. Study always shows both languages.
- One- and two-item sets work. Extra answer choices come from the library, but only selected targets are scored. Items sharing the same displayed Japanese word are not competing choices in the same question.

## Controls

In XR, point the right controller at a menu and press/release its trigger. The left controller is used if the right controller is unavailable. The hammer remains the test answer mechanism. Recenter relocates the menu and objects in front of the current head direction.

Selecting a text field opens a world-space keyboard with Latin, hiragana and katakana layouts, shift/small kana, voiced marks, backspace and space. Done returns to item selection. Physical keyboard/IME input works, and a System keyboard option is available when supported by the platform.

In the Editor, menus accept mouse clicks, Study accepts Left/Right arrow keys, and tests accept clicking a box or pressing 1/2/3 for the left/middle/right choice.

The spawner Inspector has separate display distances for tests and study. Tests currently place boxes 1 m forward and 0.25 m below eye level; study places one box 1.5 m forward and 0.1 m below eye level. These can be adjusted after checking reach and comfort in the headset.

## Content and saved data

- Vocabulary boxes remain under `Assets/Resources/VocabularyBoxes/<category>/`.
- `Assets/Resources/VocabularyData/Library.json` supplies English names, Japanese terms, readings, categories and resource paths. Prefab GUIDs are stable item identities, so renaming or moving a prefab does not disconnect a saved custom set.
- After adding, moving or renaming boxes, use **Tools > Vocabulary > Rebuild Library**. It also runs before builds. Existing Japanese terms/readings are retained by GUID. Review new entries in the JSON, then run **Rebuild Japanese Font** after changing translations; builds do this automatically.
- Known text is baked into a Noto Sans JP TMP atlas. A dynamic fallback supports custom names. The font and its redistribution license are in `Assets/Fonts/Vocabulary/`.
- Custom sets save on the device under `Application.persistentDataPath/Vocabulary/custom-sets.json`. The previous successful save is retained as `.bak`. A damaged save is preserved when recovery succeeds; unreadable or unsupported saves are not overwritten.
- Empty sets can be saved for later, but cannot start Study or Test until an available item is added. Missing asset IDs stay in saved sets and are skipped during play.

## Code and verification

`VocabularyApp` controls navigation and sessions. `VocabularyView` builds the uGUI canvas; `VocabularyPointerInput` and `VocabularyKeyboard` provide headset input. `RandomObjectSpawner` presents boxes and forwards one accepted hammer hit per question. `TestSession` and `CustomSetRepository` isolate round rules and persistent set operations.

Unity Test Runner includes the core EditMode suite and the PlayMode flow suite. PlayMode tests use an isolated camera/app and temporary save files, exercising the real UI callbacks, resource prefabs and collider selection entry point without altering player data. Headset comfort and controller aiming should also be checked on the target Quest hardware.
