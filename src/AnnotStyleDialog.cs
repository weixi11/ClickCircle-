using System;
using System.Drawing;
using System.Windows.Forms;

namespace ClickCircle
{
    /// <summary>编辑“新标注自动套用”的样式：字段、颜色、边框、测量参数。</summary>
    public sealed class AnnotStyleDialog : Form
    {
        private readonly CheckBox enabledBox;
        private readonly CheckBox measureOnlyBox;
        private readonly TextBox subjectBox;
        private readonly TextBox authorBox;
        private readonly TextBox contentsBox;
        private readonly Button strokeButton;
        private readonly Button fillButton;
        private readonly CheckBox useFillBox;
        private readonly NumericUpDown widthInput;
        private readonly NumericUpDown opacityInput;
        private readonly CheckBox dashedBox;
        private readonly NumericUpDown scaleInput;
        private readonly NumericUpDown decimalsInput;
        private readonly CheckBox thousandsBox;

        private int strokeArgb;
        private int fillArgb;

        public AnnotStyleDialog(AnnotStyle style)
        {
            AnnotStyle source = (style ?? AnnotStyle.Default()).Clone();
            source.Normalize();
            strokeArgb = source.StrokeArgb;
            fillArgb = source.FillArgb;

            Text = "标注样式";
            AutoScaleMode = AutoScaleMode.None;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(452, 640);
            Font = new Font("Microsoft YaHei UI", 9.5F);
            BackColor = Color.FromArgb(245, 247, 250);
            Padding = new Padding(16, 16, 16, 12);

            TableLayoutPanel root = new TableLayoutPanel {
                Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = Padding.Empty
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52F));
            Controls.Add(root);

            TableLayoutPanel layout = new TableLayoutPanel {
                Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 14, Margin = Padding.Empty,
                AutoScroll = true
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            float[] rowHeights = { 28, 26, 28, 36, 36, 36, 28, 34, 28, 36, 28, 36, 24, 28 };
            for (int row = 0; row < rowHeights.Length; row++)
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, rowHeights[row]));
            root.Controls.Add(layout, 0, 0);

            enabledBox = new CheckBox {
                Text = "新标注自动套用本样式", AutoSize = true, Checked = source.Enabled,
                Font = new Font(Font.FontFamily, 10.5F, FontStyle.Bold), Margin = new Padding(0, 2, 0, 6)
            };
            layout.Controls.Add(enabledBox, 0, 0);

            measureOnlyBox = new CheckBox {
                Text = "只套用到测量标注（周长 / 面积 / 线），不改其它标注",
                AutoSize = true, Checked = source.MeasureOnly, Margin = new Padding(0, 0, 0, 10)
            };
            layout.Controls.Add(measureOnlyBox, 0, 1);

            layout.Controls.Add(MakeSection("字段（留空表示不修改）"), 0, 2);
            subjectBox = AddTextRow(layout, 3, "主题", source.Subject);
            authorBox = AddTextRow(layout, 4, "作者", source.Author);
            contentsBox = AddTextRow(layout, 5, "标签/正文", source.Contents);

            layout.Controls.Add(MakeSection("样式"), 0, 6);

