# Before the Storm Access

Screen reader accessibility mod for Life is Strange: Before the Storm (original,
Unity 5.5.3p1, GOG). BepInEx 5.4.23.5 plugin, net35, Prism speech output
(PrismSpeech.cs; fetch binaries with `scripts\Get-Prism.ps1`). Mac/Linux zips
are experimental: never run, Feral build's class names unverified.

## User

- Blind screen reader user (NVDA). User directs, assistant codes and explains.
- Output formatting: NO markdown tables — use lists.

## Session start

Read `project_status.md` first: current phase, last work, pending tests. If pending
tests exist, ask for results before continuing. Update it on significant progress
and before session end.

## Key facts

- Game dir: `D:\Root\Life is Strange - Before the Storm`
- Build: `dotnet build -c Release` (copies plugin DLL into the game automatically)
- Game classes are obfuscated at class-name level only; methods/fields readable.
  Verify names in `decompiled\`, record mappings in `GameRefs.cs` + `docs\game-api.md`.
  Obfuscated `T_XXXXXXXX` names may appear ONLY in those two files.
- UI is a renamed NGUI; dialogue engine is "StoryTeller"; audio CriWare.
- net35 restrictions apply (no Application.quitting, monolithic UnityEngine.dll).
- All screen reader strings go through `Loc.Get()`.
- The Remastered edition (Unity 2019.4) is a separate target; not installed, not covered.
- Reference implementation with mature patterns: `C:\Users\amock\mod template`
  (Date Everything Access) and its `docs\` guides.

## Coding rules

- Handler classes `[Feature]Handler`; private fields `_camelCase`; XML docs on
  public members; logs and comments in English.
- Null-safety with logging — never fail silently. try/catch only around native speech (Prism),
  reflection, and unstable game APIs.
- Respect game keys: check `eGameInput` bindings before claiming a key; avoid
  the game's debug keys (kDebugDisplay, kSTDebugDisplay, kToggleFPSDisplay).
