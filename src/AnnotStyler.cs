using System;
using PDFXEdit;

namespace ClickCircle
{
    /// <summary>
    /// 把 AnnotStyle 写进已有标注：字段、颜色、边框、不透明度，
    /// 以及测量标注（周长/面积/线）的比例尺与数字格式。
    /// 所有改动都包在 try/catch 里：某个属性不支持时跳过，不影响其它属性。
    /// </summary>
    internal static class AnnotStyler
    {
        /// <summary>返回是否真的改动了标注；error 非空表示整体失败。</summary>
        internal static bool Apply(IPXC_Annotation annotation, AnnotStyle style, IAUX_Inst auxiliary,
            out string error)
        {
            error = null;
            if (annotation == null || style == null || !style.Enabled)
                return false;
            IPXC_AnnotData data = annotation.Data;
            if (data == null)
            {
                error = "标注没有可编辑的数据";
                return false;
            }

            bool isMeasure = data is IPXC_AnnotData_Line || data is IPXC_AnnotData_Poly;
            if (style.MeasureOnly && !isMeasure)
                return false;

            AnnotStyle normalized = style.Clone();
            normalized.Normalize();

            bool changed = false;
            changed |= ApplyFields(data, normalized);
            changed |= ApplyColors(data, normalized, auxiliary);
            changed |= ApplyBorder(data, normalized);
            changed |= ApplyMeasure(data, normalized);
            if (!changed)
                return false;

            annotation.Data = data;
            return true;
        }

        /// <summary>主题、作者、标签/正文；空字符串表示保持原值。</summary>
        private static bool ApplyFields(IPXC_AnnotData data, AnnotStyle style)
        {
            IPXC_AnnotData_Markup markup = data as IPXC_AnnotData_Markup;
            if (markup == null)
                return false;
            bool changed = false;
            if (!String.IsNullOrEmpty(style.Subject))
            {
                if (markup.Subject != style.Subject) { markup.Subject = style.Subject; changed = true; }
            }
            if (!String.IsNullOrEmpty(style.Author))
            {
                if (markup.Title != style.Author) { markup.Title = style.Author; changed = true; }
            }
            if (!String.IsNullOrEmpty(style.Contents))
            {
                if (markup.Contents != style.Contents) { markup.Contents = style.Contents; changed = true; }
            }
            return changed;
        }

        private static bool ApplyColors(IPXC_AnnotData data, AnnotStyle style, IAUX_Inst auxiliary)
        {
            if (auxiliary == null)
                return false;
            bool changed = false;
            IColor stroke = auxiliary.CreateColor(ColorType.ColorType_RGB);
            SetColor(stroke, style.StrokeColor);

            IPXC_AnnotData_Line line = data as IPXC_AnnotData_Line;
            IPXC_AnnotData_Poly poly = data as IPXC_AnnotData_Poly;
            IPXC_AnnotData_SquareCircle square = data as IPXC_AnnotData_SquareCircle;

            if (line != null)
            {
                line.SColor = stroke;
                if (style.UseFill) line.FColor = CreateFill(auxiliary, style);
                else line.FColor = null;
                changed = true;
            }
            else if (poly != null)
            {
                poly.SColor = stroke;
                if (style.UseFill) poly.FColor = CreateFill(auxiliary, style);
                else poly.FColor = null;
                changed = true;
            }
            else if (square != null)
            {
                square.SColor = stroke;
                if (style.UseFill) square.FColor = CreateFill(auxiliary, style);
                else square.FColor = null;
                changed = true;
            }
            else
            {
                data.Color = stroke;
                changed = true;
            }
            return changed;
        }

        private static IColor CreateFill(IAUX_Inst auxiliary, AnnotStyle style)
        {
            IColor fill = auxiliary.CreateColor(ColorType.ColorType_RGB);
            SetColor(fill, style.FillColor);
            return fill;
        }

        private static void SetColor(IColor color, System.Drawing.Color value)
        {
            if (color == null)
                return;
            color.SetRGB(value.R / 255f, value.G / 255f, value.B / 255f);
        }

        private static bool ApplyBorder(IPXC_AnnotData data, AnnotStyle style)
        {
            PXC_AnnotBorder border = new PXC_AnnotBorder {
                nStyle = style.Dashed ? PXC_AnnotBorderStyle.ABS_Dashed : PXC_AnnotBorderStyle.ABS_Solid,
                nWidth = (float)style.BorderWidthPt,
                nDashCount = style.Dashed ? 2u : 0u,
                DashArray = new float[10],
                nRadius = new float[2]
            };
            if (style.Dashed)
            {
                border.DashArray[0] = 4;
                border.DashArray[1] = 2;
            }
            try { data.set_Border(ref border); }
            catch (Exception) { return false; }
            try
            {
                data.Opacity = style.OpacityPercent / 100.0;
                data.BlendMode = PXC_BlendMode.BlendMode_Normal;
            }
            catch (Exception) { }
            return true;
        }

        /// <summary>测量标注：比例尺（1 mm = X m）与数字格式（小数位、千位分隔）。</summary>
        private static bool ApplyMeasure(IPXC_AnnotData data, AnnotStyle style)
        {
            if (!(style.MetersPerMm > 0) && style.DecimalPlaces < 0)
                return false;
            IPXC_Measure measure = null;
            IPXC_AnnotData_Line line = data as IPXC_AnnotData_Line;
            if (line != null)
                measure = line.Measure;
            else
            {
                IPXC_AnnotData_Poly poly = data as IPXC_AnnotData_Poly;
                if (poly != null)
                    measure = poly.Measure;
            }
            IPXC_MeasureRL ruler = measure as IPXC_MeasureRL;
            if (ruler == null)
                return false;

            bool changed = false;
            double metersPerPoint = 0;
            if (style.MetersPerMm > 0)
            {
                metersPerPoint = AnnotStyle.MetersPerPoint(style.MetersPerMm);
                try
                {
                    string ratio = AnnotStyle.FormatRatio(style.MetersPerMm);
                    if (ratio.Length > 0) { ruler.Ratio = ratio; changed = true; }
                }
                catch (Exception) { }
            }

            try { changed |= ConfigureFormat(ruler.GetDistance(true), style, metersPerPoint, "m"); }
            catch (Exception) { }
            try { changed |= ConfigureFormat(ruler.GetArea(true), style, metersPerPoint * metersPerPoint, "m\u00b2"); }
            catch (Exception) { }
            return changed;
        }

        private static bool ConfigureFormat(IPXC_NumberFormatArray array, AnnotStyle style,
            double coefficient, string unit)
        {
            if (array == null)
                return false;
            array.Clear();
            IPXC_NumberFormatItem item = array.CreateNewItem();
            if (item == null)
                return false;
            if (coefficient > 0)
            {
                item.Coefficient = coefficient;
                item.Units = unit;
            }
            if (style.DecimalPlaces >= 0)
                item.Precision = style.DecimalPlaces;
            item.ThousandSep = style.UseThousands ? "," : String.Empty;
            item.DecimalSep = ".";
            array.Insert(item, 0);
            return true;
        }
    }
}
