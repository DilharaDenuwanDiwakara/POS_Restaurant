using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using PointOfSale.Core.DTOs;
using PointOfSale.Core.Interfaces.Repositories.Restaurant;
using PointOfSale.Core.Interfaces.Repositories.System;
using PointOfSale.Core.Models.System;
using PointOfSale.Core.Services;
using PointOfSale.UI.Commands;

namespace PointOfSale.UI.ViewModels.Restaurant
{
    public class RestaurantReportsViewModel : BaseViewModel
    {
        private readonly IRestaurantReportRepository _reportRepository;
        private readonly IBranchRepository _branchRepository;
        private readonly IUserSessionService _userSessionService;
        private readonly AsyncRelayCommand _generateReportCommand;
        private readonly List<RepeatCustomerDetailDto> _allRepeatCustomers = new List<RepeatCustomerDetailDto>();
        private bool _isInitialized;

        public RestaurantReportsViewModel(
            IRestaurantReportRepository reportRepository,
            IBranchRepository branchRepository,
            IUserSessionService userSessionService)
        {
            _reportRepository = reportRepository ?? throw new ArgumentNullException(nameof(reportRepository));
            _branchRepository = branchRepository ?? throw new ArgumentNullException(nameof(branchRepository));
            _userSessionService = userSessionService ?? throw new ArgumentNullException(nameof(userSessionService));

            StartDate = DateTime.Today;
            EndDate = DateTime.Today;

            LoadedCommand = new AsyncRelayCommand(_ => LoadInitialDataAsync());
            _generateReportCommand = new AsyncRelayCommand(_ => GenerateReportAsync(), _ => !IsBusy);
            GenerateReportCommand = _generateReportCommand;
        }

        public ObservableCollection<Branch> Branches { get; } = new ObservableCollection<Branch>();
        public ObservableCollection<RepeatCustomerDetailDto> RepeatCustomers { get; } = new ObservableCollection<RepeatCustomerDetailDto>();
        public ObservableCollection<StewardPerformanceDto> StewardPerformance { get; } = new ObservableCollection<StewardPerformanceDto>();

        private Branch _selectedBranch;
        public Branch SelectedBranch
        {
            get => _selectedBranch;
            set => SetProperty(ref _selectedBranch, value);
        }

        private DateTime _startDate;
        public DateTime StartDate
        {
            get => _startDate;
            set => SetProperty(ref _startDate, value);
        }

        private DateTime _endDate;
        public DateTime EndDate
        {
            get => _endDate;
            set => SetProperty(ref _endDate, value);
        }

        private int _totalGuestsServed;
        public int TotalGuestsServed { get => _totalGuestsServed; set => SetProperty(ref _totalGuestsServed, value); }

        private int _totalTableVisits;
        public int TotalTableVisits { get => _totalTableVisits; set => SetProperty(ref _totalTableVisits, value); }

        private int _repeatCustomerCount;
        public int RepeatCustomerCount { get => _repeatCustomerCount; set => SetProperty(ref _repeatCustomerCount, value); }

        private string _highestRepeatCustomerDisplay = "—";
        public string HighestRepeatCustomerDisplay { get => _highestRepeatCustomerDisplay; set => SetProperty(ref _highestRepeatCustomerDisplay, value); }

        private string _topPerformingStewardDisplay = "—";
        public string TopPerformingStewardDisplay { get => _topPerformingStewardDisplay; set => SetProperty(ref _topPerformingStewardDisplay, value); }

        private decimal _totalNetSales;
        public decimal TotalNetSales { get => _totalNetSales; set => SetProperty(ref _totalNetSales, value); }

