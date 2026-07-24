using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using CrystalDecisions.CrystalReports.Engine;
using CrystalDecisions.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using PointOfSale.Core.Enums;
using PointOfSale.Core.Interfaces.Repositories.Restaurant;
using PointOfSale.Core.Interfaces.Repositories.Sales;
using PointOfSale.Core.Interfaces.Services;
using PointOfSale.Core.Models.Restaurant;
using PointOfSale.Core.Services;
using PointOfSale.UI.Commands;
using PointOfSale.UI.ViewModels.Accounts;
using PointOfSale.UI.ViewModels.Sales;
using PointOfSale.UI.Views.Sales;
using PointOfSale.UI.Views.Shell;

namespace PointOfSale.UI.ViewModels.Restaurant
{
    public class RoomBookingViewModel : BaseViewModel, IDataErrorInfo
    {
        private static readonly string[] BookingFormProperties =
        {
            nameof(SelectedBookingRoom),
            nameof(SelectedMealPeriod),
            nameof(FromDate),
            nameof(ToDate),
            nameof(FromTimeText),
            nameof(ToTimeText),
            nameof(BookingNumberOfPax),
            nameof(BookingCustomerName),
            nameof(BookingCustomerPhoneNumber)
        };

        private readonly IRoomBookingRepository _roomBookingRepository;
        private readonly IMealPeriodRepository _mealPeriodRepository;
        private readonly IDialogService _dialogService;
        private readonly IUserSessionService _userSessionService;
        private readonly IServiceProvider _serviceProvider;
        private readonly ICustomerRepository _customerRepository;

        public RoomBookingViewModel(
            IRoomBookingRepository roomBookingRepository,
            IMealPeriodRepository mealPeriodRepository,
            IDialogService dialogService,
            IUserSessionService userSessionService,
            IServiceProvider serviceProvider,
            ICustomerRepository customerRepository)
        {
            _roomBookingRepository = roomBookingRepository ?? throw new ArgumentNullException(nameof(roomBookingRepository));
            _mealPeriodRepository = mealPeriodRepository ?? throw new ArgumentNullException(nameof(mealPeriodRepository));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            _userSessionService = userSessionService ?? throw new ArgumentNullException(nameof(userSessionService));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _customerRepository = customerRepository ?? throw new ArgumentNullException(nameof(customerRepository));

            SaveBookingCommand = new AsyncRelayCommand(async _ => await SaveBookingAsync(), _ => CanSaveBooking);
            NewBookingCommand = new RelayCommand(_ => CreateNewBooking());
            RefreshRoomsCommand = new AsyncRelayCommand(async _ => await LoadRoomsAsync());
            RefreshMealPeriodsCommand = new AsyncRelayCommand(async _ => await LoadMealPeriodsAsync());
            RefreshBookingsCommand = new AsyncRelayCommand(async _ => await LoadBookingsAsync());
            GenerateBookingReportCommand = new AsyncRelayCommand(async _ => await GenerateBookingReportAsync(), _ => BookingList.Any());
            AddMealPeriodCommand = new RelayCommand(ExecuteOpenMealPeriod);

            CancelBookingCommand = new AsyncRelayCommand(async _ => await CancelBookingAsync(), _ => CanCancelBooking);
            AddAdvancePaymentCommand = new AsyncRelayCommand(async param => await AddAdvancePaymentAsync(param), _ => CanAddAdvancePayment);
            CheckInCommand = new AsyncRelayCommand(async _ => await CheckInAsync(), _ => CanCheckIn);
            SearchCustomerCommand = new AsyncRelayCommand(async _ => await SearchCustomerAsync());

            _ = InitializeAsync();
        }

        public ObservableCollection<Room> BookableRooms { get; } = new ObservableCollection<Room>();
        public ObservableCollection<MealPeriod> MealPeriods { get; } = new ObservableCollection<MealPeriod>();
        public ObservableCollection<RoomBooking> BookingList { get; } = new ObservableCollection<RoomBooking>();

