using System.Collections.Generic;
using System.ComponentModel;
using System;

namespace PointOfSale.Core.Models.Security
{
    public class PermissionNode : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        protected bool SetProperty<T>(ref T backingField, T value, string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(backingField, value))
                return false;
            backingField = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        public int PermissionId { get; set; }
        public string Module { get; set; }       // e.g. "Inventory"
        public string PermissionKey { get; set; } // e.g. "PRODUCT_DELETE"
        public string Description { get; set; }

        private bool _isGranted;
        public bool IsGranted
        {
            get => _isGranted;
            set => SetProperty(ref _isGranted, value);
        }

        private DateTime? _validFrom;
        public DateTime? ValidFrom
        {
            get => _validFrom;
            set => SetProperty(ref _validFrom, value, nameof(ValidFrom));
        }

        private DateTime? _validTo;
        public DateTime? ValidTo
        {
            get => _validTo;
            set => SetProperty(ref _validTo, value, nameof(ValidTo));
        }
    }
}
