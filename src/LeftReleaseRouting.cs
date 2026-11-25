namespace ClickCircle
{
    /// <summary>左键松开时页面工具要执行的动作。</summary>
    public enum LeftReleaseAction
    {
        /// <summary>不处理（例如松开点不在页面内）。</summary>
        None,

        /// <summary>当作一次点击：取字状态取文字，圈注状态放圈。</summary>
        Click,

        /// <summary>取字状态拖出了矩形：框选取字。</summary>
        BoxSelect,
    }

    /// <summary>
    /// 左键手势路由。取字状态的拖动是框选；其余状态（尤其是连续圈注）即使按住左键时
    /// 拖动了鼠标，松开也按一次点击处理——原来拖动会被当成框选以外的无效手势丢掉，
    /// 用户手一抖就一个圈也放不出来。
    /// </summary>
    public static class LeftReleaseRouting
    {
        public static LeftReleaseAction For(bool picking, bool dragged, bool releaseInsidePage)
        {
            if (picking && dragged) return LeftReleaseAction.BoxSelect;
            return releaseInsidePage ? LeftReleaseAction.Click : LeftReleaseAction.None;
        }
    }
}
