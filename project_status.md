# Before the Storm Access - Project Status

Last updated: 2026-10-20

## v1.0.0 — RELEASED (GitHub release v1.0.0, 2026-10-20)

User asked to merge, push and release, bumping to 1.0. v1.0.0 = v0.9.2 content
(Prism speech, all 9 languages, How to Play/tutorials incl. controller icons).
Release assets: windows (tested), macos + linux (experimental, never run).
How to Play / controller icon reading was released before its in-game test;
still pending (see v0.9.1/v0.9.2 below).

## v0.9.2 — Controller icons in dynamic labels (BUILT, PENDING IN-GAME TEST)

User request: cover controller glyphs too (v0.9.1 skipped them).
  - KeyTokenText.ControllerName: "[P..]"/"[X..]" tokens from DawnUI's joystick
    token method. Sticks → pad_left_stick/pad_right_stick; D-pad → pad_dpad
    "D-pad {0}" reusing dir_* words; PS face buttons → pad_square/triangle/
    circle/cross (Action1-4 = left/up/right/down, confirmed from the PS3_*
    sprite names); Xbox face buttons → X/Y/B/A; shoulders → L1/R1/L2/R2 or
    LB/RB/LT/RT (printed labels, untranslated).
  - Start/Select depend on the pad generation (GameRefs.ControllerType =
    InputManager.GetControlType()): PS4 Options / touch pad (Share is reserved
    by the system, so the game's Select is the touch pad); PS3 Start/Select;
    Xbox One pad_menu / pad_view; Xbox 360 Start/Back; "[XOLDSEL]" = Back.
  - Keyboard tokens are looked up first, so "[P]", "[X]", "[PGUP]" stay keys.
  - 10 new mod keys, all translated (77 keys per file).
Pending test: How to Play and a tutorial prompt with a controller connected
(any pad), if available.

## v0.9.1 — How to Play + tutorial prompts read (BUILT, PENDING IN-GAME TEST)

User report (2026-10-20): v0.9.0 tested and working (Prism, all tests, new
translations). But the "How to Play" text isn't read.

Root cause: How to Play pages (T_CF3FA5DC.m_pageText) are NOT UILabels
(T_A243E23D) but the game's "Idol Dynamic Label" (T_5D6C057E, also a
T_73BC0C17 widget), which WindowWatcher never collected. The same class
carries tutorial prompts (T_C811E0CD.m_uiButtonLabel in TutorialWindow),
UserInformWindow's m_displayLabel and the brightness/scaling directions, so
those were silent too. Its text holds inline key-icon tokens ("Use
[W][A][S][D] to move", "[LSHIFT]", "[MOUSEL]", "[B/]").

Fix:
  - WindowWatcher.CollectVisibleLabels also collects T_5D6C057E (shared
    TryMakeEntry for both label classes); the F10 dump lists them with raw +
    spoken text.
  - KeyTokenText.ToSpeech: token runs → localized key names joined by ", "
    (FocusHandler.PrettyKeyName). "[MOUSE]"/"[CONSEQ]"/"[NEW]" → new mod
    strings key_mouse / icon_consequence / icon_new (all 8 translations). NGUI
    markup tokens are left for CleanLabel. Leading "-" bullets are dropped.
    Controller tokens ([PAD], [XR2]) have no game names and are skipped.
  - GameRefs.InlineKeyTokens(): token→KeyCode table built by reflection over
    DawnUI's private KeyCode→token method (obfuscated name; found by signature
    string(KeyCode) + output "[W]" for W, since a sprite-name sibling shares the
    signature).
  - Simulated offline on the EN/FR How to Play text: "Use W, A, S, D to move.
    Use mouse to move the camera. Hold Left Shift to move faster..."
Pending test: open How to Play (pause menu), flip pages with left/right; check
tutorial prompts in a new game; press F10 on How to Play if anything reads oddly.

## v0.9.0 — Prism speech + Mac/Linux packages (TESTED, working per user 2026-10-20)