        private RoomBooking _selectedBooking;
        public RoomBooking SelectedBooking
        {
            get => _selectedBooking;
            set
            {
                if (SetProperty(ref _selectedBooking, value))
                {
                    if (value != null)
                    {
                        SelectedStatus = value.Status;
                        CustomerId = value.CustomerId;
                        AdvancePayment = value.AdvancePayment;
                    }
                    else
                    {
                        SelectedStatus = BookingStatus.Pending;
                        CustomerId = null;
                        AdvancePayment = 0m;
                    }
                    RaiseCanExecuteChanged();
                }
            }
        }

        private Room _selectedBookingRoom;
        public Room SelectedBookingRoom
        {
            get => _selectedBookingRoom;
            set
            {
                SetProperty(ref _selectedBookingRoom, value);
                ValidateSelectedBookingRoom();
                ValidateBookingNumberOfPax();
                RaiseCanExecuteChanged();
            }
        }

        private MealPeriod _selectedMealPeriod;
        public MealPeriod SelectedMealPeriod
        {
            get => _selectedMealPeriod;
            set
            {
                if (SetProperty(ref _selectedMealPeriod, value))
                {
                    ValidateSelectedMealPeriod();
                    RaiseCanExecuteChanged();
                }
            }
        }

        private DateTime? _fromDate = DateTime.Today;
        public DateTime? FromDate
        {
            get => _fromDate;
            set
            {
                if (!SetProperty(ref _fromDate, value))
                {
                    return;
                }

                ValidateBookingSchedule();
                RaiseCanExecuteChanged();
            }
        }

        private DateTime? _toDate = DateTime.Today;
        public DateTime? ToDate
        {
            get => _toDate;
            set
            {
                if (!SetProperty(ref _toDate, value))
                {
                    return;
                }

                ValidateBookingSchedule();
                RaiseCanExecuteChanged();
            }
        }

        private string _fromTimeText = "09:00";
        public string FromTimeText
        {
            get => _fromTimeText;
            set
            {
                if (!SetProperty(ref _fromTimeText, value))
                {
                    return;
                }

                ValidateBookingSchedule();
                RaiseCanExecuteChanged();
            }
        }

        private string _toTimeText = "10:00";
        public string ToTimeText
        {
            get => _toTimeText;
            set
            {
                if (!SetProperty(ref _toTimeText, value))
                {
                    return;
                }

                ApplyMidnightCrossoverIfNeeded();
                ValidateBookingSchedule();
                RaiseCanExecuteChanged();
            }
        }

        private string _bookingCustomerName;
        public string BookingCustomerName
        {
            get => _bookingCustomerName;
            set
            {
                SetProperty(ref _bookingCustomerName, value);
                ValidateBookingCustomerName();
                RaiseCanExecuteChanged();
            }
        }

        private int _bookingNumberOfPax = 1;
        public int BookingNumberOfPax
        {
            get => _bookingNumberOfPax;
            set
            {
                SetProperty(ref _bookingNumberOfPax, value);
                ValidateBookingNumberOfPax();
                RaiseCanExecuteChanged();
            }
        }

        private string _bookingCustomerPhoneNumber;
        public string BookingCustomerPhoneNumber
        {
            get => _bookingCustomerPhoneNumber;
            set
            {
                SetProperty(ref _bookingCustomerPhoneNumber, value);
                ValidateBookingCustomerPhone();
                RaiseCanExecuteChanged();
            }
        }

        private bool _isOverlayVisible;
        public bool IsOverlayVisible
        {
            get { return _isOverlayVisible; }
            set
            {
                _isOverlayVisible = value;
                OnPropertyChanged(nameof(IsOverlayVisible));
            }
        }

        private string _remarks;
        public string Remarks
        {
            get => _remarks;
            set => SetProperty(ref _remarks, value);
        }

