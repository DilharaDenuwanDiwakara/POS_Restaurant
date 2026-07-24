using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Diagnostics;
using System.IO;
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
    public class SupplierReturnApprovalViewModel : BaseViewModel
    {
        private readonly ISupplierReturnRepository _supplierReturnRepository;
        private readonly ISupplierRepository _supplierRepository;
        private readonly IUserSessionService _userSessionService;

        public SupplierReturnApprovalViewModel(
            ISupplierReturnRepository supplierReturnRepository,
            ISupplierRepository supplierRepository,
            IUserSessionService userSessionService)
        {
            _supplierReturnRepository = supplierReturnRepository;
            _supplierRepository = supplierRepository;
            _userSessionService = userSessionService;

            ApprovalQueue = new ObservableCollection<SupplierReturn>();
            SelectedReturnLines = new ObservableCollection<SupplierReturnLine>();

            SearchCommand = new AsyncRelayCommand(async _ => await LoadApprovalQueueAsync());
            ApproveCommand = new AsyncRelayCommand(async _ => await ApproveAsync(), _ => CanApprove);
            RejectCommand = new AsyncRelayCommand(async _ => await RejectAsync(), _ => CanReject);

            _ = LoadSuppliersAsync();
            _ = LoadApprovalQueueAsync();
        }

        public ObservableCollection<SupplierReturn> ApprovalQueue { get; }
        public ObservableCollection<SupplierReturnLine> SelectedReturnLines { get; }

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

        private SupplierReturn _selectedReturn;
        public SupplierReturn SelectedReturn
        {
            get => _selectedReturn;
            set
            {
                if (SetProperty(ref _selectedReturn, value))
                {
                    ApproverRemark = string.Empty;
                    RaiseActionCanExecuteChanged();
                    _ = LoadSelectedReturnLinesAsync();
                }
            }
        }

        private string _approverRemark;
        public string ApproverRemark
        {
            get => _approverRemark;
            set
            {
                if (SetProperty(ref _approverRemark, value))
                {
                    (RejectCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        public ICommand SearchCommand { get; }
        public ICommand ApproveCommand { get; }
        public ICommand RejectCommand { get; }

        private async Task LoadSuppliersAsync()
        {
            try
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
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading suppliers: {GetDetailedErrorMessage(ex)}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task LoadApprovalQueueAsync()
        {
            try
            {
                int? supplierId = FilterSupplierId == -1 ? (int?)null : FilterSupplierId;

                var items = await _supplierReturnRepository.GetPendingApprovalsAsync(
                    _userSessionService.BranchId,
                    supplierId,
                    SearchDateFrom,
                    SearchDateTo);

                ApprovalQueue.Clear();
                foreach (var item in items)
                {
                    ApprovalQueue.Add(item);
                }

                if (ApprovalQueue.Count > 0)
                {
                    SelectedReturn = ApprovalQueue[0];
                }
                else
                {
                    SelectedReturn = null;
                    SelectedReturnLines.Clear();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading supplier return approval queue: {GetDetailedErrorMessage(ex)}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task LoadSelectedReturnLinesAsync()
        {
            SelectedReturnLines.Clear();

            if (SelectedReturn == null)
            {
                return;
            }

            try
            {
                var lines = await _supplierReturnRepository.GetLinesByReturnIdAsync(SelectedReturn.SupplierReturnId);
                foreach (var line in lines)
                {
                    SelectedReturnLines.Add(line);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading supplier return lines: {GetDetailedErrorMessage(ex)}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool CanApprove =>
            SelectedReturn != null &&
            IsPendingStatus(SelectedReturn);
        private async Task ApproveAsync()
        {
            if (!CanApprove)
            {
                return;
            }

            try
            {
                int approvedReturnId = SelectedReturn.SupplierReturnId;

                await _supplierReturnRepository.ApproveAsync(
                    approvedReturnId,
                    _userSessionService.UserId);

                MessageBox.Show("Supplier Return approved successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                ApproverRemark = string.Empty;
                await LoadApprovalQueueAsync();

                await OpenSupplierReturnReportAsync(approvedReturnId);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error approving Supplier Return: {GetDetailedErrorMessage(ex)}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool CanReject =>
            SelectedReturn != null &&
            IsPendingStatus(SelectedReturn) &&
            !string.IsNullOrWhiteSpace(ApproverRemark);

        private async Task RejectAsync()
        {
            if (!CanReject)
            {
                return;
            }

            try
            {
                await _supplierReturnRepository.RejectAsync(
                    SelectedReturn.SupplierReturnId,
                    ApproverRemark,
                    _userSessionService.UserId);

                MessageBox.Show("Supplier Return rejected successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                ApproverRemark = string.Empty;
                await LoadApprovalQueueAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error rejecting Supplier Return: {GetDetailedErrorMessage(ex)}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task OpenSupplierReturnReportAsync(int supplierReturnId)
        {
            DataTable reportData = await _supplierReturnRepository.GetSupplierReturnReportDataAsync(supplierReturnId);

            if (reportData == null || reportData.Rows.Count == 0)
            {
                MessageBox.Show("No data found for this Supplier Return Note.", "Export Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string tempFile = Path.Combine(Path.GetTempPath(), $"SRN_{supplierReturnId}_{DateTime.Now:yyyyMMdd_HHmm}.pdf");

            await Task.Run(() => ExportSupplierReturnReport(reportData, tempFile));

            Process.Start(new ProcessStartInfo(tempFile) { UseShellExecute = true });
        }

        private void ExportSupplierReturnReport(DataTable reportData, string filePath)
        {
            using (var report = new ReportDocument())
            {
                string reportPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Reports", "SupplierReturnNote.rpt");

                if (!File.Exists(reportPath))
                {
                    throw new FileNotFoundException("Crystal report file not found.", reportPath);
                }

                report.Load(reportPath);

                var ds = new SupplierReturnDS();
                ds.EnforceConstraints = false;
                ds.rptGetSupplierReturnNote.Merge(reportData);

                report.SetDataSource(ds);
                report.ExportToDisk(ExportFormatType.PortableDocFormat, filePath);
            }
        }

        private void RaiseActionCanExecuteChanged()
        {
            (ApproveCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
            (RejectCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        }

        private static bool IsPendingStatus(SupplierReturn supplierReturn)
        {
            if (supplierReturn == null || string.IsNullOrWhiteSpace(supplierReturn.Status))
            {
                return false;
            }

            return string.Equals(supplierReturn.Status, "PENDING_APPROVAL", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(supplierReturn.Status, SupplierReturnStatus.Pending.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        private static string GetDetailedErrorMessage(Exception ex)
        {
            return ex.GetBaseException()?.Message ?? ex.Message;
        }
    }
}
