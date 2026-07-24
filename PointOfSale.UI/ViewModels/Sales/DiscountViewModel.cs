using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using PointOfSale.Core.Interfaces.Repositories.Restaurant;
using PointOfSale.Core.Interfaces.Repositories.Sales;
using PointOfSale.Core.Models.Restaurant;
using PointOfSale.Core.Models.Sales;
using PointOfSale.Core.Services;
using PointOfSale.UI.Commands;

namespace PointOfSale.UI.ViewModels.Sales
{
    public class DiscountViewModel : BaseViewModel
    {
        private readonly IDiscountRepository _discountRepository;
        private readonly IMenuCategoryRepository _menuCategoryRepository;
        private readonly IUserSessionService _userSessionService;
        private const string TimeFormat = @"hh\:mm";

        public DiscountViewModel(
            IDiscountRepository discountRepository,
            IMenuCategoryRepository menuCategoryRepository,
            IUserSessionService userSessionService)
        {
            _discountRepository = discountRepository;
            _menuCategoryRepository = menuCategoryRepository;
            _userSessionService = userSessionService;

            DiscountTypes = new ObservableCollection<string> { "PERCENT", "FLAT" };
            ApplyScopes = new ObservableCollection<string> { "BILL", "CATEGORY" };
            DayOptions = new ObservableCollection<DayOption>
            {
                new DayOption{ Name = "All Days", Mask = 127 },
                new DayOption{ Name = "Weekdays", Mask = 2 + 4 + 8 + 16 + 32 },
                new DayOption{ Name = "Weekend", Mask = 1 + 64 },
                new DayOption{ Name = "Saturday", Mask = 64 },
                new DayOption{ Name = "Sunday", Mask = 1 }
            };

            DiscountList = new ObservableCollection<DiscountDefinition>();
            MenuCategories = new ObservableCollection<MenuCategory>();

            SaveCommand = new AsyncRelayCommand(async _ => await SaveAsync(), _ => CanSave);
            LoadCommand = new AsyncRelayCommand(async _ => await LoadAsync());
            EditCommand = new RelayCommand(_ => SetEditMode(true), _ => SelectedDiscount != null);
            ToggleActiveCommand = new AsyncRelayCommand(async _ => await ToggleActiveAsync(), _ => SelectedDiscount != null);

            ValidFrom = DateTime.Today;
            ValidTo = DateTime.Today.AddMonths(1);
            IsActive = true;
            SelectedDayOption = DayOptions.FirstOrDefault();
            SelectedApplyScope = "BILL";

            _ = LoadAsync();
        }

        public ObservableCollection<DiscountDefinition> DiscountList { get; }
        public ObservableCollection<string> DiscountTypes { get; }
        public ObservableCollection<string> ApplyScopes { get; }
        public ObservableCollection<DayOption> DayOptions { get; }
        public ObservableCollection<MenuCategory> MenuCategories { get; }

        private DiscountDefinition _selectedDiscount;
        public DiscountDefinition SelectedDiscount
        {
            get => _selectedDiscount;
            set
            {
                if (SetProperty(ref _selectedDiscount, value))
                {
                    SetEditMode(false);
                    RaiseCanExecuteChanged();
                }
            }
        }

        private bool _isEditing;
        public bool IsEditing
        {
            get => _isEditing;
            set
            {
                if (SetProperty(ref _isEditing, value))
                    OnPropertyChanged(nameof(SaveButtonText));
            }
        }

        public string SaveButtonText => IsEditing ? "Update" : "Save";

        private int _discountId;
        public int DiscountId { get => _discountId; set => SetProperty(ref _discountId, value); }

        private string _code;
        public string Code
        {
            get => _code;
            set
            {
                if (SetProperty(ref _code, value))
                {
                    ValidateCode();
                    RaiseCanExecuteChanged();
                }
            }
        }

        private string _name;
        public string Name
        {
            get => _name;
            set
            {
                if (SetProperty(ref _name, value))
                {
                    ValidateName();
                    RaiseCanExecuteChanged();
                }
            }
        }

        private string _selectedDiscountType = "PERCENT";
        public string SelectedDiscountType
        {
            get => _selectedDiscountType;
            set
            {
                if (SetProperty(ref _selectedDiscountType, value))
                {
                    ValidateDiscountType();
                    ValidateDiscountValue();
                    RaiseCanExecuteChanged();
                }
            }
        }

