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
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using Microsoft.Win32;
using PointOfSale.Core.DTOs;
using PointOfSale.Core.Interfaces.Repositories.Inventory;
using PointOfSale.Core.Interfaces.Repositories.System;
using PointOfSale.Core.Models.Inventory;
using PointOfSale.Core.Models.System;
using PointOfSale.UI.Commands;

namespace PointOfSale.UI.ViewModels.Inventory
{
    public class InventoryReportViewModel : BaseViewModel
    {
        private readonly IInventoryReportRepository _reportRepository;
        private readonly ICategoryRepository _categoryRepository;
        private readonly IBranchRepository _branchRepository;
        private readonly ILocationRepository _locationRepository;
        private readonly IProductRepository _productRepository;

        #region Constructor
        public InventoryReportViewModel(IInventoryReportRepository reportRepository,
            ICategoryRepository categoryRepository,
            IBranchRepository branchRepository,
            ILocationRepository locationRepository,
            IProductRepository productRepository)
        {
            _reportRepository = reportRepository;
            _categoryRepository = categoryRepository;
            _branchRepository = branchRepository;
            _locationRepository = locationRepository;
            _productRepository = productRepository;

            SelectedReportType = ReportTypes.First();

            GenerateReportCommand = new AsyncRelayCommand(_ => GeneratePdfReportAsync());

            _ = LoadInitialDataAsync();
        }
        #endregion

        #region Properties

        public ObservableCollection<string> ReportTypes { get; } = new ObservableCollection<string>
        {
            "Current Inventory Stock",
            "Reorder Level List",
            "Expiry Reach List",
            "Stock Movement Ledger"
        };

        private string _selectedReportType;
        public string SelectedReportType
        {
            get => _selectedReportType;
            set
            {
                if (SetProperty(ref _selectedReportType, value))
                {
                    OnPropertyChanged(nameof(IsLedgerReportSelected));
                    OnPropertyChanged(nameof(IsStandardReportSelected));
                }
            }
        }

        public bool IsLedgerReportSelected => SelectedReportType == "Stock Movement Ledger";
        public bool IsStandardReportSelected => !IsLedgerReportSelected;

        // Branch
        public ObservableCollection<Branch> Branches { get; } = new ObservableCollection<Branch>();

        private Branch _selectedBranch;
        public Branch SelectedBranch
        {
            get => _selectedBranch;
            set
            {
                if (SetProperty(ref _selectedBranch, value))
                    _ = LoadLocationsByBranchAsync();
            }
        }

        // Location — strictly from [Inventory].[Location], filtered by the selected branch
        public ObservableCollection<Location> Locations { get; } = new ObservableCollection<Location>();

        private Location _selectedLocation;
        public Location SelectedLocation
        {
            get => _selectedLocation;
            set
            {
                if (SetProperty(ref _selectedLocation, value))
                    _ = LoadCategoryChart();
            }
        }

        // Category (standard reports)
        public ObservableCollection<CategoryLookupItem> Categories { get; } = new ObservableCollection<CategoryLookupItem>();

        private CategoryLookupItem _selectedCategory;
        public CategoryLookupItem SelectedCategory
        {
            get => _selectedCategory;
            set => SetProperty(ref _selectedCategory, value);
        }

        // As-Of date (standard reports)
        private DateTime _reportDate = DateTime.Now;
        public DateTime ReportDate
        {
            get => _reportDate;
            set => SetProperty(ref _reportDate, value);
        }

        // Date range (Stock Movement Ledger)
        private DateTime _startDate = DateTime.Now.AddMonths(-1);
        public DateTime StartDate
        {
            get => _startDate;
            set => SetProperty(ref _startDate, value);
        }

        private DateTime _endDate = DateTime.Now;
        public DateTime EndDate
        {
            get => _endDate;
            set => SetProperty(ref _endDate, value);
        }

        // Product (Stock Movement Ledger)
        public ObservableCollection<Product> Products { get; } = new ObservableCollection<Product>();

        private Product _selectedProduct;
        public Product SelectedProduct
        {
            get => _selectedProduct;
            set => SetProperty(ref _selectedProduct, value);
        }

        // Chart
        private ISeries[] _inventoryValueSeries;
        public ISeries[] InventoryValueSeries
        {
            get => _inventoryValueSeries;
            set => SetProperty(ref _inventoryValueSeries, value);
        }