Decisions (user, 2026-10-20): replace Tolk with Prism (ethindp/prism, MPL-2.0)
rather than UnityAccessibilityLib, whose speech manager is a subset of
ScreenReader.cs and which brings no new screen readers. Prism adds VoiceOver
(Mac), Orca/speech-dispatcher (Linux), and Windows readers matching the new
languages (PC-Talker JA; ZDSR, BoyPC ZH; Sense Reader KO), plus Narrator and
OneCore. Stay on BepInEx 5.4.23.5 (it already ships Mac/Linux; v6 is still a
pre-release; MelonLoader gains nothing). Ship windows/macos/linux zips.

Mechanism (PrismSpeech.cs):
  - Prismatoid (.NET bindings) needs net8, so the mod binds the C API itself.
    The library is loaded by ABSOLUTE PATH from the plugin folder
    (LoadLibraryW / dlopen via libdl.so.2 or /usr/lib/libSystem.dylib), and
    every export is bound through GetDelegateForFunctionPointer. No reliance
    on Mono's per-platform DllImport search.
  - prism_init(NULL) (no config struct marshalling), then
    prism_registry_create_best. Strings are UTF-8 byte[] with a trailing NUL;
    bools are I1.
  - Output failure → re-select best backend, retry once. On a TTS fallback
    (SAPI/OneCore/AVSpeech/speechd), probe higher-priority backends every 5s
    (IS_SUPPORTED_AT_RUNTIME check before initialize) so a screen reader
    started after the game takes over, as with Tolk.
  - ScreenReader.cs logic is unchanged; stop-before-interrupt is kept for NVDA
    sleep mode.
  - Binaries: scripts\Get-Prism.ps1 fetches v0.18.3 (SHA-256 pinned) into
    native\ (gitignored). The build copies prism.dll to plugins.
    Package-Mod.ps1 makes 3 zips; Mac/Linux are based on the official BepInEx
    zips (hash pinned) and ship Prism NOTICE + LICENSES.
  - Facts: prism.dll needs only system DLLs. libprism.dylib is universal.
    libprism.so NEEDS glibmm/giomm 2.68 + sigc 3 (in the README). Windows 10+.
    Mac VoiceOver can't stop/interrupt.

NOT VERIFIED — Feral Mac/Linux build: the user owns the game on GOG, which
sells Windows only, so there is no Mac build to decompile. Whether Feral's
Assembly-CSharp keeps the same T_ names is unknown. The README marks Mac/Linux
experimental. If a build becomes available, decompile into a gitignored
decompiled-mac\ and diff the T_ names used in GameRefs.cs and across the mod.

Pending tests (Windows, NVDA):
  1. Log: "Prism: loaded ...prism.dll", "Prism: speaking through NVDA."
  2. Normal play: subtitles, choices, menus, repeat key; braille if available.
  3. NVDA sleep mode: interrupting announcements still cut off.
  4. Start with NVDA off → system voice; start NVDA → "switched to NVDA"
     within ~5s.
  5. Quit NVDA mid-game → "output failed; re-selecting", then system voice.
  6. No BepInEx error about prism.dll sitting in plugins (a debug-level skip
     is fine).
Old Tolk.dll / nvdaControllerClient64.dll remain in the game root, unused.

## v0.8.0 — Localization pass, all 9 game languages (DEPLOYED, merged to master)

Context: PR #1 (MattrimCotton) added a French file. Merged locally on branch
localization-pass (contributor commit kept), then a full localization pass on top.
Not yet pushed — needs user OK.

Audit result: all speech already went through Loc.Get, but three paths leaked
English made from code identifiers: window names (GameObject names), key names
(KeyCode enum), decimal formatting (always "2.5").

