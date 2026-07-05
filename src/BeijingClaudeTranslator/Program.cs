using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
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

[assembly: AssemblyTitle("TimeLingo")]
[assembly: AssemblyProduct("TimeLingo")]
[assembly: AssemblyCompany("QingChen Cloud")]
[assembly: AssemblyCopyright("Copyright (c) 2026 QingChen Cloud")]
[assembly: AssemblyVersion("0.5.0.0")]
[assembly: AssemblyFileVersion("0.5.0.0")]

namespace BeijingClaudeTranslator
{
    internal static class AppInfo
    {
        public const string ProductName = "TimeLingo";
        public const string ChineseName = "TimeLingo";
        public const string Version = "0.5.0";
        public const string Owner = "qingchencloud";
        public const string Repo = "time-lingo";
        public const string RepoUrl = "https://github.com/qingchencloud/time-lingo";
        public const string LatestReleaseApi = "https://api.github.com/repos/qingchencloud/time-lingo/releases/latest";
        public const string AssetName = "TimeLingo.exe";
        public const string LegacyAssetName = "ClaudeBridgeCN.exe";
        public const string VeryLegacyAssetName = "BeijingClaudeTranslator.exe";
        public const string MutexName = "Local\\BeijingClaudeTranslator.SingleInstance";

        public static readonly string[] ReleaseAssetNames = { AssetName, LegacyAssetName, VeryLegacyAssetName };
    }

    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            AppSettings.Load();
            if (args.Length > 0 && args[0].Equals("--smoke", StringComparison.OrdinalIgnoreCase))
            {
                string input = "cd D:\\Test\r\ngit status --short";
                string output = Translator.Translate(input, "自动判断");
                if (output != input)
                {
                    Console.Error.WriteLine("Smoke test failed.");
                    Environment.Exit(2);
                }
                foreach (object item in Translator.GetDirectionOptions())
                {
                    string direction = Convert.ToString(item);
                    string directionOutput = Translator.Translate(input, direction);
                    if (directionOutput != input)
                    {
                        Console.Error.WriteLine("Direction smoke failed: " + direction);
                        Environment.Exit(2);
                    }
                }
                return;
            }
            if (args.Length > 0 && args[0].Equals("--update-smoke", StringComparison.OrdinalIgnoreCase))
            {
                ReleaseInfo latest = UpdateManager.GetLatestRelease();
                if (string.IsNullOrWhiteSpace(latest.DownloadUrl))
                {
                    Console.Error.WriteLine("Update smoke failed.");
                    Environment.Exit(3);
                }
                Console.WriteLine(latest.Tag + " " + latest.DownloadUrl);
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
        private readonly Label inputLabel;
        private readonly Label outputLabel;
        private readonly Label statusLabel;
        private readonly ComboBox timeZoneBox;
        private readonly ComboBox directionBox;
        private readonly TextBox inputBox;
        private readonly TextBox outputBox;
        private readonly Button translateButton;
        private readonly Button copyButton;
        private readonly Button clearButton;
        private readonly Button settingsButton;
        private readonly CheckBox topMostCheck;
        private readonly CheckBox trayCheck;
        private readonly CheckBox themeCheck;
        private readonly CheckBox autoStartCheck;
        private readonly NotifyIcon notifyIcon;
        private readonly ContextMenuStrip trayMenu;
        private readonly ToolStripMenuItem showTrayItem;
        private readonly ToolStripMenuItem settingsTrayItem;
        private readonly ToolStripMenuItem aboutTrayItem;
        private readonly ToolStripMenuItem exitTrayItem;
        private readonly System.Windows.Forms.Timer clockTimer;
        private readonly Icon appIcon;

        private bool allowExit;
        private bool darkTheme;
        private bool updatingAutoStart;

        public MainForm()
        {
            Text = AppInfo.ProductName;
            Size = new Size(520, 540);
            MinimumSize = new Size(500, 480);
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
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 74));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            Controls.Add(layout);

