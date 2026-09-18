using System;
using System.Collections.ObjectModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using CrystalDecisions.Shared;
using PointOfSale.Core.Interfaces.Repositories.Inventory;
using PointOfSale.Core.Models.Inventory;
using PointOfSale.Core.Services;
using PointOfSale.UI.Commands;
using PointOfSale.UI.Reports;

namespace PointOfSale.UI.ViewModels.Inventory
{
    public class WastageApprovalViewModel : BaseViewModel
    {
        private readonly IWastageRepository _wastageRepository;
        private readonly IUserSessionService _userSessionService;
        private PendingWastageModel _selectedWastage;
        private string _approvalRemarks;

        public WastageApprovalViewModel(
            IWastageRepository wastageRepository,
            IUserSessionService userSessionService)
        {
            _wastageRepository = wastageRepository ?? throw new ArgumentNullException(nameof(wastageRepository));
            _userSessionService = userSessionService ?? throw new ArgumentNullException(nameof(userSessionService));

            PendingWastageQueue = new ObservableCollection<PendingWastageModel>();
            SelectedWastageItems = new ObservableCollection<WastageDetailModel>();

            LoadPendingCommand = new AsyncRelayCommand(async _ => await LoadPendingAsync());
            ApproveCommand = new AsyncRelayCommand(async _ => await ApproveAsync(), _ => CanApprove);
            RejectCommand = new AsyncRelayCommand(async _ => await RejectAsync(), _ => CanReject);

            _ = LoadPendingAsync();
        }

        public ObservableCollection<PendingWastageModel> PendingWastageQueue { get; }
        public ObservableCollection<WastageDetailModel> SelectedWastageItems { get; }

        public PendingWastageModel SelectedWastage
        {
            get => _selectedWastage;
            set
            {
                if (SetProperty(ref _selectedWastage, value))
                {
                    RaiseCommandStates();
                    _ = LoadSelectedWastageItemsAsync();
                }
            }
        }

        public string ApprovalRemarks
        {
            get => _approvalRemarks;
            set
            {
                if (SetProperty(ref _approvalRemarks, value))
                {
                    RaiseCommandStates();
                }
            }
        }

        public decimal SelectedWastageTotalCost => SelectedWastageItems.Sum(item => item.TotalCost);

        public ICommand LoadPendingCommand { get; }
        public ICommand ApproveCommand { get; }
        public ICommand RejectCommand { get; }

        private bool CanApprove => SelectedWastage != null && IsPending(SelectedWastage);

        private bool CanReject =>
            SelectedWastage != null &&
            IsPending(SelectedWastage) &&
            !string.IsNullOrWhiteSpace(ApprovalRemarks);

        private async Task LoadPendingAsync()
        {
            try
            {
                var selectedWastageId = SelectedWastage?.WastageId;
                var items = await _wastageRepository.GetPendingWastageAsync(_userSessionService.BranchId);

                PendingWastageQueue.Clear();
                foreach (var item in items)
                {
                    PendingWastageQueue.Add(item);
                }

                SelectedWastage = selectedWastageId.HasValue
                    ? PendingWastageQueue.FirstOrDefault(item => item.WastageId == selectedWastageId.Value)
                    : PendingWastageQueue.FirstOrDefault();

                if (SelectedWastage == null)
                {
                    SelectedWastageItems.Clear();
                    OnPropertyChanged(nameof(SelectedWastageTotalCost));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading wastage approval queue: {ex.Message}", "Wastage Approval", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task LoadSelectedWastageItemsAsync()
        {
            SelectedWastageItems.Clear();
            OnPropertyChanged(nameof(SelectedWastageTotalCost));

            if (SelectedWastage == null)
            {
                return;
            }

            try
            {
                var details = await _wastageRepository.GetWastageDetailsAsync(SelectedWastage.WastageId);
                foreach (var detail in details)
                {
                    SelectedWastageItems.Add(detail);
                }

                OnPropertyChanged(nameof(SelectedWastageTotalCost));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading wastage details: {ex.Message}", "Wastage Approval", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task ApproveAsync()
        {
            if (!CanApprove)
            {
                return;
            }

            try
            {
                long approvedWastageId = SelectedWastage.WastageId;

                await _wastageRepository.ApproveWastageAsync(approvedWastageId, _userSessionService.UserId);

                MessageBox.Show("Wastage approved successfully.", "Wastage Approval", MessageBoxButton.OK, MessageBoxImage.Information);
                ApprovalRemarks = string.Empty;
                await LoadPendingAsync();

                await OpenWastageReportAsync(approvedWastageId);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error approving wastage: {ex.Message}", "Wastage Approval", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task RejectAsync()
        {
            if (!CanReject)
            {
                return;
            }

            try
            {
                await _wastageRepository.RejectWastageAsync(
                    SelectedWastage.WastageId,
                    _userSessionService.UserId,
                    ApprovalRemarks);

                MessageBox.Show("Wastage rejected successfully.", "Wastage Approval", MessageBoxButton.OK, MessageBoxImage.Information);
                ApprovalRemarks = string.Empty;
                await LoadPendingAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error rejecting wastage: {ex.Message}", "Wastage Approval", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task OpenWastageReportAsync(long wastageId)
        {
            DataTable reportData = await _wastageRepository.GetWastageReportDataAsync(wastageId);

            if (reportData == null || reportData.Rows.Count == 0)
            {
                MessageBox.Show("No data found for this Wastage Note.", "Wastage Approval", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string tempFile = Path.Combine(Path.GetTempPath(), $"Wastage_{wastageId}_{DateTime.Now:yyyyMMdd_HHmm}.pdf");

            await Task.Run(() => ExportWastageReport(reportData, wastageId, tempFile));

            Process.Start(new ProcessStartInfo(tempFile) { UseShellExecute = true });
        }

        private void ExportWastageReport(DataTable reportData, long wastageId, string filePath)
        {
            using (var report = new WastageNoteReport())
            {
                report.SetDataSource(reportData);
                TrySetWastageIdParameter(report, wastageId);
                report.ExportToDisk(ExportFormatType.PortableDocFormat, filePath);
            }
        }

        private static void TrySetWastageIdParameter(WastageNoteReport report, long wastageId)
        {
            try
            {
                report.SetParameterValue("@WastageId", wastageId);
            }
            catch
            {
                // The report is already bound to the retrieved data when no runtime parameter is exposed.
            }
        }

        private void RaiseCommandStates()
        {
            (ApproveCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
            (RejectCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        }

        private static bool IsPending(PendingWastageModel wastage)
        {
            return string.Equals(wastage?.Status?.Trim(), "PENDING", StringComparison.OrdinalIgnoreCase);
        }
    }
}
