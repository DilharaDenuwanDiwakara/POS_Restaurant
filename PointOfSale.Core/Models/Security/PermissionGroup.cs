using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace PointOfSale.Core.Models.Security
{
    public class PermissionGroup : INotifyPropertyChanged
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
        public string GroupName { get; set; }
        public ObservableCollection<PermissionNode> Permissions { get; set; } = new ObservableCollection<PermissionNode>();

        private bool _isAllSelected;
        public bool IsAllSelected
        {
            get => _isAllSelected;
            set
            {
                if (SetProperty(ref _isAllSelected, value))
                {
                    // When "Select All" is clicked, check/uncheck all children
                    foreach (var p in Permissions) p.IsGranted = value;
                }
            }
        }

    }
}
