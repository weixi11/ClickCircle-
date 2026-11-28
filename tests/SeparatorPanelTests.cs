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

            panel.Separator = "/";
            panel.ResetPickedFragments();
            Expect(panel.PickedFragmentCount == 0, "reset clears fragment count");
            Expect(panel.AppendPickedFragments(new string[] { "A", "B" }), "append accepts fragments");
            ExpectSubject(panel, "A/B", "first append replaces the previous subject");
            Expect(panel.PickedFragmentCount == 2, "append counts all fragments");
            Expect(!panel.AppendPickedFragments(new string[] { "", " " }), "blank append is ignored");
            ExpectSubject(panel, "A/B", "blank append keeps the subject");
            Expect(panel.AppendPickedFragments(new string[] { "C" }), "second append is accepted");
            ExpectSubject(panel, "A/B/C", "second append is joined with the separator");
            Expect(panel.PickedFragmentCount == 3, "append grows the fragment count");
            panel.Separator = "-";
            ExpectSubject(panel, "A-B-C", "appended fragments still follow separator changes");
            panel.Subject = "MANUAL";
            Expect(panel.PickedFragmentCount == 0, "manual edit clears the fragment count");
            Expect(panel.AppendPickedFragments(new string[] { "D" }), "append after manual edit is accepted");
            ExpectSubject(panel, "D", "append after manual edit starts a new series");
        }
    }

    private static void CheckLock()
    {
        using (StampPanel panel = new StampPanel(new PluginSettings { ShowHistory = true }))
        {
            NumericUpDown lockInput = (NumericUpDown)panel.Controls.Find("LockedSegments", true)[0];
            Expect(lockInput.Value == 0 && panel.LockedSegments == 0, "默认不锁定");
            panel.SetPickedFragments(new[] { "V2K-12", "0-10V" });
            ExpectSubject(panel, "V2K-12/0-10V", "先取两段");
            lockInput.Value = 2;
            Expect(panel.LockedSegments == 2 && lockInput.Value == 2, "锁定两段后段数为 2");
            ExpectSubject(panel, "V2K-12/0-10V", "锁定不改变当前主题");

            panel.BeginPickingRound(true);
            Expect(panel.PickedFragmentCount == 2, "重新取字从锁定两段起步");
            ExpectSubject(panel, "V2K-12/0-10V", "重新取字保留锁定主题");
            panel.AppendPickedFragments(new[] { "V2K-11" });
            ExpectSubject(panel, "V2K-12/0-10V/V2K-11", "新取的段接在锁定段后面");

            panel.BeginPickingRound(true);
            Expect(panel.PickedFragmentCount == 2, "再次重新取字只保留锁定段");
            ExpectSubject(panel, "V2K-12/0-10V", "再次重新取字回到锁定主题");

            panel.Separator = "-";
            ExpectSubject(panel, "V2K-12-0-10V", "锁定主题跟随分隔符重组");
            panel.Separator = "/";

            lockInput.Value = 1;
            Expect(panel.LockedSegments == 1, "改锁定段数为 1 立即生效");
            ExpectSubject(panel, "V2K-12", "锁定段裁剪当前主题");
            panel.BeginPickingRound(true);
            panel.AppendPickedFragments(new[] { "0-10V" });
            ExpectSubject(panel, "V2K-12/0-10V", "锁定一段后只取新段");

            panel.SetPickedFragments(new[] { "A", "B", "C" });
            lockInput.Value = 5;
            Expect(panel.LockedSegments == 3 && lockInput.Value == 3, "锁定段数超过已取段数时按实际段数");
            ExpectSubject(panel, "A/B/C", "超量锁定后主题保留全部已取段");

            lockInput.Value = 0;
            Expect(panel.LockedSegments == 0, "解锁后不再锁定");
            panel.BeginPickingRound(true);
            Expect(panel.PickedFragmentCount == 0 && panel.Subject == String.Empty,
                "解锁后重新取字按原来的清空逻辑");

            PluginSettings saved = panel.GetSettings();
            using (StampPanel reopened = new StampPanel(saved))
            {
                Expect(reopened.LockedSegments == 0, "锁定段数随设置保存（解锁后为 0）");
            }
        }

        using (StampPanel panel = new StampPanel())
        {
            NumericUpDown lockInput = (NumericUpDown)panel.Controls.Find("LockedSegments", true)[0];
            lockInput.Value = 2;
            Expect(panel.LockedSegments == 0, "没有已取段时不锁定空内容");
            panel.SetPickedFragments(new[] { "V2K-12", "0-10V" });
            lockInput.Value = 2;
            PluginSettings saved = panel.GetSettings();
            Expect(saved.LockedSegments == 2 && saved.LockedFragments.Count == 2 &&
                saved.LockedFragments[0] == "V2K-12", "锁定片段随设置保存");
            using (StampPanel reopened = new StampPanel(saved))
            {
                Expect(reopened.LockedSegments == 2, "重开面板恢复锁定段数");
                reopened.BeginPickingRound(false);
                ExpectSubject(reopened, "V2K-12/0-10V", "重开面板后重新取字仍从锁定段起步");
            }
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
            CheckLock();
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
