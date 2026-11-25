using System;

namespace ClickCircle
{
    /// <summary>右键在页面上的含义：按当前状态决定完成哪一步，还是交回编辑器的右键菜单。</summary>
    public enum RightClickAction
    {
        /// <summary>不处理，让编辑器自己弹右键菜单。</summary>
        None,

        /// <summary>完成取字：保留累积片段，进入连续圈注。</summary>
        FinishPicking,

        /// <summary>完成标注：结束本次连续圈注，主题保留。</summary>
        FinishStamping,
    }

    /// <summary>
    /// 右键路由。取字状态（不含区域识别）右键完成取字；连续圈注状态右键完成标注；
    /// 区域识别等待框选用右键不拦截，交给编辑器；其余状态一律不处理。
    /// </summary>
    public static class RightClickRouting
    {
        public static RightClickAction For(bool picking, bool ocrPicking, bool stamping)
        {
            if (picking && !ocrPicking) return RightClickAction.FinishPicking;
            if (stamping) return RightClickAction.FinishStamping;
            return RightClickAction.None;
        }
    }

    /// <summary>
    /// 右键菜单吞掉窗口。取字/圈注中右键完成时，编辑器会紧接着处理这次右键的
    /// WM_RBUTTONUP 与 WM_CONTEXTMENU 并弹出页面右键菜单；页面工具要先吃掉这两条消息。
    /// 完成标注会立刻把工具交回编辑器，因此插件把“交回工具”推迟到吞掉窗口之后；
    /// 延迟必须小于吞掉窗口，否则工具已经不在，右键菜单还是会弹出来。
    /// </summary>
    public static class ContextMenuGuard
    {
        /// <summary>WM_RBUTTONDOWN：右键按下，插件在这里决定完成取字还是完成标注。</summary>
        public const int MessageRightButtonDown = 0x0204;

        /// <summary>WM_RBUTTONUP：右键抬起，编辑器通常在这时准备右键菜单。</summary>
        public const int MessageRightButtonUp = 0x0205;

        /// <summary>WM_CONTEXTMENU：右键菜单消息。</summary>
        public const int MessageContextMenu = 0x007B;

        /// <summary>右键完成后继续吞掉菜单消息的时间，覆盖同一轮右键的抬起与菜单请求。</summary>
        public const int BlockMilliseconds = 750;

        /// <summary>完成标注后推迟交回工具的时间；必须短于 <see cref="BlockMilliseconds"/>。</summary>
        public const int RestoreDelayMilliseconds = 300;

        /// <summary>这条页面消息是否属于右键菜单（抬起或菜单请求）。</summary>
        public static bool IsMenuMessage(int code)
        {
            return code == MessageRightButtonUp || code == MessageContextMenu;
        }

        /// <summary>当前时间是否仍在吞掉窗口内；窗口内的菜单消息不交给编辑器。</summary>
        public static bool ShouldSwallow(int code, DateTime nowUtc, DateTime blockedUntilUtc)
        {
            return IsMenuMessage(code) && blockedUntilUtc != DateTime.MinValue && nowUtc < blockedUntilUtc;
        }

        /// <summary>右键按下时开始新一轮吞掉窗口，返回窗口结束时间。</summary>
        public static DateTime BlockFrom(DateTime nowUtc)
        {
            return nowUtc.AddMilliseconds(BlockMilliseconds);
        }

        /// <summary>完成标注后安排交回工具的时间，返回该时间点。</summary>
        public static DateTime RestoreFrom(DateTime nowUtc)
        {
            return nowUtc.AddMilliseconds(RestoreDelayMilliseconds);
        }

        /// <summary>到达安排的时间点时应当交回工具；MinValue 表示没有待办。</summary>
        public static bool ShouldRestore(DateTime nowUtc, DateTime restoreAtUtc)
        {
            return restoreAtUtc != DateTime.MinValue && nowUtc >= restoreAtUtc;
        }
    }
}
