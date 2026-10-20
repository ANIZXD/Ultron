using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using Ultron.Core;

namespace Ultron
{
    public class SettingsDialog : Form
    {
        private readonly AppSettings _settings;
        private readonly TextBox _url;
        private readonly TextBox _model;
        private readonly ComboBox _voice;
        private readonly TrackBar _rate;
        private readonly Label _rateValue;
        private readonly TrackBar _confidence;
        private readonly Label _confidenceValue;
        private readonly CheckBox _review;
        private readonly ComboBox _engine;
        private readonly CheckBox _pushToTalk;
        private readonly NumericUpDown _history;
        private readonly TextBox _prompt;
        private readonly Label _testStatus;
        private readonly FlatButton _testButton;

        private readonly string[] _voiceValues = new string[]
        {
            "en-US-AvaMultilingualNeural",
            "en-US-AndrewMultilingualNeural",
            "en-US-EmmaMultilingualNeural",
            "sapi:Microsoft Zira Desktop",
            "sapi:Microsoft David Desktop"
        };

        private readonly string[] _engineValues = new string[]
        {
            "windows",
            "sapi"
        };

        public AppSettings Settings
        {
            get { return _settings; }
        }

        public SettingsDialog(AppSettings current)
        {
            _settings = current.Clone();

            Text = "Ultron settings";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(680, 720);
            BackColor = Theme.Background;
            ShowInTaskbar = false;
            AutoScaleMode = AutoScaleMode.None;

            CardPanel card = new CardPanel();
            card.Bounds = new Rectangle(10, 10, 660, 700);
            card.CornerRadius = 14;
            card.BackColor = Theme.Surface;
            Controls.Add(card);

            Label eyebrow = MakeLabel(card, "SETTINGS", 26, 20, Theme.SmallBold, Theme.Accent);
            Label title = MakeLabel(card, "Precision & voice", 24, 40, new Font("Segoe UI Semibold", 17f, FontStyle.Bold), Theme.Text);

            MakeLabel(card, "OLLAMA URL", 26, 92, Theme.SmallBold, Theme.Muted);
            _url = MakeTextBox(card, _settings.OllamaUrl, 26, 112, 390);

            MakeLabel(card, "MODEL", 432, 92, Theme.SmallBold, Theme.Muted);
            _model = MakeTextBox(card, _settings.Model, 432, 112, 202);

            MakeLabel(card, "VOICE", 26, 158, Theme.SmallBold, Theme.Muted);
            _voice = new ComboBox();
            _voice.DropDownStyle = ComboBoxStyle.DropDownList;
            _voice.FlatStyle = FlatStyle.Flat;
            _voice.BackColor = Theme.Surface2;
            _voice.ForeColor = Theme.Text;
            _voice.Font = Theme.Body;
            _voice.Bounds = new Rectangle(26, 178, 608, 28);
            _voice.Items.Add("Ava - warm, lively (Edge neural)");
            _voice.Items.Add("Andrew - calm, deep (Edge neural)");
            _voice.Items.Add("Emma - clear, bright (Edge neural)");
            _voice.Items.Add("Zira - offline (Windows SAPI)");
            _voice.Items.Add("David - offline (Windows SAPI)");
            int selected = Array.IndexOf(_voiceValues, _settings.Voice);
            if (selected < 0)
            {
                _voice.Items.Add("Custom - " + _settings.Voice);
                selected = _voice.Items.Count - 1;
            }
            _voice.SelectedIndex = selected;
            card.Controls.Add(_voice);

            MakeLabel(card, "SPEAKING RATE", 26, 220, Theme.SmallBold, Theme.Muted);
            _rate = new TrackBar();
            _rate.Minimum = -20;
            _rate.Maximum = 30;
            _rate.Value = Math.Max(_rate.Minimum, Math.Min(_rate.Maximum, _settings.RatePercent));
            _rate.TickStyle = TickStyle.None;
            _rate.Bounds = new Rectangle(24, 236, 250, 34);
            _rate.BackColor = Theme.Surface;
            _rate.ValueChanged += delegate { UpdateRateLabel(); };
            card.Controls.Add(_rate);
            _rateValue = MakeLabel(card, string.Empty, 282, 242, Theme.SmallBold, Theme.Text);

            MakeLabel(card, "CONFIDENCE FLOOR", 340, 220, Theme.SmallBold, Theme.Muted);
            _confidence = new TrackBar();
            _confidence.Minimum = 20;
            _confidence.Maximum = 80;
            _confidence.Value = Math.Max(_confidence.Minimum,
                Math.Min(_confidence.Maximum, (int)Math.Round(_settings.ConfidenceThreshold * 100)));
            _confidence.TickStyle = TickStyle.None;
            _confidence.Bounds = new Rectangle(338, 236, 230, 34);
            _confidence.BackColor = Theme.Surface;
            _confidence.ValueChanged += delegate { UpdateConfidenceLabel(); };
            card.Controls.Add(_confidence);
            _confidenceValue = MakeLabel(card, string.Empty, 580, 242, Theme.SmallBold, Theme.Text);

            _review = new CheckBox();
            _review.Text = "Ask me to confirm low-confidence speech before sending";
            _review.Font = Theme.Body;
            _review.ForeColor = Theme.Text;
            _review.BackColor = Theme.Surface;
            _review.AutoSize = true;
            _review.Location = new Point(26, 284);
            _review.Checked = _settings.ReviewLowConfidence;
            card.Controls.Add(_review);

            MakeLabel(card, "SPEECH INPUT", 26, 314, Theme.SmallBold, Theme.Muted);

            _engine = new ComboBox();
            _engine.DropDownStyle = ComboBoxStyle.DropDownList;
            _engine.FlatStyle = FlatStyle.Flat;
            _engine.BackColor = Theme.Surface2;
            _engine.ForeColor = Theme.Text;
            _engine.Font = Theme.Body;
            _engine.Bounds = new Rectangle(26, 334, 360, 28);
            _engine.Items.Add("Windows Speech (better, may use cloud)");
            _engine.Items.Add("Offline SAPI (private, lower accuracy)");
            _engine.SelectedIndex = string.Equals(_settings.SpeechEngine, "sapi",
                StringComparison.OrdinalIgnoreCase) ? 1 : 0;
            card.Controls.Add(_engine);

            FlatButton speechSettings = new FlatButton();
            speechSettings.Text = "Windows speech settings";
            speechSettings.Bounds = new Rectangle(396, 334, 190, 28);
            speechSettings.BackColor = Theme.Surface2;
            speechSettings.ForeColor = Theme.Text;
            speechSettings.Font = Theme.Small;
            speechSettings.Click += delegate { WindowsSpeechInput.OpenPrivacySettings(); };
            card.Controls.Add(speechSettings);

            _pushToTalk = new CheckBox();
            _pushToTalk.Text = "Push to talk (recommended)";
            _pushToTalk.Font = Theme.Body;
            _pushToTalk.ForeColor = Theme.Text;
            _pushToTalk.BackColor = Theme.Surface;
            _pushToTalk.AutoSize = true;
            _pushToTalk.Location = new Point(26, 372);
            _pushToTalk.Checked = _settings.PushToTalk;
            card.Controls.Add(_pushToTalk);
            MakeLabel(card, "Mic opens for one phrase per press. Ctrl+Alt+Space toggles.",
                26, 396, Theme.Small, Theme.Faint);

            MakeLabel(card, "HISTORY", 26, 430, Theme.SmallBold, Theme.Muted);
            _history = new NumericUpDown();
            _history.Minimum = 2;
            _history.Maximum = 40;
            _history.Value = Math.Max(2, Math.Min(40, _settings.MaxHistory));
            _history.Bounds = new Rectangle(26, 450, 64, 24);
            _history.BackColor = Theme.Surface2;
            _history.ForeColor = Theme.Text;
            _history.BorderStyle = BorderStyle.FixedSingle;
            _history.Font = Theme.Body;
            card.Controls.Add(_history);
            MakeLabel(card, "messages kept in context", 100, 452, Theme.Small, Theme.Faint);

            MakeLabel(card, "SYSTEM PROMPT", 26, 490, Theme.SmallBold, Theme.Muted);
            _prompt = new TextBox();
            _prompt.Multiline = true;
            _prompt.ScrollBars = ScrollBars.Vertical;
            _prompt.Bounds = new Rectangle(26, 510, 608, 92);
            _prompt.BackColor = Theme.Surface2;
            _prompt.ForeColor = Theme.Text;
            _prompt.BorderStyle = BorderStyle.FixedSingle;
            _prompt.Font = Theme.Small;
            _prompt.Text = _settings.SystemPrompt;
            card.Controls.Add(_prompt);

            _testButton = new FlatButton();
            _testButton.Text = "Test Ollama";
            _testButton.Bounds = new Rectangle(26, 612, 150, 36);
            _testButton.BackColor = Theme.Surface2;
            _testButton.ForeColor = Theme.Text;
            _testButton.Click += TestConnection_Click;
            card.Controls.Add(_testButton);

            _testStatus = MakeLabel(card, "Not tested.", 190, 620, Theme.Small, Theme.Faint);

            FlatButton cancel = new FlatButton();
            cancel.Text = "Cancel";
            cancel.Bounds = new Rectangle(396, 650, 108, 40);
            cancel.BackColor = Theme.Surface2;
            cancel.ForeColor = Theme.Text;
            cancel.DialogResult = DialogResult.Cancel;
            card.Controls.Add(cancel);

            FlatButton save = new FlatButton();
            save.Text = "Save settings";
            save.Bounds = new Rectangle(514, 650, 120, 40);
            save.BackColor = Theme.Accent;
            save.ForeColor = Color.FromArgb(4, 20, 19);
            save.FlatAppearance.MouseOverBackColor = Theme.AccentHover;
            save.Click += Save_Click;
            card.Controls.Add(save);

            CancelButton = cancel;
            UpdateRateLabel();
            UpdateConfidenceLabel();
        }

