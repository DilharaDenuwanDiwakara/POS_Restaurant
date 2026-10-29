using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using PointOfSale.Core.Interfaces.Repositories.Restaurant;
using PointOfSale.Core.Interfaces.Repositories.Sales;
using PointOfSale.Core.Interfaces.Repositories.System;
using PointOfSale.Core.Models.Restaurant;
using PointOfSale.Core.Models.Sales;
using PointOfSale.Core.Models.System;
using PointOfSale.Core.DTOs;
using PointOfSale.Core.Interfaces.Services;
using PointOfSale.UI.Commands;
using Category = PointOfSale.Core.Models.Restaurant.MenuCategory;

namespace PointOfSale.UI.ViewModels.Sales
{
    public class BankPromotionViewModel : BaseViewModel
    {
        private readonly IBankPromotionRepository _promotions;
        private readonly IDialogService _dialogs;
        private readonly IBankRepository _banks;
        private readonly IMenuItemRepository _variants;
        private readonly IMenuCategoryRepository _categories;
        private int _promotionId;
        private int _variantLoadVersion;
        private int _binLoadVersion;

        public BankPromotionViewModel(IBankPromotionRepository promotions, IBankRepository banks, IMenuItemRepository variants, IMenuCategoryRepository categories, IDialogService dialogs)
        {
            _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
            _promotions = promotions ?? throw new ArgumentNullException(nameof(promotions));
            _banks = banks ?? throw new ArgumentNullException(nameof(banks));
            _variants = variants ?? throw new ArgumentNullException(nameof(variants));
            _categories = categories ?? throw new ArgumentNullException(nameof(categories));
            SaveBinCommand = new AsyncRelayCommand(_ => SaveBinAsync(), _ => !IsBusy && !IsLoadingBins && SelectedGlobalBank != null);
            EditBinCommand = new RelayCommand(p => EditBin((BankBINMapping)p), p => !IsBusy && !IsLoadingBins && p is BankBINMapping bin && bin.BankId == SelectedGlobalBank?.Id && BinPrefixes.Contains(bin));
            DeleteBinCommand = new AsyncRelayCommand(p => DeleteBinAsync((BankBINMapping)p), p => !IsBusy && !IsLoadingBins && p is BankBINMapping bin && bin.Id > 0 && bin.BankId == SelectedGlobalBank?.Id && BinPrefixes.Contains(bin));
            CancelBinEditCommand = new RelayCommand(_ => { ResetBinDraft(); ErrorMessage = StatusMessage = string.Empty; }, _ => !IsBusy && IsEditingBin);
            AddExcludedVariantCommand = new RelayCommand(_ => AddExcludedVariant(), _ => CanAddExcludedVariant);
            RemoveExcludedVariantCommand = new RelayCommand(p => ExcludedVariants.Remove((Variant)p), p => !IsBusy && p is Variant variant && ExcludedVariants.Contains(variant));
            SavePromotionCommand = new AsyncRelayCommand(_ => SaveAsync(), _ => !IsBusy);
            AddNewPromotionCommand = new RelayCommand(_ => { Clear(); SelectedTabIndex = 1; }, _ => !IsBusy);
            EditPromotionCommand = new RelayCommand(p => EditPromotion((BankPromotionDto)p), p => !IsBusy && p is BankPromotionDto promotion && OngoingPromotions.Contains(promotion));
            ClearCommand = new RelayCommand(_ => Clear(), _ => !IsBusy);
            BinPrefixes.CollectionChanged += (_, __) => { StatusMessage = string.Empty; RefreshBinCommands(); };
            ExcludedVariants.CollectionChanged += (_, __) => { StatusMessage = string.Empty; AddExcludedVariantCommand.RaiseCanExecuteChanged(); RemoveExcludedVariantCommand.RaiseCanExecuteChanged(); };
            OngoingPromotions.CollectionChanged += (_, __) => EditPromotionCommand.RaiseCanExecuteChanged();
            Initialization = InitializeAsync();
        }

