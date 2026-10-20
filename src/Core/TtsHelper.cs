using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace Ultron.Core
{
    public class TtsResult
    {
        public bool Success { get; set; }
        public string WavPath { get; set; }
        public string Engine { get; set; }
        public string Error { get; set; }
    }

    /// <summary>
    /// Bridges to the Node helper that uses Edge neural TTS with an offline SAPI fallback.
    /// The helper returns a PCM WAV file so Windows SoundPlayer can interrupt playback.
    /// </summary>
    public static class TtsHelper
    {
        public static async Task<TtsResult> SynthesizeAsync(
            string text,
            string voice,
            int ratePercent,
            string projectRoot,
            CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            TtsResult result = new TtsResult();
            if (string.IsNullOrWhiteSpace(text))
            {
                result.Error = "Nothing to say.";
                return result;
            }

            string helper = Path.Combine(Path.Combine(projectRoot, "tools"), "edge-tts.mjs");
            if (!File.Exists(helper))
            {
                result.Error = "TTS helper not found: " + helper;
                return result;
            }

            string tempDirectory = Path.Combine(Path.GetTempPath(), "ultron-tts");
            Directory.CreateDirectory(tempDirectory);
            string wavPath = Path.Combine(tempDirectory, Guid.NewGuid().ToString("N") + ".wav");

            Process process = null;
            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo();
                startInfo.FileName = "node";
                startInfo.Arguments = Quote(helper) +
                    " --voice " + Quote(voice) +
                    " --rate " + ratePercent +
                    " --out " + Quote(wavPath);
                startInfo.WorkingDirectory = projectRoot;
                startInfo.UseShellExecute = false;
                startInfo.CreateNoWindow = true;
                startInfo.RedirectStandardInput = true;
                startInfo.RedirectStandardOutput = true;
                startInfo.RedirectStandardError = true;
                startInfo.StandardOutputEncoding = Encoding.UTF8;
                startInfo.StandardErrorEncoding = Encoding.UTF8;

                process = new Process();
                process.StartInfo = startInfo;
                process.Start();

                Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
                Task<string> stderrTask = process.StandardError.ReadToEndAsync();

                process.StandardInput.Write(text);
                process.StandardInput.Close();

                Task waitTask = Task.Run(delegate { process.WaitForExit(); });
                TaskCompletionSource<bool> cancelSignal = new TaskCompletionSource<bool>();
                using (token.Register(delegate
                {
                    try
                    {
                        if (!process.HasExited)
                        {
                            process.Kill();
                        }
                    }
                    catch
                    {
                        // Ignore kill races.
                    }
                    cancelSignal.TrySetResult(true);
                }))
                {
                    Task finished = await Task.WhenAny(waitTask, cancelSignal.Task).ConfigureAwait(false);
                    if (finished == cancelSignal.Task && token.IsCancellationRequested)
                    {
                        throw new OperationCanceledException(token);
                    }
                    await waitTask.ConfigureAwait(false);
                }

                string stdout = await stdoutTask.ConfigureAwait(false);
                string stderr = await stderrTask.ConfigureAwait(false);
                token.ThrowIfCancellationRequested();

                result = ParseHelperOutput(stdout, stderr, wavPath);
                return result;
            }
            catch (OperationCanceledException)
            {
                TryDelete(wavPath);
                throw;
            }
            catch (Exception exception)
            {
                TryDelete(wavPath);
                result.Success = false;
                result.Error = Redaction.Redact(exception.Message);
                return result;
            }
            finally
            {
                if (process != null)
                {
                    process.Dispose();
                }
            }
        }

        private static TtsResult ParseHelperOutput(string stdout, string stderr, string expectedWav)
        {
            TtsResult result = new TtsResult();
            if (string.IsNullOrWhiteSpace(stdout))
            {
                result.Error = "TTS helper returned no output. " + Redaction.Redact(stderr);
                return result;
            }

            try
            {
                JavaScriptSerializer serializer = JsonUtil.CreateSerializer();
                Dictionary<string, object> root = serializer.Deserialize<Dictionary<string, object>>(stdout.Trim());
                bool ok = JsonUtil.GetBool(root, "ok");
                string wav = JsonUtil.GetString(root, "wav");
                string engine = JsonUtil.GetString(root, "engine");
                string error = JsonUtil.GetString(root, "error");

                if (ok && !string.IsNullOrWhiteSpace(wav) && File.Exists(wav))
                {
                    result.Success = true;
                    result.WavPath = wav;
                    result.Engine = string.IsNullOrWhiteSpace(engine) ? "unknown" : engine;
                    return result;
                }

                result.Error = string.IsNullOrWhiteSpace(error)
                    ? ("TTS failed. " + Redaction.Redact(stderr))
                    : error;
                TryDelete(expectedWav);
                return result;
            }
            catch (Exception exception)
            {
                result.Error = "Could not parse TTS output: " + Redaction.Redact(exception.Message) +
                    " " + Redaction.Redact(stderr);
                TryDelete(expectedWav);
                return result;
            }
        }

        private static string Quote(string value)
        {
            if (value == null)
            {
                return "\"\"";
            }
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }

        public static void TryDelete(string path)
        {
            try
            {
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
                // Temp cleanup is best effort.
            }
        }
    }
}
