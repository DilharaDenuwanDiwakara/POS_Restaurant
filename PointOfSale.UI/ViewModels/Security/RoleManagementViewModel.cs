using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using PointOfSale.Core.Interfaces.Security;
using PointOfSale.Core.Models.Security;
using PointOfSale.UI.Commands;

namespace PointOfSale.UI.ViewModels.Security
{
    public class RoleManagementViewModel : BaseViewModel
    {
        private readonly IRoleRepository _roleRepository;
        private Role _selectedRole;
        private bool _isBusy;
        private bool _isLoadingPermissions;
        private bool _rolesLoaded;
        private int _loadVersion;
        private int? _loadedRoleId;
        private string _statusMessage;
        private bool _isCreatingRole;
        private Task _initializationTask;

        public RoleManagementViewModel(IRoleRepository roleRepository)
        {
            _roleRepository = roleRepository ?? throw new ArgumentNullException(nameof(roleRepository));
            AddNewRoleCommand = new RelayCommand(_ => AddNewRole(), _ => !IsBusy && _rolesLoaded && !IsCreatingRole);
            CancelCreateRoleCommand = new RelayCommand(_ => CancelCreateRole(), _ => IsCreatingRole && !IsBusy);
            EditRoleCommand = new RelayCommand(p => SelectedRole = (Role)p,
                p => !IsBusy && p is Role && Roles.Contains((Role)p));
            SaveRoleCommand = new AsyncRelayCommand(_ => SaveRoleAsync(), _ => CanSaveRole);
            DeleteRoleCommand = new AsyncRelayCommand(p => DeleteRoleAsync(p as Role ?? SelectedRole),
                p => !IsBusy && (p as Role ?? SelectedRole)?.Id > 0);
            SavePermissionsCommand = new AsyncRelayCommand(_ => SavePermissionsAsync(), _ => CanEditPermissions);
            RefreshRolesCommand = new AsyncRelayCommand(_ => LoadRolesAsync(), _ => !IsBusy);
        }

        public ObservableCollection<Role> Roles { get; } = new ObservableCollection<Role>();
        public ObservableCollection<PermissionGroup> PermissionGroups { get; } = new ObservableCollection<PermissionGroup>();
        public RelayCommand AddNewRoleCommand { get; }
        public RelayCommand CancelCreateRoleCommand { get; }
        public RelayCommand EditRoleCommand { get; }
        public AsyncRelayCommand SaveRoleCommand { get; }
        public AsyncRelayCommand DeleteRoleCommand { get; }
        public AsyncRelayCommand SavePermissionsCommand { get; }
        public AsyncRelayCommand RefreshRolesCommand { get; }
        public Task PermissionsLoadTask { get; private set; } = Task.CompletedTask;

