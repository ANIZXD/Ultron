using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace Ultron.Core
{
    public class OllamaChunk
    {
        public string Content { get; set; }
        public string Thinking { get; set; }
        public bool Done { get; set; }
        public string Error { get; set; }
    }

    public class OllamaException : Exception
    {
        public int StatusCode { get; private set; }

        public OllamaException(string message) : base(message)
        {
            StatusCode = 0;
        }

        public OllamaException(string message, int statusCode) : base(message)
        {
            StatusCode = statusCode;
        }
    }

    /// <summary>
    /// Minimal Ollama /api/chat client with NDJSON streaming.
    /// Uses think=false so small models answer instead of spending tokens on reasoning.
    /// </summary>
    public class OllamaClient : IDisposable
    {
        private readonly HttpClient _http;
        private string _baseUrl;
        private string _model;

        public OllamaClient(string baseUrl, string model)
        {
            _baseUrl = NormalizeBaseUrl(baseUrl);
            _model = string.IsNullOrWhiteSpace(model) ? "qwen3:0.6b" : model.Trim();

            _http = new HttpClient();
            _http.Timeout = TimeSpan.FromSeconds(120);
        }

        public string BaseUrl
        {
            get { return _baseUrl; }
            set { _baseUrl = NormalizeBaseUrl(value); }
        }

        public string Model
        {
            get { return _model; }
            set { _model = string.IsNullOrWhiteSpace(value) ? "qwen3:0.6b" : value.Trim(); }
        }

        public async Task<List<string>> ListModelsAsync(CancellationToken token)
        {
            List<string> models = new List<string>();
            using (HttpResponseMessage response = await _http.GetAsync(
                BuildUri("/api/tags"), HttpCompletionOption.ResponseContentRead, token).ConfigureAwait(false))
            {
                string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    throw new OllamaException(
                        "Ollama returned HTTP " + (int)response.StatusCode + ": " + Shorten(body),
                        (int)response.StatusCode);
                }

                JavaScriptSerializer serializer = JsonUtil.CreateSerializer();
                Dictionary<string, object> root = serializer.Deserialize<Dictionary<string, object>>(body);
                object[] items = root == null ? null : JsonUtil.AsArray(GetValue(root, "models"));
                if (items != null)
                {
                    for (int i = 0; i < items.Length; i++)
                    {
                        Dictionary<string, object> item = JsonUtil.AsDictionary(items[i]);
                        string name = JsonUtil.GetString(item, "name");
                        if (string.IsNullOrWhiteSpace(name))
                        {
                            name = JsonUtil.GetString(item, "model");
                        }
                        if (!string.IsNullOrWhiteSpace(name))
                        {
                            models.Add(name);
                        }
                    }
                }
            }
            return models;
        }

        public async Task<string> ChatAsync(
            string systemPrompt,
            IList<ChatMessage> history,
            Action<string> onDelta,
            CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            List<object> messages = new List<object>();
            if (!string.IsNullOrWhiteSpace(systemPrompt))
            {
                messages.Add(Message("system", systemPrompt.Trim()));
            }

            if (history != null)
            {
                for (int i = 0; i < history.Count; i++)
                {
                    ChatMessage item = history[i];
                    if (item != null && !string.IsNullOrWhiteSpace(item.Content))
                    {
                        messages.Add(Message(item.Role, item.Content));
                    }
                }
            }

            Dictionary<string, object> options = new Dictionary<string, object>();
            options["temperature"] = 0.2;
            options["num_predict"] = 96;
            options["top_p"] = 0.9;

            Dictionary<string, object> payload = new Dictionary<string, object>();
            payload["model"] = _model;
            payload["messages"] = messages;
            payload["stream"] = true;
            payload["think"] = false;
            payload["options"] = options;

            JavaScriptSerializer serializer = JsonUtil.CreateSerializer();
            string json = serializer.Serialize(payload);
            StringContent content = new StringContent(json, Encoding.UTF8, "application/json");

            HttpResponseMessage response = null;
            StringBuilder reply = new StringBuilder();
            try
            {
                HttpRequestMessage request = new HttpRequestMessage(
                    HttpMethod.Post, BuildUri("/api/chat"));
                request.Content = content;

                response = await _http.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    token).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    string errorBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    throw new OllamaException(
                        "Ollama returned HTTP " + (int)response.StatusCode + ": " + Shorten(errorBody),
                        (int)response.StatusCode);
                }

                // Disposing the response is the only reliable way to abort a pending
                // ReadLineAsync when the user presses Interrupt.
                using (token.Register(delegate
                {
                    try
                    {
                        if (response != null)
                        {
                            response.Dispose();
                        }
                    }
                    catch
                    {
                        // Ignore disposal races.
                    }
                }))
                {
                    using (Stream stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                    using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
                    {
                        while (true)
                        {
                            token.ThrowIfCancellationRequested();
                            string line = await reader.ReadLineAsync().ConfigureAwait(false);
                            if (line == null)
                            {
                                break;
                            }

                            OllamaChunk chunk = ParseLine(line);
                            if (chunk == null)
                            {
                                continue;
                            }

                            if (!string.IsNullOrEmpty(chunk.Error))
                            {
                                throw new OllamaException("Ollama error: " + chunk.Error);
                            }

                            if (!string.IsNullOrEmpty(chunk.Content))
                            {
                                reply.Append(chunk.Content);
                                if (onDelta != null)
                                {
                                    onDelta(chunk.Content);
                                }
                            }

                            if (chunk.Done)
                            {
                                break;
                            }
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (ObjectDisposedException)
            {
                if (token.IsCancellationRequested)
                {
                    throw new OperationCanceledException(token);
                }
                throw new OllamaException("The Ollama connection was closed unexpectedly.");
            }
            catch (HttpRequestException exception)
            {
                throw new OllamaException(
                    "Cannot reach Ollama at " + _baseUrl + ". Is `ollama serve` running? " +
                    Redaction.Redact(exception.Message));
            }
            finally
            {
                if (response != null)
                {
                    response.Dispose();
                }
                content.Dispose();
            }

            string text = reply.ToString().Trim();
            if (text.Length == 0)
            {
                throw new OllamaException(
                    "Ollama returned an empty answer. Check that model '" + _model + "' is installed.");
            }
            return text;
        }

        public static OllamaChunk ParseLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return null;
            }

            JavaScriptSerializer serializer = JsonUtil.CreateSerializer();
            Dictionary<string, object> root;
            try
            {
                root = serializer.Deserialize<Dictionary<string, object>>(line);
            }
            catch (Exception exception)
            {
                throw new OllamaException("Invalid Ollama stream data: " + Redaction.Redact(exception.Message));
            }

            if (root == null)
            {
                return null;
            }

            OllamaChunk chunk = new OllamaChunk();
            chunk.Done = JsonUtil.GetBool(root, "done");
            chunk.Error = JsonUtil.GetString(root, "error");

            Dictionary<string, object> message = JsonUtil.AsDictionary(GetValue(root, "message"));
            if (message != null)
            {
                chunk.Content = JsonUtil.GetString(message, "content");
                chunk.Thinking = JsonUtil.GetString(message, "thinking");
            }
            return chunk;
        }

        public void Dispose()
        {
            _http.Dispose();
        }

        private static Dictionary<string, object> Message(string role, string content)
        {
            Dictionary<string, object> message = new Dictionary<string, object>();
            message["role"] = role;
            message["content"] = content;
            return message;
        }

        private static object GetValue(Dictionary<string, object> dictionary, string key)
        {
            object value;
            if (dictionary != null && dictionary.TryGetValue(key, out value))
            {
                return value;
            }
            return null;
        }

        private Uri BuildUri(string path)
        {
            return new Uri(_baseUrl + path);
        }

        private static string NormalizeBaseUrl(string value)
        {
            string url = string.IsNullOrWhiteSpace(value) ? "http://127.0.0.1:11434" : value.Trim();
            return url.TrimEnd('/');
        }

        private static string Shorten(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "(empty response)";
            }
            string cleaned = Redaction.Redact(value.Replace("\r", " ").Replace("\n", " ").Trim());
            return cleaned.Length <= 300 ? cleaned : cleaned.Substring(0, 300) + "…";
        }
    }
}
