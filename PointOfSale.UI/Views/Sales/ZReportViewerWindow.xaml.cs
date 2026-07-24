using System;
using System.Windows;
using CrystalDecisions.CrystalReports.Engine;

namespace PointOfSale.UI.Views.Sales
{
    public partial class ZReportViewerWindow : Window
    {
        private readonly ReportDocument _reportDocument;

        public ZReportViewerWindow(ReportDocument reportDocument)
        {
            _reportDocument = reportDocument ?? throw new ArgumentNullException(nameof(reportDocument));

            InitializeComponent();
            ReportViewer.ViewerCore.ReportSource = _reportDocument;
        }

        protected override void OnClosed(EventArgs e)
        {
            ReportViewer.ViewerCore.ReportSource = null;

            base.OnClosed(e);
        }
    }
}