Structure (fewer mod strings, game terminology):
  - Decoded the game's .lsb sheets (XOR format, see docs\game-api.md
    "Localization sheets"; scripts\dump_game_strings.py). 4,540 keys x 9 languages.
  - GameRefs.GameText(key): game translation, "" when missing or "[PH-xx]"
    placeholder. Loc.Game(key, englishFallback) logs once per missing key.
  - From the game now: Backtalk/Choices headings, "Your Choices" recap title,
    all key names (mirrors the keybinding row's table), window titles (Pause,
    Settings, Journal, Episodes, Extras, Credits, How to Play, Map, Mixtape Mode,
    Loading). Windows with no game title are announced by content only (was
    "Tutorial window." etc.); main menu keeps mod string window_main_menu.
  - Percent strings are "{0}%" (screen reader speaks % in its language).
  - Decimals use the language's culture (Loc.FormatNumber; fr "2,5").
  - Removed keys: backtalk_prefix, choices_prefix, choices_recap_prefix,
    key_unknown, key_left/right/middle_mouse. Added: heading, window_main_menu.
  - Key game finding: official FR Backtalk is "Insolence" — the PR's
    "Joute verbale" was replaced; every translation uses the game's own terms.

Translations: Translations\*.txt for fr, de, es, es-mx, it, pt, ja, zh-hans
(64 keys each). Build copies them to BepInEx\plugins; Package-Mod.ps1 (from the
PR) ships them. scripts\Check-Translations.ps1 validates keys + placeholders.

Pending tests:
  1. English: choices read "Choices: ...", backtalk "Backtalk: ...", recap
     starts "Your Choices.", key names in settings, window titles.
  2. Switch Text language to French (or any): mod speech follows live; log
     shows "Loc: loaded 64 strings for 'fr'"; distances say "2,5".
  3. Log has no "Loc: game has no ... text for" warnings.

Known, not investigated: the help text names mod keys by US symbol (brackets,
backslash, grave). How Unity 5.5 maps those KeyCodes on AZERTY/QWERTZ layouts is
unverified; a French/German keyboard user may need different keys.

## v0.4.0 - v0.7.0 test results (user, 2026-10-20)

All pending in-game tests passed ("They went great"): SMS live order, burner
phone, journal page order, interact on-screen gate, choices recap + absent-stats
note, choices holding the speech floor.

## v0.7.0 — SMS live-order + burner phone + journal page order (TESTED, working)

User report (2026-07-16), four related issues:
  1. Burner phone contacts/messages screen only said "contacts"/"messages" (window
     titles) — the contact names and thread content were never read.
  2. Live back-and-forth texting read the messages BADLY OUT OF ORDER.
  3. The same out-of-order problem happens in journal SMS threads that update in real
     time (Chloe types, someone replies) — a general SMS issue, not burner-only.
  4. Journal PAGES (not SMS): choice-dependent rows read out of order, disjointed.

Root causes:
  - SMS ordering: the old handler recorded every row on T_AD5CB3F7.Setup and spoke the
    WHOLE thread at once on SetupPage postfix. But a live conversation is animated in by
    CR_ActiveMessages ONE MESSAGE AT A TIME (queued rows built hidden, then activated +
    ShowFullMessage as each "arrives"). Speaking the batch up front raced that animation
    → wrong order / future messages read early.
  - Burner phone was never patched (deferred in v0.4.0). Its SetupPage is a SEPARATE
    method from the journal tab's, so no flush fired; contacts weren't read because the
    contact button (T_9E2FDFCF) added to the menu carries no name — the name/unread live
    on the sibling/owner T_EF99895D.
  - Journal page order: WindowWatcher sorted page labels purely by screen y/x. But a
    journal page (T_32AFC3D1) fills m_labels[] in NARRATIVE order via TextTriggered, and
    choice-dependent rows are placed where screen layout diverges from that order.

