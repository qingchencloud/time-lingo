using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace BeijingClaudeTranslator
{
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

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
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
            Text = "北京时间翻译助手";
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

            toolbar = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1 };
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 88));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 88));
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
            toolbar.Controls.Add(directionBox, 0, 0);
            toolbar.Controls.Add(translateButton, 1, 0);
            toolbar.Controls.Add(copyButton, 2, 0);
            toolbar.Controls.Add(clearButton, 3, 0);
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
            ToolStripMenuItem exitTrayItem = new ToolStripMenuItem("退出");
            trayMenu.Items.Add(showTrayItem);
            trayMenu.Items.Add(exitTrayItem);

            notifyIcon = new NotifyIcon
            {
                Text = "北京时间翻译助手",
                Icon = appIcon,
                ContextMenuStrip = trayMenu,
                Visible = true
            };

            clockTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            clockTimer.Tick += delegate { UpdateClock(); };
            clockTimer.Start();

            showTrayItem.Click += delegate { ShowMainWindow(); };
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
                notifyIcon.ShowBalloonTip(1200, "北京时间翻译助手", "已到托盘，右键可退出。", ToolTipIcon.Info);
            }
        }

        private void ShowMainWindow()
        {
            Show();
            WindowState = FormWindowState.Normal;
            Activate();
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

    internal static class AutoStart
    {
        private const string ShortcutName = "BeijingClaudeTranslator.lnk";

        public static bool IsEnabled()
        {
            return File.Exists(GetShortcutPath());
        }

        public static void SetEnabled(bool enabled)
        {
            string shortcutPath = GetShortcutPath();
            if (enabled)
            {
                SaveShortcut(shortcutPath);
                return;
            }

            if (File.Exists(shortcutPath)) File.Delete(shortcutPath);
        }

        private static string GetShortcutPath()
        {
            string startup = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
            return Path.Combine(startup, ShortcutName);
        }

        private static void SaveShortcut(string shortcutPath)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(shortcutPath));

            Type shellType = Type.GetTypeFromProgID("WScript.Shell");
            object shell = Activator.CreateInstance(shellType);
            object shortcut = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { shortcutPath });
            Type shortcutType = shortcut.GetType();

            SetProperty(shortcutType, shortcut, "TargetPath", Application.ExecutablePath);
            SetProperty(shortcutType, shortcut, "WorkingDirectory", AppDomain.CurrentDomain.BaseDirectory);
            SetProperty(shortcutType, shortcut, "IconLocation", Application.ExecutablePath);
            SetProperty(shortcutType, shortcut, "Description", "北京时间翻译助手");
            shortcutType.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
        }

        private static void SetProperty(Type type, object target, string name, object value)
        {
            type.InvokeMember(name, BindingFlags.SetProperty, null, target, new[] { value });
        }
    }
}
