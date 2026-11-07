using CardLab.Domain.Common;
using CardLab.Domain.Controls;
using CardLab.Domain.Payments;
using CardLab.Domain.Ports;

namespace CardLab.Domain.Application;

/// <summary>The cardholder view aggregate read (R-1..R-3, R-13, R-16). Pure read: no writes, no status check (DEC-13).</summary>
public sealed class CardholderViewService
{
    private const int ActivityRows = 10;

    private readonly ICardStore _store;
    private readonly ICurrentAccountBank _bank;

    /// <summary>Creates the service.</summary>
    /// <param name="store">Card store (card, balance, pending payment, activity, linked account).</param>
    /// <param name="bank">Bank (available balance and last 10 entries).</param>
    public CardholderViewService(ICardStore store, ICurrentAccountBank bank)
    {
        _store = store;
        _bank = bank;
    }

    /// <summary>Builds the view of one card.</summary>
    /// <remarks>Availability message: W-7 / W-8 / W-9 (W-9 names the pending payment); pending receipt with W-5;
    /// card activity <c>ListActivityAsync(id, 10)</c>; account activity <c>ListRecentEntriesAsync(ref, 10)</c>.</remarks>
    /// <param name="cardId">Card.</param>
    /// <param name="ct">Cancellation.</param>
    /// <returns>The view, or CardNotFound.</returns>
    public async Task<Result<CardholderView, PaymentError>> GetAsync(CardId cardId, CancellationToken ct = default)
    {
        var card = await _store.GetCardAsync(cardId, ct);
        var balance = await _store.GetBalanceAsync(cardId, ct);
        if (card is null || balance is null)
        {
            return Result<CardholderView, PaymentError>.Fail(new PaymentError.CardNotFound());
        }

        var account = await _store.GetLinkedAccountAsync(ct) ?? throw new InvalidOperationException("No linked current account is configured.");
        var records = await _store.ListPaymentRecordsAsync(cardId, ct);
        var pending = records.FirstOrDefault(r => r.Payment is { State: PaymentState.Pending });
        var availability = BalanceRules.Availability(balance.Owed, pending is not null);
        var message = availability switch
        {
            PayAvailability.NothingToPay => WordingV1.NothingToPay(),
            PayAvailability.AboveLimit => WordingV1.AboveLimit(balance.Owed),
            PayAvailability.PaymentPending => WordingV1.PendingExists(pending!.Payment!.Amount, pending.Payment.Reference),
            _ => null,
        };
        var pendingReceipt = pending is null ? null : PaymentService.BuildReceipt(pending.Quote, pending.Payment!, balance.Owed, repeated: false);
        var snapshot = await _bank.GetAccountAsync(account.BankRef, ct);
        var accountActivity = await _bank.ListRecentEntriesAsync(account.BankRef, ActivityRows, ct);
        var cardActivity = await _store.ListActivityAsync(cardId, ActivityRows, ct);
        return Result<CardholderView, PaymentError>.Ok(new CardholderView(
            new CardRef(cardId, card.LastFour),
            card.Name,
            card.CardholderName,
            card.Status,
            balance.Owed,
            availability,
            message,
            pendingReceipt,
            account,
            snapshot.AvailableBalance,
            cardActivity,
            accountActivity));
    }
}
