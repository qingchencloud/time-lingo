using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

[assembly: AssemblyTitle("ClaudeBridge CN")]
[assembly: AssemblyProduct("ClaudeBridge CN")]
[assembly: AssemblyCompany("QingChen Cloud")]
[assembly: AssemblyCopyright("Copyright (c) 2026 QingChen Cloud")]
[assembly: AssemblyVersion("0.4.0.0")]
[assembly: AssemblyFileVersion("0.4.0.0")]

namespace BeijingClaudeTranslator
{
    internal static class AppInfo
    {
        public const string ProductName = "ClaudeBridge CN";
        public const string ChineseName = "Claude 中文桥";
        public const string Version = "0.4.0";
        public const string Owner = "qingchencloud";
        public const string Repo = "claude-bridge-cn";
        public const string RepoUrl = "https://github.com/qingchencloud/claude-bridge-cn";
        public const string LatestReleaseApi = "https://api.github.com/repos/qingchencloud/claude-bridge-cn/releases/latest";
        public const string AssetName = "ClaudeBridgeCN.exe";
        public const string LegacyAssetName = "BeijingClaudeTranslator.exe";
        public const string MutexName = "Local\\BeijingClaudeTranslator.SingleInstance";

        public static readonly string[] ReleaseAssetNames = { AssetName, LegacyAssetName };
    }

    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            if (args.Length > 0 && args[0].Equals("--smoke", StringComparison.OrdinalIgnoreCase))
            {
                string input = "cd D:\\Test\r\ngit status --short";
                string output = Translator.Translate(input, "自动判断");
                if (output != input)
                {
                    Console.Error.WriteLine("Smoke test failed.");
                    Environment.Exit(2);
                }
                return;
            }

