using System;
using System.Drawing;
using System.Windows.Forms;
using ClickCircle;

// Standalone UI fixture for checking accessibility edits and dialog layout.
internal static class OcrReviewPreview
{
    [STAThread]
    private static void Main()
    {
        Application.EnableVisualStyles();
        using (Bitmap bitmap = new Bitmap(1000, 460))
        {
            using (Graphics graphics = Graphics.FromImage(bitmap))
            using (Font font = new Font("Arial", 36, FontStyle.Regular, GraphicsUnit.Pixel))
            {
                graphics.Clear(Color.White);
                graphics.DrawString("LI-058", font, Brushes.Black, 60, 55);
                graphics.DrawString("LI-058", font, Brushes.Black, 400, 55);
                graphics.DrawString("LI-282", font, Brushes.Black, 60, 190);
            }
            using (OcrReviewDialog dialog = new OcrReviewDialog(bitmap, "/"))
            {
                dialog.ShowInTaskbar = true;
                dialog.ShowDialog();
            }
        }
    }
}
