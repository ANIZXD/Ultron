using System;
using System.Diagnostics;
using System.Threading;
using Windows.Foundation;
using Windows.Globalization;
using Windows.Media.SpeechRecognition;

namespace Ultron.Core
{
    /// <summary>
    /// One-shot dictation through the modern Windows speech recognizer.
    /// This engine is materially better than System.Speech, but Windows requires
    /// the user to accept its speech privacy setting before it will capture audio.
    /// The caller is responsible for falling back to SpeechInput (SAPI) if the
    /// privacy setting is disabled.
    /// </summary>
    public class WindowsSpeechInput : ISpeechInput
    {
        private readonly object _gate = new object();
        private IAsyncOperation<SpeechRecognitionResult> _operation;
        private Thread _worker;
        private Timer _watchdog;
        private volatile bool _listening;
        private volatile bool _stopping;
        private string _recognizerName;
        private string _initializationError;

        public event EventHandler<SpeechHeardEventArgs> SpeechHeard;
        public event EventHandler<string> StatusChanged;
        public event EventHandler<string> ListeningStopped;
        public event EventHandler<SpeechErrorEventArgs> Failed;

        public WindowsSpeechInput()
        {
            _recognizerName = "Windows Speech en-US";
        }

        public bool IsAvailable
        {
            get { return true; }
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

        public static bool IsSupported
        {
            get
            {
                try
                {
                    SpeechRecognizer recognizer = new SpeechRecognizer();
                    if (recognizer != null)
                    {
                        recognizer.Dispose();
                    }
                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }

        public static void OpenPrivacySettings()
        {
            try
            {
                Process.Start(new ProcessStartInfo("ms-settings:privacy-speech")
                {
                    UseShellExecute = true
                });
            }
            catch
            {
                try
                {
                    Process.Start("ms-settings:privacy-speech");
                }
                catch
                {
                    // The caller already shows a human-readable instruction.
                }
            }
        }

        public void Start()
        {
            lock (_gate)
            {
                if (_listening)
                {
                    return;
                }

                _listening = true;
                _stopping = false;

                // The Windows recognizer can stay in "hearing babble" mode in a
                // noisy room and ignore its initial-silence timeout. Enforce the
                // one-phrase rule ourselves so the microphone always closes.
                _watchdog = new Timer(WatchdogElapsed, null, 12000, Timeout.Infinite);

                _worker = new Thread(Worker);
                _worker.IsBackground = true;
                _worker.Name = "Ultron Windows Speech";
                try
                {
                    _worker.SetApartmentState(ApartmentState.STA);
                }
                catch
                {
                    // The COM apartment is a preference, not a requirement.
                }
                _worker.Start();
            }
        }

        public void Stop()
        {
            DisposeWatchdog();
            IAsyncOperation<SpeechRecognitionResult> operation = null;
            lock (_gate)
            {
                _listening = false;
                _stopping = true;
                operation = _operation;
            }

            if (operation != null)
            {
                try
                {
                    operation.Cancel();
                }
                catch
                {
                    // The operation may already have completed.
                }
            }
        }

        public void Dispose()
        {
            Stop();
        }

        private void WatchdogElapsed(object state)
        {
            IAsyncOperation<SpeechRecognitionResult> operation = null;
            lock (_gate)
            {
                if (!_listening || _stopping || _operation == null)
                {
                    return;
                }
                _stopping = true;
                _listening = false;
                operation = _operation;
            }

            try
            {
                operation.Cancel();
            }
            catch
            {
                // The operation may have completed between the check and Cancel.
            }

            RaiseListeningStopped("No speech detected. Press Talk to try again.");
        }

        private void DisposeWatchdog()
        {
            Timer timer;
            lock (_gate)
            {
                timer = _watchdog;
                _watchdog = null;
            }
            if (timer != null)
            {
                try { timer.Dispose(); } catch { }
            }
        }

        private void Worker()
        {
            SpeechRecognizer recognizer = null;
            try
            {
                recognizer = new SpeechRecognizer(new Language("en-US"));
                recognizer.Timeouts.InitialSilenceTimeout = TimeSpan.FromSeconds(7);
                recognizer.Timeouts.BabbleTimeout = TimeSpan.FromSeconds(1.5);
                recognizer.Timeouts.EndSilenceTimeout = TimeSpan.FromMilliseconds(800);
                recognizer.Constraints.Add(new SpeechRecognitionTopicConstraint(
                    SpeechRecognitionScenario.Dictation, "dictation"));

                SpeechRecognitionCompilationResult compilation =
                    WaitFor(recognizer.CompileConstraintsAsync());
                if (compilation.Status != SpeechRecognitionResultStatus.Success)
                {
                    _initializationError = "Windows Speech could not compile its dictation grammar (" +
                                           compilation.Status + ").";
                    RaiseFailed(_initializationError, false);
                    return;
                }

                _recognizerName = "Windows Speech " + recognizer.CurrentLanguage.LanguageTag;
                RaiseStatus("Listening. Speak now.");

                IAsyncOperation<SpeechRecognitionResult> operation = recognizer.RecognizeAsync();
                lock (_gate)
                {
                    _operation = operation;
                }

                SpeechRecognitionResult result = WaitFor(operation);
                lock (_gate)
                {
                    _listening = false;
                    _operation = null;
                }
                DisposeWatchdog();

                if (result.Status == SpeechRecognitionResultStatus.Success)
                {
                    string text = result.Text == null ? string.Empty : result.Text.Trim();
                    if (text.Length > 0)
                    {
                        EventHandler<SpeechHeardEventArgs> handler = SpeechHeard;
                        if (handler != null)
                        {
                            handler(this, new SpeechHeardEventArgs(text, NumericConfidence(result)));
                        }
                    }
                    else
                    {
                        RaiseListeningStopped("No clear speech detected. Press Talk to try again.");
                    }
                }
                else if (result.Status == SpeechRecognitionResultStatus.UserCanceled)
                {
                    if (_stopping)
                    {
                        RaiseStatus("Microphone closed.");
                    }
                    else
                    {
                        _listening = false;
                        RaiseListeningStopped("Microphone closed.");
                    }
                }
                else if (result.Status == SpeechRecognitionResultStatus.TimeoutExceeded)
                {
                    _listening = false;
                    RaiseListeningStopped("No speech detected. Press Talk to try again.");
                }
                else if (result.Status == SpeechRecognitionResultStatus.AudioQualityFailure)
                {
                    _listening = false;
                    RaiseListeningStopped("The microphone audio was too poor to recognize. Press Talk to retry.");
                }
                else
                {
                    // Unknown/NetworkFailure/RecognizerError and similar runtime
                    // failures should use the offline fallback instead of
                    // leaving the selected Windows engine permanently broken.
                    _listening = false;
                    RaiseFailed("Windows Speech stopped: " + result.Status + ".", false);
                }
            }
            catch (Exception exception)
            {
                string message = Redaction.Redact(exception.Message);
                bool privacy = message.IndexOf("privacy", StringComparison.OrdinalIgnoreCase) >= 0;
                _initializationError = message;
                if (!_stopping)
                {
                    RaiseFailed(message, privacy);
                }
            }
            finally
            {
                DisposeWatchdog();
                lock (_gate)
                {
                    _operation = null;
                    _listening = false;
                }

                if (recognizer != null)
                {
                    try { recognizer.Dispose(); } catch { }
                }
            }
        }

        private static T WaitFor<T>(IAsyncOperation<T> operation)
        {
            while (operation.Status == AsyncStatus.Started)
            {
                Thread.Sleep(20);
            }

            if (operation.Status != AsyncStatus.Completed)
            {
                throw new InvalidOperationException("Windows Speech operation ended with " +
                                                    operation.Status + ".");
            }

            return operation.GetResults();
        }

        private static float NumericConfidence(SpeechRecognitionResult result)
        {
            double raw = result.RawConfidence;
            if (!double.IsNaN(raw) && raw > 0.0)
            {
                return (float)Math.Max(0.0, Math.Min(1.0, raw));
            }

            switch (result.Confidence)
            {
                case SpeechRecognitionConfidence.High:
                    return 0.95f;
                case SpeechRecognitionConfidence.Medium:
                    return 0.75f;
                case SpeechRecognitionConfidence.Low:
                    return 0.40f;
                default:
                    return 0.0f;
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

        private void RaiseFailed(string message, bool privacyConsentRequired)
        {
            EventHandler<SpeechErrorEventArgs> handler = Failed;
            if (handler != null)
            {
                handler(this, new SpeechErrorEventArgs(message, privacyConsentRequired));
            }
        }
    }
}
