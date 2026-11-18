using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using PDFXEdit;

namespace ClickCircle
{
    public struct ShxTextLabel
    {
        public string Text;
        public PXC_Rect Bounds;
    }

    public static class ShxTextReader
    {
        public static List<ShxTextLabel> Read(IPXC_Page page)
        {
            if (page == null) throw new ArgumentNullException("page");
            var labels = new List<ShxTextLabel>();
            uint count = page.GetAnnotsCount();
            for (uint index = 0; index < count; index++)
            {
                try
                {
                    IPXC_Annotation annotation = page.GetAnnot(index);
                    var data = annotation.Data as IPXC_AnnotData_Markup;
                    if (data == null) continue;
                    ShxTextLabel label;
                    if (TryCreate(data.Title, data.Contents, annotation.get_Rect(), out label))
                        labels.Add(label);
                }
                catch (COMException)
                {
                    // A damaged annotation must not block other labels on the page.
                }
            }
            return labels;
        }

        public static bool TryCreate(string title, string contents, PXC_Rect bounds,
            out ShxTextLabel label)
        {
            label = default(ShxTextLabel);
            if (!String.Equals(title, "AutoCAD SHX Text", StringComparison.Ordinal) ||
                String.IsNullOrWhiteSpace(contents) ||
                !IsFinite(bounds.left) || !IsFinite(bounds.right) ||
                !IsFinite(bounds.top) || !IsFinite(bounds.bottom))
                return false;
            PXC_Rect normalized = new PXC_Rect
            {
                left = Math.Min(bounds.left, bounds.right),
                right = Math.Max(bounds.left, bounds.right),
                bottom = Math.Min(bounds.bottom, bounds.top),
                top = Math.Max(bounds.bottom, bounds.top)
            };
            if (normalized.left == normalized.right || normalized.bottom == normalized.top)
                return false;
            label = new ShxTextLabel { Text = contents.Trim(), Bounds = normalized };
            return true;
        }

        public static bool TryGetText(IList<ShxTextLabel> labels, PXC_Point point,
            out string text)
        {
            if (labels == null) throw new ArgumentNullException("labels");
            text = null;
            double bestArea = Double.PositiveInfinity;
            double bestDistance = Double.PositiveInfinity;
            foreach (ShxTextLabel label in labels)
            {
                PXC_Rect bounds = label.Bounds;
                if (point.x < bounds.left || point.x > bounds.right ||
                    point.y < bounds.bottom || point.y > bounds.top)
                    continue;
                double area = (bounds.right - bounds.left) * (bounds.top - bounds.bottom);
                double dx = point.x - (bounds.left + bounds.right) / 2;
                double dy = point.y - (bounds.bottom + bounds.top) / 2;
                double distance = dx * dx + dy * dy;
                if (area < bestArea || (area == bestArea && distance < bestDistance))
                {
                    text = label.Text;
                    bestArea = area;
                    bestDistance = distance;
                }
            }
            return text != null;
        }

        private static bool IsFinite(double value)
        {
            return !Double.IsNaN(value) && !Double.IsInfinity(value);
        }
    }
}
