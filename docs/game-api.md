# Before the Storm - Game API Reference

Verified findings from `decompiled\` (ilspycmd output of Assembly-CSharp.dll).
Class names are obfuscated (`T_XXXXXXXX`); methods, properties, and serialized
fields are readable. Record every newly confirmed mapping here AND as an
accessor/alias in `GameRefs.cs`.

## Build facts

- Unity 5.5.3p1, 64-bit, legacy Mono. Mod target: net35.
- UI framework: renamed NGUI (atlas sprites, UILabel/UICamera clones, BBCode-style markup).
- Dialogue engine: Deck Nine "StoryTeller" (`StreamingAssets\Data\StoryTellerData.pbl`).
- Audio: CriWare (subtitle timing keys off voice clip playback).
- Localization: NGUI Localization clone `T_A7F32A9D`, `Get(key)` / `Get(key, sheet)`.
- Text assets: `StreamingAssets\Localization\`.

## Core singletons

- `T_A6E913D1` GameManager — static `Instance`. Fields: `m_inputManager`,
  `m_gameModeManager`, `m_gameStateManager`, `m_backtalkManager`, `m_tagManager`,
  `m_levelManager`, `m_saveManager`, `m_gameDataManager`, `m_dawnUI`,
  `m_followCamera`, `m_zenJukebox`, `m_achievementManager`, more. Also `Paused`.
- `T_E7B3064D` WindowManager — static `Singleton`. `GetWindow<T>(string name)`
  where `T : T_D25B278`; also `AddWindow`, `OpenWindow`, `CloseWindow`, `MainWindow`.
- `T_408CFC35` DawnUI — global UI/flow controller. Statics: `s_inHotspotRange`,
  `s_currentControlState`, `CurrentScheme`, `CanAnimate(eGameInput)`,
  `GetSpriteName(KeyCode)`.

## Windows (name → class)

All windows derive from `T_D25B278` (open = `gameObject.activeSelf`; `Open()`,
`_Close()`, `CloseWindow()`, `AcceptingInput`). `T_A67E7A0` is the overlay-type base.

- SubtitleWindow → `T_2F0DA2D9`
- DialogWindow → `T_A35DE5E1`
- FreeRoamWindow → `T_F8FE3E1C`
- MainMenuWindow → `T_6BD00C7B`
- PauseWindow → `T_E7AEA2C5`
- SettingsWindow and DarkSettingsWindow → `T_CCEFA2EA`
- JournalWindow → `T_7F8868E`
- BurnerSMSWindow → `T_3FE8D8BE`
- ChoicesWindow (end-of-episode recap) → `T_35562F97`
- ConsequenceWindow → `T_B6500484`
- TutorialWindow → `T_64B68373`
- BTTutorialWindow → `T_7A847831`
- ModalPopup → `T_B64E1BAE`
- SceneSelectionWindow → `T_63A31627`
- EndOfEpisodeWindow → `T_C3DBAF42`
- ExtrasWindow → `T_94254EC4`
- CreditsWindow → `T_6025074F`
- MovieWindow → `T_27B78397`
- UserInformWindow → `T_859A152B`
- HowToPlayWindow → `T_CF3FA5DC`
- PosterWindow → `T_2CD30DA2`
- TreasureMapWindow → `T_2FD302B0`
- ZenJukeboxWindow → `T_2433FC36`
- PlaybackWindow → `T_17C5D012`
- InventoryWindow → `T_1F3D03DB`
- CustomizationWindow → `T_9806DB88`
- ImageOverlayWindow → `T_94BAC810`
- TransitionWindow → `T_9A8C5124`
- STInfoWindow → `T_E745DA90`

## Subtitles

- `T_2F0DA2D9` SubtitleWindow: `m_speech` (UILabel) holds the currently displayed
  chunk; internal list of chunks advances on a timer synced to the voice clip.
  `SetupSubtitle(T_E8819104.T_41023B3E line, uint voiceId)` enqueues a line.
  `ForceOverrideSubtitle` (string property) displays arbitrary text.
  Statics: `ShowSubtitles`, `SubtitleSize`, `ShowSubtitleOverlay`.
- `T_E8819104` appears to be the StoryTeller audio/line hub (CriPlayParams nested
  type, `T_41023B3E` = dialogue line with `GetVoice(uint)`).

## Dialogue choices and backtalk

- `T_A35DE5E1` DialogWindow: `m_choiceUI` (normal), `m_btChoiceUI` (backtalk),
  both `T_6876113C`. `SetupChoices(T_74A043F9)`, `SetupBacktalk()`,
  `ChoiceMade(eInteractMenu)`, `UpdatePoints(int)`, `BacktalkEnded(bool)`.
  Backtalk state via `GameManager.m_backtalkManager` (`T_6D7C1D07`): `InBacktalk`,
  `BacktalkRunning`.
- `T_6876113C` ChoiceUI: `m_choices` = `T_B611E532[4]` indexed by `eInteractMenu`
  (0 up, 1 right, 2 down, 3 left). Callbacks `m_choiceMadeCallback`,
  `m_choiceDoneCallback`. `CheckChoices()`, `ButtonPressed(eInteractMenu)`.
- `T_B611E532` ChoiceButton (base `T_9E2FDFCF`): `Setup(text, textKey, textSheet,
  bool[, bool underline])` — good Harmony hook. Base exposes `m_label` (UILabel),
  `m_button` (eGameInput), `Selected`, `Active`.

## Free roam and hotspots

- `T_F8FE3E1C` FreeRoamWindow: static `List<T_6FD30C1C> s_currentTriggers`
  (hotspots currently in range/candidates), `m_interactUI` (`T_572A4969`,
  `SetHotspot(...)`), `m_nameUI` (`T_8F74F848`), SMS/graffiti notification labels,
  `ResetHotspots()`.
- `T_6FD30C1C` ST_Hotspot (MonoBehaviour + Collider): `m_friendlyName`,
  `m_nameKey`, `m_localizationSheet`, `m_activeVerbs` (`List<T_6160F3EF>`),
  `m_nonInteractVerbs`, `m_pointAt`, `m_lookAt`, `m_interactDistanceOverride`,
  `m_hotspotObj` (`T_244D769F`: `Enabled`, `tagReference`), angle-interact fields.
  Friendly name comes from sibling component `T_E6DAC959` (`m_friendlyName`).
  Layer: "WorldInteraction".
- eInteractVerb: kLook, kBacktalk, kSpeak, kKick, kOverlook, kCustomization, kNone.

## Burner SMS (phone texts)

- SMS threads appear in TWO windows sharing the same data + row shapes:
  `T_6BD5080F` (JournalWindow's SMS tab — the normal review path, a `T_87122939`
  journal tab) and `T_3FE8D8BE` BurnerSMSWindow (standalone phone; opens directly
  only for special/Damon texts via `static s_newSMS`). Both have `AddMessage`/
  `SetupPage` with the same InstantTraversal-then-Seen=true ordering. `T_87122939`
  (journal tab base): `m_selection` (contact list `T_51AF6A60`), `m_specific`
  (thread container), virtual `SetupPage()`. `T_6BD5080F` also: static
  `s_smsByContact` (Dictionary<contact, Dictionary<nodeName, T_BFD9C065>>),
  `SetSMSContact(name)` (marks ShouldShow, sets s_newSMS for Damon texts),
  message rows named `TextMessage##`.
