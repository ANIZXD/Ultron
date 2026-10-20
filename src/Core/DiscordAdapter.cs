using System;
using System.Collections.Generic;

namespace Ultron.Core
{
    public class DiscordCapability
    {
        public string Name { get; set; }
        public string Status { get; set; }
        public string Detail { get; set; }

        public DiscordCapability(string name, string status, string detail)
        {
            Name = name;
            Status = status;
            Detail = detail;
        }
    }

    /// <summary>
    /// Documents the verified boundary. This class performs no network calls and
    /// deliberately does not implement self-bots or DM-call interception.
    /// </summary>
    public static class DiscordAdapter
    {
        public const string PrivateDmStatus = "UNSUPPORTED";

        public static string Summary
        {
            get
            {
                return "Private 1:1 Discord DM calls are not supported by Discord's documented bot voice API. " +
                       "Ultron therefore does not join, listen to, or speak in DM calls. " +
                       "Guild voice channels are a separate, legitimate integration and are not enabled in this build.";
            }
        }

        public static List<DiscordCapability> Capabilities()
        {
            List<DiscordCapability> list = new List<DiscordCapability>();
            list.Add(new DiscordCapability(
                "Private 1:1 DM call audio",
                "UNSUPPORTED",
                "The documented voice API uses a guild_id and channel_id. No supported bot path to an ordinary " +
                "1:1 DM call is documented. All Discord calls now require DAVE end-to-end encryption."));
            list.Add(new DiscordCapability(
                "Incoming DM-call detection",
                "UNSUPPORTED",
                "No supported API exposes incoming private calls to bots. Ultron does not display a fake call UI."));
            list.Add(new DiscordCapability(
                "Guild voice channel",
                "NOT ENABLED",
                "A legitimate alternative, but it is a server voice channel, not a private DM call. " +
                "It would require a separate adapter, bot token, and explicit user approval."));
            list.Add(new DiscordCapability(
                "Self-bot / user-token automation",
                "PROHIBITED",
                "Ultron does not use personal account tokens, reverse-engineered endpoints, or unauthorized " +
                "audio interception."));
            return list;
        }

        public static List<string> Sources()
        {
            List<string> sources = new List<string>();
            sources.Add("https://docs.discord.com/developers/topics/voice-connections");
            sources.Add("https://discord.com/blog/every-voice-and-video-call-on-discord-is-now-end-to-end-encrypted");
            sources.Add("https://daveprotocol.com");
            return sources;
        }
    }
}
