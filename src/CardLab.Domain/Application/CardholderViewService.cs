using CardLab.Domain.Common;
using CardLab.Domain.Ports;

namespace CardLab.Domain.Application;

/// <summary>The cardholder view aggregate read (R-1..R-3, R-13, R-16). Pure read: no writes, no status check (DEC-13).</summary>
/// <remarks>Implemented in PR-04.</remarks>
public sealed class CardholderViewService
{
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
    public Task<Result<CardholderView, PaymentError>> GetAsync(CardId cardId, CancellationToken ct = default) =>
        throw new NotImplementedException("PR-04");
}
