using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using PointOfSale.Core.Interfaces.Repositories.System;
using PointOfSale.Core.Interfaces.Security;
using PointOfSale.Core.Models.Security;
using PointOfSale.Core.Models.System;
using PointOfSale.Core.Services;
using PointOfSale.UI.Commands;

namespace PointOfSale.UI.ViewModels.Security
{
    public class UserViewModel : BaseViewModel
    {
        private readonly IUserRepository _userRepository;
        private readonly IPasswordHasher _passwordHasher;
        private readonly IBranchRepository _locationRepository;

        public UserViewModel(
            IUserRepository userRepository,
            IPasswordHasher passwordHasher,
            IBranchRepository locationRepository)
        {
            _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
            _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
            _locationRepository = locationRepository ?? throw new ArgumentNullException(nameof(locationRepository));

            // Initialize commands
            SaveUserCommand = new AsyncRelayCommand(async _ => await SaveUserAsync(), _ => CanSaveUser);
            UpdatePermissionsCommand = new AsyncRelayCommand(async _ => await SavePermissionsAsync(), _ => SelectedUser != null);
            LoadUserCommand = new AsyncRelayCommand(async _ => await LoadUserAsync());
            NewUserCommand = new RelayCommand(_ => CreateNewUser());
            EditUserCommand = new AsyncRelayCommand(async _ => await SetEditMode(true), _ => SelectedUser != null);
            DeleteUserCommand = new AsyncRelayCommand(async _ => await DeleteUserAsync(), _ => SelectedUser != null);

            // Load users on startup
            _ = LoadDataAsync();
        }

        #region Collections
        public ObservableCollection<User> UserList { get; } = new ObservableCollection<User>();
        public ObservableCollection<Branch> Branches { get; } = new ObservableCollection<Branch>();
        public ObservableCollection<Roles> Roles { get; } = new ObservableCollection<Roles>();

        public ObservableCollection<PermissionGroup> PermissionGroups { get; } = new ObservableCollection<PermissionGroup>();
        #endregion

