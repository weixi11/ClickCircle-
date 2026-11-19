using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using PDFXEdit;

namespace ClickCircle
{
    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.None)]
    public sealed class CircleTool : IPXV_Tool, IPXV_PagesViewDrawCallback
    {
        private readonly Plugin owner;
        private bool pressed;
        private bool dragged;
        private bool pickingGesture;
        private bool captureOwned;
        private bool drawRegistered;
        private bool drawingFailed;
        private tagPOINT pressPoint;
        private IPXV_PagesView gestureView;
        private uint gestureDocument;
        private uint gesturePage;
        private uint gestureCurrentPage;
        private int gestureRotation;
        private int gesturePageRotation;
        private PXC_Matrix gestureMatrix;
        private tagPOINT gestureScroll;
        private tagRECT gesturePageRect;
        private tagRECT gestureClientRect;
        private tagRECT selectionRect;
        private tagRECT selectionDirtyRect;
        private List<RegionGlyph> gestureGlyphs;
        private RegionPickResult selection;
        private IUIX_Brush selectionBrush;
        private IUIX_Pen selectionPen;
        private DateTime contextMenuBlockedUntilUtc;

        public CircleTool(Plugin owner)
        {
            this.owner = owner;
            ID = owner.Instance.Str2ID("ClickCircle.PageTool", true);
        }

        public int ID { get; private set; }
        public string Name { get { return "\u53d6\u5b57\u8fde\u7eed\u5708\u6ce8"; } }
        public string Description { get { return Name; } }
        public uint Features { get { return 0; } }

        public bool get_IsEnabled(IPXV_PagesView view)
        {
            return view != null && view.Doc != null;
        }

        public bool get_IsExclusive(IPXV_Document document) { return true; }
        public bool CanActivateForDocument(IPXV_Document document) { return document != null; }
        public bool get_IsGuidelinesAllowed(IPXV_PagesView view, ref tagPOINT position) { return false; }

        public void OnActivated(IPXV_Document document, uint flags)
        {
            CancelGesture();
            owner.OnToolActivated(document);
        }

        public void OnDeactivated(IPXV_Document document, uint flags)
        {
            CancelGesture();
            owner.OnToolDeactivated(document);
        }

        public void OnFinalize() { CancelGesture(); }

        /// <summary>模式切换后立刻同步光标，不必等用户先移动鼠标。</summary>
        public void ApplyModeCursor(IPXV_PagesView view)
        {
            if (view == null) return;
            view.Cursor = pressed ? ModeCursor.Dragging : owner.CurrentCursorId;
        }

        public void OnProcessViewEvent(IPXV_PagesView view, IUIX_Event e)
        {
            try
            {
                if ((e.Code == 0x0100 || e.Code == 0x0104) && (e.Param1 & 0xffff) == 27)
                {
                    CancelGesture();
                    e.Handled = true;
                    owner.Pause();
                    return;
                }
                // 取字时右键完成：保留已取片段，切到连续圈注；并短暂吞掉这次右键菜单。
                if ((e.Code == 0x0205 || e.Code == 0x007B) &&
                    DateTime.UtcNow < contextMenuBlockedUntilUtc)
                {
                    e.Handled = true;
                    return;
                }
                if (e.Code == 0x0204)
                {
                    RightClickAction action = RightClickRouting.For(
                        owner.IsPicking, owner.IsOcrPicking, owner.IsStamping);
                    if (action != RightClickAction.None)
                    {
                        e.Handled = true;
                        contextMenuBlockedUntilUtc = DateTime.UtcNow.AddMilliseconds(750);
                        CancelGesture();
                        if (action == RightClickAction.FinishPicking) owner.FinishPicking();
                        else owner.FinishStamping();
                        return;
                    }
                }
                if (e.Code == (int)UIX_EventCodes.e_MouseCaptureLost || e.Code == 0x0215 ||
                    e.Code == (int)UIX_EventCodes.e_ScrollChanged)
                {
                    CancelGesture();
                    return;
                }

                if (pressed && !MatchesGestureView(view))
                    CancelGesture();

                if (e.Code == 0x0201)
                {
                    e.Handled = true;
                    BeginGesture(view, e.get_Pos());
                }
                else if (e.Code == 0x0200)
                {
                    view.Cursor = pressed ? ModeCursor.Dragging : owner.CurrentCursorId;
                    e.Handled = true;
                    if (pressed)
                        UpdateGesture(e.get_Pos());
                }
                else if (e.Code == 0x0203)
                {
                    CancelGesture();
                    e.Handled = true;
                }
                else if (e.Code == 0x0202)
                {
                    e.Handled = true;
                    CompleteGesture(view, e.get_Pos());
                }
            }
            catch (Exception exception)
            {
                owner.Log("Gesture FAILED " + exception);
                CancelGesture();
            }
        }

        private void BeginGesture(IPXV_PagesView view, tagPOINT point)
        {
            CancelGesture();
            IPXV_PagesLayoutManager layout = view.Layout;
            int pageIndex = layout.GetNearestPage(ref point, false);
            if (pageIndex < 0)
                return;
            tagRECT pageRect = layout.GetPageRect((uint)pageIndex);
            if (!Contains(pageRect, point))
                return;

            gestureView = view;
            gestureDocument = view.Doc.ID;
            gesturePage = (uint)pageIndex;
            gestureCurrentPage = layout.CurrentPage;
            gestureRotation = layout.ViewRotation;
            gesturePageRotation = view.Doc.CoreDoc.Pages[gesturePage].Rotation;
            gestureMatrix = layout.GetPageToDeviceMatrix(gesturePage, true);
            gesturePageRect = pageRect;
            gestureClientRect = view.Obj.ClientRect;
            tagSIZE scrollSize;
            view.Obj.GetScrollInfo(out gestureScroll, out scrollSize);
            pressPoint = point;
            pickingGesture = owner.IsPicking;
            pressed = true;
            dragged = false;

            view.Obj.CaptureMouse();
            captureOwned = true;
            if (pickingGesture)
            {
                uint page = gesturePage;
                List<RegionGlyph> glyphs = owner.IsOcrPicking ? new List<RegionGlyph>() :
                    TextRegionPicker.Capture(view.Doc.CoreDoc.Pages[page],
                    delegate(PXC_Rect bounds)
                    {
                        return layout.PageRectToDeviceRect(page, ref bounds, true);
                    });
                if (!pressed || !MatchesGestureView(view))
                {
                    CancelGesture();
                    return;
                }
                gestureGlyphs = glyphs;
                EnsureDrawingResources(view);
                view.RegisterDrawCallback(PXV_PagesViewDrawStage.PXV_PagesViewDraw_Foreground, this, 0);
                drawRegistered = true;
            }
        }

        private void EnsureDrawingResources(IPXV_PagesView view)
        {
            if (selectionBrush == null || selectionPen == null)
            {
                var ui = (IUIX_Inst)owner.Instance.GetExtension("UIX");
                selectionBrush = ui.CreateNewBrush();
                selectionBrush.Color0 = 0xFFFF9933;
                selectionBrush.Opacity = 0.35;
                selectionPen = ui.CreateNewPen();
                selectionPen.Brush.Color0 = 0xFFFF9933;
                selectionPen.Brush.Opacity = 0.9;
                selectionPen.Inside = true;
                selectionPen.SetDashPattern2(2, 2);
            }
            selectionPen.Width = Math.Max(1, view.Obj.Px96toPx(1));
        }

        private void UpdateGesture(tagPOINT point)
        {
            int threshold = Math.Max(4, gestureView.Obj.Px96toPx(4));
            if (Math.Abs((long)point.x - pressPoint.x) > threshold ||
                Math.Abs((long)point.y - pressPoint.y) > threshold)
                dragged = true;
            if (!pickingGesture || !dragged)
                return;

            tagRECT oldRect = selectionDirtyRect;
            selectionRect = new tagRECT
            {
                left = Math.Max(gesturePageRect.left, Math.Min(pressPoint.x, point.x)),
                right = Math.Min(gesturePageRect.right, Math.Max(pressPoint.x, point.x)),
                top = Math.Max(gesturePageRect.top, Math.Min(pressPoint.y, point.y)),
                bottom = Math.Min(gesturePageRect.bottom, Math.Max(pressPoint.y, point.y))
            };
            selection = TextRegionPicker.Select(gestureGlyphs, selectionRect);
            selectionDirtyRect = selectionRect;
            foreach (tagRECT highlight in selection.HighlightRects)
                selectionDirtyRect = Union(selectionDirtyRect, highlight);
            RedrawSelection(gestureView, oldRect);
            RedrawSelection(gestureView, selectionDirtyRect);
        }

        private void CompleteGesture(IPXV_PagesView view, tagPOINT point)
        {
            if (!pressed || !MatchesGestureView(view))
            {
                CancelGesture();
                return;
            }
            UpdateGesture(point);
            bool wasPicking = pickingGesture;
            bool wasDragged = dragged;
            uint pageIndex = gesturePage;
            RegionPickResult result = selection;
            int displayRotation = gesturePageRotation + view.Doc.PagesViewRotation;
            PXC_Rect pageRegion = new PXC_Rect();
            if (wasPicking && wasDragged)
            {
                tagPOINT first = new tagPOINT { x = selectionRect.left, y = selectionRect.top };
                tagPOINT last = new tagPOINT { x = selectionRect.right, y = selectionRect.bottom };
                PXC_Point a = view.Layout.DevicePointToPagePoint(pageIndex, ref first, true);
                PXC_Point b = view.Layout.DevicePointToPagePoint(pageIndex, ref last, true);
                pageRegion = new PXC_Rect {
                    left = Math.Min(a.x, b.x), right = Math.Max(a.x, b.x),
                    bottom = Math.Min(a.y, b.y), top = Math.Max(a.y, b.y)
                };
            }
            bool insidePage = Contains(gesturePageRect, point);
            PXC_Point pagePoint = new PXC_Point();
            if (!wasDragged && insidePage)
                pagePoint = view.Layout.DevicePointToPagePoint(pageIndex, ref point, true);

            // ReleaseMouse can synchronously deliver capture-lost; clear state before callbacks.
            CancelGesture();
            if (wasPicking && wasDragged)
            {
                owner.HandlePageBoxSelection(view, pageIndex, result, pageRegion, displayRotation);
                return;
            }
            if (!wasDragged && insidePage)
                owner.HandlePageClick(view, pageIndex, pagePoint);
        }

        public void CheckGestureView()
        {
            if (!pressed)
                return;
            try
            {
                IPXV_MainView main = owner.Instance == null ? null : owner.Instance.ActiveMainView;
                IPXV_Document document = main == null || main.ActiveDocView == null
                    ? null : main.ActiveDocView.Doc;
                IPXV_PagesView currentView = document == null || document.ActiveView == null
                    ? null : document.ActiveView.PagesView;
                if (drawingFailed || currentView == null || !MatchesGestureView(currentView))
                    CancelGesture();
            }
            catch (Exception exception)
            {
                owner.Log("Gesture view check " + exception.Message);
                CancelGesture();
            }
        }

        private bool MatchesGestureView(IPXV_PagesView view)
        {
            if (gestureView == null || view == null || !SameComObject(view, gestureView) ||
                view.Doc == null || view.Doc.ID != gestureDocument ||
                owner.IsPicking != pickingGesture)
                return false;
            IPXV_PagesLayoutManager layout = view.Layout;
            if (layout.CurrentPage != gestureCurrentPage || layout.ViewRotation != gestureRotation ||
                view.Doc.CoreDoc.Pages[gesturePage].Rotation != gesturePageRotation ||
                !SameRect(layout.GetPageRect(gesturePage), gesturePageRect) ||
                !SameRect(view.Obj.ClientRect, gestureClientRect))
                return false;
            tagPOINT scroll;
            tagSIZE scrollSize;
            view.Obj.GetScrollInfo(out scroll, out scrollSize);
            if (scroll.x != gestureScroll.x || scroll.y != gestureScroll.y)
                return false;
            PXC_Matrix matrix = layout.GetPageToDeviceMatrix(gesturePage, true);
            return NearlyEqual(matrix.a, gestureMatrix.a) && NearlyEqual(matrix.b, gestureMatrix.b) &&
                NearlyEqual(matrix.c, gestureMatrix.c) && NearlyEqual(matrix.d, gestureMatrix.d) &&
                NearlyEqual(matrix.e, gestureMatrix.e) && NearlyEqual(matrix.f, gestureMatrix.f);
        }

        public void CancelGesture()
        {
            IPXV_PagesView oldView = gestureView;
            tagRECT oldRect = selectionDirtyRect;
            bool unregister = drawRegistered;
            bool release = captureOwned;
            pressed = false;
            dragged = false;
            pickingGesture = false;
            captureOwned = false;
            drawRegistered = false;
            drawingFailed = false;
            gestureView = null;
            gestureGlyphs = null;
            selection = null;
            selectionRect = new tagRECT();
            selectionDirtyRect = new tagRECT();
            if (oldView == null)
                return;

            if (unregister)
            {
                try
                {
                    oldView.UnregisterDrawCallback(
                        PXV_PagesViewDrawStage.PXV_PagesViewDraw_Foreground, this);
                }
                catch (Exception exception)
                {
                    owner.Log("Gesture unregister " + exception.Message);
                    try { oldView.UnregisterDrawCallback2(this); }
                    catch (Exception fallback) { owner.Log("Gesture unregister fallback " + fallback.Message); }
                }
            }
            if (release)
            {
                try
                {
                    if (oldView.Obj.HasMouseCapture)
                        oldView.Obj.ReleaseMouse();
                }
                catch (Exception exception) { owner.Log("Gesture release " + exception.Message); }
            }
            RedrawSelection(oldView, oldRect);
        }

        public void OnDrawPagesView(IPXV_PagesView view, PXV_PagesViewDrawStage stage,
            IUIX_RenderContext context, IPXV_PagesLayoutRegions regions)
        {
            if (!pressed || !pickingGesture || !dragged || selection == null ||
                stage != PXV_PagesViewDrawStage.PXV_PagesViewDraw_Foreground)
                return;
            try
            {
                if (!MatchesGestureView(view))
                {
                    drawingFailed = true;
                    return;
                }
                tagRECT clip = new tagRECT
                {
                    left = Math.Max(gestureClientRect.left, gesturePageRect.left),
                    right = Math.Min(gestureClientRect.right, gesturePageRect.right),
                    top = Math.Max(gestureClientRect.top, gesturePageRect.top),
                    bottom = Math.Min(gestureClientRect.bottom, gesturePageRect.bottom)
                };
                if (clip.left >= clip.right || clip.top >= clip.bottom)
                    return;
                foreach (tagRECT highlight in selection.HighlightRects)
                {
                    tagRECT bounds = highlight;
                    context.DrawRect(ref bounds, selectionBrush, null, ref clip, 1);
                }
                tagRECT outline = selectionRect;
                context.DrawRect(ref outline, null, selectionPen, ref clip, 1);
            }
            catch (Exception exception)
            {
                if (!drawingFailed)
                    owner.Log("Gesture drawing " + exception.Message);
                drawingFailed = true;
            }
        }

        private void RedrawSelection(IPXV_PagesView view, tagRECT rect)
        {
            if (view == null || rect.left >= rect.right || rect.top >= rect.bottom)
                return;
            try
            {
                int margin = Math.Max(2, view.Obj.Px96toPx(2));
                rect.left -= margin;
                rect.top -= margin;
                rect.right += margin;
                rect.bottom += margin;
                view.RedrawRect(ref rect);
            }
            catch (Exception exception) { owner.Log("Gesture redraw " + exception.Message); }
        }

        private static bool Contains(tagRECT rect, tagPOINT point)
        {
            return point.x >= rect.left && point.x < rect.right &&
                point.y >= rect.top && point.y < rect.bottom;
        }

        private static bool SameRect(tagRECT first, tagRECT second)
        {
            return first.left == second.left && first.right == second.right &&
                first.top == second.top && first.bottom == second.bottom;
        }

        private static tagRECT Union(tagRECT first, tagRECT second)
        {
            if (first.left >= first.right || first.top >= first.bottom)
                return second;
            if (second.left >= second.right || second.top >= second.bottom)
                return first;
            return new tagRECT
            {
                left = Math.Min(first.left, second.left),
                right = Math.Max(first.right, second.right),
                top = Math.Min(first.top, second.top),
                bottom = Math.Max(first.bottom, second.bottom)
            };
        }

        private static bool NearlyEqual(double first, double second)
        {
            return Math.Abs(first - second) < 0.0000001;
        }

        private static bool SameComObject(object first, object second)
        {
            if (Object.ReferenceEquals(first, second))
                return true;
            if (first == null || second == null)
                return false;
            IntPtr a = IntPtr.Zero;
            IntPtr b = IntPtr.Zero;
            try
            {
                a = Marshal.GetIUnknownForObject(first);
                b = Marshal.GetIUnknownForObject(second);
                return a == b;
            }
            finally
            {
                if (b != IntPtr.Zero) Marshal.Release(b);
                if (a != IntPtr.Zero) Marshal.Release(a);
            }
        }
    }

    public static class TextPicker
    {
        public struct Glyph
        {
            public char Character;
            public PXC_Rect PageBounds;
            public PXC_RectF LineBounds;
        }

        public static bool TryGetWord(IPXC_Page page, PXC_Point point, out string word)
        {
            if (page == null) throw new ArgumentNullException("page");
            try
            {
                if (TryGetNativeWord(page, point, out word))
                    return true;
            }
            catch (COMException)
            {
                // SHX originals can still be usable when the native text layer is damaged.
            }
            return ShxTextReader.TryGetText(ShxTextReader.Read(page), point, out word);
        }

        private static bool TryGetNativeWord(IPXC_Page page, PXC_Point point, out string word)
        {
            word = null;
            IPXC_PageText text = page.GetText(null, false);
            if (text == null || text.CharCount == 0)
                return false;

            PXC_PointF hitPoint = new PXC_PointF { x = (float)point.x, y = (float)point.y };
            int lineIndex;
            text.HitTest(ref hitPoint, 0, false, out lineIndex);
            if (lineIndex < 0 || (uint)lineIndex >= text.LinesCount)
                return false;

            PXC_TextLineInfo line = text.get_LineInfo((uint)lineIndex);
            var glyphs = new List<Glyph>();
            int hit = -1;
            double bestDistance = double.MaxValue;
            for (uint offset = 0; offset < line.nCharsCount; ++offset)
            {
                uint index = line.nFirstCharIndex + offset;
                var glyph = new Glyph
                {
                    Character = (char)text.get_Char(index),
                    PageBounds = text.get_CharRect(index),
                    LineBounds = text.get_CharBBox(index)
                };
                glyphs.Add(glyph);
                if (IsSeparator(glyph.Character) || !Contains(glyph.PageBounds, point))
                    continue;
                double dx = point.x - (glyph.PageBounds.left + glyph.PageBounds.right) / 2;
                double dy = point.y - (glyph.PageBounds.bottom + glyph.PageBounds.top) / 2;
                double distance = dx * dx + dy * dy;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    hit = (int)offset;
                }
            }
            return TryGetWord(glyphs, hit, out word);
        }

        public static bool TryGetWord(IList<Glyph> glyphs, int hit, out string word)
        {
            word = null;
            if (hit < 0 || hit >= glyphs.Count || IsSeparator(glyphs[hit].Character))
                return false;
            int first = hit;
            int last = hit;
            while (first > 0 && IsJoined(glyphs[first - 1], glyphs[first]))
                --first;
            while (last + 1 < glyphs.Count && IsJoined(glyphs[last], glyphs[last + 1]))
                ++last;
            char[] chars = new char[last - first + 1];
            for (int i = first; i <= last; ++i)
                chars[i - first] = glyphs[i].Character;
            word = new string(chars);
            return word.Length != 0;
        }

        internal static bool IsSeparator(char character)
        {
            return char.IsWhiteSpace(character) || char.IsControl(character) || character == '\ufffd';
        }

        private static bool Contains(PXC_Rect bounds, PXC_Point point)
        {
            return point.x >= Math.Min(bounds.left, bounds.right) &&
                   point.x <= Math.Max(bounds.left, bounds.right) &&
                   point.y >= Math.Min(bounds.bottom, bounds.top) &&
                   point.y <= Math.Max(bounds.bottom, bounds.top);
        }

        internal static bool IsJoined(Glyph first, Glyph second)
        {
            if (IsSeparator(first.Character) || IsSeparator(second.Character))
                return false;
            PXC_RectF a = first.LineBounds;
            PXC_RectF b = second.LineBounds;
            double ah = Math.Abs(a.top - a.bottom);
            double bh = Math.Abs(b.top - b.bottom);
            double height = Math.Max(ah, bh);
            if (height < 0.000001)
                return false;

            double aCenter = (a.top + a.bottom) / 2;
            double bCenter = (b.top + b.bottom) / 2;
            if (Math.Abs(aCenter - bCenter) > height * 0.4)
                return false;

            double aLeft = Math.Min(a.left, a.right);
            double aRight = Math.Max(a.left, a.right);
            double bLeft = Math.Min(b.left, b.right);
            double bRight = Math.Max(b.left, b.right);
            double gap = Math.Max(bLeft - aRight, aLeft - bRight);
            if (gap > height * 0.4)
                return false;

            // A large backwards jump usually starts another drawing label.
            return bLeft >= aLeft - Math.Max(aRight - aLeft, bRight - bLeft) * 0.5;
        }
    }

    public static class CircleWriter
    {
        public static IPXC_Annotation Add(
            IPXV_Inst instance, IPXV_Document document, uint pageIndex,
            PXC_Point center, double widthMm, double heightMm,
            string subject, int displayRotation)
        {
            if (string.IsNullOrWhiteSpace(subject))
                throw new ArgumentException("A subject is required.", "subject");
            if (double.IsNaN(widthMm) || double.IsInfinity(widthMm) || widthMm <= 0 ||
                double.IsNaN(heightMm) || double.IsInfinity(heightMm) || heightMm <= 0)
                throw new ArgumentException("Ellipse dimensions must be positive.");

            int rotation = ((displayRotation % 360) + 360) % 360;
            if (rotation == 90 || rotation == 270)
            {
                double swap = widthMm;
                widthMm = heightMm;
                heightMm = swap;
            }

            double rx = widthMm * 72.0 / 25.4 / 2;
            double ry = heightMm * 72.0 / 25.4 / 2;
            PXC_Rect bounds = new PXC_Rect
            {
                left = center.x - rx, right = center.x + rx,
                bottom = center.y - ry, top = center.y + ry
            };
            IPXC_Page page = document.CoreDoc.Pages[pageIndex];
            var atoms = (IPXS_Inst)instance.GetExtension("PXS");
            var auxiliary = (IAUX_Inst)instance.GetExtension("AUX");
            IPXC_Annotation annotation = null;
            bool committed = false;
            try
            {
                // nPos 是注释在页面列表中的插入位置：0 会插到最底层，追加到末尾才是置顶。
                uint insertionPosition = page.GetAnnotsCount();
                annotation = page.InsertNewAnnot(atoms.StrToAtom("Circle"), ref bounds, insertionPosition);
                var data = (IPXC_AnnotData_SquareCircle)annotation.Data;
                var fill = auxiliary.CreateColor(ColorType.ColorType_RGB);
                fill.SetRGB(239f / 255, 83f / 255, 80f / 255);
                var stroke = auxiliary.CreateColor(ColorType.ColorType_RGB);
                stroke.SetRGB(211f / 255, 47f / 255, 47f / 255);
                data.FColor = fill;
                data.SColor = stroke;
                data.Opacity = 0.6;
                data.Subject = subject;
                data.Contents = "";
                PXC_AnnotBorder border = new PXC_AnnotBorder
                {
                    nStyle = PXC_AnnotBorderStyle.ABS_Solid,
                    nWidth = 0.2f,
                    nDashCount = 0,
                    DashArray = new float[10],
                    nRadius = new float[2]
                };
                data.set_Border(ref border);
                annotation.Data = data;
                annotation.Flags = 4;
                annotation.Name = Guid.NewGuid().ToString("D");
                IOperation operation = instance.CreateOp(instance.Str2ID("op.annots.addNew", false));
                operation.Params.Root["Input"].Add(CabDataTypeID.dt_Invalid).v = annotation;
                operation.Do(0);
                committed = true;
                return annotation;
            }
            finally
            {
                if (annotation != null && !committed)
                {
                    try
                    {
                        int index = annotation.IndexOnPage;
                        if (index >= 0)
                            page.RemoveAnnots((uint)index, 1);
                    }
                    catch (COMException) { }
                }
            }
        }
    }
}
