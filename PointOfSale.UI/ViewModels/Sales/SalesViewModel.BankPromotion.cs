using System;
using System.Linq;
using System.Threading.Tasks;
using PointOfSale.Core.DTOs;
using PointOfSale.Core.Services;

namespace PointOfSale.UI.ViewModels.Sales
{
    // Payment state lives in SalesViewModel in this application.
    public partial class SalesViewModel
    {
        private BankPromoWithExclusionsDto _bankPromotion;
        private string _bankPromotionReference = string.Empty;
        private decimal? _recalledBankDiscountAmount;
        private int _bankEvaluationVersion;
        private bool _isBankPromotionEvaluating;
        private bool _bankPromotionLookupFailed;
        private decimal _bankDiscountAmount;
        private string _bankPromotionMessage;
        private const string BankPaymentMessage = "Split payments are not allowed for Bank Promotions. Please clear payments and use a single card.";

        public Task BankPromotionEvaluation { get; private set; } = Task.CompletedTask;

        private void OnCardReferenceChanged()
        {
            // User edits invalidate older lookups and any payment tied to the old reference.
            ResetBankPromotion();
            if (IsPaymentTypeCard && ReferenceNumber?.Length == 6)
                BankPromotionEvaluation = ApplyBankPromotionAsync(ReferenceNumber);
        }

        // Adding a payment clears its input field, but its saved reference and discount
        // belong to the payment row until the cashier edits/removes that payment.
        private void ClearReferenceEntryAfterPayment()
        {
            _referenceNumber = string.Empty;
            OnPropertyChanged(nameof(ReferenceNumber));
            OnPropertyChanged(nameof(CardReferenceNumber));
        }

        public bool HasBankPromotion => _bankPromotion != null;
        public string PromoType => HasBankPromotion ? "BANK" : BillDiscount > 0 ? "CASH" : null;
        public int? PromoBankId => HasBankPromotion ? (int?)_bankPromotion.BankId : null;
        public decimal BankDiscountAmount
        {
            get => _bankDiscountAmount;
            private set => SetProperty(ref _bankDiscountAmount, value);
        }
        public string BankPromotionMessage
        {
            get => _bankPromotionMessage;
            private set => SetProperty(ref _bankPromotionMessage, value);
        }
        public bool IsBankPromotionEvaluating
        {
            get => _isBankPromotionEvaluating;
            private set
            {
                SetProperty(ref _isBankPromotionEvaluating, value);
                RaiseSaveCommandState();
            }
        }

        private async Task ApplyBankPromotionAsync(string referenceNumber)
        {
            var version = ++_bankEvaluationVersion;
            var bin = referenceNumber;
            IsBankPromotionEvaluating = true;
            _bankPromotionLookupFailed = false;
            try
            {
                var result = await new CheckoutService(_salesRepository).ApplyBankPromotionAsync(bin, CartItems.ToList());
                if (version != _bankEvaluationVersion) return;
                // Recheck mutable state after awaiting the database.
                if (result.DiscountAmount > 0 && (AppliedPayments.Any() || LineDiscountPercent > 0 || HasAppliedDiscountCode || HasAppliedLoyaltyPoints || _autoBillDiscountAmount > 0))
                    throw new InvalidOperationException("Clear payments and other bill discounts, then re-enter the six-digit reference to apply the bank promotion.");
                _bankPromotionReference = referenceNumber;
                _bankPromotion = result.DiscountAmount > 0 ? result.Promotion : null;
                NotifyBankPromotion();
                CalculateTotals();
                BankPromotionMessage = HasBankPromotion
                    ? $"{_bankPromotion.PromoName}: Rs. {BankDiscountAmount:N2}. Pay the full invoice with this card."
                    : "No eligible bank promotion for this BIN and cart.";
                if (IsPaymentTypeCard) CurrentPaymentAmount = RemainingBalance;
            }
            catch (Exception ex)
            {
                if (version != _bankEvaluationVersion) return;
                _bankPromotionLookupFailed = true;
                BankPromotionMessage = ex.Message;
            }
            finally
            {
                if (version == _bankEvaluationVersion) IsBankPromotionEvaluating = false;
            }
        }

        private void ResetBankPromotion()
        {
            _bankEvaluationVersion++;
            if (HasBankPromotion) AppliedPayments.Clear();
            _bankPromotion = null;
            _bankPromotionReference = string.Empty;
            _recalledBankDiscountAmount = null;
            _bankPromotionLookupFailed = false;
            IsBankPromotionEvaluating = false;
            BankPromotionMessage = string.Empty;
            NotifyBankPromotion();
            CalculateTotals();
        }

        private void NotifyBankPromotion()
        {
            OnPropertyChanged(nameof(HasBankPromotion));
            OnPropertyChanged(nameof(PromoType));
            OnPropertyChanged(nameof(PromoBankId));
        }

        private bool BankPaymentEntryIsValid() => !IsBankPromotionEvaluating && !_bankPromotionLookupFailed &&
            (!HasBankPromotion || (IsPaymentTypeCard && !AppliedPayments.Any() &&
                CurrentPaymentAmount == NetAmount && BankDiscountAmount > 0));

        private bool BankPaymentsAreValid() => !IsBankPromotionEvaluating && !_bankPromotionLookupFailed &&
            (!HasBankPromotion || (BankDiscountAmount > 0 && AppliedPayments.Count == 1 &&
                AppliedPayments[0].PaymentMethod == CardPaymentMethod && AppliedPayments[0].Amount == NetAmount &&
                AppliedPayments[0].PaymentTerminalId.HasValue && HasSixDigitReference(AppliedPayments[0].ReferenceNumber) &&
                AppliedPayments[0].ReferenceNumber == _bankPromotionReference));

        private async Task RevalidateBankPromotionForSaveAsync()
        {
            if (!HasBankPromotion) return;
            var previousDiscount = BankDiscountAmount;
            var previousId = _bankPromotion.BankPromotionId;
            var current = await new CheckoutService(_salesRepository).ApplyBankPromotionAsync(AppliedPayments.Single().ReferenceNumber, CartItems.ToList());
            if (current.Promotion == null || current.Promotion.BankPromotionId != previousId ||
                current.Promotion.BankId != PromoBankId || current.DiscountAmount != previousDiscount)
            {
                ResetBankPromotion();
                throw new InvalidOperationException("The bank promotion changed or expired. Re-enter the six-digit reference and apply the card payment again.");
            }
        }

        private Task RestoreBankPromotionAsync(RecalledSaleDto sale)
        {
            if (sale.PromoType != "BANK") return Task.CompletedTask;
            // Held bills have no payment row yet. Ask for the reference again rather
            // than saving a separate BIN on the invoice header.
            _bankPromotion = new BankPromoWithExclusionsDto
            {
                BankId = sale.PromoBankId ?? 0, BankPromotionId = sale.BankPromotionId ?? 0,
                PromoName = "Held bank promotion", DiscountPercentage = 1
            };
            _recalledBankDiscountAmount = sale.BankDiscountAmount;
            _bankPromotionLookupFailed = true;
            BankPromotionMessage = "Enter the six-digit card reference again to validate this held promotion.";
            NotifyBankPromotion();
            CalculateTotals();
            return Task.CompletedTask;
        }
    }
}