        public Role SelectedRole
        {
            get => _selectedRole;
            set
            {
                if (ReferenceEquals(_selectedRole, value)) return;
                if (_selectedRole != null) _selectedRole.PropertyChanged -= SelectedRoleChanged;
                _selectedRole = value;
                if (_selectedRole?.Id != 0) IsCreatingRole = false;
                if (_selectedRole != null) _selectedRole.PropertyChanged += SelectedRoleChanged;
                OnPropertyChanged();
                PermissionsLoadTask = LoadPermissionsAsync();
                RefreshCommandStates();
            }
        }

        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (SetProperty(ref _isBusy, value)) RefreshCommandStates();
            }
        }

        public bool IsLoadingPermissions
        {
            get => _isLoadingPermissions;
            private set
            {
                if (SetProperty(ref _isLoadingPermissions, value)) RefreshCommandStates();
            }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            private set => SetProperty(ref _statusMessage, value);
        }

        public bool IsCreatingRole
        {
            get => _isCreatingRole;
            private set
            {
                if (SetProperty(ref _isCreatingRole, value))
                {
                    OnPropertyChanged(nameof(RoleDetailsTitle));
                    OnPropertyChanged(nameof(PermissionEditorHint));
                    AddNewRoleCommand.RaiseCanExecuteChanged();
                    CancelCreateRoleCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public bool CanEditRole => !IsBusy && SelectedRole != null;
        public string RoleDetailsTitle => IsCreatingRole ? "Create New Role" : SelectedRole == null ? "Select a role"
            : "Edit Role: " + SelectedRole.Name;
        public string SaveRoleButtonText => SelectedRole?.Id == 0 ? "Create Role" : "Save Changes";
        public string PermissionEditorHint => IsCreatingRole ? "Save the new role to start assigning permissions."
            : SelectedRole == null ? "Select a role to manage its permissions."
            : "Permissions for " + SelectedRole.Name + ". Check the allowed actions, then click Save Permissions.";
        public bool CanSaveRole => CanEditRole && !string.IsNullOrWhiteSpace(SelectedRole.Name)
            && SelectedRole.Name.Trim().Length <= 100;
        public bool CanEditPermissions => CanEditRole && SelectedRole.Id > 0
            && !IsLoadingPermissions && _loadedRoleId == SelectedRole.Id;

        // Called by the view's Loaded event; constructing a ViewModel does not query SQL.
        public Task InitializeAsync()
        {
            return _initializationTask ?? (_initializationTask = LoadRolesAsync());
        }

        private async Task LoadRolesAsync()
        {
            if (IsBusy) return;
            IsBusy = true;
            ErrorMessage = null;
            StatusMessage = null;
            try
            {
                var roles = await _roleRepository.GetAllRolesAsync();
                SelectedRole = null;
                Roles.Clear();
                foreach (var role in roles) Roles.Add(role);
                _rolesLoaded = true;
                SelectedRole = Roles.FirstOrDefault();
                await PermissionsLoadTask;
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void AddNewRole()
        {
            if (IsBusy || !_rolesLoaded) return;
            var draft = new Role { Name = string.Empty };
            Roles.Add(draft);
            SelectedRole = draft;
            IsCreatingRole = true;
            StatusMessage = null;
        }

        private void CancelCreateRole()
        {
            if (!IsCreatingRole || IsBusy) return;
            var draft = SelectedRole;
            IsCreatingRole = false;
            if (draft?.Id == 0) Roles.Remove(draft);
            SelectedRole = Roles.FirstOrDefault();
        }

        private async Task LoadPermissionsAsync()
        {
            var version = ++_loadVersion;
            var role = SelectedRole;
            _loadedRoleId = null;
            PermissionGroups.Clear();
            ErrorMessage = null;
            StatusMessage = null;
            IsLoadingPermissions = role != null;
            RefreshCommandStates();
            if (role == null) return;

            try
            {
                var permissions = await _roleRepository.GetAllPermissionsAsync();
                var ids = role.Id > 0
                    ? new HashSet<int>(await _roleRepository.GetRolePermissionIdsAsync(role.Id))
                    : new HashSet<int>();
                if (version != _loadVersion) return;

                foreach (var group in permissions.GroupBy(p => p.Module))
                {
                    var nodes = new ObservableCollection<PermissionNode>();
                    foreach (var node in group)
                    {
                        node.IsGranted = ids.Contains(node.PermissionId);
                        nodes.Add(node);
                    }
                    PermissionGroups.Add(new PermissionGroup
                    {
                        GroupName = group.Key,
                        Permissions = nodes,
                        IsAllSelected = nodes.All(p => p.IsGranted)
                    });
                }
                _loadedRoleId = role.Id;
            }
            catch (Exception ex)
            {
                if (version == _loadVersion) ErrorMessage = ex.Message;
            }
            finally
            {
                if (version == _loadVersion)
                {
                    IsLoadingPermissions = false;
                    RefreshCommandStates();
                }
            }
        }

        public async Task SaveRoleAsync()
        {
            if (!CanSaveRole) return;
            var selected = SelectedRole;
            var snapshot = new Role { Id = selected.Id, Name = selected.Name.Trim() };
            IsBusy = true;
            ErrorMessage = null;
            StatusMessage = null;
            try
            {
                var id = await _roleRepository.SaveRoleAsync(snapshot);
                var wasNew = selected.Id == 0;
                selected.Id = id;
                selected.Name = snapshot.Name;
                if (wasNew)
                {
                    IsCreatingRole = false;
                }
                if (wasNew && ReferenceEquals(SelectedRole, selected))
                {
                    PermissionsLoadTask = LoadPermissionsAsync();
                    await PermissionsLoadTask;
                }
                if (ReferenceEquals(SelectedRole, selected)) StatusMessage = "Role saved successfully.";
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }

        public async Task DeleteRoleAsync(Role role)
        {
            if (IsBusy || role == null || role.Id <= 0) return;
            IsBusy = true;
            ErrorMessage = null;
            StatusMessage = null;
            try
            {
                await _roleRepository.DeleteRoleAsync(role.Id);
                Roles.Remove(role);
                if (ReferenceEquals(SelectedRole, role)) SelectedRole = Roles.FirstOrDefault();
                await PermissionsLoadTask;
                StatusMessage = "Role deleted successfully.";
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }

        public async Task SavePermissionsAsync()
        {
            if (!CanEditPermissions) return;
            var roleId = SelectedRole.Id;
            var ids = PermissionGroups.SelectMany(g => g.Permissions)
                .Where(p => p.IsGranted).Select(p => p.PermissionId).Distinct().ToList();
            IsBusy = true;
            ErrorMessage = null;
            StatusMessage = null;
            try
            {
                await _roleRepository.SaveRolePermissionsAsync(roleId, ids);
                if (SelectedRole?.Id == roleId) StatusMessage = "Role permissions saved successfully.";
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void SelectedRoleChanged(object sender, PropertyChangedEventArgs e)
        {
            RefreshCommandStates();
        }

        private void RefreshCommandStates()
        {
            OnPropertyChanged(nameof(CanEditRole));
            OnPropertyChanged(nameof(CanEditPermissions));
            OnPropertyChanged(nameof(CanSaveRole));
            OnPropertyChanged(nameof(RoleDetailsTitle));
            OnPropertyChanged(nameof(SaveRoleButtonText));
            OnPropertyChanged(nameof(PermissionEditorHint));
            AddNewRoleCommand.RaiseCanExecuteChanged();
            CancelCreateRoleCommand.RaiseCanExecuteChanged();
            EditRoleCommand.RaiseCanExecuteChanged();
            SaveRoleCommand.RaiseCanExecuteChanged();
            DeleteRoleCommand.RaiseCanExecuteChanged();
            SavePermissionsCommand.RaiseCanExecuteChanged();
            RefreshRolesCommand.RaiseCanExecuteChanged();
        }
    }
}