Fixes (SmsPatches.cs rewritten; shared by both SMS windows):
  - Two modes per thread, decided at the SetupPage flush from each row's
    activeInHierarchy (queued live rows are inactive at flush; AddMessage applies
    SetActive AFTER Setup, so liveness is judged at flush, not at RecordRow):
      * STATIC open (all rows visible): batch, unread-only when there are unread,
        else full thread — journal rule, now also for the burner.
      * LIVE conversation (queued rows present): history batch spoken now; each queued
        message spoken on its own when it reveals.
  - New postfix on T_AD5CB3F7.ShowFullMessage: when armed and the row is active (the
    coroutine reveal, not the Setup-time call) and not already spoken, speak that single
    message with interrupt:false so reveals queue in order. Typing-dots placeholder
    (./../...) guarded out. Row instance IDs dedupe against the history batch.
  - Burner phone (T_3FE8D8BE) now patched: SetupPage prefix Reset + postfix
    AnnounceThread (mirrors the journal tab); BackInSpecific postfix Reset. So viewing
    the phone normally reads like the journal, and a live incoming Damon text reveals
    message by message.
  - FocusHandler: SMS contact rows read as "{name}, {unread}" via
    GetComponentInParent<T_EF99895D>() from the selected button (m_name +
    active new-message label). Journal contacts already worked (focus lands on the
    T_EF99895D object); this also covers the burner, where focus lands on the button.
    New Loc key sms_contact_unread.

Fix (WindowWatcher.cs) for journal page order:
  - CollectVisibleLabels now anchors each label on a T_32AFC3D1 page to its index in the
    page's authored m_labels[] array (grouped per page by the page root's y). Non-page
    labels keep positional top-to-bottom/left-to-right. Single-anchor total order in
    CompareForReading (no transitivity bug). Both the on-open read and the change tracker
    use it.

NEW MAPPINGS (docs\game-api.md): T_AD5CB3F7.ShowFullMessage + live-reveal mechanism
(queued rows hidden via SetActive, revealed by CR_ActiveMessages); T_EF99895D
SMSContactRow (m_name/m_highlightedName, m_newMessage/m_highlightedNewMessage, m_button,
m_index); T_32AFC3D1 JournalPageFiller (m_labels authored in reading order, TextTriggered
fills sequentially).

## v0.6.2 — Interact "on screen" gate reads the game's own flag (DEPLOYED, PENDING IN-GAME TEST)

User report (2026-07-16): certain in-range interactables — most notably scene EXITS —
don't announce "in range" even though the game lets you interact with them (verb-based
interact, same mechanic as doors/items, NOT walk-through boundaries). Log + F10 dumps
provided.

Log analysis:
  - Verb-carrying exits DO work: "Door. down: Leave." / "Dormitory Entrance. down:
    Unlock." announced fine when in range. So the browser/verb path is OK for those.
  - The failing case is the CheckOnScreen gate over-suppressing. v0.13.1 added
    IsInteractPromptOnScreen by RE-DERIVING the game's CheckOnScreen (T_572A4969:234)
    viewport math from Camera.main + the aim point. That re-derivation can diverge from
    what the game actually decides (extended-view / UI-3D-camera offsets, T_34182F31.main
    vs Camera.main, anchor-vs-aimPoint, per-frame timing), and when it does the mod
    suppresses a prompt the game would allow — silence on exits approached head-on.

