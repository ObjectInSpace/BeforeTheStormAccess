# Before the Storm Access - Project Status

Last updated: 2026-07-13

## Recent versions (v0.10.1 - v0.11.1, from user playtesting)

- v0.10.1: journal pages re-read when the user returns to them (label VISIBILITY
  tracking, not just text changes; page text batched into one announcement in
  reading order). User report: "only reads each journal page once."
- v0.10.2: flap guard softened (6 changes/10s trips it, 4s mute — was 4/10s, 15s).
  User report: "fifteen seconds seems pretty long."
- v0.11.0: elevation for camera aiming — browser/backslash announcements append
  "slightly up/down" (10-30 deg) or "up high"/"down low" (>30 deg), measured
  camera-to-aim-point (hotspot m_pointAt when authored); guidance arrival speaks
  "Aim at {clock} o'clock, {elevation}". User report: clock directions lacked
  verticality for small objects (photos, phones).
- v0.11.1: interact announcement gated on the game's true reach check — trigger
  volume entry says "{name} nearby.", full verbs + hint only when the 3D distance
  from the main character (T_A6E913D1.m_mainCharacter) or camera to the aim point
  is under m_interactDistanceOverride (mirrors FreeRoamWindow's interact UI
  scaling gate). User report: announced interactable when merely in the vicinity
  (trigger volumes extend vertically).
- Git repo initialized at project root (decompiled/ excluded — copyrighted);
  release zip: dist\BeforeTheStormAccess-v0.10.0.zip (rebuild via
  scripts\Package-Mod.ps1 for newer versions).

## Current phase

v0.10.0 — localization + verbosity config + distribution packaging — PENDING IN-GAME TEST.
Backtalk testing DEFERRED by user until they play past the tutorial.

## Distribution

- README.md: player-facing install/keys/settings/translation/troubleshooting doc.
- THIRD_PARTY_NOTICES.txt: BepInEx (LGPL-2.1), HarmonyX/MonoMod/Cecil (MIT),
  Tolk (LGPL-3), NVDA controller client (LGPL-2.1) — all unmodified binaries.
- scripts\Package-Mod.ps1: builds dist\BeforeTheStormAccess-v{version}.zip
  (extract-into-game-folder layout: BepInEx core + plugin DLL + doorstop +
  Tolk + README + notices). Tested; v0.10.0 zip is ~0.8 MB.

## v0.10.0 changes (Phase 5 foundation)

- Loc: follows the game's text language live (T_A7F32A9D.s_language, LanguageCode
  enum EN/DE/ES-MX/ES/FR/IT/PT/ZH-HANS/JA; RegisterPostLanguageChanged hook).
  Non-English strings load from BeforeTheStormAccess.strings.{lang}.txt next to
  the DLL (UTF-8 key=value, # comments), English fallback per key. A current
  translation template (strings.template.txt) is regenerated in the plugins
  folder on every start.
- ModConfig (BepInEx config, com.amock.beforethestormaccess.cfg):
  Verbosity/AnnounceWindowNames, ReadWindowTextOnOpen, AnnounceOutOfRange,
  InteractHintCount (0 disables), AnnounceObjectiveChanges;
  Navigation/GuidanceProgressSeconds. All wired into their handlers.

## v0.8.0 test results (user, 2026-07-11)

- REVERTED the all-hotspots browser change at user request: verb population IS the
  game's availability gating (only the fence was selectable until hopped, then the
  yard populated; routes past the fence were partial before hopping). The verb
  filter is intentional and correct — DO NOT remove it again.
- The no-trespassing sign never appears as a hotspot — accepted as not browseable;
  moved on per user.
- Interact hold-mechanic hint: kept, "a decent change."

## v0.9.0 changes

- HotspotHandler: verb-count browser filter restored (with explanatory comment).
- GameInfoHandler: O = current objective (GameStateModel.GetCurrentObjective ->
  localized), Shift+O = current location (gameDataManager.currentScene.nameKey).
  Harmony postfix on GameStateModel.SetCurrentObjective announces "New objective:"
  automatically (suppressed until m_UILoaded; appends, never interrupts dialogue).
