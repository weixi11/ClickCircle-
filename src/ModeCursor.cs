using PDFXEdit;

namespace ClickCircle
{
    /// <summary>
    /// 页面工具在各状态下的鼠标光标。取字是“点一行字”，用文本光标（IBeam）；
    /// 拖框与放椭圆是“定一个点、拉一个范围”，用十字光标（Cross）。
    /// 光标必须在编辑器自己的页面视图里设置，因此这里只放常量，由 CircleTool 写入 view.Cursor。
    /// </summary>
    public static class ModeCursor
    {
        /// <summary>不在取字/标注状态时交回编辑器的普通箭头。</summary>
        public const int Arrow = (int)UIX_StdCursor.UIX_StdCursor_Arrow;

        /// <summary>取字：等待单击一段文字。</summary>
        public const int Picking = (int)UIX_StdCursor.UIX_StdCursor_IBeam;

        /// <summary>按住左键拖框：取字框选与区域识别共用。</summary>
        public const int Dragging = (int)UIX_StdCursor.UIX_StdCursor_Cross;

        /// <summary>区域识别：等待拖出识别区域。</summary>
        public const int Region = (int)UIX_StdCursor.UIX_StdCursor_Cross;

        /// <summary>连续圈注：等待单击放置椭圆。</summary>
        public const int Stamping = (int)UIX_StdCursor.UIX_StdCursor_Cross;
    }
}
