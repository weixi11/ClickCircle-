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
