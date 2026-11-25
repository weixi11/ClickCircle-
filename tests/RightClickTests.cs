using System;
using ClickCircle;

// 右键路由的回归：取字与连续圈注各自完成一步，区域识别与暂停不动右键菜单。
internal static class RightClickTests
{
    private static int checkedCases;

    private static void Expect(bool condition, string description)
    {
        if (!condition)
            throw new Exception("Failed: " + description);
        ++checkedCases;
    }

    private static void ExpectAction(RightClickAction expected, bool picking, bool ocrPicking,
        bool stamping, string description)
    {
        RightClickAction actual = RightClickRouting.For(picking, ocrPicking, stamping);
        Expect(actual == expected, description + ": expected " + expected + ", got " + actual);
    }

    private static void CheckContextMenuGuard()
    {
        DateTime now = new DateTime(2026, 11, 25, 11, 0, 0, DateTimeKind.Utc);
        DateTime blockedUntil = ContextMenuGuard.BlockFrom(now);
        Expect(blockedUntil == now.AddMilliseconds(ContextMenuGuard.BlockMilliseconds),
            "右键按下开始一段吞掉窗口");
        Expect(ContextMenuGuard.ShouldSwallow(ContextMenuGuard.MessageRightButtonUp, now, blockedUntil),
            "窗口内的右键抬起不交给编辑器");
        Expect(ContextMenuGuard.ShouldSwallow(ContextMenuGuard.MessageContextMenu,
            now.AddMilliseconds(100), blockedUntil), "窗口内的右键菜单请求被吃掉");
        Expect(!ContextMenuGuard.ShouldSwallow(ContextMenuGuard.MessageRightButtonDown, now, blockedUntil),
            "右键按下本身要处理，不算菜单消息");
        Expect(!ContextMenuGuard.ShouldSwallow(0x0201, now, blockedUntil),
            "左键按下不受右键吞掉窗口影响");
        Expect(!ContextMenuGuard.ShouldSwallow(ContextMenuGuard.MessageContextMenu,
            blockedUntil, blockedUntil), "窗口结束后不再吞菜单");
        Expect(!ContextMenuGuard.ShouldSwallow(ContextMenuGuard.MessageContextMenu, now, DateTime.MinValue),
            "没有开始吞掉窗口时不拦截");
        Expect(ContextMenuGuard.IsMenuMessage(ContextMenuGuard.MessageRightButtonUp) &&
            ContextMenuGuard.IsMenuMessage(ContextMenuGuard.MessageContextMenu) &&
            !ContextMenuGuard.IsMenuMessage(ContextMenuGuard.MessageRightButtonDown),
            "菜单消息只包含抬起与菜单请求");

        DateTime restoreAt = ContextMenuGuard.RestoreFrom(now);
        Expect(restoreAt == now.AddMilliseconds(ContextMenuGuard.RestoreDelayMilliseconds),
            "完成标注后按固定延迟交回工具");
        Expect(ContextMenuGuard.RestoreDelayMilliseconds < ContextMenuGuard.BlockMilliseconds,
            "交回工具的延迟必须短于吞掉窗口，否则工具已走、菜单仍会弹出");
        Expect(!ContextMenuGuard.ShouldRestore(now, restoreAt), "延迟未到不交回工具");
        Expect(ContextMenuGuard.ShouldRestore(restoreAt, restoreAt), "到期立刻交回工具");
        Expect(!ContextMenuGuard.ShouldRestore(now, DateTime.MinValue), "没有待办时不做任何事");
    }

    [STAThread]
    public static int Main()
    {
        try
        {
            ExpectAction(RightClickAction.FinishPicking, true, false, false,
                "取字状态右键完成取字");
            ExpectAction(RightClickAction.FinishStamping, false, false, true,
                "连续圈注状态右键完成标注");
            ExpectAction(RightClickAction.None, false, true, false,
                "区域识别等待框选不拦截右键");
            ExpectAction(RightClickAction.None, true, true, false,
                "区域识别同时处于取字时不完成取字");
            ExpectAction(RightClickAction.None, false, false, false,
                "暂停状态交回编辑器右键菜单");
            ExpectAction(RightClickAction.FinishPicking, true, false, true,
                "取字与圈注标志同时为真时取字优先，避免丢掉已取片段");
            ExpectAction(RightClickAction.FinishPicking, true, false, false,
                "重复判定结果稳定");
            Expect(RightClickAction.FinishPicking != RightClickAction.FinishStamping,
                "两次右键的动作必须不同");
            CheckContextMenuGuard();
            Console.WriteLine("RightClick: " + checkedCases + " cases passed.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }
}
