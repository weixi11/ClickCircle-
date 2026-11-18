using System;
using System.Windows.Forms;
using ClickCircle;

internal static class HotkeyServiceTests
{
    private static int checkedCases;

    private sealed class Scenario : IDisposable
    {
        public bool TriggerAllowed = true;
        public bool CaptureAllowed = true;
        public bool Foreground = true;
        public bool ThrowScope;
        public int Triggers;
        public int Captures;
        public HotkeyBinding LastCapture;
        public readonly HotkeyService Service;

        public Scenario()
        {
            Service = new HotkeyService(
                delegate {
                    if (ThrowScope) throw new InvalidOperationException("scope test");
                    return TriggerAllowed;
                },
                delegate { return CaptureAllowed; },
                delegate { return Foreground; }, false);
            Service.Binding = new HotkeyBinding(Keys.F9, Keys.None);
            Service.Triggered += delegate { Triggers++; };
            Service.Captured += delegate(HotkeyBinding value) {
                Captures++;
                LastCapture = value;
            };
        }

        public bool Down(Keys key, Keys modifiers = Keys.None, bool windows = false)
        {
            return Service.ProcessKey((int)key, true, modifiers, windows);
        }

        public bool Up(Keys key)
        {
            return Service.ProcessKey((int)key, false, Keys.None, false);
        }

        public void Dispose()
        {
            Service.Dispose();
        }
    }

    private static void Expect(bool condition, string description)
    {
        if (!condition)
            throw new Exception("Failed: " + description);
        checkedCases++;
    }

    private static void Pump()
    {
        Application.DoEvents();
    }

    private static void Stroke(Scenario scenario, Keys key, bool consumed, string description,
        Keys modifiers = Keys.None, bool windows = false)
    {
        Expect(scenario.Down(key, modifiers, windows) == consumed, description + " down");
        Expect(scenario.Up(key) == consumed, description + " up");
    }

