using System;
using System.IO;
using System.Media;
using System.Speech.Synthesis;
using System.Threading;
using System.Threading.Tasks;

namespace Ultron.Core
{
    /// <summary>
    /// Synthesizes to a WAV file, then plays it with SoundPlayer so interruption is immediate.
    /// </summary>
    public class SpeechOutput : IDisposable
    {
        private readonly object _gate = new object();
        private readonly string _projectRoot;
        private SoundPlayer _player;
        private bool _speaking;
        private bool _stopRequested;

        public event EventHandler<string> StatusChanged;

        public SpeechOutput(string projectRoot, string voice, int ratePercent)
        {
            _projectRoot = projectRoot;
            Voice = voice;
            RatePercent = ratePercent;
        }

        public string Voice { get; set; }
        public int RatePercent { get; set; }

        public bool IsSpeaking
        {
            get
            {
                lock (_gate)
                {
                    return _speaking;
                }
            }
        }

        public async Task SpeakAsync(string text, CancellationToken token)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            token.ThrowIfCancellationRequested();
            _stopRequested = false;
            SetSpeaking(true);
            RaiseStatus("Synthesizing voice…");

            TtsResult result = await TtsHelper.SynthesizeAsync(
                text, Voice, RatePercent, _projectRoot, token).ConfigureAwait(false);

            if (!result.Success)
            {
                RaiseStatus("Neural voice unavailable; using offline SAPI.");
                string fallbackPath = Path.Combine(
                    Path.GetTempPath(), "ultron-tts", Guid.NewGuid().ToString("N") + "-sapi.wav");
                string fallbackError;
                bool fallbackOk = SynthesizeSapiFallback(text, Voice, RatePercent, fallbackPath, out fallbackError);
                if (!fallbackOk)
                {
                    SetSpeaking(false);
                    throw new InvalidOperationException(
                        "Text-to-speech failed. " + (result.Error ?? string.Empty) + " " + fallbackError);
                }

                result = new TtsResult();
                result.Success = true;
                result.WavPath = fallbackPath;
                result.Engine = "sapi";
            }

            token.ThrowIfCancellationRequested();
            SoundPlayer player = null;
            try
            {
                player = new SoundPlayer(result.WavPath);
                lock (_gate)
                {
                    _player = player;
                }

                player.Load();
                RaiseStatus("Speaking (" + result.Engine + ").");
                await Task.Run(delegate { player.PlaySync(); }, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    "Could not play the synthesized voice: " + Redaction.Redact(exception.Message), exception);
            }
            finally
            {
                lock (_gate)
                {
                    _player = null;
                }
                if (player != null)
                {
                    try { player.Dispose(); } catch { }
                }
                SetSpeaking(false);
                TtsHelper.TryDelete(result.WavPath);
            }

            if (token.IsCancellationRequested || _stopRequested)
            {
                return;
            }
            RaiseStatus("Speech finished.");
        }

        public void Stop()
        {
            _stopRequested = true;
            SoundPlayer player;
            lock (_gate)
            {
                player = _player;
                _speaking = false;
            }

            if (player != null)
            {
                try
                {
                    player.Stop();
                }
                catch
                {
                    // Ignore stop races.
                }
            }

            RaiseStatus("Speech stopped.");
        }

        public void Dispose()
        {
            Stop();
        }

        private void SetSpeaking(bool value)
        {
            lock (_gate)
            {
                _speaking = value;
            }
        }

        private void RaiseStatus(string message)
        {
            EventHandler<string> handler = StatusChanged;
            if (handler != null)
            {
                handler(this, message);
            }
        }

        private static bool SynthesizeSapiFallback(
            string text, string voice, int ratePercent, string wavPath, out string error)
        {
            string localError = null;
            bool ok = false;
            Thread thread = new Thread(new ThreadStart(delegate
            {
                SpeechSynthesizer synthesizer = null;
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(wavPath));
                    synthesizer = new SpeechSynthesizer();
                    string sapiVoice = SelectSapiVoice(voice);
                    if (!string.IsNullOrEmpty(sapiVoice))
                    {
                        try { synthesizer.SelectVoice(sapiVoice); } catch { }
                    }
                    synthesizer.Rate = Math.Max(-10, Math.Min(10, (int)Math.Round(ratePercent / 10.0)));
                    synthesizer.SetOutputToWaveFile(wavPath);
                    synthesizer.Speak(text);
                    synthesizer.Dispose();
                    synthesizer = null;
                    ok = File.Exists(wavPath) && new FileInfo(wavPath).Length > 1000;
                    if (!ok)
                    {
                        localError = "SAPI produced no audio.";
                    }
                }
                catch (Exception exception)
                {
                    localError = Redaction.Redact(exception.Message);
                }
                finally
                {
                    if (synthesizer != null)
                    {
                        try { synthesizer.Dispose(); } catch { }
                    }
                }
            }));
            thread.IsBackground = true;
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join(TimeSpan.FromSeconds(30));

            if (thread.IsAlive)
            {
                error = "SAPI timed out.";
                TtsHelper.TryDelete(wavPath);
                return false;
            }

            error = localError;
            if (!ok)
            {
                TtsHelper.TryDelete(wavPath);
            }
            return ok;
        }

        private static string SelectSapiVoice(string voice)
        {
            string value = voice ?? string.Empty;
            if (value.IndexOf("andrew", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("david", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Microsoft David Desktop";
            }
            return "Microsoft Zira Desktop";
        }
    }
}
