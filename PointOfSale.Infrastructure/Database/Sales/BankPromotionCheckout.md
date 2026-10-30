# Bank promotion checkout

This application uses `SalesViewModel` for payments and `SalesLine` for cart items.
`SalesViewModel.BankPromotion.cs` integrates the payment feature into that existing
ViewModel; `Core/Services/CheckoutService.cs` performs the promotion lookup and calculation.

## Database rollout

Apply these scripts to the intended POS database in order before starting the updated app:

1. `BankPromotionSchema.sql` (existing promotion/BIN/exclusion schema).
2. `BankPromotionCheckoutSchema.sql` (invoice promotion metadata).
3. `uspFinalizeSale.BankPromotion.sql` (complete ALTER of the supplied procedure).

The scripts have not been applied to the configured application database. The finalize
script is based on the procedure supplied with this request; compare it with any newer
deployed procedure before replacing it.

Map `BANK_PROMO_EXPENSE` in `[System].[AccountMapping]` to the intended bank promotion
expense ledger account. `CASH_DISCOUNT_EXPENSE` is optional and falls back to the existing
active discount account `4105`. Account IDs are installation specific; none are seeded.
Existing line discounts debit the ordinary discount account; the bank bill discount
debits the bank promotion account. A missing bank mapping rolls back settlement.

`PromoType` and `PromoBankId` are reused. The migration also adds `BankPromotionId`
and `BankDiscountAmount`, so held bills retain their restrictions and SQL
can independently validate the selected offer and eligible amount. The six-digit lookup
value is sent as the existing payment `ReferenceNumber`, not a separate BIN parameter or
invoice header column. The existing hold/recall procedures and payment TVP signatures are unchanged;
the repository saves the held header, lines and promotion metadata in one transaction.

## Cashier flow

Select CARD and enter six digits in the existing Reference No / Auth Code field.
There is no separate BIN field or promotion button. The reference triggers the active bank BIN lookup,
promotion schedule and required card type. Existing line discounts and excluded variants
remain untouched and do not contribute to the bank eligible subtotal. The bank discount
is applied once as a bill discount. Automatic item promotions may still apply to their
own items; automatic bill discounts, manual bill discounts, coupon discounts and loyalty
redemption cannot stack with the bank discount.

The cashier must remove existing payments and conflicting bill discounts before applying
a matching bank promotion. After a bank promotion is applied, add exactly one CARD payment equal to the
invoice net amount, using the existing terminal and six-digit reference.
Editing or shortening the reference clears the associated promotion/payment and invalidates older asynchronous
responses. Cart changes recalculate eligibility and block saving stale payment amounts.
The summary displays the bank promotion name, rate and discount amount.
The automatic input clear after ADD preserves the saved payment's reference and promotion.

Held bank invoices restore their promotion information on recall and require the cashier
to re-enter the reference to validate the offer; a held invoice has no payment reference yet.
An expired or changed offer must be cleared or reevaluated before payment. Canceling or completing an invoice
clears bank promotion state along with the customer selection.

The BIN identifies the offer from the cashier's input. This POS does not integrate with a
card processor; validating that the physical card charged matches the entered BIN remains
part of the external terminal payment process. No full card number is requested or stored.

## SQL safeguards and related corrections

Finalization reads the saved promotion metadata under an update lock and rejects bank
invoices with multiple payments, a non-card method, mismatched bank/BIN, expired offer,
incorrect discount, incomplete payment, or an existing posted payment. The split-payment
failure uses the exact message requested. Validation precedes payment, stock and ledger
writes. Database exceptions roll back the transaction.

The supplied procedure calculated the payment balance from subtotal without deducting
the bill discount. It now uses subtotal minus bill discount; taxes and service charge
remain inclusive, consistent with the existing ViewModel/hold contract. Ordinary item
discounts are also posted explicitly so mixed bank/previously discounted carts balance.
Bank journals reject variances over Rs. 0.02 instead of hiding them in the supplied
procedure's unlimited auto-balancing entry. The legacy behavior remains for other bills.

## Verification

Run `tests/Verify-BankPromotionCheckout.ps1` from the repository root. It builds the solution
and tests eligibility, midpoint rounding, ViewModel payment guards, conflicting discounts,
customer/promotion reset, and out-of-order BIN responses. SQL checks create and remove a
disposable LocalDB database, run the complete finalize procedure with representative stock
and ledger fixtures, verify rollback on rejected payments, and verify separate discount
accounts, duplicate rejection, fractional quantities and ordinary split payments.
Repository checks cover metadata hold/recall, atomic rollback, promotion removal, and an
end-to-end six-digit `ReferenceNumber` payment round trip including a leading zero.

The fixture uses contract stubs for the unchanged hold/recall procedures. It does not
replace a staging acceptance test with the deployed stock, tax and accounting setup.
