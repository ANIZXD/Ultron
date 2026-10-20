using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Ultron.Core;

namespace Ultron
{
    public class MainForm : Form
    {
        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HTCAPTION = 0x2;
        private const int WM_HOTKEY = 0x0312;
        private const int TALK_HOTKEY_ID = 0x554C;
        private const int MOD_ALT = 0x0001;
        private const int MOD_CONTROL = 0x0002;
        private const int VK_SPACE = 0x20;

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, int modifiers, int virtualKey);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private readonly bool _smoke;
        private readonly string _projectRoot;
        private readonly Conversation _conversation;
        private readonly Logger _logger;
        private readonly PermissionManager _permissions;

        private AppSettings _settings;
        private ISpeechInput _speechInput;
        private SpeechOutput _speechOutput;
        private OllamaClient _ollama;
        private CancellationTokenSource _cts;
        private int _requestGeneration;
        private bool _active;
        private bool _muted;
        private bool _processing;
        private bool _streamStarted;
        private bool _usingSapiFallback;
        private DateTime _lastNoiseNotice;
        private UltronState _state;
        private string _micName;
        private string _speakerName;

        private Panel _titleBar;
        private Panel _sidebar;
        private Panel _contentHost;
        private Panel _consolePage;
        private Panel _activityPage;
        private Panel _diagnosticsPage;
        private CardPanel _stateCard;
        private StatusDot _stateDot;
        private Label _stateLabel;
        private Label _permissionLabel;
        private Label _statusDetail;
        private Label _chapterTitle;
        private Label _chapterSubtitle;
        private Label _chipStt;
        private Label _chipLlm;
        private Label _chipTts;
        private Label _chipDiscord;
        private CardPanel _permissionBanner;
        private Label _permissionTitle;
        private Label _permissionText;
        private FlatButton _bannerButton;
        private CardPanel _transcriptCard;
        private RichTextBox _transcript;
        private Label _micLabel;
        private Label _speakerLabel;
        private Label _confidenceLabel;
        private TextBox _typedInput;
        private FlatButton _sendButton;
        private FlatButton _startButton;
        private FlatButton _muteButton;
        private FlatButton _talkButton;
        private FlatButton _interruptButton;
        private FlatButton _takeOverButton;
        private FlatButton _settingsButton;
        private FlatButton _navConsole;
        private FlatButton _navActivity;
        private FlatButton _navDiagnostics;
        private FlatButton _navSettings;
        private RichTextBox _activityLog;
        private RichTextBox _diagnosticsLog;
        private Label _diagOllama;
        private Label _diagStt;
        private Label _diagTts;
        private Label _diagDevices;
        private Label _diagDiscord;
        private FlatButton _diagTestLlm;
        private FlatButton _diagTestVoice;
        private FlatButton _diagTestStt;
        private FlatButton _diagRefresh;
        private System.Windows.Forms.Timer _smokeTimer;

