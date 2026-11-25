using System;
using ClickCircle;

// 左键松开手势的回归：圈注时按住左键稍微拖动过，松开也必须放圈；取字的拖动仍是框选。
internal static class LeftReleaseTests
{
    private static int checkedCases;

    private static void Expect(bool condition, string description)
    {
        if (!condition)
            throw new Exception("Failed: " + description);
        ++checkedCases;
    }

    private static void ExpectAction(LeftReleaseAction expected, bool picking, bool dragged,
        bool releaseInsidePage, string description)
    {
        LeftReleaseAction actual = LeftReleaseRouting.For(picking, dragged, releaseInsidePage);
        Expect(actual == expected, description + ": expected " + expected + ", got " + actual);
    }

    [STAThread]
    public static int Main()
    {
        try
        {
            ExpectAction(LeftReleaseAction.Click, false, false, true,
                "圈注状态原地单击放圈");
            ExpectAction(LeftReleaseAction.Click, false, true, true,
                "圈注状态按住左键拖动过，松开照样放圈（本次修复）");
            ExpectAction(LeftReleaseAction.None, false, true, false,
                "圈注状态拖动后松开点不在页内不落圈");
            ExpectAction(LeftReleaseAction.None, false, false, false,
                "圈注状态原地单击在页外不落圈");

            ExpectAction(LeftReleaseAction.BoxSelect, true, true, true,
                "取字状态拖动是框选");
            ExpectAction(LeftReleaseAction.BoxSelect, true, true, false,
                "取字状态拖动松开在页外也按框选处理（沿用原逻辑）");
            ExpectAction(LeftReleaseAction.Click, true, false, true,
                "取字状态原地单击取文字");
            ExpectAction(LeftReleaseAction.None, true, false, false,
                "取字状态原地单击在页外不处理");

            Expect(LeftReleaseRouting.For(false, true, true) == LeftReleaseRouting.For(false, false, true),
                "圈注状态拖动与不拖动的结果一致");
            Expect(LeftReleaseRouting.For(true, true, true) != LeftReleaseRouting.For(false, true, true),
                "取字拖动与圈注拖动的动作必须不同");

            Console.WriteLine("LeftRelease: " + checkedCases + " cases passed.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }
}
