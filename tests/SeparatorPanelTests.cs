using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using ClickCircle;

internal static class SeparatorPanelTests
{
    private static int checkedCases;

    private static void Expect(bool condition, string description)
    {
        if (!condition)
            throw new Exception("Failed: " + description);
        ++checkedCases;
    }

    private static void ExpectSubject(StampPanel panel, string expected, string description)
    {
        Expect(panel.Subject == expected, description + ": expected [" + expected +
            "], got [" + panel.Subject + "]");
    }

    private static void CheckComposition()
    {
        using (StampPanel panel = new StampPanel())
        {
            string[] fragments = { "LI-058", "LI-058", "LI-282" };
            Expect(panel.Separator == "/", "default separator");
            panel.SetPickedFragments(fragments);
            ExpectSubject(panel, "LI-058/LI-058/LI-282", "default combination preserves repeats");
            fragments[0] = "CHANGED";
            panel.Separator = "-";
            ExpectSubject(panel, "LI-058-LI-058-LI-282", "cached fragments are copied");

            string[] separators = { "/", "\uff0c", "-", " ", "\u3001", " / ", "||", "", "\u81ea\u5b9a" };
            foreach (string separator in separators)
            {
                panel.Separator = separator;
                ExpectSubject(panel, "LI-058" + separator + "LI-058" + separator + "LI-282",
                    "separator [" + separator + "] recomposes immediately");
                Expect(panel.GetSettings().Separator == separator, "settings retain exact separator");
            }

            panel.Subject = "MANUAL-LI-058";
            panel.Separator = "/";
            ExpectSubject(panel, "MANUAL-LI-058", "manual subject survives separator changes");
            panel.SetPickedFragments(new string[] { "LI-058", "LI-282" });
            ExpectSubject(panel, "LI-058/LI-282", "new selection resumes composition");

            panel.Subject = panel.Subject;
            panel.Separator = "\u3001";
            ExpectSubject(panel, "LI-058/LI-282", "explicit unchanged subject invalidates composition");

            panel.SetPickedFragments(new string[] { "LI-058", "LI-282" });
            TextBox subject = (TextBox)panel.Controls.Find("Subject", true)[0];
            subject.Text = "USER-TEXT";
            panel.Separator = "-";
            ExpectSubject(panel, "USER-TEXT", "editing subject control invalidates composition");

            panel.SetPickedFragments(new string[] { "LI-156x8rows" });
            panel.Separator = " / ";
            ExpectSubject(panel, "LI-156x8rows", "single fragment retains internal punctuation");
            panel.SetPickedFragments(new string[] { "", null, " " });
            ExpectSubject(panel, "LI-156x8rows", "empty selection preserves previous subject");
            panel.SetPickedFragments(new string[] { "\u5e94\u6025\u7167\u660e", "\u4e00\u5c42" });
            ExpectSubject(panel, "\u5e94\u6025\u7167\u660e / \u4e00\u5c42", "unicode fragments preserved");
        }
    }

    private static void CheckControlAndEvents()
    {
        using (StampPanel panel = new StampPanel())
        {
            ComboBox combo = (ComboBox)panel.Controls.Find("Separator", true)[0];
            Expect(combo.DropDownStyle == ComboBoxStyle.DropDown, "separator remains editable");
            Expect(combo.Items.Count == 6, "six separator presets");
            string[] values = { "/", "-", "\uff0c", "\u3001", " ", "" };
            string[] labels = { "/", "-", "\uff0c", "\u3001", "\u7a7a\u683c", "\u65e0\u5206\u9694\u7b26" };
            panel.SetPickedFragments(new string[] { "LI-058", "LI-282" });
            for (int index = 0; index < values.Length; ++index)
            {
                combo.SelectedIndex = index;
                Expect(panel.Separator == values[index], "preset maps label to value " + index);
                Expect(combo.Text == labels[index], "preset label " + index);
                ExpectSubject(panel, "LI-058" + values[index] + "LI-282",
                    "preset produces exact combination " + index);
            }

            combo.SelectedIndex = -1;
            combo.Text = "::";
            Expect(panel.Separator == "::", "typed custom separator");
            ExpectSubject(panel, "LI-058::LI-282", "typed separator updates subject");
            combo.Text = " ";
            Expect(panel.Separator == " ", "typed whitespace is not trimmed");
            combo.Text = "";
            Expect(panel.Separator == "", "typed empty value means no separator");

            int changes = 0;
            panel.SeparatorChanged += delegate { ++changes; };
            panel.Separator = "/";
            Expect(changes == 1, "one event per programmatic value change");
            panel.Separator = "/";
            Expect(changes == 1, "unchanged value does not raise event");
            panel.ApplySettings(new PluginSettings { Separator = "\u3001", WidthMm = 2, HeightMm = 3 });
            Expect(changes == 1, "ApplySettings does not publish changes");
            ExpectSubject(panel, "LI-058\u3001LI-282", "ApplySettings recomposes cached selection");
            Expect(panel.GetSettings().WidthMm == 2 && panel.GetSettings().HeightMm == 3,
                "separator settings preserve dimensions");
            panel.Separator = null;
            Expect(panel.Separator == "/", "null property uses default");
            Expect(changes == 2, "defaulting null publishes changed separator once");
        }
    }

    private static void CheckSettings()
    {
        string directory = Path.Combine(Path.GetTempPath(), "ClickCircle-Separator-" +
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "settings.xml");
        try
        {
            string[] separators = { "/", "-", "\uff0c", "\u3001", " ", "", "  ", " / ", "||", "<>&", "\u81ea\u5b9a" };
            foreach (string separator in separators)
            {
                PluginSettings settings = new PluginSettings {
                    WidthMm = 2.5, HeightMm = 3.5, PickHotkey = "F9", Separator = separator
                };
                Expect(settings.Save(path), "save separator [" + separator + "]");
                PluginSettings restored = PluginSettings.Load(path);
                Expect(restored.Separator == separator, "XML preserves exact separator [" + separator + "]");
                Expect(restored.WidthMm == 2.5 && restored.HeightMm == 3.5 &&
                    restored.PickHotkey == "F9", "round trip preserves other settings");
            }
            File.WriteAllText(path, "<CircleStampSettings><WidthMm>2</WidthMm>" +
                "<HeightMm>3</HeightMm><PickHotkey>F9</PickHotkey></CircleStampSettings>");
            PluginSettings legacy = PluginSettings.Load(path);
            Expect(legacy.Separator == "/", "legacy settings receive default");
            Expect(legacy.WidthMm == 2 && legacy.HeightMm == 3 && legacy.PickHotkey == "F9",
                "legacy settings retain existing values");
            File.WriteAllText(path, "<CircleStampSettings><Separator /></CircleStampSettings>");
            Expect(PluginSettings.Load(path).Separator == "", "explicit empty XML remains empty");
            Expect(new PluginSettings { Separator = null }.Save(path), "save null separator safely");
            Expect(PluginSettings.Load(path).Separator == "/", "saved null receives default");
            File.WriteAllText(path, "<invalid");
            Expect(PluginSettings.Load(path).Separator == "/", "invalid XML receives defaults");
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
            Directory.Delete(directory);
        }
    }

    [STAThread]
    public static int Main()
    {
        try
        {
            CheckComposition();
            CheckControlAndEvents();
            CheckSettings();
            Console.WriteLine("SeparatorPanel: " + checkedCases + " cases passed.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }
}