- BacktalkHandler + patches: challenge start ("Backtalk challenge against {opponent},
  win {n} points"), point won/lost with running score (postfixes on DialogWindow
  UpdatePoints/UpdatePointsLost, change-gated because the game calls them every
  frame during tweens), win/loss results, "Hurry" when the round timer drops below
  30% (re-arms above 60%).
- NEW MAPPINGS: T_D81BBCF4 SceneData (nameKey, location enum), T_3A129DE7 GameTimer
  (TimeSet/TimeElapsed/Running), GameStateModel.GetCurrentObjective/SetCurrentObjective,
  T_36BE1CF4 GameDataManager (currentScene, currentEpisodeNumber).

## v0.7.0 test results (user, 2026-07-11)

- Shift+N continuous guidance: "worked reliably."
- Sign still missing from browser. F10 dump SOLVED BOTH open issues:
  - KEY FINDING: the game only populates hotspot verb lists when the player is
    CLOSE. Distant hotspots report verbs(a/n/l)=0/0/0. The v0.5-0.7 browser
    filter "must have a verb" therefore hid the whole scene except the in-range
    object. NEVER filter hotspots by verb count.
  - Interact "not working": dump showed mouse scheme + in-range + no angle gate —
    the mechanic is HOLD interact, then press action direction. A tap does
    nothing. Not a bug; a mechanic that needs teaching.
  - The sign is NOT an ST_Hotspot (not in the filtered-out list) — likely part of
    the journal/poster system (T_32AFC3D1 page filler, PosterWindow — read on
    open by WindowWatcher) or purely decorative. Re-check after v0.8 full list.

## v0.8.0 changes

- Browser lists ALL enabled hotspots (no verb filter); verbs still announced when
  populated (i.e., in range).
- In-range announcement appends, for the first 3 hotspots per session:
  "Hold {interact key}, then press the action's direction key." with the live
  binding name from InputManager.GetKeyBinding(kInteract).

## v0.6.0 test results (user, 2026-07-11)

- N-key route guidance "seems workable" — tested one simple unobstructed route only.
  Needs testing on longer routes with obstructions.
- Sign-in-browser fix and F10 interact diagnostics not yet exercised.

## v0.7.0 changes

- NavigationHandler: Shift+N toggles continuous guidance to the browser target.
  Re-plans every 0.4s from the current position; speaks "{clock} o'clock,
  {remaining} meters" whenever the steering hour changes (self-correcting against
  the player's live facing), a bare "{remaining} meters" progress tick every 3.5s
  (append, never interrupts dialogue), "Arrived at X" within 2 meters, "Route
  lost" when the path dies. Beacon pauses automatically outside kFreeRoam
  (dialogue, menus) and resumes after.
- Key safety verified in decompiled source: N, brackets, and backslash are not
  default game bindings (they only appear in rebind display code). Players CAN
  rebind game actions onto them — future mod-settings phase can add remapping.
- Exit naming deferred: verb exitTarget is an abstract StoryTeller graph node
  (T_F21F6473); naming the destination scene needs more digging.

## v0.5.0 test results (user, 2026-07-11)

- Overall works; user satisfied with direction.
- ISSUE 1: object list may be incomplete — a sign near a fence didn't appear (only
  the fence did). Hypothesis: verbs in m_nonInteractVerbs / m_localTriggerVerbs
  (v0.5 only scanned m_activeVerbs), or the sign is part of the poster system.
- ISSUE 2: "in range" announced but interact did nothing — possibly angle-gated
  interaction (m_LeftInteractAngle/m_RightInteractAngle) or camera-facing
  requirement. NEEDS F10 DATA at the moment it happens.

## v0.6.0 changes

- Browser now includes hotspots with active verbs in ANY of the three verb lists;
  verb announcements read all three (deduped by label).
