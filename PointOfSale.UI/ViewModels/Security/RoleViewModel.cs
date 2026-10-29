using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using PointOfSale.Core.Interfaces.Security;
using PointOfSale.Core.Models.Security;
using PointOfSale.UI.Commands;

namespace PointOfSale.UI.ViewModels.Security
{
    public class RoleViewModel : BaseViewModel
    {
        private readonly IUserRepository _userRepository;
        private Roles _selectedRole;
        private int? _loadedRoleId;
        private int _permissionLoadVersion;
        private bool _isSaving;
        private string _statusMessage;

        public RoleViewModel(IUserRepository userRepository)
        {
            _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
            LoadRolesCommand = new AsyncRelayCommand(_ => LoadRolesAsync(), _ => !IsSaving);
            UpdatePermissionsCommand = new AsyncRelayCommand(_ => SavePermissionsAsync(), _ => CanSavePermissions);
            _ = LoadRolesAsync();
        }

        public ObservableCollection<Roles> Roles { get; } = new ObservableCollection<Roles>();
        public ObservableCollection<PermissionGroup> PermissionGroups { get; } = new ObservableCollection<PermissionGroup>();
        public AsyncRelayCommand LoadRolesCommand { get; }
        public AsyncRelayCommand UpdatePermissionsCommand { get; }

        public Roles SelectedRole
        {
            get => _selectedRole;
            set
            {
                if (SetProperty(ref _selectedRole, value))
                    _ = LoadPermissionsAsync();
            }
        }

        public bool IsSaving
        {
            get => _isSaving;
            private set
            {
                if (SetProperty(ref _isSaving, value))
                {
                    LoadRolesCommand.RaiseCanExecuteChanged();
                    RaiseSaveCanExecuteChanged();
                }
            }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            private set => SetProperty(ref _statusMessage, value);
        }

        public bool CanSavePermissions => !IsSaving && SelectedRole != null && _loadedRoleId == SelectedRole.Id;

        public async Task LoadRolesAsync()
        {
            ErrorMessage = null;
            StatusMessage = null;
            SelectedRole = null;
            try
            {
                var roles = await _userRepository.GetAllRolesAsync();
                Roles.Clear();
                foreach (var role in roles) Roles.Add(role);
                SelectedRole = Roles.FirstOrDefault();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to load roles: {ex.Message}";
            }
        }

        private async Task LoadPermissionsAsync()
        {
            var version = ++_permissionLoadVersion;
            var role = SelectedRole;
            _loadedRoleId = null;
            PermissionGroups.Clear();
            ErrorMessage = null;
            StatusMessage = null;
            RaiseSaveCanExecuteChanged();
            if (role == null) return;

            try
            {
                var permissions = await _userRepository.GetAllPermissionsAsync();
                var grantedIds = new HashSet<int>(await _userRepository.GetRolePermissionIdsAsync(role.Id));
                // A slow response for a previously selected role must not replace the current editor.
                if (version != _permissionLoadVersion) return;

                foreach (var group in permissions.GroupBy(p => p.Module))
                {
                    var nodes = new ObservableCollection<PermissionNode>();
                    foreach (var permission in group)
                    {
                        permission.IsGranted = grantedIds.Contains(permission.PermissionId);
                        nodes.Add(permission);
                    }
                    PermissionGroups.Add(new PermissionGroup
                    {
                        GroupName = group.Key,
                        Permissions = nodes,
                        IsAllSelected = nodes.All(p => p.IsGranted)
                    });
                }
                _loadedRoleId = role.Id;
                RaiseSaveCanExecuteChanged();
            }
            catch (Exception ex)
            {
                if (version == _permissionLoadVersion)
                    ErrorMessage = $"Failed to load role permissions: {ex.Message}";
            }
        }

        public async Task SavePermissionsAsync()
        {
            if (!CanSavePermissions) return;
            var role = SelectedRole;
            var grantedIds = PermissionGroups.SelectMany(g => g.Permissions)
                .Where(p => p.IsGranted).Select(p => p.PermissionId).Distinct().ToList();

            IsSaving = true;
            ErrorMessage = null;
            StatusMessage = null;
            try
            {
                await _userRepository.SaveRolePermissionsAsync(role.Id, grantedIds);
                if (SelectedRole == role)
                    StatusMessage = "Role permissions updated successfully.";
            }
            catch (Exception ex)
            {
                if (SelectedRole == role)
                    ErrorMessage = $"Failed to save role permissions: {ex.Message}";
            }
            finally
            {
                IsSaving = false;
            }
        }

        private void RaiseSaveCanExecuteChanged()
        {
            OnPropertyChanged(nameof(CanSavePermissions));
            UpdatePermissionsCommand.RaiseCanExecuteChanged();
        }
    }
}
