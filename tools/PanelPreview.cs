using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
using System.Windows.Forms;
using ClickCircle;

// 一次性验证工具：渲染 StampPanel，并在离屏窗口里模拟“点快捷键按钮 -> 按组合键”的录制流程。
internal static class PanelPreview
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("usage: PanelPreview <output.png> <hotkey text> [record]");
            return 2;
        }
        string output = args[0];
        string hotkeyText = args[1];
        bool record = args.Length >= 3 && args[2] == "record";

        Application.EnableVisualStyles();
        using (StampPanel panel = new StampPanel(new PluginSettings
        {
            WidthMm = 5,
            HeightMm = 5,
            PickHotkey = hotkeyText
        }))
        {
            int changes = 0;
            panel.HotkeyChanged += delegate { ++changes; };

            panel.StartPosition = FormStartPosition.Manual;
            panel.Location = new Point(-4000, -4000);
            panel.CreateControl();
            panel.Show();
            panel.Activate();
            Application.DoEvents();

            Snapshot(panel, output);

            if (record)
            {
                FieldInfo field = typeof(StampPanel).GetField(
                    "hotkeyButton", BindingFlags.Instance | BindingFlags.NonPublic);
                if (field == null)
                {
                    Console.Error.WriteLine("hotkeyButton field not found");
                    return 3;
                }
                Button button = (Button)field.GetValue(panel);
                button.PerformClick();
                Application.DoEvents();
                Snapshot(panel, output.Replace(".png", "-capturing.png"));
                Console.WriteLine("capturing_text=" + button.Text);

                bool consumed = panel.HandleHotkeyKey(Keys.Control | Keys.Shift | Keys.F9);
                Application.DoEvents();
                Console.WriteLine("consumed=" + consumed);
                Console.WriteLine("recorded_display=" + panel.Hotkey.ToDisplay());
                Console.WriteLine("recorded_storage=" + panel.GetSettings().PickHotkey);
                Console.WriteLine("hotkey_changed_events=" + changes);
                Console.WriteLine("button_text=" + button.Text);
                Snapshot(panel, output.Replace(".png", "-recorded.png"));

                // 清除流程：再点一次按钮，按 Backspace
                button.PerformClick();
                Application.DoEvents();
                bool consumedClear = panel.HandleHotkeyKey(Keys.Back);
                Application.DoEvents();
                Console.WriteLine("clear_consumed=" + consumedClear);
                Console.WriteLine("cleared_storage=" + panel.GetSettings().PickHotkey);
                Console.WriteLine("cleared_display='" + panel.Hotkey.ToDisplay() + "'");
                Console.WriteLine("cleared_button_text=" + button.Text);

                // 未进入录制时，按键不应被消费
                Console.WriteLine("idle_consumed=" + panel.HandleHotkeyKey(Keys.Control | Keys.Alt | Keys.P));

                // Escape 取消录制后保持原值
                button.PerformClick();
                Application.DoEvents();
                bool escapeConsumed = panel.HandleHotkeyKey(Keys.Escape);
                Console.WriteLine("escape_consumed=" + escapeConsumed + " value_after_escape='" + panel.Hotkey.ToDisplay() + "'");
            }

            panel.Hide();
        }
        Console.WriteLine("saved " + output);
        return 0;
    }

    private static void Snapshot(Form form, string path)
    {
        using (Bitmap bitmap = new Bitmap(form.Width, form.Height))
        {
            form.DrawToBitmap(bitmap, new Rectangle(0, 0, form.Width, form.Height));
            bitmap.Save(path, ImageFormat.Png);
        }
    }
}
