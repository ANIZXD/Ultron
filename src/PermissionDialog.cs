using System;
using System.Drawing;
using System.Windows.Forms;

namespace Ultron
{
    public class PermissionDialog : Form
    {
        /// <summary>
        /// True only when the user explicitly pressed "Allow session".
        /// ShowDialog returning OK is not enough on its own.
        /// </summary>
        public bool Approved { get; private set; }

        public PermissionDialog(string speechEngine)
        {
            Text = "Ultron permission";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(560, 420);
            BackColor = Theme.Background;
            ShowInTaskbar = false;
            AutoScaleMode = AutoScaleMode.None;

            CardPanel card = new CardPanel();
            card.Bounds = new Rectangle(10, 10, 540, 400);
            card.CornerRadius = 14;
            card.BorderColor = Theme.Border;
            card.BackColor = Theme.Surface;
            Controls.Add(card);

            Label eyebrow = new Label();
            eyebrow.Text = "SESSION PERMISSION";
            eyebrow.Font = Theme.SmallBold;
            eyebrow.ForeColor = Theme.Accent;
            eyebrow.BackColor = Theme.Surface;
            eyebrow.AutoSize = true;
            eyebrow.Location = new Point(26, 24);
            card.Controls.Add(eyebrow);

            Label title = new Label();
            title.Text = "Allow Ultron to listen and speak?";
            title.Font = new Font("Segoe UI Semibold", 17f, FontStyle.Bold);
            title.ForeColor = Theme.Text;
            title.BackColor = Theme.Surface;
            title.AutoSize = true;
            title.Location = new Point(24, 48);
            card.Controls.Add(title);

            bool windowsSpeech = !string.Equals(speechEngine, "sapi",
                StringComparison.OrdinalIgnoreCase);
            string recognitionLine = windowsSpeech
                ? "   -  transcribe it with Windows Speech, which may use Microsoft's\n" +
                  "      online speech service and may send microphone audio to Microsoft\n"
                : "   -  transcribe it offline on this PC with Windows SAPI\n";

            Label body = new Label();
            body.Text = "This session will:\n" +
                        "   -  listen through your microphone for speech\n" +
                        recognitionLine +
                        "   -  send only the transcribed text to your local Ollama model\n" +
                        "   -  speak responses aloud through your speakers\n\n" +
                        "It will not join calls, send messages, or access private data.\n" +
                        "You can interrupt or take over at any time.";
            body.Font = Theme.Body;
            body.ForeColor = Theme.Muted;
            body.BackColor = Theme.Surface;
            body.AutoSize = false;
            body.Bounds = new Rectangle(26, 96, 488, 190);
            card.Controls.Add(body);

            FlatButton decline = new FlatButton();
            decline.Text = "Not now";
            decline.Bounds = new Rectangle(292, 300, 108, 40);
            decline.BackColor = Theme.Surface2;
            decline.ForeColor = Theme.Text;
            decline.FlatAppearance.MouseOverBackColor = Theme.Surface3;
            decline.DialogResult = DialogResult.Cancel;
            card.Controls.Add(decline);

            FlatButton approve = new FlatButton();
            approve.Text = "Allow session";
            approve.Bounds = new Rectangle(410, 300, 130, 40);
            approve.BackColor = Theme.Accent;
            approve.ForeColor = Color.FromArgb(4, 20, 19);
            approve.FlatAppearance.MouseOverBackColor = Theme.AccentHover;
            approve.DialogResult = DialogResult.OK;
            approve.Click += delegate
            {
                Approved = true;
                DialogResult = DialogResult.OK;
                Close();
            };
            card.Controls.Add(approve);

            Label footnote = new Label();
            footnote.Text = "Approval is required every time a new session starts.";
            footnote.Font = Theme.Tiny;
            footnote.ForeColor = Theme.Faint;
            footnote.BackColor = Theme.Surface;
            footnote.AutoSize = true;
            footnote.Location = new Point(26, 354);
            card.Controls.Add(footnote);

            CancelButton = decline;
            ActiveControl = decline;
        }
    }
}