        private BookingStatus _selectedStatus = BookingStatus.Pending;
        public BookingStatus SelectedStatus
        {
            get => _selectedStatus;
            set
            {
                if (SetProperty(ref _selectedStatus, value))
                {
                    RaiseCanExecuteChanged();
                    OnPropertyChanged(nameof(IsPayAdvanceVisible));
                    OnPropertyChanged(nameof(IsCheckInVisible));
                }
            }
        }

        private int? _customerId;
        public int? CustomerId
        {
            get => _customerId;
            set
            {
                if (SetProperty(ref _customerId, value))
                {
                    OnPropertyChanged(nameof(IsCustomerNameReadOnly));
                }
            }
        }

        public bool IsCustomerNameReadOnly => CustomerId.HasValue;

        private decimal _advancePayment;
        public decimal AdvancePayment
        {
            get => _advancePayment;
            set => SetProperty(ref _advancePayment, value);
        }

        public bool IsPayAdvanceVisible => SelectedStatus == BookingStatus.Pending && SelectedBooking != null;
        public bool IsCheckInVisible => SelectedStatus == BookingStatus.Confirmed && SelectedBooking != null;

        private bool CanCancelBooking => SelectedBooking != null && SelectedStatus != BookingStatus.Cancelled && SelectedStatus != BookingStatus.Completed;
        private bool CanAddAdvancePayment => SelectedBooking != null && SelectedStatus == BookingStatus.Pending;
        private bool CanCheckIn => SelectedBooking != null && SelectedStatus == BookingStatus.Confirmed;

        public IEnumerable<BookingStatus> BookingStatuses => Enum.GetValues(typeof(BookingStatus)).Cast<BookingStatus>();

        public bool CanSaveBooking =>
            !HasValidationErrorsFor(BookingFormProperties) &&
            SelectedBookingRoom != null &&
            SelectedMealPeriod != null &&
            BookingNumberOfPax > 0 &&
            !string.IsNullOrWhiteSpace(BookingCustomerName) &&
            !string.IsNullOrWhiteSpace(BookingCustomerPhoneNumber);

        string IDataErrorInfo.Error => null;

        string IDataErrorInfo.this[string columnName]
        {
            get
            {
                var errors = GetErrors(columnName);
                if (errors == null)
                {
                    return null;
                }

                foreach (var error in errors)
                {
                    return error?.ToString();
                }

                return null;
            }
        }

        #region Commands
        public ICommand SaveBookingCommand { get; }
        public ICommand NewBookingCommand { get; }
        public ICommand RefreshRoomsCommand { get; }
        public ICommand RefreshMealPeriodsCommand { get; }
        public ICommand RefreshBookingsCommand { get; }
        public ICommand GenerateBookingReportCommand { get; }
        public ICommand AddMealPeriodCommand { get; }
        public ICommand CancelBookingCommand { get; }
        public ICommand AddAdvancePaymentCommand { get; }
        public ICommand CheckInCommand { get; }
        public ICommand SearchCustomerCommand { get; }
        #endregion

        #region Methods
        private async Task InitializeAsync()
        {
            await LoadRoomsAsync();
            await LoadMealPeriodsAsync();
            await LoadBookingsAsync();
            CreateNewBooking();
        }

