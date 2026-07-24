using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
#if !NO_CRYSTAL_REPORTS
using CrystalDecisions.CrystalReports.Engine;
#endif
using PointOfSale.Core.DTOs;
using PointOfSale.Core.Interfaces.Repositories.Sales;
using PointOfSale.Core.Services;
using PointOfSale.UI.Commands;
#if !NO_CRYSTAL_REPORTS
using PointOfSale.UI.Views.Sales;
#endif

namespace PointOfSale.UI.ViewModels.Sales
{
    public class ShiftManagementViewModel : BaseViewModel
    {
        private readonly IShiftRepository _shiftRepository;
        private readonly IUserSessionService _userSessionService;

        public ShiftManagementViewModel(IShiftRepository shiftRepository, IUserSessionService userSessionService)
        {
            _shiftRepository = shiftRepository ?? throw new ArgumentNullException(nameof(shiftRepository));
            _userSessionService = userSessionService ?? throw new ArgumentNullException(nameof(userSessionService));

            ReconciliationShifts = new ObservableCollection<ShiftDto>();

            RefreshCommand = new AsyncRelayCommand(async _ => await LoadReconciliationShiftsAsync());
            PostShiftToLedgerCommand = new AsyncRelayCommand(async _ => await PostShiftToLedgerAsync(), _ => CanPostShiftToLedger());

            _ = LoadReconciliationShiftsAsync();
        }

        public ObservableCollection<ShiftDto> ReconciliationShifts { get; }

