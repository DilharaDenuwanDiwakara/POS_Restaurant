using PointOfSale.Core.DTOs;
using PointOfSale.Core.Models.Sales;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PointOfSale.UI.ViewModels.Sales
{
    public class SalesReturnModel : BaseViewModel
    {
        public SalesReturnModel(
            string returnNo,
            string invoiceNo,
            decimal totalRefundAmount,
            DateTime createDate,
            IEnumerable<SalesReturnLineModel> lines)
        {
            ReturnNo = returnNo;
            InvoiceNo = invoiceNo;
            TotalRefundAmount = totalRefundAmount;
            CreateDate = createDate;

            ReturnLines = new ObservableCollection<SalesReturnLineModel>(lines);
        }

        public string ReturnNo { get; }
        public string InvoiceNo { get; }
        public decimal TotalRefundAmount { get; }
        public DateTime CreateDate { get; }
        public ObservableCollection<SalesReturnLineModel> ReturnLines { get; }

        private bool _isExpanded;
        public bool IsExpanded
        {
            get => _isExpanded;
            set => SetProperty(ref _isExpanded, value);
        }
    }
}