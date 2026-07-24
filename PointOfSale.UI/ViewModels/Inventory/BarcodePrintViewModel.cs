using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;
using PointOfSale.Core.Interfaces.Repositories.Inventory;
using PointOfSale.Core.Interfaces.Services;
using PointOfSale.Core.Models.Inventory;
using PointOfSale.Infrastructure.Service;
using PointOfSale.UI.Commands;

namespace PointOfSale.UI.ViewModels.Inventory
{
    public class BarcodePrintViewModel : BaseViewModel
    {
        private readonly IProductRepository _productRepository;
        private readonly PrintService _printService;

        // The list of items currently in the "Print Queue"
        public ObservableCollection<PrintItem> PrintQueue { get; set; }

        // Search properties
        private string _searchTerm;
        public string SearchTerm
        {
            get => _searchTerm;
            set { _searchTerm = value; OnPropertyChanged(); }
        }

        public ICommand SearchCommand { get; set; }
        public ICommand AddToQueueCommand { get; set; }
        public ICommand PrintAllCommand { get; set; }

        // Temporary list to show search results
        public ObservableCollection<Product> SearchResults { get; set; }
        public BarcodePrintViewModel(IProductRepository productRepository)
        {
            _productRepository = productRepository;

            IBarcodeService barcodeService = new BarcodeService();

            _printService = new PrintService(barcodeService);
            PrintQueue = new ObservableCollection<PrintItem>();
            SearchResults = new ObservableCollection<Product>();

            SearchCommand = new RelayCommand(async (o) => await SearchProducts());
            AddToQueueCommand = new RelayCommand(AddToQueue);
            PrintAllCommand = new RelayCommand(PrintSelected);
        }

        private async Task SearchProducts()
        {
            if (string.IsNullOrWhiteSpace(SearchTerm)) return;

            var results = await _productRepository.SearchProductAsync(SearchTerm);
            SearchResults.Clear();
            foreach (var p in results)
            {
                SearchResults.Add(p);
            }
        }

        private void AddToQueue(object selectedProductObj)
        {
            if (selectedProductObj is Product product)
            {
                // Check if already in queue to avoid duplicates (optional)
                if (PrintQueue.Any(x => x.ProductCode == product.ProductCode)) return;

                PrintQueue.Add(new PrintItem
                {
                    ProductCode = product.ProductCode,
                    ProductName = product.ProductName,
                    QtyToPrint = 1,
                    IsSelected = true
                });

                // Clear search to reset UI
                SearchResults.Clear();
                SearchTerm = string.Empty;
            }
        }

        private void PrintSelected(object obj)
        {
            var items = PrintQueue.Where(x => x.IsSelected).ToList();
            if (!items.Any()) return;

            PrintDialog printDialog = new PrintDialog();

            if (printDialog.ShowDialog() == true)
            {
                foreach (var item in items)
                {
                    for (int i = 0; i < item.QtyToPrint; i++)
                    {
                        _printService.PrintPharmacyLabel(printDialog, item.ProductCode, item.ProductName);
                    }
                }
            }

            // Clear printed items
            PrintQueue.Clear();
        }

        public class PrintItem : BaseViewModel
        {
            public string ProductCode { get; set; }
            public string ProductName { get; set; }
            public decimal SellingPrice { get; set; }

            private bool _isSelected = true;
            public bool IsSelected
            {
                get => _isSelected;
                set { _isSelected = value; OnPropertyChanged(); }
            }

            private int _qtyToPrint = 1;
            public int QtyToPrint
            {
                get => _qtyToPrint;
                set { _qtyToPrint = value; OnPropertyChanged(); }
            }
        }
    }
}
