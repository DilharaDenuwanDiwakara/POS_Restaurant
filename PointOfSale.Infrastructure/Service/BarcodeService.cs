using System;
using System.Windows.Media.Imaging;
using PointOfSale.Core.Interfaces.Services;
using ZXing;
using ZXing.Common;

namespace PointOfSale.Infrastructure.Service
{
    public class BarcodeService : IBarcodeService
    {
        public BitmapSource GenerateBarcode(string content, int width, int height)
        {
            var writer = new BarcodeWriter
            {
                Format = BarcodeFormat.CODE_128,
                Options = new EncodingOptions
                {
                    Height = height,
                    Width = width,
                    PureBarcode = true,
                    Margin = 0
                }
            };

            using (var bitmap = writer.Write(content))
            {
                return System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                    bitmap.GetHbitmap(),
                    IntPtr.Zero,
                    System.Windows.Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
            }
        }
    }
}