        private ShiftDto _selectedShift;
        public ShiftDto SelectedShift
        {
            get => _selectedShift;
            set
            {
                if (SetProperty(ref _selectedShift, value))
                {
                    ClearSelectedReconciliation();
                    OnPropertyChanged(nameof(HasSelectedShift));
                    (PostShiftToLedgerCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();

                    if (value != null)
                    {
                        _ = LoadSelectedReconciliationAsync(value.Id);
                    }
                }
            }
        }

        public bool HasSelectedShift => SelectedShift != null;

        private ShiftReconciliationDto _selectedReconciliation;
        public ShiftReconciliationDto SelectedReconciliation
        {
            get => _selectedReconciliation;
            private set
            {
                if (SetProperty(ref _selectedReconciliation, value))
                {
                    SelectedSystemCash = value?.SystemCash ?? 0;
                    SelectedPhysicalCash = value?.PhysicalCash ?? 0;
                    SelectedVariance = value?.Variance ?? 0;
                    ExpectedCardTotal = value?.ExpectedCardTotal ?? 0;
                    ExpectedCreditTotal = value?.ExpectedCreditTotal ?? 0;

                    if (value != null && string.IsNullOrWhiteSpace(ManagerNotes))
                    {
                        ManagerNotes = value.Notes;
                    }

                    OnPropertyChanged(nameof(HasSelectedReconciliation));
                    (PostShiftToLedgerCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        public bool HasSelectedReconciliation => SelectedReconciliation != null;

        private decimal _selectedSystemCash;
        public decimal SelectedSystemCash
        {
            get => _selectedSystemCash;
            private set => SetProperty(ref _selectedSystemCash, value);
        }

        private decimal _selectedPhysicalCash;
        public decimal SelectedPhysicalCash
        {
            get => _selectedPhysicalCash;
            private set => SetProperty(ref _selectedPhysicalCash, value);
        }

        private decimal _selectedVariance;
        public decimal SelectedVariance
        {
            get => _selectedVariance;
            private set
            {
                if (SetProperty(ref _selectedVariance, value))
                {
                    OnPropertyChanged(nameof(IsSelectedVarianceNegative));
                    OnPropertyChanged(nameof(IsSelectedVarianceNonNegative));
                }
            }
        }

        public bool IsSelectedVarianceNegative => SelectedVariance < 0;
        public bool IsSelectedVarianceNonNegative => SelectedVariance >= 0;

        private decimal _expectedCardTotal;
        public decimal ExpectedCardTotal
        {
            get => _expectedCardTotal;
            private set => SetProperty(ref _expectedCardTotal, value);
        }

        private decimal _expectedCreditTotal;
        public decimal ExpectedCreditTotal
        {
            get => _expectedCreditTotal;
            private set => SetProperty(ref _expectedCreditTotal, value);
        }

        private string _managerNotes;
        public string ManagerNotes
        {
            get => _managerNotes;
            set => SetProperty(ref _managerNotes, value);
        }

        public ICommand RefreshCommand { get; }
        public ICommand PostShiftToLedgerCommand { get; }

        private async Task LoadReconciliationShiftsAsync()
        {
            try
            {
                var previouslySelectedShiftId = SelectedShift?.Id;
                var shifts = await _shiftRepository.GetShiftsForReconciliationAsync(_userSessionService.BranchId);

                ReconciliationShifts.Clear();
                foreach (var shift in shifts.OrderByDescending(x => x.EndTime ?? x.StartTime))
                {
                    ReconciliationShifts.Add(shift);
                }

                SelectedShift = previouslySelectedShiftId.HasValue
                    ? ReconciliationShifts.FirstOrDefault(x => x.Id == previouslySelectedShiftId.Value)
                    : ReconciliationShifts.FirstOrDefault();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load shifts for audit: {ex.Message}", "Shift Reconciliation", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task LoadSelectedReconciliationAsync(int shiftId)
        {
            try
            {
                var reconciliation = await _shiftRepository.GetShiftReconciliationAsync(shiftId);

                if (SelectedShift == null || SelectedShift.Id != shiftId)
                {
                    return;
                }

                SelectedReconciliation = reconciliation;

                if (reconciliation == null)
                {
                    MessageBox.Show("No cashier close record was found for the selected shift.", "Shift Reconciliation", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load shift reconciliation: {ex.Message}", "Shift Reconciliation", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task PostShiftToLedgerAsync()
        {
            try
            {
                if (!CanPostShiftToLedger())
                {
                    return;
                }

                var result = MessageBox.Show(
                    "Verify this shift and prepare it for posting to the General Ledger?",
                    "Shift Reconciliation",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes)
                {
                    return;
                }

                var shiftId = SelectedShift.Id;

                // GL Posting logic here.
                await _shiftRepository.PostShiftToLedgerAsync(shiftId, _userSessionService.UserId, ManagerNotes);

                var reportShown = TryShowZReport(shiftId);

                MessageBox.Show(
                    reportShown
                        ? "Shift verified and prepared for General Ledger integration."
                        : "Shift verified and prepared for General Ledger integration. Z Report preview could not be opened.",
                    "Shift Reconciliation",
                    MessageBoxButton.OK,
                    reportShown ? MessageBoxImage.Information : MessageBoxImage.Warning);

                await LoadReconciliationShiftsAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to post shift to ledger: {ex.Message}", "Shift Reconciliation", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool CanPostShiftToLedger()
        {
            return SelectedShift != null && SelectedReconciliation != null;
        }

        private bool TryShowZReport(int shiftId)
        {
#if NO_CRYSTAL_REPORTS
            MessageBox.Show(
                "Crystal Reports is not installed on this machine. Install the SAP Crystal Reports runtime to preview the Z Report.",
                "Z Report",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return false;
#else
            try
            {
                var reportPath = ResolveZReportPath();
                var zReportDataSet = _shiftRepository.GetZReportDataSet(shiftId);

                ReportDocument reportDocument = null;

                try
                {
                    reportDocument = new ReportDocument();
                    reportDocument.Load(reportPath);
                    reportDocument.SetDataSource(zReportDataSet);

                    var viewerWindow = new ZReportViewerWindow(reportDocument)
                    {
                        Owner = Application.Current?.MainWindow
                    };

                    viewerWindow.ShowDialog();

                    return true;
                }
                finally
                {
                    if (reportDocument != null)
                    {
                        reportDocument.Close();
                        reportDocument.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Shift was posted, but the Z Report could not be opened: {ex.Message}",
                    "Z Report",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return false;
            }
#endif
        }

#if !NO_CRYSTAL_REPORTS
        private static string ResolveZReportPath()
        {
            var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            var candidatePaths = new[]
            {
                Path.Combine(baseDirectory, "Reports", "ZReport.rpt"),
                Path.Combine(baseDirectory, "ZReport.rpt"),
                Path.GetFullPath(Path.Combine(baseDirectory, @"..\..\Reports\ZReport.rpt"))
            };

            foreach (var candidatePath in candidatePaths)
            {
                if (File.Exists(candidatePath))
                {
                    return candidatePath;
                }
            }

            throw new FileNotFoundException(
                "Crystal report file not found. Expected ZReport.rpt under the application Reports folder.",
                candidatePaths[0]);
        }
#endif

        private void ClearSelectedReconciliation()
        {
            SelectedReconciliation = null;
            SelectedSystemCash = 0;
            SelectedPhysicalCash = 0;
            SelectedVariance = 0;
            ExpectedCardTotal = 0;
            ExpectedCreditTotal = 0;
            ManagerNotes = string.Empty;
        }
    }
}
