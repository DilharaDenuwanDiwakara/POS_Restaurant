using System;
using System.Windows;
using CrystalDecisions.CrystalReports.Engine;

namespace PointOfSale.UI.Views.Accounts
{
    public partial class SupplierPaymentVoucherViewerWindow : Window
    {
        private readonly ReportDocument _reportDocument;

        public SupplierPaymentVoucherViewerWindow(ReportDocument reportDocument)
        {
            _reportDocument = reportDocument ?? throw new ArgumentNullException(nameof(reportDocument));

            InitializeComponent();
            ReportViewer.ViewerCore.ReportSource = _reportDocument;
        }

        protected override void OnClosed(EventArgs e)
        {
            try
            {
                // 1. Safely detach the report source
                if (ReportViewer?.ViewerCore != null)
                {
                    ReportViewer.ViewerCore.ReportSource = null;
                }

                // 2. Safely close and dispose the document
                if (_reportDocument != null)
                {
                    _reportDocument.Close();
                    _reportDocument.Dispose();
                }
            }
            catch (Exception)
            {
                // Crystal Reports engine occasionally throws random COM exceptions during disposal.
                // Catching them prevents the UI from crashing when the user is just trying to close the window.
            }
            finally
            {
                // 3. Always ensure the base method executes so the window actually closes
                base.OnClosed(e);
            }
        }
    }
}
