using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using ClickCircle;

internal static class HistoryPreferenceTests
{
    private static int checks;
    private static void Expect(bool condition, string description)
    {
        if (!condition) throw new Exception(description);
        checks++;
    }

    private static object Call(object target, string method, params object[] arguments)
    {
        return target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).
            Invoke(target, arguments);
    }

    private static void Model()
    {
        SubjectHistory history = new SubjectHistory();
        Expect(!history.Remember(" \t"), "Blank input never creates history.");
        history.Remember(" LI-058/LI-058/LI-282 ");
        history.Remember("MANUAL  / -");
        history.Remember("LI-058/LI-058/LI-282");
        Expect(history.Search("").Count == 2 &&
            history.Search("")[0].Subject == "LI-058/LI-058/LI-282",
            "Exact complete themes deduplicate, preserving repeated IDs and internal punctuation.");
        Expect(!history.Remember("LI-058/LI-058/LI-282"), "Reusing newest theme is a no-op.");
        Expect(history.SetPinned("MANUAL  / -", true), "Existing theme can be pinned.");
        Expect(history.Search("")[0].Subject == "MANUAL  / -", "Pinned theme sorts first.");
        history.Remember("MANUAL  / -");
        Expect(history.Search("manual")[0].Pinned, "Reuse keeps pin and search ignores case.");
        history.Remember("manual  / -");
        Expect(history.Search("manual").Count == 2, "Theme identity remains case-sensitive.");
        Expect(history.Search("LI-058/LI-058")[0].Subject == "LI-058/LI-058/LI-282",
            "Search matches inside a combined theme.");
        List<SubjectHistoryEntry> snapshot = history.Export();
        snapshot[0].Subject = "MUTATED";
        Expect(history.Search("MUTATED").Count == 0, "Export is defensively copied.");
        snapshot = history.Search("");
        snapshot[0].Pinned = false;
        Expect(history.Search("MANUAL")[0].Pinned, "Search results are defensively copied.");

        history = new SubjectHistory();
        history.Remember("FAVORITE");
        history.SetPinned("FAVORITE", true);
        for (int index = 0; index < 65; index++) history.Remember("RECENT-" + index);
        Expect(history.Search("").Count == 51 && history.Search("FAVORITE")[0].Pinned,
            "Fifty recent entries never evict a favorite.");
        Expect(history.Search("RECENT-0").Count == 0 &&
            history.Search("")[1].Subject == "RECENT-64", "Oldest recent entries are evicted.");
        history.SetPinned("FAVORITE", false);
        Expect(history.Search("").Count == 50 && history.Search("FAVORITE").Count == 0,
            "Unpinning applies the normal recency limit.");
        for (int index = 0; index < 50; index++)
        {
            history.Remember("PIN-" + index);
            history.SetPinned("PIN-" + index, true);
        }
        history.Remember("EXTRA");
        Expect(!history.SetPinned("EXTRA", true), "Pin cap cannot discard existing favorites.");
        Expect(history.Search("PIN-").Count == 50 && !history.Search("EXTRA")[0].Pinned,
            "Full pinned set remains intact.");
        Expect(!history.SetPinned("MISSING", true), "Missing theme is not inserted by pinning.");

        List<SubjectHistoryEntry> saved = new List<SubjectHistoryEntry> {
            null, new SubjectHistoryEntry { Subject = " " },
            new SubjectHistoryEntry { Subject = " FIRST ", Pinned = true },
            new SubjectHistoryEntry { Subject = "FIRST" },
            new SubjectHistoryEntry { Subject = "SECOND" }
        };
        history = new SubjectHistory(saved);
        saved[2].Subject = "CHANGED";
        Expect(history.Search("").Count == 2 && history.Search("")[0].Subject == "FIRST",
            "Load sanitizes, deduplicates, copies and preserves order.");
    }

    private static void Persistence()
    {
        string directory = Path.Combine(Path.GetTempPath(), "ClickCircle-History-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "settings.xml");
        try
        {
            PluginSettings settings = new PluginSettings {
                WidthMm = 7, HeightMm = 8, Separator = " ", PickHotkey = "F9", OcrLanguage = "en-US",
                SubjectHistory = new List<SubjectHistoryEntry> {
                    new SubjectHistoryEntry { Subject = "LI-058/LI-058/LI-282", Pinned = true },
                    new SubjectHistoryEntry { Subject = "\u5e94\u6025\u7167\u660e  / B" }
                }
            };
            Expect(settings.Save(path), "First settings save succeeds.");
            PluginSettings loaded = PluginSettings.Load(path);
            Expect(loaded.OcrLanguage == "en-US" && loaded.SubjectHistory.Count == 2 &&
                loaded.SubjectHistory[0].Pinned, "Language and pinned themes survive disk reload.");
            Expect(loaded.WidthMm == 7 && loaded.HeightMm == 8 && loaded.Separator == " " &&
                loaded.PickHotkey == "F9", "New fields preserve existing settings.");
            Expect(loaded.SubjectHistory[1].Subject == "\u5e94\u6025\u7167\u660e  / B",
                "Unicode and internal spaces survive serialization.");
            settings.OcrLanguage = "";
            settings.Separator = "";
            Expect(settings.Save(path) && PluginSettings.Load(path).OcrLanguage == "" &&
                PluginSettings.Load(path).Separator == "", "Atomic replacement preserves explicit auto and empty separator.");
            Expect(Directory.GetFiles(directory, "*.tmp").Length == 0, "Successful save leaves no temp file.");
            File.WriteAllText(path, "<CircleStampSettings><WidthMm>4</WidthMm><Separator>-</Separator></CircleStampSettings>");
            loaded = PluginSettings.Load(path);
            Expect(loaded.OcrLanguage == "" && loaded.SubjectHistory.Count == 0 &&
                loaded.WidthMm == 4 && loaded.Separator == "-", "Legacy settings get safe defaults.");
            loaded.OcrLanguage = "en-US\";bad";
            loaded.SubjectHistory = null;
            Expect(loaded.Save(path) && PluginSettings.Load(path).OcrLanguage == "" &&
                PluginSettings.Load(path).SubjectHistory.Count == 0, "Invalid preference and null history are sanitized.");
            string preserved = File.ReadAllText(path);
            using (FileStream locked = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None))
                Expect(!settings.Save(path), "A locked destination reports a failed save.");
            Expect(File.ReadAllText(path) == preserved && Directory.GetFiles(directory, "*.tmp").Length == 0,
                "Failed replacement preserves prior settings and cleans the temp file.");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
            Directory.Delete(directory, false);
        }
    }

    private static void Panel()
    {
        using (StampPanel panel = new StampPanel())
        {
            int changes = 0, resumes = 0, languageChanges = 0;
            panel.HistoryChanged += delegate { changes++; };
            panel.ResumeRequested += delegate { resumes++; };
            panel.OcrLanguageChanged += delegate { languageChanges++; };
            DataGridView grid = (DataGridView)panel.Controls.Find("SubjectHistory", true)[0];
            TextBox search = (TextBox)panel.Controls.Find("HistorySearch", true)[0];
            panel.SetPickedFragments(new[] { "LI-058", "LI-058", "LI-282" });
            Expect(changes == 1 && grid.Rows.Count == 1, "Successful text pick records one complete theme.");
            panel.Subject = "MANUAL";
            Expect(changes == 1, "Typing does not flood history.");
            panel.RememberCurrentSubject();
            Expect(changes == 2 && grid.Rows.Count == 2, "A used manual subject is recorded.");
            panel.CreateControl();
            IntPtr handle = panel.Handle;
            grid.Rows[1].Cells[0].Value = true;
            Application.DoEvents();
            Expect(resumes == 0 && changes == 3 && (bool)grid.Rows[0].Cells[0].Value,
                "Pinning reorders and saves without resuming.");
            search.Text = "li-058";
            Expect(grid.Rows.Count == 1, "Search filters combined themes immediately.");
            Call(grid, "OnCellClick", new DataGridViewCellEventArgs(1, 0));
            Expect(resumes == 1 && panel.Subject == "LI-058/LI-058/LI-282",
                "One theme click restores exact text and requests one resume.");
            panel.Separator = "-";
            Expect(panel.Subject == "LI-058/LI-058/LI-282", "History is literal and ignores the current separator.");
            Call(grid, "OnCellClick", new DataGridViewCellEventArgs(0, 0));
            Expect(resumes == 1, "Clicking pin cell does not resume.");
            KeyEventArgs enter = new KeyEventArgs(Keys.Enter);
            Call(grid, "OnKeyDown", enter);
            Expect(resumes == 2 && enter.SuppressKeyPress, "Enter activates once without a second grid action.");
            Call(grid, "OnKeyDown", new KeyEventArgs(Keys.Down));
            Expect(resumes == 2, "Arrow navigation does not activate.");
            search.Text = "NO-MATCH";
            Expect(grid.Rows.Count == 0 && panel.Subject == "LI-058/LI-058/LI-282",
                "Empty search results preserve the subject.");
            panel.OcrLanguage = "en-US";
            panel.OcrLanguage = "en-US";
            Expect(languageChanges == 1, "Language preference emits once per real change.");
            PluginSettings saved = panel.GetSettings();
            using (StampPanel reopened = new StampPanel(saved))
                Expect(reopened.OcrLanguage == "en-US" && reopened.GetSettings().SubjectHistory.Count == 2 &&
                    reopened.GetSettings().SubjectHistory[1].Pinned,
                    "Panel restart restores language, history and pin.");
            panel.ApplySettings(saved);
            Expect(changes == 3 && languageChanges == 1, "Loading settings emits no save events.");
            panel.StartPosition = FormStartPosition.Manual;
            panel.Location = new Point(-4000, -4000);
            panel.Show();
            panel.Activate();
            search.Focus();
            Application.DoEvents();
            Expect(panel.IsTextInputFocused(), "History search is protected from pick shortcuts.");
            grid.Focus();
            Expect(panel.IsTextInputFocused(), "Grid keyboard navigation is protected from pick shortcuts.");
            Expect(grid.Height >= 160 && grid.Width > 400, "History fits the compact panel.");
            panel.Hide();
        }
    }

    private static OcrResult Result(string used)
    {
        OcrResult result = new OcrResult { Language = used, Fragments = new[] { "LI-058" } };
        result.Languages.Add(new OcrLanguage("en-US", "English"));
        result.Languages.Add(new OcrLanguage("zh-Hans-CN", "Chinese"));
        return result;
    }

    private static void Language()
    {
        using (Bitmap image = new Bitmap(40, 40))
        {
            foreach (string tag in new[] { "", "en-US", "zh-Hans-CN" })
            using (OcrReviewDialog dialog = new OcrReviewDialog(image, "/", tag))
            {
                int changes = 0;
                dialog.LanguagePreferenceChanged += delegate { changes++; };
                Expect(dialog.SelectedLanguageTag == tag, "Preferred language is ready before first recognition: " + tag);
                Call(dialog, "CompleteRecognition", Result("en-US"));
                Expect(dialog.SelectedLanguageTag == tag && changes == 0,
                    "Inventory refresh and actual engine choice do not overwrite preference: " + tag);
                ComboBox languages = (ComboBox)dialog.Controls.Find("OcrLanguage", true)[0];
                languages.SelectedIndex = tag == "en-US" ? 2 : 1;
                Expect(changes == 1, "User language selection publishes immediately.");
                dialog.CancelRecognition();
                Expect(dialog.SelectedLanguageTag != tag && changes == 1,
                    "Canceling review does not discard selected language.");
            }
            using (OcrReviewDialog dialog = new OcrReviewDialog(image, "/", "xx-XX"))
            {
                int changes = 0;
                dialog.LanguagePreferenceChanged += delegate { changes++; };
                OcrResult unavailable = Result("");
                unavailable.Error = "Unavailable";
                unavailable.LanguageUnavailable = true;
                Call(dialog, "CompleteRecognition", unavailable);
                Expect(dialog.SelectedLanguageTag == "" && changes == 1, "Missing saved language falls back and publishes auto.");
                Call(dialog, "CompleteRecognition", Result("en-US"));
                Expect(dialog.SelectedLanguageTag == "" && changes == 1, "Fallback does not repeatedly overwrite preference.");
                Expect(dialog.Controls.Find("OcrStatus", true)[0].Text.Contains("\u81ea\u52a8"),
                    "Fallback notice remains visible after recognition.");
            }
            using (OcrReviewDialog dialog = new OcrReviewDialog(image, "/", "en-US"))
            {
                Call(dialog, "CompleteRecognition", new OcrResult { Error = "Temporary failure" });
                Expect(dialog.SelectedLanguageTag == "en-US", "Transient OCR failure preserves saved language.");
            }
            foreach (string invalid in new[] { null, "en US", "\"", new string('a', 65) })
            using (OcrReviewDialog dialog = new OcrReviewDialog(image, "/", invalid))
                Expect(dialog.SelectedLanguageTag == "", "Unsafe saved preference defaults to auto.");
        }
    }

    [STAThread]
    private static int Main()
    {
        try
        {
            Application.EnableVisualStyles();
            Model(); Persistence(); Panel(); Language();
            Console.WriteLine("HistoryPreference: " + checks + " checks passed.");
            return 0;
        }
        catch (Exception exception) { Console.Error.WriteLine(exception); return 1; }
    }
}
