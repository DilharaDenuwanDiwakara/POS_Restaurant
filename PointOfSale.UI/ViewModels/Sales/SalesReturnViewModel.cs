using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using PointOfSale.Core.Interfaces.Repositories.Sales;
using PointOfSale.Core.Interfaces.Services;
using PointOfSale.Core.Models.Sales;
using PointOfSale.Core.Services;
using PointOfSale.UI.Commands;
using PointOfSale.UI.ViewModels.Security;

namespace PointOfSale.UI.ViewModels.Sales
{
    public class SalesReturnViewModel : BaseViewModel
    {
        private readonly ISalesReturnRepository _salesReturnRepository;
        private readonly IUserSessionService _userSessionService;
        private readonly IDialogService _dialogService;

        public SalesReturnViewModel(
            ISalesReturnRepository salesReturnRepository,
            IUserSessionService userSessionService,
            IDialogService dialogService)
        {
            _salesReturnRepository = salesReturnRepository;
            _userSessionService = userSessionService;
            _dialogService = dialogService;

            ReturnLines = new ObservableCollection<SalesReturnItemModel>();

            SearchInvoiceCommand = new AsyncRelayCommand(async _ => await OnSearchInvoiceAsync(), _ => CanSearchInvoice());
            ProcessReturnCommand = new AsyncRelayCommand(async _ => await OnProcessReturnAsync(), _ => CanProcessReturn());
            AuthorizeManagerCommand = new RelayCommand(_ => AuthorizeManager());

            _ = LoadReturnReasonsAsync();
        }

        #region Properties - Search / Header

        private string _invoiceNumber;
        public string InvoiceNumber
        {
            get => _invoiceNumber;
            set
            {
                if (SetProperty(ref _invoiceNumber, value))
                {
                    RefreshCommands();
                }
            }
        }

        private long _salesId;
        public long SalesId
        {
            get => _salesId;
            private set => SetProperty(ref _salesId, value);
        }

        private string _loadedInvoiceNumber;
        public string LoadedInvoiceNumber
        {
            get => _loadedInvoiceNumber;
            private set => SetProperty(ref _loadedInvoiceNumber, value);
        }

        private DateTime? _salesDate;
        public DateTime? SalesDate
        {
            get => _salesDate;
            private set => SetProperty(ref _salesDate, value);
        }

        private string _authorizedByName;
        public string AuthorizedByName
        {
            get => _authorizedByName;
            private set => SetProperty(ref _authorizedByName, value);
        }

        #endregion

        #region Properties - Grid / Footer

        private ObservableCollection<SalesReturnItemModel> _returnLines;
        public ObservableCollection<SalesReturnItemModel> ReturnLines
        {
            get => _returnLines;
            private set => SetProperty(ref _returnLines, value);
        }

        public ObservableCollection<ReturnReasonModel> ReturnReasons { get; } = new ObservableCollection<ReturnReasonModel>();

        private decimal _totalRefund;
        public decimal TotalRefund
        {
            get => _totalRefund;
            private set => SetProperty(ref _totalRefund, value);
        }

        #endregion

        #region Commands
        public AsyncRelayCommand SearchInvoiceCommand { get; }
        public AsyncRelayCommand ProcessReturnCommand { get; }
        public ICommand AuthorizeManagerCommand { get; }
        #endregion

        /// <summary>Raised after a return is successfully processed, so a hosting modal (e.g. from the POS screen) can close itself.</summary>
        public event Action ReturnProcessed;

        #region Core Logic

        private void RefreshCommands()
        {
            SearchInvoiceCommand.RaiseCanExecuteChanged();
            ProcessReturnCommand.RaiseCanExecuteChanged();
        }

        private bool CanSearchInvoice() => !string.IsNullOrWhiteSpace(InvoiceNumber);

        private bool CanProcessReturn() =>
            SalesId > 0 &&
            ReturnLines != null &&
            ReturnLines.Any(l => l.ReturnQty > 0);

