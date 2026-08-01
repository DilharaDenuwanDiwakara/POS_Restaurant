using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using CrystalDecisions.CrystalReports.Engine;
using SAPBusinessObjects.WPF.Viewer;

namespace PointOfSale.UI.Views.Sales
{
    public partial class ZReportViewerWindow : Window
    {
        private readonly ReportDocument _reportDocument;
        private readonly bool _disposeReportOnClose;
        private bool _isClosing;
        private bool _isDisposed;

        public ZReportViewerWindow(ReportDocument reportDocument, bool disposeReportOnClose = false)
        {
            _reportDocument = reportDocument ?? throw new ArgumentNullException(nameof(reportDocument));
            _disposeReportOnClose = disposeReportOnClose;

            InitializeComponent();
            ReportViewer.Owner = this;
            ReportViewer.ShowToolbar = true;
            ReportViewer.ShowPrintButton = true;
            ReportViewer.ViewerCore.Error += ViewerCore_Error;
            ReportViewer.ViewerCore.ReportSource = _reportDocument;
        }

        protected override void OnClosed(EventArgs e)
        {
            _isClosing = true;

            if (_disposeReportOnClose)
            {
                Dispatcher.BeginInvoke(new Action(DisposeReportSafely), DispatcherPriority.ApplicationIdle);
            }

            base.OnClosed(e);
        }

        private void ViewerCore_Error(object source, ExceptionEventArgs e)
        {
            Trace.TraceError($"Crystal Reports viewer error in {Title}: {e.Exception}");
            e.Handled = true;
        }

        private void DisposeReportSafely()
        {
            if (_isDisposed)
                return;

            try
            {
                if (ReportViewer?.ViewerCore != null)
                    ReportViewer.ViewerCore.Error -= ViewerCore_Error;

                _reportDocument.Close();
                _reportDocument.Dispose();
                _isDisposed = true;
            }
            catch (Exception ex)
            {
                Trace.TraceError($"Failed to dispose Crystal report in {Title}: {ex}");
                // Closing a preview should not crash the POS screen.
            }

            if (_isClosing)
            {
                _isClosing = false;
            }
        }
    }
}
