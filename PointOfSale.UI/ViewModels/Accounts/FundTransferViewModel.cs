using System;
using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using CrystalDecisions.CrystalReports.Engine;
using PointOfSale.Core.DTOs;
using PointOfSale.Core.Interfaces;
using PointOfSale.Core.Interfaces.Repositories.Accounts;
using PointOfSale.Core.Interfaces.Services;
using PointOfSale.Core.Models.Accounts.Entities;
using PointOfSale.Core.Services;
using PointOfSale.UI.Commands;
using PointOfSale.UI.Views.Sales;

namespace PointOfSale.UI.ViewModels.Accounts
{
    public class FundTransferViewModel : BaseViewModel
    {
        private readonly IFundTransferRepository _fundTransferRepository;
        private readonly IAccountingRepository _accountingRepository;
        private readonly IUserSessionService _userSessionService;

        public FundTransferViewModel(IFundTransferRepository fundTransferRepository,
            IAccountingRepository accountingRepository,
            IUserSessionService userSessionService)
        {
            _fundTransferRepository = fundTransferRepository ?? throw new ArgumentNullException(nameof(fundTransferRepository));
            _accountingRepository = accountingRepository ?? throw new ArgumentNullException(nameof(accountingRepository));
            _userSessionService = userSessionService ?? throw new ArgumentNullException(nameof(userSessionService));

            TransferList = new ObservableCollection<FundTransfer>();
            TransferAccounts = new ObservableCollection<AccountDto>();

            SaveTransferCommand = new AsyncRelayCommand(async _ => await SaveTransferAsync());
            LoadTransfersCommand = new AsyncRelayCommand(async _ => await LoadTransfersAsync());
            SearchTransfersCommand = new AsyncRelayCommand(async _ => await LoadTransfersAsync());
            PrintTransferCommand = new AsyncRelayCommand(async parameter => await PrintTransferAsync(parameter));
            NewTransferCommand = new RelayCommand(_ => CreateNewTransfer());

            _ = LoadTransfersAsync();
            _ = LoadAccountsAsync();
        }

        public ObservableCollection<FundTransfer> TransferList { get; }
        public ObservableCollection<AccountDto> TransferAccounts { get; }

        private FundTransfer _selectedTransfer;
        public FundTransfer SelectedTransfer
        {
            get => _selectedTransfer;
            set => SetProperty(ref _selectedTransfer, value);
        }

