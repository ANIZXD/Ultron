using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Speech.Synthesis;
using System.Text;
using System.Threading;
using Ultron.Core;

namespace Ultron.Tests
{
    internal class SkipException : Exception
    {
        public SkipException(string message) : base(message) { }
    }

    internal static class TestProgram
    {
        private static int _passed;
        private static int _failed;
        private static int _skipped;
        private static string _root;
        private static string _temp;

        private static int Main(string[] args)
        {
            StreamWriter report = null;
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], "--out", StringComparison.OrdinalIgnoreCase) &&
                    i + 1 < args.Length)
                {
                    report = new StreamWriter(args[++i], false, new UTF8Encoding(true));
                    report.AutoFlush = true;
                    Console.SetOut(report);
                }
            }

            try { Console.OutputEncoding = Encoding.UTF8; } catch { }

            _root = AppDomain.CurrentDomain.BaseDirectory;
            _temp = Path.Combine(Path.GetTempPath(), "ultron-tests");
            Directory.CreateDirectory(_temp);

            Console.WriteLine("Ultron tests - " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            Console.WriteLine("Root: " + _root);
            Console.WriteLine();

            Run("Permission gate", TestPermission);
            Run("Secret redaction", TestRedaction);
            Run("Ollama NDJSON parser", TestOllamaParser);
            Run("Conversation history", TestConversation);
            Run("Settings round-trip", TestSettings);
            Run("Settings migration", TestSettingsMigration);
            Run("Noise filter", TestNoiseFilter);
            Run("Windows Speech support", TestWindowsSpeechSupport);
            Run("Discord boundary", TestDiscordBoundary);
            Run("Speech recognizers", TestSpeechRecognizers);
            Run("STT from WAV (offline)", TestSpeechWave);
            Run("TTS SAPI (offline)", TestTtsSapi);
            Run("TTS Edge (network)", TestTtsEdge);
            Run("Ollama live (local)", TestOllamaLive);

            Console.WriteLine();
            Console.WriteLine("Result: " + _passed + " passed, " + _failed + " failed, " + _skipped + " skipped.");

            int exitCode = _failed == 0 ? 0 : 1;
            if (report != null)
            {
                report.Flush();
                report.Dispose();
            }
            return exitCode;
        }

        private static void Run(string name, Action test)
        {
            Console.Write("[" + name + "] ");
            try
            {
                test();
                _passed++;
                Console.WriteLine("PASS");
            }
            catch (SkipException exception)
            {
                _skipped++;
                Console.WriteLine("SKIP - " + exception.Message);
            }
            catch (Exception exception)
            {
                _failed++;
                Console.WriteLine("FAIL - " + exception.Message);
            }
        }

        private static void TestPermission()
        {
            PermissionManager manager = new PermissionManager();
            int changes = 0;
            manager.StateChanged += delegate { changes++; };

            AssertEqual(PermissionState.Locked, manager.State, "initial state");
            Assert(!manager.Approve(), "cannot approve while locked");
            Assert(!manager.CanSpeak, "locked cannot speak");
            Assert(manager.RequestStart(), "request start");
            AssertEqual(PermissionState.Pending, manager.State, "pending state");
            Assert(manager.Approve(), "approve pending");
            AssertEqual(PermissionState.Approved, manager.State, "approved state");
            Assert(manager.CanSpeak, "approved can speak");
            Assert(manager.Revoke(), "take over");
            AssertEqual(PermissionState.Revoked, manager.State, "revoked state");
            Assert(!manager.CanSpeak, "revoked cannot speak");
            manager.Reset();
            AssertEqual(PermissionState.Locked, manager.State, "reset state");
            AssertEqual(4, changes, "state change events");
        }

        private static void TestRedaction()
        {
            // Build the fake credentials at runtime so secret scanners do not
            // mistake this test fixture for a real token. The redaction regex
            // still receives the full token-shaped value.
            string discordToken = "MTIzNDU2Nzg5MDEyMzQ1Njc4" + "." + "GaBcDe" + "." +
                                  "abcdefghijklmnopqrstuvwxyz1234567890";
            string openAiKey = "sk-" + "abcdefghijklmnopqrstuvwxyz123456";
            string input = "token=" + discordToken + " api_key=supersecret " + openAiKey;
            string output = Redaction.Redact(input);

            Assert(!output.Contains(discordToken), "Discord token was not redacted");
            Assert(!output.Contains("supersecret"), "api_key value was not redacted");
            Assert(!output.Contains(openAiKey), "OpenAI-style key was not redacted");
            Assert(output.Contains("[REDACTED]"), "redaction marker missing");
            AssertEqual("No secret here.", Redaction.Redact("No secret here."), "normal text changed");
        }

        private static void TestOllamaParser()
        {
            OllamaChunk chunk = OllamaClient.ParseLine("{\"message\":{\"content\":\"hello\"},\"done\":false}");
            Assert(chunk != null, "chunk missing");
            AssertEqual("hello", chunk.Content, "content");
            Assert(!chunk.Done, "done flag");

            chunk = OllamaClient.ParseLine("{\"message\":{\"thinking\":\"hmm\",\"content\":\"\"},\"done\":false}");
            Assert(chunk != null, "thinking chunk missing");
            AssertEqual("hmm", chunk.Thinking, "thinking");
            Assert(string.IsNullOrEmpty(chunk.Content), "thinking must not leak into content");

            chunk = OllamaClient.ParseLine("{\"done\":true}");
            Assert(chunk != null && chunk.Done, "done chunk");

            chunk = OllamaClient.ParseLine("{\"error\":\"boom\"}");
            Assert(chunk != null, "error chunk");
            AssertEqual("boom", chunk.Error, "error text");

            Assert(OllamaClient.ParseLine("") == null, "empty line must be ignored");
            Assert(OllamaClient.ParseLine("   ") == null, "whitespace line must be ignored");
        }

        private static void TestConversation()
        {
            Conversation conversation = new Conversation(4);
            for (int i = 1; i <= 6; i++)
            {
                conversation.AddUser("m" + i);
            }

            AssertEqual(4, conversation.Count, "history cap");
            List<ChatMessage> prompt = conversation.BuildPrompt("sys");
            AssertEqual(5, prompt.Count, "system + history");
            AssertEqual("system", prompt[0].Role, "system role");
            AssertEqual("m3", prompt[1].Content, "oldest retained message");
            AssertEqual("m6", prompt[4].Content, "newest message");

            conversation.Clear();
            AssertEqual(0, conversation.Count, "clear");
        }

        private static void TestSettings()
        {
            string path = Path.Combine(_temp, "settings-test.json");
            AppSettings settings = new AppSettings();
            settings.Model = "test-model";
            settings.RatePercent = 17;
            settings.ConfidenceThreshold = 0.6;
            settings.Voice = "sapi:Microsoft Zira Desktop";
            settings.SpeechEngine = "sapi";
            settings.PushToTalk = false;
            settings.SaveTo(path);

            AppSettings loaded = AppSettings.LoadFrom(path);
            AssertEqual("test-model", loaded.Model, "model");
            AssertEqual(17, loaded.RatePercent, "rate");
            AssertEqual(0.6, loaded.ConfidenceThreshold, "confidence");
            AssertEqual("sapi:Microsoft Zira Desktop", loaded.Voice, "voice");
            AssertEqual("sapi", loaded.SpeechEngine, "speech engine");
            Assert(!loaded.PushToTalk, "push to talk");
            AssertEqual(0, loaded.Validate().Count, "defaults should validate");
            File.Delete(path);
        }

        private static void TestSettingsMigration()
        {
            string path = Path.Combine(_temp, "settings-migration.json");
            File.WriteAllText(path,
                "{\"OllamaUrl\":\"http://127.0.0.1:11434\",\"Model\":\"qwen3:0.6b\"," +
                "\"Voice\":\"en-US-AvaMultilingualNeural\",\"RatePercent\":8," +
                "\"ConfidenceThreshold\":0.45,\"ReviewLowConfidence\":true," +
                "\"MaxHistory\":12}");
            AppSettings loaded = AppSettings.LoadFrom(path);
            AssertEqual(0.70, loaded.ConfidenceThreshold, "old low floor migrated");
            AssertEqual("windows", loaded.SpeechEngine, "new engine default");
            Assert(loaded.PushToTalk, "push to talk default");
            File.Delete(path);
        }

        private static void TestNoiseFilter()
        {
            Assert(NoiseFilter.IsLikelyNoise("the"), "single filler");
            Assert(NoiseFilter.IsLikelyNoise("The hello and"), "filler phrase");
            Assert(NoiseFilter.IsLikelyNoise("um... uh..."), "punctuated filler");
            Assert(!NoiseFilter.IsLikelyNoise("stop"), "stop command");
            Assert(!NoiseFilter.IsLikelyNoise("mute the microphone"), "real command");
            Assert(!NoiseFilter.IsLikelyNoise("what is seventeen times twenty three"),
                "question must pass");
        }

        private static void TestWindowsSpeechSupport()
        {
            Assert(WindowsSpeechInput.IsSupported, "Windows Speech recognizer is not available");
            using (WindowsSpeechInput input = new WindowsSpeechInput())
            {
                Assert(input.IsAvailable, "Windows Speech reports unavailable");
                Assert(!input.IsListening, "must not listen before Start");
            }
        }

        private static void TestDiscordBoundary()
        {
            Assert(DiscordAdapter.Summary.ToLowerInvariant().Contains("not supported"),
                "summary must state the limitation");
            bool foundPrivate = false;
            foreach (DiscordCapability capability in DiscordAdapter.Capabilities())
            {
                if (capability.Name.IndexOf("Private", StringComparison.OrdinalIgnoreCase) >= 0 &&
                    capability.Status == "UNSUPPORTED")
                {
                    foundPrivate = true;
                }
            }
            Assert(foundPrivate, "private DM capability must be marked unsupported");
            Assert(DiscordAdapter.Sources().Count >= 2, "sources missing");
        }

        private static void TestSpeechRecognizers()
        {
            List<string> recognizers = SpeechInput.GetRecognizers();
            Assert(recognizers.Count > 0, "no Windows speech recognizers installed");
            Console.WriteLine();
            Console.WriteLine("      recognizers: " + string.Join("; ", recognizers.ToArray()));
            Console.Write("      ");
        }

        private static void TestSpeechWave()
        {
            string wav = Path.Combine(_temp, "stt-test.wav");
            CreateSpeechWav("Ultron, what is seventeen times twenty three?", wav);
            SpeechProbeResult result = SpeechInput.ProbeWave(wav);
            Assert(result.Success, "STT failed: " + result.Error);
            string lower = result.Text.ToLowerInvariant();
            Assert(lower.Contains("17") || lower.Contains("seventeen"),
                "did not hear the first number: " + result.Text);
            Assert(lower.Contains("23") || lower.Contains("twenty"),
                "did not hear the second number: " + result.Text);
            Assert(result.Confidence > 0.10f, "confidence too low: " + result.Confidence);
            Console.WriteLine();
            Console.WriteLine("      heard: \"" + result.Text + "\" (" + result.Confidence.ToString("P1") + ")");
            Console.Write("      ");
            File.Delete(wav);
        }

        private static void TestTtsSapi()
        {
            CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(45));
            TtsResult result = TtsHelper.SynthesizeAsync(
                "Ultron local speech test.", "sapi:Microsoft Zira Desktop", 0, _root, cts.Token)
                .GetAwaiter().GetResult();
            Assert(result.Success, "SAPI TTS failed: " + result.Error);
            Assert(File.Exists(result.WavPath), "SAPI WAV missing");
            Assert(new FileInfo(result.WavPath).Length > 1000, "SAPI WAV too small");
            AssertEqual("sapi", result.Engine, "engine");
            TtsHelper.TryDelete(result.WavPath);
        }

        private static void TestTtsEdge()
        {
            CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            TtsResult result = TtsHelper.SynthesizeAsync(
                "Ultron online. Speech systems precise.", "en-US-AvaMultilingualNeural", 8, _root, cts.Token)
                .GetAwaiter().GetResult();
            Assert(result.Success, "Edge TTS failed: " + result.Error);
            Assert(File.Exists(result.WavPath), "Edge WAV missing");
            long bytes = new FileInfo(result.WavPath).Length;
            Assert(bytes > 1000, "Edge WAV too small");
            Console.WriteLine();
            Console.WriteLine("      engine: " + result.Engine + ", wav bytes: " + bytes);
            if (result.Engine != "edge")
            {
                Console.WriteLine("      NOTE: Edge was unavailable; the offline SAPI fallback succeeded.");
            }
            Console.Write("      ");
            TtsHelper.TryDelete(result.WavPath);
        }

        private static void TestOllamaLive()
        {
            OllamaClient client = new OllamaClient("http://127.0.0.1:11434", "qwen3:0.6b");
            try
            {
                List<string> models = client.ListModelsAsync(CancellationToken.None)
                    .GetAwaiter().GetResult();
                Assert(models.Count > 0, "no Ollama models installed");

                List<ChatMessage> history = new List<ChatMessage>();
                history.Add(new ChatMessage("user", "What is 17 times 23? Reply with just the number."));
                StringBuilder deltas = new StringBuilder();
                Stopwatch stopwatch = Stopwatch.StartNew();
                string reply = client.ChatAsync(
                    "You are a precise calculator. Reply with only the number.",
                    history,
                    delegate(string delta) { deltas.Append(delta); },
                    CancellationToken.None).GetAwaiter().GetResult();
                stopwatch.Stop();

                Assert(reply.Contains("391"), "expected 391, got: " + reply);
                Assert(deltas.Length > 0, "stream callback never fired");
                Assert(stopwatch.Elapsed.TotalSeconds < 60, "response took too long");
                Console.WriteLine();
                Console.WriteLine("      reply: \"" + reply + "\" (" + stopwatch.ElapsedMilliseconds + " ms)");
                Console.Write("      ");
            }
            catch (OllamaException exception)
            {
                throw new SkipException("Ollama unavailable: " + exception.Message);
            }
            finally
            {
                client.Dispose();
            }
        }

        private static void CreateSpeechWav(string text, string path)
        {
            Exception error = null;
            Thread thread = new Thread(new ThreadStart(delegate
            {
                try
                {
                    using (SpeechSynthesizer synthesizer = new SpeechSynthesizer())
                    {
                        try { synthesizer.SelectVoice("Microsoft Zira Desktop"); } catch { }
                        synthesizer.SetOutputToWaveFile(path);
                        synthesizer.Speak(text);
                    }
                }
                catch (Exception exception)
                {
                    error = exception;
                }
            }));
            thread.IsBackground = true;
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join(TimeSpan.FromSeconds(30));
            if (error != null)
            {
                throw new Exception("Could not create test WAV: " + error.Message);
            }
            Assert(File.Exists(path), "test WAV was not created");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new Exception(message);
            }
        }

        private static void AssertEqual<T>(T expected, T actual, string message)
        {
            if (!object.Equals(expected, actual))
            {
                throw new Exception(message + " (expected " + expected + ", got " + actual + ")");
            }
        }
    }
}
