using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using PointOfSale.Core.DTOs;

namespace PointOfSale.UI.ViewModels.Sales
{
    public class InvoiceSummaryModel : BaseViewModel
    {
        private readonly Func<long, Task<List<SalesLineItemDto>>> _loadLineItemsAsync;
        private readonly Action<Exception> _onLoadFailed;

        public InvoiceSummaryModel(
            SalesListDto source,
            Func<long, Task<List<SalesLineItemDto>>> loadLineItemsAsync,
            Action<Exception> onLoadFailed)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            _loadLineItemsAsync = loadLineItemsAsync ?? throw new ArgumentNullException(nameof(loadLineItemsAsync));
            _onLoadFailed = onLoadFailed;

            SalesId = source.SalesId;
            Date = source.SalesDate;
            InvoiceNo = source.InvoiceNumber;
            CustomerName = source.CustomerName;
            NetAmount = source.NetAmount;
            PaymentMethod = source.PaymentStatus;
        }

        public long SalesId { get; }
        public DateTime Date { get; }
        public string InvoiceNo { get; }
        public string CustomerName { get; }
        public decimal NetAmount { get; }
        public string PaymentMethod { get; }

        private ObservableCollection<InvoiceLineItemModel> _lineItems;
        public ObservableCollection<InvoiceLineItemModel> LineItems
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
                var lineItems = await _loadLineItemsAsync(SalesId);
                var models = new ObservableCollection<InvoiceLineItemModel>();

                foreach (var item in lineItems)
                {
                    models.Add(new InvoiceLineItemModel
                    {
                        ItemName = item.ItemName,
                        Qty = item.Quantity,
                        UnitPrice = item.UnitPrice,
                        StoredTotal = item.LineTotal
                    });
                }

                LineItems = models;
            }
            catch (Exception ex)
            {
                _onLoadFailed?.Invoke(ex);
                LineItems = new ObservableCollection<InvoiceLineItemModel>();
            }
            finally
            {
                IsLoadingLineItems = false;
            }
        }
    }
}
