using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MemuTiler
{
    public static class ApplicationIcons
    {
        private static ImageSource _shieldIconImage;

        private static ImageSource GetShieldIconImage()
        {
            var img = System.Drawing.SystemIcons.Shield;

            var bitmap = img.ToBitmap();
            var hBitmap = bitmap.GetHbitmap();

            return System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                    hBitmap, IntPtr.Zero, Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
        }

        public static ImageSource ShieldIconImage => _shieldIconImage ?? (_shieldIconImage = GetShieldIconImage());
    }
}