    [STAThread]
    public static int Main()
    {
        try
        {
            using (Scenario scenario = new Scenario())
            {
                Stroke(scenario, Keys.F9, true, "page shortcut");
                Expect(scenario.Triggers == 0, "trigger is deferred to the message queue");
                Pump();
                Expect(scenario.Triggers == 1, "allowed page shortcut triggers once");
            }

            using (Scenario scenario = new Scenario())
            {
                scenario.TriggerAllowed = false;
                Stroke(scenario, Keys.F9, false, "text-input suppression");
                Pump();
                Expect(scenario.Triggers == 0, "suppressed shortcut does not trigger");
                scenario.TriggerAllowed = true;
                scenario.Foreground = false;
                Stroke(scenario, Keys.F9, false, "other application foreground");
                Pump();
                Expect(scenario.Triggers == 0, "other application shortcut does not trigger");
                scenario.Foreground = true;
                Stroke(scenario, Keys.F9, true, "return to page focus");
                Pump();
                Expect(scenario.Triggers == 1, "returning to the page restores the shortcut");
            }

            using (Scenario scenario = new Scenario())
            {
                Stroke(scenario, Keys.F9, true, "queued before input focus changes");
                scenario.TriggerAllowed = false;
                Pump();
                Expect(scenario.Triggers == 0, "queued shortcut is discarded after focus changes");
                scenario.TriggerAllowed = true;
                Stroke(scenario, Keys.F9, true, "shortcut after discarded focus event");
                Pump();
                Expect(scenario.Triggers == 1, "discarded focus event does not block later shortcuts");
            }

            using (Scenario scenario = new Scenario())
            {
                Stroke(scenario, Keys.F9, true, "queued before foreground changes");
                scenario.Foreground = false;
                Pump();
                Expect(scenario.Triggers == 0, "queued shortcut is discarded after app switch");
                scenario.Foreground = true;
                Stroke(scenario, Keys.F9, true, "shortcut after returning from another app");
                Pump();
                Expect(scenario.Triggers == 1, "returning to the editor allows a fresh shortcut");
            }

            using (Scenario scenario = new Scenario())
            {
                Stroke(scenario, Keys.F9, true, "queued before binding replacement");
                scenario.Service.Binding = new HotkeyBinding(Keys.F10, Keys.Control);
                Pump();
                Expect(scenario.Triggers == 0, "binding replacement invalidates queued shortcuts");
                Stroke(scenario, Keys.F9, false, "old binding after replacement");
                Stroke(scenario, Keys.F10, true, "new binding after replacement", Keys.Control);
                Pump();
                Expect(scenario.Triggers == 1, "replacement binding is usable");
                Stroke(scenario, Keys.F10, true, "queued before clearing", Keys.Control);
                scenario.Service.Binding = HotkeyBinding.Empty;
                Pump();
                Expect(scenario.Triggers == 1, "clearing invalidates queued shortcuts");
                Stroke(scenario, Keys.F10, false, "cleared shortcut", Keys.Control);
            }

            using (Scenario scenario = new Scenario())
            {
                Expect(scenario.Down(Keys.F9), "initial held-key down is consumed");
                for (int repeat = 0; repeat < 8; repeat++)
                    Expect(scenario.Down(Keys.F9), "held-key repeat is consumed");
                Pump();
                Expect(scenario.Triggers == 1, "holding the key does not repeat the action");
                Expect(scenario.Up(Keys.F9), "held shortcut key-up is paired");
                for (int press = 0; press < 4; press++)
                    Stroke(scenario, Keys.F9, true, "rapid separate press " + press);
                Pump();
                Expect(scenario.Triggers == 5, "rapid separate presses are not throttled away");
            }

            using (Scenario scenario = new Scenario())
            {
                Stroke(scenario, Keys.F10, false, "unrelated key");
                Stroke(scenario, Keys.F9, false, "extra modifier", Keys.Shift);
                Stroke(scenario, Keys.F9, false, "Windows modifier", Keys.None, true);
                Stroke(scenario, Keys.LControlKey, false, "modifier key by itself", Keys.Control);
                scenario.ThrowScope = true;
                Stroke(scenario, Keys.F9, false, "scope callback failure");
                Pump();
                Expect(scenario.Triggers == 0, "unmatched keys and scope failures have no action");
            }

            using (Scenario scenario = new Scenario())
            {
                scenario.Service.Binding = HotkeyBinding.Default;
                Stroke(scenario, Keys.P, false, "missing required modifiers");
                Stroke(scenario, Keys.P, false, "additional modifier",
                    Keys.Control | Keys.Alt | Keys.Shift);
                Stroke(scenario, Keys.P, true, "default combination", Keys.Control | Keys.Alt);
                Pump();
                Expect(scenario.Triggers == 1, "exact modifier combination triggers");
            }

            using (Scenario scenario = new Scenario())
            {
                scenario.CaptureAllowed = false;
                scenario.Service.BeginCapture();
                Stroke(scenario, Keys.F10, false, "capture outside its scope");
                Pump();
                Expect(scenario.Captures == 0, "outside-scope capture does not update settings");
                Expect(scenario.Service.IsCapturing, "outside-scope key does not finish capture");
                scenario.CaptureAllowed = true;
                Stroke(scenario, Keys.LControlKey, false, "capture modifier", Keys.Control);
                Stroke(scenario, Keys.P, true, "capture selected combination", Keys.Control | Keys.Alt);
                Expect(scenario.Captures == 0, "capture result is deferred");
                Pump();
                Expect(scenario.Captures == 1 &&
                    scenario.LastCapture.Equals(HotkeyBinding.Default), "capture reports the selected binding");
                Expect(scenario.Triggers == 0, "capture does not also invoke the page tool");
            }

            using (Scenario scenario = new Scenario())
            {
                scenario.Foreground = false;
                scenario.Service.BeginCapture();
                Stroke(scenario, Keys.F10, false, "capture in another application");
                scenario.Foreground = true;
                Stroke(scenario, Keys.F10, false, "capture with Windows modifier", Keys.None, true);
                Expect(!scenario.Service.ProcessKey(0xFFFE, true, Keys.None, false),
                    "undefined virtual key is not captured");
                scenario.Service.ProcessKey(0xFFFE, false, Keys.None, false);
                Pump();
                Expect(scenario.Captures == 0 && scenario.Service.IsCapturing,
                    "ignored capture keys leave recording active");
                Stroke(scenario, Keys.Escape, true, "capture escape");
                Pump();
                Expect(scenario.Captures == 1 && scenario.LastCapture == null,
                    "escape reports cancellation");
                scenario.Service.BeginCapture();
                Stroke(scenario, Keys.Delete, true, "capture delete");
                Pump();
                Expect(scenario.Captures == 2 && scenario.LastCapture.IsEmpty,
                    "delete reports an empty binding");
                scenario.Service.BeginCapture();
                Stroke(scenario, Keys.Back, true, "capture backspace");
                Pump();
                Expect(scenario.Captures == 3 && scenario.LastCapture.IsEmpty,
                    "backspace reports an empty binding");
            }

            using (Scenario scenario = new Scenario())
            {
                scenario.Service.BeginCapture();
                Stroke(scenario, Keys.F10, true, "capture before cancellation");
                scenario.Service.CancelCapture();
                Pump();
                Expect(scenario.Captures == 0, "cancel invalidates a queued capture");
                Stroke(scenario, Keys.F9, true, "shortcut after cancelling queued capture");
                Pump();
                Expect(scenario.Triggers == 1, "cancelled capture does not block page shortcuts");
            }

            using (Scenario scenario = new Scenario())
            {
                scenario.Service.BeginCapture();
                Stroke(scenario, Keys.F10, true, "capture before leaving scope");
                scenario.CaptureAllowed = false;
                Pump();
                Expect(scenario.Captures == 0, "queued capture is discarded when its scope is lost");
                scenario.CaptureAllowed = true;
                Stroke(scenario, Keys.F9, true, "shortcut after rejected capture");
                Pump();
                Expect(scenario.Triggers == 1, "rejected capture does not leave the service blocked");
            }

            using (Scenario scenario = new Scenario())
            {
                scenario.Service.BeginCapture();
                Stroke(scenario, Keys.F10, true, "capture before app switch");
                scenario.Foreground = false;
                Pump();
                Expect(scenario.Captures == 0, "queued capture is discarded after app switch");
                scenario.Foreground = true;
                Stroke(scenario, Keys.F9, true, "shortcut after rejected foreground capture");
                Pump();
                Expect(scenario.Triggers == 1, "foreground rejection clears pending capture state");
            }

            using (Scenario scenario = new Scenario())
            {
                scenario.Service.BeginCapture();
                Stroke(scenario, Keys.F10, true, "old capture before restarting");
                scenario.Service.BeginCapture();
                Stroke(scenario, Keys.F11, true, "new capture after restarting", Keys.Control);
                Pump();
                Expect(scenario.Captures == 1 &&
                    scenario.LastCapture.Equals(new HotkeyBinding(Keys.F11, Keys.Control)),
                    "stale capture messages cannot erase or apply a newer capture");
            }

            using (Scenario scenario = new Scenario())
            {
                Stroke(scenario, Keys.F9, true, "trigger before starting capture");
                scenario.Service.BeginCapture();
                Pump();
                Expect(scenario.Triggers == 0, "starting capture invalidates older page actions");
                scenario.Service.CancelCapture();
            }

            using (Scenario scenario = new Scenario())
            {
                Stroke(scenario, Keys.F9, true, "queued shortcut before dispose");
                scenario.Service.Dispose();
                Pump();
                Expect(scenario.Triggers == 0, "disposed service does not invoke queued page actions");
                Stroke(scenario, Keys.F9, false, "disposed shortcut");
                scenario.Service.BeginCapture();
                Expect(!scenario.Service.IsCapturing, "disposed service cannot start recording");
            }

            using (Scenario scenario = new Scenario())
            {
                scenario.Service.BeginCapture();
                Stroke(scenario, Keys.F10, true, "queued capture before dispose");
                scenario.Service.Dispose();
                Pump();
                Expect(scenario.Captures == 0, "disposed service does not report queued capture");
            }

            using (Scenario scenario = new Scenario())
            {
                scenario.Service.Triggered += delegate { throw new InvalidOperationException("handler test"); };
                Stroke(scenario, Keys.F9, true, "throwing page handler");
                Pump();
                Stroke(scenario, Keys.F9, true, "page handler after an exception");
                Pump();
                Expect(scenario.Triggers == 2, "handler exceptions do not break later shortcuts");
                scenario.Service.Captured += delegate { throw new InvalidOperationException("capture handler test"); };
                scenario.Service.BeginCapture();
                Stroke(scenario, Keys.F10, true, "throwing capture handler");
                Pump();
                Expect(scenario.Captures == 1, "capture handler exceptions do not escape the message loop");
            }

            Console.WriteLine("HotkeyService: " + checkedCases + " cases passed.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }
}
