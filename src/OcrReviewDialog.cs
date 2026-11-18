using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace ClickCircle
{
    public sealed class OcrReviewDialog : Form
    {
        private Bitmap image;
        private readonly PictureBox preview;
        private readonly ComboBox languages;
        private readonly TextBox fragments;
        private readonly TextBox subject;
        private readonly Label status;
        private readonly Button recognize, rotate, accept;
        private readonly string separator;
        private readonly ToolTip tips = new ToolTip();
        private readonly System.Windows.Forms.Timer reviewTimer = new System.Windows.Forms.Timer { Interval = 200 };
        private string composedText;
        private CancellationTokenSource job;
        private int generation;
        private bool busy;
        private bool updatingLanguages;
        private string languageNotice = String.Empty;
        public string[] ConfirmedFragments { get; private set; }
        public event Action LanguagePreferenceChanged;

        public string SelectedLanguageTag
        {
            get
            {
                OcrLanguage selected = languages.SelectedItem as OcrLanguage;
                return selected == null ? String.Empty : selected.Tag;
            }
        }

        public OcrReviewDialog(Bitmap bitmap, string separator) : this(bitmap, separator, "") { }

        public OcrReviewDialog(Bitmap bitmap, string separator, string preferredLanguage)
        {
            if (bitmap == null) throw new ArgumentNullException("bitmap");
            image = new Bitmap(bitmap);
            this.separator = separator ?? "/";
            Text = "区域识别校对";
            Font = new Font("Microsoft YaHei UI", 10F);
            // The native host changes DPI contexts; avoid a second WinForms rescale.
            AutoScaleMode = AutoScaleMode.None;
            ClientSize = new Size(760, 566);
            MinimumSize = new Size(600, 480);
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            MinimizeBox = false;
            MaximizeBox = false;
            Padding = new Padding(16);

            TableLayoutPanel layout = new TableLayoutPanel {
                Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Margin = Padding.Empty
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            Controls.Add(layout);

            TableLayoutPanel toolbar = new TableLayoutPanel {
                Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1, Margin = Padding.Empty
            };
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 56));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 44));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 104));
            languages = new ComboBox {
                Name = "OcrLanguage", AccessibleName = "识别语言", Dock = DockStyle.Top,
                DropDownStyle = ComboBoxStyle.DropDownList, Margin = new Padding(0, 4, 8, 0)
            };
            languages.Items.Add(new OcrLanguage("", "自动（编号优先）"));
            languages.SelectedIndex = 0;
            string savedLanguage = OcrService.NormalizeLanguagePreference(preferredLanguage);
            if (savedLanguage.Length != 0)
            {
                languages.Items.Add(new OcrLanguage(savedLanguage, "上次选择"));
                languages.SelectedIndex = 1;
            }
            languages.SelectedIndexChanged += delegate {
                if (updatingLanguages) return;
                languageNotice = String.Empty;
                if (LanguagePreferenceChanged != null) LanguagePreferenceChanged();
            };
            rotate = MakeButton("\u21bb", "顺时针旋转 90 度");
            rotate.Font = new Font("Segoe UI Symbol", 15);
            tips.SetToolTip(rotate, "顺时针旋转 90 度");
            rotate.Click += delegate {
                image.RotateFlip(RotateFlipType.Rotate90FlipNone);
                preview.Invalidate();
                StartRecognition();
            };
            recognize = MakeButton("重新识别", "重新识别");
            recognize.Click += delegate { StartRecognition(); };
            toolbar.Controls.Add(MakeLabel("语言"), 0, 0);
            toolbar.Controls.Add(languages, 1, 0);
            toolbar.Controls.Add(rotate, 2, 0);
            toolbar.Controls.Add(recognize, 3, 0);
            layout.Controls.Add(toolbar, 0, 0);

            TableLayoutPanel content = new TableLayoutPanel {
                Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, Margin = Padding.Empty
            };
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            content.Controls.Add(MakeLabel("原图区域"), 0, 0);
            content.Controls.Add(MakeLabel("文字片段"), 1, 0);
            preview = new PictureBox {
                Image = image, SizeMode = PictureBoxSizeMode.Zoom, Dock = DockStyle.Fill,
                BackColor = Color.White, BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(0, 0, 12, 0), AccessibleName = "识别区域原图"
            };
            fragments = new TextBox {
                Name = "OcrFragments", AccessibleName = "识别文字片段", Multiline = true,
                AcceptsReturn = true, ScrollBars = ScrollBars.Both, WordWrap = false,
                Dock = DockStyle.Fill, Margin = Padding.Empty, MaxLength = 1000000
            };
            fragments.TextChanged += delegate { UpdateSubject(); };
            // Native accessibility edits can bypass the WinForms TextChanged event.
            reviewTimer.Tick += delegate {
                if (fragments.Text != composedText) UpdateSubject();
            };
            content.Controls.Add(preview, 0, 1);
            content.Controls.Add(fragments, 1, 1);
            layout.Controls.Add(content, 0, 1);

            TableLayoutPanel combined = new TableLayoutPanel {
                Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = Padding.Empty
            };
            combined.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            combined.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            combined.Controls.Add(MakeLabel("合并主题"), 0, 0);
            subject = new TextBox {
                Name = "OcrSubject", AccessibleName = "识别后合并主题", ReadOnly = true,
                Multiline = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill,
                Margin = Padding.Empty
            };
            combined.Controls.Add(subject, 0, 1);
            layout.Controls.Add(combined, 0, 2);
            status = MakeLabel("");
            status.Name = "OcrStatus";
            status.AccessibleName = "识别状态";
            layout.Controls.Add(status, 0, 3);
            TableLayoutPanel commands = new TableLayoutPanel {
                ColumnCount = 3, RowCount = 1, Dock = DockStyle.Fill, Margin = Padding.Empty
            };
            commands.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            commands.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));
            commands.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 132));
            accept = MakeButton("确认并圈注", "确认识别结果并圈注");
            accept.Name = "OcrAccept";
            accept.Width = 124;
            accept.Click += delegate {
                if (busy) return;
                string[] values = OcrService.ReviewFragments(fragments.Text);
                if (values.Length == 0) return;
                ConfirmedFragments = values;
                DialogResult = DialogResult.OK;
                Close();
            };
            Button cancel = MakeButton("取消", "取消区域识别");
            cancel.Width = 88;
            cancel.DialogResult = DialogResult.Cancel;
            CancelButton = cancel;
            commands.Controls.Add(cancel, 1, 0);
            commands.Controls.Add(accept, 2, 0);
            layout.Controls.Add(commands, 0, 4);
            UpdateSubject();
        }

        private static Label MakeLabel(string text)
        {
            return new Label { Text = text, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft,
                Margin = Padding.Empty };
        }

        private static Button MakeButton(string text, string accessible)
        {
            return new Button { Text = text, AccessibleName = accessible, Height = 34,
                Dock = DockStyle.Fill, Margin = new Padding(4, 2, 0, 2), UseVisualStyleBackColor = true };
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            reviewTimer.Start();
            StartRecognition();
        }

        private void StartRecognition()
        {
            if (busy || IsDisposed) return;
            if (job != null) job.Dispose();
            job = new CancellationTokenSource();
            CancellationToken cancel = job.Token;
            int request = ++generation;
            string tag = SelectedLanguageTag;
            Bitmap input = new Bitmap(image);
            SetBusy(true);
            status.Text = languageNotice + "正在本地识别…";
            ThreadPool.QueueUserWorkItem(delegate {
                OcrResult result;
                try { using (input) result = OcrService.Recognize(input, tag, cancel); }
                catch (OperationCanceledException) { return; }
                catch (Exception exception) { result = new OcrResult { Error = exception.Message }; }
                OcrResult completed = result;
                try
                {
                    if (!cancel.IsCancellationRequested && !IsDisposed && IsHandleCreated)
                        BeginInvoke((Action)delegate {
                            if (IsDisposed || request != generation || cancel.IsCancellationRequested) return;
                            CompleteRecognition(completed);
                        });
                }
                catch (InvalidOperationException) { }
            });
        }

        private void CompleteRecognition(OcrResult result)
        {
            string selected = SelectedLanguageTag;
            bool unavailable = false;
            if (result.Languages.Count > 0 || result.LanguageUnavailable)
            {
                updatingLanguages = true;
                try
                {
                    languages.Items.Clear();
                    languages.Items.Add(new OcrLanguage("", "自动（编号优先）"));
                    int selectedIndex = 0;
                    foreach (OcrLanguage language in result.Languages)
                    {
                        int index = languages.Items.Add(language);
                        if (String.Equals(language.Tag, selected, StringComparison.OrdinalIgnoreCase))
                            selectedIndex = index;
                    }
                    unavailable = selected.Length != 0 && (selectedIndex == 0 || result.LanguageUnavailable);
                    languages.SelectedIndex = unavailable ? 0 : selectedIndex;
                }
                finally { updatingLanguages = false; }
            }
            if (unavailable)
            {
                languageNotice = "上次语言不可用，已改为自动。";
                if (LanguagePreferenceChanged != null) LanguagePreferenceChanged();
            }
            fragments.Text = result.Error == null ? String.Join(Environment.NewLine, result.Fragments) : "";
            SetBusy(false);
            if (unavailable && result.Error != null && Visible)
            {
                StartRecognition();
                return;
            }
            status.Text = languageNotice + (result.Error ?? (result.Fragments.Length == 0 ? "未识别到文字" :
                "识别完成，待校对 · " + result.Fragments.Length + " 段 · " + result.Language));
            fragments.Focus();
            fragments.Select(0, 0);
        }

        private void SetBusy(bool value)
        {
            busy = value;
            languages.Enabled = recognize.Enabled = rotate.Enabled = fragments.Enabled = !busy;
            UpdateSubject();
        }

        private void UpdateSubject()
        {
            if (subject == null || accept == null) return;
            composedText = fragments.Text;
            string[] values = OcrService.ReviewFragments(composedText);
            subject.Text = String.Join(separator, values);
            accept.Enabled = !busy && values.Length != 0;
        }

        public void CancelRecognition()
        {
            generation++;
            reviewTimer.Stop();
            if (job != null) job.Cancel();
            DialogResult = DialogResult.Cancel;
            Close();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            generation++;
            reviewTimer.Stop();
            if (job != null) job.Cancel();
            base.OnFormClosing(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                generation++;
                reviewTimer.Dispose();
                if (job != null) { job.Cancel(); job.Dispose(); job = null; }
                tips.Dispose();
                if (image != null) { preview.Image = null; image.Dispose(); image = null; }
            }
            base.Dispose(disposing);
        }
    }
}