        public MainForm(bool smoke)
        {
            _smoke = smoke;
            _projectRoot = AppDomain.CurrentDomain.BaseDirectory;
            _settings = AppSettings.Load();
            _conversation = new Conversation(_settings.MaxHistory);
            _logger = new Logger();
            _permissions = new PermissionManager();
            _permissions.StateChanged += Permissions_StateChanged;
            _logger.Logged += Logger_Logged;
            _micName = "Default microphone";
            _speakerName = "Default speakers";

            BuildUi();
            InitializeCores();
            ApplySettingsToCores();
            SetState(UltronState.Idle, "Ready. Start a session to allow microphone access.");
            UpdatePermissionUi();
            UpdateDiagnostics();
            AppendIntro();

            Shown += MainForm_Shown;
            FormClosing += MainForm_FormClosing;
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateWindowRegion();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try
            {
                RegisterHotKey(Handle, TALK_HOTKEY_ID, MOD_CONTROL | MOD_ALT, VK_SPACE);
            }
            catch
            {
                // A global shortcut is a convenience; the Talk button still works.
            }
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            try
            {
                UnregisterHotKey(Handle, TALK_HOTKEY_ID);
            }
            catch
            {
                // The window is already going away.
            }
            base.OnHandleDestroyed(e);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == TALK_HOTKEY_ID)
            {
                ToggleTalk();
                return;
            }
            base.WndProc(ref m);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (Pen pen = new Pen(Theme.BorderSoft, 1f))
            {
                e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            }
        }

        private void UpdateWindowRegion()
        {
            if (Width <= 0 || Height <= 0)
            {
                return;
            }

            using (GraphicsPath path = Theme.RoundedRect(new Rectangle(0, 0, Width, Height), 14))
            {
                Region old = Region;
                Region = new Region(path);
                if (old != null)
                {
                    old.Dispose();
                }
            }
        }

        private void BuildUi()
        {
            Text = "Ultron";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(1180, 760);
            BackColor = Theme.Background;
            ForeColor = Theme.Text;
            Font = Theme.Body;
            AutoScaleMode = AutoScaleMode.None;
            DoubleBuffered = true;
            KeyPreview = true;
            UpdateWindowRegion();

            BuildTitleBar();
            BuildSidebar();
            BuildContent();
        }

        private void BuildTitleBar()
        {
            _titleBar = new Panel();
            _titleBar.Bounds = new Rectangle(0, 0, 1180, 48);
            _titleBar.BackColor = Theme.Background;
            _titleBar.MouseDown += DragWindow;
            Controls.Add(_titleBar);

            Panel accent = new Panel();
            accent.Bounds = new Rectangle(20, 15, 4, 18);
            accent.BackColor = Theme.Accent;
            _titleBar.Controls.Add(accent);

            Label brand = MakeLabel(_titleBar, "ULTRON", 32, 8, Theme.Brand, Theme.Text);
            brand.MouseDown += DragWindow;

            Label tagline = MakeLabel(_titleBar, "PRECISION VOICE ASSISTANT", 196, 18, Theme.Tiny, Theme.Faint);
            tagline.MouseDown += DragWindow;

            FlowLayoutPanel buttons = new FlowLayoutPanel();
            buttons.Bounds = new Rectangle(1054, 9, 110, 32);
            buttons.FlowDirection = FlowDirection.RightToLeft;
            buttons.WrapContents = false;
            buttons.BackColor = Theme.Background;
            _titleBar.Controls.Add(buttons);

            FlatButton close = WindowButton("X");
            close.FlatAppearance.MouseOverBackColor = Theme.DangerDark;
            close.Click += delegate { Close(); };
            buttons.Controls.Add(close);

            FlatButton minimize = WindowButton("-");
            minimize.Click += delegate { WindowState = FormWindowState.Minimized; };
            buttons.Controls.Add(minimize);

            Panel edge = new Panel();
            edge.Bounds = new Rectangle(0, 47, 1180, 1);
            edge.BackColor = Theme.BorderSoft;
            _titleBar.Controls.Add(edge);
        }

        private FlatButton WindowButton(string text)
        {
            FlatButton button = new FlatButton();
            button.Text = text;
            button.Size = new Size(34, 28);
            button.Margin = new Padding(2, 0, 0, 0);
            button.BackColor = Theme.Background;
            button.ForeColor = Theme.Muted;
            button.Font = new Font("Segoe UI", 11f, FontStyle.Regular);
            button.TabStop = false;
            return button;
        }

        private void BuildSidebar()
        {
            _sidebar = new Panel();
            _sidebar.Bounds = new Rectangle(0, 48, 232, 712);
            _sidebar.BackColor = Theme.Sidebar;
            Controls.Add(_sidebar);

            Panel edge = new Panel();
            edge.Bounds = new Rectangle(231, 0, 1, 712);
            edge.BackColor = Theme.BorderSoft;
            _sidebar.Controls.Add(edge);

            _stateCard = new CardPanel();
            _stateCard.Bounds = new Rectangle(16, 18, 200, 112);
            _stateCard.BackColor = Theme.Surface;
            _stateCard.BorderColor = Theme.Border;
            _stateCard.CornerRadius = 10;
            _sidebar.Controls.Add(_stateCard);

            _stateDot = new StatusDot();
            _stateDot.Location = new Point(16, 19);
            _stateDot.DotColor = Theme.Muted;
            _stateCard.Controls.Add(_stateDot);

            _stateLabel = new Label();
            _stateLabel.Text = "IDLE";
            _stateLabel.Bounds = new Rectangle(34, 10, 152, 28);
            _stateLabel.Font = new Font("Segoe UI Semibold", 14f, FontStyle.Bold);
            _stateLabel.ForeColor = Theme.Text;
            _stateLabel.BackColor = Theme.Surface;
            _stateLabel.AutoSize = false;
            _stateCard.Controls.Add(_stateLabel);

            _permissionLabel = new Label();
            _permissionLabel.Text = "Permission: Locked";
            _permissionLabel.Bounds = new Rectangle(16, 48, 168, 20);
            _permissionLabel.Font = Theme.Small;
            _permissionLabel.ForeColor = Theme.Muted;
            _permissionLabel.BackColor = Theme.Surface;
            _permissionLabel.AutoSize = false;
            _stateCard.Controls.Add(_permissionLabel);

            _statusDetail = new Label();
            _statusDetail.Text = "Ready.";
            _statusDetail.Bounds = new Rectangle(16, 70, 168, 34);
            _statusDetail.Font = Theme.Tiny;
            _statusDetail.ForeColor = Theme.Faint;
            _statusDetail.BackColor = Theme.Surface;
            _statusDetail.AutoSize = false;
            _stateCard.Controls.Add(_statusDetail);

            MakeLabel(_sidebar, "WORKSPACE", 18, 148, Theme.SmallBold, Theme.Faint);
            _navConsole = NavButton("Console", 168, delegate { ShowPage(_consolePage, _navConsole); });
            _navActivity = NavButton("Activity", 210, delegate { ShowPage(_activityPage, _navActivity); });
            _navDiagnostics = NavButton("Diagnostics", 252, delegate { ShowPage(_diagnosticsPage, _navDiagnostics); });
            _sidebar.Controls.Add(_navConsole);
            _sidebar.Controls.Add(_navActivity);
            _sidebar.Controls.Add(_navDiagnostics);

            _navSettings = NavButton("Settings", 294, OpenSettings);
            _sidebar.Controls.Add(_navSettings);

            Panel divider = new Panel();
            divider.Bounds = new Rectangle(16, 344, 200, 1);
            divider.BackColor = Theme.BorderSoft;
            _sidebar.Controls.Add(divider);

            MakeLabel(_sidebar, "STATUS", 18, 362, Theme.SmallBold, Theme.Faint);
            MakeLabel(_sidebar, "Local-first. No telemetry.", 18, 384, Theme.Tiny, Theme.Muted);
            MakeLabel(_sidebar, "Microphone gated by approval.", 18, 404, Theme.Tiny, Theme.Muted);
            MakeLabel(_sidebar, "DM calls: unsupported.", 18, 424, Theme.Tiny, Theme.Warning);

            Label footer = new Label();
            footer.Text = "ULTRON  v1.0\nBuilt for precision, not pretense.";
            footer.Bounds = new Rectangle(18, 646, 196, 44);
            footer.Font = Theme.Tiny;
            footer.ForeColor = Theme.Faint;
            footer.BackColor = Theme.Sidebar;
            footer.AutoSize = false;
            _sidebar.Controls.Add(footer);
        }

        private FlatButton NavButton(string text, int top, EventHandler onClick)
        {
            FlatButton button = new FlatButton();
            button.Text = text;
            button.Bounds = new Rectangle(16, top, 200, 38);
            button.TextAlign = ContentAlignment.MiddleLeft;
            button.Padding = new Padding(14, 0, 0, 0);
            button.BackColor = Theme.Sidebar;
            button.ForeColor = Theme.Muted;
            button.FlatAppearance.MouseOverBackColor = Theme.Surface2;
            button.Click += onClick;
            return button;
        }

        private void BuildContent()
        {
            _contentHost = new Panel();
            _contentHost.Bounds = new Rectangle(232, 48, 948, 712);
            _contentHost.BackColor = Theme.Background;
            Controls.Add(_contentHost);

            BuildConsolePage();
            BuildActivityPage();
            BuildDiagnosticsPage();

            _consolePage.Visible = true;
            _activityPage.Visible = false;
            _diagnosticsPage.Visible = false;
            SetNav(_navConsole, true);
            SetNav(_navActivity, false);
            SetNav(_navDiagnostics, false);
        }

        private void BuildConsolePage()
        {
            _consolePage = new Panel();
            _consolePage.Bounds = new Rectangle(0, 0, 948, 712);
            _consolePage.BackColor = Theme.Background;
            _contentHost.Controls.Add(_consolePage);

            _chapterTitle = MakeLabel(_consolePage, "Console", 24, 12, Theme.H1, Theme.Text);
            _chapterSubtitle = MakeLabel(_consolePage,
                "Speak naturally. Ultron listens only after you approve a session.",
                26, 64, Theme.Body, Theme.Muted);

            FlowLayoutPanel chips = new FlowLayoutPanel();
            chips.Bounds = new Rectangle(24, 92, 900, 28);
            chips.FlowDirection = FlowDirection.LeftToRight;
            chips.WrapContents = false;
            chips.BackColor = Theme.Background;
            _consolePage.Controls.Add(chips);

            _chipStt = MakeChip(chips, "STT  SAPI en-US", Theme.Muted);
            _chipLlm = MakeChip(chips, "LLM  qwen3:0.6b", Theme.Muted);
            _chipTts = MakeChip(chips, "TTS  Edge Ava", Theme.Muted);
            _chipDiscord = MakeChip(chips, "DISCORD  unsupported", Theme.Warning);

            _permissionBanner = new CardPanel();
            _permissionBanner.Bounds = new Rectangle(24, 130, 900, 88);
            _permissionBanner.BackColor = Theme.Surface2;
            _permissionBanner.BorderColor = Theme.WarningDark;
            _permissionBanner.CornerRadius = 10;
            _consolePage.Controls.Add(_permissionBanner);

            _permissionTitle = MakeLabel(_permissionBanner, "Session locked", 18, 12, Theme.H2, Theme.Warning);
            _permissionText = new Label();
            _permissionText.Text = "Ultron cannot listen or speak until you approve this session. " +
                                  "Approval is explicit and revocable.";
            _permissionText.Bounds = new Rectangle(18, 40, 650, 36);
            _permissionText.Font = Theme.Small;
            _permissionText.ForeColor = Theme.Muted;
            _permissionText.BackColor = Theme.Surface2;
            _permissionText.AutoSize = false;
            _permissionBanner.Controls.Add(_permissionText);

            _bannerButton = MakeButton(_permissionBanner, "Start session", 742, 26, 140, 36, Theme.Accent);
            _bannerButton.ForeColor = Color.FromArgb(4, 20, 19);
            _bannerButton.Click += StartSession_Click;

            _transcriptCard = new CardPanel();
            _transcriptCard.Bounds = new Rectangle(24, 230, 900, 314);
            _transcriptCard.BackColor = Theme.Surface;
            _transcriptCard.BorderColor = Theme.Border;
            _transcriptCard.CornerRadius = 10;
            _consolePage.Controls.Add(_transcriptCard);

            MakeLabel(_transcriptCard, "CONVERSATION", 18, 14, Theme.SmallBold, Theme.Muted);
            FlatButton clear = MakeButton(_transcriptCard, "Clear", 820, 10, 62, 26, Theme.Surface2);
            clear.Font = Theme.Small;
            clear.ForeColor = Theme.Muted;
            clear.Click += ClearTranscript_Click;

            Panel line = new Panel();
            line.Bounds = new Rectangle(18, 38, 864, 1);
            line.BackColor = Theme.BorderSoft;
            _transcriptCard.Controls.Add(line);

            _transcript = new RichTextBox();
            _transcript.Bounds = new Rectangle(18, 44, 864, 252);
            _transcript.BackColor = Theme.Surface;
            _transcript.ForeColor = Theme.Text;
            _transcript.BorderStyle = BorderStyle.None;
            _transcript.Font = Theme.Body;
            _transcript.ReadOnly = true;
            _transcript.DetectUrls = false;
            _transcript.HideSelection = false;
            _transcript.ScrollBars = RichTextBoxScrollBars.Vertical;
            _transcriptCard.Controls.Add(_transcript);

            Panel statusLine = new Panel();
            statusLine.Bounds = new Rectangle(24, 548, 900, 1);
            statusLine.BackColor = Theme.BorderSoft;
            _consolePage.Controls.Add(statusLine);

            _micLabel = MakeLabel(_consolePage, "MIC  Default microphone", 26, 554, Theme.Small, Theme.Muted);
            _speakerLabel = MakeLabel(_consolePage, "OUT  Default speakers", 360, 554, Theme.Small, Theme.Muted);
            _confidenceLabel = MakeLabel(_consolePage, "LAST  -", 700, 554, Theme.Small, Theme.Faint);

            MakeLabel(_consolePage, "TYPE INSTEAD", 26, 574, Theme.Tiny, Theme.Faint);
            _typedInput = new TextBox();
            _typedInput.Bounds = new Rectangle(24, 590, 760, 32);
            _typedInput.BackColor = Theme.Surface2;
            _typedInput.ForeColor = Theme.Text;
            _typedInput.BorderStyle = BorderStyle.FixedSingle;
            _typedInput.Font = Theme.Body;
            _typedInput.MaxLength = 600;
            _typedInput.KeyDown += TypedInput_KeyDown;
            _consolePage.Controls.Add(_typedInput);

            _sendButton = MakeButton(_consolePage, "Send", 792, 588, 110, 36, Theme.Surface2);
            _sendButton.ForeColor = Theme.Text;
            _sendButton.Click += SendTyped_Click;

            _startButton = MakeButton(_consolePage, "Start session", 24, 636, 170, 44, Theme.Accent);
            _startButton.ForeColor = Color.FromArgb(4, 20, 19);
            _startButton.Click += StartSession_Click;

            _muteButton = MakeButton(_consolePage, "Mute", 204, 636, 104, 44, Theme.Surface2);
            _muteButton.Click += Mute_Click;

            _interruptButton = MakeButton(_consolePage, "Interrupt", 318, 636, 110, 44, Theme.Surface2);
            _interruptButton.ForeColor = Theme.Danger;
            _interruptButton.Click += Interrupt_Click;

            _takeOverButton = MakeButton(_consolePage, "Take over", 438, 636, 120, 44, Theme.Surface2);
            _takeOverButton.ForeColor = Theme.Warning;
            _takeOverButton.Click += TakeOver_Click;

            _talkButton = MakeButton(_consolePage, "Talk", 568, 636, 110, 44, Theme.Surface2);
            _talkButton.ForeColor = Theme.Accent;
            _talkButton.Click += Talk_Click;

            _settingsButton = MakeButton(_consolePage, "Settings", 688, 636, 110, 44, Theme.Surface2);
            _settingsButton.Click += OpenSettings;

            Label foot = new Label();
            foot.Text = "Ctrl+Alt+Space to talk. Voice is gated by approval.";
            foot.Bounds = new Rectangle(24, 690, 900, 18);
            foot.Font = Theme.Tiny;
            foot.ForeColor = Theme.Faint;
            foot.BackColor = Theme.Background;
            foot.AutoSize = false;
            _consolePage.Controls.Add(foot);
        }

        private void BuildActivityPage()
        {
            _activityPage = new Panel();
            _activityPage.Bounds = new Rectangle(0, 0, 948, 712);
            _activityPage.BackColor = Theme.Background;
            _contentHost.Controls.Add(_activityPage);

            MakeLabel(_activityPage, "Activity", 24, 12, Theme.H1, Theme.Text);
            MakeLabel(_activityPage, "Event log. Conversation text is never written to disk.",
                26, 64, Theme.Body, Theme.Muted);

            CardPanel card = new CardPanel();
            card.Bounds = new Rectangle(24, 88, 900, 600);
            card.BackColor = Theme.Surface;
            card.BorderColor = Theme.Border;
            card.CornerRadius = 10;
            _activityPage.Controls.Add(card);
            MakeLabel(card, "ACTIVITY LOG", 18, 14, Theme.SmallBold, Theme.Muted);

            _activityLog = new RichTextBox();
            _activityLog.Bounds = new Rectangle(18, 44, 864, 538);
            _activityLog.BackColor = Theme.Surface;
            _activityLog.ForeColor = Theme.Muted;
            _activityLog.BorderStyle = BorderStyle.None;
            _activityLog.Font = Theme.Mono;
            _activityLog.ReadOnly = true;
            _activityLog.DetectUrls = false;
            _activityLog.ScrollBars = RichTextBoxScrollBars.Vertical;
            card.Controls.Add(_activityLog);
        }

        private void BuildDiagnosticsPage()
        {
            _diagnosticsPage = new Panel();
            _diagnosticsPage.Bounds = new Rectangle(0, 0, 948, 712);
            _diagnosticsPage.BackColor = Theme.Background;
            _contentHost.Controls.Add(_diagnosticsPage);

            MakeLabel(_diagnosticsPage, "Diagnostics", 24, 12, Theme.H1, Theme.Text);
            MakeLabel(_diagnosticsPage, "Verified environment and live checks.",
                26, 64, Theme.Body, Theme.Muted);

            _diagTestLlm = MakeButton(_diagnosticsPage, "Test LLM", 24, 84, 118, 34, Theme.Surface2);
            _diagTestLlm.Click += TestLlm_Click;
            _diagTestVoice = MakeButton(_diagnosticsPage, "Test voice", 150, 84, 118, 34, Theme.Surface2);
            _diagTestVoice.Click += TestVoice_Click;
            _diagTestStt = MakeButton(_diagnosticsPage, "Test STT", 276, 84, 118, 34, Theme.Surface2);
            _diagTestStt.Click += TestStt_Click;
            _diagRefresh = MakeButton(_diagnosticsPage, "Refresh", 402, 84, 108, 34, Theme.Surface2);
            _diagRefresh.Click += RefreshDiagnostics_Click;

            CardPanel card = new CardPanel();
            card.Bounds = new Rectangle(24, 130, 900, 280);
            card.BackColor = Theme.Surface;
            card.BorderColor = Theme.Border;
            card.CornerRadius = 10;
            _diagnosticsPage.Controls.Add(card);
            MakeLabel(card, "ENVIRONMENT", 18, 14, Theme.SmallBold, Theme.Muted);

            _diagOllama = DiagnosticLabel(card, 18, 42, 860, 22);
            _diagStt = DiagnosticLabel(card, 18, 68, 860, 22);
            _diagTts = DiagnosticLabel(card, 18, 94, 860, 22);
            _diagDevices = DiagnosticLabel(card, 18, 120, 860, 44);
            _diagDiscord = DiagnosticLabel(card, 18, 174, 860, 88);
            _diagDiscord.Font = Theme.Small;

            CardPanel output = new CardPanel();
            output.Bounds = new Rectangle(24, 422, 900, 264);
            output.BackColor = Theme.Surface;
            output.BorderColor = Theme.Border;
            output.CornerRadius = 10;
            _diagnosticsPage.Controls.Add(output);
            MakeLabel(output, "CHECK OUTPUT", 18, 14, Theme.SmallBold, Theme.Muted);

            _diagnosticsLog = new RichTextBox();
            _diagnosticsLog.Bounds = new Rectangle(18, 44, 864, 204);
            _diagnosticsLog.BackColor = Theme.Surface;
            _diagnosticsLog.ForeColor = Theme.Muted;
            _diagnosticsLog.BorderStyle = BorderStyle.None;
            _diagnosticsLog.Font = Theme.Mono;
            _diagnosticsLog.ReadOnly = true;
            _diagnosticsLog.DetectUrls = false;
            _diagnosticsLog.ScrollBars = RichTextBoxScrollBars.Vertical;
            output.Controls.Add(_diagnosticsLog);
        }

        private Label DiagnosticLabel(Control parent, int x, int y, int width, int height)
        {
            Label label = new Label();
            label.Bounds = new Rectangle(x, y, width, height);
            label.Font = Theme.Small;
            label.ForeColor = Theme.Muted;
            label.BackColor = parent.BackColor;
            label.AutoSize = false;
            label.Text = "...";
            parent.Controls.Add(label);
            return label;
        }

        private void ShowPage(Panel page, FlatButton nav)
        {
            _consolePage.Visible = page == _consolePage;
            _activityPage.Visible = page == _activityPage;
            _diagnosticsPage.Visible = page == _diagnosticsPage;

            SetNav(_navConsole, page == _consolePage);
            SetNav(_navActivity, page == _activityPage);
            SetNav(_navDiagnostics, page == _diagnosticsPage);
        }

        private void SetNav(FlatButton button, bool selected)
        {
            button.BackColor = selected ? Theme.Surface2 : Theme.Sidebar;
            button.ForeColor = selected ? Theme.Accent : Theme.Muted;
        }

        private void InitializeCores()
        {
            CreateSpeechInput();

            _speechOutput = new SpeechOutput(_projectRoot, _settings.Voice, _settings.RatePercent);
            _speechOutput.StatusChanged += SpeechOutput_StatusChanged;

            _ollama = new OllamaClient(_settings.OllamaUrl, _settings.Model);

            Task.Run(delegate
            {
                string mic = AudioDevices.DefaultInputName();
                string speaker = AudioDevices.DefaultOutputName();
                UI(delegate
                {
                    _micName = mic;
                    _speakerName = speaker;
                    _micLabel.Text = "MIC  " + mic;
                    _speakerLabel.Text = "OUT  " + speaker;
                    UpdateDiagnostics();
                });
            });
        }

        private void CreateSpeechInput()
        {
            ISpeechInput previous = _speechInput;
            if (previous != null)
            {
                previous.SpeechHeard -= SpeechInput_SpeechHeard;
                previous.StatusChanged -= SpeechInput_StatusChanged;
                previous.ListeningStopped -= SpeechInput_ListeningStopped;
                WindowsSpeechInput previousWindows = previous as WindowsSpeechInput;
                if (previousWindows != null)
                {
                    previousWindows.Failed -= SpeechInput_Failed;
                }
                try { previous.Dispose(); } catch { }
            }

            _speechInput = null;
            _usingSapiFallback = false;

            if (string.Equals(_settings.SpeechEngine, "windows", StringComparison.OrdinalIgnoreCase) &&
                WindowsSpeechInput.IsSupported)
            {
                try
                {
                    WindowsSpeechInput windowsInput = new WindowsSpeechInput();
                    windowsInput.Failed += SpeechInput_Failed;
                    _speechInput = windowsInput;
                }
                catch (Exception exception)
                {
                    _logger.Error("Windows Speech initialization failed", exception);
                }
            }

            if (_speechInput == null)
            {
                try
                {
                    _speechInput = new SpeechInput();
                    _usingSapiFallback = string.Equals(_settings.SpeechEngine, "windows",
                        StringComparison.OrdinalIgnoreCase);
                }
                catch (Exception exception)
                {
                    _speechInput = null;
                    _logger.Error("Speech input initialization failed", exception);
                }
            }

            if (_speechInput != null)
            {
                _speechInput.SpeechHeard += SpeechInput_SpeechHeard;
                _speechInput.StatusChanged += SpeechInput_StatusChanged;
                _speechInput.ListeningStopped += SpeechInput_ListeningStopped;
            }
        }

        private void ApplySettingsToCores()
        {
            if (_ollama != null)
            {
                _ollama.BaseUrl = _settings.OllamaUrl;
                _ollama.Model = _settings.Model;
            }
            if (_speechOutput != null)
            {
                _speechOutput.Voice = _settings.Voice;
                _speechOutput.RatePercent = _settings.RatePercent;
            }
            _conversation.MaxHistory = _settings.MaxHistory;
            UpdateChips();
            UpdateDiagnostics();
        }

        private void UpdateChips()
        {
            if (_chipStt == null)
            {
                return;
            }

            string stt = "unavailable";
            if (_speechInput != null && _speechInput.IsAvailable)
            {
                if (_speechInput is WindowsSpeechInput && !_usingSapiFallback)
                {
                    stt = "Windows en-US";
                }
                else
                {
                    stt = _usingSapiFallback ? "SAPI fallback" : "SAPI en-US";
                }
            }
            _chipStt.Text = "STT  " + stt;
            _chipStt.ForeColor = _speechInput != null && _speechInput.IsAvailable
                ? (_usingSapiFallback ? Theme.Warning : Theme.Muted)
                : Theme.Warning;
            _chipLlm.Text = "LLM  " + _settings.Model;
            _chipTts.Text = "TTS  " + VoiceShort(_settings.Voice);
            _chipDiscord.Text = "DISCORD  unsupported";
        }

        private void UpdateDiagnostics()
        {
            if (_diagOllama == null)
            {
                return;
            }

            _diagOllama.Text = "OLLAMA   " + _settings.OllamaUrl + "   |   " + _settings.Model;
            if (_speechInput == null || !_speechInput.IsAvailable)
            {
                _diagStt.Text = "STT      Unavailable";
            }
            else if (_usingSapiFallback)
            {
                _diagStt.Text = "STT      " + _speechInput.RecognizerName +
                                "   |   Windows Speech needs its privacy setting; offline fallback active";
            }
            else if (_speechInput is WindowsSpeechInput)
            {
                _diagStt.Text = "STT      " + _speechInput.RecognizerName +
                                "   |   may use Microsoft speech services";
            }
            else
            {
                _diagStt.Text = "STT      " + _speechInput.RecognizerName + "   |   offline SAPI";
            }
            _diagTts.Text = "TTS      " + VoiceLabel(_settings.Voice) + "   |   Edge first, SAPI fallback";
            _diagDevices.Text = "MIC      " + _micName + "\nOUT      " + _speakerName;
            _diagDiscord.Text = "DISCORD  " + DiscordAdapter.Summary;
        }

        private static string VoiceShort(string voice)
        {
            if (string.IsNullOrEmpty(voice))
            {
                return "none";
            }
            if (voice.StartsWith("sapi:", StringComparison.OrdinalIgnoreCase))
            {
                return "SAPI offline";
            }
            if (voice.IndexOf("Ava", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Edge Ava";
            }
            if (voice.IndexOf("Andrew", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Edge Andrew";
            }
            if (voice.IndexOf("Emma", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Edge Emma";
            }
            return "custom";
        }

        private static string VoiceLabel(string voice)
        {
            if (string.IsNullOrEmpty(voice))
            {
                return "none";
            }
            if (voice.StartsWith("sapi:", StringComparison.OrdinalIgnoreCase))
            {
                return voice.Substring(5);
            }
            if (voice.IndexOf("Ava", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Ava (Edge neural)";
            }
            if (voice.IndexOf("Andrew", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Andrew (Edge neural)";
            }
            if (voice.IndexOf("Emma", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Emma (Edge neural)";
            }
            return voice;
        }

        private void AppendIntro()
        {
            AppendSystem("Session idle. Start a session to allow microphone access.");
            AppendSystem("Typed input is available after approval. Nothing is sent until you approve.");
        }

        private void StartSession_Click(object sender, EventArgs e)
        {
            if (_active)
            {
                StopSession();
                return;
            }

            if (_permissions.State != PermissionState.Pending)
            {
                if (!_permissions.RequestStart())
                {
                    return;
                }
            }

            UpdatePermissionUi();
            using (PermissionDialog dialog = new PermissionDialog(
                _usingSapiFallback ? "sapi" : _settings.SpeechEngine))
            {
                DialogResult result = dialog.ShowDialog(this);
                _logger.Info("Permission", "Dialog result: " + result +
                            ", approved by button: " + dialog.Approved);
                if (result == DialogResult.OK && dialog.Approved && _permissions.Approve())
                {
                    _active = true;
                    _muted = false;
                    _conversation.Clear();
                    _streamStarted = false;
                    AppendSystem("Session approved. Microphone access is available.");
                    UpdatePermissionUi();
                    if (_settings.PushToTalk)
                    {
                        SetState(UltronState.Idle,
                            "Ready. Press Talk or Ctrl+Alt+Space to speak.");
                    }
                    else
                    {
                        SetState(UltronState.Listening, "Listening...");
                        StartListening();
                    }
                }
                else
                {
                    _permissions.Decline();
                    UpdatePermissionUi();
                    SetState(UltronState.Idle, "Permission declined.");
                }
            }
        }

        private void StopSession()
        {
            _active = false;
            _muted = false;
            _requestGeneration++;
            CancelCurrent();
            StopListening();
            if (_speechOutput != null)
            {
                _speechOutput.Stop();
            }
            _permissions.Reset();
            SetState(UltronState.Idle, "Session ended.");
            UpdatePermissionUi();
            AppendSystem("Session ended. Microphone released.");
        }

        private void Mute_Click(object sender, EventArgs e)
        {
            if (!_active)
            {
                return;
            }

            _muted = !_muted;
            if (_muted)
            {
                _requestGeneration++;
                CancelCurrent();
                StopListening();
                if (_speechOutput != null)
                {
                    _speechOutput.Stop();
                }
                SetState(UltronState.Muted, "Microphone and voice are muted.");
            }
            else
            {
                if (_settings.PushToTalk)
                {
                    SetState(UltronState.Idle, "Ready. Press Talk or Ctrl+Alt+Space to speak.");
                }
                else
                {
                    SetState(UltronState.Listening, "Listening...");
                    StartListening();
                }
            }
            UpdateControls();
        }

        private void Interrupt_Click(object sender, EventArgs e)
        {
            _requestGeneration++;
            CancelCurrent();
            if (_speechOutput != null)
            {
                _speechOutput.Stop();
            }
            if (_active)
            {
                if (_settings.PushToTalk)
                {
                    SetState(UltronState.Idle, "Interrupted. Press Talk to speak.");
                }
                else
                {
                    SetState(UltronState.Listening, "Interrupted. Listening...");
                    StartListening();
                }
            }
            AppendSystem("Interrupted.");
        }

        private void TakeOver_Click(object sender, EventArgs e)
        {
            _active = false;
            _muted = false;
            _requestGeneration++;
            CancelCurrent();
            StopListening();
            if (_speechOutput != null)
            {
                _speechOutput.Stop();
            }
            _permissions.Revoke();
            SetState(UltronState.TakenOver, "You have taken over. Ultron is silent.");
            UpdatePermissionUi();
            AppendSystem("Take over engaged. Microphone released and permission revoked.");
        }

        private void SendTyped_Click(object sender, EventArgs e)
        {
            SendTyped();
        }

        private void TypedInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                SendTyped();
            }
        }

        private void SendTyped()
        {
            if (!_active || !_permissions.CanSpeak)
            {
                AppendSystem("Start and approve a session before sending text.");
                return;
            }
            if (_processing)
            {
                return;
            }

            string text = _typedInput.Text.Trim();
            if (text.Length == 0)
            {
                return;
            }
            _typedInput.Clear();
            ProcessUserText(text);
        }

        private void Talk_Click(object sender, EventArgs e)
        {
            ToggleTalk();
        }

        private void ToggleTalk()
        {
            if (!_active || !_permissions.CanSpeak)
            {
                AppendSystem("Start and approve a session before talking.");
                return;
            }
            if (_processing || _state == UltronState.Thinking || _state == UltronState.Speaking)
            {
                AppendSystem("Wait for the current reply, or press Interrupt.");
                return;
            }
            if (_speechInput == null || !_speechInput.IsAvailable)
            {
                SetState(UltronState.Error, "Microphone unavailable. Type instead.");
                return;
            }

            if (_speechInput.IsListening)
            {
                StopListening();
                SetState(UltronState.Idle, "Microphone closed.");
            }
            else
            {
                StartListening();
                if (_speechInput.IsListening)
                {
                    SetState(UltronState.Listening, "Listening. Speak one phrase.");
                }
            }
            UpdateControls();
        }

        private void ClearTranscript_Click(object sender, EventArgs e)
        {
            if (_processing)
            {
                AppendSystem("Wait for the current response, or press Interrupt before clearing.");
                return;
            }
            _transcript.Clear();
            _conversation.Clear();
            _streamStarted = false;
            AppendSystem("Transcript and conversation context cleared.");
        }

        private void StartListening()
        {
            if (!_active || _muted || !_permissions.CanSpeak)
            {
                return;
            }
            if (_speechInput == null || !_speechInput.IsAvailable)
            {
                SetState(UltronState.Error, "Microphone unavailable. Type instead.");
                return;
            }
            if (_speechInput.IsListening)
            {
                return;
            }

            try
            {
                _speechInput.Start();
            }
            catch (Exception exception)
            {
                _logger.Error("Microphone start failed", exception);
                AppendSystem("Microphone unavailable: " + Redaction.Redact(exception.Message) + " Type instead.");
                SetState(UltronState.Error, "Microphone unavailable. Type instead.");
            }
        }

        private void StopListening()
        {
            if (_speechInput != null)
            {
                _speechInput.Stop();
            }
        }

        private void CancelCurrent()
        {
            CancellationTokenSource cts = _cts;
            _cts = null;
            _processing = false;
            if (cts != null)
            {
                try { cts.Cancel(); } catch { }
            }
            UpdateControls();
        }

        private void SpeechInput_SpeechHeard(object sender, SpeechHeardEventArgs e)
        {
            UI(delegate
            {
                if (!_active || _muted || !_permissions.CanSpeak)
                {
                    return;
                }
                if (_state == UltronState.Thinking || _state == UltronState.Speaking)
                {
                    return;
                }

                // One result per microphone press. This is the key defense
                // against a room full of background speech.
                StopListening();

                if (NoiseFilter.IsLikelyNoise(e.Text))
                {
                    _confidenceLabel.Text = "LAST  ignored";
                    _logger.Info("STT", "Ignored noise candidate: \"" + e.Text + "\"");
                    DateTime now = DateTime.UtcNow;
                    if ((now - _lastNoiseNotice).TotalSeconds > 4)
                    {
                        _lastNoiseNotice = now;
                        AppendSystem("Ignored background noise. Press Talk to try again.");
                    }
                    ReturnToReadyOrListen("Ignored background noise. Press Talk to try again.");
                    return;
                }

                _confidenceLabel.Text = "LAST  " + e.Confidence.ToString("P0");
                if (e.Confidence < _settings.ConfidenceThreshold)
                {
                    _logger.Info("STT", "Ignored low-confidence result: \"" + e.Text + "\" at " +
                                       e.Confidence.ToString("P0"));
                    AppendSystem("Low-confidence speech ignored. Press Talk to retry or type.");
                    ReturnToReadyOrListen("Low confidence. Press Talk to retry or type.");
                    return;
                }

                double reviewThreshold = Math.Min(0.85, _settings.ConfidenceThreshold + 0.15);
                if (_settings.ReviewLowConfidence && e.Confidence < reviewThreshold)
                {
                    using (TranscriptReviewDialog dialog = new TranscriptReviewDialog(e.Text, e.Confidence))
                    {
                        if (dialog.ShowDialog(this) == DialogResult.OK && dialog.Transcript.Length > 0)
                        {
                            ProcessUserText(dialog.Transcript);
                        }
                        else
                        {
                            ReturnToReadyOrListen("Discarded. Press Talk to try again or type.");
                        }
                    }
                    return;
                }

                ProcessUserText(e.Text);
            });
        }

        private void ReturnToReadyOrListen(string detail)
        {
            if (!_active || _muted || !_permissions.CanSpeak)
            {
                return;
            }
            if (_settings.PushToTalk)
            {
                SetState(UltronState.Idle, detail);
            }
            else
            {
                SetState(UltronState.Listening, "Listening...");
                StartListening();
            }
            UpdateControls();
        }

        private void SpeechInput_StatusChanged(object sender, string message)
        {
            UI(delegate
            {
                _statusDetail.Text = message;
                _logger.Info("STT", message);
            });
        }

        private void SpeechInput_ListeningStopped(object sender, string message)
        {
            UI(delegate
            {
                _logger.Info("STT", message);
                _statusDetail.Text = message;
                if (_processing || _state != UltronState.Listening)
                {
                    return;
                }
                ReturnToReadyOrListen(message);
            });
        }

        private void SpeechInput_Failed(object sender, SpeechErrorEventArgs e)
        {
            UI(delegate
            {
                _logger.Error("STT", "Windows Speech failed: " + e.Message);
                if (e.PrivacyConsentRequired)
                {
                    AppendSystem("Windows Speech needs the Windows speech privacy setting. " +
                                 "Open Settings > Privacy & security > Speech and enable Online speech recognition. " +
                                 "Using offline SAPI until then.");
                }
                else
                {
                    AppendSystem("Windows Speech unavailable: " + e.Message +
                                 " Using offline SAPI instead.");
                }

                FallbackToSapi();
                if (_state != UltronState.TakenOver)
                {
                    SetState(UltronState.Idle, "Ready. Press Talk to speak with offline SAPI.");
                }
            });
        }

        private void FallbackToSapi()
        {
            if (_speechInput is SpeechInput)
            {
                return;
            }

            ISpeechInput previous = _speechInput;
            if (previous != null)
            {
                previous.SpeechHeard -= SpeechInput_SpeechHeard;
                previous.StatusChanged -= SpeechInput_StatusChanged;
                previous.ListeningStopped -= SpeechInput_ListeningStopped;
                WindowsSpeechInput windows = previous as WindowsSpeechInput;
                if (windows != null)
                {
                    windows.Failed -= SpeechInput_Failed;
                }
                try { previous.Dispose(); } catch { }
            }

            try
            {
                _speechInput = new SpeechInput();
                _speechInput.SpeechHeard += SpeechInput_SpeechHeard;
                _speechInput.StatusChanged += SpeechInput_StatusChanged;
                _speechInput.ListeningStopped += SpeechInput_ListeningStopped;
                _usingSapiFallback = true;
            }
            catch (Exception exception)
            {
                _speechInput = null;
                _logger.Error("SAPI fallback initialization failed", exception);
                SetState(UltronState.Error, "Microphone unavailable. Type instead.");
            }

            UpdateChips();
            UpdateDiagnostics();
        }

        private void SpeechOutput_StatusChanged(object sender, string message)
        {
            UI(delegate
            {
                _statusDetail.Text = message;
            });
        }

        private async void ProcessUserText(string text)
        {
            if (!_active || !_permissions.CanSpeak || string.IsNullOrWhiteSpace(text) || _processing)
            {
                return;
            }

            _processing = true;
            StopListening();
            _conversation.AddUser(text);
            AppendTranscript("You", text, Theme.UserColor);
            SetState(UltronState.Thinking, "Thinking with " + _settings.Model + "...");
            UpdateControls();

            int generation = ++_requestGeneration;
            CancellationTokenSource cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromSeconds(90));
            _cts = cts;
            _streamStarted = false;
            string reply = null;

            try
            {
                reply = await _ollama.ChatAsync(
                    _settings.SystemPrompt,
                    _conversation.Messages,
                    delegate(string delta)
                    {
                        UI(delegate
                        {
                            if (generation != _requestGeneration)
                            {
                                return;
                            }
                            if (!_streamStarted)
                            {
                                AppendStreamingStart();
                            }
                            AppendStreamingDelta(delta);
                        });
                    },
                    cts.Token);

                if (generation != _requestGeneration)
                {
                    return;
                }

                bool hadStream = _streamStarted;
                EndStreamingTranscript();
                if (!hadStream)
                {
                    AppendTranscript("ULTRON", reply, Theme.Accent);
                }
                _conversation.AddAssistant(reply);

                SetState(UltronState.Speaking, "Speaking.");
                await _speechOutput.SpeakAsync(reply, cts.Token);

                if (generation != _requestGeneration)
                {
                    return;
                }
                if (_active && !_muted && _permissions.CanSpeak)
                {
                    if (_settings.PushToTalk)
                    {
                        SetState(UltronState.Idle, "Ready. Press Talk or Ctrl+Alt+Space to speak.");
                    }
                    else
                    {
                        SetState(UltronState.Listening, "Listening...");
                        StartListening();
                    }
                }
            }
            catch (OperationCanceledException)
            {
                if (generation == _requestGeneration)
                {
                    EndStreamingTranscript();
                    if (_active && _permissions.CanSpeak && !_muted)
                    {
                        if (_settings.PushToTalk)
                        {
                            SetState(UltronState.Idle, "Interrupted or timed out. Press Talk to retry.");
                        }
                        else
                        {
                            SetState(UltronState.Listening, "Interrupted or timed out. Listening...");
                            StartListening();
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                if (generation == _requestGeneration)
                {
                    EndStreamingTranscript();
                    _logger.Error("LLM/TTS request failed", exception);
                    AppendSystem("Request failed: " + Redaction.Redact(exception.Message));
                    SetState(UltronState.Error, "Request failed. Check Diagnostics.");
                }
            }
            finally
            {
                if (generation == _requestGeneration)
                {
                    _processing = false;
                    if (_cts == cts)
                    {
                        _cts = null;
                    }
                    UpdateControls();
                }
                cts.Dispose();
            }
        }

        private void AppendTranscript(string role, string text, Color roleColor)
        {
            if (_transcript == null)
            {
                return;
            }

            if (_transcript.TextLength > 0)
            {
                _transcript.AppendText(Environment.NewLine);
            }

            _transcript.SelectionStart = _transcript.TextLength;
            _transcript.SelectionColor = Theme.Faint;
            _transcript.SelectionFont = Theme.Tiny;
            _transcript.AppendText(DateTime.Now.ToString("HH:mm") + "   ");

            _transcript.SelectionColor = roleColor;
            _transcript.SelectionFont = Theme.BodyBold;
            _transcript.AppendText(role.ToUpperInvariant() + Environment.NewLine);

            _transcript.SelectionColor = Theme.Text;
            _transcript.SelectionFont = Theme.Body;
            _transcript.AppendText(text + Environment.NewLine);
            _transcript.SelectionStart = _transcript.TextLength;
            _transcript.ScrollToCaret();
        }

        private void AppendSystem(string text)
        {
            AppendTranscript("System", text, Theme.Muted);
        }

        private void AppendStreamingStart()
        {
            if (_transcript == null)
            {
                return;
            }

            if (_transcript.TextLength > 0)
            {
                _transcript.AppendText(Environment.NewLine);
            }

            _transcript.SelectionStart = _transcript.TextLength;
            _transcript.SelectionColor = Theme.Faint;
            _transcript.SelectionFont = Theme.Tiny;
            _transcript.AppendText(DateTime.Now.ToString("HH:mm") + "   ");
            _transcript.SelectionColor = Theme.Accent;
            _transcript.SelectionFont = Theme.BodyBold;
            _transcript.AppendText("ULTRON" + Environment.NewLine);
            _transcript.SelectionColor = Theme.Text;
            _transcript.SelectionFont = Theme.Body;
            _streamStarted = true;
        }

        private void AppendStreamingDelta(string delta)
        {
            if (_transcript == null || string.IsNullOrEmpty(delta))
            {
                return;
            }

            _transcript.SelectionStart = _transcript.TextLength;
            _transcript.SelectionColor = Theme.Text;
            _transcript.SelectionFont = Theme.Body;
            _transcript.AppendText(delta);
            _transcript.SelectionStart = _transcript.TextLength;
            _transcript.ScrollToCaret();
        }

        private void EndStreamingTranscript()
        {
            if (_streamStarted && _transcript != null)
            {
                _transcript.AppendText(Environment.NewLine + Environment.NewLine);
                _transcript.SelectionStart = _transcript.TextLength;
                _transcript.ScrollToCaret();
            }
            _streamStarted = false;
        }

        private void OpenSettings(object sender, EventArgs e)
        {
            string previousEngine = _settings.SpeechEngine;
            bool previousPushToTalk = _settings.PushToTalk;
            using (SettingsDialog dialog = new SettingsDialog(_settings))
            {
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    _settings = dialog.Settings;
                    try
                    {
                        _settings.Save();
                    }
                    catch (Exception exception)
                    {
                        _logger.Error("Settings save failed", exception);
                        AppendSystem("Could not save settings: " + Redaction.Redact(exception.Message));
                    }
                    ApplySettingsToCores();
                    bool engineChanged = !string.Equals(previousEngine, _settings.SpeechEngine,
                        StringComparison.OrdinalIgnoreCase);
                    bool pushToTalkChanged = previousPushToTalk != _settings.PushToTalk;
                    bool retryWindows = _usingSapiFallback &&
                        string.Equals(_settings.SpeechEngine, "windows",
                            StringComparison.OrdinalIgnoreCase);
                    if (engineChanged || pushToTalkChanged || retryWindows)
                    {
                        StopListening();
                        if (engineChanged || retryWindows)
                        {
                            CreateSpeechInput();
                        }
                        if (_active)
                        {
                            if (_settings.PushToTalk)
                            {
                                SetState(UltronState.Idle, "Ready. Press Talk to speak.");
                            }
                            else
                            {
                                SetState(UltronState.Listening, "Listening...");
                                StartListening();
                            }
                        }
                    }
                    AppendSystem("Settings saved. New voice, engine, and model apply to the next message.");
                }
            }
        }

        private async void TestLlm_Click(object sender, EventArgs e)
        {
            _diagTestLlm.Enabled = false;
            AppendDiagnostic("Testing Ollama at " + _settings.OllamaUrl + "...");
            try
            {
                using (CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(45)))
                {
                    List<string> models = await _ollama.ListModelsAsync(cts.Token);
                    AppendDiagnostic("Ollama reachable. Models: " + (models.Count == 0 ? "(none)" : string.Join(", ", models.ToArray())));

                    List<ChatMessage> history = new List<ChatMessage>();
                    history.Add(new ChatMessage("user", "Reply with exactly: ULTRON ONLINE"));
                    string reply = await _ollama.ChatAsync(
                        "You are a precise systems check. Reply with exactly the requested text.",
                        history,
                        null,
                        cts.Token);
                    AppendDiagnostic("Chat check: " + reply);
                }
            }
            catch (Exception exception)
            {
                AppendDiagnostic("LLM check failed: " + Redaction.Redact(exception.Message));
            }
            finally
            {
                _diagTestLlm.Enabled = true;
            }
        }

        private async void TestVoice_Click(object sender, EventArgs e)
        {
            if (!_permissions.CanSpeak)
            {
                AppendDiagnostic("Voice test blocked: start and approve a session first.");
                return;
            }

            _diagTestVoice.Enabled = false;
            AppendDiagnostic("Testing voice: " + VoiceLabel(_settings.Voice));
            try
            {
                await _speechOutput.SpeakAsync("Ultron voice test. Systems precise.", CancellationToken.None);
                AppendDiagnostic("Voice test completed.");
            }
            catch (Exception exception)
            {
                AppendDiagnostic("Voice test failed: " + Redaction.Redact(exception.Message));
            }
            finally
            {
                _diagTestVoice.Enabled = true;
            }
        }

        private async void TestStt_Click(object sender, EventArgs e)
        {
            _diagTestStt.Enabled = false;
            AppendDiagnostic("Testing STT with a generated WAV (no microphone needed)...");
            string wav = null;
            try
            {
                using (CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(60)))
                {
                    TtsResult tts = await TtsHelper.SynthesizeAsync(
                        "Ultron, what is seventeen times twenty three?",
                        "sapi:Microsoft Zira Desktop",
                        0,
                        _projectRoot,
                        cts.Token);
                    if (!tts.Success)
                    {
                        AppendDiagnostic("STT test setup failed: " + tts.Error);
                        return;
                    }
                    wav = tts.WavPath;
                    SpeechProbeResult result = await Task.Run(delegate
                    {
                        return SpeechInput.ProbeWave(wav);
                    });
                    if (result.Success)
                    {
                        AppendDiagnostic("STT heard: \"" + result.Text + "\" (" + result.Confidence.ToString("P1") + ")");
                    }
                    else
                    {
                        AppendDiagnostic("STT failed: " + result.Error);
                    }
                }
            }
            catch (Exception exception)
            {
                AppendDiagnostic("STT test failed: " + Redaction.Redact(exception.Message));
            }
            finally
            {
                if (wav != null)
                {
                    TtsHelper.TryDelete(wav);
                }
                _diagTestStt.Enabled = true;
            }
        }

        private void RefreshDiagnostics_Click(object sender, EventArgs e)
        {
            UpdateDiagnostics();
            AppendDiagnostic("Diagnostics refreshed.");
        }

        private void AppendDiagnostic(string text)
        {
            UI(delegate
            {
                if (_diagnosticsLog == null)
                {
                    return;
                }
                _diagnosticsLog.AppendText(DateTime.Now.ToString("HH:mm:ss") + "  " + text + Environment.NewLine);
                _diagnosticsLog.SelectionStart = _diagnosticsLog.TextLength;
                _diagnosticsLog.ScrollToCaret();
            });
        }

        private void Permissions_StateChanged(object sender, PermissionState state)
        {
            UI(delegate { UpdatePermissionUi(); });
        }

        private void Logger_Logged(object sender, LogEntry entry)
        {
            UI(delegate
            {
                if (_activityLog == null)
                {
                    return;
                }
                _activityLog.AppendText(entry.ToDisplayString() + Environment.NewLine);
                _activityLog.SelectionStart = _activityLog.TextLength;
                _activityLog.ScrollToCaret();
            });
        }

        private void UpdatePermissionUi()
        {
            if (_permissionBanner == null)
            {
                return;
            }

            PermissionState state = _permissions.State;
            _permissionLabel.Text = "Permission: " + StateText.ForPermission(state);
            _permissionBanner.Visible = state != PermissionState.Approved;

            switch (state)
            {
                case PermissionState.Pending:
                    _permissionTitle.Text = "Waiting for approval";
                    _permissionText.Text = "Approve the permission dialog to begin listening. Nothing is recorded before approval.";
                    _bannerButton.Text = "Approve...";
                    _permissionBanner.BorderColor = Theme.WarningDark;
                    break;
                case PermissionState.Revoked:
                    _permissionTitle.Text = "Permission revoked";
                    _permissionText.Text = "Take over is active. Ultron is silent until you start and approve a new session.";
                    _bannerButton.Text = "Start new session";
                    _permissionBanner.BorderColor = Theme.DangerDark;
                    break;
                case PermissionState.Approved:
                    _permissionTitle.Text = "Session approved";
                    _permissionText.Text = "Microphone access is active. Interrupt or take over at any time.";
                    _bannerButton.Text = "Session active";
                    _permissionBanner.BorderColor = Theme.Border;
                    break;
                default:
                    _permissionTitle.Text = "Session locked";
                    _permissionText.Text = "Ultron cannot listen or speak until you approve this session. Approval is explicit and revocable.";
                    _bannerButton.Text = "Start session";
                    _permissionBanner.BorderColor = Theme.WarningDark;
                    break;
            }

            LayoutConsole();
            UpdateControls();
        }

        private void LayoutConsole()
        {
            if (_transcriptCard == null)
            {
                return;
            }
            bool bannerVisible = _permissions.State != PermissionState.Approved;
            _transcriptCard.Top = bannerVisible ? 222 : 122;
            _transcriptCard.Height = bannerVisible ? 322 : 422;
            if (_transcript != null)
            {
                _transcript.Height = _transcriptCard.Height - 62;
            }
        }

        private void SetState(UltronState state, string detail)
        {
            _state = state;
            _stateLabel.Text = StateText.ForState(state);
            _stateDot.DotColor = StateColor(state);
            _statusDetail.Text = detail;
            UpdateControls();
        }

        private static Color StateColor(UltronState state)
        {
            switch (state)
            {
                case UltronState.Listening:
                    return Theme.Success;
                case UltronState.Thinking:
                    return Theme.Warning;
                case UltronState.Speaking:
                    return Theme.Accent;
                case UltronState.Muted:
                    return Theme.Faint;
                case UltronState.Error:
                    return Theme.Danger;
                case UltronState.TakenOver:
                    return Theme.Danger;
                default:
                    return Theme.Muted;
            }
        }

        private void UpdateControls()
        {
            if (_startButton == null)
            {
                return;
            }

            bool approved = _permissions.CanSpeak;
            _startButton.Text = _active ? "Stop session" : "Start session";
            _startButton.BackColor = _active ? Theme.Surface2 : Theme.Accent;
            _startButton.ForeColor = _active ? Theme.Text : Color.FromArgb(4, 20, 19);
            _startButton.FlatAppearance.MouseOverBackColor = _active ? Theme.Surface3 : Theme.AccentHover;

            _muteButton.Enabled = _active;
            _muteButton.Text = _muted ? "Unmute" : "Mute";
            _muteButton.BackColor = _muted ? Theme.WarningDark : Theme.Surface2;
            _muteButton.ForeColor = _muted ? Theme.Warning : Theme.Text;
            _muteButton.FlatAppearance.MouseOverBackColor = _muted ? Color.FromArgb(146, 64, 14) : Theme.Surface3;

            bool listening = _speechInput != null && _speechInput.IsListening;
            _talkButton.Enabled = _active && approved && !_processing &&
                                  _state != UltronState.Thinking && _state != UltronState.Speaking;
            _talkButton.Text = listening ? "Stop mic" : "Talk";
            _talkButton.BackColor = listening ? Theme.Accent : Theme.Surface2;
            _talkButton.ForeColor = listening ? Color.FromArgb(4, 20, 19) : Theme.Accent;
            _talkButton.FlatAppearance.MouseOverBackColor = listening ? Theme.AccentHover : Theme.Surface3;

            _interruptButton.Enabled = _active && (_state == UltronState.Thinking || _state == UltronState.Speaking);
            _takeOverButton.Enabled = _active;
            _sendButton.Enabled = _active && approved && !_processing && _state != UltronState.Thinking && _state != UltronState.Speaking;
            _typedInput.Enabled = _active && approved && !_processing && _state != UltronState.Thinking && _state != UltronState.Speaking;
            _bannerButton.Enabled = _permissions.State != PermissionState.Pending;
        }

        private void MainForm_Shown(object sender, EventArgs e)
        {
            if (!_smoke)
            {
                return;
            }
            _smokeTimer = new System.Windows.Forms.Timer();
            _smokeTimer.Interval = 1400;
            _smokeTimer.Tick += delegate
            {
                _smokeTimer.Stop();
                Close();
            };
            _smokeTimer.Start();
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            _active = false;
            _requestGeneration++;
            CancelCurrent();
            StopListening();
            if (_speechOutput != null)
            {
                _speechOutput.Dispose();
                _speechOutput = null;
            }
            if (_speechInput != null)
            {
                _speechInput.Dispose();
                _speechInput = null;
            }
            if (_ollama != null)
            {
                _ollama.Dispose();
                _ollama = null;
            }
        }

        private void DragWindow(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(Handle, WM_NCLBUTTONDOWN, HTCAPTION, 0);
            }
        }

        private void UI(Action action)
        {
            if (action == null || IsDisposed || !IsHandleCreated)
            {
                return;
            }
            if (InvokeRequired)
            {
                try
                {
                    Invoke(action);
                }
                catch (ObjectDisposedException) { }
                catch (InvalidOperationException) { }
            }
            else
            {
                action();
            }
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

        private static Label MakeChip(Control parent, string text, Color color)
        {
            Label label = new Label();
            label.Text = text;
            label.AutoSize = true;
            label.Padding = new Padding(9, 4, 9, 4);
            label.Margin = new Padding(0, 0, 8, 0);
            label.Font = Theme.SmallBold;
            label.ForeColor = color;
            label.BackColor = Theme.Surface2;
            parent.Controls.Add(label);
            return label;
        }

        private static FlatButton MakeButton(
            Control parent, string text, int x, int y, int width, int height, Color back)
        {
            FlatButton button = new FlatButton();
            button.Text = text;
            button.Bounds = new Rectangle(x, y, width, height);
            button.BackColor = back;
            button.ForeColor = Theme.Text;
            if (back == Theme.Accent)
            {
                button.FlatAppearance.MouseOverBackColor = Theme.AccentHover;
            }
            else if (back == Theme.Danger)
            {
                button.FlatAppearance.MouseOverBackColor = Theme.DangerDark;
            }
            else
            {
                button.FlatAppearance.MouseOverBackColor = Theme.Surface3;
            }
            parent.Controls.Add(button);
            return button;
        }
    }
}

