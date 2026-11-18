using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Serialization;

namespace ClickCircle
{
    public sealed class StampPanel : Form
    {
        private readonly TextBox subjectBox;
        private readonly ComboBox separatorInput;
        private readonly NumericUpDown widthInput;
        private readonly NumericUpDown heightInput;
        private readonly Button pickButton;
        private readonly Button ocrButton;
        private readonly Button resumeButton;
        private readonly Button pauseButton;
        private readonly Button hotkeyButton;
        private readonly Button clearHotkeyButton;
        private readonly Label statusLabel;
        private HotkeyBinding hotkey = HotkeyBinding.Default;
        private bool capturingHotkey;
        private string currentMode = "paused";
        private bool applyingSettings;
        private bool updatingSeparator;
        private bool composingSubject;
        private string separator = "/";
        private string[] pickedFragments;

        public event Action PickRequested;
        public event Action OcrRequested;
        public event Action ResumeRequested;
        public event Action PauseRequested;
        public event Action DimensionsChanged;
        public event Action SeparatorChanged;
        public event Action HotkeyChanged;
        public event Action HotkeyCaptureStarted;
        public event Action HotkeyCaptureStopped;

        public StampPanel() : this(new PluginSettings())
        {
        }

        public StampPanel(PluginSettings settings)
        {
            Text = "取字连续圈注";
            AutoScaleMode = AutoScaleMode.None;
            ClientSize = new Size(440, 318);
            FormBorderStyle = FormBorderStyle.FixedToolWindow;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            TopMost = false;
            KeyPreview = true;
            Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Regular);
            Padding = new Padding(12);

            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.ColumnCount = 1;
            layout.RowCount = 6;
            layout.Margin = Padding.Empty;
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            Controls.Add(layout);

