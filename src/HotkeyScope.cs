using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using PDFXEdit;

namespace ClickCircle
{
    internal static class HotkeyScope
    {
        private const uint MenuModeFlags = 0x1C;

        internal static bool CanTrigger(IPXV_Inst instance, StampPanel panel)
        {
            try
            {
                if (instance == null)
                    return false;

                IUIX_Inst ui = instance.GetExtension("UIX") as IUIX_Inst;
                if (ui == null || ui.IsModalDisplayed() || ui.IsAppModalDisplayed() ||
                    ui.IsPopupDisplayed())
                    return false;

                GuiThreadInfo nativeFocus;
                uint nativeThread;
                if (!TryGetFocus(out nativeFocus, out nativeThread) ||
                    nativeFocus.Caret != IntPtr.Zero ||
                    IsNativeTextInput(nativeFocus.Focus))
                    return false;

                IPXV_PagesView view = instance.ActivePagesView;
                IPXV_Document document = instance.ActiveDoc;
                if (view == null || document == null || view.Doc == null ||
                    !SameComObject(document, view.Doc) || document.ActiveTextEditor != null)
                    return false;

                if (PanelHasFocus(panel, nativeFocus))
                {
                    Control focused = Control.FromHandle(nativeFocus.Focus);
                    return !panel.IsTextInputFocused() && focused is ButtonBase;
                }

                IUIX_Obj pageObject = view.Obj;
                if (pageObject == null || pageObject.IsInvalid || !pageObject.Visible ||
                    !pageObject.Enabled || !pageObject.HasInputFocus)
                    return false;

                IUIX_ThreadContext context = ui.get_CurrentThreadCtx();
                if (context == null || unchecked((uint)context.ThreadID) != nativeThread ||
                    !SameComObject(pageObject, context.InputFocus) ||
                    ui.IsObjOnPopup(pageObject) || ui.IsObjOnPopupMenu(pageObject))
                    return false;

                // Windowless child editors can share a window with the page canvas.
                ulong pageWindow = pageObject.GetFirstWndHandle();
                return pageWindow != 0 &&
                    nativeFocus.Focus == new IntPtr(unchecked((long)pageWindow));
            }
            catch (Exception)
            {
                return false;
            }
        }

        internal static bool CanCapture(StampPanel panel)
        {
            try
            {
                GuiThreadInfo nativeFocus;
                uint nativeThread;
                return TryGetFocus(out nativeFocus, out nativeThread) &&
                    PanelHasFocus(panel, nativeFocus);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool PanelHasFocus(StampPanel panel, GuiThreadInfo nativeFocus)
        {
            if (panel == null || panel.IsDisposed || !panel.IsHandleCreated ||
                !panel.Visible || !panel.Enabled || !panel.ContainsFocus)
                return false;

            IntPtr panelWindow = panel.Handle;
            return nativeFocus.Focus == panelWindow ||
                IsChild(panelWindow, nativeFocus.Focus);
        }

        private static bool TryGetFocus(out GuiThreadInfo focus, out uint thread)
        {
            focus = new GuiThreadInfo();
            focus.Size = (uint)Marshal.SizeOf(typeof(GuiThreadInfo));
            thread = 0;
            IntPtr foreground = GetForegroundWindow();
            if (foreground == IntPtr.Zero)
                return false;

            uint process;
            thread = GetWindowThreadProcessId(foreground, out process);
            return thread != 0 && GetGUIThreadInfo(thread, ref focus) &&
                focus.Focus != IntPtr.Zero && (focus.Flags & MenuModeFlags) == 0;
        }

        private static bool IsNativeTextInput(IntPtr window)
        {
            StringBuilder name = new StringBuilder(256);
            if (GetClassName(window, name, name.Capacity) == 0)
                return true;

            string className = name.ToString();
            return className.Equals("Edit", StringComparison.OrdinalIgnoreCase) ||
                className.StartsWith("RichEdit", StringComparison.OrdinalIgnoreCase) ||
                className.IndexOf(".EDIT.", StringComparison.OrdinalIgnoreCase) >= 0 ||
                className.IndexOf(".RichEdit", StringComparison.OrdinalIgnoreCase) >= 0 ||
                className.Equals("Scintilla", StringComparison.OrdinalIgnoreCase);
        }

        private static bool SameComObject(object first, object second)
        {
            if (first == null || second == null)
                return false;
            if (Object.ReferenceEquals(first, second))
                return true;

            IntPtr firstIdentity = IntPtr.Zero;
            IntPtr secondIdentity = IntPtr.Zero;
            try
            {
                firstIdentity = Marshal.GetIUnknownForObject(first);
                secondIdentity = Marshal.GetIUnknownForObject(second);
                return firstIdentity == secondIdentity;
            }
            finally
            {
                if (secondIdentity != IntPtr.Zero)
                    Marshal.Release(secondIdentity);
                if (firstIdentity != IntPtr.Zero)
                    Marshal.Release(firstIdentity);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct GuiThreadInfo
        {
            internal uint Size;
            internal uint Flags;
            internal IntPtr Active;
            internal IntPtr Focus;
            internal IntPtr Capture;
            internal IntPtr MenuOwner;
            internal IntPtr MoveSize;
            internal IntPtr Caret;
            internal NativeRect CaretRect;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect
        {
            internal int Left;
            internal int Top;
            internal int Right;
            internal int Bottom;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetGUIThreadInfo(uint thread, ref GuiThreadInfo info);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsChild(IntPtr parent, IntPtr child);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int GetClassName(IntPtr window, StringBuilder name, int length);
    }
}