        public Task Initialization { get; }
        public ObservableCollection<BankPromotionDto> OngoingPromotions { get; } = new ObservableCollection<BankPromotionDto>();
        private BankPromotionDto _selectedPromotion;
        public BankPromotionDto SelectedPromotion { get => _selectedPromotion; set => SetProperty(ref _selectedPromotion, value); }
        private int _selectedTabIndex;
        public int SelectedTabIndex { get => _selectedTabIndex; set => SetProperty(ref _selectedTabIndex, value); }
        private Bank _selectedGlobalBank;
        public Bank SelectedGlobalBank
        {
            get => _selectedGlobalBank;
            set
            {
                if (!SetProperty(ref _selectedGlobalBank, value)) return;
                ResetBinDraft();
                BinsLoadTask = LoadBinsAsync(value?.Id);
                RefreshBinCommands();
            }
        }
        public Task BinsLoadTask { get; private set; } = Task.CompletedTask;
        private bool _isLoadingBins;
        public bool IsLoadingBins
        {
            get => _isLoadingBins;
            private set { if (SetProperty(ref _isLoadingBins, value)) RefreshBinCommands(); }
        }
        public ObservableCollection<Bank> Banks { get; } = new ObservableCollection<Bank>();
        public ObservableCollection<BankBINMapping> BinPrefixes { get; } = new ObservableCollection<BankBINMapping>();
        public ObservableCollection<Variant> ExcludedVariants { get; } = new ObservableCollection<Variant>();
        public ObservableCollection<Category> Categories { get; } = new ObservableCollection<Category>();
        public ObservableCollection<Variant> AvailableVariants { get; } = new ObservableCollection<Variant>();
        public Task VariantsLoadTask { get; private set; } = Task.CompletedTask;
        private Bank _selectedBank;
        public Bank SelectedBank { get => _selectedBank; set { if (SetProperty(ref _selectedBank, value)) StatusMessage = string.Empty; } }
        private string _promoName = string.Empty;
        public string PromoName { get => _promoName; set { if (SetProperty(ref _promoName, value)) { ValidatePromoName(); StatusMessage = string.Empty; } } }
        // Keep raw input so invalid text cannot leave a previously valid decimal available to Save.
        private string _discountPercentage = string.Empty;
        public string DiscountPercentage { get => _discountPercentage; set { if (SetProperty(ref _discountPercentage, value)) { ValidatePercentage(out _); StatusMessage = string.Empty; } } }
        public string[] TimeOptions { get; } = Enumerable.Range(0, 96)
            .Select(i => TimeSpan.FromMinutes(i * 15).ToString(@"hh\:mm")).ToArray();
        private DateTime? _validFromDate = DateTime.Today;
        public DateTime? ValidFromDate { get => _validFromDate; set { if (SetProperty(ref _validFromDate, value)) ClearValidityErrors(); } }
        private string _validFromTime = "00:00";
        public string ValidFromTime { get => _validFromTime; set { if (SetProperty(ref _validFromTime, value)) ClearValidityErrors(); } }
        private DateTime? _validToDate = DateTime.Today;
        public DateTime? ValidToDate { get => _validToDate; set { if (SetProperty(ref _validToDate, value)) ClearValidityErrors(); } }
        private string _validToTime = "23:59:59";
        public string ValidToTime { get => _validToTime; set { if (SetProperty(ref _validToTime, value)) ClearValidityErrors(); } }
        public DateTime ValidFrom { get; private set; }
        public DateTime ValidTo { get; private set; }

        private void ClearValidityErrors()
        {
            ClearErrors(nameof(ValidFromDate)); ClearErrors(nameof(ValidFromTime));
            ClearErrors(nameof(ValidToDate)); ClearErrors(nameof(ValidToTime));
            StatusMessage = string.Empty;
        }

        private bool ValidateValidity()
        {
            ClearValidityErrors();
            var from = CombineTimestamp(ValidFromDate, ValidFromTime, nameof(ValidFromDate), nameof(ValidFromTime));
            var to = CombineTimestamp(ValidToDate, ValidToTime, nameof(ValidToDate), nameof(ValidToTime));
            if (!from.HasValue || !to.HasValue) return false;
            if (to.Value <= from.Value)
            {
                AddError(nameof(ValidToTime), "Valid To must be strictly later than Valid From.");
                return false;
            }
            ValidFrom = from.Value; ValidTo = to.Value;
            OnPropertyChanged(nameof(ValidFrom)); OnPropertyChanged(nameof(ValidTo));
            return true;
        }

        private DateTime? CombineTimestamp(DateTime? date, string time, string dateProperty, string timeProperty)
        {
            if (!date.HasValue || date.Value.Year < 1753)
                AddError(dateProperty, "Choose a date from 1753 through 9999.");
            var validTime = DateTime.TryParseExact(time, new[] { "HH:mm", "HH:mm:ss" }, CultureInfo.InvariantCulture,
                DateTimeStyles.NoCurrentDateDefault, out var parsedTime);
            if (!validTime) AddError(timeProperty, "Enter a 24-hour time as HH:mm or HH:mm:ss.");
            if (!date.HasValue || date.Value.Year < 1753 || !validTime) return null;
            return DateTime.SpecifyKind(date.Value.Date.Add(parsedTime.TimeOfDay), DateTimeKind.Unspecified);
        }