Fix — read the verdict instead of recomputing it:
  - IsInteractPromptOnScreen now returns freeRoam.m_interactUI.m_choiceUI.gameObject
    .activeInHierarchy. The game's CheckOnScreen runs every frame and stores its result
    by enabling/disabling that exact object (choice UI live == interact actionable). This
    is the signal the HotspotHandler comment always said to use; the implementation was
    re-deriving it instead. The old geometric mirror is kept as a fallback only when the
    choice UI object can't be reached, and still returns true when the camera is
    unreadable (no over-suppression).
  - Refactor: distance test extracted to IsDistanceOk (camera OR main-character within
    m_interactDistanceOverride); IsWithinInteractDistance = IsDistanceOk && angle &&
    promptOnScreen. New helper InteractChoiceUI() (T_6876113C m_choiceUI on the interact
    UI). No new GameRefs mappings needed (m_interactUI + m_choiceUI already public).
  - F10 dump now prints, for the current interact target: hasPointAt, choiceUIActive,
    distanceOk, angleOk, promptOnScreen, and the final interactable verdict — so any
    remaining "in range but silent" is diagnosable in one line.
  - NOTE: pure walk-through boundary exits (Boundarytrigger/DoorwayTrigger obj, PointXtoY
    navpoints) are isOnlyTrigger with 0 verbs; the game shows no interact prompt for them
    (crossed by walking). User confirmed the missing exits are verb-interact ones, so
    those boundaries are out of scope here. Revisit as a separate "exit nearby" cue if
    the user later reports walk-through exits.

## v0.6.1 — Choices recap explains absent stats (DEPLOYED, PENDING IN-GAME TEST)

User report (2026-07-16): the recap still "isn't reading the world stats." User is on
the GOG OFFLINE build and was NOT signed in.

Diagnosis (not a reading bug): the world/friends percentages come from Deck Nine's
online stats server. T_35562F97 fetches them on open (GetWorldChoices/GetFriendChoices)
and only the async callback (OnFriendChoicesLoaded / _1C3E99DD9) populates
T_80D0C1C9.m_worldPercent/m_friendPercent. Offline + not signed in, that fetch never
happens, so every option's stat stays 0. The mod was reading them correctly — there
were none. A sighted player sees blank percentages on the same setup (the game also
blanks the on-screen m_percent label unconditionally in this build). So the mod was
silently omitting stats, which read as "the mod skipped them."

Fix — ChoicesHandler now EXPLAINS the absence instead of staying silent:
  - Tracks anyStat across the whole recap (any option with a stat > 0). If none
    carried a stat, appends a one-time note chosen from the game's network state:
    choices_stats_offline (CurrentNetworkState != Online),
    choices_stats_not_signed_in (online but CurrentSignInState != Online),
    else choices_stats_unavailable. anyStat feeds the signature ("|nostats"), so if
    stats DO arrive later (online session) the recap re-speaks with the numbers.
  - Diagnostic: logs each option's raw world/friend/chosen ints, so the next test
    confirms whether stats are genuinely 0 (offline) or populated-but-unread.
  - GameRefs.NetworkManager accessor added (T_A6E913D1.m_networkManager, type
    T_4CC5666A with CurrentNetworkState / CurrentSignInState, enum Online/Offline/
    Unknown/None). New Loc keys: choices_stats_offline / _not_signed_in / _unavailable.
  - To hear actual percentages the user must run online AND signed in; otherwise the
    new spoken note is the expected, correct behavior.

## v0.6.0 — Choices recap screen reads decisions + stats (DEPLOYED, PENDING IN-GAME TEST)