        private AccountDto _selectedSourceAccount;
        public AccountDto SelectedSourceAccount
        {
            get => _selectedSourceAccount;
            set
            {
                if (SetProperty(ref _selectedSourceAccount, value))
                {
                    ValidateSourceAccount();
                    ValidateDestinationAccount();
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        private AccountDto _selectedDestinationAccount;
        public AccountDto SelectedDestinationAccount
        {
            get => _selectedDestinationAccount;
            set
            {
                if (SetProperty(ref _selectedDestinationAccount, value))
                {
                    ValidateDestinationAccount();
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        private DateTime _transferDate = DateTime.Now;
        public DateTime TransferDate
        {
            get => _transferDate;
            set => SetProperty(ref _transferDate, value);
        }

        private DateTime _fromDate = DateTime.Today;
        public DateTime FromDate
        {
            get => _fromDate;
            set
            {
                if (SetProperty(ref _fromDate, value))
                {
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        private DateTime _toDate = DateTime.Today;
        public DateTime ToDate
        {
            get => _toDate;
            set
            {
                if (SetProperty(ref _toDate, value))
                {
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        private decimal _amount;
        public decimal Amount
        {
            get => _amount;
            set
            {
                if (SetProperty(ref _amount, value))
                {
                    ValidateAmount();
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        private string _referenceNo;
        public string ReferenceNo
        {
            get => _referenceNo;
            set
            {
                if (SetProperty(ref _referenceNo, value))
                {
                    ValidateReferenceNo();
                }
            }
        }

        private string _description;
        public string Description
        {
            get => _description;
            set
            {
                if (SetProperty(ref _description, value))
                {
                    ValidateDescription();
                }
            }
        }

        #region Command
        public ICommand SaveTransferCommand { get; }
        public ICommand LoadTransfersCommand { get; }
        public ICommand SearchTransfersCommand { get; }
        public ICommand PrintTransferCommand { get; }
        public ICommand NewTransferCommand { get; }
        #endregion

        private void CreateNewTransfer()
        {
            TransferDate = DateTime.Now;
            SelectedSourceAccount = null;
            SelectedDestinationAccount = null;
            Amount = 0m;
            ReferenceNo = string.Empty;
            Description = string.Empty;
            ClearAllErrors();
            SelectedTransfer = null;
        }

        #region Command Implementation
        private async Task LoadTransfersAsync()
        {
            try
            {
                TransferList.Clear();
                var currentBranchId = _userSessionService.BranchId;

                if (ToDate.Date < FromDate.Date)
                {
                    MessageBox.Show("To Date cannot be earlier than From Date.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var items = await _fundTransferRepository.GetAllAsync(currentBranchId, FromDate, ToDate);
                if (items == null) return;

                foreach (var item in items)
                {
                    TransferList.Add(item);
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to load fund transfers: {ex.Message}";
            }
        }

        private async Task LoadAccountsAsync()
        {
            try
            {
                var accounts = await _accountingRepository.GetAccountsAsync();
                TransferAccounts.Clear();
                foreach (var account in accounts.Where(a => a.AccountTypeId == 1 && !a.IsHeader && a.IsActive))
                {
                    TransferAccounts.Add(account);
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to load accounts: {ex.Message}";
            }
        }

        private async Task SaveTransferAsync()
        {
            try
            {
                ValidateAll();

                if (HasErrors)
                {
                    MessageBox.Show("Please correct the errors before saving.");
                    return;
                }

                var fundTransfer = new FundTransfer
                {
                    TransferDate = TransferDate,
                    BranchId = _userSessionService.BranchId,
                    SourceAccountId = SelectedSourceAccount.Id,
                    DestinationAccountId = SelectedDestinationAccount.Id,
                    Amount = Amount,
                    ReferenceNo = ReferenceNo,
                    Description = Description,
                    CreatedBy = _userSessionService.UserId
                };

                var fundTransferId = await _fundTransferRepository.CreateAsync(fundTransfer);
                MessageBox.Show($"Fund transfer {fundTransfer.TransferNumber} posted successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);

                await OpenFundTransferVoucherAsync(fundTransferId);

                await LoadTransfersAsync();
                CreateNewTransfer();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving: {ex.Message}");
            }
        }

        private async Task PrintTransferAsync(object parameter)
        {
            var transfer = parameter as FundTransfer;
            if (transfer == null || transfer.FundTransferId <= 0)
            {
                return;
            }

            try
            {
                await OpenFundTransferVoucherAsync(transfer.FundTransferId);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unable to open Fund Transfer Voucher preview: {ex.Message}", "Fund Transfer Voucher", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task OpenFundTransferVoucherAsync(long fundTransferId)
        {
            try
            {
                DataTable reportData = await _fundTransferRepository.GetFundTransferVoucherAsync(fundTransferId);

                if (reportData == null || reportData.Rows.Count == 0)
                {
                    MessageBox.Show(
                        "No data found for this Fund Transfer Voucher.",
                        "Fund Transfer Voucher",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    ReportDocument reportDocument = null;

                    try
                    {
                        reportDocument = new ReportDocument();
                        reportDocument.Load(ResolveFundTransferVoucherReportPath());
                        reportDocument.SetDataSource(reportData);

                        var previewWindow = new ZReportViewerWindow(reportDocument, disposeReportOnClose: true)
                        {
                            Title = "Fund Transfer Voucher"
                        };

                        var owner = Application.Current.MainWindow;
                        if (owner != null && owner != previewWindow)
                        {
                            previewWindow.Owner = owner;
                            previewWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                        }
                        else
                        {
                            previewWindow.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                        }

                        previewWindow.ShowDialog();
                        reportDocument = null;
                    }
                    finally
                    {
                        if (reportDocument != null)
                        {
                            reportDocument.Close();
                            reportDocument.Dispose();
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Fund transfer was saved, but the voucher could not be opened: {ex.Message}",
                    "Fund Transfer Voucher",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private static string ResolveFundTransferVoucherReportPath()
        {
            var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            var candidatePaths = new[]
            {
                Path.Combine(baseDirectory, "Reports", "FundTransferVoucher.rpt"),
                Path.Combine(baseDirectory, "FundTransferVoucher.rpt"),
                Path.GetFullPath(Path.Combine(baseDirectory, @"..\..\Reports\FundTransferVoucher.rpt"))
            };

            foreach (var candidatePath in candidatePaths)
            {
                if (File.Exists(candidatePath))
                {
                    return candidatePath;
                }
            }

            throw new FileNotFoundException(
                "Crystal report file not found. Expected FundTransferVoucher.rpt under the application Reports folder.",
                candidatePaths[0]);
        }
        #endregion

        #region Validation
        private void ValidateAll()
        {
            ValidateAmount();
            ValidateSourceAccount();
            ValidateDestinationAccount();
            ValidateReferenceNo();
            ValidateDescription();
        }
        private void ValidateAmount()
        {
            ClearErrors(nameof(Amount));
            if (Amount <= 0m)
            {
                AddError(nameof(Amount), "Enter a valid amount.");
            }
        }
        private void ValidateSourceAccount()
        {
            ClearErrors(nameof(SelectedSourceAccount));
            if (SelectedSourceAccount == null)
                AddError(nameof(SelectedSourceAccount), "Please select a source account.");
        }
        private void ValidateDestinationAccount()
        {
            ClearErrors(nameof(SelectedDestinationAccount));
            if (SelectedDestinationAccount == null)
            {
                AddError(nameof(SelectedDestinationAccount), "Please select a destination account.");
            }
            else if (SelectedSourceAccount != null && SelectedDestinationAccount.Id == SelectedSourceAccount.Id)
            {
                AddError(nameof(SelectedDestinationAccount), "Destination account must be different from the source account.");
            }
        }
        private void ValidateReferenceNo()
        {
            ClearErrors(nameof(ReferenceNo));
            if (!string.IsNullOrWhiteSpace(ReferenceNo) && ReferenceNo.Length > 100)
                AddError(nameof(ReferenceNo), "Reference No cannot exceed 100 characters.");
        }
        private void ValidateDescription()
        {
            ClearErrors(nameof(Description));
            if (!string.IsNullOrWhiteSpace(Description) && Description.Length > 500)
                AddError(nameof(Description), "Description cannot exceed 500 characters.");
            else if (!string.IsNullOrWhiteSpace(Description) && !Regex.IsMatch(Description, @"^[a-zA-Z0-9\s\-\(\)]+$"))
                AddError(nameof(Description), "Cannot contain special character");
        }
        #endregion
    }
}
