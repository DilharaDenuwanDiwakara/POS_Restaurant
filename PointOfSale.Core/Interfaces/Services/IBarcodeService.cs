using System.Windows.Media.Imaging;

namespace PointOfSale.Core.Interfaces.Services
{
    public interface IBarcodeService
    {
        BitmapSource GenerateBarcode(string content, int width, int height);
    }
}