User report: the end-of-episode / main-menu "Choices" recap screen read the whole
screen top-to-bottom (WindowWatcher's generic net) instead of the decisions the
player made, and never read the stats.

Root cause: ChoicesWindow (T_35562F97, gameObject "ChoicesWindow") was NOT excluded
from WindowWatcher, so the generic label net dumped episode title + every option of
every decision + tab captions + options bar as one undifferentiated wall — with no
"which one did I pick" and no percentages. The on-screen percent labels are blanked
in this build: T_80D0C1C9.ShowStats / ShowPercent set m_percent.text = "" and
m_percent.enabled = false, so the visible label had no text to read. The actual
numbers live only in the option components (m_worldPercent / m_friendPercent ints).

Fix — new ChoicesHandler.cs (polling, in AccessibilityWatcher after Backtalk):
  - Reads the ACTIVE choice group: whichever T_35562F97.m_majorChoices[i] /
    m_minorChoices[i] is activeInHierarchy (ShowMajor/MinorChoices ResetWindow-then-
    activate exactly one, so the single active group is the visible tab+episode).
  - Per decision (T_8F7CA4EC.m_choices) reads each option (T_80D0C1C9): chosen
    option first ("you chose X"), then the alternative ("The other option: Y"),
    using m_isChosen (always correct locally) and m_choice.text (resolved label).
  - Stats: m_worldPercent (world icon active) else m_friendPercent, appended only
    when > 0. SCALING: SetWorld/FriendPercent store a WHOLE percentage point
    (server "0.67" -> RoundToInt(0.67*100)=67) and T_8F7CA4EC.Setup rebalances the
    pair to sum to 100 — so the field IS the percent; speak it directly, do NOT /100.
    (Stats are online-only; offline they stay 0 and are simply omitted.)
  - Signature-gated: re-speaks only when the shown content changes (window open, tab
    switch major/minor, episode change, stats arriving from the server).
  - Repeat key (grave) re-reads the recap via ChoicesHandler.RepeatCurrent(), added
    to HandleModKeys after the live-dialogue-choice branch.
  - ChoicesWindow added to WindowWatcher._excludedWindows so the two don't both speak.
  - GameRefs.ChoicesWindow accessor added. New Loc keys: choices_recap_prefix,
    choices_decision, choices_you_chose, choices_alternative, choices_option_stat.
  - NOTE: ConsequenceWindow (T_B6500484) is the SEPARATE in-gameplay "choice made"
    popup (m_majorChoiceMsg/m_minorChoiceMsg) — NOT touched here; still read by the
    generic net. Revisit if the user reports it too.

## v0.5.0 — Choices hold the speech floor (DEPLOYED, PENDING IN-GAME TEST)

User report: backtalk challenge / choice text was being pre-empted by other text
cues (in front of AND behind it). Backtalk choices are the highest-priority read —
direction keys commit instantly with no review step — so nothing may cut them off.

Root cause: the earlier "choices win the speech race" fix (75849aa) only stopped
DIALOGUE/subtitle lines from interrupting a choice. Two other vectors still cut it:
  1. The coalesced ambient chain. AccessibilityWatcher.Update calls
     BeginCoalescedCycle() every tick, which arms the FIRST SayCoalesced of that tick
     to interrupt stale speech. While a long choice read was still playing, the first
     ambient cue (Focus / Hotspots / Windows / Nav / GameInfo) on any LATER tick
     interrupted it. The choice announcer uses raw Say (not SayCoalesced), so it never
     participated in the coalescing that would have protected it.
  2. Backtalk points won/lost (BacktalkPatches) used Say(interrupt:true), cutting off
     a choice being read during a round.

Fix:
  - AccessibilityWatcher.Update: skip BeginCoalescedCycle() while IsChoiceActive().
    With the interrupt un-armed, every coalesced ambient announcement that tick
    APPENDS instead of silencing the in-progress option read. The choice announcer's
    own Say(interrupt:true) still clears stale speech on the tick the set first
    appears (evaluated before the arming check, so IsChoiceActive() is already true).
  - BacktalkPatches point won/lost: interrupt only when NO choice is active
    (!AccessibilityWatcher.IsChoiceActive()); otherwise queue behind the choice.
    bt_won/bt_lost normally fire after the choice UI is gone, so they still interrupt
    at round end.
  - Net effect: while any dialogue/backtalk choice is on screen it holds the speech
    floor — ambient cues and score feedback queue behind it, never cut it off.
    Highlight-move re-announces (hover / joystick aim over a single option) use a
    direct Say(interrupt:true) so they interrupt EACH OTHER — scrubbing stays
    responsive, latest highlight wins. They can't cut the full set read: that lives
    in the signature-change branch and returns before the highlight-move branch.

## v0.4.0 — SMS unread-only reading (TESTED, working per user 2026-07-14)

NOTE: version scheme reset to single-digit minors climbing toward 1.0 (0.4, 0.5,
...). Earlier "0.13.x" numbering was dropped; prior builds were never published.

- New SmsPatches.cs (SmsHandler + Harmony patches). Opening an SMS thread now reads
  ONLY the unread messages when the thread has any; a thread with nothing new is
  read in full (user deliberately reopened it).
- Patched window: T_6BD5080F = JournalWindow's SMS tab — the way the user reviews
  texts. It lives inside JournalWindow, which WindowWatcher reads, so v1 (which only
  patched the standalone burner phone) failed: the whole thread still read via the
  journal. Exposes AddMessage(T_200B34A4) + SetupPage() with the
  InstantTraversal()-then-Seen=true ordering the fix depends on.
- NOT YET PATCHED: T_3FE8D8BE BurnerSMSWindow (standalone phone; opens directly only
  for special/Damon texts). Same shape, so the same SmsHandler will cover it — left
  out until the user actually reaches the burner phone in-game. Per user request.
- How: unread state is the game's own per-node Seen flag (T_BFD9C065.Seen, save-backed
  "Seen_SMS"+nodeName). SetupPage marks every node Seen=true while building the page,
  so the pre-open value must be captured earlier — during the row build, before that
  node's Seen=true. Collection is a postfix on the message ROW's Setup (T_AD5CB3F7.Setup,
  shared by both SMS windows), which runs after ShowFullMessage has resolved the labels
  and while SMSParent.Seen still holds its pre-open value.
