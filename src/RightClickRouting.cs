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
}
