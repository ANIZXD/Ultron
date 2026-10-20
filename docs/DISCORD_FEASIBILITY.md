# Discord feasibility

**Status: private 1:1 Discord DM calls are not supported. Ultron does not implement them.**

This note records what Discord's official documentation and policy allow, and why Ultron refuses to present a workaround as if it were a private call.

## The hard boundary

Discord's documented voice API is built around **guild voice channels**. The voice-state update that starts a voice connection uses a **`guild_id`** and a **`channel_id`**. There is no documented bot path to an ordinary 1:1 DM call.

Discord also rolled out **end-to-end encryption (DAVE)** for voice and video calls, including DM and group-DM calls. A bot cannot simply join and decode that audio; it would need a compatible end-to-end encryption implementation and the consent of every participant. Ultron has neither, and does not pretend otherwise.

Sources:

- Discord voice connections: <https://docs.discord.com/developers/topics/voice-connections>
- Discord's end-to-end encryption announcement: <https://discord.com/blog/every-voice-and-video-call-on-discord-is-now-end-to-end-encrypted>
- DAVE protocol: <https://daveprotocol.com>

## What Ultron will not do

- **No self-bots.** Ultron will not log in as your personal account, reuse a user token, or drive an unofficial client to join a call.
- **No fake incoming-call UI.** It will not show a “Discord call” banner or claim a call is ringing when no supported API can detect one.
- **No DM audio interception.** It will not capture, transcribe, or speak into a private DM call.
- **No silent substitution.** A server voice channel is not a private DM call. If a guild voice-channel integration is ever added, the UI must label it as a server channel and require explicit approval.

## What is possible (but not enabled in this build)

A **guild voice channel** integration is legitimate: a bot can join a server voice channel with the documented API, subject to the bot's permissions and Discord's policies. It would still require:

1. A Discord application and bot token, stored outside the repository.
2. The **Message Content Intent** if the bot reads text commands.
3. Explicit per-channel and per-session user approval.
4. A clear UI label that this is a **server voice channel**, not a private DM call.

Ultron ships with no Discord connection at all. The `DiscordAdapter` class only documents the boundary; it performs no network calls.

## Security notes

- Never put a Discord bot token or user token in this repository, a screenshot, or a log.
- A bot token pasted into chat should be rotated in the Discord Developer Portal.
- If you build a separate Discord bot, keep `.env` out of version control and use the smallest permission set that works.

## Related

- `src/Core/DiscordAdapter.cs` — the in-app capability description.
- `README.md` — the app's privacy and permission model.
