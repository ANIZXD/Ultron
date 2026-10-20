using System;
using System.Collections.Generic;

namespace Ultron.Core
{
    public class ChatMessage
    {
        public string Role { get; set; }
        public string Content { get; set; }
        public DateTime At { get; set; }

        public ChatMessage(string role, string content)
        {
            Role = role;
            Content = content;
            At = DateTime.Now;
        }
    }

    /// <summary>
    /// In-memory conversation history. Nothing here is written to disk.
    /// </summary>
    public class Conversation
    {
        private readonly List<ChatMessage> _messages;
        private int _maxHistory;

        public Conversation(int maxHistory)
        {
            _messages = new List<ChatMessage>();
            MaxHistory = maxHistory;
        }

        public int MaxHistory
        {
            get { return _maxHistory; }
            set { _maxHistory = Math.Max(2, Math.Min(40, value)); }
        }

        public int Count
        {
            get { return _messages.Count; }
        }

        public IList<ChatMessage> Messages
        {
            get { return _messages.AsReadOnly(); }
        }

        public void Add(string role, string content)
        {
            if (string.IsNullOrWhiteSpace(role) || string.IsNullOrWhiteSpace(content))
            {
                return;
            }

            _messages.Add(new ChatMessage(role.Trim(), content.Trim()));
            while (_messages.Count > _maxHistory)
            {
                _messages.RemoveAt(0);
            }
        }

        public void AddUser(string content)
        {
            Add("user", content);
        }

        public void AddAssistant(string content)
        {
            Add("assistant", content);
        }

        public List<ChatMessage> BuildPrompt(string systemPrompt)
        {
            List<ChatMessage> prompt = new List<ChatMessage>();
            if (!string.IsNullOrWhiteSpace(systemPrompt))
            {
                prompt.Add(new ChatMessage("system", systemPrompt.Trim()));
            }
            prompt.AddRange(_messages);
            return prompt;
        }

        public void Clear()
        {
            _messages.Clear();
        }
    }
}
