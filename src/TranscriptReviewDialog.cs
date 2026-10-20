using System;
using System.Drawing;
using System.Windows.Forms;

namespace Ultron
{
    public class TranscriptReviewDialog : Form
    {
        private readonly TextBox _text;

        public string Transcript
        {
            get { return _text.Text.Trim(); }
        }

        public TranscriptReviewDialog(string heard, float confidence)
        {
            Text = "Review transcript";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(640, 380);
            BackColor = Theme.Background;
            ShowInTaskbar = false;
            AutoScaleMode = AutoScaleMode.None;

            CardPanel card = new CardPanel();
            card.Bounds = new Rectangle(10, 10, 620, 360);
            card.CornerRadius = 14;
            card.BackColor = Theme.Surface;
            Controls.Add(card);

            Label eyebrow = new Label();
            eyebrow.Text = "LOW-CONFIDENCE TRANSCRIPT";
            eyebrow.Font = Theme.SmallBold;
            eyebrow.ForeColor = Theme.Warning;
            eyebrow.BackColor = Theme.Surface;
            eyebrow.AutoSize = true;
            eyebrow.Location = new Point(26, 22);
            card.Controls.Add(eyebrow);

            Label title = new Label();
            title.Text = "Check what Ultron heard";
            title.Font = new Font("Segoe UI Semibold", 16f, FontStyle.Bold);
            title.ForeColor = Theme.Text;
            title.BackColor = Theme.Surface;
            title.AutoSize = true;
            title.Location = new Point(24, 44);
            card.Controls.Add(title);

            Label score = new Label();
            score.Text = "Confidence " + confidence.ToString("P0") + " - edit before sending if needed.";
            score.Font = Theme.Small;
            score.ForeColor = confidence < 0.35f ? Theme.Warning : Theme.Muted;
            score.BackColor = Theme.Surface;
            score.AutoSize = true;
            score.Location = new Point(26, 76);
            card.Controls.Add(score);

            _text = new TextBox();
            _text.Multiline = true;
            _text.ScrollBars = ScrollBars.Vertical;
            _text.Bounds = new Rectangle(26, 104, 568, 154);
            _text.BackColor = Theme.Surface2;
            _text.ForeColor = Theme.Text;
            _text.BorderStyle = BorderStyle.FixedSingle;
            _text.Font = new Font("Segoe UI", 11f, FontStyle.Regular);
            _text.Text = heard;
            card.Controls.Add(_text);

            Label hint = new Label();
            hint.Text = "The local model only sees the text you send.";
            hint.Font = Theme.Tiny;
            hint.ForeColor = Theme.Faint;
            hint.BackColor = Theme.Surface;
            hint.AutoSize = true;
            hint.Location = new Point(26, 270);
            card.Controls.Add(hint);

            FlatButton discard = new FlatButton();
            discard.Text = "Discard";
            discard.Bounds = new Rectangle(354, 296, 108, 40);
            discard.BackColor = Theme.Surface2;
            discard.ForeColor = Theme.Text;
            discard.DialogResult = DialogResult.Cancel;
            card.Controls.Add(discard);

            FlatButton send = new FlatButton();
            send.Text = "Send to Ultron";
            send.Bounds = new Rectangle(472, 296, 122, 40);
            send.BackColor = Theme.Accent;
            send.ForeColor = Color.FromArgb(4, 20, 19);
            send.FlatAppearance.MouseOverBackColor = Theme.AccentHover;
            send.DialogResult = DialogResult.OK;
            card.Controls.Add(send);

            AcceptButton = send;
            CancelButton = discard;
            ActiveControl = _text;
            _text.SelectAll();
        }
    }
}