- F10 dump now includes full interact state: s_inHotspotRange, control state/scheme,
  game mode, current interact hotspot (distance, angle gates, verb counts per list),
  all browseable hotspots with per-list verb counts, and FILTERED-OUT hotspots with
  the reason data — for diagnosing both open issues.
- NavigationHandler.cs (Phase 4 slice 1): N key = spoken walking route to the
  browser target via NavMesh.CalculatePath on the game's own NavMesh.
  "Route to X: 23 meters total. turn to 2 o'clock, 6 meters. turn to 11 o'clock,
  9 meters." First leg relative to player facing, later legs to travel direction;
  partial-path warning; max 6 legs then "press N again". SpatialUtil.cs extracted
  (player pose, clock hours, distance formatting; no Vector3.SignedAngle in 5.5).

## v0.4.0 test results (user, 2026-07-11)

- "Looks good." Sliders, value changes, keybind names, and the repeat bug all confirmed fixed.

## v0.5.0 changes (Phase 3)

- HotspotHandler.cs:
  - Interact UI polling: when FreeRoamWindow.m_interactUI targets a hotspot,
    announces "{name}. up: Look. right: Speak." (verb positions = action buttons);
    "out of range" when it clears.
  - Hotspot browser: RightBracket next / LeftBracket previous through enabled
    hotspots with active verbs, nearest first (FindObjectsOfType on keypress only).
    Announces "N of M: {name}, {distance} meters at {clock} o'clock." + verbs.
    Backslash re-announces the selected target with fresh distance/direction.
  - Player origin/facing: T_C3DD66D9.s_mainCharacterMesh (Camera.main fallback).
    Unity 5.5 has no Vector3.SignedAngle — computed via Atan2(Cross.y, Dot).
- Verb labels: T_A7F32A9D.Get(verb.localizationKey, "global") fallback nodeName.
  Hotspot names: Get(m_nameKey, m_localizationSheet) fallback m_friendlyName.
- NEW MAPPINGS: T_6160F3EF VerbEntry (localizationKey, verbPositionEnum
  top/right/bottom/left, storyVerb, exitTarget, active), T_572A4969 InteractUI
  (hotSpotObj, SetHotspot), T_8F74F848 HotspotNameUI (SetHotSpot). VerbEntry
  exitTarget (T_F21F6473) marks scene exits — useful for Phase 4 navigation.

## v0.3.1 test results (user, 2026-07-11)

- Menu focus reading WORKS (most menus speak on navigation).
- Bugs found: slider values not read; option value changes (e.g. Subtitles yes/no)
  not read on change; keybind names not read under controls; graphics setting
  change repeated 4 times.
- User direction: keep localization in mind — game ships many languages; mod must
  be structured to support them (Loc.cs already routes everything; game-language
  detection + translations deferred to Phase 5).

## v0.4.0 changes

- ScreenReader: global duplicate suppression — identical text within 800ms speaks
  once (repeat-last key bypasses). Fixes the 4x graphics-setting repeat.
- FocusHandler.BuildFullText: labels + slider percent (T_AD683E92.m_slider.value)
  + bound key name (T_55D727DD.m_boundKey, "press a key to bind" while rebinding).
  PollFocusedValue(): while focus is steady, full text is re-polled; option cycling,
  slider drags, and rebinds announce their new value.
- WindowWatcher: label changes inside menu-list buttons are no longer announced by
  the generic tracker (focus handler owns them) — removes double/multi announces.
- NEW MAPPINGS: T_12B4FFCA OptionCycleButton (m_optionText, CurrentOption,
  m_titleLabel; m_label = value label), T_AD683E92 SliderButton (m_slider),
  T_5B491CA4 UISlider clone (value), T_55D727DD KeybindRow (m_gameInput, m_boundKey,
  m_isKeybinding), T_B2F04CEA ControlsSettingsPanel, T_7D601BE KeybindingsPanel,
  T_6C7B7218 NumberSpinButton (m_numberLabel, CurrentNumber).

