using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using CrystalDecisions.CrystalReports.Engine;
using CrystalDecisions.Shared;
using PointOfSale.Core.Enums;
using PointOfSale.Core.Interfaces.Purchasing;
using PointOfSale.Core.Interfaces.Repositories.Purchasing;
using PointOfSale.Core.Models.Purchasing;
using PointOfSale.Core.Services;
using PointOfSale.UI.Commands;
using PointOfSale.UI.DataSets;

namespace PointOfSale.UI.ViewModels.Purchasing
{
    public class GoodsPurchaseNoteApprovalViewModel : BaseViewModel
    {
        private readonly IGoodsPurchaseNoteRepository _goodsPurchaseNoteRepository;
        private readonly ISupplierRepository _supplierRepository;
        private readonly IUserSessionService _userSessionService;

        public GoodsPurchaseNoteApprovalViewModel(
            IGoodsPurchaseNoteRepository goodsPurchaseNoteRepository,
            ISupplierRepository supplierRepository,
            IUserSessionService userSessionService)
        {
            _goodsPurchaseNoteRepository = goodsPurchaseNoteRepository;
            _supplierRepository = supplierRepository;
            _userSessionService = userSessionService;

            ApprovalQueue = new ObservableCollection<GoodPurchaseNote>();
            SelectedPOLines = new ObservableCollection<GoodsPurchaseNoteLine>();

            SearchCommand = new AsyncRelayCommand(async _ => await LoadApprovalQueueAsync());
            ApproveCommand = new AsyncRelayCommand(async _ => await ApproveAsync(), _ => CanApprove);
            RejectCommand = new AsyncRelayCommand(async _ => await RejectAsync(), _ => CanReject);

            _ = LoadSuppliersAsync();
            _ = LoadApprovalQueueAsync();
        }

        public ObservableCollection<GoodPurchaseNote> ApprovalQueue { get; }
        public ObservableCollection<GoodsPurchaseNoteLine> SelectedPOLines { get; }

        private ObservableCollection<Supplier> _searchSuppliers;
        public ObservableCollection<Supplier> SearchSuppliers
        {
            get => _searchSuppliers;
            private set => SetProperty(ref _searchSuppliers, value);
        }

        private int _filterSupplierId = -1;
        public int FilterSupplierId
        {
            get => _filterSupplierId;
            set => SetProperty(ref _filterSupplierId, value);
        }

        private DateTime? _searchDateFrom = DateTime.Today.AddDays(-30);
        public DateTime? SearchDateFrom
        {
            get => _searchDateFrom;
            set => SetProperty(ref _searchDateFrom, value);
        }

        private DateTime? _searchDateTo = DateTime.Today;
        public DateTime? SearchDateTo
        {
            get => _searchDateTo;
            set => SetProperty(ref _searchDateTo, value);
        }

