using System;
using System.Reflection;
using System.Windows.Forms;
using ClickCircle;

internal static class HotkeyPanelTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private const string ApplyError = "快捷键不可用：模拟注册失败";
    private static int checkedCases;

    private static void Expect(bool condition, string description)
    {
        if (!condition)
            throw new Exception("Failed: " + description);
        checkedCases++;
    }

    private static T Field<T>(StampPanel panel, string name)
    {
        return (T)typeof(StampPanel).GetField(name, PrivateInstance).GetValue(panel);
    }

    private static string Status(StampPanel panel)
    {
        return Field<Label>(panel, "statusLabel").Text;
    }

    private static void ClickButton(StampPanel panel, string field)
    {
        Button button = Field<Button>(panel, field);
        typeof(Button).GetMethod("OnClick", PrivateInstance)
            .Invoke(button, new object[] { EventArgs.Empty });
    }

    private static void BeginCapture(StampPanel panel)
    {
        ClickButton(panel, "hotkeyButton");
        Expect(Field<bool>(panel, "capturingHotkey"), "capture starts without showing a window");
    }

    private static StampPanel NewPanel()
    {
        return new StampPanel(new PluginSettings {
            WidthMm = 2.5,
            HeightMm = 3.5,
            PickHotkey = "Ctrl+Alt+P"
        });
    }

    [STAThread]
    public static int Main()
    {
        try
        {
            using (StampPanel panel = NewPanel())
            {
                int changes = 0;
                panel.HotkeyChanged += delegate { changes++; };
                BeginCapture(panel);
                panel.CompleteHotkeyCapture(new HotkeyBinding(Keys.F9, Keys.None));
                Expect(changes == 1, "captured binding is applied once");
                Expect(panel.GetSettings().PickHotkey == "F9", "captured binding is stored");
                Expect(Status(panel) == "快捷键已设为 F9", "successful application keeps its status");
                Expect(!Field<bool>(panel, "capturingHotkey"), "successful capture ends recording");
                Expect(Math.Abs(panel.WidthMm - 2.5) < 0.001 &&
                    Math.Abs(panel.HeightMm - 3.5) < 0.001, "hotkey application preserves dimensions");
            }

            using (StampPanel panel = NewPanel())
            {
                int attempts = 0;
                panel.Subject = "LI-282";
                panel.SetMode("stamping");
                panel.HotkeyChanged += delegate {
                    attempts++;
                    panel.SetStatus(ApplyError);
                };
                BeginCapture(panel);
                panel.CompleteHotkeyCapture(new HotkeyBinding(Keys.F9, Keys.None));
                Expect(Status(panel) == ApplyError, "failed registration remains visible");
                Expect(!Field<Button>(panel, "resumeButton").Enabled &&
                    Field<Button>(panel, "pauseButton").Enabled, "application failure preserves stamp mode");
                BeginCapture(panel);
                panel.CompleteHotkeyCapture(new HotkeyBinding(Keys.F9, Keys.None));
                Expect(attempts == 2, "recording the same binding retries registration");
                Expect(Status(panel) == ApplyError, "a failed retry remains visible");
                BeginCapture(panel);
                panel.CompleteHotkeyCapture(HotkeyBinding.Empty);
                Expect(panel.GetSettings().PickHotkey == "None", "capture can clear an unavailable binding");
                Expect(Status(panel) == "快捷键已清除", "clearing requires no available shortcut engine");
            }

            using (StampPanel panel = NewPanel())
            {
                int changes = 0;
                panel.HotkeyChanged += delegate {
                    changes++;
                    panel.SetStatus(ApplyError);
                };
                ClickButton(panel, "clearHotkeyButton");
                Expect(changes == 1, "clear button applies the empty binding");
                Expect(panel.Hotkey.IsEmpty, "clear button removes the binding");
                Expect(Status(panel) == "快捷键已清除", "clear button reports a completed clear");
            }

            using (StampPanel panel = NewPanel())
            {
                int changes = 0;
                panel.HotkeyChanged += delegate { changes++; };
                BeginCapture(panel);
                panel.CompleteHotkeyCapture(null);
                Expect(panel.GetSettings().PickHotkey == "Ctrl+Alt+P", "hook cancellation keeps the binding");
                Expect(changes == 0, "hook cancellation does not reapply the binding");
                Expect(Status(panel) == "已取消快捷键设置", "hook cancellation reports cancellation");
                BeginCapture(panel);
                Expect(panel.HandleHotkeyKey(Keys.Escape), "local escape is consumed while recording");
                Expect(panel.GetSettings().PickHotkey == "Ctrl+Alt+P", "local cancellation keeps the binding");
                Expect(changes == 0, "local cancellation does not reapply the binding");
                Expect(Status(panel) == "已取消快捷键设置", "local cancellation reports cancellation");
                Expect(!panel.HandleHotkeyKey(Keys.F9), "ordinary keys are not consumed outside recording");
            }

            using (StampPanel panel = NewPanel())
            {
                panel.HotkeyChanged += delegate { panel.SetStatus(ApplyError); };
                BeginCapture(panel);
                Expect(panel.HandleHotkeyKey(Keys.F9), "local capture consumes the selected key");
                Expect(Status(panel) == ApplyError, "local capture also preserves registration errors");
                BeginCapture(panel);
                Expect(panel.HandleHotkeyKey(Keys.Back), "backspace clears during local recording");
                Expect(panel.GetSettings().PickHotkey == "None", "local clear stores None");
                Expect(Status(panel) == "快捷键已清除", "local clear also works without a shortcut engine");
            }

            Console.WriteLine("HotkeyPanel: " + checkedCases + " cases passed.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }
}
