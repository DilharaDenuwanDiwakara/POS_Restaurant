using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Diagnostics;
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
    public class GoodsReceiveNoteApprovalViewModel : BaseViewModel
    {
        private readonly IGoodsReceiveNoteRepository _goodsReceiveNoteRepository;
        private readonly ISupplierRepository _supplierRepository;
        private readonly IUserSessionService _userSessionService;

        public GoodsReceiveNoteApprovalViewModel(
            IGoodsReceiveNoteRepository goodsReceiveNoteRepository,
            ISupplierRepository supplierRepository,
            IUserSessionService userSessionService)
        {
            _goodsReceiveNoteRepository = goodsReceiveNoteRepository;
            _supplierRepository = supplierRepository;
            _userSessionService = userSessionService;

            ApprovalQueue = new ObservableCollection<GoodsReceiveNote>();
            SelectedGRNLines = new ObservableCollection<GoodsReceiveNoteLine>();

            SearchCommand = new AsyncRelayCommand(async _ => await LoadApprovalQueueAsync());
            ApproveCommand = new AsyncRelayCommand(async _ => await ApproveAsync(), _ => CanApprove);
            RejectCommand = new AsyncRelayCommand(async _ => await RejectAsync(), _ => CanReject);

            _ = LoadSuppliersAsync();
            _ = LoadApprovalQueueAsync();
        }

        public ObservableCollection<GoodsReceiveNote> ApprovalQueue { get; }
        public ObservableCollection<GoodsReceiveNoteLine> SelectedGRNLines { get; }

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

        private GoodsReceiveNote _selectedGRN;
        public GoodsReceiveNote SelectedGRN
        {
            get => _selectedGRN;
            set
            {
                if (SetProperty(ref _selectedGRN, value))
                {
                    ApproverRemark = string.Empty;
                    (ApproveCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
                    (RejectCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
                    _ = LoadSelectedGRNLinesAsync();
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

        private decimal _selectedGRNSubTotal;
        public decimal SelectedGRNSubTotal
        {
            get => _selectedGRNSubTotal;
            private set => SetProperty(ref _selectedGRNSubTotal, value);
        }

        private decimal _selectedGRNDiscount;
        public decimal SelectedGRNDiscount
        {
            get => _selectedGRNDiscount;
            private set => SetProperty(ref _selectedGRNDiscount, value);
        }

        private decimal _selectedGRNNetAmount;
        public decimal SelectedGRNNetAmount
        {
            get => _selectedGRNNetAmount;
            private set => SetProperty(ref _selectedGRNNetAmount, value);
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

                var items = await _goodsReceiveNoteRepository.GetPendingApprovalsAsync(
                    _userSessionService.BranchId,
                    supplierId,
                    SearchDateFrom,
                    SearchDateTo);

                var pendingItems = items.Where(x => HasStatus(x, GoodsReceiveNoteStatus.PENDING_APPROVAL));

                ApprovalQueue.Clear();
                foreach (var item in pendingItems)
                {
                    ApprovalQueue.Add(item);
                }

                if (ApprovalQueue.Count == 0)
                {
                    SelectedGRN = null;
                    SelectedGRNLines.Clear();
                    SelectedGRNSubTotal = 0;
                    SelectedGRNDiscount = 0;
                    SelectedGRNNetAmount = 0;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading GRN approval queue: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private async Task LoadSelectedGRNLinesAsync()
        {
            SelectedGRNLines.Clear();
            SelectedGRNSubTotal = 0;
            SelectedGRNDiscount = 0;
            SelectedGRNNetAmount = 0;

            if (SelectedGRN == null)
            {
                return;
            }

            try
            {
                var lines = await _goodsReceiveNoteRepository.GetLinesByGRNIdAsync(SelectedGRN.GoodsReceiveNoteId);
                foreach (var line in lines)
                {
                    SelectedGRNLines.Add(line);
                }

                SelectedGRNSubTotal = SelectedGRN.SubTotal;
                SelectedGRNDiscount = SelectedGRN.DiscountAmount;
                SelectedGRNNetAmount = SelectedGRN.TotalAmount;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading GRN lines: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool CanApprove =>
            SelectedGRN != null &&
            HasStatus(SelectedGRN, GoodsReceiveNoteStatus.PENDING_APPROVAL);
        private async Task ApproveAsync()
        {
            if (!CanApprove)
            {
                return;
            }

            try
            {
                long approvedGrnId = SelectedGRN.GoodsReceiveNoteId;
                string approvedGrnNumber = SelectedGRN.GoodsReceiveNoteNumber;

                await _goodsReceiveNoteRepository.ApproveRejectAsync(
                    approvedGrnId,
                    true,
                    _userSessionService.UserId,
                    ApproverRemark);

                MessageBox.Show("GRN approved successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                ApproverRemark = string.Empty;
                await LoadApprovalQueueAsync();

                await OpenGoodsReceiveNoteReportAsync(approvedGrnId);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error approving GRN: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool CanReject =>
            SelectedGRN != null &&
            HasStatus(SelectedGRN, GoodsReceiveNoteStatus.PENDING_APPROVAL) &&
            !string.IsNullOrWhiteSpace(ApproverRemark);
        private async Task RejectAsync()
        {
            if (!CanReject)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(ApproverRemark))
            {
                MessageBox.Show("Please enter a rejection remark.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                await _goodsReceiveNoteRepository.ApproveRejectAsync(
                    SelectedGRN.GoodsReceiveNoteId,
                    false,
                    _userSessionService.UserId,
                    ApproverRemark);

                MessageBox.Show("GRN rejected successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                ApproverRemark = string.Empty;
                await LoadApprovalQueueAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error rejecting GRN: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task OpenGoodsReceiveNoteReportAsync(long goodsReceiveNoteId)
        {
            DataTable reportData = await _goodsReceiveNoteRepository.GetGoodsReceiveNoteReportDataAsync(goodsReceiveNoteId);

            if (reportData == null || reportData.Rows.Count == 0)
            {
                MessageBox.Show("No data found for this Goods Receive Note.", "Export Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string tempFile = Path.Combine(Path.GetTempPath(), $"GRN_{goodsReceiveNoteId}_{DateTime.Now:yyyyMMdd_HHmm}.pdf");

            await Task.Run(() => ExportGoodsReceiveNoteReport(reportData, tempFile));

            Process.Start(new ProcessStartInfo(tempFile) { UseShellExecute = true });
        }
        private void ExportGoodsReceiveNoteReport(DataTable reportData, string filePath)
        {
            using (var report = new ReportDocument())
            {
                string reportPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Reports", "GoodsReceiveNote.rpt");

                if (!File.Exists(reportPath))
                {
                    throw new FileNotFoundException("Crystal report file not found.", reportPath);
                }

                report.Load(reportPath);

                var ds = new GoodsReceiveDS();
                ds.EnforceConstraints = false;
                ds.rptGetGoodsReceiveNote.Merge(reportData);

                report.SetDataSource(ds);
                report.ExportToDisk(ExportFormatType.PortableDocFormat, filePath);
            }
        }
        private static bool HasStatus(GoodsReceiveNote goodsReceiveNote, GoodsReceiveNoteStatus expectedStatus)
        {
            if (goodsReceiveNote == null || string.IsNullOrWhiteSpace(goodsReceiveNote.Status))
            {
                return false;
            }

            return Enum.TryParse(goodsReceiveNote.Status, true, out GoodsReceiveNoteStatus currentStatus) &&
                   currentStatus == expectedStatus;
        }
    }
}
