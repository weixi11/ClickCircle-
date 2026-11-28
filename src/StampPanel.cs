using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Serialization;

namespace ClickCircle
{
    public sealed class StampPanel : Form
    {
        private TextBox subjectBox;
        private ComboBox separatorInput;
        private NumericUpDown widthInput;
        private NumericUpDown heightInput;
        private Button pickButton;
        private Button ocrButton;
        private Button resumeButton;
        private Button pauseButton;
        private Button hotkeyButton;
        private Button clearHotkeyButton;
        private Button updateButton;
        private Label statusLabel;
        private TextBox historySearch;
        private DataGridView historyGrid;
        private Label historyStatus;
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
        private Panel mainPage;
        private Panel settingsPage;
        private LinkLabel backLink;
        private Button settingsButton;
        private Label hotkeySummary;
        private Label summaryLabel;
        private Label statusDot;
        private Label versionLabel;
        private Label settingsHint;
        private Label styleStatusLabel;
        private Button styleEditorButton;
        private Label updateStatusLabel;
        private CheckBox ocrReviewCheckBox;
        private CheckBox historyCheckBox;
        private TableLayoutPanel mainLayout;
        private TableLayoutPanel historyLayout;
        private bool showHistory = true;
        private int historyHiddenHeight = -1;
        private PickHintForm pickHint;
        private string versionText = String.Empty;

        private static readonly Color Accent = Color.FromArgb(47, 107, 255);
        private static readonly Color PageBack = Color.FromArgb(245, 247, 250);
        private static readonly Color TextColor = Color.FromArgb(43, 47, 54);
        private static readonly Color Muted = Color.FromArgb(138, 147, 163);
        private static readonly Color BorderColor = Color.FromArgb(221, 227, 236);
        private static readonly Color PausedDot = Color.FromArgb(245, 166, 35);

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
        public event Action OcrReviewChanged;
        public event Action HistoryVisibilityChanged;
        public event Action CheckUpdateRequested;
        public event Action StyleEditRequested;

        /// <summary>用户拖动窗口边缘改尺寸后触发，用于记住面板大小。</summary>
        public event Action PanelSizeChanged;

        /// <summary>默认窗口客户区尺寸。</summary>
        public const int DefaultPanelWidth = 480;
        public const int DefaultPanelHeight = 566;

        /// <summary>最小客户区尺寸：标题、取字行、分隔符、尺寸、按钮、快捷键、状态行与历史表格都要露出来。</summary>
        public const int MinimumPanelWidth = 440;
        public const int MinimumPanelHeight = 380;

        /// <summary>关掉主题历史后的紧凑客户区高度（只留主题、按钮、状态行）。</summary>
        public const int CompactPanelHeight = 190;

        public StampPanel() : this(new PluginSettings())
        {
        }

        public StampPanel(PluginSettings settings)
        {
            Text = "取字连续圈注";
            AutoScaleMode = AutoScaleMode.None;
            ClientSize = new Size(DefaultPanelWidth, DefaultPanelHeight);
            FormBorderStyle = FormBorderStyle.SizableToolWindow;
            MinimumSize = new Size(MinimumPanelWidth + 32, MinimumPanelHeight + 48);
            MaximumSize = new Size(4000, 4000);
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            TopMost = false;
            KeyPreview = true;
            Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Regular);
            BackColor = PageBack;
            Padding = new Padding(14);
            ResizeEnd += delegate { if (!applyingSettings) Raise(PanelSizeChanged); };

            BuildMainPage();
            BuildSettingsPage();
            Deactivate += delegate { CancelHotkeyCapture(); };

            ApplySettings(settings ?? new PluginSettings());
            UpdateHotkeyButton();
            UpdateSummary();
            SetMode("paused", "待取字");
        }

