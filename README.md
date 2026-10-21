# Before the Storm Access

A screen reader accessibility mod for Life is Strange: Before the Storm
(original edition, not the Remastered version). Speaks dialogue, menus,
choices, backtalk challenges, interactive objects, and provides walking
navigation using the game's own pathfinding.

Speech and braille go through the Prism library, which supports NVDA, JAWS,
Narrator, ZoomText, System Access, ZDSR, BoyPC Reader, PC-Talker and Sense
Reader on Windows, VoiceOver on Mac, and Orca on Linux. When no screen reader
is running, the mod uses the system voice, and it switches to your screen
reader within a few seconds of you starting one.

## Requirements

- Life is Strange: Before the Storm (original, Unity 5.5 build). Tested on the
  GOG Windows version; Steam's Windows version should also work.
- Windows 10 or later, 64-bit. Mac and Linux are experimental; see below.
- A screen reader (NVDA recommended), or the mod will use the system voice.

## Installation (Windows)

Use the "-windows" zip.

1. Extract this archive into the game's install folder (the folder containing
   "Life is Strange - Before the Storm.exe") so that the "BepInEx" folder,
   "winhttp.dll" and "doorstop_config.ini" all sit next to the game's exe.
2. Start the game. You should hear "Before the Storm Access loaded."
3. Press F1 at any time for the key list.

Upgrading from version 0.8 or earlier: "Tolk.dll" and
"nvdaControllerClient64.dll" in the game folder are no longer used and can be
deleted.

## Mac and Linux (experimental, untested)

The game's Mac and Linux versions are Feral Interactive's 2018 ports, sold on
Steam. The mod has never been run on them. Its speech and loader parts are
built for both systems. But the mod finds the game's menus, dialogue and
objects by internal names that were only checked against the Windows version.
If Feral's build uses different names, the mod may load and speak "Before the
Storm Access loaded" but stay silent in the game. Reports are very welcome,
with BepInEx/LogOutput.log attached.

- Use the "-macos" or "-linux" zip. The Mac App Store version is sandboxed and
  cannot load mods; use the Steam version.
- Extract the zip into the game's folder: the one that contains the .app on
  Mac, or the game's executable on Linux. In Steam, use Manage, Browse local
  files.
- Make the launcher executable: in a terminal in that folder, run
  `chmod +x run_bepinex.sh`
- Start the game through the launcher. The easiest way is a Steam launch
  option (game Properties, Launch Options), with the real path to the folder:
  `"/path/to/game/folder/run_bepinex.sh" %command%`
  Alternatively, open run_bepinex.sh in a text editor, set
  executable_name to the .app folder name (Mac) or the game's executable name
  (Linux), and run it directly.
- Mac only: macOS may block the downloaded libraries. In a terminal in the
  game folder, run `xattr -dr com.apple.quarantine .` before the first start.
  Speech goes through VoiceOver when it is on, otherwise through the system
  voice. VoiceOver cannot be interrupted by the mod, so new announcements wait
  for the current one to finish.
- Linux only: the speech library needs glibmm 2.68 and libsigc++ 3. Install
  them with your package manager: on Debian and Ubuntu, `libglibmm-2.68-1`
  (named `libglibmm-2.68-1t64` on newer releases); on Fedora, `glibmm2.68`; on
  Arch, `glibmm-2.68`. Speech goes through Orca, or through speech-dispatcher
  if Orca isn't running.
- Linux players running the Windows version through Proton should use the
  "-windows" zip. That setup is untested too.

## Keys

- F1: help — full key list and how interaction works
- Grave accent (backtick): repeat the last announcement
- Right bracket: next interactive object, nearest first
- Left bracket: previous interactive object
- Backslash: repeat the selected object with fresh distance and direction
- N: spoken walking route to the selected object
- Shift+N: toggle continuous guidance (spoken beacon with clock directions)
- O: current objective
- Shift+O: current location
- F9: toggle debug logging
- F10: write a diagnostic snapshot to BepInEx\LogOutput.log

Everything else is automatic: dialogue lines, subtitles, menu focus, settings
values, window text, dialogue choices with their directions, interactive
object announcements with the actions they offer, new objectives, and
backtalk challenge scoring.

## Directions and distances

Directions are clock positions relative to where Chloe faces: 12 is straight
ahead, 3 is right, 6 is behind, 9 is left. Distances are meters. During
continuous guidance, keep the calls at 12 o'clock to stay on course.

Interaction is hold-based: hold the interact key (usually left mouse button
or E), then press the announced direction key (W, D, S, or A) to pick an
action. The mod reminds you of this the first few times each session.

## Settings

Edit BepInEx\config\com.amock.beforethestormaccess.cfg (created after first
run) while the game is closed. Verbosity options cover window name
announcements, window text reading, out-of-range announcements, interact
hints, and automatic objective announcements; navigation options cover the
guidance progress interval.

## Translations

The mod speaks in the game's text language (Settings, Text language) and
switches when you change it. All nine of the game's languages are included:
English, German, Mexican Spanish, Spanish, French, Italian, Brazilian
Portuguese, Simplified Chinese and Japanese.

Where the game already has a word, such as key names, window titles, or its
names for Backtalk and Choices, the mod uses the game's own translation, so
speech matches what is on screen. The mod's own sentences live in
BepInEx\plugins\BeforeTheStormAccess.strings.{language}.txt files, which you
can edit. To improve a translation, change the text after the equals sign.
Placeholders like {0} must remain, and any line you remove falls back to
English. A complete, current list of English strings is written to
BeforeTheStormAccess.strings.template.txt in the same folder each time the
game starts. Developers can run scripts\Check-Translations.ps1 to check every
translation file for missing keys or broken placeholders.

## Troubleshooting

- No speech at all: open BepInEx\LogOutput.log and look for lines starting
  with "Prism:". "speaking through" names the screen reader or voice in use.
  "is missing" or "could not load" means the speech library (prism.dll on
  Windows, libprism.dylib on Mac, libprism.so on Linux) isn't in
  BepInEx\plugins or can't start; the line says why.
- Game does not load the mod: check that winhttp.dll and doorstop_config.ini
  are in the game folder and that BepInEx\core exists.
- Anything else: press F9 (debug logging), reproduce the issue, press F10,
  and share BepInEx\LogOutput.log.

## Credits

Built with BepInEx 5, HarmonyX, and Prism.

## License

GPL-3.0. See LICENSE for the full text, and THIRD_PARTY_NOTICES.txt for the
licenses of the bundled components (BepInEx, HarmonyX/MonoMod/Cecil, Prism).
Prism's own notice and license texts are in PRISM_NOTICE.txt and the
PRISM_LICENSES folder.

This mod is an unofficial, fan-made accessibility project. It is not affiliated
with or endorsed by Deck Nine Games or Square Enix. It contains no game assets
or game code; you must own a copy of the game to use it.
