using System;
using ClickCircle;
using PDFXEdit;

// 光标映射的回归：取字与标注必须是两个不同光标，其余状态不能退回箭头。
internal static class CursorTests
{
    private static int checkedCases;

    private static void Expect(bool condition, string description)
    {
        if (!condition)
            throw new Exception("Failed: " + description);
        ++checkedCases;
    }

    [STAThread]
    public static int Main()
    {
        try
        {
            Expect(ModeCursor.Picking != ModeCursor.Stamping,
                "取字与标注使用不同的光标");
            Expect(ModeCursor.Picking != ModeCursor.Region,
                "取字与区域框选使用不同的光标");
            Expect(ModeCursor.Picking == (int)UIX_StdCursor.UIX_StdCursor_IBeam,
                "取字使用文本光标 IBeam");
            Expect(ModeCursor.Stamping == (int)UIX_StdCursor.UIX_StdCursor_Cross,
                "标注使用十字光标 Cross");
            Expect(ModeCursor.Region == (int)UIX_StdCursor.UIX_StdCursor_Cross,
                "区域识别等待拖框时使用十字光标");
            Expect(ModeCursor.Dragging == (int)UIX_StdCursor.UIX_StdCursor_Cross,
                "拖框过程中使用十字光标");
            Expect(ModeCursor.Arrow == (int)UIX_StdCursor.UIX_StdCursor_Arrow,
                "非取字非标注状态交回普通箭头");
            Expect(ModeCursor.Picking != ModeCursor.Arrow && ModeCursor.Stamping != ModeCursor.Arrow,
                "取字与标注都不使用箭头");
            Console.WriteLine("Cursor: " + checkedCases + " cases passed.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }
}
