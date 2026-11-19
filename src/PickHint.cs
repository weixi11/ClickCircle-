using System;
using System.Drawing;
using System.Windows.Forms;

namespace ClickCircle
{
    /// <summary>
    /// 取字成功时在鼠标旁短暂显示的小提示。不抢焦点、不挡点击，1.4 秒后自动隐藏。
    /// </summary>
    internal sealed class PickHintForm : Form
    {
        private readonly Label label;
        private readonly Timer timer;

        public PickHintForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            BackColor = Color.FromArgb(43, 47, 54);
            Padding = new Padding(12, 7, 12, 7);
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            label = new Label {
                AutoSize = true,
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                Font = new Font("Microsoft YaHei UI", 9F),
                Margin = Padding.Empty
            };
            Controls.Add(label);
            timer = new Timer { Interval = 1400 };
            timer.Tick += delegate { timer.Stop(); Hide(); };
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams parameters = base.CreateParams;
                parameters.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE
                parameters.ExStyle |= 0x00000080; // WS_EX_TOOLWINDOW
                parameters.ExStyle |= 0x00000020; // WS_EX_TRANSPARENT
                return parameters;
            }
        }

        /// <summary>在屏幕坐标 point 附近显示 text，已显示时只更新文字并重新计时。</summary>
        public void ShowAt(string text, Point point)
        {
            label.Text = text ?? String.Empty;
            if (!Visible)
                Show();
            else
                PerformLayout();
            Rectangle area = Screen.FromPoint(point).WorkingArea;
            int x = point.X + 18;
            int y = point.Y + 22;
            if (x + Width > area.Right)
                x = point.X - Width - 18;
            if (y + Height > area.Bottom)
                y = point.Y - Height - 22;
            Location = new Point(Math.Max(area.Left, x), Math.Max(area.Top, y));
            timer.Stop();
            timer.Start();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && timer != null)
                timer.Dispose();
            base.Dispose(disposing);
        }
    }
}
