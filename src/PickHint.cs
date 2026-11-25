using System;
using System.Drawing;
using System.Windows.Forms;

namespace ClickCircle
{
    /// <summary>
    /// 取字提示的落点：屏幕工作区右下角。提示固定待在角落，不跟着鼠标走，
    /// 避免挡住图纸上正在点的下一个编号。
    /// </summary>
    public static class PickHintPlacement
    {
        /// <summary>提示与屏幕边缘留的距离。</summary>
        public const int Margin = 12;

        public static Point BottomRight(Rectangle workingArea, Size size)
        {
            int x = workingArea.Right - size.Width - Margin;
            int y = workingArea.Bottom - size.Height - Margin;
            return new Point(Math.Max(workingArea.Left, x), Math.Max(workingArea.Top, y));
        }
    }

    /// <summary>
    /// 取字成功时在屏幕右下角短暂显示的小提示。不抢焦点、不挡点击，1.4 秒后自动隐藏。
    /// 位置固定在角落而不是鼠标旁：连续取字时鼠标旁的小窗会盖住下一次要点的编号。
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

        /// <summary>在 anchorPoint 所在屏幕的右下角显示 text，已显示时只更新文字并重新计时。</summary>
        public void ShowAt(string text, Point anchorPoint)
        {
            label.Text = text ?? String.Empty;
            if (!Visible)
                Show();
            else
                PerformLayout();
            Point location = PickHintPlacement.BottomRight(
                Screen.FromPoint(anchorPoint).WorkingArea, Size);
            Location = location;
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