        #region Properties
        private User _selectedUser;
        public User SelectedUser
        {
            get => _selectedUser;
            set
            {
                if (SetProperty(ref _selectedUser, value))
                {
                    if (value != null)
                    {
                        // Load permissions for selected user
                        _ = LoadPermissionsForUser(value.UserId);
                    }
                    else
                    {
                        // Clear permissions if no user selected
                        PermissionGroups.Clear();
                    }
                    (UpdatePermissionsCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
                    (EditUserCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
                    (DeleteUserCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
                }
            }

        }

        private Branch _selectedBranch;
        public Branch SelectedBranch
        {
            get => _selectedBranch;
            set
            {
                SetProperty(ref _selectedBranch, value);
            }
        }

        public bool _isEditing;
        public bool IsEditing
        {
            get => _isEditing;
            set
            {
                SetProperty(ref _isEditing, value);
                OnPropertyChanged(nameof(SaveButtonText));
            }
        }
        public string SaveButtonText => IsEditing ? "Update" : "Save";

        private int _userId;
        public int UserId
        {
            get => _userId;
            set => SetProperty(ref _userId, value);
        }

        private int _branchId;
        public int BranchId
        {
            get => _branchId;
            set
            {
                SetProperty(ref _branchId, value);
                ValidateLocation();
                RaiseCanExecuteChanged();
            }
        }

        private int _roleId;
        public int RoleId
        {
            get => _roleId;
            set { SetProperty(ref _roleId, value); ValidateRole(); RaiseCanExecuteChanged(); }
        }

        private string _fullName;
        public string FullName
        {
            get => _fullName;
            set
            {
                SetProperty(ref _fullName, value);
                ValidateFullName();
                RaiseCanExecuteChanged();
            }
        }

        private string _username;
        public string Username
        {
            get => _username;
            set
            {
                SetProperty(ref _username, value);
                ValidateUsername();
                RaiseCanExecuteChanged();
            }
        }

        private string _password;
        public string Password
        {
            get => _password;
            set
            {
                SetProperty(ref _password, value);
                ValidatePassword();
                RaiseCanExecuteChanged();
            }
        }

        private bool _isActive = true;
        public bool IsActive
        {
            get => _isActive;
            set => SetProperty(ref _isActive, value);
        }


        private int _selectedTabIndex;
        public int SelectedTabIndex
        {
            get => _selectedTabIndex;
            set => SetProperty(ref _selectedTabIndex, value);
        }

        // Validation gate for save button
        public bool CanSaveUser => !HasErrors &&
            !string.IsNullOrWhiteSpace(FullName) &&
            !string.IsNullOrWhiteSpace(Username) &&
            (!string.IsNullOrWhiteSpace(Password) || !IsEditing) &&
            BranchId > 0 &&
            RoleId > 0;
        #endregion

        #region Commands
        public ICommand SaveUserCommand { get; }
        public ICommand LoadUserCommand { get; }
        public ICommand UpdatePermissionsCommand { get; }
        public ICommand NewUserCommand { get; }
        public ICommand EditUserCommand { get; }
        public ICommand DeleteUserCommand { get; }
        #endregion

        #region CRUD Methods
        private async Task LoadDataAsync()
        {
            try
            {
                // 1. Load Locations
                var branches = await _locationRepository.GetAllAsync();
                Branches.Clear();
                foreach (var b in branches) Branches.Add(b);

                // 2. Load Roles (From DB now)
                var roles = await _userRepository.GetAllRolesAsync();
                Roles.Clear();
                foreach (var r in roles) Roles.Add(r);

                // 3. Load Users
                UserList.Clear();
                var users = await _userRepository.GetAllAsync();
                foreach (var user in users)
                {
                    if (user.UserId == 1) continue; // Skip Super Admin
                    UserList.Add(user);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load data: {ex.Message}");
            }
        }
        private async Task LoadPermissionsForUser(int userId)
        {
            PermissionGroups.Clear();

            try
            {
                // 1. Fetch Flat List
                var allPerms = await _userRepository.GetAllPermissionsAsync();
                var userPermIds = userId > 0 ? await _userRepository.GetUserPermissionIdsAsync(userId) : new List<int>();

                // 2. Group by "Module"
                var grouped = allPerms.GroupBy(p => p.Module);

                foreach (var group in grouped)
                {
                    var permissionNodes = new ObservableCollection<PermissionNode>();
                    foreach (var p in group)
                    {
                        p.IsGranted = userPermIds.Contains(p.PermissionId);
                        permissionNodes.Add(p);
                    }

                    PermissionGroups.Add(new PermissionGroup
                    {
                        GroupName = group.Key,
                        Permissions = permissionNodes
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading permissions: {ex.Message}");
            }
        }
        private async Task LoadUserAsync()
        {
            try
            {
                UserList.Clear();
                var users = await _userRepository.GetAllAsync();

                if (users != null)
                {
                    foreach (var user in users)
                    {
                        if (user.UserId == 1) continue;

                        UserList.Add(user);
                    }

                }

            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to load users: {ex.Message}";
            }

        }
        private async Task SaveUserAsync()
        {
            ValidateAll();
            if (HasErrors)
            {
                MessageBox.Show("Please check the form for errors.");
                return;
            }

            try
            {
                int savedUserId = UserId;

                if (IsEditing)
                {
                    // UPDATE Logic
                    var userToUpdate = SelectedUser;
                    userToUpdate.FullName = FullName;
                    userToUpdate.Username = Username;
                    userToUpdate.Role = RoleId;
                    userToUpdate.BranchId = BranchId;
                    userToUpdate.IsActive = IsActive;

                    // Only update password if user typed something
                    if (!string.IsNullOrEmpty(Password))
                        userToUpdate.PasswordHash = _passwordHasher.HashPassword(Password);

                    await _userRepository.UpdateAsync(userToUpdate);
                    savedUserId = userToUpdate.UserId;

                    MessageBox.Show("User updated successfully.", "User Management", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    // CREATE Logic
                    var newUser = new User
                    {
                        FullName = FullName,
                        Username = Username,
                        PasswordHash = _passwordHasher.HashPassword(Password),
                        Role = RoleId, // int ID
                        BranchId = BranchId,
                        IsActive = IsActive
                    };

                    savedUserId = await _userRepository.CreateAsync(newUser);
                    MessageBox.Show("User created successfully.", "User Management", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                // Refresh List
                await LoadDataAsync();
                CreateNewUser();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private async Task SavePermissionsAsync()
        {
            int targetUserId = SelectedUser?.UserId ?? UserId;

            if (targetUserId <= 0)
            {
                MessageBox.Show("Please select a user first.");
                return;
            }

            try
            {
                // Get IDs of checked boxes
                var grantedIds = new List<int>();

                foreach (var group in PermissionGroups)
                {
                    foreach (var perm in group.Permissions)
                    {
                        if (perm.IsGranted)
                        {
                            grantedIds.Add(perm.PermissionId);
                        }
                    }
                }

                await _userRepository.SaveUserPermissionsAsync(targetUserId, grantedIds);

                MessageBox.Show("Permissions updated successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save permissions: {ex.Message}");
            }
        }
        private async Task DeleteUserAsync()
        {
            if (SelectedUser == null) return;
            if (MessageBox.Show("Delete this user?", "Confirm", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                await _userRepository.DeleteAsync(SelectedUser.UserId);
                await LoadDataAsync();
                CreateNewUser();
            }
        }
        #endregion

        #region Form Helpers
        private void CreateNewUser()
        {
            SelectedUser = null;
            IsEditing = false;

            UserId = 0;
            FullName = string.Empty;
            Username = string.Empty;
            Password = string.Empty;
            RoleId = 0;
            BranchId = 0;
            IsActive = true;

            // Load clean permission list (all unchecked)
            _ = LoadPermissionsForUser(0);

            ClearAllErrors();
            RaiseCanExecuteChanged();
        }

        private async Task SetEditMode(bool isEditing)
        {
            IsEditing = isEditing;

            if (isEditing && SelectedUser != null)
            {
                UserId = SelectedUser.UserId;
                FullName = SelectedUser.FullName;
                Username = SelectedUser.Username;
                Password = string.Empty;

                RoleId = SelectedUser.Role;
                IsActive = SelectedUser.IsActive;
                BranchId = SelectedUser.BranchId;
            }
            else
            {
                CreateNewUser();
            }
        }
        private void RaiseCanExecuteChanged()
        {
            (SaveUserCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
            (EditUserCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (DeleteUserCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        }
        #endregion

        #region Validation
        private void ValidateAll()
        {
            ValidateFullName();
            ValidateUsername();
            ValidatePassword();
            ValidateLocation();
        }

        private void ValidateFullName()
        {
            ClearErrors(nameof(FullName));
            if (string.IsNullOrWhiteSpace(FullName))
                AddError(nameof(FullName), "Full name is required.");
            else if (!Regex.IsMatch(FullName, @"^[a-zA-Z\s]+$"))
                AddError(nameof(FullName), "Full name cannot contain numbers or special characters.");
        }

        private void ValidateUsername()
        {
            ClearErrors(nameof(Username));
            if (string.IsNullOrWhiteSpace(Username))
                AddError(nameof(Username), "Username is required.");
            else if (!Regex.IsMatch(Username, @"^[a-zA-Z0-9]+$"))
                AddError(nameof(Username), "Username must be alphanumeric (no spaces).");
        }

        private void ValidatePassword()
        {
            ClearErrors(nameof(Password));
            if (!IsEditing && string.IsNullOrWhiteSpace(Password))
                AddError(nameof(Password), "Password is required.");
        }

        private void ValidateLocation()
        {
            ClearErrors(nameof(BranchId));
            if (BranchId <= 0)
                AddError(nameof(BranchId), "Please select a branch.");
        }
        private void ValidateRole()
        {
            ClearErrors(nameof(RoleId));
            if (RoleId <= 0) AddError(nameof(RoleId), "Role is required.");
        }

        #endregion
    }
}