            header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2 };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 270));
            header.RowStyles.Add(new RowStyle(SizeType.Percent, 64));
            header.RowStyles.Add(new RowStyle(SizeType.Percent, 36));

            timeLabel = NewLabel("", 22f, FontStyle.Bold);
            dateLabel = NewLabel("", 9f, FontStyle.Regular);
            timeZoneBox = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Dock = DockStyle.Fill,
                Font = UiFont(8.5f),
                Margin = new Padding(8, 1, 0, 1)
            };

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
            header.Controls.Add(timeZoneBox, 1, 1);
            layout.Controls.Add(header, 0, 0);

            toolbar = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 5, RowCount = 1 };
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 94));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 94));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 76));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 76));

            directionBox = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Dock = DockStyle.Fill,
                Font = UiFont(9f),
                Margin = new Padding(0, 4, 8, 4)
            };
            directionBox.Items.AddRange(Translator.GetDirectionOptions());
            directionBox.SelectedIndex = 0;

            translateButton = NewButton("开始翻译");
            copyButton = NewButton("复制结果");
            clearButton = NewButton("清空");
            settingsButton = NewButton("设置");
            toolbar.Controls.Add(directionBox, 0, 0);
            toolbar.Controls.Add(translateButton, 1, 0);
            toolbar.Controls.Add(copyButton, 2, 0);
            toolbar.Controls.Add(clearButton, 3, 0);
            toolbar.Controls.Add(settingsButton, 4, 0);
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
            showTrayItem = new ToolStripMenuItem("显示");
            settingsTrayItem = new ToolStripMenuItem("设置");
            aboutTrayItem = new ToolStripMenuItem("关于 / 更新");
            exitTrayItem = new ToolStripMenuItem("退出");
            trayMenu.Items.Add(showTrayItem);
            trayMenu.Items.Add(settingsTrayItem);
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
            settingsTrayItem.Click += delegate { ShowSettings(); };
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
            timeZoneBox.SelectedIndexChanged += delegate { SaveSelectedTimeZone(); };
            settingsButton.Click += delegate { ShowSettings(); };
            translateButton.Click += async delegate { await TranslateFromUiAsync(); };
            copyButton.Click += delegate
            {
                if (!string.IsNullOrWhiteSpace(outputBox.Text))
                {
                    Clipboard.SetText(outputBox.Text);
                    SetStatus(I18n.Text("已复制到剪贴板。", "Copied."), false);
                }
            };
            clearButton.Click += delegate
            {
                inputBox.Clear();
                outputBox.Clear();
                SetStatus(I18n.Text("准备好了。", "Ready."), false);
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
                ReloadTimeZoneOptions();
                ReloadDirectionOptions();
                ApplyUiLanguage();
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
                SetStatus(I18n.Text("先输入内容。", "Enter something first."), true);
                return;
            }

            SetBusy(true);
            SetStatus(I18n.Text("翻译中...", "Translating..."), false);

            try
            {
                object direction = directionBox.SelectedItem;
                string result = await Task.Run(delegate { return Translator.Translate(text, direction); });
                outputBox.Text = result;
                SetStatus(I18n.Text("好了，可以复制。", "Done. You can copy it."), false);
            }
            catch (Exception ex)
            {
                SetStatus(I18n.Text("没翻成，稍后再试。详情：", "Translation failed. Details: ") + ex.Message, true);
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
                SetStatus(autoStartCheck.Checked ? I18n.Text("开机自启已开启。", "Auto start is on.") : I18n.Text("开机自启已关闭。", "Auto start is off."), false);
            }
            catch (Exception ex)
            {
                SetStatus(I18n.Text("自启设置失败：", "Auto start failed: ") + ex.Message, true);
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
                notifyIcon.ShowBalloonTip(1200, AppInfo.ProductName, I18n.Text("已到托盘，右键可退出。", "Still running in the tray. Right-click to exit."), ToolTipIcon.Info);
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

        private void ShowSettings()
        {
            ShowMainWindow();
            using (SettingsForm settings = new SettingsForm(appIcon))
            {
                if (settings.ShowDialog(this) == DialogResult.OK)
                {
                    ApplyUiLanguage();
                    ReloadDirectionOptions();
                    ReloadTimeZoneOptions();
                    UpdateClock();
                    ApplyTheme();
                    SetStatus(I18n.Text("设置已保存。", "Settings saved."), false);
                }
            }
        }

        private void ApplyUiLanguage()
        {
            Text = AppInfo.ProductName;
            inputLabel.Text = I18n.Text("输入内容", "Input");
            outputLabel.Text = I18n.Text("翻译结果", "Result");
            translateButton.Text = I18n.Text("开始翻译", "Translate");
            copyButton.Text = I18n.Text("复制结果", "Copy");
            clearButton.Text = I18n.Text("清空", "Clear");
            settingsButton.Text = I18n.Text("设置", "Settings");
            topMostCheck.Text = I18n.Text("置顶", "Top");
            trayCheck.Text = I18n.Text("托盘", "Tray");
            themeCheck.Text = I18n.Text("夜间", "Dark");
            autoStartCheck.Text = I18n.Text("自启", "Startup");
            showTrayItem.Text = I18n.Text("显示", "Show");
            settingsTrayItem.Text = I18n.Text("设置", "Settings");
            aboutTrayItem.Text = I18n.Text("关于 / 更新", "About / Update");
            exitTrayItem.Text = I18n.Text("退出", "Exit");
            if (statusLabel.Text == "准备好了。" || statusLabel.Text == "Ready.")
            {
                SetStatus(I18n.Text("准备好了。", "Ready."), false);
            }
        }

        private void ReloadDirectionOptions()
        {
            object selected = directionBox.SelectedItem;
            string selectedKey = selected is DirectionOption ? ((DirectionOption)selected).Key : "auto";

            directionBox.Items.Clear();
            directionBox.Items.AddRange(Translator.GetDirectionOptions());
            for (int i = 0; i < directionBox.Items.Count; i++)
            {
                DirectionOption option = directionBox.Items[i] as DirectionOption;
                if (option != null && option.Key == selectedKey)
                {
                    directionBox.SelectedIndex = i;
                    return;
                }
            }
            if (directionBox.Items.Count > 0) directionBox.SelectedIndex = 0;
        }

        private void ReloadTimeZoneOptions()
        {
            string selectedKey = AppSettings.TimeZoneKey;
            timeZoneBox.Items.Clear();
            timeZoneBox.Items.AddRange(TimeZonePreset.GetAll());
            for (int i = 0; i < timeZoneBox.Items.Count; i++)
            {
                TimeZonePreset preset = timeZoneBox.Items[i] as TimeZonePreset;
                if (preset != null && preset.Key == selectedKey)
                {
                    timeZoneBox.SelectedIndex = i;
                    return;
                }
            }
            if (timeZoneBox.Items.Count > 0) timeZoneBox.SelectedIndex = 0;
        }

        private void SaveSelectedTimeZone()
        {
            TimeZonePreset preset = timeZoneBox.SelectedItem as TimeZonePreset;
            if (preset == null) return;
            AppSettings.TimeZoneKey = preset.Key;
            AppSettings.Save();
            UpdateClock();
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
            TimeZonePreset preset = TimeZonePreset.Find(AppSettings.TimeZoneKey);
            DateTime now = GetSelectedTimeNow(preset);
            timeLabel.Text = now.ToString("HH:mm:ss");
            dateLabel.Text = preset.DisplayName + "  " + now.ToString("yyyy-MM-dd ddd");
        }

        private DateTime GetSelectedTimeNow(TimeZonePreset preset)
        {
            try
            {
                TimeZoneInfo zone = TimeZoneInfo.FindSystemTimeZoneById(preset.WindowsId);
                return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zone);
            }
            catch
            {
                return DateTime.UtcNow.AddHours(preset.FallbackOffsetHours);
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

            foreach (Label label in new[] { statusLabel })
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
            timeZoneBox.BackColor = surface;
            timeZoneBox.ForeColor = text;
            inputBox.BackColor = surface;
            inputBox.ForeColor = text;
            outputBox.BackColor = surfaceAlt;
            outputBox.ForeColor = text;

            SetButtonTheme(translateButton, true);
            SetButtonTheme(copyButton, false);
            SetButtonTheme(clearButton, false);
            SetButtonTheme(settingsButton, false);
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
                Dock = DockStyle.Fill,
                Margin = new Padding(4, 4, 0, 4),
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

    internal static class JsonUtil
    {
        public static IEnumerable<object> EnumerateArray(object value)
        {
            if (value == null) yield break;

            object[] array = value as object[];
            if (array != null)
            {
                foreach (object item in array) yield return item;
                yield break;
            }

            IEnumerable enumerable = value as IEnumerable;
            if (enumerable == null || value is string) yield break;

            foreach (object item in enumerable) yield return item;
        }

        public static Dictionary<string, object> FirstObject(object value, string errorMessage)
        {
            foreach (object item in EnumerateArray(value))
            {
                Dictionary<string, object> result = item as Dictionary<string, object>;
                if (result != null) return result;
            }

            throw new InvalidOperationException(errorMessage);
        }
    }

    internal static class I18n
    {
        public static bool IsChinese
        {
            get { return AppSettings.UiLanguage == "zh-CN"; }
        }

        public static string Text(string zh, string en)
        {
            return IsChinese ? zh : en;
        }
    }

    internal static class AppSettings
    {
        public static string UiLanguage = DetectDefaultLanguage();
        public static string TimeZoneKey = "beijing";
        public static string DefaultTargetLanguageKey = "en";

        public static void Load()
        {
            string path = GetSettingsPath();
            if (!File.Exists(path)) return;

            foreach (string line in File.ReadAllLines(path, Encoding.UTF8))
            {
                int index = line.IndexOf('=');
                if (index <= 0) continue;
                string key = line.Substring(0, index).Trim();
                string value = line.Substring(index + 1).Trim();
                if (key == "ui_language") UiLanguage = value;
                if (key == "time_zone") TimeZoneKey = value;
                if (key == "default_target_language") DefaultTargetLanguageKey = value;
            }

            if (LanguageInfo.Find(DefaultTargetLanguageKey) == null) DefaultTargetLanguageKey = "en";
            if (TimeZonePreset.Find(TimeZoneKey) == null) TimeZoneKey = "beijing";
            if (UiLanguage != "zh-CN" && UiLanguage != "en-US") UiLanguage = DetectDefaultLanguage();
        }

        public static void Save()
        {
            string path = GetSettingsPath();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string content =
                "ui_language=" + UiLanguage + "\r\n" +
                "time_zone=" + TimeZoneKey + "\r\n" +
                "default_target_language=" + DefaultTargetLanguageKey + "\r\n";
            File.WriteAllText(path, content, Encoding.UTF8);
        }

        private static string GetSettingsPath()
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TimeLingo");
            return Path.Combine(dir, "settings.ini");
        }

        private static string DetectDefaultLanguage()
        {
            return CultureInfo.CurrentUICulture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase) ? "zh-CN" : "en-US";
        }
    }

    internal sealed class TimeZonePreset
    {
        public readonly string Key;
        public readonly string WindowsId;
        public readonly double FallbackOffsetHours;
        private readonly string zhName;
        private readonly string enName;

        public TimeZonePreset(string key, string windowsId, double fallbackOffsetHours, string zhName, string enName)
        {
            Key = key;
            WindowsId = windowsId;
            FallbackOffsetHours = fallbackOffsetHours;
            this.zhName = zhName;
            this.enName = enName;
        }

        public string DisplayName
        {
            get { return I18n.IsChinese ? zhName : enName; }
        }

        public override string ToString()
        {
            return DisplayName;
        }

        public static TimeZonePreset[] GetAll()
        {
            return new[]
            {
                new TimeZonePreset("beijing", "China Standard Time", 8, "北京 UTC+8", "Beijing UTC+8"),
                new TimeZonePreset("utc", "UTC", 0, "UTC 世界标准时间", "UTC"),
                new TimeZonePreset("pacific", "Pacific Standard Time", -8, "美国太平洋时间", "US Pacific"),
                new TimeZonePreset("eastern", "Eastern Standard Time", -5, "美国东部时间", "US Eastern"),
                new TimeZonePreset("london", "GMT Standard Time", 0, "伦敦时间", "London"),
                new TimeZonePreset("berlin", "W. Europe Standard Time", 1, "柏林 / 巴黎时间", "Berlin / Paris"),
                new TimeZonePreset("tokyo", "Tokyo Standard Time", 9, "东京时间", "Tokyo"),
                new TimeZonePreset("seoul", "Korea Standard Time", 9, "首尔时间", "Seoul"),
                new TimeZonePreset("singapore", "Singapore Standard Time", 8, "新加坡时间", "Singapore"),
                new TimeZonePreset("dubai", "Arabian Standard Time", 4, "迪拜时间", "Dubai"),
                new TimeZonePreset("sydney", "AUS Eastern Standard Time", 10, "悉尼时间", "Sydney")
            };
        }

        public static TimeZonePreset Find(string key)
        {
            foreach (TimeZonePreset preset in GetAll())
            {
                if (preset.Key == key) return preset;
            }
            return GetAll()[0];
        }
    }

    internal sealed class LanguageInfo
    {
        public readonly string Key;
        public readonly string MyMemoryCode;
        public readonly string MicrosoftCode;
        public readonly string DeepLSourceCode;
        public readonly string DeepLTargetCode;
        private readonly string zhName;
        private readonly string enName;

        public LanguageInfo(string key, string myMemoryCode, string microsoftCode, string deepLSourceCode, string deepLTargetCode, string zhName, string enName)
        {
            Key = key;
            MyMemoryCode = myMemoryCode;
            MicrosoftCode = microsoftCode;
            DeepLSourceCode = deepLSourceCode;
            DeepLTargetCode = deepLTargetCode;
            this.zhName = zhName;
            this.enName = enName;
        }

        public string DisplayName
        {
            get { return I18n.IsChinese ? zhName : enName; }
        }

        public override string ToString()
        {
            return DisplayName;
        }

        public static LanguageInfo[] GetAll()
        {
            return new[]
            {
                new LanguageInfo("zh", "zh-CN", "zh-Hans", "ZH", "ZH-HANS", "中文", "Chinese"),
                new LanguageInfo("en", "en", "en", "EN", "EN-US", "英文", "English"),
                new LanguageInfo("ja", "ja", "ja", "JA", "JA", "日文", "Japanese"),
                new LanguageInfo("ko", "ko", "ko", "KO", "KO", "韩文", "Korean"),
                new LanguageInfo("es", "es", "es", "ES", "ES", "西班牙文", "Spanish"),
                new LanguageInfo("fr", "fr", "fr", "FR", "FR", "法文", "French"),
                new LanguageInfo("de", "de", "de", "DE", "DE", "德文", "German"),
                new LanguageInfo("ru", "ru", "ru", "RU", "RU", "俄文", "Russian"),
                new LanguageInfo("pt", "pt", "pt", "PT", "PT-PT", "葡萄牙文", "Portuguese")
            };
        }

        public static LanguageInfo Find(string key)
        {
            foreach (LanguageInfo language in GetAll())
            {
                if (language.Key == key) return language;
            }
            return null;
        }
    }

    internal sealed class DirectionOption
    {
        public readonly string Key;
        public readonly string SourceKey;
        public readonly string TargetKey;

        public DirectionOption(string key, string sourceKey, string targetKey)
        {
            Key = key;
            SourceKey = sourceKey;
            TargetKey = targetKey;
        }

        public bool IsAuto
        {
            get { return Key == "auto"; }
        }

        public override string ToString()
        {
            if (IsAuto) return I18n.Text("自动判断", "Auto detect");
            LanguageInfo source = LanguageInfo.Find(SourceKey);
            LanguageInfo target = LanguageInfo.Find(TargetKey);
            return source.DisplayName + " -> " + target.DisplayName;
        }
    }

    internal static class Translator
    {
        private const int MaxChunkLength = 260;

        public static object[] GetDirectionOptions()
        {
            return new object[]
            {
                new DirectionOption("auto", "", ""),
                new DirectionOption("zh-en", "zh", "en"),
                new DirectionOption("en-zh", "en", "zh"),
                new DirectionOption("en-ja", "en", "ja"),
                new DirectionOption("ja-en", "ja", "en"),
                new DirectionOption("en-ko", "en", "ko"),
                new DirectionOption("ko-en", "ko", "en"),
                new DirectionOption("en-es", "en", "es"),
                new DirectionOption("es-en", "es", "en"),
                new DirectionOption("en-fr", "en", "fr"),
                new DirectionOption("fr-en", "fr", "en"),
                new DirectionOption("en-de", "en", "de"),
                new DirectionOption("de-en", "de", "en"),
                new DirectionOption("en-ru", "en", "ru"),
                new DirectionOption("ru-en", "ru", "en"),
                new DirectionOption("en-pt", "en", "pt"),
                new DirectionOption("pt-en", "pt", "en"),
                new DirectionOption("zh-ja", "zh", "ja"),
                new DirectionOption("ja-zh", "ja", "zh"),
                new DirectionOption("zh-ko", "zh", "ko"),
                new DirectionOption("ko-zh", "ko", "zh")
            };
        }

        public static string Translate(string text, object direction)
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
            Dictionary<string, object> first = JsonUtil.FirstObject(serializer.DeserializeObject(json), "Microsoft Translator 没有返回结果。");
            Dictionary<string, object> translation = JsonUtil.FirstObject(first["translations"], "Microsoft Translator 没有返回结果。");
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
            Dictionary<string, object> translation = JsonUtil.FirstObject(root["translations"], "DeepL 没有返回结果。");
            return Convert.ToString(translation["text"]).Trim();
        }

        private static DirectionInfo ResolveDirection(string text, object direction)
        {
            DirectionOption option = direction as DirectionOption;
            if (option != null && !option.IsAuto) return Pair(option.SourceKey, option.TargetKey);

            string sourceKey = DetectLanguageKey(text);
            string targetKey = AppSettings.DefaultTargetLanguageKey;
            if (sourceKey == targetKey) targetKey = sourceKey == "en" ? "zh" : "en";
            return Pair(sourceKey, targetKey);
        }

        private static DirectionInfo Pair(string sourceKey, string targetKey)
        {
            LanguageInfo source = LanguageInfo.Find(sourceKey);
            LanguageInfo target = LanguageInfo.Find(targetKey);
            if (source == null || target == null) throw new InvalidOperationException("Unsupported language direction.");
            return new DirectionInfo(source.MyMemoryCode, target.MyMemoryCode, source.MicrosoftCode, target.MicrosoftCode, source.DeepLSourceCode, target.DeepLTargetCode);
        }

        private static string DetectLanguageKey(string text)
        {
            if (Regex.IsMatch(text, @"[\u4e00-\u9fff]")) return "zh";
            if (Regex.IsMatch(text, @"[\u3040-\u30ff]")) return "ja";
            if (Regex.IsMatch(text, @"[\uac00-\ud7af]")) return "ko";
            if (Regex.IsMatch(text, @"[\u0400-\u04ff]")) return "ru";
            return "en";
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
            Text = I18n.Text("安装引导", "Setup");
            Size = new Size(520, 360);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Font = new Font("Microsoft YaHei UI", 9f);
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

            TableLayoutPanel layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(24),
                ColumnCount = 1,
                RowCount = 6
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
            Controls.Add(layout);

            Label title = new Label
            {
                Text = I18n.Text("安装 TimeLingo", "Install TimeLingo"),
                Dock = DockStyle.Fill,
                Font = new Font("Microsoft YaHei UI", 15f, FontStyle.Bold)
            };
            Label desc = new Label
            {
                Text = I18n.Text("推荐安装到本机，之后可以从桌面或开始菜单启动。也可以先直接试用。", "Install it for daily use, or run it once without installing."),
                Dock = DockStyle.Fill,
                ForeColor = Color.FromArgb(71, 85, 105)
            };
            Label info = new Label
            {
                Text = I18n.Text("安装位置：当前用户目录\r\n不会修改系统时区\r\n关闭窗口后可留在系统托盘", "Install location: current user folder\r\nIt does not change the system time zone\r\nIt can keep running in the system tray"),
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.FixedSingle,
                Padding = new Padding(10),
                ForeColor = Color.FromArgb(15, 23, 42),
                BackColor = Color.FromArgb(248, 250, 252)
            };
            desktopCheck = new CheckBox { Text = I18n.Text("创建桌面图标", "Create desktop shortcut"), Checked = true, AutoSize = true };
            autoStartCheck = new CheckBox { Text = I18n.Text("开机自动启动", "Start with Windows"), Checked = false, AutoSize = true };

            FlowLayoutPanel buttons = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.RightToLeft,
                Dock = DockStyle.Fill,
                WrapContents = false,
                Padding = new Padding(0, 8, 0, 0)
            };
            Button installButton = new Button { Text = I18n.Text("安装并打开", "Install"), Width = 116, Height = 34 };
            Button directButton = new Button { Text = I18n.Text("直接试用", "Run once"), Width = 92, Height = 34 };
            Button exitButton = new Button { Text = I18n.Text("退出", "Exit"), Width = 76, Height = 34 };
            buttons.Controls.Add(installButton);
            buttons.Controls.Add(directButton);
            buttons.Controls.Add(exitButton);

            layout.Controls.Add(title, 0, 0);
            layout.Controls.Add(desc, 0, 1);
            layout.Controls.Add(info, 0, 2);
            layout.Controls.Add(desktopCheck, 0, 3);
            layout.Controls.Add(autoStartCheck, 0, 4);
            layout.Controls.Add(buttons, 0, 5);
            AcceptButton = installButton;
            CancelButton = exitButton;

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
                    MessageBox.Show(this, I18n.Text("安装失败：", "Install failed: ") + ex.Message, AppInfo.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };
            directButton.Click += delegate
            {
                DialogResult = DialogResult.OK;
                Close();
            };
            exitButton.Click += delegate
            {
                DialogResult = DialogResult.Abort;
                Close();
            };
        }
    }

    internal sealed class SettingsForm : Form
    {
        private readonly ComboBox uiLanguageBox;
        private readonly ComboBox targetLanguageBox;

        public SettingsForm(Icon icon)
        {
            Text = I18n.Text("设置", "Settings");
            Size = new Size(500, 320);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Font = new Font("Microsoft YaHei UI", 9f);
            Icon = icon;

            TableLayoutPanel layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(24),
                ColumnCount = 2,
                RowCount = 5
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 132));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
            Controls.Add(layout);

            Label title = new Label
            {
                Text = I18n.Text("偏好设置", "Preferences"),
                Dock = DockStyle.Fill,
                Font = new Font("Microsoft YaHei UI", 15f, FontStyle.Bold)
            };
            layout.Controls.Add(title, 0, 0);
            layout.SetColumnSpan(title, 2);

            layout.Controls.Add(NewSettingsLabel(I18n.Text("界面语言", "App language")), 0, 1);
            uiLanguageBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
            uiLanguageBox.Items.Add(new LanguageChoice("zh-CN", "简体中文"));
            uiLanguageBox.Items.Add(new LanguageChoice("en-US", "English"));
            SelectLanguageChoice(uiLanguageBox, AppSettings.UiLanguage);
            layout.Controls.Add(uiLanguageBox, 1, 1);

            layout.Controls.Add(NewSettingsLabel(I18n.Text("自动翻译到", "Auto translate to")), 0, 2);
            targetLanguageBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
            targetLanguageBox.Items.AddRange(LanguageInfo.GetAll());
            SelectTargetLanguage();
            layout.Controls.Add(targetLanguageBox, 1, 2);

            Label hint = new Label
            {
                Text = I18n.Text("时区可以在主窗口右上角直接选择。自动判断会优先翻译到这里设置的目标语言。", "Time zone can be changed from the main window. Auto detect translates to the target language selected here."),
                Dock = DockStyle.Fill,
                ForeColor = Color.FromArgb(71, 85, 105)
            };
            layout.Controls.Add(hint, 0, 3);
            layout.SetColumnSpan(hint, 2);

            FlowLayoutPanel buttons = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.RightToLeft,
                Dock = DockStyle.Fill,
                WrapContents = false,
                Padding = new Padding(0, 8, 0, 0)
            };
            Button saveButton = new Button { Text = I18n.Text("保存", "Save"), Width = 90, Height = 34 };
            Button cancelButton = new Button { Text = I18n.Text("取消", "Cancel"), Width = 90, Height = 34 };
            Button aboutButton = new Button { Text = I18n.Text("关于 / 更新", "About / Update"), Width = 116, Height = 34 };
            buttons.Controls.Add(saveButton);
            buttons.Controls.Add(cancelButton);
            buttons.Controls.Add(aboutButton);
            layout.Controls.Add(buttons, 0, 4);
            layout.SetColumnSpan(buttons, 2);

            AcceptButton = saveButton;
            CancelButton = cancelButton;

            saveButton.Click += delegate
            {
                LanguageChoice uiLanguage = uiLanguageBox.SelectedItem as LanguageChoice;
                LanguageInfo targetLanguage = targetLanguageBox.SelectedItem as LanguageInfo;
                if (uiLanguage != null) AppSettings.UiLanguage = uiLanguage.Key;
                if (targetLanguage != null) AppSettings.DefaultTargetLanguageKey = targetLanguage.Key;
                AppSettings.Save();
                DialogResult = DialogResult.OK;
                Close();
            };
            cancelButton.Click += delegate
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };
            aboutButton.Click += delegate
            {
                using (AboutForm about = new AboutForm(icon))
                {
                    about.ShowDialog(this);
                }
            };
        }

        private Label NewSettingsLabel(string text)
        {
            return new Label { Text = text, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
        }

        private void SelectLanguageChoice(ComboBox box, string key)
        {
            for (int i = 0; i < box.Items.Count; i++)
            {
                LanguageChoice choice = box.Items[i] as LanguageChoice;
                if (choice != null && choice.Key == key)
                {
                    box.SelectedIndex = i;
                    return;
                }
            }
            box.SelectedIndex = 0;
        }

        private void SelectTargetLanguage()
        {
            for (int i = 0; i < targetLanguageBox.Items.Count; i++)
            {
                LanguageInfo language = targetLanguageBox.Items[i] as LanguageInfo;
                if (language != null && language.Key == AppSettings.DefaultTargetLanguageKey)
                {
                    targetLanguageBox.SelectedIndex = i;
                    return;
                }
            }
            targetLanguageBox.SelectedIndex = 1;
        }

        private sealed class LanguageChoice
        {
            public readonly string Key;
            private readonly string label;

            public LanguageChoice(string key, string label)
            {
                Key = key;
                this.label = label;
            }

            public override string ToString()
            {
                return label;
            }
        }
    }

    internal sealed class AboutForm : Form
    {
        private readonly Label statusLabel;
        private readonly Button updateButton;

        public AboutForm(Icon icon)
        {
            Text = I18n.Text("关于", "About");
            Size = new Size(500, 280);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Font = new Font("Microsoft YaHei UI", 9f);
            Icon = icon;

            TableLayoutPanel layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(24),
                ColumnCount = 1,
                RowCount = 4
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
            Controls.Add(layout);

            Label title = new Label
            {
                Text = AppInfo.ProductName,
                Dock = DockStyle.Fill,
                Font = new Font("Microsoft YaHei UI", 15f, FontStyle.Bold)
            };
            Label desc = new Label
            {
                Text = I18n.Text("版本 ", "Version ") + AppInfo.Version + I18n.Text("\r\n世界时间和多语言翻译小工具。", "\r\nA small world-time and multilingual translation tool."),
                Dock = DockStyle.Fill,
                ForeColor = Color.FromArgb(71, 85, 105)
            };
            statusLabel = new Label
            {
                Text = I18n.Text("可以检查 GitHub 上的新版本。", "Check GitHub for updates."),
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.FixedSingle,
                Padding = new Padding(10),
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true
            };

            FlowLayoutPanel buttons = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.RightToLeft,
                Dock = DockStyle.Fill,
                WrapContents = false,
                Padding = new Padding(0, 8, 0, 0)
            };
            Button closeButton = new Button { Text = I18n.Text("关闭", "Close"), Width = 90, Height = 34 };
            Button githubButton = new Button { Text = "GitHub", Width = 90, Height = 34 };
            updateButton = new Button { Text = I18n.Text("检查更新", "Check Update"), Width = 112, Height = 34 };
            buttons.Controls.Add(closeButton);
            buttons.Controls.Add(githubButton);
            buttons.Controls.Add(updateButton);

            layout.Controls.Add(title, 0, 0);
            layout.Controls.Add(desc, 0, 1);
            layout.Controls.Add(statusLabel, 0, 2);
            layout.Controls.Add(buttons, 0, 3);
            AcceptButton = updateButton;
            CancelButton = closeButton;

            closeButton.Click += delegate { Close(); };
            githubButton.Click += delegate { Process.Start(AppInfo.RepoUrl); };
            updateButton.Click += async delegate { await CheckUpdateAsync(); };
        }

        private async Task CheckUpdateAsync()
        {
            updateButton.Enabled = false;
            statusLabel.Text = I18n.Text("正在检查...", "Checking...");
            try
            {
                ReleaseInfo latest = await Task.Run(delegate { return UpdateManager.GetLatestRelease(); });
                if (VersionUtil.Compare(latest.Version, AppInfo.Version) <= 0)
                {
                    statusLabel.Text = I18n.Text("已经是最新版。", "Already up to date.");
                    return;
                }

                DialogResult confirm = MessageBox.Show(
                    this,
                    I18n.Text("发现新版本 ", "New version ") + latest.Tag + I18n.Text("，现在更新吗？", " is available. Update now?"),
                    AppInfo.ProductName,
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);
                if (confirm != DialogResult.Yes)
                {
                    statusLabel.Text = I18n.Text("已取消更新。", "Update canceled.");
                    return;
                }

                statusLabel.Text = I18n.Text("正在下载更新...", "Downloading update...");
                await Task.Run(delegate { UpdateManager.DownloadAndRestart(latest); });
                Application.Exit();
            }
            catch (Exception ex)
            {
                statusLabel.Text = I18n.Text("更新失败：", "Update failed: ") + ex.Message;
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
            IntPtr handle = NativeMethods.FindWindow(null, AppInfo.ProductName);
            if (handle == IntPtr.Zero) handle = NativeMethods.FindWindow(null, "Claude 中文桥");
            if (handle == IntPtr.Zero) handle = NativeMethods.FindWindow(null, "北京时间翻译助手");
            if (handle == IntPtr.Zero) handle = NativeMethods.FindWindow(null, I18n.Text("安装引导", "Setup"));
            if (handle == IntPtr.Zero) handle = NativeMethods.FindWindow(null, I18n.Text("关于", "About"));
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
            return SamePath(currentExe, GetInstalledExePath()) || SamePath(currentExe, GetLegacyInstalledExePath()) || SamePath(currentExe, GetVeryLegacyInstalledExePath());
        }

        private static string GetInstallDir()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "TimeLingo");
        }

        private static string GetLegacyInstallDir()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "ClaudeBridgeCN");
        }

        private static string GetVeryLegacyInstallDir()
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

        private static string GetVeryLegacyInstalledExePath()
        {
            return Path.Combine(GetVeryLegacyInstallDir(), AppInfo.VeryLegacyAssetName);
        }

        private static string GetDesktopShortcutPath()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "TimeLingo.lnk");
        }

        private static string GetStartMenuShortcutPath()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs", "TimeLingo.lnk");
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
            List<Dictionary<string, object>> assets = new List<Dictionary<string, object>>();

            if (root.ContainsKey("assets"))
            {
                foreach (object item in JsonUtil.EnumerateArray(root["assets"]))
                {
                    Dictionary<string, object> asset = item as Dictionary<string, object>;
                    if (asset == null) continue;
                    assets.Add(asset);
                }
            }

            foreach (string assetName in AppInfo.ReleaseAssetNames)
            {
                foreach (Dictionary<string, object> asset in assets)
                {
                    string name = Convert.ToString(asset["name"]);
                    if (string.Equals(name, assetName, StringComparison.OrdinalIgnoreCase))
                    {
                        downloadUrl = Convert.ToString(asset["browser_download_url"]);
                        break;
                    }
                }
                if (!string.IsNullOrWhiteSpace(downloadUrl)) break;
            }

            if (string.IsNullOrWhiteSpace(downloadUrl))
            {
                throw new InvalidOperationException("没有找到 Windows exe 下载文件。");
            }

            return new ReleaseInfo(tag, htmlUrl, downloadUrl);
        }

        public static void DownloadAndRestart(ReleaseInfo release)
        {
            string tempExe = Path.Combine(Path.GetTempPath(), "TimeLingo-update-" + Guid.NewGuid().ToString("N") + ".exe");
            using (WebClient client = new WebClient())
            {
                client.Headers.Add("User-Agent", AppInfo.ProductName);
                client.DownloadFile(release.DownloadUrl, tempExe);
            }

            string script = Path.Combine(Path.GetTempPath(), "TimeLingo-update-" + Guid.NewGuid().ToString("N") + ".cmd");
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
        private const string ShortcutName = "TimeLingo.lnk";
        private const string LegacyShortcutName = "ClaudeBridgeCN.lnk";
        private const string VeryLegacyShortcutName = "BeijingClaudeTranslator.lnk";

        public static bool IsEnabled()
        {
            return File.Exists(GetShortcutPath(ShortcutName)) || File.Exists(GetShortcutPath(LegacyShortcutName)) || File.Exists(GetShortcutPath(VeryLegacyShortcutName));
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
            string veryLegacyShortcutPath = GetShortcutPath(VeryLegacyShortcutName);
            if (File.Exists(veryLegacyShortcutPath)) File.Delete(veryLegacyShortcutPath);
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
            SetProperty(shortcutType, shortcut, "Description", AppInfo.ProductName);
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