        private bool _isProcessing;
        public bool IsProcessing
        {
            get => _isProcessing;
            set => SetProperty(ref _isProcessing, value);
        }

        #endregion

        #region Commands
        public ICommand GenerateReportCommand { get; }
        #endregion

        #region Methods

        private async Task LoadInitialDataAsync()
        {
            try
            {
                IsProcessing = true;

                var branches = await _branchRepository.GetAllAsync();
                Branches.Clear();
                foreach (var b in branches)
                    Branches.Add(b);

                // Setting SelectedBranch triggers LoadLocationsByBranchAsync automatically.
                SelectedBranch = Branches.FirstOrDefault();

                var rawCategoryList = await _categoryRepository.GetAllAsync();
                Categories.Clear();
                Categories.Add(new CategoryLookupItem { Id = 0, DisplayName = "ALL CATEGORIES", Level = 0 });
                BuildHierarchy(rawCategoryList, 0, 0);
                SelectedCategory = Categories.First();

                var products = await _productRepository.GetAllAsync();
                Products.Clear();
                foreach (var p in products.Where(p => p.IsActive).OrderBy(p => p.ProductName))
                    Products.Add(p);
                SelectedProduct = Products.FirstOrDefault();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Init Failed: {ex.Message}";
            }
            finally
            {
                IsProcessing = false;
            }
        }

        // Cascading dropdown: called whenever SelectedBranch changes.
        private async Task LoadLocationsByBranchAsync()
        {
            Locations.Clear();
            SelectedLocation = null; // Force the user to pick a valid location for the new branch.

            if (_selectedBranch == null) return;

            try
            {
                var locations = await _locationRepository.GetByBranchIdAsync(_selectedBranch.Id);
                foreach (var loc in locations)
                    Locations.Add(loc);

                SelectedLocation = Locations.FirstOrDefault();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to load locations: {ex.Message}";
            }
        }

        // Chart uses the selected Location's Id — a proper [Inventory].[Location] key.
        private async Task LoadCategoryChart()
        {
            if (SelectedLocation == null) return;

            try
            {
                var data = await _reportRepository.GetInventoryValueByCategoryAsync(SelectedLocation.Id);

                InventoryValueSeries = data.Select(item => new PieSeries<double>
                {
                    Values = new double[] { (double)item.TotalValue },
                    Name = item.CategoryName,
                    ToolTipLabelFormatter = point => $"{point.Context.Series.Name}: {point.Coordinate.PrimaryValue:N2}",
                    DataLabelsPosition = LiveChartsCore.Measure.PolarLabelsPosition.Middle,
                    DataLabelsFormatter = point => $"{point.Coordinate.PrimaryValue:N0}"
                }).ToArray();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Chart Error: {ex.Message}");
            }
        }

