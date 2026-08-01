using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using PointOfSale.Core.Models.Purchasing;

namespace PointOfSale.UI.ViewModels.Purchasing
{
    public class PurchaseOrderSummaryModel : BaseViewModel
    {
        private readonly Func<long, Task<IEnumerable<GoodsPurchaseNoteLine>>> _loadLineItemsAsync;
        private readonly Action<Exception> _onLoadFailed;

        public PurchaseOrderSummaryModel(
            GoodPurchaseNote source,
            Func<long, Task<IEnumerable<GoodsPurchaseNoteLine>>> loadLineItemsAsync,
            Action<Exception> onLoadFailed)
        {
            Source = source ?? throw new ArgumentNullException(nameof(source));
            _loadLineItemsAsync = loadLineItemsAsync ?? throw new ArgumentNullException(nameof(loadLineItemsAsync));
            _onLoadFailed = onLoadFailed;
        }

        public GoodPurchaseNote Source { get; }

        public long GoodsPurchaseNoteId => Source.GoodsPurchaseNoteId;
        public string PONumber => Source.PONumber;
        public string SupplierName => Source.SupplierName;
        public decimal TaxAmount => Source.TaxAmount;
        public decimal TotalAmount => Source.TotalAmount;
        public string Note => Source.Note;
        public string Notes => Source.Notes;
        public string OrderBy => Source.OrderBy;
        public DateTime OrderDate => Source.OrderDate;
        public DateTime? ExpectedDeliveryDate => Source.ExpectedDeliveryDate;
        public string Status => Source.Status;
        public string Username => Source.Username;
        public bool IsDraft => Source.IsDraft;

        private ObservableCollection<GoodsPurchaseNoteLine> _lineItems;
        public ObservableCollection<GoodsPurchaseNoteLine> LineItems
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
                var lineItems = await _loadLineItemsAsync(GoodsPurchaseNoteId);
                LineItems = new ObservableCollection<GoodsPurchaseNoteLine>(lineItems);
            }
            catch (Exception ex)
            {
                _onLoadFailed?.Invoke(ex);
                LineItems = new ObservableCollection<GoodsPurchaseNoteLine>();
            }
            finally
            {
                IsLoadingLineItems = false;
            }
        }
    }
}