        private string _discountValue = "0.00";
        public string DiscountValue
        {
            get => _discountValue;
            set
            {
                if (SetProperty(ref _discountValue, value))
                {
                    ValidateDiscountValue();
                    RaiseCanExecuteChanged();
                }
            }
        }

        private string _minimumBillAmount = "0.00";
        public string MinimumBillAmount
        {
            get => _minimumBillAmount;
            set
            {
                if (SetProperty(ref _minimumBillAmount, value))
                {
                    ValidateMinimumBillAmount();
                    RaiseCanExecuteChanged();
                }
            }
        }

        private string _maximumDiscountAmount;
        public string MaximumDiscountAmount
        {
            get => _maximumDiscountAmount;
            set
            {
                if (SetProperty(ref _maximumDiscountAmount, value))
                {
                    ValidateMaximumDiscountAmount();
                    RaiseCanExecuteChanged();
                }
            }
        }

        private bool _isSingleUse;
        public bool IsSingleUse
        {
            get => _isSingleUse;
            set
            {
                if (SetProperty(ref _isSingleUse, value))
                {
                    if (_isSingleUse)
                        MaxRedemptionCount = "1";
                    else
                        MaxRedemptionCount = null;

                    ValidateMaxRedemptionCount();
                    RaiseCanExecuteChanged();
                }
            }
        }

        private string _maxRedemptionCount;
        public string MaxRedemptionCount
        {
            get => _maxRedemptionCount;
            set
            {
                if (SetProperty(ref _maxRedemptionCount, value))
                {
                    ValidateMaxRedemptionCount();
                    RaiseCanExecuteChanged();
                }
            }
        }

        private DateTime? _validFrom;
        public DateTime? ValidFrom
        {
            get => _validFrom;
            set
            {
                if (SetProperty(ref _validFrom, value))
                {
                    ValidateValidDates();
                    RaiseCanExecuteChanged();
                }
            }
        }

        private DateTime? _validTo;
        public DateTime? ValidTo
        {
            get => _validTo;
            set
            {
                if (SetProperty(ref _validTo, value))
                {
                    ValidateValidDates();
                    RaiseCanExecuteChanged();
                }
            }
        }

        private int? _branchId;
        public int? BranchId
        {
            get => _branchId;
            set => SetProperty(ref _branchId, value);
        }

        private bool _isActive;
        public bool IsActive
        {
            get => _isActive;
            set => SetProperty(ref _isActive, value);
        }

        private bool _isAutoApply;
        public bool IsAutoApply
        {
            get => _isAutoApply;
            set
            {
                if (SetProperty(ref _isAutoApply, value))
                {
                    OnPropertyChanged(nameof(IsAutoRuleVisible));
                    OnPropertyChanged(nameof(IsCategorySelectionVisible));
                    ValidateAutoApplyFields();
                    RaiseCanExecuteChanged();
                }
            }
        }

        private string _selectedApplyScope = "BILL";
        public string SelectedApplyScope
        {
            get => _selectedApplyScope;
            set
            {
                if (SetProperty(ref _selectedApplyScope, value))
                {
                    OnPropertyChanged(nameof(IsCategorySelectionVisible));
                    ValidateApplyScope();
                    ValidateMenuCategory();
                    RaiseCanExecuteChanged();
                }
            }
        }

        private int? _selectedMenuCategoryId;
        public int? SelectedMenuCategoryId
        {
            get => _selectedMenuCategoryId;
            set
            {
                if (SetProperty(ref _selectedMenuCategoryId, value))
                {
                    ValidateMenuCategory();
                    RaiseCanExecuteChanged();
                }
            }
        }

        private DayOption _selectedDayOption;
        public DayOption SelectedDayOption
        {
            get => _selectedDayOption;
            set
            {
                if (SetProperty(ref _selectedDayOption, value))
                {
                    ValidateDayOption();
                    RaiseCanExecuteChanged();
                }
            }
        }

        private string _startTimeText;
        public string StartTimeText
        {
            get => _startTimeText;
            set
            {
                if (SetProperty(ref _startTimeText, value))
                {
                    ValidateRuleTimes();
                    RaiseCanExecuteChanged();
                }
            }
        }