        public string[] CardTypes { get; } = { "CREDIT", "DEBIT" };
        public string[] RequiredCardTypes { get; } = { "ALL", "CREDIT", "DEBIT" };
        private string _requiredCardType = "ALL";
        public string RequiredCardType { get => _requiredCardType; set => SetProperty(ref _requiredCardType, value?.Trim().ToUpperInvariant()); }
        private string _binCardType;
        public string BinCardType { get => _binCardType; set => SetProperty(ref _binCardType, value?.Trim().ToUpperInvariant()); }
        private string _binCardBrand;
        public string BinCardBrand { get => _binCardBrand; set => SetProperty(ref _binCardBrand, value?.ToUpperInvariant()); }
        private bool _isActive = true;
        public bool IsActive { get => _isActive; set { if (SetProperty(ref _isActive, value)) StatusMessage = string.Empty; } }
        private string _binPrefix = string.Empty;
        public string BinPrefix { get => _binPrefix; set { if (SetProperty(ref _binPrefix, value)) ValidateBin(false); } }
        private bool _isBinActive = true;
        public bool IsBinActive { get => _isBinActive; set => SetProperty(ref _isBinActive, value); }
        private Category _selectedCategory;
        public Category SelectedCategory
        {
            get => _selectedCategory;
            set
            {
                if (SetProperty(ref _selectedCategory, value))
                    VariantsLoadTask = LoadVariantsAsync(value?.Id);
            }
        }
        private Variant _selectedVariant;
        public Variant SelectedVariant
        {
            get => _selectedVariant;
            set { if (SetProperty(ref _selectedVariant, value)) AddExcludedVariantCommand?.RaiseCanExecuteChanged(); }
        }
        private bool _isLoadingVariants;
        public bool IsLoadingVariants
        {
            get => _isLoadingVariants;
            private set
            {
                if (!SetProperty(ref _isLoadingVariants, value)) return;
                OnPropertyChanged(nameof(CanSelectVariant));
                AddExcludedVariantCommand.RaiseCanExecuteChanged();
            }
        }
        public bool CanSelectVariant => !IsBusy && !IsLoadingVariants && AvailableVariants.Count > 0;
        private bool CanAddExcludedVariant => CanSelectVariant && SelectedVariant != null &&
            AvailableVariants.Any(v => v.Id == SelectedVariant.Id) && !ExcludedVariants.Any(v => v.Id == SelectedVariant.Id);
        private string _variantStatusMessage = "Select a category to see its variants.";
        public string VariantStatusMessage { get => _variantStatusMessage; private set => SetProperty(ref _variantStatusMessage, value); }
        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (!SetProperty(ref _isBusy, value)) return;
                OnPropertyChanged(nameof(IsFormEnabled));
                OnPropertyChanged(nameof(CanSelectVariant));
                RefreshBinCommands();
                OnPropertyChanged(nameof(CanChangeGlobalBank));
                AddNewPromotionCommand.RaiseCanExecuteChanged(); EditPromotionCommand.RaiseCanExecuteChanged();
                AddExcludedVariantCommand.RaiseCanExecuteChanged();
                RemoveExcludedVariantCommand.RaiseCanExecuteChanged(); SavePromotionCommand.RaiseCanExecuteChanged();
                ClearCommand.RaiseCanExecuteChanged();
            }
        }
        public bool IsFormEnabled => !IsBusy;
        private string _statusMessage = string.Empty;
        public string StatusMessage
        {
            get => _statusMessage;
            private set { if (SetProperty(ref _statusMessage, value)) OnPropertyChanged(nameof(HasStatusMessage)); }
        }
        public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);
        public AsyncRelayCommand SaveBinCommand { get; }
        public AsyncRelayCommand AddBinCommand => SaveBinCommand;
        public RelayCommand EditBinCommand { get; }
        public AsyncRelayCommand DeleteBinCommand { get; }
        public RelayCommand CancelBinEditCommand { get; }
        private BankBINMapping _editingBin;
        public bool IsEditingBin => _editingBin != null;
        public string BinSaveButtonText => IsEditingBin ? "Save BIN" : "Add BIN";
        public bool CanChangeGlobalBank => !IsBusy && !IsEditingBin;

        public RelayCommand AddNewPromotionCommand { get; }
        public RelayCommand EditPromotionCommand { get; }
        public RelayCommand AddExcludedVariantCommand { get; }
        public RelayCommand RemoveExcludedVariantCommand { get; }
        public AsyncRelayCommand SavePromotionCommand { get; }
        public AsyncRelayCommand SaveCommand => SavePromotionCommand;
        public RelayCommand ClearCommand { get; }

        private void RefreshBinCommands()
        {
            SaveBinCommand.RaiseCanExecuteChanged();
            EditBinCommand.RaiseCanExecuteChanged();
            DeleteBinCommand.RaiseCanExecuteChanged();
            CancelBinEditCommand.RaiseCanExecuteChanged();
        }

        private void NotifyBinEditChanged()
        {
            OnPropertyChanged(nameof(IsEditingBin));
            OnPropertyChanged(nameof(BinSaveButtonText));
            OnPropertyChanged(nameof(CanChangeGlobalBank));
            RefreshBinCommands();
        }

        private void ResetBinDraft()
        {
            _editingBin = null;
            BinPrefix = string.Empty;
            IsBinActive = true;
            BinCardType = BinCardBrand = null;
            ClearErrors(nameof(BinPrefix));
            NotifyBinEditChanged();
        }

        private async Task DeleteBinAsync(BankBINMapping bin)
        {
            IsBusy = true;
            try
            {
                if (!_dialogs.ShowConfirmation($"Delete BIN {bin.BINPrefix} from {SelectedGlobalBank.BankName}?", "Delete BIN")) return;
                ErrorMessage = StatusMessage = string.Empty;
                await _promotions.DeleteBinAsync(bin.Id, bin.BankId);
                // Update the list only after the database confirms the deletion.
                if (SelectedGlobalBank?.Id == bin.BankId)
                {
                    var row = BinPrefixes.FirstOrDefault(b => b.Id == bin.Id);
                    if (row != null) BinPrefixes.Remove(row);
                    if (_editingBin?.Id == bin.Id) ResetBinDraft();
                }
                StatusMessage = $"BIN {bin.BINPrefix} deleted successfully.";
            }
            catch (Exception ex) { ErrorMessage = "Unable to delete BIN: " + ex.Message; }
            finally { IsBusy = false; }
        }

        private void EditBin(BankBINMapping bin)
        {
            _editingBin = new BankBINMapping { Id = bin.Id, BankId = bin.BankId, BINPrefix = bin.BINPrefix, IsActive = bin.IsActive, CardType = bin.CardType, CardBrand = bin.CardBrand };
            BinPrefix = bin.BINPrefix;
            IsBinActive = bin.IsActive;
            BinCardType = bin.CardType; BinCardBrand = bin.CardBrand;
            ErrorMessage = StatusMessage = string.Empty;
            NotifyBinEditChanged();
        }
        private async Task InitializeAsync()
        {
            IsBusy = true;
            try
            {
                var banksTask = _banks.GetAllAsync();
                var categoriesTask = _categories.GetAllAsync();
                await Task.WhenAll(banksTask, categoriesTask);
                foreach (var bank in await banksTask) Banks.Add(bank);
                foreach (var category in (await categoriesTask).Where(c => c.IsActive).OrderBy(c => c.DisplayOrder).ThenBy(c => c.Name))
                    Categories.Add(category);
                await RefreshPromotionsAsync();
            }
            catch (Exception ex) { ErrorMessage = "Unable to load promotion settings: " + ex.Message; }
            finally { IsBusy = false; }
        }

        private async Task RefreshPromotionsAsync()
        {
            var promotions = await _promotions.GetAllAsync();
            OngoingPromotions.Clear();
            foreach (var promotion in promotions) OngoingPromotions.Add(promotion);
        }

        private void EditPromotion(BankPromotionDto promotion)
        {
            Clear();
            SelectedPromotion = promotion;
            _promotionId = promotion.Id;
            SelectedBank = Banks.FirstOrDefault(b => b.Id == promotion.BankId);
            PromoName = promotion.PromoName;
            DiscountPercentage = promotion.DiscountPercentage.ToString("0.##", CultureInfo.CurrentCulture);
            IsActive = promotion.IsActive;
            RequiredCardType = promotion.RequiredCardType;
            // The initial validity migration used 1900-01-01 for previously unscheduled offers.
            // Present today's date as the draft default; persist it only when the user saves.
            ValidFromDate = promotion.ValidFrom == new DateTime(1900, 1, 1)
                ? DateTime.Today : promotion.ValidFrom.Date;
            ValidFromTime = promotion.ValidFrom.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
            // Replace the legacy open-ended placeholder only in the edit draft.
            ValidToDate = promotion.ValidTo >= new DateTime(9999, 12, 31, 23, 59, 59)
                ? DateTime.Today : promotion.ValidTo.Date;
            ValidToTime = promotion.ValidTo.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
            // Copy rows so removing or changing a draft cannot mutate the master list.
            foreach (var variant in promotion.ExcludedVariants)
                ExcludedVariants.Add(new Variant { Id = variant.Id, ItemCode = variant.ItemCode, Name = variant.Name });
            SelectedTabIndex = 1;
        }

        private async Task LoadBinsAsync(int? bankId)
        {
            var version = ++_binLoadVersion;
            BinPrefixes.Clear();
            ErrorMessage = StatusMessage = string.Empty;
            IsLoadingBins = bankId.HasValue;
            if (!bankId.HasValue) return;
            try
            {
                var bins = await _promotions.GetBinsAsync(bankId.Value);
                if (version != _binLoadVersion) return;
                foreach (var bin in bins) BinPrefixes.Add(bin);
            }
            catch (Exception ex)
            {
                if (version == _binLoadVersion) ErrorMessage = "Unable to load bank BINs: " + ex.Message;
            }
            finally { if (version == _binLoadVersion) IsLoadingBins = false; }
        }

        private async Task SaveBinAsync()
        {
            ErrorMessage = StatusMessage = string.Empty;
            if (SelectedGlobalBank == null) { ErrorMessage = "Select a bank for the BIN."; return; }
            if (!ValidateBin(true))
            { ErrorMessage = "Enter exactly six digits for the BIN prefix."; return; }
            if (BinPrefixes.Any(b => b.BINPrefix == BinPrefix && b.Id != (_editingBin?.Id ?? 0)))
            { ErrorMessage = "That BIN prefix is already saved for this bank."; return; }
            var bin = new BankBINMapping { Id = _editingBin?.Id ?? 0, BankId = SelectedGlobalBank.Id, BINPrefix = BinPrefix, IsActive = IsBinActive, CardType = BinCardType, CardBrand = BinCardBrand };
            IsBusy = true;
            try
            {
                var updating = IsEditingBin;
                if (updating) await _promotions.UpdateBinAsync(bin);
                else bin.Id = await _promotions.AddBinAsync(bin);
                if (bin.Id <= 0) throw new InvalidOperationException("The database did not confirm the BIN was saved.");
                // A programmatic bank switch during the request must not display another bank's BIN.
                if (SelectedGlobalBank?.Id == bin.BankId)
                {
                    if (updating)
                    {
                        var original = BinPrefixes.FirstOrDefault(b => b.Id == bin.Id);
                        if (original != null) BinPrefixes[BinPrefixes.IndexOf(original)] = bin;
                    }
                    else BinPrefixes.Add(bin);
                    ResetBinDraft();
                }
                StatusMessage = "BIN saved successfully.";
            }
            catch (Exception ex) { ErrorMessage = "Unable to save BIN: " + ex.Message; }
            finally { IsBusy = false; }
        }

        private bool ValidatePromoName()
        {
            ClearErrors(nameof(PromoName));
            if (!string.IsNullOrWhiteSpace(PromoName) && PromoName.Trim().Length <= 200 && !PromoName.Any(char.IsControl)) return true;
            AddError(nameof(PromoName), "Enter a promotion name of 1–200 characters without line breaks or control characters.");
            return false;
        }

        private bool ValidatePercentage(out decimal percentage)
        {
            ClearErrors(nameof(DiscountPercentage));
            var text = DiscountPercentage?.Trim() ?? string.Empty;
            var separator = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
            var parts = text.Split(new[] { separator }, StringSplitOptions.None);
            var validFormat = parts.Length <= 2 && parts[0].Length > 0 && parts[0].All(c => c >= '0' && c <= '9') &&
                (parts.Length == 1 || (parts[1].Length >= 1 && parts[1].Length <= 2 && parts[1].All(c => c >= '0' && c <= '9')));
            percentage = 0;
            if (validFormat && decimal.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.CurrentCulture, out percentage) && percentage > 0 && percentage <= 100) return true;
            AddError(nameof(DiscountPercentage), "Enter a percentage greater than 0 and at most 100, with up to two decimal places.");
            return false;
        }

        private bool ValidateBin(bool required)
        {
            ClearErrors(nameof(BinPrefix));
            if (!required && string.IsNullOrEmpty(BinPrefix)) return true;
            if (BinPrefix != null && BinPrefix.Length == 6 && BinPrefix.All(c => c >= '0' && c <= '9')) return true;
            AddError(nameof(BinPrefix), "BIN prefix must contain exactly six digits (0–9).");
            return false;
        }

        private async Task LoadVariantsAsync(int? categoryId)
        {
            var loadVersion = ++_variantLoadVersion;
            ErrorMessage = string.Empty;
            SelectedVariant = null;
            AvailableVariants.Clear();
            OnPropertyChanged(nameof(CanSelectVariant));
            IsLoadingVariants = categoryId.HasValue;
            VariantStatusMessage = categoryId.HasValue ? "Loading variants..." : "Select a category to see its variants.";
            if (!categoryId.HasValue) return;
            try
            {
                var variants = await _variants.GetVariantsByCategoryIdAsync(categoryId.Value);
                // A previous category request must not replace the current dropdown contents.
                if (loadVersion != _variantLoadVersion) return;
                foreach (var variant in variants.GroupBy(v => v.Id).Select(g => g.First())) AvailableVariants.Add(variant);
                VariantStatusMessage = AvailableVariants.Count == 0 ? "No variants in this category." : "Select a variant to exclude.";
            }
            catch (Exception ex)
            {
                if (loadVersion != _variantLoadVersion) return;
                ErrorMessage = "Unable to load variants: " + ex.Message;
                VariantStatusMessage = "Choose another category or select this category again to retry.";
            }
            finally { if (loadVersion == _variantLoadVersion) IsLoadingVariants = false; }
        }

        private void AddExcludedVariant()
        {
            ErrorMessage = string.Empty;
            if (!CanSelectVariant || SelectedVariant == null || !AvailableVariants.Any(v => v.Id == SelectedVariant.Id)) return;
            if (ExcludedVariants.Any(v => v.Id == SelectedVariant.Id))
            { ErrorMessage = "That variant is already excluded."; return; }
            ExcludedVariants.Add(SelectedVariant);
        }

        private async Task SaveAsync()
        {
            ErrorMessage = StatusMessage = string.Empty;
            if (SelectedBank == null || !Banks.Any(b => b.Id == SelectedBank.Id))
            { ErrorMessage = "Select a bank."; return; }
            var validName = ValidatePromoName();
            var validPercentage = ValidatePercentage(out var percentage);
            var validDates = ValidateValidity();
            if (!validName || !validPercentage || !validDates)
            { ErrorMessage = "Correct the highlighted fields before saving."; return; }
            IsBusy = true;
            try
            {
                var promotionName = PromoName.Trim();
                var savedId = await _promotions.SaveAsync(new BankPromotion
                {
                    Id = _promotionId, BankId = SelectedBank.Id, PromoName = PromoName.Trim(),
                    DiscountPercentage = percentage, IsActive = IsActive, ValidFrom = ValidFrom, ValidTo = ValidTo, RequiredCardType = RequiredCardType
                }, ExcludedVariants.Select(v => v.Id).ToList());
                if (savedId <= 0) throw new InvalidOperationException("The database did not confirm the promotion was saved.");
                Clear();
                SelectedTabIndex = 0;
                // A committed insert must not remain in the editor if refreshing the list fails.
                try { await RefreshPromotionsAsync(); }
                catch (Exception ex) { ErrorMessage = "Promotion saved, but the list could not be refreshed: " + ex.Message; }
                StatusMessage = $"Bank promotion \"{promotionName}\" saved successfully (ID: {savedId}).";
            }
            catch (Exception ex) { ErrorMessage = "Unable to save promotion: " + ex.Message; }
            finally { IsBusy = false; }
        }

        private void Clear()
        {
            _promotionId = 0;
            RequiredCardType = "ALL";
            SelectedPromotion = null;
            SelectedBank = null; PromoName = string.Empty; DiscountPercentage = string.Empty; IsActive = true;
            ValidFromDate = DateTime.Today; ValidFromTime = "00:00";
            ValidToDate = DateTime.Today; ValidToTime = "23:59:59";
            ValidFrom = ValidTo = default;
            SelectedCategory = null; SelectedVariant = null;
            ExcludedVariants.Clear(); AvailableVariants.Clear();
            ErrorMessage = StatusMessage = string.Empty;
            ClearAllErrors();
        }
    }
}
