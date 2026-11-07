using CardLab.Domain.Application;
using CardLab.Domain.Common;
using CardLab.Domain.Payments;
using CardLab.Domain.Ports;

namespace CardLab.Api.Contracts;

/// <summary>
/// Domain → wire mapping shared by the cardholder (PR-06) and admin (PR-07) endpoints (design §7). Complete in PR-00:
/// pure field mapping, no rules. Wire names come from <see cref="PaymentWireNames"/> / <see cref="WireNames"/>.
/// </summary>
public static class PaymentDtoMapper
{
    /// <summary>Maps a receipt.</summary>
    /// <param name="r">Receipt.</param>
    /// <returns>The DTO.</returns>
    public static ReceiptDto ToDto(Receipt r) => new(
        r.Reference.Value,
        PaymentWireNames.ToWire(r.State),
        r.Amount.ToString(),
        r.Fee.ToString(),
        r.Time,
        r.FromLabel,
        r.ToLabel,
        r.Reason is { } reason ? PaymentWireNames.ToWire(reason) : null,
        r.Message,
        r.BalanceOwed.ToString(),
        r.Repeated,
        PaymentWireNames.ToWire(r.Origin));

    /// <summary>Maps an activity row.</summary>
    /// <param name="a">Row.</param>
    /// <returns>The DTO.</returns>
    public static ActivityDto ToDto(ActivityEntry a) => new(
        a.At,
        PaymentWireNames.ToWire(a.Kind),
        a.Amount.ToString(),
        a.Description,
        a.PurchaseOutcome is { } outcome ? WireNames.ToWire(outcome) : null,
        a.PaymentState is { } state ? PaymentWireNames.ToWire(state) : null,
        a.Reference?.Value,
        a.Origin is { } origin ? PaymentWireNames.ToWire(origin) : null,
        a.PurchaseCategory is { } category ? WireNames.ToWire(category) : null,
        a.PurchaseReason is { } reason ? WireNames.ToWire(reason) : null);

    /// <summary>Maps a ledger entry.</summary>
    /// <param name="e">Entry.</param>
    /// <returns>The DTO.</returns>
    public static AccountEntryDto ToDto(BankEntry e) => new(e.Reference.Value, e.Amount.ToString(), e.PostedAtUtc, e.Narrative);

    /// <summary>Maps rendered wording.</summary>
    /// <param name="w">Wording.</param>
    /// <returns>The DTO.</returns>
    public static WordingDto ToDto(RenderedWording w) => new(w.Version, w.Heading, w.Lines.ToList(), w.ButtonLabel);

    /// <summary>Maps a quote (201 body).</summary>
    /// <param name="q">Quote.</param>
    /// <returns>The DTO.</returns>
    public static QuoteDto ToDto(Quote q) => new(
        q.Reference.Value,
        q.Amount.ToString(),
        q.Fee.ToString(),
        q.Total.ToString(),
        q.FromLabel,
        q.ToLabel,
        ToDto(q.Shown),
        q.CreatedAtUtc,
        PaymentWireNames.ToWire(q.Origin));

    /// <summary>Maps the cardholder view.</summary>
    /// <param name="v">View.</param>
    /// <returns>The DTO.</returns>
    public static CardholderViewDto ToDto(CardholderView v) => new(
        new CardholderCardDto(v.Card.Id.Value, v.CardName, v.Card.LastFour, v.CardholderName, WireNames.ToWire(v.Status)),
        v.BalanceOwed.ToString(),
        PaymentWireNames.ToWire(v.Availability),
        v.AvailabilityMessage,
        v.PendingPayment is null ? null : ToDto(v.PendingPayment),
        new LinkedAccountDto(v.Account.DisplayName, v.Account.LastFour, v.AccountAvailable.ToString(), v.Account.AuthorityBasis),
        v.CardActivity.Select(ToDto).ToList(),
        v.AccountActivity.Select(ToDto).ToList());

    /// <summary>Maps a record to a list item.</summary>
    /// <param name="r">Record.</param>
    /// <returns>The DTO.</returns>
    public static PaymentListItemDto ToListItem(PaymentRecord r) => new(
        r.Quote.Reference.Value,
        PaymentWireNames.ToWire(r.Status),
        r.Payment is null ? null : PaymentWireNames.ToWire(r.Payment.State),
        r.Quote.Amount.ToString(),
        r.Quote.CreatedAtUtc,
        r.Payment?.Settlement?.SettledAtUtc,
        r.Payment?.Settlement?.Reason is { } reason ? PaymentWireNames.ToWire(reason) : null,
        PaymentWireNames.ToWire(r.Quote.Origin));

    /// <summary>Maps a full record (AC-20). Acceptance time: payment creation, else the first ConfirmRefused event, else null.</summary>
    /// <param name="r">Record.</param>
    /// <returns>The DTO.</returns>
    public static PaymentRecordDto ToDto(PaymentRecord r)
    {
        var q = r.Quote;
        var acceptedAt = r.Payment?.CreatedAtUtc
            ?? r.Events.Where(e => e.Kind == PaymentEventKind.ConfirmRefused).Select(e => (DateTimeOffset?)e.AtUtc).FirstOrDefault();
        var payment = r.Payment is null
            ? null
            : new RecordPaymentDto(
                PaymentWireNames.ToWire(r.Payment.State),
                r.Payment.Amount.ToString(),
                q.Fee.ToString(),
                r.Payment.CreatedAtUtc,
                r.Payment.Settlement?.SettledAtUtc,
                r.Payment.Settlement?.EffectiveAtUtc,
                r.Payment.Settlement?.BankDebitedAtUtc,
                r.Payment.Settlement?.Reason is { } reason ? PaymentWireNames.ToWire(reason) : null,
                r.Payment.Settlement?.BalanceBefore.ToString(),
                r.Payment.Settlement?.BalanceAfter.ToString(),
                r.Payment.Settlement?.Simulated ?? false);
        return new PaymentRecordDto(
            new RecordQuoteDto(q.Reference.Value, PaymentWireNames.ToWire(q.Kind), q.Amount.ToString(), q.Fee.ToString(), q.Total.ToString(), q.FromLabel, q.ToLabel, ToDto(q.Shown), q.CreatedAtUtc),
            PaymentWireNames.ToWire(r.Status),
            r.RefusalReason,
            r.BalanceAtRefusal?.ToString(),
            new AcceptanceDto(acceptedAt, new ActorDto(PaymentWireNames.ToWire(q.Actor.Kind), q.Actor.Name), q.AuthorityBasis, PaymentWireNames.ToWire(q.Origin)),
            payment,
            r.Events.Select(e => new PaymentEventDto(
                e.AtUtc,
                PaymentWireNames.ToWire(e.Kind),
                e.From is { } from ? PaymentWireNames.ToWire(from) : null,
                e.To is { } to ? PaymentWireNames.ToWire(to) : null,
                e.Detail,
                e.Simulated)).ToList());
    }
}