- `T_3FE8D8BE` BurnerSMSWindow: `AddMessage(T_200B34A4)` adds one message row while
  a thread builds; `SetupPage()` builds the whole thread (called by `SetupSpecific()`
  when a contact is chosen, and directly from `OnOpen()` when `static s_newSMS`
  (`T_BFD9C065`) opens the phone to a new text). `SetupSelection()` builds the contact
  list (menu `m_contactMenu` = `T_51AF6A60`, unread count per contact). Key ordering:
  SetupPage calls each node's `InstantTraversal()` then sets `node.Seen = true`, so
  the pre-open unread state is only visible DURING AddMessage. `ResetWindow()` tears
  down; `m_message` = row prefab (`T_AD5CB3F7`), `m_messageTable`/`m_messageScrollView`.
- `T_200B34A4` SMSMessage (StoryTeller node): `sender`, `content`, `SMSParent`
  (`T_BFD9C065`, cached ancestor). `Trigger()` routes to BurnerSMSWindow.AddMessage
  (or the JournalWindow SMS tab `T_6BD5080F` when the journal is open).
- `T_BFD9C065` SMSNode : `T_BA92A050`: `Seen` / `ShouldShow` (both save-backed flags
  `Seen_SMS`+nodeName / `Show_SMS`+nodeName via GameStateModel), `specialSMS`
  (`contact` contains "damon"), `unlockedAtStart` (Seen/ShouldShow forced true),
  `contact`. Base `T_BA92A050` defines the `Seen`/`ShouldShow` save-flag getters.
