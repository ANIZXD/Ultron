using System;
using System.Text.RegularExpressions;

namespace Ultron.Core
{
    /// <summary>
    /// Best-effort secret redaction for logs and error messages.
    /// The app never writes conversation text to disk; this is a second line of defense.
    /// </summary>
    public static class Redaction
    {
        private static readonly Regex DiscordToken = new Regex(
            @"(?<![A-Za-z0-9_-])[A-Za-z0-9_-]{23,28}\.[A-Za-z0-9_-]{6,7}\.[A-Za-z0-9_-]{27,40}(?![A-Za-z0-9_-])",
            RegexOptions.Compiled);

        private static readonly Regex OpenAiKey = new Regex(
            @"\bsk-[A-Za-z0-9_-]{16,}\b",
            RegexOptions.Compiled);

        private static readonly Regex BearerToken = new Regex(
            @"(?i)\bBearer\s+[A-Za-z0-9\-\._~\+/]{12,}=*",
            RegexOptions.Compiled);

        private static readonly Regex PrivateKeyBlock = new Regex(
            @"-----BEGIN [A-Z ]*PRIVATE KEY-----.*?-----END [A-Z ]*PRIVATE KEY-----",
            RegexOptions.Compiled | RegexOptions.Singleline);

        private static readonly Regex KeyValueSecret = new Regex(
            @"(?i)\b(api[_-]?key|access[_-]?token|refresh[_-]?token|bot[_-]?token|token|secret|password)\b\s*[:=]\s*[""']?[^\s""',;]{3,}",
            RegexOptions.Compiled);

        public static string Redact(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return input;
            }

            string output = input;
            output = PrivateKeyBlock.Replace(output, "[REDACTED PRIVATE KEY]");
            output = DiscordToken.Replace(output, "[REDACTED DISCORD TOKEN]");
            output = OpenAiKey.Replace(output, "[REDACTED API KEY]");
            output = BearerToken.Replace(output, "Bearer [REDACTED]");
            output = KeyValueSecret.Replace(output, "$1=[REDACTED]");
            return output;
        }
    }
}
