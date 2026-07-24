using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using PointOfSale.Core.Enums;

namespace PointOfSale.Core.Models.Purchasing
{
    public class Supplier : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public Supplier()
        {
            // Wire up the collection so DocumentsCount/HasDocuments stay in sync
            // without requiring a full property replacement.
            _documents = new ObservableCollection<SupplierDocument>();
            _documents.CollectionChanged += OnDocumentsCollectionChanged;
        }

        // 1. Core Identification
        public int SupplierId { get; set; }
        public string SupplierCode { get; set; }
        public string SupplierName { get; set; }

        // Legal & Compliance
        public string TaxRegistrationNumber { get; set; }
        public string BusinessRegistrationNumber { get; set; }

        // Contact Information
        public string Address { get; set; }
        public int ContactsCount { get; set; }
        public string ContactDetailsTooltip { get; set; }
        public List<SupplierContact> Contacts { get; set; } = new List<SupplierContact>();

        // Financial & Billing
        public SupplierPaymentMethod? DefaultPaymentMethod { get; set; }
        public bool IsCredit { get; set; }
        public int? CreditPeriodDays { get; set; }
        public decimal? CreditLimit { get; set; }

        // Bank Details
        public string BankName { get; set; }
        public string BankBranch { get; set; }
        public string AccountName { get; set; }
        public string AccountNumber { get; set; }

        // Documents — observable so the DataGrid column reacts immediately
        private ObservableCollection<SupplierDocument> _documents;
        public ObservableCollection<SupplierDocument> Documents
        {
            get => _documents;
            set
            {
                if (_documents != null)
                    _documents.CollectionChanged -= OnDocumentsCollectionChanged;

                _documents = value ?? new ObservableCollection<SupplierDocument>();
                _documents.CollectionChanged += OnDocumentsCollectionChanged;

                OnPropertyChanged();
                OnPropertyChanged(nameof(DocumentsCount));
                OnPropertyChanged(nameof(HasDocuments));
            }
        }

        public int DocumentsCount => _documents?.Count ?? 0;
        public bool HasDocuments => DocumentsCount > 0;

        private void OnDocumentsCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(DocumentsCount));
            OnPropertyChanged(nameof(HasDocuments));
        }

        // Audit & Status
        public bool IsActive { get; set; } = true;
        public int CreatedBy { get; set; }
        public int? UpdatedBy { get; set; }
    }
}
