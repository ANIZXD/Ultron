# Ultron — Implementation Plan

## Environment findings (verified 2026-10-20)

| Item | Status |
|---|---|
| OS | Windows 11 Pro x64, PowerShell 5.1 |
| Node.js | v24.21.0 / npm 11.19.0 (native `.ts` via type stripping) |
| **Python** | **NOT INSTALLED** — `python` is only a Store stub. No pip. |
| uv 0.13.1 | present at `~/.local/bin`, provisions CPython 3.12 — could bootstrap Python but adds weight |
| Ollama 0.40.6 | **running on 127.0.0.1:11434**, model `qwen3:0.6b` (522 MB) — the only model on disk |
| Audio hardware | 1 mic (`Microphone Array - Intel Smart Sound`), 1 speaker (`Speaker - Realtek`) |
| WinForms (`System.Windows.Forms`) | **loadable in-process** — native Windows desktop UI, zero install |
| SAPI (`System.Speech`) | **loadable** — offline speech-to-text *and* text-to-speech |
| Existing Discord voice libs | `C:\Users\ay660\discord-tts-bot\node_modules` has `@discordjs/voice 0.19.2`, `discord.js 14.27`, `opusscript`, `ffmpeg-static`, **`msedge-ttts 2.1.0`** — reusable |
| Network | pypi/ollama.com/discord.com → 200, but large downloads run 10–16 KB/s |

## Architecture decision

**Node 24 + WinForms + SAPI + Ollama + msedge-tts. No Python, no Electron, no paid services.**

Rationale:
- Python is absent and installing it (or uv-managed CPython) is avoidable weight for a desktop shell.
- WinForms is a native, lightweight Windows desktop UI already on the box — no bundler, no Chromium.
- SAPI gives offline STT/TTS with zero dependencies; msedge-tts (already installed) gives the better neural voice for TTS.
- Ollama `qwen3:0.6b` is the local LLM; an OpenAI-compatible cloud provider can be plugged in later via one adapter.

## Modular components

1. `audio/capture.js` — microphone enumeration + capture (SAPI recognition stream + raw PCM for VAD)
2. `audio/vad.js` — voice activity detection (energy-based + SAPI confidence floor)
3. `stt/sapi.js` — speech-to-text via `System.Speech.Recognition`
4. `llm/` — conversation engine: `ollama.js` adapter + `engine.js` (streaming, transcript, stop-sequences)
5. `tts/` — `msedge-tts.js` (neural) with `sapi.js` fallback
6. `audio/playback.js` — WAV/PCM playback with **immediate interrupt**
7. `permission/session.js` — approval gates, session lifecycle, Take Over
8. `discord/adapter.js` — **optional**, only if a supported integration exists (see Phase 2)
9. `ui/` — WinForms UI (`Form1.cs`-equivalent via .NET WinForms, driven from Node through `System.Windows.Forms`)
10. `core/app.js` — orchestrator wiring everything together

## Phase 2 — Discord private DM call: VERIFIED BLOCKER

Discord's official voice docs (`docs.discord.com/developers/topics/voice-connections`) show:
- Voice connections require a **`guild_id` + `channel_id`** in the Voice State Update. There is **no documented bot path to a 1:1 DM call**.
- **As of 2026-03-01 all DM / GDM / voice-channel audio is end-to-end encrypted (DAVE).** A bot would need a full MLS/E2EE implementation (`libdave`) to decrypt audio even if it could join.
- The community request "Allow bots to join and start group/non-group DM calls" is archived and unsupported.

**Conclusion: private DM call audio is NOT accessible through supported interfaces.** This is documented, not faked. The legitimate alternative — **guild voice channels** — is implemented as a separate optional adapter (the existing `discord-tts-bot` already proves it works). Server VCs are *not* presented as private DM calls.

## Secrets

API keys come from environment variables only (`process.env.*`), never hardcoded, never logged. The app reads `OPENAI_API_KEY`, `OPENAI_BASE_URL`, `ANTHROPIC_API_KEY`, `ELEVEN_API_KEY`, `DISCORD_TOKEN` if present.

## Test plan

`test/` covers: STT + response generation, playback + interrupt, approve/decline, Take Over shutdown, API failure/reconnect, secret handling/logging, Discord boundary (assertion that DM calls are unsupported).