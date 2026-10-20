using System;

namespace Ultron.Core
{
    public class SpeechErrorEventArgs : EventArgs
    {
        public string Message { get; private set; }
        public bool PrivacyConsentRequired { get; private set; }

        public SpeechErrorEventArgs(string message, bool privacyConsentRequired)
        {
            Message = message;
            PrivacyConsentRequired = privacyConsentRequired;
        }
    }

    public interface ISpeechInput : IDisposable
    {
        event EventHandler<SpeechHeardEventArgs> SpeechHeard;
        event EventHandler<string> StatusChanged;

        /// <summary>
        /// Raised when the engine stops listening without producing a result,
        /// for example after a timeout or a rejected/noisy phrase.
        /// </summary>
        event EventHandler<string> ListeningStopped;

        bool IsAvailable { get; }
        bool IsListening { get; }
        string RecognizerName { get; }
        string InitializationError { get; }

        void Start();
        void Stop();
    }
}
