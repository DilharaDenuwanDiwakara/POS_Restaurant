using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using PointOfSale.Core.Models.Inventory;

namespace PointOfSale.UI.ViewModels.Inventory
{
    public class StockTransferSummaryModel : BaseViewModel
    {
        private readonly Func<long, Task<IEnumerable<StockTransferLine>>> _loadLineItemsAsync;
        private readonly Action<Exception> _onLoadFailed;

        public StockTransferSummaryModel(
            StockTransfer source,
            Func<long, Task<IEnumerable<StockTransferLine>>> loadLineItemsAsync,
            Action<Exception> onLoadFailed)
        {
            Source = source ?? throw new ArgumentNullException(nameof(source));
            _loadLineItemsAsync = loadLineItemsAsync ?? throw new ArgumentNullException(nameof(loadLineItemsAsync));
            _onLoadFailed = onLoadFailed;
        }

        public StockTransfer Source { get; }

        public long TransferId => Source.TransferId;
        public string TransferNumber => Source.TransferNumber;
        public string FromLocationName => Source.FromLocationName;
        public string ToLocationName => Source.ToLocationName;
        public DateTime TransferDate => Source.TransferDate;
        public string Note => Source.Note;
        public string Status => Source.Status;
        public string Username => Source.Username;

        private ObservableCollection<StockTransferLine> _lineItems;
        public ObservableCollection<StockTransferLine> LineItems
        {
            get => _lineItems;
            private set => SetProperty(ref _lineItems, value);
        }

        private bool _isExpanded;
        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (!SetProperty(ref _isExpanded, value))
                    return;

                if (_isExpanded && LineItems == null)
                {
                    _ = LoadLineItemsAsync();
                }
            }
        }

        private bool _isLoadingLineItems;
        public bool IsLoadingLineItems
        {
            get => _isLoadingLineItems;
            private set => SetProperty(ref _isLoadingLineItems, value);
        }

        private async Task LoadLineItemsAsync()
        {
            if (IsLoadingLineItems)
                return;

            try
            {
                IsLoadingLineItems = true;
                var lineItems = await _loadLineItemsAsync(TransferId);
                LineItems = new ObservableCollection<StockTransferLine>(lineItems);
            }
            catch (Exception ex)
            {
                _onLoadFailed?.Invoke(ex);
                LineItems = new ObservableCollection<StockTransferLine>();
            }
            finally
            {
                IsLoadingLineItems = false;
            }
        }
    }
}
