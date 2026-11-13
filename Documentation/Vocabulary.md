# Vocabulary study and tests

Open `Assets/Scenes/SampleScene.unity` and press Play. The scene's `VocabularyApp` component starts the application and references its object presenter and hammer. `VocabularyObjectPresenter` retains the camera, controller anchor and spacing settings. No additional scene setup is required.

## Player flow

- **Study:** select All items, a premade set, or a custom set. Previous/Next cycles through every object, with Japanese, kana reading and English shown together. Premade sets follow their authored item order.
- **Test:** select a set and hit the matching box with the hammer. Each selected item is the target exactly once per round. The results offer a full retest or another round containing only missed targets. Continue until a round has no misses.
- **My custom sets:** create an empty set, copy a premade template, or duplicate a custom set. Name it, search/filter items, add/remove items and save. Open an existing set to rename/edit/delete it. Leaving the editor prompts before discarding a draft.
- Premade and custom sets occupy separate tabs. Names are labels, not IDs; matching names are allowed, including between two custom sets.
- Japanese test prompts are the default. The main menu can switch to English prompts. Study always shows both languages.
- One- and two-item sets work. Extra answer choices come from the library, but only selected targets are scored. Items sharing the same displayed Japanese word are not competing choices in the same question.

## Controls

In XR, point the right controller at a menu and press/release its trigger. The left controller is used if the right controller is unavailable. The hammer remains the test answer mechanism. The menu is positioned at startup, then stays in place when navigating, changing settings, or starting Study or Test. Recenter relocates the menu and objects in front of the current head direction.

Selecting a text field opens a world-space keyboard with Latin, hiragana and katakana layouts, shift/small kana, voiced marks, backspace and space. Done returns to item selection. Physical keyboard/IME input works, and a System keyboard option is available when supported by the platform.

In the Editor, menus accept mouse clicks, Study accepts Left/Right arrow keys, and tests accept clicking a box or pressing 1/2/3 for the left/middle/right choice.

The object presenter Inspector has separate display distances for tests and study. Tests currently place boxes 1 m forward and 0.25 m below eye level; study places one box 1.5 m forward and 0.1 m below eye level. These can be adjusted after checking reach and comfort in the headset.

## Content and saved data

- Vocabulary boxes remain under `Assets/Resources/VocabularyBoxes/<category>/`.
- `Assets/Resources/VocabularyData/Library.json` supplies English names, Japanese terms, readings, categories and resource paths. Prefab GUIDs are stable item identities, so renaming or moving a prefab does not disconnect a saved custom set.
- `Assets/Resources/VocabularyData/PremadeSets/` contains one JSON definition per curated premade set. All 14 former category sets were migrated with their original names, IDs, membership and order (182 total item memberships). Future changes to category folders do not rewrite these definitions.
- After adding, moving or renaming boxes, use **Tools > Vocabulary > Rebuild Library**. It also runs before builds. Existing Japanese terms/readings are retained by GUID. Review new entries in the JSON, then run **Rebuild Japanese Font** after changing translations; builds do this automatically.
- Known text is baked into a Noto Sans JP TMP atlas. A dynamic fallback supports custom names. The font and its redistribution license are in `Assets/Fonts/Vocabulary/`.
- Custom sets save on the device under `Application.persistentDataPath/Vocabulary/custom-sets.json`. The previous successful save is retained as `.bak`. A damaged save is preserved when recovery succeeds; unreadable or unsupported saves are not overwritten.
- Empty sets can be saved for later, but cannot start Study or Test until an available item is added. Missing asset IDs stay in saved sets and are skipped during play.

## Authoring premade sets

1. Open **Tools > Vocabulary > Premade Set Editor**.
2. Select a set to edit, or choose **New set** or **Duplicate**. Each new set receives its own stable `premade:` ID. Editing the name does not change the ID.
3. Enter its name, optional description, and menu order (lower numbers appear first).
4. Search the catalog by English, Japanese or reading, and optionally filter by category. Select individual items or use **Add matching**. Items from different categories can belong to the same set, and an item can belong to several sets.
5. Use the arrows in the selected list to change the Study order. Tests still shuffle targets.
6. Select **Save**, then **Validate all**. The editor writes directly to the JSON definition; new files use a unique filename. Filenames can be renamed in Unity without changing set identity.

Saving validates the full definition collection before replacing a file. Premade sets need at least one item; invalid versions, duplicate set IDs, repeated members and references to unavailable items are rejected. Builds rebuild the item catalog, validate the premade definitions and prefab references, then rebuild the font including premade names and descriptions. Manually changed Japanese set text can be baked immediately with **Tools > Vocabulary > Rebuild Japanese Font**.

The JSON fields are `version` (currently 1), `id`, `name`, `description`, `sortOrder`, and the ordered `itemIds` array. Item IDs reference the catalog's prefab GUIDs, never names or paths. Preserve prefab `.meta` files when moving assets. The editor displays names so authors do not need to copy GUIDs manually.

**All items** remains a dynamic catalog-wide option with the reserved ID `premade:all`; it has no JSON definition. Categories remain useful catalog filters but do not define premade membership. Rebuilding the catalog never regenerates premade sets. Deleting a prefab requires repairing any premade definitions that refer to it before building.

Premade definitions ship read-only with the application. Copying one into a player's custom sets creates independent membership and a new `custom:` ID. Later changes to the shipped definition do not alter that copy. Custom-save paths, schema, recovery and backup behavior are unchanged.

## Code and verification

Runtime code is organized under `Assets/Scripts/Vocabulary/`:

| Folder | Responsibility |
| --- | --- |
| `Application` | `VocabularyApp` initializes dependencies and coordinates navigation. Separate controllers own browsing, custom-set editing, study and test flows. |
| `Core` | `VocabularySet`, `StudySession` and `TestSession` implement data and progression rules. This assembly has no Unity engine references. |
| `Data` | `VocabularyLibrary` loads item metadata; `PremadeSetCatalog` loads and validates curated definitions; `CustomSetRepository` handles player saves; `VocabularyPrefabProvider` loads prefabs. |
| `UI` and `UI/Screens` | `VocabularyView` builds the shared canvas and controls. Screen classes draw layouts and invoke callbacks supplied by controllers. |
| `Presentation` | `VocabularyObjectPresenter` places objects and forwards one accepted selection per question. It does not start the app. |
| `Input` | Controller pointer, world keyboard, desktop controls and hammer collision forwarding. |

`Assets/Editor/PremadeSetEditorWindow.cs` provides the authoring UI; `PremadeSetAuthoring.cs` handles JSON saving and validation; `VocabularyLibraryBuilder.cs` rebuilds catalog/font assets and runs build validation.

Every page transition cancels test feedback, stops answer input, clears presented objects and closes the keyboard before drawing the next screen. Menu pose remains fixed until explicit Recenter, apart from the startup tracking correction. Explicit `VocabularyApp.Initialize(presenter, savePath)` remains available for isolated tests; `Start()` does not initialize it a second time.

Unity Test Runner includes EditMode tests for core rules, storage, premade validation and scene wiring, plus the PlayMode flow suite. PlayMode tests use an isolated camera/app and temporary save files, exercising the real UI callbacks, every premade set's Study order, resource prefabs and collider selection entry point without altering player data. Headset comfort and controller aiming should also be checked on the target Quest hardware.