- `T_AD5CB3F7` SMSMessageRow: `sender`, `content` via `m_messageLabel`, `m_contactName`,
  `m_timeStamp`; `Setup(msg, index, animateIn=!SMSParent.Seen && first)` — calls
  `ShowFullMessage()` once at build. `ShowFullMessage()` writes the row's real content
  into `m_messageLabel` (called AGAIN by `CR_ActiveMessages` when a queued message
  reveals). LIVE-REVEAL ORDERING: both windows' `CR_ActiveMessages` coroutine builds
  new-conversation rows HIDDEN (`gameObject.SetActive(_1F2CFCB7.Count == 0)` in
  AddMessage — only index 0 stays active), then activates + reveals them one at a time
  (typing dots `.`/`..`/`...`, then `ShowFullMessage()`). So a row's
  `gameObject.activeInHierarchy` at Setup discriminates static history (active) from a
  queued live message (inactive); a later `ShowFullMessage` while active is the reveal.
- `T_EF99895D` SMSContactRow (`T_C714B4A0`): one contact button on the phone/journal
  contact list. `m_name`/`m_highlightedName` (contact name label), `m_newMessage`/
  `m_highlightedNewMessage` (unread-count label "N new messages", localized), `m_button`
  (`T_9E2FDFCF`, the selectable added to the menu `T_51AF6A60`), `m_index`. The name +
  unread text live HERE, not under `m_button` — FocusHandler resolves the contact via
  `GetComponentInParent<T_EF99895D>()` from the selected button.
- `T_32AFC3D1` JournalPageFiller: `m_labels` (`T_A243E23D[]`, authored in NARRATIVE
  reading order), `m_bioPage`, `Data` (`T_BA92A050`). `TextTriggered(text, key)` fills
  `m_labels[i++]` sequentially — so the array index is the intended reading order, which
  choice-dependent rows can make DIVERGE from screen layout. WindowWatcher orders page
  labels by their `m_labels` index (grouped per page by the page root y), not raw y/x.

## Player and navigation

- `T_C3DD66D9` PlayerController (MonoBehaviour): `m_navAgent` (UnityEngine.AI
  NavMeshAgent — REAL NAVMESH EXISTS), `m_animation`, `m_actor` (`T_FD77464D`),
  static `s_mainCharacterMesh` (GameObject), `s_forceRelocateNextEnable`.
- `T_884A92DB` FollowCamera: `m_isInteractionBlocked`, `Halt()`.
- `T_6B664603` GameModeManager: `CurrentMode` (eGameMode; kFreeRoam etc.), `blockInput`.

## Input

- `T_6FCAE66C` InputManager: `GetInputState(eGameInput[, ...])` → eInputState
  (kNone/kDown/kHeld/kUp), `GetKeyBinding(eGameInput)` → `T_9005A419` (`m_key` =
  `List<KeyCode>`).
- `eGameInput`: full readable enum (kInteract, kOpenJournal, kOpenPause,
  kActionUp/Right/Down/Left, kMenuNavigate*, kMenuSelect, kMenuBack, kSkipCutscene,
  kCheckObjective, kOpenBurner, ...). Debug keys exist: kDebugDisplay,
  kSTDebugDisplay, kToggleFPSDisplay — avoid their bindings for mod keys.
- Control scheme: `T_D90B5C2A.ControlScheme` (Mouse/Controller), change event
  `T_D90B5C2A.onSchemeChange`.

## Menu focus (the game's own system — NOT NGUI hover)