        private GoodPurchaseNote _selectedPO;
        public GoodPurchaseNote SelectedPO
        {
            get => _selectedPO;
            set
            {
                if (SetProperty(ref _selectedPO, value))
                {
                    (ApproveCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
                    (RejectCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
                    _ = LoadSelectedPOLinesAsync();
                }
            }
        }

        private string _approvalRemarks;
        public string ApprovalRemarks
        {
            get => _approvalRemarks;
            set
            {
                if (SetProperty(ref _approvalRemarks, value))
                {
                    (RejectCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        private decimal _selectedPONetAmount;
        public decimal SelectedPONetAmount
        {
            get => _selectedPONetAmount;
            private set => SetProperty(ref _selectedPONetAmount, value);
        }

        #region Commands
        public ICommand SearchCommand { get; }
        public ICommand ApproveCommand { get; }
        public ICommand RejectCommand { get; }
        #endregion

        private async Task LoadSuppliersAsync()
        {
            var supplierList = await _supplierRepository.GetAllAsync();
            var searchList = new List<Supplier>(supplierList);

            searchList.Insert(0, new Supplier
            {
                SupplierId = -1,
                SupplierName = "ALL SUPPLIERS"
            });

            SearchSuppliers = new ObservableCollection<Supplier>(searchList);
            FilterSupplierId = -1;
        }

        private async Task LoadApprovalQueueAsync()
        {
            try
            {
                int? supplierId = FilterSupplierId == -1 ? (int?)null : FilterSupplierId;

                var items = await _goodsPurchaseNoteRepository.GetPOsForApprovalAsync(
                    _userSessionService.BranchId,
                    supplierId,
                    SearchDateFrom,
                    SearchDateTo);

                ApprovalQueue.Clear();
                foreach (var item in items)
                {
                    ApprovalQueue.Add(item);
                }

                if (ApprovalQueue.Count == 0)
                {
                    SelectedPO = null;
                    SelectedPOLines.Clear();
                    SelectedPONetAmount = 0;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading PO approval queue: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task LoadSelectedPOLinesAsync()
        {
            SelectedPOLines.Clear();
            SelectedPONetAmount = 0;

            if (SelectedPO == null)
            {
                return;
            }

            try
            {
                var lines = await _goodsPurchaseNoteRepository.GetPOLinesAsync(SelectedPO.GoodsPurchaseNoteId);
                foreach (var line in lines)
                {
                    SelectedPOLines.Add(line);
                }

                SelectedPONetAmount = SelectedPOLines.Sum(x => x.LineTotal);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading PO lines: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool CanApprove =>
            SelectedPO != null &&
            HasStatus(SelectedPO, PurchaseOrderStatus.PENDING_APPROVAL);
        private async Task ApproveAsync()
        {
            if (!CanApprove)
            {
                return;
            }

            try
            {
                await _goodsPurchaseNoteRepository.ApproveRejectPOAsync(
                    SelectedPO.GoodsPurchaseNoteId,
                    true,
                    _userSessionService.UserId,
                    ApprovalRemarks);

                long approvedPOId = SelectedPO.GoodsPurchaseNoteId;

                MessageBox.Show("Purchase Order approved successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                ApprovalRemarks = string.Empty;
                await LoadApprovalQueueAsync();

                await OpenPurchaseOrderReportAsync(approvedPOId);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error approving Purchase Order: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool CanReject =>
            SelectedPO != null &&
            HasStatus(SelectedPO, PurchaseOrderStatus.PENDING_APPROVAL) &&
            !string.IsNullOrWhiteSpace(ApprovalRemarks);
        private async Task RejectAsync()
        {
            if (!CanReject) return;

            try
            {
                await _goodsPurchaseNoteRepository.ApproveRejectPOAsync(
                    SelectedPO.GoodsPurchaseNoteId,
                    false,
                    _userSessionService.UserId,
                    ApprovalRemarks);

                MessageBox.Show("Purchase Order cancelled successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                ApprovalRemarks = string.Empty;
                await LoadApprovalQueueAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error cancelling Purchase Order: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task OpenPurchaseOrderReportAsync(long purchaseNoteId)
        {
            DataTable reportData = await _goodsPurchaseNoteRepository.GetPurchaseOrderReportDataAsync(purchaseNoteId);

            if (reportData == null || reportData.Rows.Count == 0)
            {
                MessageBox.Show("No data found for this Purchase Order.", "Export Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string tempFile = Path.Combine(Path.GetTempPath(), $"PO_{purchaseNoteId}_{DateTime.Now:yyyyMMdd_HHmm}.pdf");

            await Task.Run(() => ExportPurchaseOrderReport(reportData, tempFile));

            Process.Start(new ProcessStartInfo(tempFile) { UseShellExecute = true });
        }

        private void ExportPurchaseOrderReport(DataTable reportData, string filePath)
        {
            using (var report = new ReportDocument())
            {
                string reportPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Reports", "PurchaseOrderNote.rpt");
                if (!File.Exists(reportPath))
                {
                    throw new FileNotFoundException("Crystal report file not found.", reportPath);
                }

                report.Load(reportPath);

                var ds = new PurchaseOrderDS();
                var compatibleReportData = PurchaseOrderReportDataNormalizer.Normalize(reportData, ds.PurchaseOrderReport);
                ds.PurchaseOrderReport.Merge(compatibleReportData);

                report.SetDataSource(ds);
                report.ExportToDisk(ExportFormatType.PortableDocFormat, filePath);
            }
        }

        private static bool HasStatus(GoodPurchaseNote purchaseOrder, PurchaseOrderStatus expectedStatus)
        {
            if (purchaseOrder == null || string.IsNullOrWhiteSpace(purchaseOrder.Status))
            {
                return false;
            }

            return Enum.TryParse(purchaseOrder.Status, true, out PurchaseOrderStatus currentStatus) &&
                   currentStatus == expectedStatus;
        }
    }

    internal static class PurchaseOrderReportDataNormalizer
    {
        public static DataTable Normalize(DataTable source, DataTable targetSchema)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (targetSchema == null)
            {
                throw new ArgumentNullException(nameof(targetSchema));
            }

            var normalized = new DataTable(source.TableName);

            foreach (DataColumn sourceColumn in source.Columns)
            {
                var targetColumn = targetSchema.Columns[sourceColumn.ColumnName];
                var columnType = targetColumn?.DataType ?? sourceColumn.DataType;

                normalized.Columns.Add(sourceColumn.ColumnName, columnType);
            }

            if (!normalized.Columns.Contains("ComapanyLogo") && targetSchema.Columns.Contains("ComapanyLogo"))
            {
                normalized.Columns.Add("ComapanyLogo", targetSchema.Columns["ComapanyLogo"].DataType);
            }

            foreach (DataRow sourceRow in source.Rows)
            {
                var normalizedRow = normalized.NewRow();

                foreach (DataColumn column in normalized.Columns)
                {
                    var value = GetSourceValue(source, sourceRow, column.ColumnName);
                    normalizedRow[column.ColumnName] = ConvertValue(value, column.DataType);
                }

                normalized.Rows.Add(normalizedRow);
            }

            return normalized;
        }

        private static object GetSourceValue(DataTable source, DataRow sourceRow, string columnName)
        {
            if (source.Columns.Contains(columnName))
            {
                return sourceRow[columnName];
            }

            if (string.Equals(columnName, "ComapanyLogo", StringComparison.OrdinalIgnoreCase) &&
                source.Columns.Contains("CompanyLogo"))
            {
                return sourceRow["CompanyLogo"];
            }

            return DBNull.Value;
        }

        private static object ConvertValue(object value, Type targetType)
        {
            if (value == null || value == DBNull.Value)
            {
                return DBNull.Value;
            }

            if (targetType == typeof(string))
            {
                return Convert.ToString(value, CultureInfo.CurrentCulture);
            }

            if (targetType == typeof(byte[]))
            {
                return value is byte[]? value : DBNull.Value;
            }

            if (value.GetType() == targetType)
            {
                return value;
            }

            return Convert.ChangeType(value, targetType, CultureInfo.CurrentCulture);
        }
    }
}
