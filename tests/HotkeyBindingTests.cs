using System;
using System.IO;
using System.Windows.Forms;
using ClickCircle;

internal static class HotkeyBindingTests
{
    private static int checkedCases;

    private static void Expect(bool condition, string description)
    {
        if (!condition)
            throw new Exception("Failed: " + description);
        ++checkedCases;
    }

    private static void ExpectBinding(string text, Keys key, Keys modifiers, string description)
    {
        HotkeyBinding binding;
        bool parsed = HotkeyBinding.TryParse(text, out binding);
        Expect(parsed, description + " should parse");
        Expect(binding.Key == key, description + " key should be " + key);
        Expect(binding.Modifiers == modifiers, description + " modifiers should be " + modifiers);
    }

    private static void ExpectRejected(string text, string description)
    {
        HotkeyBinding binding;
        Expect(!HotkeyBinding.TryParse(text, out binding), description + " should be rejected");
    }

    public static int Main()
    {
        try
        {
            HotkeyBinding fallback;
            Expect(HotkeyBinding.TryParse(HotkeyBinding.Default.ToStorage(), out fallback),
                "default storage text must parse");
            Expect(fallback.Equals(HotkeyBinding.Default), "default storage text must round-trip");

            // 显示与存储格式
            Expect(HotkeyBinding.Default.ToDisplay() == "Ctrl+Alt+P", "default displays as Ctrl+Alt+P");
            Expect(HotkeyBinding.Default.ToStorage() == "Ctrl+Alt+P", "default stores as Ctrl+Alt+P");
            Expect(HotkeyBinding.Empty.ToDisplay() == String.Empty, "empty displays as blank");
            Expect(HotkeyBinding.Empty.ToStorage() == "None", "empty stores as None");
            Expect(HotkeyBinding.Empty.IsEmpty, "empty binding is empty");

            // 大小写与顺序无关
            ExpectBinding("Ctrl+Alt+P", Keys.P, Keys.Control | Keys.Alt, "canonical Ctrl+Alt+P");
            ExpectBinding("alt+ctrl+p", Keys.P, Keys.Control | Keys.Alt, "lower case and reversed order");
            ExpectBinding(" Ctrl + Shift + F9 ", Keys.F9, Keys.Control | Keys.Shift, "padded Ctrl+Shift+F9");
            ExpectBinding("F9", Keys.F9, Keys.None, "bare F9");
            ExpectBinding("Ctrl+`", Keys.Oemtilde, Keys.Control, "Ctrl+backtick");
            ExpectBinding("Shift+-", Keys.OemMinus, Keys.Shift, "Shift+minus");
            ExpectBinding("Ctrl+OemQuestion", Keys.OemQuestion, Keys.Control, "enum name for slash");
            ExpectBinding("Control+F2", Keys.F2, Keys.Control, "Control alias");
            ExpectBinding("None", Keys.None, Keys.None, "explicit none");
            ExpectBinding("none", Keys.None, Keys.None, "lower case none");
            ExpectBinding("P", Keys.P, Keys.None, "bare letter");
            ExpectBinding("D1", Keys.D1, Keys.None, "digit key enum name");
            ExpectBinding("Ctrl+,", Keys.Oemcomma, Keys.Control, "literal comma key");
            ExpectBinding("Ctrl+Oem3", Keys.Oemtilde, Keys.Control, "OEM enum alias");
            ExpectBinding("LControlKey+F9", Keys.F9, Keys.Control, "left control enum alias");
            ExpectBinding("Cancel", Keys.Cancel, Keys.None, "Ctrl-Break virtual key");

            // 拒绝非法输入
            ExpectRejected("", "blank text");
            ExpectRejected(null, "null text");
            ExpectRejected("Ctrl+Alt+NotAKey", "unknown key name");
            ExpectRejected("Ctrl+Alt+P+Q", "two main keys");
            ExpectRejected("Ctrl+Alt", "modifiers without a main key");
            ExpectRejected("Ctrl+ Shift +", "trailing separator without a key");
            ExpectRejected("1", "numeric mouse-button value");
            ExpectRejected("65", "numeric keyboard value");
            ExpectRejected("Ctrl+9999", "undefined numeric key");
            ExpectRejected("Ctrl+P,Alt", "comma flags cannot silently drop Alt");
            ExpectRejected("Ctrl+F9,P", "comma flags cannot combine main keys");
            ExpectRejected("Ctrl+KeyCode", "key-code mask is not a key");
            ExpectRejected("Ctrl+Modifiers", "modifier mask is not a key");
            ExpectRejected("Ctrl+LButton", "mouse button is not a keyboard key");
            ExpectRejected("Ctrl+LWin+F9", "unsupported Windows modifier cannot be dropped");

            // 非法值回退到默认
            HotkeyBinding parsedFallback;
            Expect(HotkeyBinding.TryParse("Ctrl+Alt+P", out parsedFallback), "reference binding parses");
            Expect(HotkeyBinding.ParseOrDefault("garbage").Equals(HotkeyBinding.Default),
                "unknown text falls back to default");
            Expect(HotkeyBinding.ParseOrDefault("").Equals(HotkeyBinding.Default),
                "blank text falls back to default");
            Expect(HotkeyBinding.ParseOrDefault(null).Equals(HotkeyBinding.Default),
                "null text falls back to default");
            Expect(HotkeyBinding.ParseOrDefault("None").IsEmpty, "None stays empty instead of default");
            Expect(HotkeyBinding.ParseOrDefault("Ctrl+9999").Equals(HotkeyBinding.Default),
                "invalid numeric key falls back to default");
            Expect(HotkeyBinding.ParseOrDefault("Ctrl+P,Alt").Equals(HotkeyBinding.Default),
                "invalid comma flags fall back to default");

            // 存取往返
            string directory = Path.Combine(Path.GetTempPath(), "ClickCircleHotkeyTests", Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(directory);
                string path = Path.Combine(directory, "settings.xml");

                PluginSettings settings = new PluginSettings {
                    WidthMm = 2.5, HeightMm = 3.5, PickHotkey = "Ctrl+Shift+F9"
                };
                Expect(settings.Save(path), "settings save");
                PluginSettings loaded = PluginSettings.Load(path);
                Expect(loaded.PickHotkey == "Ctrl+Shift+F9", "hotkey survives save/load");
                Expect(Math.Abs(loaded.WidthMm - 2.5) < 0.001, "width survives save/load");
                Expect(Math.Abs(loaded.HeightMm - 3.5) < 0.001, "height survives save/load");

                settings.PickHotkey = "None";
                Expect(settings.Save(path), "save with hotkey disabled");
                loaded = PluginSettings.Load(path);
                Expect(loaded.PickHotkey == "None", "disabled hotkey survives save/load");

                settings.PickHotkey = "Ctrl+Alt+NotAKey";
                Expect(settings.Save(path), "save sanitizes invalid hotkey");
                loaded = PluginSettings.Load(path);
                Expect(loaded.PickHotkey == "Ctrl+Alt+P", "invalid hotkey is stored as default");

                settings.PickHotkey = "Ctrl+P,Alt";
                Expect(settings.Save(path), "save sanitizes comma flags");
                loaded = PluginSettings.Load(path);
                Expect(loaded.PickHotkey == "Ctrl+Alt+P", "comma flags are stored as default");

                File.WriteAllText(path,
                    "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n" +
                    "<CircleStampSettings><WidthMm>2.5</WidthMm><HeightMm>3.5</HeightMm>" +
                    "<PickHotkey>Ctrl+9999</PickHotkey></CircleStampSettings>\r\n");
                loaded = PluginSettings.Load(path);
                Expect(loaded.PickHotkey == "Ctrl+Alt+P", "invalid numeric key from XML falls back to default");
                Expect(Math.Abs(loaded.WidthMm - 2.5) < 0.001, "invalid hotkey does not discard XML dimensions");

                // 旧版本设置文件没有 PickHotkey 元素
                string legacyPath = Path.Combine(directory, "legacy.xml");
                File.WriteAllText(legacyPath,
                    "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n" +
                    "<CircleStampSettings><WidthMm>1.5</WidthMm><HeightMm>1.2</HeightMm></CircleStampSettings>\r\n");
                PluginSettings legacy = PluginSettings.Load(legacyPath);
                Expect(legacy.PickHotkey == "Ctrl+Alt+P", "legacy settings get the default hotkey");
                Expect(Math.Abs(legacy.WidthMm - 1.5) < 0.001, "legacy width is preserved");

                // 新装默认值
                PluginSettings fresh = new PluginSettings();
                Expect(fresh.PickHotkey == "Ctrl+Alt+P", "fresh settings default to Ctrl+Alt+P");
            }
            finally
            {
                try
                {
                    if (Directory.Exists(directory))
                        Directory.Delete(directory, true);
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }

            Console.WriteLine("HotkeyBinding: " + checkedCases + " cases passed.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }
}