        private async Task LoadRoomsAsync()
        {
            try
            {
                var rooms = await _roomBookingRepository.GetAllRoomsAsync();
                var selectedRoomId = SelectedBookingRoom?.Id;

                BookableRooms.Clear();
                foreach (var room in rooms.Where(r => r.IsActive).OrderBy(r => r.Name))
                {
                    BookableRooms.Add(room);
                }

                if (selectedRoomId.HasValue)
                {
                    SelectedBookingRoom = BookableRooms.FirstOrDefault(r => r.Id == selectedRoomId.Value);
                }

                if (SelectedBookingRoom == null)
                {
                    SelectedBookingRoom = BookableRooms.FirstOrDefault();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load rooms: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task LoadMealPeriodsAsync()
        {
            try
            {
                var mealPeriods = await _mealPeriodRepository.GetAllAsync();
                var selectedMealPeriodId = SelectedMealPeriod?.Id;

                MealPeriods.Clear();
                foreach (var mealPeriod in mealPeriods.Where(m => m.IsActive).OrderBy(m => m.Name))
                {
                    MealPeriods.Add(mealPeriod);
                }

                if (selectedMealPeriodId.HasValue)
                {
                    SelectedMealPeriod = MealPeriods.FirstOrDefault(m => m.Id == selectedMealPeriodId.Value);
                }

                if (SelectedMealPeriod == null)
                {
                    SelectedMealPeriod = MealPeriods.FirstOrDefault();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load meal periods: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task LoadBookingsAsync()
        {
            try
            {
                var bookings = await _roomBookingRepository.GetAllBookingsAsync();

                BookingList.Clear();
                foreach (var booking in bookings)
                {
                    BookingList.Add(booking);
                }

                RaiseCanExecuteChanged();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load bookings: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task SaveBookingAsync()
        {
            ValidateBookingForm();

            if (SelectedBookingRoom != null && BookingNumberOfPax > SelectedBookingRoom.MaxCapacity)
            {
                MessageBox.Show(
                    $"No. Of Pax cannot exceed the selected room max capacity ({SelectedBookingRoom.MaxCapacity}).",
                    "Capacity Warning",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (HasValidationErrorsFor(BookingFormProperties))
            {
                MessageBox.Show("Please correct booking form validation errors before saving.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                if (_userSessionService.UserId <= 0)
                {
                    MessageBox.Show("User session is invalid. Please log in again before creating a booking.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var booking = new RoomBooking
                {
                    RoomId = SelectedBookingRoom.Id,
                    MealPeriodId = SelectedMealPeriod.Id,
                    From = BuildDateTime(FromDate.Value, FromTimeText),
                    To = BuildDateTime(ToDate.Value, ToTimeText),
                    CustomerName = BookingCustomerName,
                    CustomerPhoneNumber = BookingCustomerPhoneNumber,
                    NumberOfPax = BookingNumberOfPax,
                    Status = SelectedStatus,
                    Remarks = Remarks,
                    CustomerId = CustomerId,
                    AdvancePayment = AdvancePayment,
                    CreatedBy = _userSessionService.UserId
                };

                await _roomBookingRepository.CreateBookingAsync(booking);
                MessageBox.Show("Room booking created successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);

                await LoadBookingsAsync();
                await LoadRoomsAsync();
                await LoadMealPeriodsAsync();
                CreateNewBooking();
            }
            catch (InvalidOperationException ex)
            {
                MessageBox.Show(ex.Message, "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error creating booking: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task CancelBookingAsync()
        {
            if (SelectedBooking == null) return;

            var result = MessageBox.Show(
                $"Are you sure you want to cancel booking #{SelectedBooking.Id} for room '{SelectedBooking.RoomName}'?",
                "Confirm Cancel Booking",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                await _roomBookingRepository.UpdateBookingStatusAsync(SelectedBooking.Id, BookingStatus.Cancelled);
                SelectedBooking.Status = BookingStatus.Cancelled;
                SelectedStatus = BookingStatus.Cancelled;

                SelectedBooking = null;
                CreateNewBooking();

                MessageBox.Show("Booking cancelled successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);

                await LoadBookingsAsync();
                await LoadRoomsAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error cancelling booking: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task SearchCustomerAsync()
        {
            var phone = BookingCustomerPhoneNumber;
            if (string.IsNullOrWhiteSpace(phone))
            {
                MessageBox.Show("Please enter a phone number to search.", "Search", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var customer = await _customerRepository.GetCustomerByPhoneAsync(phone.Trim());
                if (customer != null)
                {
                    BookingCustomerName = customer.CustomerName;
                    CustomerId = customer.Id;
                    MessageBox.Show($"Existing customer found: {customer.CustomerName}", "Customer Search", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    CustomerId = null;
                    MessageBox.Show("No customer found with this phone number. Booking will be registered as a walk-in customer.", "Customer Search", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error searching customer: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task AddAdvancePaymentAsync(object parameter)
        {
            if (SelectedBooking == null) return;

            decimal amount = 0m;
            if (parameter != null)
            {
                decimal.TryParse(parameter.ToString(), out amount);
            }

            if (amount <= 0)
            {
                amount = AdvancePayment;
            }

            if (amount <= 0)
            {
                MessageBox.Show("Please enter a valid advance payment amount greater than zero.", "Invalid Amount", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                IsOverlayVisible = true;
                _dialogService.ShowDialog<CustomerAdvanceViewModel>(out var advanceVm);

                var paymentSuccess = MessageBox.Show(
                    $"Has the advance payment of {amount:C} been successfully saved in the Advance Payment window?",
                    "Confirm Payment",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (paymentSuccess == MessageBoxResult.Yes)
                {
                    SelectedBooking.AdvancePayment = amount;
                    SelectedBooking.Status = BookingStatus.Confirmed;

                    await _roomBookingRepository.UpdateBookingAsync(SelectedBooking);

                    SelectedStatus = BookingStatus.Confirmed;
                    AdvancePayment = amount;

                    MessageBox.Show($"Advance payment of {amount:C} saved and booking confirmed!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);

                    await LoadBookingsAsync();
                    await LoadRoomsAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error recording advance payment: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsOverlayVisible = false;
            }
        }

        private async Task CheckInAsync()
        {
            if (SelectedBooking == null) return;

            var result = MessageBox.Show(
                $"Check in booking #{SelectedBooking.Id} for room '{SelectedBooking.RoomName}'?",
                "Confirm Check-In",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                await _roomBookingRepository.UpdateBookingStatusAsync(SelectedBooking.Id, BookingStatus.CheckedIn);
                SelectedBooking.Status = BookingStatus.CheckedIn;
                SelectedStatus = BookingStatus.CheckedIn;

                MessageBox.Show("Check-in successful! Transitioning to POS Sales...", "Success", MessageBoxButton.OK, MessageBoxImage.Information);

                var nextWindow = _serviceProvider.GetRequiredService<SalesView>();
                nextWindow.DataContext = _serviceProvider.GetRequiredService<SalesViewModel>();

                Application.Current.MainWindow = nextWindow;
                nextWindow.Show();

                Application.Current.Windows.OfType<MainView>().FirstOrDefault()?.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during check-in: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion

        private void CreateNewBooking()
        {
            FromDate = DateTime.Today;
            ToDate = DateTime.Today;
            FromTimeText = "09:00";
            ToTimeText = "10:00";
            BookingNumberOfPax = 1;
            BookingCustomerName = string.Empty;
            BookingCustomerPhoneNumber = string.Empty;
            SelectedStatus = BookingStatus.Pending;
            Remarks = string.Empty;
            CustomerId = null;
            AdvancePayment = 0m;

            if (SelectedBookingRoom == null)
            {
                SelectedBookingRoom = BookableRooms.FirstOrDefault();
            }

            if (SelectedMealPeriod == null)
            {
                SelectedMealPeriod = MealPeriods.FirstOrDefault();
            }

            ClearBookingErrors();
            RaiseCanExecuteChanged();
        }

        private void RaiseCanExecuteChanged()
        {
            (SaveBookingCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
            (GenerateBookingReportCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
            (CancelBookingCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
            (AddAdvancePaymentCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
            (CheckInCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
            (SearchCustomerCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        }

        private async Task GenerateBookingReportAsync()
        {
            if (!BookingList.Any())
            {
                MessageBox.Show("No bookings found to generate the report.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                var reportData = BuildBookingReportTable();

                var saveDialog = new SaveFileDialog
                {
                    Filter = "PDF Files (*.pdf)|*.pdf",
                    FileName = $"RoomBookings_{DateTime.Now:yyyyMMdd_HHmm}.pdf",
                    Title = "Save Room Booking Report"
                };

                if (saveDialog.ShowDialog() != true)
                {
                    return;
                }

                await Task.Run(() =>
                {
                    try
                    {
                        ExportBookingReportToDisk(reportData, saveDialog.FileName);
                    }
                    catch (CrystalReportsException)
                    {
                        var csvPath = Path.ChangeExtension(saveDialog.FileName, ".csv");
                        ExportBookingReportToCsv(reportData, csvPath);

                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            MessageBox.Show(
                                "Crystal runtime could not load the report. A CSV export was created instead.",
                                "Crystal Report Fallback",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);
                        });
                    }
                    catch (TypeInitializationException ex) when (ex.InnerException != null)
                    {
                        var csvPath = Path.ChangeExtension(saveDialog.FileName, ".csv");
                        ExportBookingReportToCsv(reportData, csvPath);

                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            MessageBox.Show(
                                "Crystal initialization failed. A CSV export was created instead.",
                                "Crystal Report Fallback",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);
                        });
                    }
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error generating room booking report: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private DataTable BuildBookingReportTable()
        {
            var table = new DataTable("RoomBookings");
            table.Columns.Add("BookingId", typeof(int));
            table.Columns.Add("RoomName", typeof(string));
            table.Columns.Add("MealPeriodName", typeof(string));
            table.Columns.Add("FromDateTime", typeof(DateTime));
            table.Columns.Add("ToDateTime", typeof(DateTime));
            table.Columns.Add("CustomerName", typeof(string));
            table.Columns.Add("CustomerPhoneNumber", typeof(string));
            table.Columns.Add("NumberOfPax", typeof(int));
            table.Columns.Add("StatusText", typeof(string));

            foreach (var booking in BookingList.OrderBy(b => b.From))
            {
                table.Rows.Add(
                    booking.Id,
                    booking.RoomName ?? string.Empty,
                    booking.MealPeriodName ?? string.Empty,
                    booking.From,
                    booking.To,
                    booking.CustomerName ?? string.Empty,
                    booking.CustomerPhoneNumber ?? string.Empty,
                    booking.NumberOfPax,
                    booking.Status.ToString().ToUpper());
            }

            return table;
        }

        private void ExportBookingReportToDisk(DataTable reportData, string filePath)
        {
            using (var report = new ReportDocument())
            {
                var reportPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Reports", "RoomBookingReport.rpt");
                if (!File.Exists(reportPath))
                {
                    throw new FileNotFoundException("Report file missing: RoomBookingReport.rpt");
                }

                report.Load(reportPath);
                report.SetDataSource(reportData);

                SetDefaultRequiredParameters(report);
                SetReportParameterIfAvailable(report, "GeneratedOn", DateTime.Now);
                SetReportParameterIfAvailable(report, "ReportTitle", "Room Booking Report");

                report.ExportToDisk(ExportFormatType.PortableDocFormat, filePath);
            }

            Application.Current.Dispatcher.Invoke(() =>
            {
                if (MessageBox.Show("Room booking report saved. Open now?", "Success", MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
                {
                    Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });
                }
            });
        }

        private void ExportBookingReportToCsv(DataTable reportData, string filePath)
        {
            using (var writer = new StreamWriter(filePath, false, Encoding.UTF8))
            {
                for (var i = 0; i < reportData.Columns.Count; i++)
                {
                    if (i > 0)
                    {
                        writer.Write(",");
                    }

                    writer.Write(EscapeCsv(reportData.Columns[i].ColumnName));
                }

                writer.WriteLine();

                foreach (DataRow row in reportData.Rows)
                {
                    for (var i = 0; i < reportData.Columns.Count; i++)
                    {
                        if (i > 0)
                        {
                            writer.Write(",");
                        }

                        var value = row[i] == DBNull.Value ? string.Empty : row[i].ToString();
                        writer.Write(EscapeCsv(value));
                    }

                    writer.WriteLine();
                }
            }

            Application.Current.Dispatcher.Invoke(() =>
            {
                if (MessageBox.Show("CSV report saved. Open now?", "Success", MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
                {
                    Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });
                }
            });
        }

        private string EscapeCsv(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "\"\"";
            }

            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        private void SetReportParameterIfAvailable(ReportDocument report, string parameterName, object value)
        {
            if (report == null || string.IsNullOrWhiteSpace(parameterName))
            {
                return;
            }

            foreach (ParameterFieldDefinition parameterField in report.DataDefinition.ParameterFields)
            {
                if (string.Equals(parameterField.Name, parameterName, StringComparison.OrdinalIgnoreCase))
                {
                    report.SetParameterValue(parameterName, value);
                    break;
                }
            }
        }

        private void SetDefaultRequiredParameters(ReportDocument report)
        {
            if (report == null)
            {
                return;
            }

            foreach (ParameterFieldDefinition parameterField in report.DataDefinition.ParameterFields)
            {
                if (string.IsNullOrWhiteSpace(parameterField.Name))
                {
                    continue;
                }

                try
                {
                    report.SetParameterValue(parameterField.Name, DateTime.Now);
                    continue;
                }
                catch
                {
                }

                try
                {
                    report.SetParameterValue(parameterField.Name, 0);
                    continue;
                }
                catch
                {
                }

                try
                {
                    report.SetParameterValue(parameterField.Name, false);
                    continue;
                }
                catch
                {
                }

                try
                {
                    report.SetParameterValue(parameterField.Name, string.Empty);
                }
                catch
                {
                    // Ignore incompatible defaults and allow explicit parameter values to override.
                }
            }
        }

        private bool HasValidationErrorsFor(IEnumerable<string> propertyNames)
        {
            foreach (var propertyName in propertyNames)
            {
                if (GetErrors(propertyName) != null)
                {
                    return true;
                }
            }

            return false;
        }

        private DateTime BuildDateTime(DateTime date, string time)
        {
            TimeSpan parsedTime;
            if (!TryParseTimeText(time, out parsedTime))
            {
                throw new InvalidOperationException("Booking contains an invalid time value.");
            }

            return date.Date.Add(parsedTime);
        }

        #region Validation
        private void ValidateBookingForm()
        {
            ValidateSelectedBookingRoom();
            ValidateSelectedMealPeriod();
            ValidateBookingSchedule();
            ValidateBookingNumberOfPax();
            ValidateBookingCustomerName();
            ValidateBookingCustomerPhone();
        }

        private void ValidateSelectedBookingRoom()
        {
            ClearErrors(nameof(SelectedBookingRoom));

            if (SelectedBookingRoom == null)
            {
                AddError(nameof(SelectedBookingRoom), "Please select a room.");
            }
        }

        private void ValidateSelectedMealPeriod()
        {
            ClearErrors(nameof(SelectedMealPeriod));

            if (SelectedMealPeriod == null)
            {
                AddError(nameof(SelectedMealPeriod), "Please select a meal period.");
            }
        }

        private void ValidateBookingSchedule()
        {
            ClearErrors(nameof(FromDate));
            ClearErrors(nameof(ToDate));
            ClearErrors(nameof(FromTimeText));
            ClearErrors(nameof(ToTimeText));

            if (!FromDate.HasValue)
            {
                AddError(nameof(FromDate), "From date is required.");
            }

            if (!ToDate.HasValue)
            {
                AddError(nameof(ToDate), "To date is required.");
            }

            TimeSpan fromTime;
            TimeSpan toTime;
            var isFromTimeValid = TryParseTimeText(FromTimeText, out fromTime);
            var isToTimeValid = TryParseTimeText(ToTimeText, out toTime);

            if (!isFromTimeValid)
            {
                AddError(nameof(FromTimeText), "From time must be a valid 24-hour time in HH:mm format.");
            }

            if (!isToTimeValid)
            {
                AddError(nameof(ToTimeText), "To time must be a valid 24-hour time in HH:mm format.");
            }

            if (FromDate.HasValue && ToDate.HasValue && isFromTimeValid && isToTimeValid)
            {
                var fromDateTime = FromDate.Value.Date.Add(fromTime);
                var toDateTime = ToDate.Value.Date.Add(toTime);

                if (toDateTime <= fromDateTime)
                {
                    AddError(nameof(ToTimeText), "To date/time must be later than From date/time.");
                }
            }
        }

        private void ApplyMidnightCrossoverIfNeeded()
        {
            if (!FromDate.HasValue || !ToDate.HasValue || FromDate.Value.Date != ToDate.Value.Date)
            {
                return;
            }

            TimeSpan fromTime;
            TimeSpan toTime;
            if (!TryParseTimeText(FromTimeText, out fromTime) || !TryParseTimeText(ToTimeText, out toTime))
            {
                return;
            }

            if (toTime < fromTime)
            {
                ToDate = ToDate.Value.Date.AddDays(1);
            }
        }

        private static bool TryParseTimeText(string value, out TimeSpan time)
        {
            DateTime parsedTime;
            var isValid = DateTime.TryParseExact(
                value,
                "HH:mm",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out parsedTime);

            time = isValid ? parsedTime.TimeOfDay : TimeSpan.Zero;
            return isValid;
        }

        private void ValidateBookingCustomerName()
        {
            ClearErrors(nameof(BookingCustomerName));

            if (string.IsNullOrWhiteSpace(BookingCustomerName))
            {
                AddError(nameof(BookingCustomerName), "Customer name is required.");
            }
            if (!Regex.IsMatch(BookingCustomerName.Trim(), @"^[a-zA-Z\s\-\'\.]+$"))
            {
                AddError(nameof(BookingCustomerName), "Invalid phone number format.");
            }
        }

        private void ValidateBookingNumberOfPax()
        {
            ClearErrors(nameof(BookingNumberOfPax));

            if (BookingNumberOfPax <= 0)
            {
                AddError(nameof(BookingNumberOfPax), "No. Of Pax must be greater than zero.");
                return;
            }

            if (SelectedBookingRoom != null && BookingNumberOfPax > SelectedBookingRoom.MaxCapacity)
            {
                AddError(nameof(BookingNumberOfPax), $"Maximum room capacity is ({SelectedBookingRoom.MaxCapacity}).");
            }
        }

        private void ValidateBookingCustomerPhone()
        {
            ClearErrors(nameof(BookingCustomerPhoneNumber));

            if (string.IsNullOrWhiteSpace(BookingCustomerPhoneNumber))
            {
                AddError(nameof(BookingCustomerPhoneNumber), "Customer phone number is required.");
                return;
            }

            if (!Regex.IsMatch(BookingCustomerPhoneNumber.Trim(), @"^[0-9\+\-\s\(\)]{7,20}$"))
            {
                AddError(nameof(BookingCustomerPhoneNumber), "Invalid phone number format.");
            }
        }
        #endregion
        private void ClearBookingErrors()
        {
            foreach (var property in BookingFormProperties)
            {
                ClearErrors(property);
            }
        }

        private async void ExecuteOpenMealPeriod(object parameter)
        {
            try
            {
                IsOverlayVisible = true;

                _dialogService.ShowDialog<MealPeriodViewModel>(out var mealPeriodViewModel);

                if (mealPeriodViewModel == null)
                {
                    return;
                }

                if (mealPeriodViewModel.AddedMealPeriods != null && mealPeriodViewModel.AddedMealPeriods.Count > 0)
                {
                    foreach (var mealPeriod in mealPeriodViewModel.AddedMealPeriods)
                    {
                        if (!MealPeriods.Any(m => m.Id == mealPeriod.Id))
                        {
                            MealPeriods.Add(mealPeriod);
                        }
                    }

                    SelectedMealPeriod = mealPeriodViewModel.AddedMealPeriods.Last();
                }
                else
                {
                    await LoadMealPeriodsAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening meal period window: {ex.Message}\n\nStack Trace: {ex.StackTrace}");
            }
            finally
            {
                IsOverlayVisible = false;
            }
        }
    }
}
