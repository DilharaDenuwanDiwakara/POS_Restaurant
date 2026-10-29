using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PointOfSale.Core.Interfaces.Security;
using PointOfSale.Core.Models.Security;
using PointOfSale.UI.ViewModels.Security;
using PointOfSale.UI.Views.Security;

// Standalone regression checks; run with Verify-RoleManagement.ps1. No SQL connection is used.
internal static class RoleManagementChecks
{
    private static int _checks;
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        _checks++;
    }

    [STAThread]
    private static void Main(string[] args)
    {
        var repository = new FakeRoleRepository();
        var vm = new RoleManagementViewModel(repository);
        Check(repository.RoleLoads == 0, "Constructing the ViewModel must not query SQL.");
        vm.InitializeAsync().GetAwaiter().GetResult();
        Check(vm.Roles.Count == 2 && vm.SelectedRole.Id == 1, "Initial roles not selected.");
        Check(vm.CanEditPermissions && vm.PermissionGroups.Count == 3, "Permission editor not initialized.");
        var sales = vm.PermissionGroups.Single(g => g.GroupName == "Sales");
        Check(sales.Permissions[0].IsGranted && !sales.Permissions[1].IsGranted, "Role grants were not mapped.");
        sales.Permissions[0].IsGranted = false;
        sales.Permissions[1].IsGranted = true;
        vm.SavePermissionsAsync().GetAwaiter().GetResult();
        Check(repository.SavedRoleId == 1 && repository.SavedPermissionIds.SequenceEqual(new[] { 20 }), "Incorrect permission payload.");

        vm.AddNewRoleCommand.Execute(null);
        Check(vm.SelectedRole.Id == 0 && !vm.CanEditPermissions && !vm.CanSaveRole, "Draft role should require a name and saving first.");
        vm.SelectedRole.Name = "  Shift Supervisor  ";
        Check(vm.SaveRoleCommand.CanExecute(null), "Changing the role name did not enable Save.");
        vm.SaveRoleAsync().GetAwaiter().GetResult();
        Check(vm.SelectedRole.Id == 3 && vm.SelectedRole.Name == "Shift Supervisor" && vm.CanEditPermissions, "Created role did not receive its ID.");
        vm.SavePermissionsAsync().GetAwaiter().GetResult();
        Check(repository.SavedRoleId == 3 && repository.SavedPermissionIds.Count == 0, "Clearing every grant must save an empty list.");

        var cashier = vm.Roles.Single(r => r.Id == 2);
        vm.EditRoleCommand.Execute(cashier);
        vm.PermissionsLoadTask.GetAwaiter().GetResult();
        Check(vm.SelectedRole == cashier && vm.RoleDetailsTitle.Contains("Cashier") && vm.CanEditPermissions,
            "Edit must select the existing role and enable its permission editor.");
        cashier.Name = "Senior Cashier";
        vm.SaveRoleAsync().GetAwaiter().GetResult();
        Check(repository.SavedRole.Id == 2 && repository.SavedRole.Name == "Senior Cashier",
            "Editing an existing role must update its ID rather than create a new role.");
        vm.PermissionGroups.First().Permissions[1].IsGranted = true;
        vm.SavePermissionsAsync().GetAwaiter().GetResult();
        Check(repository.SavedRoleId == 2 && repository.SavedPermissionIds.SequenceEqual(new[] { 20 }),
            "Edited role permissions were saved for the wrong role.");
        vm.PermissionGroups.Single(g => g.GroupName == "System").Permissions[0].IsGranted = true;
        vm.SavePermissionsAsync().GetAwaiter().GetResult();
        Check(repository.SavedPermissionIds.Contains(0), "Permission ID 0 must be included in saves.");
        vm.SelectedRole = vm.Roles.Single(r => r.Id == 1);
        vm.EditRoleCommand.Execute(cashier);
        vm.PermissionsLoadTask.GetAwaiter().GetResult();
        Check(vm.PermissionGroups.Single(g => g.GroupName == "System").Permissions[0].IsGranted
            && vm.PermissionGroups.Single(g => g.GroupName == "Sales").Permissions[1].IsGranted,
            "Previously saved role grants did not reload after switching roles.");
        vm.SelectedRole = vm.Roles.Single(r => r.Id == 1);

        var pending = new TaskCompletionSource<List<int>>();
        repository.PendingRole2 = pending;
        vm.SelectedRole = vm.Roles.Single(r => r.Id == 2);
        var obsoleteLoad = vm.PermissionsLoadTask;
        Check(!vm.CanEditPermissions, "Pending permissions must not be writable.");
        vm.SelectedRole = vm.Roles.Single(r => r.Id == 1);
        pending.SetResult(new List<int> { 20 });
        obsoleteLoad.GetAwaiter().GetResult();
        Check(vm.PermissionGroups.Count == 3 && vm.PermissionGroups[0].Permissions[1].IsGranted,
            "A stale role response replaced the current permissions.");

        repository.PendingRole2 = null;
        repository.FailPermissionLoad = true;
        vm.SelectedRole = vm.Roles.Single(r => r.Id == 2);
        Check(!vm.CanEditPermissions && !vm.IsLoadingPermissions && vm.HasError, "Permission load failure left editor writable.");
        repository.FailPermissionLoad = false;
        vm.SelectedRole = vm.Roles.Single(r => r.Id == 1);
        repository.BlockDelete = true;
        vm.DeleteRoleAsync(vm.SelectedRole).GetAwaiter().GetResult();
        Check(vm.Roles.Count == 3 && vm.HasError && !vm.IsBusy, "Rejected deletion removed a role or left the UI busy.");
        repository.BlockDelete = false;
        vm.DeleteRoleAsync(vm.Roles.Single(r => r.Id == 2)).GetAwaiter().GetResult();
        Check(vm.Roles.Count == 2 && vm.SelectedRole.Id == 1, "Row deletion targeted the wrong role.");
        repository.FailPermissionSave = true;
        vm.SavePermissionsAsync().GetAwaiter().GetResult();
        Check(vm.HasError && !vm.IsBusy && vm.CanEditPermissions, "Save failure was not surfaced or left editor busy.");
        repository.FailPermissionSave = false;
        vm.SavePermissionsAsync().GetAwaiter().GetResult();
        Check(!vm.HasError && vm.StatusMessage != null, "Retrying after a failed save did not recover.");

        // Load actual application resources and render the real XAML without starting the POS or SQL.
        var app = new Application();
        foreach (var resource in new[] { "Converters", "Themes/Colors", "Themes/Icons", "Components/Text", "Components/Buttons", "Components/Inputs", "Components/Containers", "Components/DataGrids", "Styles/ViewStyles" })
        {
            app.Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri("/PointOfSale.UI;component/Resources/" + resource + ".xaml", UriKind.Relative)
            });
        }
        var view = new RoleManagementView { DataContext = vm, Width = 1200, Height = 780 };
        view.Measure(new Size(1200, 780));
        view.Arrange(new Rect(0, 0, 1200, 780));
        view.UpdateLayout();
        var list = Descendants(view).OfType<ListBox>().Single();
        var supervisor = vm.Roles.Single(r => r.Id == 3);
        list.SelectedItem = supervisor;
        view.UpdateLayout();
        var nameEditor = Descendants(view).OfType<TextBox>().Single();
        Check(vm.SelectedRole == supervisor && nameEditor.IsEnabled && nameEditor.Text == supervisor.Name,
            "Selecting a saved role in the real ListBox did not populate an editable role name.");
        nameEditor.Text = "Evening Supervisor";
        nameEditor.GetBindingExpression(TextBox.TextProperty).UpdateSource();
        Check(supervisor.Name == "Evening Supervisor" && vm.SaveRoleCommand.CanExecute(null),
            "Role name edits did not flow from the real TextBox to the ViewModel.");
        list.SelectedItem = vm.Roles.Single(r => r.Id == 1);
        view.UpdateLayout();
        var bitmap = new RenderTargetBitmap(1200, 780, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(view);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (var stream = File.Create(args[0])) encoder.Save(stream);
        Check(view.ActualWidth == 1200, "Role management XAML did not render.");
        Console.WriteLine("PASS: " + _checks + " role management checks; XAML preview: " + args[0]);
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }

    private sealed class FakeRoleRepository : IRoleRepository
    {
        public int RoleLoads;
        public int SavedRoleId;
        public List<int> SavedPermissionIds;
        public Role SavedRole;
        public bool FailPermissionLoad, FailPermissionSave, BlockDelete;
        public TaskCompletionSource<List<int>> PendingRole2;
        private readonly Dictionary<int, List<int>> _assignments = new Dictionary<int, List<int>>
        {
            { 1, new List<int> { 10 } }
        };

        public Task<List<Role>> GetAllRolesAsync()
        {
            RoleLoads++;
            return Task.FromResult(new List<Role>
            {
                new Role { Id = 1, Name = "Administrator" }, new Role { Id = 2, Name = "Cashier" }
            });
        }
        public Task<int> SaveRoleAsync(Role role)
        {
            SavedRole = role;
            return Task.FromResult(role.Id == 0 ? 3 : role.Id);
        }
        public Task DeleteRoleAsync(int roleId)
        {
            if (BlockDelete) throw new ValidationException("This role is assigned to active users.");
            return Task.CompletedTask;
        }
        public Task<List<PermissionNode>> GetAllPermissionsAsync() => Task.FromResult(new List<PermissionNode>
        {
            new PermissionNode { PermissionId = 10, Module = "Sales", PermissionKey = "NAV_INVOICE", Description = "View invoices" },
            new PermissionNode { PermissionId = 20, Module = "Sales", PermissionKey = "SALES_RETURN", Description = "Process sales returns" },
            new PermissionNode { PermissionId = 30, Module = "Inventory", PermissionKey = "NAV_PRODUCT_LIST", Description = "View products" },
            new PermissionNode { PermissionId = 0, Module = "System", PermissionKey = "ACCESS_DASHBOARD", Description = "Access Dashboard" }
        });
        public Task<List<int>> GetRolePermissionIdsAsync(int roleId)
        {
            if (FailPermissionLoad) throw new InvalidOperationException("Permission load failed.");
            if (roleId == 2 && PendingRole2 != null) return PendingRole2.Task;
            List<int> ids;
            return Task.FromResult(_assignments.TryGetValue(roleId, out ids) ? new List<int>(ids) : new List<int>());
        }
        public Task SaveRolePermissionsAsync(int roleId, List<int> ids)
        {
            if (FailPermissionSave) throw new InvalidOperationException("Permission save failed.");
            SavedRoleId = roleId;
            SavedPermissionIds = ids;
            _assignments[roleId] = new List<int>(ids);
            return Task.CompletedTask;
        }
    }
}
