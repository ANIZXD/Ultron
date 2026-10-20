using System;
using System.Collections.Generic;

namespace Ultron.Core
{
    /// <summary>
    /// Rejects the short, filler-only phrases that dictation engines emit when
    /// they hear a door, a keyboard, or a television instead of a person.
    /// This is intentionally conservative: real commands such as "stop",
    /// "mute", "cancel", "yes", and "no" are never filtered.
    /// </summary>
    public static class NoiseFilter
    {
        private static readonly HashSet<string> FillerWords = new HashSet<string>(
            new string[]
            {
                "a", "ah", "an", "and", "at", "but", "eh", "er", "hello", "hey",
                "hmm", "huh", "i", "in", "is", "it", "like", "mm", "oh", "ok",
                "okay", "on", "or", "right", "so", "that", "the", "this", "to",
                "uh", "um", "well", "what", "yeah", "yep", "you"
            },
            StringComparer.OrdinalIgnoreCase);

        public static bool IsLikelyNoise(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return true;
            }

            string cleaned = text.Trim().Trim('.', ',', '!', '?', ';', ':', '"', '\'');
            if (cleaned.Length < 2)
            {
                return true;
            }

            string[] words = cleaned.Split(new char[] { ' ', '\t', '\r', '\n' },
                StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0)
            {
                return true;
            }

            bool allFiller = true;
            for (int i = 0; i < words.Length; i++)
            {
                string word = words[i].Trim('.', ',', '!', '?', ';', ':', '"', '\'');
                if (!FillerWords.Contains(word))
                {
                    allFiller = false;
                    break;
                }
            }

            return allFiller;
        }
    }
}