        private void Save_Click(object sender, EventArgs e)
        {
            _settings.OllamaUrl = _url.Text;
            _settings.Model = _model.Text;
            _settings.Voice = _voiceValues.Length > _voice.SelectedIndex && _voice.SelectedIndex >= 0
                ? _voiceValues[_voice.SelectedIndex]
                : Convert.ToString(_voice.SelectedItem).Replace("Custom - ", string.Empty);
            _settings.RatePercent = _rate.Value;
            _settings.ConfidenceThreshold = _confidence.Value / 100.0;
            _settings.ReviewLowConfidence = _review.Checked;
            _settings.SpeechEngine = _engineValues[Math.Max(0, _engine.SelectedIndex)];
            _settings.PushToTalk = _pushToTalk.Checked;
            _settings.MaxHistory = (int)_history.Value;
            _settings.SystemPrompt = _prompt.Text;
            _settings.Normalize();

            List<string> errors = _settings.Validate();
            if (errors.Count > 0)
            {
                _testStatus.Text = errors[0];
                _testStatus.ForeColor = Theme.Danger;
                return;
            }

            DialogResult = DialogResult.OK;
            Close();
        }

        private async void TestConnection_Click(object sender, EventArgs e)
        {
            _testButton.Enabled = false;
            _testStatus.ForeColor = Theme.Muted;
            _testStatus.Text = "Connecting...";

            OllamaClient client = null;
            try
            {
                string url = _url.Text.Trim();
                string model = _model.Text.Trim();
                client = new OllamaClient(url, model);
                CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                List<string> models = await client.ListModelsAsync(cts.Token);
                bool present = false;
                for (int i = 0; i < models.Count; i++)
                {
                    if (string.Equals(models[i], model, StringComparison.OrdinalIgnoreCase))
                    {
                        present = true;
                        break;
                    }
                }
                _testStatus.ForeColor = present ? Theme.Success : Theme.Warning;
                _testStatus.Text = present
                    ? "Connected. Model is installed."
                    : "Connected, but model was not listed.";
            }
            catch (Exception exception)
            {
                _testStatus.ForeColor = Theme.Danger;
                _testStatus.Text = "Failed: " + Redaction.Redact(exception.Message);
            }
            finally
            {
                if (client != null)
                {
                    client.Dispose();
                }
                _testButton.Enabled = true;
            }
        }

        private void UpdateRateLabel()
        {
            string sign = _rate.Value >= 0 ? "+" : string.Empty;
            _rateValue.Text = sign + _rate.Value + "%";
        }

        private void UpdateConfidenceLabel()
        {
            _confidenceValue.Text = _confidence.Value + "%";
        }

        private static Label MakeLabel(Control parent, string text, int x, int y, Font font, Color color)
        {
            Label label = new Label();
            label.Text = text;
            label.AutoSize = true;
            label.Font = font;
            label.ForeColor = color;
            label.BackColor = parent.BackColor;
            label.Location = new Point(x, y);
            parent.Controls.Add(label);
            return label;
        }

        private static TextBox MakeTextBox(Control parent, string text, int x, int y, int width)
        {
            TextBox box = new TextBox();
            box.Text = text;
            box.Bounds = new Rectangle(x, y, width, 24);
            box.BackColor = Theme.Surface2;
            box.ForeColor = Theme.Text;
            box.BorderStyle = BorderStyle.FixedSingle;
            box.Font = Theme.Body;
            parent.Controls.Add(box);
            return box;
        }
    }
}

