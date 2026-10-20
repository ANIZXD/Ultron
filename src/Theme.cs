using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Ultron
{
    public static class Theme
    {
        public static readonly Color Background = Color.FromArgb(7, 10, 15);
        public static readonly Color Sidebar = Color.FromArgb(10, 15, 23);
        public static readonly Color Surface = Color.FromArgb(13, 19, 28);
        public static readonly Color Surface2 = Color.FromArgb(17, 26, 38);
        public static readonly Color Surface3 = Color.FromArgb(22, 33, 47);
        public static readonly Color Border = Color.FromArgb(30, 42, 58);
        public static readonly Color BorderSoft = Color.FromArgb(24, 34, 48);
        public static readonly Color Text = Color.FromArgb(230, 237, 243);
        public static readonly Color Muted = Color.FromArgb(139, 155, 180);
        public static readonly Color Faint = Color.FromArgb(95, 110, 130);
        public static readonly Color Accent = Color.FromArgb(94, 234, 212);
        public static readonly Color AccentDark = Color.FromArgb(15, 118, 110);
        public static readonly Color AccentHover = Color.FromArgb(45, 212, 191);
        public static readonly Color Danger = Color.FromArgb(248, 113, 113);
        public static readonly Color DangerDark = Color.FromArgb(127, 29, 29);
        public static readonly Color Warning = Color.FromArgb(251, 191, 36);
        public static readonly Color WarningDark = Color.FromArgb(120, 53, 15);
        public static readonly Color Success = Color.FromArgb(52, 211, 153);
        public static readonly Color UserColor = Color.FromArgb(147, 197, 253);

        public static readonly Font Brand = new Font("Segoe UI Semibold", 21f, FontStyle.Bold);
        public static readonly Font H1 = new Font("Segoe UI Semibold", 23f, FontStyle.Bold);
        public static readonly Font H2 = new Font("Segoe UI Semibold", 12f, FontStyle.Bold);
        public static readonly Font Body = new Font("Segoe UI", 9.5f, FontStyle.Regular);
        public static readonly Font BodyBold = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold);
        public static readonly Font Small = new Font("Segoe UI", 8f, FontStyle.Regular);
        public static readonly Font SmallBold = new Font("Segoe UI Semibold", 8f, FontStyle.Bold);
        public static readonly Font Tiny = new Font("Segoe UI", 7.5f, FontStyle.Regular);
        public static readonly Font Mono = new Font("Consolas", 8.5f, FontStyle.Regular);

        /// <summary>
        /// WinForms divides a Form.ClientSize request by the system DPI scale when
        /// the process is DPI-aware. Pre-multiply so the physical window matches the
        /// fixed design coordinates used by the child controls.
        /// </summary>
        public static Size DpiSize(int width, int height)
        {
            float scale = 1f;
            try
            {
                using (Graphics graphics = Graphics.FromHwnd(IntPtr.Zero))
                {
                    scale = graphics.DpiX / 96f;
                }
            }
            catch
            {
                scale = 1f;
            }

            if (scale <= 0f)
            {
                scale = 1f;
            }
            return new Size(
                (int)Math.Round(width * scale),
                (int)Math.Round(height * scale));
        }

        public static GraphicsPath RoundedRect(Rectangle bounds, int radius)
        {
            int diameter = radius * 2;
            if (diameter <= 0)
            {
                GraphicsPath square = new GraphicsPath();
                square.AddRectangle(bounds);
                return square;
            }

            GraphicsPath path = new GraphicsPath();
            path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    public class CardPanel : Panel
    {
        public int CornerRadius { get; set; }
        public Color BorderColor { get; set; }

        public CardPanel()
        {
            CornerRadius = 10;
            BorderColor = Theme.Border;
            BackColor = Theme.Surface;
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.UserPaint,
                true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = Theme.RoundedRect(rect, CornerRadius))
            {
                using (SolidBrush brush = new SolidBrush(BackColor))
                {
                    e.Graphics.FillPath(brush, path);
                }
                using (Pen pen = new Pen(BorderColor, 1f))
                {
                    e.Graphics.DrawPath(pen, path);
                }
            }
        }
    }

    public class FlatButton : Button
    {
        public FlatButton()
        {
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            FlatAppearance.MouseOverBackColor = Theme.Surface3;
            FlatAppearance.MouseDownBackColor = Theme.Surface3;
            BackColor = Theme.Surface2;
            ForeColor = Theme.Text;
            Font = Theme.BodyBold;
            Cursor = Cursors.Hand;
            UseVisualStyleBackColor = false;
            TabStop = false;
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (Width > 0 && Height > 0)
            {
                using (GraphicsPath path = Theme.RoundedRect(new Rectangle(0, 0, Width, Height), 8))
                {
                    Region = new Region(path);
                }
            }
        }
    }

    public class StatusDot : Control
    {
        public Color DotColor { get; set; }

        public StatusDot()
        {
            DotColor = Theme.Muted;
            Width = 10;
            Height = 10;
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.UserPaint |
                ControlStyles.SupportsTransparentBackColor,
                true);
            BackColor = Color.Transparent;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (SolidBrush brush = new SolidBrush(DotColor))
            {
                e.Graphics.FillEllipse(brush, 0, 0, Width - 1, Height - 1);
            }
        }
    }
}
