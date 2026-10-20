# Test log

Last updated: 2026-10-20

## Automated tests

Command:

```powershell
.\build.ps1
.\UltronTests.exe
```

Result: **14 passed, 0 failed, 0 skipped.**

On this machine Smart App Control (Device Guard) blocks the unsigned console test exe. The same harness is also built as `UltronTestsWin.exe`; this run was captured with:

```powershell
Start-Process .\UltronTestsWin.exe -ArgumentList '--out','tests-output.txt' -Wait
Get-Content .\tests-output.txt -Encoding UTF8
```

Covered:

- Permission gate rejects unapproved sessions.
- Secret redaction.
- Ollama NDJSON parsing.
- Conversation history limit.
- Settings round-trip, including speech engine and push-to-talk.
- Settings migration: old confidence floors below 0.60 are raised; new installs default to Windows Speech and push-to-talk.
- Noise filter accepts real commands/questions and rejects filler.
- Windows Speech recognizer support probe.
- Discord boundary.
- SAPI recognizer enumeration.
- Offline SAPI recognition from WAV.
- SAPI TTS.
- Edge neural TTS (network).
- Live local Ollama chat.

The Edge and live Ollama tests depend on the local services/network; they are not mocked.

## Manual checks on this machine

| Check | Result |
|---|---|
| App launches at physical 1180×760 on a 120-DPI display | Pass |
| Session starts locked; permission dialog appears | Pass |
| Approval requires the **Allow session** button | Pass; log shows `Dialog result: OK, approved by button: True` |
| After approval with push-to-talk on, microphone stays closed | Pass; sidebar returned to “Ready. Press Talk…” with no listening log |
| **Talk** opens the mic and **Stop mic** closes it | Pass |
| Windows Speech privacy failure is visible and falls back to SAPI | Pass; message explains the Windows setting and the chip changes to `SAPI fallback` |
| Fallback does not silently reopen the mic | Pass; after the failure the app returns to Idle until Talk is pressed again |
| SAPI one-shot mode closes after a low-confidence result | Pass; “Le” at 40% was ignored and the app returned to Idle without staying open |
| Settings dialog fits at 120 DPI | Pass; screenshot in `docs/screenshots/ultron-settings.png` |
| Settings exposes engine, push-to-talk, confidence, Windows privacy button | Pass |

## Measured SAPI accuracy (the problem)

Test phrase: “Ultron, what is seventeen times twenty three?”

SAPI/WAV result:

```text
heard: "I'll try and play 17 times 23" (33.6%)
```

This is the exact failure mode the user reported: the offline recognizer invents a sentence from room audio and assigns it low confidence. The app now rejects it instead of sending it, but it cannot make SAPI accurate. Windows Speech is the default for that reason.

## Pending

- **Real Windows Speech recognition test.** Blocked until Windows' **Online speech recognition** privacy setting is enabled. The app opens `ms-settings:privacy-speech` from Settings but does not change the toggle itself.
- After enabling it, test a real phrase with **Talk**, confirm the transcript/confidence, and compare with the SAPI baseline above.
- Confirm that Windows Speech one-shot mode returns to Idle after a timeout/rejection and that the mic indicator is released.
