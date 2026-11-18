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
        private readonly TextBox historySearch;
        private readonly DataGridView historyGrid;
        private readonly Label historyStatus;
        private SubjectHistory history = new SubjectHistory();
        private bool updatingHistory;
        private string ocrLanguage = String.Empty;
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
        public event Action HistoryChanged;
        public event Action OcrLanguageChanged;

        /// <summary>用户拖动窗口边缘改尺寸后触发，用于记住面板大小。</summary>
        public event Action PanelSizeChanged;

        /// <summary>默认窗口客户区尺寸。</summary>
        public const int DefaultPanelWidth = 480;
        public const int DefaultPanelHeight = 566;

        /// <summary>最小客户区尺寸：标题、取字行、分隔符、尺寸、按钮、快捷键、状态行与历史表格都要露出来。</summary>
        public const int MinimumPanelWidth = 440;
        public const int MinimumPanelHeight = 380;

        public StampPanel() : this(new PluginSettings())
        {
        }

        public StampPanel(PluginSettings settings)
        {
            Text = "取字连续圈注";
            AutoScaleMode = AutoScaleMode.None;
            ClientSize = new Size(DefaultPanelWidth, DefaultPanelHeight);
            FormBorderStyle = FormBorderStyle.SizableToolWindow;
            // MinimumSize 按窗口尺寸算：先按常见工具窗口边框（左右各 16、标题加边框 48）估一份，
            // OnLoad 里用真实边框再校正一次，保证客户区不小于 MinimumPanelWidth × MinimumPanelHeight。
            MinimumSize = new Size(MinimumPanelWidth + 32, MinimumPanelHeight + 48);
            MaximumSize = new Size(4000, 4000);
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            TopMost = false;
            KeyPreview = true;
            Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Regular);
            Padding = new Padding(12);
            // 只在用户拖完窗口边缘时记一次，拖动过程中不反复写设置文件。
            ResizeEnd += delegate { if (!applyingSettings) Raise(PanelSizeChanged); };

            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.ColumnCount = 1;
            layout.RowCount = 7;
            layout.Margin = Padding.Empty;
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
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
            separatorRow.ColumnCount = 3;
            separatorRow.RowCount = 1;
            separatorRow.Margin = Padding.Empty;
            separatorRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72F));
            // 分隔符输入框给固定宽度，右边的空列吃掉剩余空间，避免下拉框被拉伸成一条长条。
            separatorRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150F));
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
            // 宽/高两个数字框各占固定宽度，中间留空隙，最后用空列把整行收在左边。
            dimensionsRow.ColumnCount = 6;
            dimensionsRow.RowCount = 1;
            dimensionsRow.Margin = Padding.Empty;
            dimensionsRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72F));
            dimensionsRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96F));
            dimensionsRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 16F));
            dimensionsRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            dimensionsRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96F));
            dimensionsRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            widthInput = MakeDimensionInput("椭圆宽度，毫米", 2);
            heightInput = MakeDimensionInput("椭圆高度，毫米", 3);
            widthInput.Margin = new Padding(0, 3, 0, 0);
            heightInput.Margin = new Padding(0, 3, 0, 0);
            dimensionsRow.Controls.Add(MakeLabel("宽 mm", "椭圆宽度"), 0, 0);
            dimensionsRow.Controls.Add(widthInput, 1, 0);
            dimensionsRow.Controls.Add(MakeLabel("高 mm", "椭圆高度"), 3, 0);
            dimensionsRow.Controls.Add(heightInput, 4, 0);
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
            hotkeyRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 76F));
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

            TableLayoutPanel historyLayout = new TableLayoutPanel {
                Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Margin = Padding.Empty
            };
            historyLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
            historyLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            historyLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24F));
            TableLayoutPanel searchRow = new TableLayoutPanel {
                Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty
            };
            searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110F));
            searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            historySearch = new TextBox {
                Name = "HistorySearch", AccessibleName = "搜索主题历史",
                Dock = DockStyle.Top, Margin = new Padding(0, 4, 0, 0), TabIndex = 10
            };
            historySearch.TextChanged += delegate { RefreshHistory(); };
            searchRow.Controls.Add(MakeLabel("主题历史", "主题历史"), 0, 0);
            searchRow.Controls.Add(historySearch, 1, 0);
            historyLayout.Controls.Add(searchRow, 0, 0);
            historyGrid = new DataGridView {
                Name = "SubjectHistory", AccessibleName = "主题历史列表",
                Dock = DockStyle.Fill, Margin = Padding.Empty, TabIndex = 11,
                AllowUserToAddRows = false, AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false, RowHeadersVisible = false,
                MultiSelect = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                BackgroundColor = SystemColors.Window, BorderStyle = BorderStyle.FixedSingle,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                ColumnHeadersHeight = 30
            };
            historyGrid.RowTemplate.Height = 30;
            historyGrid.Columns.Add(new DataGridViewCheckBoxColumn {
                Name = "Pinned", HeaderText = "固定", Width = 64,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });
            historyGrid.Columns.Add(new DataGridViewTextBoxColumn {
                Name = "HistorySubject", HeaderText = "主题", ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });
            historyGrid.CellClick += delegate(object sender, DataGridViewCellEventArgs e) {
                if (!updatingHistory && e.RowIndex >= 0 && e.ColumnIndex == 1)
                    UseHistorySubject((string)historyGrid.Rows[e.RowIndex].Tag);
            };
            historyGrid.KeyDown += delegate(object sender, KeyEventArgs e) {
                if (e.KeyCode == Keys.Enter && historyGrid.CurrentRow != null)
                {
                    e.Handled = e.SuppressKeyPress = true;
                    UseHistorySubject((string)historyGrid.CurrentRow.Tag);
                }
            };
            historyGrid.CurrentCellDirtyStateChanged += delegate {
                if (historyGrid.IsCurrentCellDirty)
                    historyGrid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };
            historyGrid.CellValueChanged += HistoryPinChanged;
            historyLayout.Controls.Add(historyGrid, 0, 1);
            historyStatus = MakeLabel(String.Empty, "历史主题数量");
            historyLayout.Controls.Add(historyStatus, 0, 2);
            layout.Controls.Add(historyLayout, 0, 6);

            widthInput.ValueChanged += DimensionValueChanged;
            heightInput.ValueChanged += DimensionValueChanged;
            Deactivate += delegate { CancelHotkeyCapture(); };

            // —— 排版收敛 ——
            // 行高按字体与控件首选高度算：150% 缩放下不会把字挤在 44 像素的固定行里，
            // 100% 下也不会留出大片空白；标签列统一到最宽的那个标签，四行的输入框左边缘对齐。
            int labelWidth = Math.Max(
                TextRenderer.MeasureText("分隔符", Font).Width,
                Math.Max(TextRenderer.MeasureText("主题历史", Font).Width,
                    TextRenderer.MeasureText("宽 mm", Font).Width)) + 12;
            int textRow = PreferredHeight(subjectBox, 24) + 16;
            int comboTall = PreferredHeight(separatorInput, 24) + 16;
            int sizeTall = PreferredHeight(widthInput, 24) + 16;
            int commandTall = PreferredHeight(pickButton, 28) + 14;
            int hotkeyTall = PreferredHeight(hotkeyButton, 28) + 14;
            int statusTall = Font.Height + 14;

            layout.RowStyles[0] = new RowStyle(SizeType.Absolute, textRow);
            layout.RowStyles[1] = new RowStyle(SizeType.Absolute, comboTall);
            layout.RowStyles[2] = new RowStyle(SizeType.Absolute, sizeTall);
            layout.RowStyles[3] = new RowStyle(SizeType.Absolute, commandTall);
            layout.RowStyles[4] = new RowStyle(SizeType.Absolute, hotkeyTall);
            layout.RowStyles[5] = new RowStyle(SizeType.Absolute, statusTall);

            subjectRow.ColumnStyles[0] = new ColumnStyle(SizeType.Absolute, labelWidth);
            separatorRow.ColumnStyles[0] = new ColumnStyle(SizeType.Absolute, labelWidth);
            dimensionsRow.ColumnStyles[0] = new ColumnStyle(SizeType.Absolute, labelWidth);
            hotkeyRow.ColumnStyles[0] = new ColumnStyle(SizeType.Absolute, labelWidth);
            searchRow.ColumnStyles[0] = new ColumnStyle(SizeType.Absolute, labelWidth);

            subjectBox.Margin = TopCentered(subjectBox, textRow);
            separatorInput.Margin = TopCentered(separatorInput, comboTall);
            widthInput.Margin = TopCentered(widthInput, sizeTall);
            heightInput.Margin = TopCentered(heightInput, sizeTall);
            historySearch.Margin = TopCentered(historySearch, PreferredHeight(historySearch, 24) + 16);
            subjectBox.Dock = DockStyle.Fill;
            separatorInput.Dock = DockStyle.Fill;
            widthInput.Dock = DockStyle.Fill;
            heightInput.Dock = DockStyle.Fill;
            historySearch.Dock = DockStyle.Fill;

            pickButton.Margin = new Padding(0, 5, 8, 5);
            ocrButton.Margin = new Padding(0, 5, 8, 5);
            resumeButton.Margin = new Padding(0, 5, 8, 5);
            pauseButton.Margin = new Padding(0, 5, 0, 5);
            hotkeyButton.Margin = new Padding(0, 5, 8, 5);
            clearHotkeyButton.Margin = new Padding(0, 5, 0, 5);
            statusLabel.Margin = new Padding(0, 2, 0, 0);

            historyLayout.RowStyles[0] =
                new RowStyle(SizeType.Absolute, PreferredHeight(historySearch, 24) + 16);
            historyLayout.RowStyles[2] = new RowStyle(SizeType.Absolute, Font.Height + 10);
            historyGrid.RowTemplate.Height = Font.Height + 14;
            historyGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.EnableResizing;
            historyGrid.ColumnHeadersHeight = Font.Height + 16;

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
            RememberCurrentSubject();
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

        public string OcrLanguage
        {
            get { return ocrLanguage; }
            set
            {
                string normalized = OcrService.NormalizeLanguagePreference(value);
                if (ocrLanguage == normalized) return;
                ocrLanguage = normalized;
                if (!applyingSettings) Raise(OcrLanguageChanged);
            }
        }

        public void RememberCurrentSubject()
        {
            if (!history.Remember(Subject)) return;
            RefreshHistory();
            if (!applyingSettings) Raise(HistoryChanged);
        }

        public void UseHistorySubject(string value)
        {
            if (String.IsNullOrWhiteSpace(value)) return;
            Subject = value;
            Raise(ResumeRequested);
        }

        private void HistoryPinChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (updatingHistory || e.RowIndex < 0 || e.ColumnIndex != 0) return;
            DataGridViewRow row = historyGrid.Rows[e.RowIndex];
            if (history.SetPinned((string)row.Tag, Convert.ToBoolean(row.Cells[0].Value)))
                Raise(HistoryChanged);
            else SetStatus("最多固定 " + SubjectHistory.PinnedLimit + " 个主题");
            // Reordering during a grid edit would re-enter its current-cell handler.
            if (IsHandleCreated)
                BeginInvoke((Action)delegate { if (!IsDisposed) RefreshHistory(); });
            else RefreshHistory();
        }

        private void RefreshHistory()
        {
            if (historyGrid == null || historyStatus == null) return;
            string selected = historyGrid.CurrentRow == null ? null : (string)historyGrid.CurrentRow.Tag;
            updatingHistory = true;
            try
            {
                historyGrid.Rows.Clear();
                foreach (SubjectHistoryEntry entry in history.Search(historySearch.Text))
                {
                    int index = historyGrid.Rows.Add(entry.Pinned, entry.Subject);
                    DataGridViewRow row = historyGrid.Rows[index];
                    row.Tag = entry.Subject;
                    row.Cells[1].ToolTipText = entry.Subject;
                    if (entry.Subject == selected) historyGrid.CurrentCell = row.Cells[1];
                }
                historyStatus.Text = historyGrid.Rows.Count == 0
                    ? (String.IsNullOrWhiteSpace(historySearch.Text) ? "暂无历史主题" : "无匹配主题")
                    : historyGrid.Rows.Count + " 个主题";
            }
            finally { updatingHistory = false; }
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

        /// <summary>焦点是否落在主题、搜索、分隔符或尺寸输入框里。</summary>
        public bool IsTextInputFocused()
        {
            if (!ContainsFocus)
                return false;
            Control focused = FindFocusedControl(this);
            return focused is TextBoxBase || focused is NumericUpDown || focused is ComboBox ||
                focused is DataGridView;
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
                OcrLanguage = settings.OcrLanguage;
                history = new SubjectHistory(settings.SubjectHistory);
                RefreshHistory();
                ClientSize = new Size(
                    PluginSettings.ClampPanelWidth(settings.PanelWidth, DefaultPanelWidth),
                    PluginSettings.ClampPanelHeight(settings.PanelHeight, DefaultPanelHeight));
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
                Separator = Separator,
                OcrLanguage = OcrLanguage,
                SubjectHistory = history.Export(),
                PanelWidth = ClientSize.Width,
                PanelHeight = ClientSize.Height
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

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            // MinimumSize 用的是窗口尺寸，客户区必须留出 MinimumPanelWidth × MinimumPanelHeight：
            // 否则拖到最小时主题历史表格只剩一条缝。
            Size border = new Size(Width - ClientSize.Width, Height - ClientSize.Height);
            if (border.Width <= 0 || border.Height <= 0)
                border = new Size(16, 39);
            MinimumSize = new Size(MinimumPanelWidth + border.Width, MinimumPanelHeight + border.Height);
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

        /// <summary>控件首选高度；布局信息还不完整时回落到给定值。</summary>
        private static int PreferredHeight(Control control, int fallback)
        {
            int height = control.PreferredSize.Height;
            if (height <= 0) height = control.Height;
            return height > 0 ? height : fallback;
        }

        /// <summary>在给定行高里把控件垂直居中：Dock=Fill 加上下相等的边距，净高正好等于首选高度。</summary>
        private static Padding TopCentered(Control control, int rowHeight)
        {
            int preferred = PreferredHeight(control, control.Height);
            int inset = Math.Max(0, (rowHeight - preferred) / 2);
            return new Padding(0, inset, 0, inset);
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
            OcrLanguage = String.Empty;
            SubjectHistory = new List<SubjectHistoryEntry>();
            PanelWidth = StampPanel.DefaultPanelWidth;
            PanelHeight = StampPanel.DefaultPanelHeight;
        }

        public double WidthMm { get; set; }
        public double HeightMm { get; set; }

        /// <summary>"Ctrl+Alt+P" 形式的快捷键文本；"None" 表示不注册。</summary>
        public string PickHotkey { get; set; }
        public string Separator { get; set; }
        public string OcrLanguage { get; set; }
        public List<SubjectHistoryEntry> SubjectHistory { get; set; }

        /// <summary>面板客户区宽度；重启后按它恢复窗口大小。</summary>
        public int PanelWidth { get; set; }

        /// <summary>面板客户区高度；重启后按它恢复窗口大小。</summary>
        public int PanelHeight { get; set; }

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
                    settings.OcrLanguage = OcrService.NormalizeLanguagePreference(settings.OcrLanguage);
                    settings.SubjectHistory = new SubjectHistory(settings.SubjectHistory).Export();
                    settings.PanelWidth = ClampPanelWidth(settings.PanelWidth, StampPanel.DefaultPanelWidth);
                    settings.PanelHeight = ClampPanelHeight(settings.PanelHeight, StampPanel.DefaultPanelHeight);
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
                    Separator = Separator ?? "/",
                    OcrLanguage = OcrService.NormalizeLanguagePreference(OcrLanguage),
                    SubjectHistory = new SubjectHistory(SubjectHistory).Export(),
                    PanelWidth = ClampPanelWidth(PanelWidth, StampPanel.DefaultPanelWidth),
                    PanelHeight = ClampPanelHeight(PanelHeight, StampPanel.DefaultPanelHeight)
                };
                XmlWriterSettings writerSettings = new XmlWriterSettings {
                    Indent = true,
                    CloseOutput = true
                };
                string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    using (XmlWriter writer = XmlWriter.Create(temporary, writerSettings))
                        new XmlSerializer(typeof(PluginSettings)).Serialize(writer, sanitized);
                    if (File.Exists(path)) File.Replace(temporary, path, null);
                    else File.Move(temporary, path);
                }
                finally
                {
                    if (File.Exists(temporary)) File.Delete(temporary);
                }
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

        /// <summary>窗口宽度：非正数回落到默认值，然后夹在最小与 4000 之间。</summary>
        internal static int ClampPanelWidth(int value, int fallback)
        {
            if (value <= 0) value = fallback;
            return Math.Min(4000, Math.Max(StampPanel.MinimumPanelWidth, value));
        }

        /// <summary>窗口高度：非正数回落到默认值，然后夹在最小与 4000 之间。</summary>
        internal static int ClampPanelHeight(int value, int fallback)
        {
            if (value <= 0) value = fallback;
            return Math.Min(4000, Math.Max(StampPanel.MinimumPanelHeight, value));
        }
    }
}