- IMPORTANT: T_200B34A4.content is a localization KEY (e.g. "SMS_083"), not text; the
  label resolves it through its localization sheet. Reading content directly spoke the
  raw key. FIX: read the ROW's resolved labels — row.m_messageLabel.text (body) and
  row.m_timeStamp.text (timestamp) — mirroring exactly what the player sees.
- Timestamp: WindowWatcher used to read it as part of the row; once rows were suppressed
  there it went missing, so RecordRow now also captures m_timeStamp and speaks it as a
  short trailing aside after each message.
- Flush: journal SetupPage PREFIX clears _pending (so only this thread's rows collect),
  POSTFIX speaks the built thread; ResetTab postfix clears stale state.
- WindowWatcher: skips any label with a T_AD5CB3F7 (SMS message row) ancestor, so
  the journal's SMS message table isn't also read in full by the generic net.
  ALSO skips labels under the global options bar T_1FBA79F9 (Select/Back/Exit
  prompts). BUG FOUND IN TEST: the announce worked, but ~0.4s after a thread opened
  WindowWatcher's poll saw the options bar's "Exit" label as new on-screen text and
  spoke it with interrupt, cutting off the in-progress SMS read ("starts to read the
  thread then exit preempts it"). Those prompts are persistent action labels, not
  content, and FocusHandler narrates the focused button — so suppressing them from
  the generic net fixes the preemption with no loss.
  The contact-selection list is still read by FocusHandler (polls T_51AF6A60
  generically) — unread counts intact.
- Speech format: "{sender}: {content}. {timestamp}. ..." (unread messages only, or
  the full thread when nothing is new). Chloe's own texts read as "Me"
  (JOURNAL_CODE_SMS_ME), mirroring the game's label logic.
- NEW MAPPINGS recorded: T_6BD5080F JournalSMSTab (AddMessage, SetupPage,
  SetupSpecific, SetupSelection, ResetTab, s_smsByContact, SetSMSContact),
  T_3FE8D8BE BurnerSMSWindow (AddMessage, SetupPage, s_newSMS), T_200B34A4 SMSMessage
  (sender, content, SMSParent), T_BFD9C065 SMSNode (Seen, ShouldShow, specialSMS,
  unlockedAtStart, contact, dreamScene), T_BA92A050 SMSNodeBase (Seen/ShouldShow save
  flags), T_87122939 JournalTabBase (m_selection, m_specific, SetupPage virtual),
  T_AD5CB3F7 SMSMessageRow (sender, m_messageLabel, Setup).

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
- v0.11.2: interact gate now also mirrors the game's ANGLE-interact test. Distance
  alone was insufficient — angle-gated hotspots (usesAngleInteract, i.e.
  m_LeftInteractAngle != m_RightInteractAngle) still announced the full verbs +
  interact hint while the player stood off-angle and the game refused to interact.
  IsWithinInteractDistance now measures distance from the follow camera (with
  character fallback) AND, for angle hotspots, computes the horizontal approach
  angle vs Vector3.forward and calls hotspot.GetAngleInteractValid — the exact
  block from T_F8FE3E1C lines 336-349. Resolves the long-standing "in range but
  interact did nothing" report (v0.5.0 ISSUE 2). GameRefs.FollowCameraTransform
  added so the mod measures from the same origin the game uses.
- v0.11.2 (cont.): navigation now paths to the hotspot's AIM POINT
  (HotspotHandler.GetAimPoint / m_pointAt), not its root transform. The game judges
  interactability from the aim point, so routing to transform.position could land
  the player at the pathing goal yet outside the interact gate (a second cause of
  "arrived but can't interact," independent of the angle fix). ComputePath's
  existing SamplePosition snap pulls an off-mesh aim point onto the nearest walkable
  cell. Navigation, the game gate, and the mod gate now all target the same point;
  the arrival announcement (already aim-point based) matches where guidance steers.
- v0.13.1: THE decisive interact gate — the game's own on-screen check, queried
  not guessed (NOTE: earlier entries in this list mislabel these as "v0.11.x"; the
  plugin was already at 0.13.0). User report: standing between two objects (Poster
  + Costume Rack, both usesAngleInteract=False), sweeping the CAMERA, both announced
  interactable but pressing interact did nothing for either until the camera found
  the right angle. Root cause found in T_572A4969.CheckOnScreen (the interact UI's
  own per-frame test): the choice UI — and thus a working interact — is enabled only
  when the aim point is in front of the main camera (angle < 90) AND its viewport
  projection is inside [0,1]. Distance/angle-interact don't capture camera aim.
  HotspotHandler.IsInteractPromptOnScreen now mirrors CheckOnScreen exactly
  (Camera.main = the game's T_34182F31.main), gating announcements on it. Earlier
  angle-interact + follow-camera-distance work retained as a pre-filter; the
  on-screen check is the decider. F10 dump now logs camDist + camFacingAngle per
  hotspot. (An interim 35-degree InteractFacingAngle config guess was written then
  removed once CheckOnScreen was found — no threshold needed.)
- v0.13.1 (cont.): CAMERA-BASIS CONSISTENCY. Confirmed from decompile
  (T_C3DD66D9.CalculateAngle) that movement is camera-relative — walk direction =
  input offset by Camera.transform.localEulerAngles.y; the body rotates toward that
  with lag. So every "which way to aim/turn" readout now measures from the camera,
  not the character body (which diverges while standing/strafing/after a swing):
    * Nav walking legs (AnnounceRoute first leg + live GuidanceTick steer) → camera
      forward via new SpatialUtil.CameraForward(). Later legs stay geometric
      (direction just walked). USER-APPROVED behavioral change.
    * Arrival aim hour → camera position + forward (was body). Elevation already was.
    * Hotspot browser DescribeRelativePosition clock hour → camera (distance stays
      player-to-object).
  Nav arrival threshold: ArrivalDistanceFor(target) = m_interactDistanceOverride
  floored at 1.5m (was fixed 2.0m), so "Arrived" matches each object's own interact
  radius. Nav still PATHS to the aim point (unchanged) — pathing answers "where to
  stand", which is distinct from and can't satisfy the camera-aim gate; the arrival
  line then tells you where to point the camera. All three — interact gate, arrival
  aim, browser direction — now reference the same camera.
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
- Speech: Prism prism.dll in BepInEx\plugins (since v0.9.0). The old Tolk.dll and
  nvdaControllerClient64.dll in the game root are unused leftovers.
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