        private async Task LoadReturnReasonsAsync()
        {
            try
            {
                var reasons = await _salesReturnRepository.GetActiveReturnReasonsAsync();

                Application.Current.Dispatcher.Invoke(() =>
                {
                    ReturnReasons.Clear();
                    foreach (var reason in reasons)
                    {
                        ReturnReasons.Add(reason);
                    }
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading return reasons: {ex.Message}", "Sales Return", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task OnSearchInvoiceAsync()
        {
            try
            {
                ErrorMessage = null;

                var lookup = await _salesReturnRepository.GetOrderForReturnAsync(InvoiceNumber);
                if (lookup == null || !lookup.Lines.Any())
                {
                    MessageBox.Show("Invoice not found, or not eligible for return.", "Sales Return", MessageBoxButton.OK, MessageBoxImage.Warning);
                    ResetOrder();
                    return;
                }

                SalesId = lookup.SalesId;
                LoadedInvoiceNumber = lookup.InvoiceNumber;
                SalesDate = lookup.SalesDate;

                DetachLineHandlers();
                ReturnLines = new ObservableCollection<SalesReturnItemModel>(lookup.Lines);
                AttachLineHandlers();

                var defaultReasonId = ReturnReasons.FirstOrDefault()?.Id ?? 0;
                foreach (var line in ReturnLines)
                {
                    line.ReturnReasonId = defaultReasonId;
                }

                CalculateTotal();
                RefreshCommands();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error searching invoice: {ex.Message}", "Sales Return", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task OnProcessReturnAsync()
        {
            try
            {
                var itemsToReturn = ReturnLines.Where(l => l.ReturnQty > 0).ToList();
                if (!itemsToReturn.Any())
                {
                    MessageBox.Show("Enter a return quantity for at least one item.", "Sales Return", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (itemsToReturn.Any(l => l.ReturnReasonId <= 0))
                {
                    MessageBox.Show("Select a return reason for every item being returned.", "Sales Return", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!_userSessionService.CurrentShiftId.HasValue)
                {
                    MessageBox.Show("No active shift found. Start a shift before processing a return.", "Sales Return", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // A manager PIN is required for every return, regardless of any earlier authorization.
                var authorizedByUserId = AuthorizeManager();
                if (authorizedByUserId == null)
                {
                    return;
                }

                var newReturnId = await _salesReturnRepository.ProcessSalesReturnAsync(
                    SalesId,
                    _userSessionService.BranchId,
                    _userSessionService.CurrentShiftId.Value,
                    authorizedByUserId.Value,
                    _userSessionService.UserId,
                    itemsToReturn);

                MessageBox.Show($"Sales return #{newReturnId} processed successfully.", "Sales Return", MessageBoxButton.OK, MessageBoxImage.Information);
                ResetOrder();
                ReturnProcessed?.Invoke();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to process return: {ex.Message}", "Sales Return", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private int? AuthorizeManager()
        {
            var dialogResult = _dialogService.ShowDialog<ManagerAuthorizationViewModel>(vm =>
            {
                vm.RequiredPermission = "AUTHORIZE_SALES_RETURN";
                vm.ActionDescription = "authorize this sales return";
            }, out var authVm);

            if (dialogResult == true && authVm.IsAuthorized)
            {
                AuthorizedByName = authVm.AuthorizedUserName;
                return authVm.AuthorizedUserId;
            }

            return null;
        }

        private void AttachLineHandlers()
        {
            foreach (var line in ReturnLines)
            {
                line.PropertyChanged += Line_PropertyChanged;
            }
        }

        private void DetachLineHandlers()
        {
            if (ReturnLines == null) return;

            foreach (var line in ReturnLines)
            {
                line.PropertyChanged -= Line_PropertyChanged;
            }
        }

        private void Line_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SalesReturnItemModel.RefundAmount))
            {
                CalculateTotal();
                RefreshCommands();
            }
        }

        private void CalculateTotal()
        {
            TotalRefund = ReturnLines?.Sum(l => l.RefundAmount) ?? 0;
        }

        private void ResetOrder()
        {
            DetachLineHandlers();
            SalesId = 0;
            LoadedInvoiceNumber = null;
            SalesDate = null;
            AuthorizedByName = null;
            InvoiceNumber = string.Empty;
            ReturnLines = new ObservableCollection<SalesReturnItemModel>();
            TotalRefund = 0;
            RefreshCommands();
        }

        #endregion
    }
}
