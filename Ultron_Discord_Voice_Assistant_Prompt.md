# Project Ultron --- Real-Time AI Voice Assistant for Discord

You are a senior AI engineer, Python developer, real-time audio
engineer, and desktop application developer.

Build a polished Windows desktop application called **Ultron**. Its
long-term goal is to act as an AI voice assistant that can speak on my
behalf during private Discord DM calls, but ONLY after I explicitly
approve each call.

## IMPORTANT: Start by inspecting the environment

Before coding: 1. Inspect the existing project directory and understand
its framework, dependencies, and conventions. 2. Check the installed
Python and Node.js versions, available audio devices, and existing
packages. 3. Determine which AI models, API providers, and speech
services are already configured. Reuse existing working configuration
where appropriate. 4. Investigate Discord's current official developer
documentation for supported private DM-call audio access and bot
participation. 5. Do not assume Discord's public APIs support private
DM-call integration. Do not implement Discord self-bots, user-token
automation, credential extraction, or prohibited account automation. 6.
Produce a short implementation plan and identify blockers before making
architectural decisions.

## Phase 1 --- Build a working AI voice assistant

Create a desktop application with a modern, premium, dark UI.

### Core functionality

-   Microphone input with visible listening status.
-   Speech-to-text transcription.
-   LLM-powered conversational responses.
-   Natural text-to-speech output with a configurable voice.
-   Low-latency conversational flow.
-   Conversation transcript and activity log.
-   Interrupt button to immediately stop Ultron's speech.
-   Mute microphone and stop-session controls.
-   Configurable AI provider and model.
-   Secure API-key handling using environment variables or an
    appropriate local secret store. Never hardcode secrets.

### Permission-first design

-   Ultron must never speak on my behalf in a real call without explicit
    approval.
-   Display an incoming-call approval interface only if a supported
    integration can reliably detect the call.
-   Include Approve, Decline, Stop, and Take Over controls.
-   Make the active microphone, speaker output, and AI speaking status
    visible.
-   If approval expires or permissions are revoked, stop AI speech
    immediately.
-   Clearly identify Ultron as an AI assistant where appropriate; never
    impersonate a human deceptively.

## Phase 2 --- Discord integration feasibility

Investigate whether an officially supported integration can access audio
from ordinary private Discord DM calls.

-   Verify current Discord API capabilities from official documentation.
-   Separate verified capabilities from assumptions.
-   Do not claim that a bot can join a private DM call unless verified.
-   Do not use self-bots, personal account tokens, reverse-engineered
    private endpoints, or unauthorized call-audio interception.
-   If private DM calls cannot be accessed through supported interfaces,
    document the limitation and provide the closest legitimate
    alternative.
-   Do not silently substitute server voice channels for private DM
    calls. Treat them as a separate optional integration.

## Phase 3 --- Reliable audio architecture

Design modular components: - Audio capture and device management. -
Voice activity detection. - Speech recognition. - LLM conversation
engine. - Text-to-speech. - Audio playback and interruption. -
Permission and session manager. - Optional Discord adapter, only if a
supported integration exists.

Prefer streaming audio and responses when supported. Handle microphone
errors, network interruptions, API failures, duplicate audio, and
cancellation cleanly.

## Phase 4 --- Testing

Create tests for: - Speech recognition and response generation. - Audio
playback and interruption. - Permission approval and rejection. -
Immediate shutdown when Take Over is pressed. - API failures and
reconnect behavior. - Secret handling and logging. - Discord integration
boundaries.

Run the tests and report actual results. Do not claim a feature works
unless it has been tested.

## Development rules

-   Build incrementally and keep the application runnable.
-   Choose a lightweight stack appropriate to the existing environment.
-   Explain important architectural decisions in plain English.
-   Avoid unnecessary dependencies and paid services unless needed.
-   Never expose API keys or private conversation data in logs.
-   Do not modify unrelated files.
-   Document installation and startup commands for Windows.

## First deliverable

Start with a working desktop app that lets me talk to Ultron, hear its
AI-generated voice, see the transcript, and interrupt it instantly.
Include a clear status panel showing which parts are implemented,
tested, or blocked.

After that works, investigate the legitimate Discord private-call
integration path. Do not fake the integration or present a simulated
call as a real Discord connection.