        private string _endTimeText;
        public string EndTimeText
        {
            get => _endTimeText;
            set
            {
                if (SetProperty(ref _endTimeText, value))
                {
                    ValidateRuleTimes();
                    RaiseCanExecuteChanged();
                }
            }
        }

        public bool IsAutoRuleVisible => IsAutoApply;
        public bool IsCategorySelectionVisible => IsAutoApply && string.Equals(SelectedApplyScope, "CATEGORY", StringComparison.OrdinalIgnoreCase);

        public bool CanSave =>
            !HasErrors &&
            !string.IsNullOrWhiteSpace(Code) &&
            !string.IsNullOrWhiteSpace(Name) &&
            TryGetDecimal(DiscountValue, out var discountValue) &&
            discountValue > 0 &&
            (!string.Equals(SelectedDiscountType, "PERCENT", StringComparison.OrdinalIgnoreCase) || discountValue <= 100) &&
            TryGetDecimal(MinimumBillAmount, out var minimumBillAmount) &&
            minimumBillAmount >= 0 &&
            TryGetOptionalDecimal(MaximumDiscountAmount, out var maximumDiscountAmount) &&
            (!maximumDiscountAmount.HasValue || maximumDiscountAmount.Value > 0) &&
            TryGetOptionalInt(MaxRedemptionCount, out var maxRedemptionCount) &&
            (!maxRedemptionCount.HasValue || maxRedemptionCount.Value > 0) &&
            (!ValidFrom.HasValue || !ValidTo.HasValue || ValidFrom <= ValidTo) &&
            (!IsAutoApply || IsAutoApplyConfigurationValid());

        public ICommand SaveCommand { get; }
        public ICommand LoadCommand { get; }
        public ICommand EditCommand { get; }
        public ICommand ToggleActiveCommand { get; }

