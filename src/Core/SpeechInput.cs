using System;
using System.Collections.Generic;
using System.Globalization;
using System.Speech.Recognition;

namespace Ultron.Core
{
    public class SpeechHeardEventArgs : EventArgs
    {
        public string Text { get; private set; }
        public float Confidence { get; private set; }

        public SpeechHeardEventArgs(string text, float confidence)
        {
            Text = text;
            Confidence = confidence;
        }
    }

    public class SpeechProbeResult
    {
        public bool Success { get; set; }
        public string Text { get; set; }
        public float Confidence { get; set; }
        public string Error { get; set; }
    }

    /// <summary>
    /// Offline dictation via Windows SAPI (System.Speech). No cloud audio is sent.
    /// </summary>
    public class SpeechInput : ISpeechInput
    {
        private SpeechRecognitionEngine _engine;
        private bool _listening;
        private string _recognizerName;
        private string _initializationError;

        public event EventHandler<SpeechHeardEventArgs> SpeechHeard;
        public event EventHandler<string> StatusChanged;
        public event EventHandler<string> ListeningStopped;

        public float ConfidenceThreshold { get; set; }

        public SpeechInput()
        {
            ConfidenceThreshold = 0.45f;
            try
            {
                RecognizerInfo selected = null;
                foreach (RecognizerInfo info in SpeechRecognitionEngine.InstalledRecognizers())
                {
                    if (selected == null)
                    {
                        selected = info;
                    }
                    if (info.Culture != null && info.Culture.Name == "en-US")
                    {
                        selected = info;
                        break;
                    }
                }

                if (selected == null)
                {
                    _initializationError = "No Windows speech recognizer is installed.";
                    return;
                }

                _engine = new SpeechRecognitionEngine(selected);
                _engine.LoadGrammar(new DictationGrammar());
                _engine.InitialSilenceTimeout = TimeSpan.FromSeconds(4);
                _engine.BabbleTimeout = TimeSpan.FromSeconds(2);
                _engine.EndSilenceTimeout = TimeSpan.FromMilliseconds(650);
                _recognizerName = selected.Culture.Name + " — " + selected.Name;

                _engine.SpeechRecognized += EngineSpeechRecognized;
                _engine.SpeechRecognitionRejected += EngineSpeechRejected;
                _engine.RecognizeCompleted += EngineRecognizeCompleted;
            }
            catch (Exception exception)
            {
                _initializationError = "Speech engine unavailable: " + Redaction.Redact(exception.Message);
                if (_engine != null)
                {
                    try { _engine.Dispose(); } catch { }
                    _engine = null;
                }
            }
        }

        public bool IsAvailable
        {
            get { return _engine != null; }
        }

        public bool IsListening
        {
            get { return _listening; }
        }

        public string RecognizerName
        {
            get { return _recognizerName; }
        }

        public string InitializationError
        {
            get { return _initializationError; }
        }

        public void Start()
        {
            if (_engine == null)
            {
                throw new InvalidOperationException(
                    string.IsNullOrEmpty(_initializationError)
                        ? "Speech recognition is unavailable."
                        : _initializationError);
            }

            if (_listening)
            {
                return;
            }

            try
            {
                _engine.SetInputToDefaultAudioDevice();
                _listening = true;
                // One utterance per Talk press. Background noise must never
                // leave the microphone open for the next sound in the room.
                _engine.RecognizeAsync(RecognizeMode.Single);
                RaiseStatus("Microphone active. Listening.");
            }
            catch (Exception exception)
            {
                _listening = false;
                throw new InvalidOperationException(
                    "Could not open the microphone: " + Redaction.Redact(exception.Message), exception);
            }
        }

        public void Stop()
        {
            if (_engine == null || !_listening)
            {
                return;
            }

            _listening = false;
            try
            {
                _engine.RecognizeAsyncCancel();
            }
            catch
            {
                // Ignore cancellation races.
            }
        }

        public void Dispose()
        {
            Stop();
            if (_engine != null)
            {
                try
                {
                    _engine.SetInputToNull();
                }
                catch
                {
                    // Ignore.
                }
                _engine.Dispose();
                _engine = null;
            }
        }

        public static List<string> GetRecognizers()
        {
            List<string> names = new List<string>();
            try
            {
                foreach (RecognizerInfo info in SpeechRecognitionEngine.InstalledRecognizers())
                {
                    names.Add(info.Culture.Name + " — " + info.Name);
                }
            }
            catch
            {
                // Caller can show an empty list.
            }
            return names;
        }

        /// <summary>
        /// Tests the full STT stack from a WAV file, avoiding microphone dependence.
        /// </summary>
        public static SpeechProbeResult ProbeWave(string wavPath)
        {
            SpeechProbeResult result = new SpeechProbeResult();
            SpeechRecognitionEngine engine = null;
            try
            {
                RecognizerInfo selected = null;
                foreach (RecognizerInfo info in SpeechRecognitionEngine.InstalledRecognizers())
                {
                    if (selected == null)
                    {
                        selected = info;
                    }
                    if (info.Culture != null && info.Culture.Name == "en-US")
                    {
                        selected = info;
                        break;
                    }
                }

                if (selected == null)
                {
                    result.Error = "No Windows speech recognizer is installed.";
                    return result;
                }

                engine = new SpeechRecognitionEngine(selected);
                engine.LoadGrammar(new DictationGrammar());
                engine.SetInputToWaveFile(wavPath);
                RecognitionResult recognition = engine.Recognize(TimeSpan.FromSeconds(15));
                if (recognition == null)
                {
                    result.Error = "No speech was recognized in the test WAV.";
                    return result;
                }

                result.Success = true;
                result.Text = recognition.Text;
                result.Confidence = recognition.Confidence;
                return result;
            }
            catch (Exception exception)
            {
                result.Error = Redaction.Redact(exception.Message);
                return result;
            }
            finally
            {
                if (engine != null)
                {
                    try { engine.Dispose(); } catch { }
                }
            }
        }

        private void EngineSpeechRecognized(object sender, SpeechRecognizedEventArgs e)
        {
            if (e.Result == null || string.IsNullOrWhiteSpace(e.Result.Text))
            {
                return;
            }

            EventHandler<SpeechHeardEventArgs> handler = SpeechHeard;
            if (handler != null)
            {
                handler(this, new SpeechHeardEventArgs(e.Result.Text.Trim(), e.Result.Confidence));
            }

            // Single-result mode should end by itself, but cancel explicitly so
            // the microphone is released even if no consumer handled the event.
            Stop();
        }

        private void EngineSpeechRejected(object sender, SpeechRecognitionRejectedEventArgs e)
        {
            // The completion event below returns the UI to idle. Do not spam
            // the activity log for every cough or door click.
        }

        private void EngineRecognizeCompleted(object sender, RecognizeCompletedEventArgs e)
        {
            _listening = false;
            if (e.Cancelled)
            {
                return;
            }
            if (e.Error != null)
            {
                RaiseListeningStopped("Microphone stopped: " +
                                      Redaction.Redact(e.Error.Message));
                return;
            }
            if (e.Result == null)
            {
                RaiseListeningStopped("No clear speech detected. Press Talk to try again.");
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

        private void RaiseListeningStopped(string message)
        {
            EventHandler<string> handler = ListeningStopped;
            if (handler != null)
            {
                handler(this, message);
            }
        }
    }
}
