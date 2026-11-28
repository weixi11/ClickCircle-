using System;
using System.Drawing;
using System.Windows.Forms;

namespace ClickCircle
{
    /// <summary>
    /// 取字提示的落点：鼠标右下方 18 / 22 像素（靠近屏幕边缘时自动翻到鼠标另一侧）。
    /// </summary>
    public static class PickHintPlacement
    {
        /// <summary>提示相对鼠标向右的偏移。</summary>
        public const int OffsetX = 18;

        /// <summary>提示相对鼠标向下的偏移。</summary>
        public const int OffsetY = 22;

        public static Point NearCursor(Point cursor, Size size, Rectangle workingArea)
        {
            int x = cursor.X + OffsetX;
            int y = cursor.Y + OffsetY;
            if (x + size.Width > workingArea.Right)
                x = cursor.X - size.Width - OffsetX;
            if (y + size.Height > workingArea.Bottom)
                y = cursor.Y - size.Height - OffsetY;
            return new Point(Math.Max(workingArea.Left, x), Math.Max(workingArea.Top, y));
        }
    }

    /// <summary>取字/圈注提示的文字格式；纯函数便于测试。</summary>
    public static class PickHintText
    {
        /// <summary>提示正文最多显示的字数，超出截断。</summary>
        public const int MaxBodyLength = 44;

        public static string ForPick(string fragment, int count)
        {
            string body = Trim(fragment);
            if (body.Length == 0 && count <= 0)
                return null;
            return count > 0
                ? "已取 " + count + " 段" + (body.Length > 0 ? " · " + body : String.Empty)
                : body;
        }

        public static string ForStamp(string subject, int count)
        {
            string body = Trim(subject);
            return count > 0
                ? "已圈注 " + count + " 个" + (body.Length > 0 ? " · " + body : String.Empty)
                : "已圈注" + (body.Length > 0 ? " · " + body : String.Empty);
        }

        private static string Trim(string value)
        {
            string body = (value ?? String.Empty).Trim();
            if (body.Length > MaxBodyLength)
                body = body.Substring(0, MaxBodyLength) + "…";
            return body;
        }
    }
    /// <summary>
    /// 取字成功时在鼠标旁短暂显示的小提示：半透明、不抢焦点、不挡点击，0.8 秒后自动隐藏。
    /// 放在鼠标旁是为了取字时一眼能看到取到了什么；半透明 + 很快消失，尽量不挡住图纸。
    /// </summary>
    internal sealed class PickHintForm : Form
    {
        /// <summary>提示窗口的整体不透明度。</summary>
        public const float HintOpacity = 0.88F;

        /// <summary>提示自动消失的时间（毫秒）。</summary>
        public const int VisibleMilliseconds = 800;

        private readonly Label label;
        private readonly Timer timer;

        public PickHintForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            BackColor = Color.FromArgb(43, 47, 54);
            Opacity = HintOpacity;
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
            timer = new Timer { Interval = VisibleMilliseconds };
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

        /// <summary>在鼠标位置（anchorPoint）旁边显示 text，已显示时只更新文字并重新计时。</summary>
        public void ShowAt(string text, Point anchorPoint)
        {
            label.Text = text ?? String.Empty;
            if (!Visible)
                Show();
            else
                PerformLayout();
            Point location = PickHintPlacement.NearCursor(anchorPoint, Size,
                Screen.FromPoint(anchorPoint).WorkingArea);
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