        private async Task LoadAsync()
        {
            try
            {
                if (!MenuCategories.Any())
                {
                    var categories = await _menuCategoryRepository.GetAllAsync();
                    MenuCategories.Clear();
                    foreach (var category in categories.Where(x => x.IsActive).OrderBy(x => x.Name))
                        MenuCategories.Add(category);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load menu categories: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            try
            {
                var list = await _discountRepository.GetAllAsync();
                DiscountList.Clear();
                foreach (var item in list.OrderByDescending(x => x.DiscountId))
                    DiscountList.Add(item);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load discounts: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task SaveAsync()
        {
            try
            {
                ValidateAll();
                if (HasErrors || !CanSave)
                {
                    MessageBox.Show("Please correct the errors before saving.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!TryParseRuleTimes(out var startTime, out var endTime, out var parseError))
                {
                    MessageBox.Show(parseError, "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var model = BuildFormModel(startTime, endTime);

                if (IsEditing)
                {
                    model.DiscountId = DiscountId;
                    await _discountRepository.UpdateAsync(model);
                    MessageBox.Show("Discount updated successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    await _discountRepository.CreateAsync(model);
                    MessageBox.Show("Discount created successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }

                await LoadAsync();
                CreateNew();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save discount: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task ToggleActiveAsync()
        {
            if (SelectedDiscount == null)
                return;

            try
            {
                SelectedDiscount.IsActive = !SelectedDiscount.IsActive;
                SelectedDiscount.CreatedBy = _userSessionService.UserId;
                await _discountRepository.UpdateAsync(SelectedDiscount);
                await LoadAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to update status: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private DiscountDefinition BuildFormModel(TimeSpan? startTime, TimeSpan? endTime)
        {
            TryGetDecimal(DiscountValue, out var discountValue);
            TryGetDecimal(MinimumBillAmount, out var minimumBillAmount);
            TryGetOptionalDecimal(MaximumDiscountAmount, out var maximumDiscountAmount);
            TryGetOptionalInt(MaxRedemptionCount, out var maxRedemptionCount);

            return new DiscountDefinition
            {
                Code = (Code ?? string.Empty).Trim().ToUpperInvariant(),
                Name = (Name ?? string.Empty).Trim(),
                DiscountType = SelectedDiscountType,
                DiscountValue = discountValue,
                MinimumBillAmount = minimumBillAmount,
                MaximumDiscountAmount = maximumDiscountAmount,
                IsSingleUse = IsSingleUse,
                MaxRedemptionCount = IsSingleUse ? 1 : maxRedemptionCount,
                ValidFrom = ValidFrom,
                ValidTo = ValidTo,
                BranchId = BranchId,
                IsActive = IsActive,
                IsAutoApply = IsAutoApply,
                ApplyScope = IsAutoApply ? SelectedApplyScope : null,
                TargetMenuCategoryId = IsCategorySelectionVisible ? SelectedMenuCategoryId : null,
                DayOfWeekMask = IsAutoApply ? SelectedDayOption?.Mask : null,
                StartTime = IsAutoApply ? startTime : null,
                EndTime = IsAutoApply ? endTime : null,
                CreatedBy = _userSessionService.UserId
            };
        }

        private bool TryParseRuleTimes(out TimeSpan? startTime, out TimeSpan? endTime, out string error)
        {
            startTime = null;
            endTime = null;
            error = null;

            if (!IsAutoApply)
                return true;

            if (!TryGetOptionalTime(StartTimeText, out startTime))
            {
                error = "Invalid Start Time. Use format HH:mm (e.g., 16:00).";
                return false;
            }

            if (!TryGetOptionalTime(EndTimeText, out endTime))
            {
                error = "Invalid End Time. Use format HH:mm (e.g., 19:00).";
                return false;
            }

            return true;
        }

        private void CreateNew()
        {
            SelectedDiscount = null;
            IsEditing = false;

            DiscountId = 0;
            Code = string.Empty;
            Name = string.Empty;
            SelectedDiscountType = "PERCENT";
            DiscountValue = "0.00";
            MinimumBillAmount = "0.00";
            MaximumDiscountAmount = null;
            IsSingleUse = false;
            MaxRedemptionCount = null;
            ValidFrom = DateTime.Today;
            ValidTo = DateTime.Today.AddMonths(1);
            BranchId = null;
            IsActive = true;
            IsAutoApply = false;
            SelectedApplyScope = "BILL";
            SelectedMenuCategoryId = null;
            SelectedDayOption = DayOptions.FirstOrDefault();
            StartTimeText = string.Empty;
            EndTimeText = string.Empty;

            ClearAllErrors();
            RaiseCanExecuteChanged();
        }

        private void SetEditMode(bool isEditing)
        {
            if (!isEditing || SelectedDiscount == null)
            {
                IsEditing = false;
                return;
            }

            ClearAllErrors();
            IsEditing = true;

            DiscountId = SelectedDiscount.DiscountId;
            Code = SelectedDiscount.Code;
            Name = SelectedDiscount.Name;
            SelectedDiscountType = SelectedDiscount.DiscountType;
            DiscountValue = SelectedDiscount.DiscountValue.ToString("0.##");
            MinimumBillAmount = SelectedDiscount.MinimumBillAmount.ToString("0.##");
            MaximumDiscountAmount = SelectedDiscount.MaximumDiscountAmount?.ToString("0.##");
            IsSingleUse = SelectedDiscount.IsSingleUse;
            MaxRedemptionCount = SelectedDiscount.MaxRedemptionCount?.ToString(CultureInfo.CurrentCulture);
            ValidFrom = SelectedDiscount.ValidFrom;
            ValidTo = SelectedDiscount.ValidTo;
            BranchId = SelectedDiscount.BranchId;
            IsActive = SelectedDiscount.IsActive;

            IsAutoApply = SelectedDiscount.IsAutoApply;
            SelectedApplyScope = string.IsNullOrWhiteSpace(SelectedDiscount.ApplyScope) ? "BILL" : SelectedDiscount.ApplyScope;
            SelectedMenuCategoryId = SelectedDiscount.TargetMenuCategoryId;
            SelectedDayOption = DayOptions.FirstOrDefault(x => x.Mask == (SelectedDiscount.DayOfWeekMask ?? 127)) ?? DayOptions.FirstOrDefault();
            StartTimeText = SelectedDiscount.StartTime.HasValue ? SelectedDiscount.StartTime.Value.ToString(@"hh\:mm") : string.Empty;
            EndTimeText = SelectedDiscount.EndTime.HasValue ? SelectedDiscount.EndTime.Value.ToString(@"hh\:mm") : string.Empty;

            OnPropertyChanged(nameof(IsAutoRuleVisible));
            OnPropertyChanged(nameof(IsCategorySelectionVisible));
        }

        #region Validation
        private void ValidateAll()
        {
            ValidateCode();
            ValidateName();
            ValidateDiscountType();
            ValidateDiscountValue();
            ValidateMinimumBillAmount();
            ValidateMaximumDiscountAmount();
            ValidateMaxRedemptionCount();
            ValidateValidDates();
            ValidateAutoApplyFields();
            RaiseCanExecuteChanged();
        }

        private void ValidateCode()
        {
            ClearErrors(nameof(Code));

            if (string.IsNullOrWhiteSpace(Code))
                AddError(nameof(Code), "Code is required.");
            else if (Code.Trim().Length > 50)
                AddError(nameof(Code), "Code cannot exceed 50 characters.");
            else if (!Regex.IsMatch(Code.Trim(), @"^[a-zA-Z0-9\-_]+$"))
                AddError(nameof(Code), "Code contains invalid characters.");
        }

        private void ValidateName()
        {
            ClearErrors(nameof(Name));

            if (string.IsNullOrWhiteSpace(Name))
                AddError(nameof(Name), "Name is required.");
            else if (Name.Trim().Length > 150)
                AddError(nameof(Name), "Name cannot exceed 150 characters.");
            else if (!Regex.IsMatch(Name.Trim(), @"^[a-zA-Z0-9\s%]+$"))
                AddError(nameof(Name), "Name cannot contain special characters.");
        }

        private void ValidateDiscountType()
        {
            ClearErrors(nameof(SelectedDiscountType));

            if (string.IsNullOrWhiteSpace(SelectedDiscountType) || !DiscountTypes.Contains(SelectedDiscountType))
                AddError(nameof(SelectedDiscountType), "Please select a valid discount type.");
        }

        private void ValidateDiscountValue()
        {
            ClearErrors(nameof(DiscountValue));

            if (string.IsNullOrWhiteSpace(DiscountValue))
            {
                AddError(nameof(DiscountValue), "Discount value is required.");
                return;
            }

            if (!TryGetDecimal(DiscountValue, out var discountValue))
            {
                AddError(nameof(DiscountValue), "Discount value must be a valid number.");
                return;
            }

            if (discountValue <= 0)
                AddError(nameof(DiscountValue), "Discount value must be greater than zero.");
            else if (string.Equals(SelectedDiscountType, "PERCENT", StringComparison.OrdinalIgnoreCase) && discountValue > 100)
                AddError(nameof(DiscountValue), "Percentage discount cannot be greater than 100.");
        }

        private void ValidateMinimumBillAmount()
        {
            ClearErrors(nameof(MinimumBillAmount));

            if (string.IsNullOrWhiteSpace(MinimumBillAmount))
            {
                AddError(nameof(MinimumBillAmount), "Minimum bill amount is required.");
                return;
            }

            if (!TryGetDecimal(MinimumBillAmount, out var minimumBillAmount))
            {
                AddError(nameof(MinimumBillAmount), "Minimum bill amount must be a valid number.");
                return;
            }

            if (minimumBillAmount < 0)
                AddError(nameof(MinimumBillAmount), "Minimum bill amount cannot be negative.");
        }

        private void ValidateMaximumDiscountAmount()
        {
            ClearErrors(nameof(MaximumDiscountAmount));

            if (!TryGetOptionalDecimal(MaximumDiscountAmount, out var maximumDiscountAmount))
            {
                AddError(nameof(MaximumDiscountAmount), "Maximum discount amount must be a valid number.");
                return;
            }

            if (maximumDiscountAmount.HasValue && maximumDiscountAmount.Value <= 0)
                AddError(nameof(MaximumDiscountAmount), "Maximum discount amount must be greater than zero.");
        }

        private void ValidateMaxRedemptionCount()
        {
            ClearErrors(nameof(MaxRedemptionCount));

            if (!TryGetOptionalInt(MaxRedemptionCount, out var maxRedemptionCount))
            {
                AddError(nameof(MaxRedemptionCount), "Max redemption count must be a valid number.");
                return;
            }

            if (maxRedemptionCount.HasValue && maxRedemptionCount.Value <= 0)
                AddError(nameof(MaxRedemptionCount), "Max redemption count must be greater than zero.");
        }

        private void ValidateValidDates()
        {
            ClearErrors(nameof(ValidFrom));
            ClearErrors(nameof(ValidTo));

            if (ValidFrom.HasValue && ValidTo.HasValue && ValidFrom > ValidTo)
                AddError(nameof(ValidTo), "Valid To date must be greater than or equal to Valid From date.");
        }

        private void ValidateAutoApplyFields()
        {
            ValidateApplyScope();
            ValidateMenuCategory();
            ValidateDayOption();
            ValidateRuleTimes();
        }

        private void ValidateApplyScope()
        {
            ClearErrors(nameof(SelectedApplyScope));

            if (!IsAutoApply)
                return;

            if (string.IsNullOrWhiteSpace(SelectedApplyScope) || !ApplyScopes.Contains(SelectedApplyScope))
                AddError(nameof(SelectedApplyScope), "Please select a valid apply scope.");
        }

        private void ValidateMenuCategory()
        {
            ClearErrors(nameof(SelectedMenuCategoryId));

            if (IsCategorySelectionVisible && (SelectedMenuCategoryId ?? 0) <= 0)
                AddError(nameof(SelectedMenuCategoryId), "Please select a menu category.");
        }

        private void ValidateDayOption()
        {
            ClearErrors(nameof(SelectedDayOption));

            if (IsAutoApply && SelectedDayOption == null)
                AddError(nameof(SelectedDayOption), "Please select a day filter.");
        }

        private void ValidateRuleTimes()
        {
            ClearErrors(nameof(StartTimeText));
            ClearErrors(nameof(EndTimeText));

            if (!IsAutoApply)
                return;

            var hasValidStart = TryGetOptionalTime(StartTimeText, out var startTime);
            var hasValidEnd = TryGetOptionalTime(EndTimeText, out var endTime);

            if (!hasValidStart)
                AddError(nameof(StartTimeText), "Start time must use HH:mm format.");

            if (!hasValidEnd)
                AddError(nameof(EndTimeText), "End time must use HH:mm format.");

            if (hasValidStart && hasValidEnd && startTime.HasValue && endTime.HasValue && startTime.Value >= endTime.Value)
                AddError(nameof(EndTimeText), "End time must be greater than start time.");
        }
        #endregion

        #region Parsing Helpers
        private bool IsAutoApplyConfigurationValid()
        {
            if (!IsAutoApply)
                return true;

            if (string.IsNullOrWhiteSpace(SelectedApplyScope) || !ApplyScopes.Contains(SelectedApplyScope))
                return false;

            if (IsCategorySelectionVisible && (SelectedMenuCategoryId ?? 0) <= 0)
                return false;

            if (SelectedDayOption == null)
                return false;

            if (!TryGetOptionalTime(StartTimeText, out var startTime) || !TryGetOptionalTime(EndTimeText, out var endTime))
                return false;

            if (startTime.HasValue && endTime.HasValue && startTime.Value >= endTime.Value)
                return false;

            return true;
        }

        private bool TryGetDecimal(string value, out decimal result)
        {
            return decimal.TryParse(value?.Trim(), out result);
        }

        private bool TryGetOptionalDecimal(string value, out decimal? result)
        {
            result = null;

            if (string.IsNullOrWhiteSpace(value))
                return true;

            if (!decimal.TryParse(value.Trim(), out var parsedValue))
                return false;

            result = parsedValue;
            return true;
        }

        private bool TryGetOptionalInt(string value, out int? result)
        {
            result = null;

            if (string.IsNullOrWhiteSpace(value))
                return true;

            if (!int.TryParse(value.Trim(), out var parsedValue))
                return false;

            result = parsedValue;
            return true;
        }

        private bool TryGetOptionalTime(string value, out TimeSpan? result)
        {
            result = null;

            if (string.IsNullOrWhiteSpace(value))
                return true;

            if (!Regex.IsMatch(value.Trim(), @"^([01]\d|2[0-3]):[0-5]\d$"))
                return false;

            if (!TimeSpan.TryParseExact(value.Trim(), TimeFormat, CultureInfo.InvariantCulture, out var parsedTime))
                return false;

            result = parsedTime;
            return true;
        }
        #endregion

        private void RaiseCanExecuteChanged()
        {
            (SaveCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
            (EditCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (ToggleActiveCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        }
    }
}
