using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;

namespace Ultron.Core
{
    /// <summary>
    /// User-editable settings. Stored as JSON under %APPDATA%\Ultron.
    /// Secrets are never stored here; provider keys come from the environment.
    /// </summary>
    public class AppSettings
    {
        public string OllamaUrl { get; set; }
        public string Model { get; set; }
        public string Voice { get; set; }
        public int RatePercent { get; set; }
        public double ConfidenceThreshold { get; set; }
        public bool ReviewLowConfidence { get; set; }
        public string SpeechEngine { get; set; }
        public bool PushToTalk { get; set; }
        public int MaxHistory { get; set; }
        public string SystemPrompt { get; set; }

        public AppSettings()
        {
            OllamaUrl = "http://127.0.0.1:11434";
            Model = "qwen3:0.6b";
            Voice = "en-US-AvaMultilingualNeural";
            RatePercent = 8;
            ConfidenceThreshold = 0.70;
            ReviewLowConfidence = true;
            SpeechEngine = "windows";
            PushToTalk = true;
            MaxHistory = 12;
            SystemPrompt =
                "You are Ultron, a precise, local-first AI voice assistant. " +
                "Speak in short, clear sentences. Never claim to be human. " +
                "Never claim access to Discord calls, messages, or private data. " +
                "If you are unsure, say you are unsure. Do not invent facts. " +
                "The user must explicitly approve every listening session; if approval is revoked, stop immediately.";
        }

        public static string SettingsDirectory
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Ultron");
            }
        }

        public static string SettingsPath
        {
            get { return Path.Combine(SettingsDirectory, "settings.json"); }
        }

        public static AppSettings Load()
        {
            return LoadFrom(SettingsPath);
        }

        public static AppSettings LoadFrom(string path)
        {
            try
            {
                if (!File.Exists(path))
                {
                    return new AppSettings();
                }

                string json = File.ReadAllText(path);
                JavaScriptSerializer serializer = JsonUtil.CreateSerializer();
                AppSettings loaded = serializer.Deserialize<AppSettings>(json);
                if (loaded == null)
                {
                    return new AppSettings();
                }

                loaded.Normalize();
                // Older builds shipped a 0.45 floor, which let SAPI send noise
                // phrases. Raise it once so existing installs get the fix too.
                if (loaded.ConfidenceThreshold < 0.60)
                {
                    loaded.ConfidenceThreshold = 0.70;
                }
                return loaded;
            }
            catch
            {
                // A corrupt settings file must never prevent the app from starting.
                return new AppSettings();
            }
        }

        public void Save()
        {
            SaveTo(SettingsPath);
        }

        public void SaveTo(string path)
        {
            Normalize();
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
            JavaScriptSerializer serializer = JsonUtil.CreateSerializer();
            string json = serializer.Serialize(this);

            string temp = path + ".tmp";
            File.WriteAllText(temp, json);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
            File.Move(temp, path);
        }

        public void Normalize()
        {
            if (string.IsNullOrWhiteSpace(OllamaUrl))
            {
                OllamaUrl = "http://127.0.0.1:11434";
            }
            OllamaUrl = OllamaUrl.Trim().TrimEnd('/');

            if (string.IsNullOrWhiteSpace(Model))
            {
                Model = "qwen3:0.6b";
            }
            Model = Model.Trim();

            if (string.IsNullOrWhiteSpace(Voice))
            {
                Voice = "en-US-AvaMultilingualNeural";
            }
            Voice = Voice.Trim();

            if (string.IsNullOrWhiteSpace(SystemPrompt))
            {
                SystemPrompt = new AppSettings().SystemPrompt;
            }

            RatePercent = Math.Max(-50, Math.Min(50, RatePercent));
            ConfidenceThreshold = Math.Max(0.10, Math.Min(0.95, ConfidenceThreshold));
            if (string.IsNullOrWhiteSpace(SpeechEngine))
            {
                SpeechEngine = "windows";
            }
            SpeechEngine = SpeechEngine.Trim().ToLowerInvariant();
            if (SpeechEngine != "windows" && SpeechEngine != "sapi")
            {
                SpeechEngine = "windows";
            }
            MaxHistory = Math.Max(2, Math.Min(40, MaxHistory));
        }

        public List<string> Validate()
        {
            List<string> errors = new List<string>();

            Uri uri;
            if (!Uri.TryCreate(OllamaUrl, UriKind.Absolute, out uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                errors.Add("Ollama URL must be an absolute http:// or https:// URL.");
            }

            if (string.IsNullOrWhiteSpace(Model))
            {
                errors.Add("Model name is required.");
            }

            if (string.IsNullOrWhiteSpace(Voice))
            {
                errors.Add("Voice is required.");
            }

            if (RatePercent < -50 || RatePercent > 50)
            {
                errors.Add("Rate must be between -50% and +50%.");
            }

            if (ConfidenceThreshold < 0.10 || ConfidenceThreshold > 0.95)
            {
                errors.Add("Confidence threshold must be between 0.10 and 0.95.");
            }

            if (MaxHistory < 2 || MaxHistory > 40)
            {
                errors.Add("History length must be between 2 and 40 messages.");
            }

            return errors;
        }

        public AppSettings Clone()
        {
            AppSettings copy = new AppSettings();
            copy.OllamaUrl = OllamaUrl;
            copy.Model = Model;
            copy.Voice = Voice;
            copy.RatePercent = RatePercent;
            copy.ConfidenceThreshold = ConfidenceThreshold;
            copy.ReviewLowConfidence = ReviewLowConfidence;
            copy.SpeechEngine = SpeechEngine;
            copy.PushToTalk = PushToTalk;
            copy.MaxHistory = MaxHistory;
            copy.SystemPrompt = SystemPrompt;
            return copy;
        }
    }
}
