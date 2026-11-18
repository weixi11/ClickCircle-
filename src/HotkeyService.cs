using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ClickCircle
{
    // The hook only consumes keys after the foreground and input-focus gates pass.
    internal sealed class HotkeyService : IDisposable
    {
        private const int WhKeyboardLl = 13;
        private const int WmKeyDown = 0x0100;
        private const int WmKeyUp = 0x0101;
        private const int WmSysKeyDown = 0x0104;
        private const int WmSysKeyUp = 0x0105;
        private const int WmTrigger = 0x8000 + 0x0C11;
        private const int WmCapture = 0x8000 + 0x0C12;
        private const int HwndMessage = -3;

        private readonly LowLevelKeyboardProc keyboardCallback;
        private readonly TriggerWindow window;
        private readonly Func<bool> canTrigger;
        private readonly Func<bool> canCapture;
        private readonly Func<bool> isOwnProcessForeground;
        private readonly HashSet<int> pressedKeys = new HashSet<int>();
        private readonly HashSet<int> consumedKeys = new HashSet<int>();
        private HotkeyBinding binding = HotkeyBinding.Empty;
        private IntPtr hook;
        private int bindingVersion;
        private int captureVersion;
        private bool capturing;
        private bool capturePending;
        private HotkeyBinding pendingCapture;
        private bool disposed;

        public HotkeyService(Func<bool> canTrigger, Func<bool> canCapture)
            : this(canTrigger, canCapture, IsOwnProcessForeground, true)
        {
        }

        // Tests exercise the same routing and message queue without installing a system hook.
        internal HotkeyService(Func<bool> canTrigger, Func<bool> canCapture,
            Func<bool> isOwnProcessForeground, bool installHook)
        {
            this.canTrigger = canTrigger;
            this.canCapture = canCapture;
            this.isOwnProcessForeground = isOwnProcessForeground;
            keyboardCallback = OnKeyboardMessage;
            window = new TriggerWindow(this);
            if (installHook)
            {
                hook = SetWindowsHookEx(WhKeyboardLl, keyboardCallback, GetModuleHandle(null), 0);
                if (hook == IntPtr.Zero)
                    Error = new Win32Exception(Marshal.GetLastWin32Error()).Message;
            }
        }

        public event Action Triggered;
        public event Action<HotkeyBinding> Captured;

        public bool IsCapturing { get { return capturing; } }
        public string Error { get; private set; }
        public bool IsActive { get { return hook != IntPtr.Zero; } }
        public bool IsHooked { get { return hook != IntPtr.Zero; } }

        public HotkeyBinding Binding
        {
            get { return binding; }
            set
            {
                binding = value ?? HotkeyBinding.Empty;
                ++bindingVersion;
            }
        }

        public void BeginCapture()
        {
            if (disposed)
                return;
            ++captureVersion;
            ++bindingVersion;
            pendingCapture = null;
            capturePending = false;
            capturing = true;
        }

        public void CancelCapture()
        {
            ++captureVersion;
            capturing = false;
            capturePending = false;
            pendingCapture = null;
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            Binding = HotkeyBinding.Empty;
            CancelCapture();
            if (hook != IntPtr.Zero)
            {
                UnhookWindowsHookEx(hook);
                hook = IntPtr.Zero;
            }
            pressedKeys.Clear();
            consumedKeys.Clear();
            window.DestroyHandle();
        }

        private bool IsAllowed(Func<bool> scope)
        {
            if (disposed || scope == null || isOwnProcessForeground == null)
                return false;
            try
            {
                return isOwnProcessForeground() && scope();
            }
            catch (Exception)
            {
                return false;
            }
        }

        private void RaiseTriggered(int version)
        {
            // Focus or settings can change between the hook callback and message dispatch.
            if (version != bindingVersion || binding.IsEmpty || capturing || capturePending ||
                !IsAllowed(canTrigger))
                return;
            Action handler = Triggered;
            if (handler == null)
                return;
            try { handler(); }
            catch (Exception) { }
        }

        private void RaiseCaptured(int version)
        {
            if (version != captureVersion || !capturePending)
                return;
            HotkeyBinding value = pendingCapture;
            pendingCapture = null;
            capturePending = false;
            if (!IsAllowed(canCapture))
                return;
            Action<HotkeyBinding> handler = Captured;
            if (handler == null)
                return;
            try { handler(value); }
            catch (Exception) { }
        }

        private IntPtr OnKeyboardMessage(int code, IntPtr message, IntPtr data)
        {
            try
            {
                if (code >= 0 && !disposed)
                {
                    int id = message.ToInt32();
                    bool down = id == WmKeyDown || id == WmSysKeyDown;
                    bool up = id == WmKeyUp || id == WmSysKeyUp;
                    if (down || up)
                    {
                        KbdLlHookStruct info = (KbdLlHookStruct)Marshal.PtrToStructure(
                            data, typeof(KbdLlHookStruct));
                        if (ProcessKey((int)info.vkCode, down, CurrentModifiers(),
                            IsDown(0x5B) || IsDown(0x5C)))
                            return new IntPtr(1);
                    }
                }
            }
            catch (Exception) { }
            return CallNextHookEx(hook, code, message, data);
        }

        internal bool ProcessKey(int virtualKey, bool down, Keys modifiers, bool windowsKeyDown)
        {
            if (disposed)
                return false;
            if (!down)
            {
                pressedKeys.Remove(virtualKey);
                return consumedKeys.Remove(virtualKey);
            }

            bool firstDown = pressedKeys.Add(virtualKey);
            if (consumedKeys.Contains(virtualKey))
                return true;
            if (!firstDown || windowsKeyDown || HotkeyBinding.IsModifierKey((Keys)virtualKey))
                return false;

            if (capturing)
            {
                if (!IsAllowed(canCapture))
                    return false;
                Keys key = (Keys)virtualKey;
                if (!Enum.IsDefined(typeof(Keys), key))
                    return false;
                HotkeyBinding value;
                if (key == Keys.Escape)
                    value = null;
                else if (key == Keys.Back || key == Keys.Delete)
                    value = HotkeyBinding.Empty;
                else
                {
                    value = new HotkeyBinding(key, modifiers);
                    if (value.IsEmpty)
                        return false;
                }
                pendingCapture = value;
                capturePending = true;
                if (!window.Post(WmCapture, captureVersion))
                {
                    pendingCapture = null;
                    capturePending = false;
                    return false;
                }
                capturing = false;
                consumedKeys.Add(virtualKey);
                return true;
            }

            if (capturePending || binding.IsEmpty || virtualKey != (int)binding.Key ||
                modifiers != binding.Modifiers || !IsAllowed(canTrigger))
                return false;
            if (!window.Post(WmTrigger, bindingVersion))
                return false;
            consumedKeys.Add(virtualKey);
            return true;
        }

        private static Keys CurrentModifiers()
        {
            Keys modifiers = Keys.None;
            if (IsDown(0x11)) modifiers |= Keys.Control;
            if (IsDown(0x12)) modifiers |= Keys.Alt;
            if (IsDown(0x10)) modifiers |= Keys.Shift;
            return modifiers;
        }

        private static bool IsDown(int virtualKey)
        {
            return (GetAsyncKeyState(virtualKey) & 0x8000) != 0;
        }

        private static bool IsOwnProcessForeground()
        {
            IntPtr foreground = GetForegroundWindow();
            if (foreground == IntPtr.Zero)
                return false;
            uint processId;
            GetWindowThreadProcessId(foreground, out processId);
            return processId == GetCurrentProcessId();
        }

        private delegate IntPtr LowLevelKeyboardProc(int code, IntPtr message, IntPtr data);

        [StructLayout(LayoutKind.Sequential)]
        private struct KbdLlHookStruct
        {
            public uint vkCode;
            public uint scanCode;
            public uint flags;
            public uint time;
            public IntPtr extraInfo;
        }

        private sealed class TriggerWindow : NativeWindow
        {
            private readonly HotkeyService owner;

            public TriggerWindow(HotkeyService service)
            {
                owner = service;
                CreateHandle(new CreateParams {
                    Caption = "ClickCircleHotkey",
                    Parent = new IntPtr(HwndMessage),
                    Style = 0,
                    ExStyle = 0
                });
            }

            public bool Post(int message, int version)
            {
                return Handle != IntPtr.Zero &&
                    PostMessage(Handle, message, new IntPtr(version), IntPtr.Zero);
            }

            protected override void WndProc(ref Message message)
            {
                if (message.Msg == WmTrigger)
                {
                    owner.RaiseTriggered(message.WParam.ToInt32());
                    return;
                }
                if (message.Msg == WmCapture)
                {
                    owner.RaiseCaptured(message.WParam.ToInt32());
                    return;
                }
                base.WndProc(ref message);
            }
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc callback,
            IntPtr module, uint thread);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnhookWindowsHookEx(IntPtr hook);
        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr message, IntPtr data);
        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int virtualKey);
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool PostMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string moduleName);
        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentProcessId();
    }
}
