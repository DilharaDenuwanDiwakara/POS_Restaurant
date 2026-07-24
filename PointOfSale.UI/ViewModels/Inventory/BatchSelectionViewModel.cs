using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using PointOfSale.Core.Models.Inventory;
using PointOfSale.UI.Commands;

namespace PointOfSale.UI.ViewModels.Inventory
{
    public enum BatchSelectionContext { Sales, SupplierReturn, StockTransfer }

    public class BatchSelectionViewModel : BaseViewModel
    {
        private ProductBatch _selectedBatch;
        public ProductBatch SelectedBatch
        {
            get => _selectedBatch;
            set => SetProperty(ref _selectedBatch, value);
        }

        public ObservableCollection<ProductBatch> AvailableBatches { get; }

        public BatchSelectionContext Context { get; }

        #region Command 
        public ICommand SelectedBatchCommand { get; }
        public ICommand CancelCommand { get; }

        #endregion

        public BatchSelectionViewModel(ObservableCollection<ProductBatch> availableBatches, BatchSelectionContext context)
        {
            AvailableBatches = new ObservableCollection<ProductBatch>(availableBatches);
            Context = context;

            SelectedBatchCommand = new RelayCommand(OnSelectBatch, CanSelectBatch);
            CancelCommand = new RelayCommand(OnCancel);

            SelectedBatch = availableBatches.FirstOrDefault();
        }

        private void OnSelectBatch(object parameter)
        {
            // The view will handle closing the dialog with a result
        }

        private bool CanSelectBatch(object parameter)
        {
            return SelectedBatch != null;
        }

        private void OnCancel(object parameter)
        {
            // The view will handle closing the dialog with a cancellation result
        }
    }
}
