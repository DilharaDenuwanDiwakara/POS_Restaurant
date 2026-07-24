using System.Globalization;
using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PointOfSale.Core.Interfaces.Services;

namespace PointOfSale.Infrastructure.Service
{
    public class PrintService
    {
        private readonly IBarcodeService _barcodeService;
        public PrintService(IBarcodeService barcodeService)
        {
            _barcodeService = barcodeService;
        }
        public void PrintPharmacyLabel(PrintDialog printDialog, string productCode, string productName)
        {
            // Pharmacy Label Size (38mm x 25mm) -> Approx 144px x 96px
            double labelWidth = 144;
            double labelHeight = 96;

            DrawingVisual visual = new DrawingVisual();
            using (DrawingContext dc = visual.RenderOpen())
            {
                // ---------------------------------------------------------
                // 1. PRODUCT NAME (Top Center)
                // ---------------------------------------------------------
                // Truncate name if it's too long (over 15 chars) to keep it on one line
                string displayTitle = productName.Length > 15
                    ? productName.Substring(0, 15) + "..."
                    : productName;

                DrawCenteredText(dc, displayTitle, 10, labelWidth, 5, FontWeights.Bold);

                // ---------------------------------------------------------
                // 2. BARCODE IMAGE (Middle Center)
                // ---------------------------------------------------------
                double imgWidth = 110;
                double imgHeight = 40;

                // Generate Barcode Image
                var barcodeImg = _barcodeService.GenerateBarcode(productCode, (int)imgWidth, (int)imgHeight);

                // Calculate Center X for Image: (ContainerWidth - ImageWidth) / 2
                double imgX = (labelWidth - imgWidth) / 2;
                double imgY = 25; // Y position below the title

                dc.DrawImage(barcodeImg, new Rect(imgX, imgY, imgWidth, imgHeight));

                // ---------------------------------------------------------
                // 3. PRODUCT CODE (Bottom Center)
                // ---------------------------------------------------------
                // Drawn below the barcode image
                DrawCenteredText(dc, productCode, 10, labelWidth, 68, FontWeights.Normal);
            }

            // Configure Printer Page Size
            printDialog.PrintTicket.PageMediaSize = new PageMediaSize(labelWidth, labelHeight);

            // Print
            printDialog.PrintVisual(visual, $"Label_{productCode}");
        }

        //private void DrawText(DrawingContext dc, string text, double size, double x, double y, FontWeight weight)
        //{
        //    var formattedText = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
        //        new Typeface(new FontFamily("Arial"), FontStyles.Normal, weight, FontStretches.Normal),
        //        size, Brushes.Black, 1.25);

        //    // Center alignment logic can be added here if 'x' represents center
        //    dc.DrawText(formattedText, new Point(x - (formattedText.Width / 2), y));
        //}

        private void DrawCenteredText(DrawingContext dc, string text, double fontSize, double containerWidth, double yPos, FontWeight weight)
        {
            var formattedText = new FormattedText(
                text,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Arial"), FontStyles.Normal, weight, FontStretches.Normal),
                fontSize,
                Brushes.Black,
                1.25); // PixelsPerDip

            // Math: (Total Width - Text Width) / 2 = Starting X Position
            double xPos = (containerWidth - formattedText.Width) / 2;

            dc.DrawText(formattedText, new Point(xPos, yPos));
        }
    }
}
