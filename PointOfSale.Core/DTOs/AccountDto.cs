using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PointOfSale.Core.DTOs
{
    public class AccountDto : INotifyPropertyChanged
    {
        private int _id;
        private int? _parentAccountId;
        private string _code;
        private string _name;
        private string _description;
        private int _accountTypeId;
        private bool _isActive;
        private bool _isHeader;
        private int _createdBy;
        private DateTime _createdAt;
        private int? _updatedBy;
        private DateTime? _updatedAt;
        private int _level;
        private string _displayName;
        private string _accountTypeName;
        private int _hierarchyLevel;

        public event PropertyChangedEventHandler PropertyChanged;

        public int Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }

        public int? ParentAccountId
        {
            get => _parentAccountId;
            set => SetProperty(ref _parentAccountId, value);
        }

        public string Code
        {
            get => _code;
            set => SetProperty(ref _code, value);
        }

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public string Description
        {
            get => _description;
            set => SetProperty(ref _description, value);
        }

        public int AccountTypeId
        {
            get => _accountTypeId;
            set => SetProperty(ref _accountTypeId, value);
        }

        public bool IsActive
        {
            get => _isActive;
            set => SetProperty(ref _isActive, value);
        }

        public bool IsHeader
        {
            get => _isHeader;
            set => SetProperty(ref _isHeader, value);
        }

        public int CreatedBy
        {
            get => _createdBy;
            set => SetProperty(ref _createdBy, value);
        }

        public DateTime CreatedAt
        {
            get => _createdAt;
            set => SetProperty(ref _createdAt, value);
        }

        public int? UpdatedBy
        {
            get => _updatedBy;
            set => SetProperty(ref _updatedBy, value);
        }

        public DateTime? UpdatedAt
        {
            get => _updatedAt;
            set => SetProperty(ref _updatedAt, value);
        }

        public int Level
        {
            get => _level;
            set => SetProperty(ref _level, value);
        }

        public string DisplayName
        {
            get => _displayName;
            set => SetProperty(ref _displayName, value);
        }

        public string AccountTypeName
        {
            get => _accountTypeName;
            set => SetProperty(ref _accountTypeName, value);
        }

        public int HierarchyLevel
        {
            get => _hierarchyLevel;
            set => SetProperty(ref _hierarchyLevel, value);
        }

        public ObservableCollection<AccountDto> Children { get; } = new ObservableCollection<AccountDto>();

        public string DisplayText => $"{Code} - {Name}";

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (Equals(field, value))
            {
                return false;
            }

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }
    }
}
