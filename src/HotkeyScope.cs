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
            string reason;
            return CanTrigger(instance, panel, false, out reason);
        }

        /// <summary>
        /// 判定是否响应“取文字”快捷键。toolActive 表示插件的页面工具正作用在当前文档上：
        /// 这种状态下页面视图即使暂时没有输入焦点（例如刚点过面板、弹过提示窗），
        /// 只要键盘焦点仍在本编辑器进程内且不在文本输入框里，就应当响应。
        /// reason 在返回 false 时给出具体原因，便于写日志定位。
        /// </summary>
        internal static bool CanTrigger(IPXV_Inst instance, StampPanel panel, bool toolActive,
            out string reason)
        {
            reason = null;
            try
            {
                if (instance == null)
                {
                    reason = "插件尚未初始化";
                    return false;
                }

                IUIX_Inst ui = instance.GetExtension("UIX") as IUIX_Inst;
                if (ui == null)
                {
                    reason = "无法获取 UI 扩展";
                    return false;
                }
                if (ui.IsModalDisplayed() || ui.IsAppModalDisplayed() || ui.IsPopupDisplayed())
                {
                    reason = "编辑器有模态框或弹出菜单";
                    return false;
                }

                GuiThreadInfo nativeFocus;
                uint nativeThread;
                if (!TryGetFocus(out nativeFocus, out nativeThread))
                {
                    reason = "取不到键盘焦点";
                    return false;
                }
                if (nativeFocus.Caret != IntPtr.Zero)
                {
                    reason = "光标在文本编辑区";
                    return false;
                }
                if (IsNativeTextInput(nativeFocus.Focus))
                {
                    reason = "焦点在原生输入框";
                    return false;
                }

                IPXV_PagesView view = instance.ActivePagesView;
                IPXV_Document document = instance.ActiveDoc;
                if (view == null || document == null || view.Doc == null ||
                    !SameComObject(document, view.Doc))
                {
                    reason = "当前没有打开的文档视图";
                    return false;
                }
                if (document.ActiveTextEditor != null)
                {
                    reason = "正在编辑标注文字";
                    return false;
                }

                // 面板输入框里敲 Q 不应触发取字；其余面板控件（按钮、历史列表）可以。
                if (PanelHasFocus(panel, nativeFocus) && panel != null && panel.IsTextInputFocused())
                {
                    reason = "焦点在面板输入框";
                    return false;
                }

                // 插件工具正在生效：焦点仍在本进程内即可响应，不再要求页面视图此刻持有输入焦点。
                if (toolActive && IsOwnProcessWindow(nativeFocus.Focus))
                    return true;

                // 兜底：编辑器是前台、焦点在编辑器进程内、且不是文本输入/标注编辑区时，
                // Q 就应当触发取字。页面视图是否持有输入焦点由编辑器内部决定，
                // 不能作为必要条件，否则会出现“按了没反应”。
                if (IsOwnProcessWindow(nativeFocus.Focus))
                    return true;

                reason = "焦点不在编辑器内";
                return false;
            }
            catch (Exception exception)
            {
                reason = "判定异常：" + exception.Message;
                return false;
            }
        }

        /// <summary>键盘焦点窗口是否属于本进程（编辑器或插件自己的窗口）。</summary>
        private static bool IsOwnProcessWindow(IntPtr window)
        {
            if (window == IntPtr.Zero)
                return false;
            uint process;
            GetWindowThreadProcessId(window, out process);
            return process == GetCurrentProcessId();
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

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentProcessId();

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