        private async Task GeneratePdfReportAsync()
        {
            if (IsLedgerReportSelected)
            {
                await GenerateLedgerReportAsync();
                return;
            }

            if (SelectedBranch == null)
            {
                MessageBox.Show("Please select a Branch.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                IsProcessing = true;
                ErrorMessage = string.Empty;

                DataTable data = null;
                string reportFileName = "";
                string rptFileName = "";

                switch (SelectedReportType)
                {
                    case "Current Inventory Stock":
                        int? catId = (SelectedCategory?.Id == 0) ? (int?)null : (int?)SelectedCategory?.Id;
                        data = await _reportRepository.GetStockReportAsync(ReportDate, catId, SelectedBranch.Id);
                        reportFileName = "InventoryStock";
                        rptFileName = "InventoryReport.rpt";
                        break;

                    case "Reorder Level List":
                        data = await _reportRepository.GetReorderListAsync(SelectedBranch.Id);
                        reportFileName = "ReorderList";
                        rptFileName = "ReorderReport.rpt";
                        break;

                    case "Expiry Reach List":
                        data = await _reportRepository.GetExpiryListAsync(SelectedBranch.Id);
                        reportFileName = "ExpiryList";
                        rptFileName = "ExpiryReport.rpt";
                        break;
                }

                if (data == null || data.Rows.Count == 0)
                {
                    MessageBox.Show("No records found for this report.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                SaveFileDialog saveDialog = new SaveFileDialog
                {
                    Filter = "PDF Files (*.pdf)|*.pdf",
                    FileName = $"{reportFileName}_{DateTime.Now:yyyyMMdd_HHmm}.pdf",
                    Title = $"Save {SelectedReportType}"
                };

                if (saveDialog.ShowDialog() == true)
                    await Task.Run(() => ExportReportToDisk(data, saveDialog.FileName, rptFileName));
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error: {ex.Message}";
            }
            finally
            {
                IsProcessing = false;
            }
        }

        private async Task GenerateLedgerReportAsync()
        {
            if (SelectedBranch == null)
            {
                MessageBox.Show("Please select a Branch.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // SelectedLocation.Id is now a proper [Inventory].[Location] key, not a BranchId.
            if (SelectedLocation == null)
            {
                MessageBox.Show("Please select a Location.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (SelectedProduct == null)
            {
                MessageBox.Show("Please select a Product.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (StartDate > EndDate)
            {
                MessageBox.Show("Start Date cannot be after End Date.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                IsProcessing = true;
                ErrorMessage = string.Empty;

                var data = await _reportRepository.GetStockMovementLedgerAsync(
                    StartDate, EndDate, SelectedProduct.ProductId, SelectedLocation.Id);

                if (data == null || data.Rows.Count == 0)
                {
                    MessageBox.Show("No movement records found for the selected product and date range.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                SaveFileDialog saveDialog = new SaveFileDialog
                {
                    Filter = "PDF Files (*.pdf)|*.pdf",
                    FileName = $"StockLedger_{SelectedProduct.ProductCode}_{DateTime.Now:yyyyMMdd_HHmm}.pdf",
                    Title = "Save Stock Movement Ledger"
                };

                if (saveDialog.ShowDialog() == true)
                    await Task.Run(() => ExportLedgerReportToDisk(data, saveDialog.FileName));
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error: {ex.Message}";
            }
            finally
            {
                IsProcessing = false;
            }
        }

        private void BuildHierarchy(IEnumerable<Category> allCategories, int parentId, int level)
        {
            var children = allCategories
                .Where(c => (c.ParentCategoryId ?? 0) == parentId)
                .OrderBy(c => c.Name);

            foreach (var cat in children)
            {
                Categories.Add(new CategoryLookupItem
                {
                    Id = cat.CategoryId,
                    DisplayName = cat.Name,
                    Level = level
                });

                BuildHierarchy(allCategories, cat.CategoryId, level + 1);
            }
        }

        private void ExportReportToDisk(DataTable data, string filePath, string rptFileName)
        {
            using (ReportDocument report = new ReportDocument())
            {
                string reportPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Reports", rptFileName);
                if (!File.Exists(reportPath)) throw new FileNotFoundException($"Report file missing: {rptFileName}");

                report.Load(reportPath);
                report.SetDataSource(data);

                if (report.ParameterFields["LocationName"] != null)
                    report.SetParameterValue("LocationName", SelectedBranch.Name);

                if (report.ParameterFields["ReportDateParam"] != null)
                    report.SetParameterValue("ReportDateParam", DateTime.Now);

                report.ExportToDisk(ExportFormatType.PortableDocFormat, filePath);
            }

            Application.Current.Dispatcher.Invoke(() =>
            {
                if (MessageBox.Show("Report saved. Open now?", "Success", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                    Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });
            });
        }

        private void ExportLedgerReportToDisk(DataTable data, string filePath)
        {
            const string rptFileName = "StockMovementLedger.rpt";

            using (ReportDocument report = new ReportDocument())
            {
                string reportPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Reports", rptFileName);
                if (!File.Exists(reportPath)) throw new FileNotFoundException($"Report file missing: {rptFileName}");

                report.Load(reportPath);
                report.SetDataSource(data);

                if (report.ParameterFields["@StartDate"] != null)
                    report.SetParameterValue("@StartDate", StartDate);

                if (report.ParameterFields["@EndDate"] != null)
                    report.SetParameterValue("@EndDate", EndDate);

                report.ExportToDisk(ExportFormatType.PortableDocFormat, filePath);
            }

            Application.Current.Dispatcher.Invoke(() =>
            {
                if (MessageBox.Show("Report saved. Open now?", "Success", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                    Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });
            });
        }

        #endregion
    }
}
