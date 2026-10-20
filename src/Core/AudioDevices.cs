using System;
using System.Collections.Generic;
using System.Management;

namespace Ultron.Core
{
    public static class AudioDevices
    {
        public static string DefaultInputName()
        {
            string input;
            string output;
            GetDefaultNames(out input, out output);
            return input;
        }

        public static string DefaultOutputName()
        {
            string input;
            string output;
            GetDefaultNames(out input, out output);
            return output;
        }

        public static List<string> GetAudioEndpoints()
        {
            List<string> names = new List<string>();
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(
                    "SELECT Name, Status FROM Win32_PnPEntity WHERE PNPClass = 'AudioEndpoint'"))
                {
                    foreach (ManagementObject item in searcher.Get())
                    {
                        string name = Convert.ToString(item["Name"]);
                        string status = Convert.ToString(item["Status"]);
                        if (!string.IsNullOrWhiteSpace(name))
                        {
                            names.Add(name + (string.IsNullOrWhiteSpace(status) ? string.Empty : "  [" + status + "]"));
                        }
                    }
                }
            }
            catch
            {
                // WMI may be unavailable in locked-down environments.
            }

            if (names.Count == 0)
            {
                try
                {
                    using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(
                        "SELECT Name FROM Win32_SoundDevice"))
                    {
                        foreach (ManagementObject item in searcher.Get())
                        {
                            string name = Convert.ToString(item["Name"]);
                            if (!string.IsNullOrWhiteSpace(name))
                            {
                                names.Add(name);
                            }
                        }
                    }
                }
                catch
                {
                    // Ignore.
                }
            }
            return names;
        }

        private static void GetDefaultNames(out string input, out string output)
        {
            input = "Default microphone";
            output = "Default speakers";

            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(
                    "SELECT Name FROM Win32_PnPEntity WHERE PNPClass = 'AudioEndpoint'"))
                {
                    foreach (ManagementObject item in searcher.Get())
                    {
                        string name = Convert.ToString(item["Name"]);
                        if (string.IsNullOrWhiteSpace(name))
                        {
                            continue;
                        }

                        string lower = name.ToLowerInvariant();
                        if (lower.Contains("microphone") || lower.Contains("mic"))
                        {
                            input = name;
                        }
                        if (lower.Contains("speaker") || lower.Contains("headphone") || lower.Contains("headset"))
                        {
                            output = name;
                        }
                    }
                }
            }
            catch
            {
                // Keep defaults.
            }
        }
    }
}