            TableLayoutPanel colorsRow = new TableLayoutPanel {
                Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1, Margin = new Padding(0, 2, 0, 2)
            };
            colorsRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 76F));
            colorsRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96F));
            colorsRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 76F));
            colorsRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            strokeButton = MakeColorButton(strokeArgb, "线条颜色");
            strokeButton.Click += delegate { PickColor(true); };
            fillButton = MakeColorButton(fillArgb, "填充颜色");
            fillButton.Click += delegate { PickColor(false); };
            useFillBox = new CheckBox {
                Text = "启用填充", AutoSize = true, Checked = source.UseFill,
                Margin = new Padding(8, 8, 0, 0)
            };
            colorsRow.Controls.Add(MakeLabel("线条颜色"), 0, 0);
            colorsRow.Controls.Add(strokeButton, 1, 0);
            colorsRow.Controls.Add(MakeLabel("填充颜色"), 2, 0);
            colorsRow.Controls.Add(fillButton, 3, 0);
            layout.Controls.Add(colorsRow, 0, 7);
            layout.Controls.Add(useFillBox, 0, 8);

            TableLayoutPanel numbersRow = new TableLayoutPanel {
                Dock = DockStyle.Fill, ColumnCount = 6, RowCount = 1, Margin = new Padding(0, 2, 0, 2)
            };
            numbersRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 76F));
            numbersRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 86F));
            numbersRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 76F));
            numbersRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 86F));
            numbersRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            numbersRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 0F));
            widthInput = MakeNumber(0.1m, 20m, 0.1m, 1, (decimal)source.BorderWidthPt);
            opacityInput = MakeNumber(0m, 100m, 5m, 0, source.OpacityPercent);
            numbersRow.Controls.Add(MakeLabel("边框宽度"), 0, 0);
            numbersRow.Controls.Add(widthInput, 1, 0);
            numbersRow.Controls.Add(MakeLabel("不透明度%"), 2, 0);
            numbersRow.Controls.Add(opacityInput, 3, 0);
            layout.Controls.Add(numbersRow, 0, 9);

            dashedBox = new CheckBox {
                Text = "虚线边框", AutoSize = true, Checked = source.Dashed,
                Margin = new Padding(0, 4, 0, 6)
            };
            layout.Controls.Add(dashedBox, 0, 10);

            TableLayoutPanel measureRow = new TableLayoutPanel {
                Dock = DockStyle.Fill, ColumnCount = 6, RowCount = 1, Margin = new Padding(0, 2, 0, 2)
            };
            measureRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 118F));
            measureRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 86F));
            measureRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92F));
            measureRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 76F));
            measureRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 74F));
            measureRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            scaleInput = MakeNumber(0m, 10000m, 0.01m, 3, (decimal)source.MetersPerMm);
            decimalsInput = MakeNumber(-1m, 6m, 1m, 0, source.DecimalPlaces);
            Label scaleLabel = new Label {
                Text = "1 mm =", AutoSize = false, Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft, Margin = Padding.Empty
            };
            Label scaleUnit = new Label {
                Text = "m", AutoSize = false, Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft, Margin = Padding.Empty
            };
            measureRow.Controls.Add(scaleLabel, 0, 0);
            measureRow.Controls.Add(scaleInput, 1, 0);
            measureRow.Controls.Add(scaleUnit, 2, 0);
            measureRow.Controls.Add(MakeLabel("小数位"), 3, 0);
            measureRow.Controls.Add(decimalsInput, 4, 0);
            layout.Controls.Add(measureRow, 0, 11);
            Label measureHint = new Label {
                Text = "比例尺填 0 表示保持标注原有比例尺；小数位 -1 表示保持原设置。",
                AutoSize = true, ForeColor = Color.FromArgb(138, 147, 163),
                Margin = new Padding(0, 2, 0, 6)
            };
            layout.Controls.Add(measureHint, 0, 12);

            thousandsBox = new CheckBox {
                Text = "测量数字使用千位分隔符", AutoSize = true, Checked = source.UseThousands,
                Margin = new Padding(0, 2, 0, 8)
            };
            layout.Controls.Add(thousandsBox, 0, 13);

            TableLayoutPanel buttons = new TableLayoutPanel {
                Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Margin = Padding.Empty
            };
            buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92F));
            buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92F));
            Button ok = new Button { Text = "确定", DialogResult = DialogResult.OK, Dock = DockStyle.Fill, Margin = new Padding(4, 10, 0, 6) };
            Button cancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel, Dock = DockStyle.Fill, Margin = new Padding(8, 10, 0, 6) };
            buttons.Controls.Add(new Label(), 0, 0);
            buttons.Controls.Add(ok, 1, 0);
            buttons.Controls.Add(cancel, 2, 0);
            root.Controls.Add(buttons, 0, 1);
            AcceptButton = ok;
            CancelButton = cancel;
        }

        /// <summary>确定后的样式（未点确定时为空）。</summary>
        public AnnotStyle Style { get; private set; }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (DialogResult == DialogResult.OK)
            {
                AnnotStyle style = new AnnotStyle {
                    Enabled = enabledBox.Checked,
                    MeasureOnly = measureOnlyBox.Checked,
                    Subject = subjectBox.Text,
                    Author = authorBox.Text,
                    Contents = contentsBox.Text,
                    StrokeArgb = strokeArgb,
                    FillArgb = fillArgb,
                    UseFill = useFillBox.Checked,
                    BorderWidthPt = (double)widthInput.Value,
                    OpacityPercent = (int)opacityInput.Value,
                    Dashed = dashedBox.Checked,
                    MetersPerMm = (double)scaleInput.Value,
                    DecimalPlaces = (int)decimalsInput.Value,
                    UseThousands = thousandsBox.Checked
                };
                style.Normalize();
                Style = style;
            }
            base.OnFormClosing(e);
        }

        private void PickColor(bool stroke)
        {
            using (ColorDialog dialog = new ColorDialog())
            {
                dialog.FullOpen = true;
                dialog.Color = AnnotStyle.ToColor(stroke ? strokeArgb : fillArgb);
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;
                int argb = AnnotStyle.ToArgb(dialog.Color.R, dialog.Color.G, dialog.Color.B);
                if (stroke)
                {
                    strokeArgb = argb;
                    strokeButton.Text = ColorText(dialog.Color);
                }
                else
                {
                    fillArgb = argb;
                    fillButton.Text = ColorText(dialog.Color);
                }
            }
        }

        private static Button MakeColorButton(int argb, string accessibleName)
        {
            Color color = AnnotStyle.ToColor(argb);
            Button button = new Button {
                Text = ColorText(color), Dock = DockStyle.Fill, AccessibleName = accessibleName,
                Margin = new Padding(0, 6, 12, 6), BackColor = color,
                ForeColor = Contrast(color), FlatStyle = FlatStyle.Flat,
                UseVisualStyleBackColor = false
            };
            button.FlatAppearance.BorderColor = Color.FromArgb(180, 188, 200);
            return button;
        }

        private static string ColorText(Color color)
        {
            return color.R + "," + color.G + "," + color.B;
        }

        private static Color Contrast(Color color)
        {
            int brightness = (color.R * 299 + color.G * 587 + color.B * 114) / 1000;
            return brightness > 128 ? Color.Black : Color.White;
        }

        private static Label MakeSection(string text)
        {
            return new Label {
                Text = text, AutoSize = true, Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold),
                Margin = new Padding(0, 8, 0, 4)
            };
        }

        private static Label MakeLabel(string text)
        {
            return new Label {
                Text = text, AutoSize = false, Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft, Margin = Padding.Empty
            };
        }

        private static NumericUpDown MakeNumber(decimal min, decimal max, decimal step, int decimals, decimal value)
        {
            NumericUpDown input = new NumericUpDown {
                Minimum = min, Maximum = max, Increment = step, DecimalPlaces = decimals,
                Value = Math.Min(max, Math.Max(min, value)), Dock = DockStyle.Fill, Margin = new Padding(0, 6, 12, 6)
            };
            return input;
        }

        private static TextBox AddTextRow(TableLayoutPanel layout, int row, string label, string value)
        {
            TableLayoutPanel rowPanel = new TableLayoutPanel {
                Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty
            };
            rowPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 76F));
            rowPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            rowPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            TextBox box = new TextBox {
                Text = value ?? String.Empty, Dock = DockStyle.Fill, Margin = new Padding(0, 7, 0, 7)
            };
            rowPanel.Controls.Add(MakeLabel(label), 0, 0);
            rowPanel.Controls.Add(box, 1, 0);
            layout.Controls.Add(rowPanel, 0, row);
            return box;
        }
    }
}