## v0.2.0 test results (user, 2026-07-11)

- Window announcements WORK: splash screen, main menu, episode list, confirm-quit
  modal, settings submenus all read on open/appear.
- F10 dump captured main menu + settings structure: settings rows are containers
  like `AudioButtons/7Subtitles` holding a `Title` label (left) and an `Options`
  value label (right) — read as pairs by the focus handler.
- User verbosity guidance: fine to gate on the game's subtitle setting OR a mod
  setting later — whichever is simplest; don't get bogged down in details now.

## v0.3.0 changes

- FocusHandler.cs: polled NGUI UICamera `T_D90B5C2A.hoveredObject`. FAILED in test:
  loaded clean, but keyboard menu navigation never touches NGUI hover in this game.

## v0.3.1 changes (deployed, PENDING TEST)

- ROOT CAUSE of v0.3.0 silence: menus use the game's OWN focus system, not NGUI
  hover. Every menu owns a button list `T_51AF6A60` (field `m_buttons` on menu
  components, e.g. MainMenuWindow.m_mainMenu.m_buttons) with `SelectedButtonIndex` /
  `SelectedButton` (T_9E2FDFCF). Keyboard nav AND mouse hover both route into it
  (buttons' m_hoverCallback -> SelectedButton setter; buttons carry `m_menu` back-ref).
- FocusHandler now polls all active T_51AF6A60 lists (0.12s interval), announcing
  selection-index changes per list; NGUI hoveredObject kept as secondary source for
  non-list elements (deduped, skipped when the element belongs to a list).
- MAPPING: T_51AF6A60 = MenuButtonList (SelectedButton, SetCurrentToFirst,
  m_navOffTopCB/m_navOffBottomCB, wraps). Recorded in game-api.md.

## v0.1.0 test results (user, 2026-07-11)

- F1 help, grave repeat, F9 debug: WORKED (Tolk + watcher + keys confirmed)
- Subtitles, tutorial prompts, objectives: NOT spoken
- ROOT CAUSE FOUND: RegexOptions.Compiled throws ArgumentOutOfRangeException on the
  game's ancient Mono; TextUtil type init failed, killing the subtitle and choice
  announcers every frame while keys (which run first) kept working. Fixed in v0.2.0.
  LESSON: no RegexOptions.Compiled on Unity 5.5 Mono; wrap every watcher subsystem
  in Guarded() (now done) so one failure can't silence the rest.

## v0.2.0 changes

- SubtitlePatches.cs: Harmony postfixes on SubtitleWindow.SetupSubtitle and
  ForceOverrideSubtitle setter — speaks each dialogue line at queue time using the
  game's own localization chain (independent of subtitle display settings); the
  m_speech polling remains as fallback with dedupe via SubtitleAnnouncer.AlreadySpoken.
- WindowWatcher.cs: generic coverage net. Announces any window becoming visible
  (activeInHierarchy + panel alpha, since NGUI hides via alpha) with a one-shot
  top-to-bottom read of its visible labels; tracks label text changes inside visible
  windows (tutorials, objectives, SMS toasts) with a flap guard. Excluded:
  SubtitleWindow, DialogWindow, TransitionWindow, MovieWindow, FreeRoamWindow.
- AccessibilityWatcher: per-subsystem Guarded() exception isolation with
  once-per-error logging; F10 = diagnostic dump of all windows/labels to log.
- BepInEx.cfg: WriteUnityLog = true (Unity-side errors were invisible before).
- Verified in-game: v0.2.0 loads with zero watcher errors; patches applied.

## Environment

- Game: Life is Strange: Before the Storm (GOG), `D:\Root\Life is Strange - Before the Storm`
- Unity 5.5.3p1, 64-bit, legacy Mono (CLR 3.0.40818.0 reported by BepInEx)
- Mod loader: BepInEx 5.4.23.5 (copied from the Date Everything install) — VERIFIED WORKING on this Unity 5.5 build (log shows chainloader complete, plugin loaded, NVDA detected)
- Mod target framework: net35 (the old Mono cannot load net472 assemblies)
- Tolk.dll + nvdaControllerClient64.dll installed in the game root
- Project: `C:\Users\amock\Before The Storm Access\BeforeTheStormAccess.csproj`
- Build: `dotnet build -c Release` (auto-copies DLL to BepInEx\plugins)
- Decompiled game code: `decompiled\` (ilspycmd output of Assembly-CSharp.dll)

## Obfuscation situation

Assembly-CSharp is obfuscated at the CLASS NAME level only (`T_XXXXXXXX` hashes).
Method, property, and serialized field names are readable. Debug strings leak some
original names (e.g. `ST_Hotspot`). The full verified rename map lives in
`GameRefs.cs` (code) and `docs\game-api.md` (reference). The UI is a renamed NGUI.
Dialogue engine is Deck Nine "StoryTeller"; audio is CriWare; text is localized
through the NGUI Localization clone (`T_A7F32A9D.Get`).

Remastered edition note: LiS:BtS Remastered runs Unity 2019.4 — an entirely
different build (not installed). This mod targets the original only.

## Implemented (v0.1.0)

- Plugin bootstrap (Main.cs), Harmony initialized (no patches yet — Phase 1 is pure polling)
- ScreenReader.cs: Tolk wrapper with SAPI fallback + coalesced interrupts (ported from Date Everything Access)
- AccessibilityWatcher: per-frame polling
  - Subtitle window text changes → spoken (NGUI markup stripped)
  - Dialogue + backtalk 4-way choice radial → choice set and highlight changes spoken with direction names (up/right/down/left = W/D/S/A)
  - F1 help, backquote repeat-last, F9 debug logging toggle
- Loc.cs English string table; TextUtil NGUI markup stripper; GameRefs typed accessors

## Pending tests (need user with NVDA)

Recommend pressing F9 (debug logging) at session start so the log captures [SR] lines.

1. Main menu and other windows: announced when they open, with their text read?
2. Start/load a game: are dialogue lines spoken as they play?
3. Tutorial prompts and objective text: spoken via the window watcher?
4. Reach any choice: choice set read with directions? Highlight moves re-read?
5. Backtalk: options read with "Backtalk:" prefix?
6. If anything is silent: press F10 at that moment (diagnostic dump), then send me the log.
7. Known gap: menu BUTTON FOCUS (which item is highlighted as you arrow around) is
   not yet read — that's the UICamera hover hook, next up. Window text is read on open.

## Next steps (pipeline)

- Phase 2: menu narration — hook/poll NGUI UICamera (`T_D90B5C2A`) hover/selection for
  MainMenuWindow, PauseWindow, SettingsWindow, ModalPopup, JournalWindow, BurnerSMSWindow;
  announce window open/close by watching WindowManager state.
- Phase 3: world interaction — browse nearby hotspots (`T_F8FE3E1C.s_currentTriggers` +
  scene scan of `T_6FD30C1C`), announce m_friendlyName + verbs + distance + clock direction;
  announce interact-range enter/leave.
- Phase 4: navigation — NavMesh.CalculatePath from player (`T_C3DD66D9.m_navAgent`) to a
  selected hotspot; spoken turn-by-turn guidance; optional auto-walk via the NavMeshAgent.
- Phase 5: journal/SMS deep reading, objectives, Zen mode, settings sliders, more languages,
  distribution packaging.

## Conventions

- All screen reader strings go through `Loc.Get()` (English base for now)
- Obfuscated type names appear ONLY in GameRefs.cs aliases/accessors and docs\game-api.md
- Never guess a class: verify in `decompiled\` first, then record the mapping
- Handler classes: `[Feature]Handler`; private fields `_camelCase`; XML docs on public members
- net35 restrictions: no Application.quitting, no modern BCL types; monolithic UnityEngine.dll