        private void BuildMainPage()
        {
            mainPage = new Panel { Dock = DockStyle.Fill, BackColor = PageBack };
            Controls.Add(mainPage);

            TableLayoutPanel layout = new TableLayoutPanel {
                Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Margin = Padding.Empty,
                BackColor = PageBack
            };
            mainLayout = layout;
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            mainPage.Controls.Add(layout);

            TableLayoutPanel subjectRow = new TableLayoutPanel {
                Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty,
                BackColor = PageBack
            };
            subjectRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 52F));
            subjectRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            Label subjectLabel = MakeLabel("主题", "标注主题");
            subjectLabel.Font = new Font(Font.FontFamily, 10F, FontStyle.Bold);
            subjectLabel.ForeColor = TextColor;
            subjectBox = new TextBox {
                Name = "Subject", AccessibleName = "标注主题", Multiline = false, WordWrap = false,
                BorderStyle = BorderStyle.FixedSingle, BackColor = Color.White, ForeColor = TextColor,
                Dock = DockStyle.Fill, Margin = new Padding(0, 12, 0, 12), TabIndex = 0
            };
            subjectBox.TextChanged += delegate {
                if (!composingSubject)
                    pickedFragments = null;
                UpdateButtonState();
            };
            subjectBox.HandleCreated += delegate { SetCueBanner(subjectBox, "请输入或选择主题"); };
            subjectRow.Controls.Add(subjectLabel, 0, 0);
            subjectRow.Controls.Add(subjectBox, 1, 0);
            layout.Controls.Add(subjectRow, 0, 0);

            TableLayoutPanel commandsRow = new TableLayoutPanel {
                Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1, Margin = Padding.Empty,
                BackColor = PageBack
            };
            for (int column = 0; column < 4; column++)
                commandsRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            pickButton = MakePrimaryButton("取文字", 4);
            ocrButton = MakeSecondaryButton("区域识别", 5);
            ocrButton.Name = "OcrPick";
            resumeButton = MakeSecondaryButton("继续标注", 6);
            pauseButton = MakeSecondaryButton("暂停", 7);
            pickButton.Margin = new Padding(0, 6, 6, 6);
            ocrButton.Margin = new Padding(3, 6, 6, 6);
            resumeButton.Margin = new Padding(3, 6, 6, 6);
            pauseButton.Margin = new Padding(3, 6, 0, 6);
            pickButton.Click += delegate { Raise(PickRequested); };
            ocrButton.Click += delegate { Raise(OcrRequested); };
            resumeButton.Click += delegate { Raise(ResumeRequested); };
            pauseButton.Click += delegate { Raise(PauseRequested); };
            commandsRow.Controls.Add(pickButton, 0, 0);
            commandsRow.Controls.Add(ocrButton, 1, 0);
            commandsRow.Controls.Add(resumeButton, 2, 0);
            commandsRow.Controls.Add(pauseButton, 3, 0);
            layout.Controls.Add(commandsRow, 0, 1);

            Panel statusPanel = new Panel {
                Dock = DockStyle.Fill, BackColor = Color.FromArgb(238, 242, 248),
                Margin = new Padding(0, 4, 0, 4)
            };
            TableLayoutPanel statusRow = new TableLayoutPanel {
                Dock = DockStyle.Fill, ColumnCount = 6, RowCount = 1, Margin = Padding.Empty,
                BackColor = Color.Transparent
            };
            statusRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 16F));
            statusRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            statusRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 42F));
            statusRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 56F));
            statusRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96F));
            statusRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 56F));
            statusDot = new Label {
                Text = "●", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = PausedDot, Font = new Font(Font.FontFamily, 9F)
            };
            statusLabel = new Label {
                Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = TextColor, AutoEllipsis = true, AutoSize = false,
                AccessibleName = "圈注状态"
            };
            Label hotkeyLabel = MakeLabel("快捷键", "取文字快捷键");
            hotkeyLabel.ForeColor = Muted;
            hotkeyLabel.Font = new Font(Font.FontFamily, 8.5F);
            hotkeySummary = new Label {
                Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.White, ForeColor = TextColor, BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(0, 7, 6, 7), AutoEllipsis = true,
                Font = new Font(Font.FontFamily, 8.5F), AccessibleName = "当前快捷键"
            };
            summaryLabel = new Label {
                Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Muted, AutoEllipsis = true, Font = new Font(Font.FontFamily, 8.5F)
            };
            settingsButton = MakeSecondaryButton("设置", 12);
            settingsButton.Margin = new Padding(4, 7, 0, 7);
            settingsButton.Font = new Font(Font.FontFamily, 9F);
            settingsButton.Click += delegate { ShowSettings(true); };
            statusRow.Controls.Add(statusDot, 0, 0);
            statusRow.Controls.Add(statusLabel, 1, 0);
            statusRow.Controls.Add(hotkeyLabel, 2, 0);
            statusRow.Controls.Add(hotkeySummary, 3, 0);
            statusRow.Controls.Add(summaryLabel, 4, 0);
            statusRow.Controls.Add(settingsButton, 5, 0);
            statusPanel.Controls.Add(statusRow);
            layout.Controls.Add(statusPanel, 0, 2);

            historyLayout = new TableLayoutPanel {
                Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Margin = Padding.Empty,
                BackColor = PageBack
            };
            historyLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            historyLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
            historyLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            TableLayoutPanel historyHeader = new TableLayoutPanel {
                Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty,
                BackColor = PageBack
            };
            historyHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            historyHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96F));
            Label historyTitle = MakeLabel("主题历史", "主题历史");
            historyTitle.Font = new Font(Font.FontFamily, 11F, FontStyle.Bold);
            historyTitle.ForeColor = TextColor;
            historyStatus = new Label {
                Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight,
                ForeColor = Muted, AutoEllipsis = true, Font = new Font(Font.FontFamily, 9F),
                AccessibleName = "历史主题数量"
            };
            historyHeader.Controls.Add(historyTitle, 0, 0);
            historyHeader.Controls.Add(historyStatus, 1, 0);
            historyLayout.Controls.Add(historyHeader, 0, 0);

            historySearch = new TextBox {
                Name = "HistorySearch", AccessibleName = "搜索主题历史",
                BorderStyle = BorderStyle.FixedSingle, BackColor = Color.White, ForeColor = TextColor,
                Dock = DockStyle.Fill, Margin = new Padding(0, 4, 0, 4), TabIndex = 10
            };
            historySearch.TextChanged += delegate { RefreshHistory(); };
            historySearch.HandleCreated += delegate { SetCueBanner(historySearch, "搜索历史主题…"); };
            historyLayout.Controls.Add(historySearch, 0, 1);

            historyGrid = new DataGridView {
                Name = "SubjectHistory", AccessibleName = "主题历史列表",
                Dock = DockStyle.Fill, Margin = Padding.Empty, TabIndex = 11,
                AllowUserToAddRows = false, AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false, RowHeadersVisible = false,
                MultiSelect = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                BackgroundColor = Color.White, BorderStyle = BorderStyle.FixedSingle,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                ColumnHeadersHeight = 30, GridColor = BorderColor, EnableHeadersVisualStyles = false
            };
            historyGrid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(242, 245, 250);
            historyGrid.ColumnHeadersDefaultCellStyle.ForeColor = Muted;
            historyGrid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(242, 245, 250);
            historyGrid.DefaultCellStyle.BackColor = Color.White;
            historyGrid.DefaultCellStyle.ForeColor = TextColor;
            historyGrid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(232, 240, 255);
            historyGrid.DefaultCellStyle.SelectionForeColor = TextColor;
            historyGrid.RowTemplate.Height = Font.Height + 14;
            historyGrid.ColumnHeadersHeight = Font.Height + 16;
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
            historyLayout.Controls.Add(historyGrid, 0, 2);
            layout.Controls.Add(historyLayout, 0, 3);
        }

        private void BuildSettingsPage()
        {
            settingsPage = new Panel {
                Dock = DockStyle.Fill, BackColor = PageBack, Visible = false, AutoScroll = true
            };
            Controls.Add(settingsPage);

            TableLayoutPanel layout = new TableLayoutPanel {
                Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 14, Margin = Padding.Empty,
                BackColor = PageBack
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 14F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 14F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24F));
            settingsPage.Controls.Add(layout);

            backLink = new LinkLabel {
                Text = "‹  返回主页面", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft,
                LinkColor = Accent, ActiveLinkColor = Accent,
                Font = new Font(Font.FontFamily, 9.5F), Margin = Padding.Empty, TabIndex = 30
            };
            backLink.LinkClicked += delegate { ShowSettings(false); };
            layout.Controls.Add(backLink, 0, 0);

            Label parameterTitle = MakeLabel("标注参数", "标注参数");
            parameterTitle.Font = new Font(Font.FontFamily, 11F, FontStyle.Bold);
            parameterTitle.ForeColor = TextColor;
            layout.Controls.Add(parameterTitle, 0, 1);

            TableLayoutPanel separatorRow = new TableLayoutPanel {
                Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Margin = Padding.Empty,
                BackColor = PageBack
            };
            separatorRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 64F));
            separatorRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200F));
            separatorRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            Label separatorLabel = MakeLabel("分隔符", "文字分隔符");
            separatorLabel.ForeColor = TextColor;
            separatorInput = new ComboBox {
                Name = "Separator", AccessibleName = "文字分隔符", Dock = DockStyle.Fill,
                Margin = new Padding(0, 10, 0, 10), DropDownStyle = ComboBoxStyle.DropDown,
                AutoCompleteMode = AutoCompleteMode.None, TabIndex = 1
            };
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
            separatorRow.Controls.Add(separatorLabel, 0, 0);
            separatorRow.Controls.Add(separatorInput, 1, 0);
            layout.Controls.Add(separatorRow, 0, 2);

            TableLayoutPanel sizeRow = new TableLayoutPanel {
                Dock = DockStyle.Fill, ColumnCount = 9, RowCount = 1, Margin = Padding.Empty,
                BackColor = PageBack
            };
            sizeRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 64F));
            sizeRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 20F));
            sizeRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 86F));
            sizeRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 32F));
            sizeRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 10F));
            sizeRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 20F));
            sizeRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 86F));
            sizeRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 32F));
            sizeRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            Label sizeLabel = MakeLabel("圈注尺寸", "圈注尺寸");
            sizeLabel.ForeColor = TextColor;
            Label widthLabel = MakeLabel("宽", "椭圆宽度");
            widthLabel.ForeColor = Muted;
            Label firstUnit = MakeLabel("mm", "宽度单位");
            firstUnit.ForeColor = Muted;
            firstUnit.Font = new Font(Font.FontFamily, 8.5F);
            firstUnit.Margin = new Padding(3, 0, 0, 0);
            Label heightLabel = MakeLabel("高", "椭圆高度");
            heightLabel.ForeColor = Muted;
            Label secondUnit = MakeLabel("mm", "高度单位");
            secondUnit.ForeColor = Muted;
            secondUnit.Font = new Font(Font.FontFamily, 8.5F);
            secondUnit.Margin = new Padding(3, 0, 0, 0);
            widthInput = MakeDimensionInput("椭圆宽度，毫米", 2);
            heightInput = MakeDimensionInput("椭圆高度，毫米", 3);
            widthInput.Dock = DockStyle.Fill;
            heightInput.Dock = DockStyle.Fill;
            widthInput.Margin = new Padding(0, 10, 0, 10);
            heightInput.Margin = new Padding(0, 10, 0, 10);
            widthInput.ValueChanged += DimensionValueChanged;
            heightInput.ValueChanged += DimensionValueChanged;
            sizeRow.Controls.Add(sizeLabel, 0, 0);
            sizeRow.Controls.Add(widthLabel, 1, 0);
            sizeRow.Controls.Add(widthInput, 2, 0);
            sizeRow.Controls.Add(firstUnit, 3, 0);
            sizeRow.Controls.Add(heightLabel, 5, 0);
            sizeRow.Controls.Add(heightInput, 6, 0);
            sizeRow.Controls.Add(secondUnit, 7, 0);
            layout.Controls.Add(sizeRow, 0, 3);

            Panel divider = new Panel {
                Dock = DockStyle.Fill, BackColor = BorderColor, Margin = new Padding(0, 7, 0, 7)
            };
            layout.Controls.Add(divider, 0, 4);

            Label hotkeyTitle = MakeLabel("快捷键", "快捷键");
            hotkeyTitle.Font = new Font(Font.FontFamily, 11F, FontStyle.Bold);
            hotkeyTitle.ForeColor = TextColor;
            layout.Controls.Add(hotkeyTitle, 0, 5);

            TableLayoutPanel hotkeyRow = new TableLayoutPanel {
                Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Margin = Padding.Empty,
                BackColor = PageBack
            };
            hotkeyRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 78F));
            hotkeyRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            hotkeyRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 64F));
            Label hotkeyLabel = MakeLabel("操作快捷键", "取文字快捷键");
            hotkeyLabel.ForeColor = TextColor;
            hotkeyButton = MakeSecondaryButton(String.Empty, 8);
            hotkeyButton.Margin = new Padding(0, 8, 8, 8);
            hotkeyButton.Click += delegate { BeginHotkeyCapture(); };
            clearHotkeyButton = MakeSecondaryButton("清除", 9);
            clearHotkeyButton.Margin = new Padding(0, 8, 0, 8);
            clearHotkeyButton.Click += delegate { SetHotkey(HotkeyBinding.Empty); };
            hotkeyRow.Controls.Add(hotkeyLabel, 0, 0);
            hotkeyRow.Controls.Add(hotkeyButton, 1, 0);
            hotkeyRow.Controls.Add(clearHotkeyButton, 2, 0);
            layout.Controls.Add(hotkeyRow, 0, 6);

            TableLayoutPanel styleRow = new TableLayoutPanel {
                Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Margin = Padding.Empty,
                BackColor = PageBack
            };
            styleRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72F));
            styleRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            styleRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 116F));
            styleEditorButton = MakeSecondaryButton("编辑样式…", 10);
            styleEditorButton.Margin = new Padding(0, 4, 8, 8);
            styleEditorButton.Click += delegate { Raise(StyleEditRequested); };
            styleStatusLabel = new Label {
                Text = "未启用", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Muted, Font = new Font(Font.FontFamily, 9F), AutoEllipsis = true
            };
            Label styleLabel = MakeLabel("标注样式", "新标注自动样式");
            styleLabel.ForeColor = TextColor;
            styleRow.Controls.Add(styleLabel, 0, 0);
            styleRow.Controls.Add(styleStatusLabel, 1, 0);
            styleRow.Controls.Add(styleEditorButton, 2, 0);
            layout.Controls.Add(styleRow, 0, 7);

            TableLayoutPanel ocrRow = new TableLayoutPanel {
                Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty,
                BackColor = PageBack
            };
            ocrRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72F));
            ocrRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            Label ocrLabel = MakeLabel("区域识别", "区域识别");
            ocrLabel.ForeColor = TextColor;
            ocrReviewCheckBox = new CheckBox {
                Name = "ShowOcrReview", AccessibleName = "区域识别后显示校对窗",
                Text = "识别后显示校对窗（关闭则直接采用结果）", Dock = DockStyle.Fill,
                AutoSize = false, Checked = true, TabIndex = 11, ForeColor = TextColor,
                TextAlign = ContentAlignment.MiddleLeft
            };
            ocrReviewCheckBox.CheckedChanged += delegate {
                if (!applyingSettings) Raise(OcrReviewChanged);
            };
            ocrRow.Controls.Add(ocrLabel, 0, 0);
            ocrRow.Controls.Add(ocrReviewCheckBox, 1, 0);
            layout.Controls.Add(ocrRow, 0, 8);

            TableLayoutPanel historyRow = new TableLayoutPanel {
                Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty,
                BackColor = PageBack
            };
            historyRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72F));
            historyRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            Label historyLabel = MakeLabel("主题历史", "主题历史显示");
            historyLabel.ForeColor = TextColor;
            historyCheckBox = new CheckBox {
                Name = "ShowHistory", AccessibleName = "显示主题历史",
                Text = "显示主题历史（关闭后不记录、不显示）", Dock = DockStyle.Fill,
                AutoSize = false, Checked = true, TabIndex = 12, ForeColor = TextColor,
                TextAlign = ContentAlignment.MiddleLeft
            };
            historyCheckBox.CheckedChanged += delegate {
                if (applyingSettings) return;
                ApplyHistoryVisibility(historyCheckBox.Checked, true);
                Raise(HistoryVisibilityChanged);
            };
            historyRow.Controls.Add(historyLabel, 0, 0);
            historyRow.Controls.Add(historyCheckBox, 1, 0);
            layout.Controls.Add(historyRow, 0, 9);

            TableLayoutPanel infoPanel = new TableLayoutPanel {
                Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Margin = Padding.Empty,
                BackColor = PageBack
            };
            infoPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            infoPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            infoPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            settingsHint = new Label {
                Text = "取字与主题历史在主页操作；分隔符、圈注尺寸、快捷键、标注样式、主题历史与区域识别校对窗修改后立即生效。",
                AutoSize = true, ForeColor = Muted, Font = new Font(Font.FontFamily, 8.5F),
                Margin = new Padding(0, 8, 0, 0), MaximumSize = new Size(400, 0)
            };
            updateStatusLabel = new Label {
                Text = String.Empty, AutoSize = true, ForeColor = TextColor,
                Font = new Font(Font.FontFamily, 9F), Margin = new Padding(0, 10, 0, 0),
                MaximumSize = new Size(400, 0)
            };
            infoPanel.Controls.Add(settingsHint, 0, 0);
            infoPanel.Controls.Add(updateStatusLabel, 0, 1);
            layout.Controls.Add(infoPanel, 0, 10);

            TableLayoutPanel versionRow = new TableLayoutPanel {
                Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty,
                BackColor = PageBack
            };
            versionRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            versionRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96F));
            versionLabel = new Label {
                Text = "软件版本", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Muted, Font = new Font(Font.FontFamily, 9F), AutoEllipsis = true
            };
            updateButton = MakeSecondaryButton("检查更新", 20);
            updateButton.Margin = new Padding(0, 8, 0, 8);
            updateButton.Click += delegate { Raise(CheckUpdateRequested); };
            versionRow.Controls.Add(versionLabel, 0, 0);
            versionRow.Controls.Add(updateButton, 1, 0);
            layout.Controls.Add(new Panel {
                Dock = DockStyle.Fill, BackColor = BorderColor, Margin = new Padding(0, 7, 0, 7)
            }, 0, 11);
            layout.Controls.Add(versionRow, 0, 12);

            Label footer = new Label {
                Text = "设置页仅调整标注参数", Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft, ForeColor = Muted,
                Font = new Font(Font.FontFamily, 8.5F)
            };
            layout.Controls.Add(footer, 0, 13);
        }

        private void ShowSettings(bool show)
        {
            if (settingsPage == null || mainPage == null)
                return;
            Size border = BorderSize();
            if (show && !showHistory && ClientSize.Height <= CompactPanelHeight + 40)
            {
                // 打开设置页时把紧凑面板临时撑回设置页需要的高度，避免设置项被截断。
                MinimumSize = new Size(MinimumPanelWidth + border.Width,
                    MinimumPanelHeight + border.Height);
                int target = historyHiddenHeight > CompactPanelHeight + 40
                    ? historyHiddenHeight
                    : DefaultPanelHeight;
                ClientSize = new Size(ClientSize.Width, Math.Max(MinimumPanelHeight, target));
            }
            else if (!show && !showHistory && ClientSize.Height > CompactPanelHeight + 40)
            {
                // 返回主页面且历史关着：重新收成紧凑高度。
                if (ClientSize.Height > CompactPanelHeight + 40)
                    historyHiddenHeight = ClientSize.Height;
                MinimumSize = new Size(MinimumPanelWidth + border.Width,
                    CompactPanelHeight + border.Height);
                ClientSize = new Size(ClientSize.Width, CompactPanelHeight);
            }
            settingsPage.Visible = show;
            mainPage.Visible = !show;
            UpdateTitle();
        }

        private void UpdateTitle()
        {
            if (versionText.Length == 0)
                return;
            string title = "取字连续圈注 v" + versionText;
            if (settingsPage != null && settingsPage.Visible)
                title += " / 设置";
            Text = title;
        }

        private void UpdateSummary()
        {
            if (summaryLabel == null)
                return;
            string separatorText = String.IsNullOrEmpty(separator) ? "无分隔符" : separator;
            summaryLabel.Text = String.Format("{0:0.##}×{1:0.##}mm · {2}",
                WidthMm, HeightMm, separatorText);
        }

        private void UpdateStatusDot()
        {
            if (statusDot == null)
                return;
            if (currentMode == "picking")
                statusDot.ForeColor = Accent;
            else if (currentMode == "stamping")
                statusDot.ForeColor = Color.FromArgb(34, 181, 115);
            else
                statusDot.ForeColor = PausedDot;
        }

        private static void SetCueBanner(TextBox box, string text)
        {
            if (box == null || !box.IsHandleCreated)
                return;
            SendMessage(box.Handle, EmSetCueBanner, (IntPtr)1, text);
        }

        private static Button MakePrimaryButton(string text, int tabIndex)
        {
            Button button = new Button {
                Text = text, AccessibleName = text, Dock = DockStyle.Fill,
                FlatStyle = FlatStyle.Flat, BackColor = Accent, ForeColor = Color.White,
                Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold),
                TabIndex = tabIndex, UseVisualStyleBackColor = false, Cursor = Cursors.Hand
            };
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(37, 92, 232);
            button.FlatAppearance.MouseDownBackColor = Color.FromArgb(28, 78, 205);
            return button;
        }

        private static Button MakeSecondaryButton(string text, int tabIndex)
        {
            Button button = new Button {
                Text = text, AccessibleName = text, Dock = DockStyle.Fill,
                FlatStyle = FlatStyle.Flat, BackColor = Color.White, ForeColor = TextColor,
                Font = new Font("Microsoft YaHei UI", 10F),
                TabIndex = tabIndex, UseVisualStyleBackColor = false, Cursor = Cursors.Hand
            };
            button.FlatAppearance.BorderSize = 1;
            button.FlatAppearance.BorderColor = BorderColor;
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(246, 249, 255);
            button.FlatAppearance.MouseDownBackColor = Color.FromArgb(235, 241, 250);
            button.EnabledChanged += delegate {
                button.BackColor = button.Enabled ? Color.White : Color.FromArgb(237, 240, 245);
                button.ForeColor = button.Enabled ? TextColor : Color.FromArgb(182, 189, 200);
            };
            return button;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, string lParam);

        private const int EmSetCueBanner = 0x1501;

        /// <summary>在标题栏和设置页显示版本号；传空值保持原标题。</summary>
        public void SetVersion(string version)
        {
            if (String.IsNullOrWhiteSpace(version))
                return;
            versionText = version.Trim();
            if (versionLabel != null)
                versionLabel.Text = "软件版本 v" + versionText;
            UpdateTitle();
        }

        /// <summary>更新按钮的文字与可用状态。</summary>
        public void SetUpdateState(string text, bool enabled)
        {
            if (updateButton == null)
                return;
            updateButton.Text = String.IsNullOrWhiteSpace(text) ? "检查更新" : text;
            updateButton.Enabled = enabled;
        }

        /// <summary>设置页底部的更新/提示文字；error 为真时用红色。</summary>
        public void SetUpdateStatus(string text, bool error = false)
        {
            if (updateStatusLabel == null)
                return;
            updateStatusLabel.Text = text ?? String.Empty;
            updateStatusLabel.ForeColor = error ? Color.FromArgb(208, 69, 63) : TextColor;
        }

        /// <summary>取字成功时在鼠标旁显示短暂提示，例如“已取 2 段 · LI-282”。</summary>
        public void ShowPickHint(string fragment, int count)
        {
            ShowHint(PickHintText.ForPick(fragment, count));
        }

        /// <summary>放下圈注时在鼠标旁显示短暂提示，例如“已圈注 3 个 · LI-282”。</summary>
        public void ShowStampHint(string subject, int count)
        {
            ShowHint(PickHintText.ForStamp(subject, count));
        }

        private void ShowHint(string text)
        {
            if (String.IsNullOrEmpty(text))
                return;
            if (pickHint == null || pickHint.IsDisposed)
                pickHint = new PickHintForm();
            pickHint.ShowAt(text, Cursor.Position);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && pickHint != null)
            {
                pickHint.Dispose();
                pickHint = null;
            }
            base.Dispose(disposing);
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
            if (!showHistory) return;
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
            UpdateSummary();
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

        /// <summary>是否显示主题历史（设置页开关；关掉后不显示也不记录）。</summary>
        public bool ShowHistory
        {
            get { return showHistory; }
        }

        /// <summary>显示/隐藏主题历史：隐藏时把面板收成紧凑高度，打开时恢复原高度。</summary>
        private void ApplyHistoryVisibility(bool show, bool restoreHeight, int requestedHeight = -1)
        {
            showHistory = show;
            if (historyCheckBox != null && historyCheckBox.Checked != show)
                historyCheckBox.Checked = show;
            if (historyLayout != null)
                historyLayout.Visible = show;
            if (mainLayout != null && mainLayout.RowStyles.Count > 3)
                mainLayout.RowStyles[3] = show
                    ? new RowStyle(SizeType.Percent, 100F)
                    : new RowStyle(SizeType.Absolute, 0F);
            Size border = BorderSize();
            if (show)
            {
                MinimumSize = new Size(MinimumPanelWidth + border.Width,
                    MinimumPanelHeight + border.Height);
                int target = historyHiddenHeight > 0
                    ? historyHiddenHeight
                    : (requestedHeight > 0 ? requestedHeight : DefaultPanelHeight);
                if (restoreHeight && ClientSize.Height > CompactPanelHeight + 40)
                    target = ClientSize.Height;
                historyHiddenHeight = -1;
                ClientSize = new Size(ClientSize.Width, Math.Max(MinimumPanelHeight, target));
            }
            else
            {
                int height = requestedHeight > 0 ? requestedHeight : ClientSize.Height;
                if (height > CompactPanelHeight + 40)
                    historyHiddenHeight = height;
                // 设置页还开着就先不缩窗口，等返回主页面再收，免得把设置页截断。
                if (settingsPage == null || !settingsPage.Visible ||
                    ClientSize.Height <= CompactPanelHeight + 40)
                {
                    MinimumSize = new Size(MinimumPanelWidth + border.Width,
                        CompactPanelHeight + border.Height);
                    ClientSize = new Size(ClientSize.Width, CompactPanelHeight);
                }
            }
            RefreshHistory();
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
                if (ocrReviewCheckBox != null)
                    ocrReviewCheckBox.Checked = settings.ShowOcrReview;
                AnnotStyle style = (settings.Style ?? AnnotStyle.Default()).Clone();
                style.Normalize();
                Style = style;
                history = new SubjectHistory(settings.SubjectHistory);
                int requestedHeight = PluginSettings.ClampPanelHeight(settings.PanelHeight, DefaultPanelHeight);
                ClientSize = new Size(
                    PluginSettings.ClampPanelWidth(settings.PanelWidth, DefaultPanelWidth),
                    requestedHeight);
                historyHiddenHeight = -1;
                ApplyHistoryVisibility(settings.ShowHistory, false, requestedHeight);
            }
            finally
            {
                applyingSettings = false;
            }
            hotkey = HotkeyBinding.ParseOrDefault(settings.PickHotkey);
            UpdateHotkeyButton();
            UpdateSummary();
            UpdateStyleStatus();
            if (separatorInput != null)
                separatorInput.SelectionLength = 0;
        }

        public PluginSettings GetSettings()
        {
            return new PluginSettings {
                WidthMm = WidthMm,
                HeightMm = HeightMm,
                PickHotkey = hotkey.ToStorage(),
                Separator = Separator,
                OcrLanguage = OcrLanguage,
                ShowOcrReview = ShowOcrReview,
                ShowHistory = showHistory,
                Style = (Style ?? AnnotStyle.Default()).Clone(),
                SubjectHistory = history.Export(),
                PanelWidth = ClientSize.Width,
                PanelHeight = ClientSize.Height
            };
        }

        /// <summary>新标注自动套用的样式；由插件在“编辑标注样式”确定后写回。</summary>
        public AnnotStyle Style { get; set; }

        /// <summary>区域识别完成后是否弹出校对窗；关闭则直接把识别结果并入主题/圈注。</summary>
        public bool ShowOcrReview
        {
            get { return ocrReviewCheckBox == null || ocrReviewCheckBox.Checked; }
        }

        /// <summary>设置页“标注样式”一行的状态文字。</summary>
        public void UpdateStyleStatus()
        {
            if (styleStatusLabel == null)
                return;
            AnnotStyle style = Style;
            if (style == null || !style.Enabled)
            {
                styleStatusLabel.Text = "未启用";
                styleStatusLabel.ForeColor = Muted;
                return;
            }
            string subject = String.IsNullOrEmpty(style.Subject) ? "不改主题" : style.Subject;
            styleStatusLabel.Text = (style.MeasureOnly ? "测量标注 · " : "所有新标注 · ") + subject;
            styleStatusLabel.ForeColor = Color.FromArgb(34, 181, 115);
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
            UpdateStatusDot();
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
            if (hotkeySummary != null)
            {
                hotkeySummary.Text = hotkey.IsEmpty ? "无" : display;
                hotkeySummary.ForeColor = hotkey.IsEmpty ? Muted : TextColor;
            }
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
            // MinimumSize 用的是窗口尺寸，客户区必须留出 MinimumPanelWidth × 最小客户区高度：
            // 否则拖到最小时主题历史表格只剩一条缝；关掉主题历史时用紧凑高度。
            Size border = BorderSize();
            int minimumHeight = showHistory ? MinimumPanelHeight : CompactPanelHeight;
            MinimumSize = new Size(MinimumPanelWidth + border.Width, minimumHeight + border.Height);
        }

        /// <summary>窗口边框与标题栏占用的尺寸（客户区之外的宽高）。</summary>
        private Size BorderSize()
        {
            Size border = new Size(Width - ClientSize.Width, Height - ClientSize.Height);
            if (border.Width <= 0 || border.Height <= 0)
                border = new Size(16, 39);
            return border;
        }

        private void DimensionValueChanged(object sender, EventArgs e)
        {
            UpdateSummary();
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
            ShowOcrReview = false;
            ShowHistory = false;
            SubjectHistory = new List<SubjectHistoryEntry>();
            PanelWidth = StampPanel.DefaultPanelWidth;
            PanelHeight = StampPanel.DefaultPanelHeight;
            Style = new AnnotStyle();
        }

        public double WidthMm { get; set; }
        public double HeightMm { get; set; }

        /// <summary>"Ctrl+Alt+P" 形式的快捷键文本；"None" 表示不注册。</summary>
        public string PickHotkey { get; set; }
        public string Separator { get; set; }
        public string OcrLanguage { get; set; }

        /// <summary>区域识别完成后是否弹出校对窗；默认开，旧 settings.xml 没有这一项也按开处理。</summary>
        public bool ShowOcrReview { get; set; }

        /// <summary>是否显示主题历史；默认开，旧 settings.xml 没有这一项也按开处理。</summary>
        public bool ShowHistory { get; set; }
        public List<SubjectHistoryEntry> SubjectHistory { get; set; }

        /// <summary>新标注自动套用的样式（字段/颜色/边框/测量参数）。</summary>
        public AnnotStyle Style { get; set; }

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
                    settings.Style = NormalizeStyle(settings.Style);
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
                    ShowOcrReview = ShowOcrReview,
                    ShowHistory = ShowHistory,
                    Style = NormalizeStyle(Style),
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

        /// <summary>反序列化得到的样式可能为 null 或数值越界，统一夹回范围。</summary>
        public static AnnotStyle NormalizeStyle(AnnotStyle style)
        {
            AnnotStyle normalized = (style ?? AnnotStyle.Default()).Clone();
            normalized.Normalize();
            return normalized;
        }

        internal static decimal ClampDimension(double value, double fallback)        {
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