            TableLayoutPanel subjectRow = new TableLayoutPanel();
            subjectRow.Dock = DockStyle.Fill;
            subjectRow.ColumnCount = 2;
            subjectRow.RowCount = 1;
            subjectRow.Margin = Padding.Empty;
            subjectRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 48F));
            subjectRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            Label subjectLabel = MakeLabel("主题", "标注主题");
            subjectBox = new TextBox();
            subjectBox.Dock = DockStyle.Top;
            subjectBox.Margin = new Padding(0, 3, 0, 0);
            subjectBox.AccessibleName = "标注主题";
            subjectBox.Name = "Subject";
            subjectBox.Multiline = false;
            subjectBox.WordWrap = false;
            subjectBox.TabIndex = 0;
            subjectBox.TextChanged += delegate {
                if (!composingSubject)
                    pickedFragments = null;
                UpdateButtonState();
            };
            subjectRow.Controls.Add(subjectLabel, 0, 0);
            subjectRow.Controls.Add(subjectBox, 1, 0);
            layout.Controls.Add(subjectRow, 0, 0);

            TableLayoutPanel separatorRow = new TableLayoutPanel();
            separatorRow.Dock = DockStyle.Fill;
            separatorRow.ColumnCount = 2;
            separatorRow.RowCount = 1;
            separatorRow.Margin = Padding.Empty;
            separatorRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72F));
            separatorRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            separatorInput = new ComboBox();
            separatorInput.Name = "Separator";
            separatorInput.AccessibleName = "文字分隔符";
            separatorInput.Dock = DockStyle.Top;
            separatorInput.Margin = new Padding(0, 3, 0, 0);
            separatorInput.DropDownStyle = ComboBoxStyle.DropDown;
            separatorInput.AutoCompleteMode = AutoCompleteMode.None;
            separatorInput.TabIndex = 1;
            separatorInput.Items.AddRange(new object[] {
                new SeparatorPreset("/", "/"),
                new SeparatorPreset("-", "-"),
                new SeparatorPreset("，", "，"),
                new SeparatorPreset("、", "、"),
                new SeparatorPreset("空格", " "),
                new SeparatorPreset("无分隔符", String.Empty)
            });
            separatorInput.TextChanged += SeparatorInputChanged;
            separatorInput.SelectedIndexChanged += SeparatorInputChanged;
            separatorRow.Controls.Add(MakeLabel("分隔符", "文字分隔符"), 0, 0);
            separatorRow.Controls.Add(separatorInput, 1, 0);
            layout.Controls.Add(separatorRow, 0, 1);

            TableLayoutPanel dimensionsRow = new TableLayoutPanel();
            dimensionsRow.Dock = DockStyle.Fill;
            dimensionsRow.ColumnCount = 4;
            dimensionsRow.RowCount = 1;
            dimensionsRow.Margin = Padding.Empty;
            dimensionsRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72F));
            dimensionsRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            dimensionsRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72F));
            dimensionsRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            widthInput = MakeDimensionInput("椭圆宽度，毫米", 2);
            heightInput = MakeDimensionInput("椭圆高度，毫米", 3);
            widthInput.Margin = new Padding(0, 3, 12, 0);
            heightInput.Margin = new Padding(0, 3, 0, 0);
            dimensionsRow.Controls.Add(MakeLabel("宽 mm", "椭圆宽度"), 0, 0);
            dimensionsRow.Controls.Add(widthInput, 1, 0);
            dimensionsRow.Controls.Add(MakeLabel("高 mm", "椭圆高度"), 2, 0);
            dimensionsRow.Controls.Add(heightInput, 3, 0);
            layout.Controls.Add(dimensionsRow, 0, 2);

            TableLayoutPanel commandsRow = new TableLayoutPanel();
            commandsRow.Dock = DockStyle.Fill;
            commandsRow.ColumnCount = 4;
            commandsRow.RowCount = 1;
            commandsRow.Margin = Padding.Empty;
            for (int column = 0; column < 4; column++)
                commandsRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            pickButton = MakeButton("取文字", 4);
            ocrButton = MakeButton("区域识别", 5);
            ocrButton.Name = "OcrPick";
            resumeButton = MakeButton("继续标注", 6);
            pauseButton = MakeButton("暂停", 7);
            pickButton.Margin = new Padding(0, 3, 8, 3);
            ocrButton.Margin = new Padding(0, 3, 8, 3);
            resumeButton.Margin = new Padding(0, 3, 8, 3);
            pauseButton.Margin = new Padding(0, 3, 0, 3);
            pickButton.Click += delegate { Raise(PickRequested); };
            ocrButton.Click += delegate { Raise(OcrRequested); };
            resumeButton.Click += delegate { Raise(ResumeRequested); };
            pauseButton.Click += delegate { Raise(PauseRequested); };
            commandsRow.Controls.Add(pickButton, 0, 0);
            commandsRow.Controls.Add(ocrButton, 1, 0);
            commandsRow.Controls.Add(resumeButton, 2, 0);
            commandsRow.Controls.Add(pauseButton, 3, 0);
            layout.Controls.Add(commandsRow, 0, 3);

            TableLayoutPanel hotkeyRow = new TableLayoutPanel();
            hotkeyRow.Dock = DockStyle.Fill;
            hotkeyRow.ColumnCount = 3;
            hotkeyRow.RowCount = 1;
            hotkeyRow.Margin = Padding.Empty;
            hotkeyRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72F));
            hotkeyRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            hotkeyRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 60F));
            hotkeyButton = MakeButton(String.Empty, 8);
            hotkeyButton.Margin = new Padding(0, 3, 8, 3);
            hotkeyButton.Click += delegate { BeginHotkeyCapture(); };
            clearHotkeyButton = MakeButton("清除", 9);
            clearHotkeyButton.Margin = new Padding(0, 3, 0, 3);
            clearHotkeyButton.Click += delegate { SetHotkey(HotkeyBinding.Empty); };
            hotkeyRow.Controls.Add(MakeLabel("快捷键", "取文字快捷键"), 0, 0);
            hotkeyRow.Controls.Add(hotkeyButton, 1, 0);
            hotkeyRow.Controls.Add(clearHotkeyButton, 2, 0);
            layout.Controls.Add(hotkeyRow, 0, 4);

            statusLabel = new Label();
            statusLabel.Dock = DockStyle.Fill;
            statusLabel.Margin = new Padding(0, 5, 0, 0);
            statusLabel.TextAlign = ContentAlignment.MiddleLeft;
            statusLabel.AccessibleName = "圈注状态";
            statusLabel.AutoEllipsis = false;
            layout.Controls.Add(statusLabel, 0, 5);

            widthInput.ValueChanged += DimensionValueChanged;
            heightInput.ValueChanged += DimensionValueChanged;
            Deactivate += delegate { CancelHotkeyCapture(); };
            ApplySettings(settings ?? new PluginSettings());
            UpdateHotkeyButton();
            SetMode("paused", "待取字");
        }

        public string Subject
        {
            get { return subjectBox.Text; }
            set
            {
                pickedFragments = null;
                subjectBox.Text = value ?? String.Empty;
            }
        }

        public string Separator
        {
            get { return separator; }
            set { SetSeparator(value, true); }
        }

        public void SetPickedFragments(IList<string> fragments)
        {
            if (fragments == null)
                throw new ArgumentNullException("fragments");
            List<string> values = new List<string>();
            foreach (string fragment in fragments)
                if (!String.IsNullOrWhiteSpace(fragment))
                    values.Add(fragment);
            if (values.Count == 0)
                return;
            pickedFragments = values.ToArray();
            RecomposeSubject();
        }

        /// <summary>已取到、可随分隔符重组的文字片段数量；手动改过主题后为 0。</summary>
        public int PickedFragmentCount
        {
            get { return pickedFragments == null ? 0 : pickedFragments.Length; }
        }

        /// <summary>开始新一轮取字：主题文本保留，但下一次取字重新开始累积。</summary>
        public void ResetPickedFragments()
        {
            pickedFragments = null;
        }

        /// <summary>
        /// 把本次取到的片段追加到已取片段后面重新合并。新一轮取字的第一次取字直接替换旧主题。
        /// 全部为空时不改动主题并返回 false。
        /// </summary>
        public bool AppendPickedFragments(IList<string> fragments)
        {
            if (fragments == null)
                throw new ArgumentNullException("fragments");
            List<string> values = new List<string>();
            foreach (string fragment in fragments)
                if (!String.IsNullOrWhiteSpace(fragment))
                    values.Add(fragment);
            if (values.Count == 0)
                return false;
            List<string> merged = pickedFragments == null
                ? new List<string>() : new List<string>(pickedFragments);
            merged.AddRange(values);
            pickedFragments = merged.ToArray();
            RecomposeSubject();
            return true;
        }

        private void RecomposeSubject()
        {
            if (pickedFragments == null)
                return;
            composingSubject = true;
            try { subjectBox.Text = String.Join(separator, pickedFragments); }
            finally { composingSubject = false; }
        }

        private void SeparatorInputChanged(object sender, EventArgs e)
        {
            if (updatingSeparator)
                return;
            SeparatorPreset preset = separatorInput.SelectedItem as SeparatorPreset;
            string value = preset != null && separatorInput.Text == preset.Label
                ? preset.Value : separatorInput.Text;
            SetSeparator(value, false);
        }

        private void SetSeparator(string value, bool updateControl)
        {
            string normalized = value ?? "/";
            bool changed = separator != normalized;
            separator = normalized;
            if (updateControl)
            {
                updatingSeparator = true;
                try
                {
                    int selected = -1;
                    for (int index = 0; index < separatorInput.Items.Count; ++index)
                        if (((SeparatorPreset)separatorInput.Items[index]).Value == normalized)
                        {
                            selected = index;
                            break;
                        }
                    separatorInput.SelectedIndex = selected;
                    if (selected < 0)
                        separatorInput.Text = normalized;
                }
                finally { updatingSeparator = false; }
            }
            if (!changed)
                return;
            RecomposeSubject();
            if (!applyingSettings)
                Raise(SeparatorChanged);
        }

        private sealed class SeparatorPreset
        {
            public readonly string Label;
            public readonly string Value;

            public SeparatorPreset(string label, string value)
            {
                Label = label;
                Value = value;
            }

            public override string ToString() { return Label; }
        }

        public double WidthMm
        {
            get { return (double)widthInput.Value; }
        }

        public double HeightMm
        {
            get { return (double)heightInput.Value; }
        }

        /// <summary>当前“取文字”快捷键；IsEmpty 表示未设置。</summary>
        public HotkeyBinding Hotkey
        {
            get { return hotkey; }
        }

        /// <summary>焦点是否落在主题、分隔符或尺寸输入框里。</summary>
        public bool IsTextInputFocused()
        {
            if (!ContainsFocus)
                return false;
            Control focused = FindFocusedControl(this);
            return focused is TextBoxBase || focused is NumericUpDown || focused is ComboBox;
        }

        private static Control FindFocusedControl(Control parent)
        {
            if (parent == null)
                return null;
            foreach (Control child in parent.Controls)
            {
                if (child.Focused)
                    return child;
                if (child.ContainsFocus)
                {
                    if (child is ComboBox)
                        return child;
                    Control nested = FindFocusedControl(child);
                    if (nested != null)
                        return nested;
                }
            }
            return null;
        }

        public void ApplySettings(PluginSettings settings)
        {
            if (settings == null)
                throw new ArgumentNullException("settings");

            applyingSettings = true;
            try
            {
                widthInput.Value = PluginSettings.ClampDimension(settings.WidthMm, 1.5);
                heightInput.Value = PluginSettings.ClampDimension(settings.HeightMm, 1.2);
                Separator = settings.Separator;
            }
            finally
            {
                applyingSettings = false;
            }
            hotkey = HotkeyBinding.ParseOrDefault(settings.PickHotkey);
            UpdateHotkeyButton();
        }

        public PluginSettings GetSettings()
        {
            return new PluginSettings {
                WidthMm = WidthMm,
                HeightMm = HeightMm,
                PickHotkey = hotkey.ToStorage(),
                Separator = Separator
            };
        }

        // Modes: "picking", "stamping", and "paused"; aliases keep the host simple.
        public void SetMode(string mode, string status = null)
        {
            string normalized = (mode ?? String.Empty).Trim().ToLowerInvariant();
            if (normalized == "pick" || normalized == "picking" || normalized == "取字")
                currentMode = "picking";
            else if (normalized == "stamp" || normalized == "stamping" ||
                     normalized == "marking" || normalized == "标注中")
                currentMode = "stamping";
            else
                currentMode = "paused";

            if (status == null)
            {
                status = currentMode == "picking" ? "待取字" :
                    currentMode == "stamping" ? "标注中" : "已暂停";
            }
            statusLabel.Text = status;
            UpdateButtonState();
        }

        /// <summary>只改状态文字，不动当前模式（用于快捷键不可用等提示）。</summary>
        public void SetStatus(string text)
        {
            if (statusLabel != null)
                statusLabel.Text = text ?? String.Empty;
        }

        /// <summary>
        /// 处理快捷键录入期间的一次按键。由 ProcessCmdKey 转发；独立成公开方法也便于自动化验证。
        /// 返回 true 表示该按键已被录制流程消费。
        /// </summary>
        public bool HandleHotkeyKey(Keys keyData)
        {
            if (!capturingHotkey)
                return false;
            Keys key = keyData & Keys.KeyCode;
            if (key == Keys.Escape)
            {
                CancelHotkeyCapture();
                SetStatus("已取消快捷键设置");
                return true;
            }
            if (HotkeyBinding.IsModifierKey(key))
                return true;
            if (key == Keys.Back || key == Keys.Delete)
            {
                CancelHotkeyCapture();
                SetHotkey(HotkeyBinding.Empty);
                return true;
            }
            if (key == Keys.None)
                return true;
            CancelHotkeyCapture();
            SetHotkey(new HotkeyBinding(key, keyData & Keys.Modifiers));
            return true;
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (HandleHotkeyKey(keyData))
                return true;
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void BeginHotkeyCapture()
        {
            if (capturingHotkey)
            {
                CancelHotkeyCapture();
                return;
            }
            capturingHotkey = true;
            UpdateHotkeyButton();
            SetStatus("请按要使用的键（Esc 取消）");
            if (!hotkeyButton.Focused)
                hotkeyButton.Focus();
            Raise(HotkeyCaptureStarted);
        }

        private void CancelHotkeyCapture()
        {
            if (!capturingHotkey)
                return;
            capturingHotkey = false;
            UpdateHotkeyButton();
            Raise(HotkeyCaptureStopped);
        }

        /// <summary>快捷键引擎在键盘钩子里抓到按键后回调；value 为 null 表示用户按了 Esc。</summary>
        public void CompleteHotkeyCapture(HotkeyBinding value)
        {
            if (!capturingHotkey)
                return;
            capturingHotkey = false;
            if (value == null)
            {
                UpdateHotkeyButton();
                SetStatus("已取消快捷键设置");
                return;
            }
            SetHotkey(value);
        }

        private void SetHotkey(HotkeyBinding value)
        {
            HotkeyBinding normalized = value ?? HotkeyBinding.Empty;
            hotkey = normalized;
            UpdateHotkeyButton();
            // Applying the binding may replace this status with an error.
            SetStatus(hotkey.IsEmpty ? "快捷键已清除" : "快捷键已设为 " + hotkey.ToDisplay());
            Raise(HotkeyChanged);
            if (hotkey.IsEmpty)
                SetStatus("快捷键已清除");
        }

        private void UpdateHotkeyButton()
        {
            if (hotkeyButton == null)
                return;
            string display = hotkey.IsEmpty ? "未设置" : hotkey.ToDisplay();
            hotkeyButton.Text = capturingHotkey ? "请按新的组合键…" :
                hotkey.IsEmpty ? "未设置（点击设置）" : display;
            hotkeyButton.AccessibleName = "取文字快捷键：" + display;
            if (clearHotkeyButton != null)
                clearHotkeyButton.Enabled = !hotkey.IsEmpty && !capturingHotkey;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                Raise(PauseRequested);
            }
            base.OnKeyDown(e);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Raise(PauseRequested);
                Hide();
            }
            base.OnFormClosing(e);
        }

        private void UpdateButtonState()
        {
            if (resumeButton == null)
                return;
            resumeButton.Enabled = currentMode != "stamping" &&
                !String.IsNullOrWhiteSpace(subjectBox.Text);
            pauseButton.Enabled = currentMode != "paused";
            pickButton.Enabled = true;
        }

        private void DimensionValueChanged(object sender, EventArgs e)
        {
            if (!applyingSettings)
                Raise(DimensionsChanged);
        }

        private static void Raise(Action handler)
        {
            if (handler != null)
                handler();
        }

        private static Label MakeLabel(string text, string accessibleName)
        {
            return new Label {
                Text = text,
                AccessibleName = accessibleName,
                Dock = DockStyle.Fill,
                Margin = Padding.Empty,
                TextAlign = ContentAlignment.MiddleLeft
            };
        }

        private static NumericUpDown MakeDimensionInput(string accessibleName, int tabIndex)
        {
            return new NumericUpDown {
                Minimum = 0.1M,
                Maximum = 100M,
                DecimalPlaces = 2,
                Increment = 0.1M,
                Dock = DockStyle.Top,
                TextAlign = HorizontalAlignment.Right,
                AccessibleName = accessibleName,
                TabIndex = tabIndex
            };
        }

        private static Button MakeButton(string text, int tabIndex)
        {
            return new Button {
                Text = text,
                AccessibleName = text,
                Dock = DockStyle.Fill,
                UseVisualStyleBackColor = true,
                TabIndex = tabIndex
            };
        }
    }

    [XmlRoot("CircleStampSettings")]
    public sealed class PluginSettings
    {
        public PluginSettings()
        {
            WidthMm = 1.5;
            HeightMm = 1.2;
            PickHotkey = HotkeyBinding.Default.ToStorage();
            Separator = "/";
        }

        public double WidthMm { get; set; }
        public double HeightMm { get; set; }

        /// <summary>"Ctrl+Alt+P" 形式的快捷键文本；"None" 表示不注册。</summary>
        public string PickHotkey { get; set; }
        public string Separator { get; set; }

        public static PluginSettings Load(string path)
        {
            try
            {
                if (!File.Exists(path))
                    return new PluginSettings();
                XmlReaderSettings readerSettings = new XmlReaderSettings {
                    DtdProcessing = DtdProcessing.Prohibit,
                    XmlResolver = null
                };
                using (XmlReader reader = XmlReader.Create(path, readerSettings))
                {
                    PluginSettings settings = (PluginSettings)
                        new XmlSerializer(typeof(PluginSettings)).Deserialize(reader);
                    if (settings == null)
                        return new PluginSettings();
                    settings.WidthMm = (double)ClampDimension(settings.WidthMm, 1.5);
                    settings.HeightMm = (double)ClampDimension(settings.HeightMm, 1.2);
                    settings.PickHotkey = HotkeyBinding.ParseOrDefault(settings.PickHotkey).ToStorage();
                    if (settings.Separator == null)
                        settings.Separator = "/";
                    return settings;
                }
            }
            catch (Exception)
            {
                return new PluginSettings();
            }
        }

        public bool Save(string path)
        {
            try
            {
                string directory = Path.GetDirectoryName(path);
                if (!String.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);
                PluginSettings sanitized = new PluginSettings {
                    WidthMm = (double)ClampDimension(WidthMm, 1.5),
                    HeightMm = (double)ClampDimension(HeightMm, 1.2),
                    PickHotkey = HotkeyBinding.ParseOrDefault(PickHotkey).ToStorage(),
                    Separator = Separator ?? "/"
                };
                XmlWriterSettings writerSettings = new XmlWriterSettings {
                    Indent = true,
                    CloseOutput = true
                };
                using (XmlWriter writer = XmlWriter.Create(path, writerSettings))
                    new XmlSerializer(typeof(PluginSettings)).Serialize(writer, sanitized);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        internal static decimal ClampDimension(double value, double fallback)
        {
            if (Double.IsNaN(value) || Double.IsInfinity(value))
                value = fallback;
            return Decimal.Round((decimal)Math.Min(100, Math.Max(0.1, value)), 2);
        }
    }
}
