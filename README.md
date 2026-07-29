# Before the Storm Access

A screen reader accessibility mod for Life is Strange: Before the Storm
(original edition, not the Remastered version). Speaks dialogue, menus,
choices, backtalk challenges, interactive objects, and provides walking
navigation using the game's own pathfinding.

Works with NVDA, JAWS, and other screen readers supported by Tolk. Falls
back to the Windows SAPI voice when no screen reader is running.

## Requirements

- Life is Strange: Before the Storm (original, Unity 5.5 build; GOG and Steam
  should both work)
- Windows, 64-bit
- A screen reader (NVDA recommended), or the mod will use the Windows voice

## Installation

1. Extract this archive into the game's install folder (the folder containing
   "Life is Strange - Before the Storm.exe") so that the "BepInEx" folder,
   "winhttp.dll", "doorstop_config.ini", "Tolk.dll", and
   "nvdaControllerClient64.dll" all sit next to the game's exe.
2. Start the game. You should hear "Before the Storm Access loaded."
3. Press F1 at any time for the key list.

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

The mod speaks the game's text language when a translation file exists. Copy
BepInEx\plugins\BeforeTheStormAccess.strings.template.txt to
BeforeTheStormAccess.strings.de.txt (or .fr/.es/.es-mx/.it/.pt/.zh-hans/.ja)
in the same folder and translate the values after the equals signs.
Placeholders like {0} must remain. Missing keys fall back to English.

## Troubleshooting

- No speech at all: check that Tolk.dll and nvdaControllerClient64.dll are in
  the game folder, next to the exe.
- Game does not load the mod: check that winhttp.dll and doorstop_config.ini
  are in the game folder and that BepInEx\core exists.
- Anything else: press F9 (debug logging), reproduce the issue, press F10,
  and share BepInEx\LogOutput.log.

## Credits

Built with BepInEx 5, HarmonyX, and Tolk.

## License

GPL-3.0. See LICENSE for the full text, and THIRD_PARTY_NOTICES.txt for the
licenses of the bundled components (BepInEx, HarmonyX/MonoMod/Cecil, Tolk,
NVDA controller client).

This mod is an unofficial, fan-made accessibility project. It is not affiliated
with or endorsed by Deck Nine Games or Square Enix. It contains no game assets
or game code; you must own a copy of the game to use it.
