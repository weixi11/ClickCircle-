using System;
using System.Drawing;
using System.Runtime.InteropServices;
using PDFXEdit;

namespace ClickCircle
{
    public static class OcrRegionRenderer
    {
        public static Size RasterSize(double width, double height)
        {
            if (Double.IsNaN(width) || Double.IsInfinity(width) || width <= 0.1 ||
                Double.IsNaN(height) || Double.IsInfinity(height) || height <= 0.1)
                throw new ArgumentException("识别区域太小");
            double scale = Math.Min(12.0, 2400.0 / Math.Max(width, height));
            return new Size(Math.Min(2400, Math.Max(1, (int)Math.Ceiling(width * scale))),
                Math.Min(2400, Math.Max(1, (int)Math.Ceiling(height * scale))));
        }

        public static Bitmap Render(IPXV_Inst instance, IPXC_Page page, PXC_Rect region, int rotation)
        {
            double width = region.right - region.left, height = region.top - region.bottom;
            Size size = RasterSize(width, height);
            IIXC_Inst ixc = (IIXC_Inst)instance.GetExtension("IXC");
            IIXC_Page target = ixc.Page_CreateEmpty((uint)size.Width, (uint)size.Height,
                IXC_PageFormat.PageFormat_8RGB, 0);
            try
            {
                tagRECT destination = new tagRECT { right = size.Width, bottom = size.Height };
                target.Fill(ref destination, 0xFFFFFFFF, 0);
                double sx = size.Width / width, sy = size.Height / height;
                PXC_Matrix matrix = new PXC_Matrix {
                    a = sx, d = -sy, e = -region.left * sx, f = region.top * sy
                };
                page.DrawToIXCPage(target, ref destination, ref matrix, null, null, null);
                ulong handle = target.CreateHBitmap(0);
                if (handle == 0) throw new InvalidOperationException("无法渲染识别区域");
                try
                {
                    using (Bitmap source = Image.FromHbitmap(new IntPtr(unchecked((long)handle))))
                    {
                        Bitmap bitmap = new Bitmap(source);
                        rotation = ((rotation % 360) + 360) % 360;
                        if (rotation == 90) bitmap.RotateFlip(RotateFlipType.Rotate90FlipNone);
                        else if (rotation == 180) bitmap.RotateFlip(RotateFlipType.Rotate180FlipNone);
                        else if (rotation == 270) bitmap.RotateFlip(RotateFlipType.Rotate270FlipNone);
                        return bitmap;
                    }
                }
                finally { DeleteObject(new IntPtr(unchecked((long)handle))); }
            }
            finally
            {
                if (target != null && Marshal.IsComObject(target)) Marshal.ReleaseComObject(target);
            }
        }

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr handle);
    }
}