            bool createdNew;
            using (Mutex mutex = new Mutex(true, AppInfo.MutexName, out createdNew))
            {
                if (!createdNew)
                {
                    ExistingInstance.ShowExistingWindow();
                    return;
                }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

                if (SetupGuide.ShouldShow())
                {
                    using (SetupGuideForm guide = new SetupGuideForm())
                    {
                        DialogResult result = guide.ShowDialog();
                        if (result == DialogResult.Abort)
                        {
                            return;
                        }
                    }
                }

            Application.Run(new MainForm());
            }
        }
    }

    internal sealed class MainForm : Form
    {
        private readonly TableLayoutPanel layout;
        private readonly TableLayoutPanel header;
        private readonly FlowLayoutPanel switchPanel;
        private readonly TableLayoutPanel toolbar;
        private readonly Label timeLabel;
        private readonly Label dateLabel;
        private readonly Label endpointLabel;
        private readonly Label inputLabel;
        private readonly Label outputLabel;
        private readonly Label statusLabel;
        private readonly ComboBox directionBox;
        private readonly TextBox inputBox;
        private readonly TextBox outputBox;
        private readonly Button translateButton;
        private readonly Button copyButton;
        private readonly Button clearButton;
        private readonly Button aboutButton;
        private readonly CheckBox topMostCheck;
        private readonly CheckBox trayCheck;
        private readonly CheckBox themeCheck;
        private readonly CheckBox autoStartCheck;
        private readonly NotifyIcon notifyIcon;
        private readonly ContextMenuStrip trayMenu;
        private readonly System.Windows.Forms.Timer clockTimer;
        private readonly Icon appIcon;

        private bool allowExit;
        private bool darkTheme;
        private bool updatingAutoStart;

        public MainForm()
        {
            Text = AppInfo.ChineseName;
            Size = new Size(460, 520);
            MinimumSize = new Size(420, 460);
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            Font = UiFont(9f);

            appIcon = LoadAppIcon();
            Icon = appIcon;

            Rectangle screen = Screen.PrimaryScreen.WorkingArea;
            Location = new Point(screen.Right - Width - 24, screen.Top + 48);

            layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 7,
                Padding = new Padding(12)
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            Controls.Add(layout);

            header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2 };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 224));
            header.RowStyles.Add(new RowStyle(SizeType.Percent, 64));
            header.RowStyles.Add(new RowStyle(SizeType.Percent, 36));

            timeLabel = NewLabel("", 22f, FontStyle.Bold);
            dateLabel = NewLabel("北京时间 UTC+8", 9f, FontStyle.Regular);
            endpointLabel = NewLabel("在线翻译接口", 8f, FontStyle.Regular);
            endpointLabel.TextAlign = ContentAlignment.MiddleRight;

            switchPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false
            };
            topMostCheck = NewCheckBox("置顶", true);
            trayCheck = NewCheckBox("托盘", true);
            themeCheck = NewCheckBox("夜间", false);
            autoStartCheck = NewCheckBox("自启", AutoStart.IsEnabled());
            switchPanel.Controls.Add(themeCheck);
            switchPanel.Controls.Add(trayCheck);
            switchPanel.Controls.Add(topMostCheck);
            switchPanel.Controls.Add(autoStartCheck);

            header.Controls.Add(timeLabel, 0, 0);
            header.Controls.Add(switchPanel, 1, 0);
            header.Controls.Add(dateLabel, 0, 1);
            header.Controls.Add(endpointLabel, 1, 1);
            layout.Controls.Add(header, 0, 0);

            toolbar = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 5, RowCount = 1 };
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 88));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 88));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 64));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 64));

            directionBox = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Dock = DockStyle.Fill,
                Font = UiFont(9f)
            };
            directionBox.Items.AddRange(new object[] { "自动判断", "中文转英文", "英文转中文" });
            directionBox.SelectedIndex = 0;

            translateButton = NewButton("开始翻译");
            copyButton = NewButton("复制结果");
            clearButton = NewButton("清空");
            aboutButton = NewButton("关于");
            toolbar.Controls.Add(directionBox, 0, 0);
            toolbar.Controls.Add(translateButton, 1, 0);
            toolbar.Controls.Add(copyButton, 2, 0);
            toolbar.Controls.Add(clearButton, 3, 0);
            toolbar.Controls.Add(aboutButton, 4, 0);
            layout.Controls.Add(toolbar, 0, 1);

            inputLabel = NewLabel("输入内容", 9f, FontStyle.Bold);
            outputLabel = NewLabel("翻译结果", 9f, FontStyle.Bold);
            layout.Controls.Add(inputLabel, 0, 2);

            inputBox = new TextBox
            {
                Multiline = true,
                AcceptsReturn = true,
                AcceptsTab = true,
                ScrollBars = ScrollBars.Vertical,
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.FixedSingle,
                Font = UiFont(10f)
            };
            layout.Controls.Add(inputBox, 0, 3);

            layout.Controls.Add(outputLabel, 0, 4);
            outputBox = new TextBox
            {
                Multiline = true,
                AcceptsReturn = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.FixedSingle,
                Font = UiFont(10f)
            };
            layout.Controls.Add(outputBox, 0, 5);

            statusLabel = NewLabel("准备好了。", 8f, FontStyle.Regular);
            layout.Controls.Add(statusLabel, 0, 6);

            trayMenu = new ContextMenuStrip();
            ToolStripMenuItem showTrayItem = new ToolStripMenuItem("显示");
            ToolStripMenuItem aboutTrayItem = new ToolStripMenuItem("关于 / 更新");
            ToolStripMenuItem exitTrayItem = new ToolStripMenuItem("退出");
            trayMenu.Items.Add(showTrayItem);
            trayMenu.Items.Add(aboutTrayItem);
            trayMenu.Items.Add(exitTrayItem);

            notifyIcon = new NotifyIcon
            {
                Text = AppInfo.ChineseName,
                Icon = appIcon,
                ContextMenuStrip = trayMenu,
                Visible = true
            };

            clockTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            clockTimer.Tick += delegate { UpdateClock(); };
            clockTimer.Start();

            showTrayItem.Click += delegate { ShowMainWindow(); };
            aboutTrayItem.Click += delegate { ShowAbout(); };
            notifyIcon.DoubleClick += delegate { ShowMainWindow(); };
            exitTrayItem.Click += delegate
            {
                allowExit = true;
                notifyIcon.Visible = false;
                Close();
            };

            topMostCheck.CheckedChanged += delegate { TopMost = topMostCheck.Checked; };
            trayCheck.CheckedChanged += delegate { notifyIcon.Visible = trayCheck.Checked; };
            themeCheck.CheckedChanged += delegate
            {
                darkTheme = themeCheck.Checked;
                ApplyTheme();
            };
            autoStartCheck.CheckedChanged += delegate { ToggleAutoStart(); };
            aboutButton.Click += delegate { ShowAbout(); };
            translateButton.Click += async delegate { await TranslateFromUiAsync(); };
            copyButton.Click += delegate
            {
                if (!string.IsNullOrWhiteSpace(outputBox.Text))
                {
                    Clipboard.SetText(outputBox.Text);
                    SetStatus("已复制到剪贴板。", false);
                }
            };
            clearButton.Click += delegate
            {
                inputBox.Clear();
                outputBox.Clear();
                SetStatus("准备好了。", false);
            };

            FormClosing += OnFormClosing;
            FormClosed += delegate
            {
                notifyIcon.Visible = false;
                notifyIcon.Dispose();
                if (appIcon != null) appIcon.Dispose();
            };
            Shown += delegate
            {
                UpdateClock();
                ApplyTheme();
                Activate();
                inputBox.Focus();
            };
        }

        private async Task TranslateFromUiAsync()
        {
            string text = inputBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                SetStatus("先输入内容。", true);
                return;
            }

            SetBusy(true);
            SetStatus("翻译中...", false);

            try
            {
                string direction = Convert.ToString(directionBox.SelectedItem);
                string result = await Task.Run(delegate { return Translator.Translate(text, direction); });
                outputBox.Text = result;
                SetStatus("好了，可以复制。", false);
            }
            catch (Exception ex)
            {
                SetStatus("没翻成，稍后再试。详情：" + ex.Message, true);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void ToggleAutoStart()
        {
            if (updatingAutoStart) return;

            try
            {
                AutoStart.SetEnabled(autoStartCheck.Checked);
                SetStatus(autoStartCheck.Checked ? "开机自启已开启。" : "开机自启已关闭。", false);
            }
            catch (Exception ex)
            {
                SetStatus("自启设置失败：" + ex.Message, true);
                updatingAutoStart = true;
                autoStartCheck.Checked = AutoStart.IsEnabled();
                updatingAutoStart = false;
            }
        }

        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            if (!allowExit && trayCheck.Checked)
            {
                e.Cancel = true;
                Hide();
                notifyIcon.Visible = true;
                notifyIcon.ShowBalloonTip(1200, AppInfo.ChineseName, "已到托盘，右键可退出。", ToolTipIcon.Info);
            }
        }

        private void ShowMainWindow()
        {
            Show();
            WindowState = FormWindowState.Normal;
            Activate();
        }

        private void ShowAbout()
        {
            ShowMainWindow();
            using (AboutForm about = new AboutForm(appIcon))
            {
                about.ShowDialog(this);
            }
        }

        private void SetBusy(bool busy)
        {
            translateButton.Enabled = !busy;
            copyButton.Enabled = !busy;
            clearButton.Enabled = !busy;
            directionBox.Enabled = !busy;
        }

        private void SetStatus(string text, bool danger)
        {
            statusLabel.ForeColor = danger ? ThemeColor("Danger") : ThemeColor("MutedText");
            statusLabel.Text = text;
        }

        private void UpdateClock()
        {
            DateTime now = GetBeijingNow();
            timeLabel.Text = now.ToString("HH:mm:ss");
            dateLabel.Text = "北京时间 UTC+8  " + now.ToString("yyyy-MM-dd ddd");
        }

        private DateTime GetBeijingNow()
        {
            try
            {
                TimeZoneInfo zone = TimeZoneInfo.FindSystemTimeZoneById("China Standard Time");
                return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zone);
            }
            catch
            {
                return DateTime.UtcNow.AddHours(8);
            }
        }

        private void ApplyTheme()
        {
            Color background = ThemeColor("Background");
            Color surface = ThemeColor("Surface");
            Color surfaceAlt = ThemeColor("SurfaceAlt");
            Color text = ThemeColor("Text");
            Color muted = ThemeColor("MutedText");

            BackColor = background;
            ForeColor = text;
            layout.BackColor = background;
            header.BackColor = background;
            switchPanel.BackColor = background;
            toolbar.BackColor = background;

            foreach (Label label in new[] { timeLabel, dateLabel, inputLabel, outputLabel })
            {
                label.ForeColor = text;
                label.BackColor = background;
            }

            foreach (Label label in new[] { endpointLabel, statusLabel })
            {
                label.ForeColor = muted;
                label.BackColor = background;
            }

            foreach (CheckBox check in new[] { topMostCheck, trayCheck, themeCheck, autoStartCheck })
            {
                check.ForeColor = muted;
                check.BackColor = background;
            }

            directionBox.BackColor = surface;
            directionBox.ForeColor = text;
            inputBox.BackColor = surface;
            inputBox.ForeColor = text;
            outputBox.BackColor = surfaceAlt;
            outputBox.ForeColor = text;

            SetButtonTheme(translateButton, true);
            SetButtonTheme(copyButton, false);
            SetButtonTheme(clearButton, false);
            SetButtonTheme(aboutButton, false);
        }

        private Label NewLabel(string text, float size, FontStyle style)
        {
            return new Label
            {
                Text = text,
                AutoSize = false,
                Dock = DockStyle.Fill,
                Font = UiFont(size, style),
                TextAlign = ContentAlignment.MiddleLeft
            };
        }

        private Button NewButton(string text)
        {
            Button button = new Button
            {
                Text = text,
                FlatStyle = FlatStyle.Flat,
                Height = 36,
                Font = UiFont(9f),
                Cursor = Cursors.Hand
            };
            return button;
        }

        private CheckBox NewCheckBox(string text, bool isChecked)
        {
            return new CheckBox
            {
                Text = text,
                Checked = isChecked,
                AutoSize = true,
                Margin = new Padding(4, 8, 0, 0),
                Font = UiFont(9f)
            };
        }

        private void SetButtonTheme(Button button, bool primary)
        {
            if (primary)
            {
                button.BackColor = ThemeColor("Primary");
                button.ForeColor = Color.White;
                button.FlatAppearance.BorderColor = ThemeColor("Primary");
                button.FlatAppearance.MouseOverBackColor = ThemeColor("PrimaryHover");
                button.FlatAppearance.MouseDownBackColor = ThemeColor("PrimaryDown");
                return;
            }

            button.BackColor = ThemeColor("Button");
            button.ForeColor = ThemeColor("Text");
            button.FlatAppearance.BorderColor = ThemeColor("Border");
            button.FlatAppearance.MouseOverBackColor = ThemeColor("ButtonHover");
            button.FlatAppearance.MouseDownBackColor = ThemeColor("ButtonDown");
        }

        private Color ThemeColor(string name)
        {
            if (darkTheme)
            {
                switch (name)
                {
                    case "Background": return Color.FromArgb(15, 23, 42);
                    case "Surface": return Color.FromArgb(30, 41, 59);
                    case "SurfaceAlt": return Color.FromArgb(17, 24, 39);
                    case "Text": return Color.FromArgb(248, 250, 252);
                    case "MutedText": return Color.FromArgb(203, 213, 225);
                    case "Border": return Color.FromArgb(71, 85, 105);
                    case "Primary": return Color.FromArgb(59, 130, 246);
                    case "PrimaryHover": return Color.FromArgb(37, 99, 235);
                    case "PrimaryDown": return Color.FromArgb(29, 78, 216);
                    case "Button": return Color.FromArgb(30, 41, 59);
                    case "ButtonHover": return Color.FromArgb(51, 65, 85);
                    case "ButtonDown": return Color.FromArgb(71, 85, 105);
                    case "Danger": return Color.FromArgb(248, 113, 113);
                    case "Warn": return Color.FromArgb(251, 146, 60);
                }
            }

            switch (name)
            {
                case "Background": return Color.FromArgb(248, 250, 252);
                case "Surface": return Color.White;
                case "SurfaceAlt": return Color.FromArgb(241, 245, 249);
                case "Text": return Color.FromArgb(15, 23, 42);
                case "MutedText": return Color.FromArgb(71, 85, 105);
                case "Border": return Color.FromArgb(203, 213, 225);
                case "Primary": return Color.FromArgb(37, 99, 235);
                case "PrimaryHover": return Color.FromArgb(29, 78, 216);
                case "PrimaryDown": return Color.FromArgb(30, 64, 175);
                case "Button": return Color.White;
                case "ButtonHover": return Color.FromArgb(241, 245, 249);
                case "ButtonDown": return Color.FromArgb(226, 232, 240);
                case "Danger": return Color.FromArgb(220, 38, 38);
                case "Warn": return Color.FromArgb(234, 88, 12);
                default: return Color.Black;
            }
        }

        private Font UiFont(float size)
        {
            return UiFont(size, FontStyle.Regular);
        }

        private Font UiFont(float size, FontStyle style)
        {
            return new Font("Microsoft YaHei UI", size, style);
        }

        private Icon LoadAppIcon()
        {
            try
            {
                return Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch
            {
                return SystemIcons.Application;
            }
        }
    }

    internal static class Translator
    {
        private const int MaxChunkLength = 260;

        public static string Translate(string text, string direction)
        {
            DirectionInfo info = ResolveDirection(text, direction);
            string provider = Environment.GetEnvironmentVariable("RED_FRAME_TRANSLATOR_PROVIDER");
            if (string.IsNullOrWhiteSpace(provider)) provider = "mymemory";
            provider = provider.Trim().ToLowerInvariant();

            switch (provider)
            {
                case "microsoft":
                    return TranslateMicrosoft(text, info);
                case "deepl":
                    return TranslateDeepL(text, info);
                case "mymemory":
                    return TranslateMyMemory(text, info);
                default:
                    throw new InvalidOperationException("未知翻译接口：" + provider + "。可用值：mymemory、microsoft、deepl。");
            }
        }

        private static string TranslateMyMemory(string text, DirectionInfo info)
        {
            string endpoint = Environment.GetEnvironmentVariable("RED_FRAME_TRANSLATOR_MYMEMORY_ENDPOINT");
            if (string.IsNullOrWhiteSpace(endpoint)) endpoint = "https://api.mymemory.translated.net/get";

            StringBuilder builder = new StringBuilder();
            foreach (string part in SplitText(text))
            {
                if (string.IsNullOrWhiteSpace(part) || IsTechnicalLine(part))
                {
                    builder.Append(part);
                    continue;
                }

                builder.Append(TranslateMyMemorySingle(endpoint, part, info));
                Thread.Sleep(120);
            }

            return builder.ToString().Trim();
        }

        private static string TranslateMyMemorySingle(string endpoint, string text, DirectionInfo info)
        {
            string uri = endpoint + "?q=" + Uri.EscapeDataString(text) + "&langpair=" + Uri.EscapeDataString(info.Source + "|" + info.Target);
            string json = HttpGet(uri);
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            Dictionary<string, object> root = serializer.Deserialize<Dictionary<string, object>>(json);

            if (root.ContainsKey("responseStatus") && Convert.ToInt32(root["responseStatus"]) >= 400)
            {
                if (root.ContainsKey("responseDetails")) throw new InvalidOperationException(Convert.ToString(root["responseDetails"]));
                throw new InvalidOperationException("翻译接口返回错误。");
            }

            if (!root.ContainsKey("responseData")) throw new InvalidOperationException("翻译接口没有返回结果。");
            Dictionary<string, object> data = root["responseData"] as Dictionary<string, object>;
            if (data == null || !data.ContainsKey("translatedText")) throw new InvalidOperationException("翻译接口没有返回结果。");
            return Convert.ToString(data["translatedText"]).Trim();
        }

        private static string TranslateMicrosoft(string text, DirectionInfo info)
        {
            string key = Environment.GetEnvironmentVariable("RED_FRAME_TRANSLATOR_MICROSOFT_KEY");
            if (string.IsNullOrWhiteSpace(key)) throw new InvalidOperationException("请先配置 Microsoft Translator 密钥：RED_FRAME_TRANSLATOR_MICROSOFT_KEY。");

            string endpoint = Environment.GetEnvironmentVariable("RED_FRAME_TRANSLATOR_MICROSOFT_ENDPOINT");
            if (string.IsNullOrWhiteSpace(endpoint)) endpoint = "https://api.cognitive.microsofttranslator.com";
            endpoint = endpoint.TrimEnd('/');

            string region = Environment.GetEnvironmentVariable("RED_FRAME_TRANSLATOR_MICROSOFT_REGION");
            string uri = endpoint + "/translate?api-version=3.0&from=" + info.MicrosoftSource + "&to=" + info.MicrosoftTarget;
            string body = "[{\"Text\":" + JsonQuote(text) + "}]";

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(uri);
            request.Method = "POST";
            request.Timeout = 20000;
            request.ContentType = "application/json; charset=utf-8";
            request.Headers["Ocp-Apim-Subscription-Key"] = key;
            if (!string.IsNullOrWhiteSpace(region)) request.Headers["Ocp-Apim-Subscription-Region"] = region;
            WriteBody(request, body, "application/json; charset=utf-8");
            string json = ReadResponse(request);

            JavaScriptSerializer serializer = new JavaScriptSerializer();
            object[] root = serializer.Deserialize<object[]>(json);
            Dictionary<string, object> first = root[0] as Dictionary<string, object>;
            object[] translations = first["translations"] as object[];
            Dictionary<string, object> translation = translations[0] as Dictionary<string, object>;
            return Convert.ToString(translation["text"]).Trim();
        }

        private static string TranslateDeepL(string text, DirectionInfo info)
        {
            string key = Environment.GetEnvironmentVariable("RED_FRAME_TRANSLATOR_DEEPL_KEY");
            if (string.IsNullOrWhiteSpace(key)) key = Environment.GetEnvironmentVariable("DEEPL_AUTH_KEY");
            if (string.IsNullOrWhiteSpace(key)) throw new InvalidOperationException("请先配置 DeepL API Key：RED_FRAME_TRANSLATOR_DEEPL_KEY 或 DEEPL_AUTH_KEY。");

            string url = Environment.GetEnvironmentVariable("RED_FRAME_TRANSLATOR_DEEPL_URL");
            if (string.IsNullOrWhiteSpace(url)) url = "https://api-free.deepl.com/v2/translate";

            string body = "text=" + Uri.EscapeDataString(text) +
                          "&source_lang=" + Uri.EscapeDataString(info.DeepLSource) +
                          "&target_lang=" + Uri.EscapeDataString(info.DeepLTarget);

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "POST";
            request.Timeout = 20000;
            request.Headers["Authorization"] = "DeepL-Auth-Key " + key;
            WriteBody(request, body, "application/x-www-form-urlencoded");
            string json = ReadResponse(request);

            JavaScriptSerializer serializer = new JavaScriptSerializer();
            Dictionary<string, object> root = serializer.Deserialize<Dictionary<string, object>>(json);
            object[] translations = root["translations"] as object[];
            Dictionary<string, object> translation = translations[0] as Dictionary<string, object>;
            return Convert.ToString(translation["text"]).Trim();
        }

        private static DirectionInfo ResolveDirection(string text, string direction)
        {
            if (direction == "中文转英文")
            {
                return new DirectionInfo("zh-CN", "en", "zh-Hans", "en", "ZH", "EN-US");
            }
            if (direction == "英文转中文")
            {
                return new DirectionInfo("en", "zh-CN", "en", "zh-Hans", "EN", "ZH-HANS");
            }
            return ProbablyChinese(text)
                ? new DirectionInfo("zh-CN", "en", "zh-Hans", "en", "ZH", "EN-US")
                : new DirectionInfo("en", "zh-CN", "en", "zh-Hans", "EN", "ZH-HANS");
        }

        private static bool ProbablyChinese(string text)
        {
            return Regex.IsMatch(text, @"\p{IsCJKUnifiedIdeographs}");
        }

        private static bool IsTechnicalLine(string text)
        {
            string trimmed = text.Trim();
            if (trimmed.Length == 0) return false;
            if (Regex.IsMatch(trimmed, @"^[A-Za-z]:\\|^\\\\|/mnt/|/home/|/usr/|/var/|/etc/")) return true;
            if (Regex.IsMatch(trimmed, @"\\|/|--|&&|\|\||\.(ps1|cmd|bat|exe|dll|json|md|go|rs|ts|tsx|js|jsx|vue|toml|yaml|yml)$")) return true;
            return Regex.IsMatch(trimmed, @"^(cd|dir|ls|pwd|git|npm|pnpm|yarn|npx|node|go|cargo|rustup|python|python3|pip|pip3|docker|docker-compose|kubectl|ssh|scp|curl|wget|powershell|pwsh|cmd|make|cmake|dotnet|deno|bun|wrangler|vercel|netlify|gh|rg|grep)\b", RegexOptions.IgnoreCase);
        }

        private static IEnumerable<string> SplitText(string text)
        {
            string[] pieces = Regex.Split(text, "(\r\n|\n|\r)");
            foreach (string piece in pieces)
            {
                if (Regex.IsMatch(piece, "^\r\n$|^\n$|^\r$"))
                {
                    yield return piece;
                    continue;
                }
                if (piece.Length <= MaxChunkLength)
                {
                    yield return piece;
                    continue;
                }

                int start = 0;
                while (start < piece.Length)
                {
                    int length = Math.Min(MaxChunkLength, piece.Length - start);
                    string chunk = piece.Substring(start, length);
                    if (start + length < piece.Length)
                    {
                        int cut = chunk.LastIndexOfAny(new[] { ' ', '，', '。', '；', '、', ',', '.', ';', ':', '：' });
                        if (cut > 80)
                        {
                            length = cut + 1;
                            chunk = piece.Substring(start, length);
                        }
                    }

                    yield return chunk;
                    start += length;
                }
            }
        }

        private static string HttpGet(string uri)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(uri);
            request.Method = "GET";
            request.Timeout = 20000;
            return ReadResponse(request);
        }

        private static void WriteBody(HttpWebRequest request, string body, string contentType)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(body);
            request.ContentType = contentType;
            request.ContentLength = bytes.Length;
            using (Stream stream = request.GetRequestStream())
            {
                stream.Write(bytes, 0, bytes.Length);
            }
        }

        private static string ReadResponse(HttpWebRequest request)
        {
            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            using (Stream stream = response.GetResponseStream())
            using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
            {
                return reader.ReadToEnd();
            }
        }

        private static string JsonQuote(string value)
        {
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            return serializer.Serialize(value);
        }

        private sealed class DirectionInfo
        {
            public readonly string Source;
            public readonly string Target;
            public readonly string MicrosoftSource;
            public readonly string MicrosoftTarget;
            public readonly string DeepLSource;
            public readonly string DeepLTarget;

            public DirectionInfo(string source, string target, string microsoftSource, string microsoftTarget, string deepLSource, string deepLTarget)
            {
                Source = source;
                Target = target;
                MicrosoftSource = microsoftSource;
                MicrosoftTarget = microsoftTarget;
                DeepLSource = deepLSource;
                DeepLTarget = deepLTarget;
            }
        }
    }

    internal sealed class SetupGuideForm : Form
    {
        private readonly CheckBox desktopCheck;
        private readonly CheckBox autoStartCheck;

        public SetupGuideForm()
        {
            Text = "安装引导";
            Size = new Size(420, 250);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Font = new Font("Microsoft YaHei UI", 9f);
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

            TableLayoutPanel layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(18),
                ColumnCount = 1,
                RowCount = 5
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            Controls.Add(layout);

            Label title = new Label
            {
                Text = "要安装到本机吗？",
                Dock = DockStyle.Fill,
                Font = new Font("Microsoft YaHei UI", 13f, FontStyle.Bold)
            };
            Label desc = new Label
            {
                Text = "安装后会放到用户目录，并创建启动入口。也可以直接使用，不安装。",
                Dock = DockStyle.Fill
            };
            desktopCheck = new CheckBox { Text = "创建桌面图标", Checked = true, AutoSize = true };
            autoStartCheck = new CheckBox { Text = "开机自动启动", Checked = false, AutoSize = true };

            FlowLayoutPanel buttons = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.RightToLeft,
                Dock = DockStyle.Fill
            };
            Button installButton = new Button { Text = "安装并打开", Width = 104, Height = 34 };
            Button directButton = new Button { Text = "直接使用", Width = 92, Height = 34 };
            buttons.Controls.Add(installButton);
            buttons.Controls.Add(directButton);

            layout.Controls.Add(title, 0, 0);
            layout.Controls.Add(desc, 0, 1);
            layout.Controls.Add(desktopCheck, 0, 2);
            layout.Controls.Add(autoStartCheck, 0, 3);
            layout.Controls.Add(buttons, 0, 4);

            installButton.Click += delegate
            {
                try
                {
                    string installedExe = Installer.Install(desktopCheck.Checked, autoStartCheck.Checked);
                    Process.Start(installedExe);
                    DialogResult = DialogResult.Abort;
                    Close();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "安装失败：" + ex.Message, AppInfo.ChineseName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };
            directButton.Click += delegate
            {
                DialogResult = DialogResult.OK;
                Close();
            };
        }
    }

    internal sealed class AboutForm : Form
    {
        private readonly Label statusLabel;
        private readonly Button updateButton;

        public AboutForm(Icon icon)
        {
            Text = "关于";
            Size = new Size(420, 260);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Font = new Font("Microsoft YaHei UI", 9f);
            Icon = icon;

            TableLayoutPanel layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(18),
                ColumnCount = 1,
                RowCount = 5
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            Controls.Add(layout);

            Label title = new Label
            {
                Text = AppInfo.ChineseName,
                Dock = DockStyle.Fill,
                Font = new Font("Microsoft YaHei UI", 13f, FontStyle.Bold)
            };
            Label desc = new Label
            {
                Text = "版本 " + AppInfo.Version + "\r\n给中文 Claude 用户用的时间和中英文小工具。",
                Dock = DockStyle.Fill
            };
            statusLabel = new Label { Text = "可以检查 GitHub 上的新版本。", Dock = DockStyle.Fill };

            FlowLayoutPanel buttons = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.RightToLeft,
                Dock = DockStyle.Fill
            };
            Button closeButton = new Button { Text = "关闭", Width = 76, Height = 34 };
            Button githubButton = new Button { Text = "GitHub", Width = 76, Height = 34 };
            updateButton = new Button { Text = "检查更新", Width = 92, Height = 34 };
            buttons.Controls.Add(closeButton);
            buttons.Controls.Add(githubButton);
            buttons.Controls.Add(updateButton);

            layout.Controls.Add(title, 0, 0);
            layout.Controls.Add(desc, 0, 1);
            layout.Controls.Add(statusLabel, 0, 2);
            layout.Controls.Add(buttons, 0, 3);

            closeButton.Click += delegate { Close(); };
            githubButton.Click += delegate { Process.Start(AppInfo.RepoUrl); };
            updateButton.Click += async delegate { await CheckUpdateAsync(); };
        }

        private async Task CheckUpdateAsync()
        {
            updateButton.Enabled = false;
            statusLabel.Text = "正在检查...";
            try
            {
                ReleaseInfo latest = await Task.Run(delegate { return UpdateManager.GetLatestRelease(); });
                if (VersionUtil.Compare(latest.Version, AppInfo.Version) <= 0)
                {
                    statusLabel.Text = "已经是最新版。";
                    return;
                }

                DialogResult confirm = MessageBox.Show(
                    this,
                    "发现新版本 " + latest.Tag + "，现在更新吗？",
                    AppInfo.ChineseName,
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);
                if (confirm != DialogResult.Yes)
                {
                    statusLabel.Text = "已取消更新。";
                    return;
                }

                statusLabel.Text = "正在下载更新...";
                await Task.Run(delegate { UpdateManager.DownloadAndRestart(latest); });
                Application.Exit();
            }
            catch (Exception ex)
            {
                statusLabel.Text = "更新失败：" + ex.Message;
            }
            finally
            {
                updateButton.Enabled = true;
            }
        }
    }

    internal static class SetupGuide
    {
        public static bool ShouldShow()
        {
            return !Installer.IsRunningInstalledCopy();
        }
    }

    internal static class ExistingInstance
    {
        public static void ShowExistingWindow()
        {
            IntPtr handle = NativeMethods.FindWindow(null, AppInfo.ChineseName);
            if (handle == IntPtr.Zero) handle = NativeMethods.FindWindow(null, "北京时间翻译助手");
            if (handle == IntPtr.Zero) handle = NativeMethods.FindWindow(null, "安装引导");
            if (handle == IntPtr.Zero) handle = NativeMethods.FindWindow(null, "关于");
            if (handle != IntPtr.Zero)
            {
                NativeMethods.ShowWindow(handle, NativeMethods.SW_RESTORE);
                NativeMethods.SetForegroundWindow(handle);
            }
        }
    }

    internal static class Installer
    {
        public static string Install(bool createDesktopShortcut, bool enableAutoStart)
        {
            string installDir = GetInstallDir();
            string installedExe = GetInstalledExePath();
            Directory.CreateDirectory(installDir);

            string currentExe = Application.ExecutablePath;
            if (!SamePath(currentExe, installedExe))
            {
                File.Copy(currentExe, installedExe, true);
            }

            ShortcutHelper.SaveShortcut(GetStartMenuShortcutPath(), installedExe);
            if (createDesktopShortcut)
            {
                ShortcutHelper.SaveShortcut(GetDesktopShortcutPath(), installedExe);
            }
            AutoStart.SetEnabledForExecutable(enableAutoStart, installedExe);
            return installedExe;
        }

        public static bool IsRunningInstalledCopy()
        {
            string currentExe = Application.ExecutablePath;
            return SamePath(currentExe, GetInstalledExePath()) || SamePath(currentExe, GetLegacyInstalledExePath());
        }

        private static string GetInstallDir()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "ClaudeBridgeCN");
        }

        private static string GetLegacyInstallDir()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "BeijingClaudeTranslator");
        }

        private static string GetInstalledExePath()
        {
            return Path.Combine(GetInstallDir(), AppInfo.AssetName);
        }

        private static string GetLegacyInstalledExePath()
        {
            return Path.Combine(GetLegacyInstallDir(), AppInfo.LegacyAssetName);
        }

        private static string GetDesktopShortcutPath()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "ClaudeBridge CN.lnk");
        }

        private static string GetStartMenuShortcutPath()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs", "ClaudeBridge CN.lnk");
        }

        private static bool SamePath(string left, string right)
        {
            return string.Equals(Path.GetFullPath(left).TrimEnd('\\'), Path.GetFullPath(right).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);
        }
    }

    internal static class UpdateManager
    {
        public static ReleaseInfo GetLatestRelease()
        {
            string json = HttpGet(AppInfo.LatestReleaseApi);
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            Dictionary<string, object> root = serializer.Deserialize<Dictionary<string, object>>(json);

            string tag = Convert.ToString(root["tag_name"]);
            string htmlUrl = root.ContainsKey("html_url") ? Convert.ToString(root["html_url"]) : AppInfo.RepoUrl;
            string downloadUrl = "";

            object[] assets = root["assets"] as object[];
            if (assets != null)
            {
                foreach (object item in assets)
                {
                    Dictionary<string, object> asset = item as Dictionary<string, object>;
                    if (asset == null) continue;
                    string name = Convert.ToString(asset["name"]);
                    if (IsWindowsAsset(name))
                    {
                        downloadUrl = Convert.ToString(asset["browser_download_url"]);
                        break;
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(downloadUrl))
            {
                throw new InvalidOperationException("没有找到 Windows exe 下载文件。");
            }

            return new ReleaseInfo(tag, htmlUrl, downloadUrl);
        }

        private static bool IsWindowsAsset(string name)
        {
            foreach (string assetName in AppInfo.ReleaseAssetNames)
            {
                if (string.Equals(name, assetName, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        public static void DownloadAndRestart(ReleaseInfo release)
        {
            string tempExe = Path.Combine(Path.GetTempPath(), "ClaudeBridgeCN-update-" + Guid.NewGuid().ToString("N") + ".exe");
            using (WebClient client = new WebClient())
            {
                client.Headers.Add("User-Agent", AppInfo.ProductName);
                client.DownloadFile(release.DownloadUrl, tempExe);
            }

            string script = Path.Combine(Path.GetTempPath(), "ClaudeBridgeCN-update-" + Guid.NewGuid().ToString("N") + ".cmd");
            string currentExe = Application.ExecutablePath;
            int pid = Process.GetCurrentProcess().Id;
            string content =
                "@echo off\r\n" +
                "setlocal\r\n" +
                "set \"SRC=" + tempExe + "\"\r\n" +
                "set \"DST=" + currentExe + "\"\r\n" +
                "set \"PID=" + pid + "\"\r\n" +
                ":wait\r\n" +
                "tasklist /fi \"PID eq %PID%\" | find \"%PID%\" >nul\r\n" +
                "if not errorlevel 1 (\r\n" +
                "  timeout /t 1 /nobreak >nul\r\n" +
                "  goto wait\r\n" +
                ")\r\n" +
                "copy /y \"%SRC%\" \"%DST%\" >nul\r\n" +
                "start \"\" \"%DST%\"\r\n" +
                "del \"%SRC%\" >nul 2>nul\r\n" +
                "del \"%~f0\" >nul 2>nul\r\n";
            File.WriteAllText(script, content, Encoding.Default);

            ProcessStartInfo start = new ProcessStartInfo("cmd.exe", "/c \"" + script + "\"")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            Process.Start(start);
        }

        private static string HttpGet(string url)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "GET";
            request.Timeout = 20000;
            request.UserAgent = AppInfo.ProductName;
            request.Accept = "application/vnd.github+json";
            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            using (Stream stream = response.GetResponseStream())
            using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
            {
                return reader.ReadToEnd();
            }
        }
    }

    internal sealed class ReleaseInfo
    {
        public readonly string Tag;
        public readonly string Version;
        public readonly string HtmlUrl;
        public readonly string DownloadUrl;

        public ReleaseInfo(string tag, string htmlUrl, string downloadUrl)
        {
            Tag = tag;
            Version = VersionUtil.Normalize(tag);
            HtmlUrl = htmlUrl;
            DownloadUrl = downloadUrl;
        }
    }

    internal static class VersionUtil
    {
        public static string Normalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "0.0.0";
            value = value.Trim();
            if (value.StartsWith("v", StringComparison.OrdinalIgnoreCase)) value = value.Substring(1);
            return value;
        }

        public static int Compare(string left, string right)
        {
            Version leftVersion;
            Version rightVersion;
            if (!Version.TryParse(Normalize(left), out leftVersion)) leftVersion = new Version(0, 0, 0);
            if (!Version.TryParse(Normalize(right), out rightVersion)) rightVersion = new Version(0, 0, 0);
            return leftVersion.CompareTo(rightVersion);
        }
    }

    internal static class AutoStart
    {
        private const string ShortcutName = "ClaudeBridgeCN.lnk";
        private const string LegacyShortcutName = "BeijingClaudeTranslator.lnk";

        public static bool IsEnabled()
        {
            return File.Exists(GetShortcutPath(ShortcutName)) || File.Exists(GetShortcutPath(LegacyShortcutName));
        }

        public static void SetEnabled(bool enabled)
        {
            SetEnabledForExecutable(enabled, Application.ExecutablePath);
        }

        public static void SetEnabledForExecutable(bool enabled, string targetExe)
        {
            string shortcutPath = GetShortcutPath();
            if (enabled)
            {
                ShortcutHelper.SaveShortcut(shortcutPath, targetExe);
                return;
            }

            if (File.Exists(shortcutPath)) File.Delete(shortcutPath);
            string legacyShortcutPath = GetShortcutPath(LegacyShortcutName);
            if (File.Exists(legacyShortcutPath)) File.Delete(legacyShortcutPath);
        }

        private static string GetShortcutPath()
        {
            return GetShortcutPath(ShortcutName);
        }

        private static string GetShortcutPath(string shortcutName)
        {
            string startup = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
            return Path.Combine(startup, shortcutName);
        }
    }

    internal static class ShortcutHelper
    {
        public static void SaveShortcut(string shortcutPath, string targetExe)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(shortcutPath));

            Type shellType = Type.GetTypeFromProgID("WScript.Shell");
            object shell = Activator.CreateInstance(shellType);
            object shortcut = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { shortcutPath });
            Type shortcutType = shortcut.GetType();

            SetProperty(shortcutType, shortcut, "TargetPath", targetExe);
            SetProperty(shortcutType, shortcut, "WorkingDirectory", Path.GetDirectoryName(targetExe));
            SetProperty(shortcutType, shortcut, "IconLocation", targetExe);
            SetProperty(shortcutType, shortcut, "Description", AppInfo.ChineseName);
            shortcutType.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
        }

        private static void SetProperty(Type type, object target, string name, object value)
        {
            type.InvokeMember(name, BindingFlags.SetProperty, null, target, new[] { value });
        }
    }

    internal static class NativeMethods
    {
        public const int SW_RESTORE = 9;

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);
    }
}