- `T_51AF6A60` MenuButtonList (MonoBehaviour): THE focus source for all menus.
  `SelectedButtonIndex` (int, -1 when none), `SelectedButton` (T_9E2FDFCF, setter
  drives Highlight + hover sound), `SetCurrentToFirst()`, `m_buttons`
  (List<T_9E2FDFCF>), `m_wraps`, `m_allowedDir` (eMenuDirection),
  `m_navOffTopCB`/`m_navOffBottomCB` (Action). Keyboard/controller navigation and
  mouse hover (button `m_hoverCallback` -> `SelectedButton` setter) both land here.
  Buttons carry a `m_menu` back-reference to their list.
- Windows reach it via menu components, e.g. `T_6BD00C7B.m_mainMenu` (`T_96E81635`)
  `.m_buttons`. Settings rows: button GameObject (e.g. `7Subtitles`) holds a
  `Title` label child (left) and an `Options` value label child (right).
- NGUI `T_D90B5C2A.hoveredObject` is only a mouse-side source; keyboard menu nav
  never touches it. Poll button lists for focus.

## Settings row components (button subclasses on the row GameObject)

- `T_12B4FFCA` OptionCycleButton: `m_optionText[]`/`m_optionTextKeys[]`,
  `CurrentOption`, `SetCurrentOption(int)`, `LeftPressed()`/`RightPressed()`,
  `m_titleLabel` (title); inherited `m_label` IS the value label.
- `T_AD683E92` SliderButton: `m_slider` (`T_5B491CA4` UISlider clone, `value` 0..1;
  `sliderValue` is the obsolete alias), `SetValue(float)`, `m_changedCallback`.
- `T_6C7B7218` NumberSpinButton: `m_numberLabel`, `CurrentNumber`, `SetNumber(int)`.
- `T_55D727DD` KeybindRow (MonoBehaviour beside/above the button): `m_gameInput`
  (List<eGameInput>), `m_boundKey` (KeyCode), `m_isKeybinding` (true while waiting
  for a key), statics `s_AllKeys`, `IsKeybinding()`.
- `T_B2F04CEA` ControlsSettingsPanel: `m_keybindings` (`T_7D601BE`), sensitivity
  slider + invert/vibration option rows.
- `T_719FBC8E` EpisodeButton: `m_data` (`T_217E2223`), coming-soon labels.
- `T_9E0B6053` DirectionalNavButton: `m_upButton`/`m_rightButton`/etc, `m_index`.

## NGUI clones

- `T_A243E23D` UILabel: `text`, `textKey` (localized via `T_A7F32A9D.Get`),
  `textSheet`, `processedText`, `fontSize`. Setting `text` clears `textKey`.
- `T_D90B5C2A` UICamera: static `Notify(GameObject, "OnHover", bool)`,
  `IsHighlighted(GameObject)`, `currentScheme`. Menu focus hook point.
- `T_9E2FDFCF` UIButtonBase: `m_label`, `m_button`, `Selected`, `Active`,
  virtual `OnHover(bool)`.
- `T_EE44F2C4` UISprite; `T_D572A3A0` bitmap font; `T_A8C03557` UIPanel;
  `T_445E6D95` UITable (`Reposition()`); `T_4609CBAE` tween group.
- Markup: `[RRGGBB]`, `[-]`, `[b]` etc. — strip via `TextUtil.CleanLabel`.

## Settings

- `T_1005C221` (+ nested `T_948ABF01`) GameSettings: `GetSubtitlesToggled()`,
  `SetSubtitleSize(int)`, etc. Persistence via `T_4374D5FF.ReadNumber/WriteNumber`.

## Leaked original names (debug strings)

- `T_6FD30C1C` / `T_5A4116A6` / `T_86044B2` → ST_Hotspot family
- `T_56F32994` → TagManager
- `T_5E38271D` → GameDataSaveManager
- `T_3C522AE5` → LightShowManager
- `T_4B84CB26` → ActorInstance
- `T_EF88A416` → CriMovieManager
- `T_F8F2ECD1` → ActorBehaviorPathEntry
- `T_D51489BB` → ActorBehaviorAnimEntry