        private decimal _averagePerHeadValue;
        public decimal AveragePerHeadValue { get => _averagePerHeadValue; set => SetProperty(ref _averagePerHeadValue, value); }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                if (SetProperty(ref _isBusy, value))
                    _generateReportCommand?.RaiseCanExecuteChanged();
            }
        }

        private bool _isEmpty = true;
        public bool IsEmpty { get => _isEmpty; set => SetProperty(ref _isEmpty, value); }

        private bool _showTopThreeRepeatCustomers;
        public bool ShowTopThreeRepeatCustomers
        {
            get => _showTopThreeRepeatCustomers;
            set
            {
                if (SetProperty(ref _showTopThreeRepeatCustomers, value))
                {
                    OnPropertyChanged(nameof(RepeatCustomerSectionTitle));
                    RefreshRepeatCustomerDisplay();
                }
            }
        }

        public string RepeatCustomerSectionTitle => ShowTopThreeRepeatCustomers
            ? "Top 3 Repeat Customers"
            : "Repeat Customer Details";

        private ISeries[] _stewardServingSeries = new ISeries[0];
        public ISeries[] StewardServingSeries
        {
            get => _stewardServingSeries;
            set => SetProperty(ref _stewardServingSeries, value);
        }

        private Axis[] _stewardServingXAxes = new Axis[0];
        public Axis[] StewardServingXAxes
        {
            get => _stewardServingXAxes;
            set => SetProperty(ref _stewardServingXAxes, value);
        }

        public ICommand LoadedCommand { get; }
        public ICommand GenerateReportCommand { get; }

        private async Task LoadInitialDataAsync()
        {
            if (_isInitialized)
                return;

            try
            {
                IsBusy = true;
                ErrorMessage = string.Empty;

                Branches.Clear();
                Branches.Add(new Branch { Id = 0, Name = "ALL BRANCHES", IsActive = true });

                var branches = await _branchRepository.GetAllAsync();
                foreach (var branch in branches.Where(b => b.IsActive).OrderBy(b => b.Name))
                    Branches.Add(branch);

                SelectedBranch = Branches.FirstOrDefault(b => b.Id == _userSessionService.BranchId)
                    ?? Branches.FirstOrDefault();

                _isInitialized = true;
            }
            catch (Exception ex)
            {
                Trace.TraceError($"Restaurant reports initialization failed: {ex}");
                ErrorMessage = $"Unable to initialize Restaurant Reports: {ex.Message}";
                return;
            }
            finally
            {
                IsBusy = false;
            }

            await GenerateReportAsync();
        }

        private async Task GenerateReportAsync()
        {
            if (!ValidateFilters())
                return;

            try
            {
                IsBusy = true;
                ErrorMessage = string.Empty;

                int? branchId = SelectedBranch != null && SelectedBranch.Id > 0
                    ? SelectedBranch.Id
                    : (int?)null;

                var dashboard = await _reportRepository.GetDashboardAsync(branchId, StartDate, EndDate);
                ApplyDashboard(dashboard);
            }
            catch (Exception ex)
            {
                Trace.TraceError($"Restaurant dashboard generation failed: {ex}");
                ErrorMessage = $"Unable to generate Restaurant Reports: {ex.Message}";
                ClearDashboard();
            }
            finally
            {
                IsBusy = false;
            }
        }

        private bool ValidateFilters()
        {
            ErrorMessage = string.Empty;

            if (SelectedBranch == null)
            {
                ErrorMessage = "Please select a branch.";
                return false;
            }

            if (EndDate.Date < StartDate.Date)
            {
                ErrorMessage = "End Date cannot be earlier than Start Date.";
                return false;
            }

            return true;
        }

        private void ApplyDashboard(RestaurantDashboardDto dashboard)
        {
            var summary = dashboard?.Summary ?? new RestaurantDashboardSummaryDto();
            TotalGuestsServed = summary.TotalGuestsServed;
            TotalTableVisits = summary.TotalTableVisits;
            RepeatCustomerCount = summary.RepeatCustomerCount;
            TotalNetSales = summary.TotalNetSales;
            AveragePerHeadValue = summary.AveragePerHeadValue;

            TopPerformingStewardDisplay = string.IsNullOrWhiteSpace(summary.TopPerformingStewardName)
                ? "—"
                : $"{summary.TopPerformingStewardName} ({summary.TopPerformingStewardServingCount:N0})";

            _allRepeatCustomers.Clear();
            _allRepeatCustomers.AddRange((dashboard?.RepeatCustomers ?? Enumerable.Empty<RepeatCustomerDetailDto>())
                .OrderByDescending(customer => customer.VisitCount)
                .ThenByDescending(customer => customer.LastVisit)
                .ThenBy(customer => customer.CustomerName));

            UpdateHighestRepeatCustomer(summary);
            RefreshRepeatCustomerDisplay();

            StewardPerformance.Clear();
            foreach (var steward in dashboard?.StewardPerformance ?? Enumerable.Empty<StewardPerformanceDto>())
                StewardPerformance.Add(steward);

            IsEmpty = TotalTableVisits == 0;
            BuildStewardChart();
        }

        private void BuildStewardChart()
        {
            if (!StewardPerformance.Any())
            {
                StewardServingSeries = new ISeries[0];
                StewardServingXAxes = new Axis[0];
                return;
            }

            StewardServingSeries = new ISeries[]
            {
                new ColumnSeries<int>
                {
                    Name = "Table Visits",
                    Values = StewardPerformance.Select(s => s.ServingCount).ToArray()
                }
            };

            StewardServingXAxes = new[]
            {
                new Axis
                {
                    Labels = StewardPerformance.Select(s => s.StewardName).ToArray(),
                    LabelsRotation = 25,
                    MinStep = 1,
                    ForceStepToMin = true
                }
            };
        }

        private void UpdateHighestRepeatCustomer(RestaurantDashboardSummaryDto summary)
        {
            if (!_allRepeatCustomers.Any())
            {
                HighestRepeatCustomerDisplay = "—";
                return;
            }

            var highestVisitCount = _allRepeatCustomers.Max(customer => customer.VisitCount);
            var tiedNames = _allRepeatCustomers
                .Where(customer => customer.VisitCount == highestVisitCount)
                .Select(customer => customer.CustomerName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.CurrentCultureIgnoreCase)
                .OrderBy(name => name)
                .ToList();

            var names = tiedNames.Any()
                ? string.Join(" / ", tiedNames)
                : summary.HighestRepeatCustomerName;

            HighestRepeatCustomerDisplay = string.IsNullOrWhiteSpace(names)
                ? "—"
                : $"{names} ({highestVisitCount:N0})";
        }

        private void RefreshRepeatCustomerDisplay()
        {
            RepeatCustomers.Clear();

            var customers = ShowTopThreeRepeatCustomers
                ? _allRepeatCustomers.Take(3)
                : _allRepeatCustomers;

            foreach (var customer in customers)
                RepeatCustomers.Add(customer);
        }

        private void ClearDashboard()
        {
            TotalGuestsServed = 0;
            TotalTableVisits = 0;
            RepeatCustomerCount = 0;
            HighestRepeatCustomerDisplay = "—";
            TopPerformingStewardDisplay = "—";
            TotalNetSales = 0;
            AveragePerHeadValue = 0;
            _allRepeatCustomers.Clear();
            RepeatCustomers.Clear();
            StewardPerformance.Clear();
            IsEmpty = true;
            BuildStewardChart();
        }
    }
}
