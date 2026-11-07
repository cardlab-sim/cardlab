using CardLab.Domain;
using CardLab.Domain.Authorisation;
using CardLab.Domain.Common;
using CardLab.Domain.Payments;
using CardLab.Domain.Ports;
using CardLab.Infrastructure.Model;
using CardLab.Infrastructure.Sql;
using Microsoft.EntityFrameworkCore;

namespace CardLab.Infrastructure.Payments;

/// <summary>
/// <see cref="IPaymentWriter"/> over the DbContext inside <c>SqlCardStore.WithCardLockAsync</c> (DEC-4), created by
/// <c>SqlCardWriter.Payments</c>. Tables <c>PaymentQuote</c>, <c>Payment</c>, <c>PaymentEvent</c> (design §6).
/// </summary>
/// <remarks>
/// Only <c>PaymentQuote.Status/RefusedAtUtc/RefusalReason/BalanceAtRefusal</c> and a pending <c>Payment</c>'s settlement
/// columns are ever updated; <c>PaymentEvent</c> is append-only; nothing is deleted (CD-5, CD-8). Sums use
/// <c>IX_Transaction_Spend</c> (approved) and <c>IX_Payment_Completed</c>. Never touches <c>Bank*</c> tables.
/// Quote appends and status/settlement changes need a query (the linked account, the existing row), so they are queued
/// and applied in call order by <see cref="ApplyPendingAsync"/> before the owning <c>SqlCardWriter</c> reads or commits;
/// payment and event creation are tracked immediately. Reads first flush everything queued by this unit of work.
/// </remarks>
internal sealed class SqlPaymentWriter : IPaymentWriter
{
    private readonly CardLabDbContext _db;
    private readonly CardId _cardId;
    private readonly Func<CancellationToken, Task> _flushUnitOfWork;
    private readonly List<Func<CancellationToken, Task>> _pending = [];

    /// <summary>Creates a writer for one locked card.</summary>
    /// <param name="db">The context whose transaction holds the card lock.</param>
    /// <param name="cardId">The locked card.</param>
    /// <param name="flushUnitOfWork">Applies everything queued by the card writer (001 changes and this writer's) and saves, so reads see this unit of work's writes.</param>
    public SqlPaymentWriter(CardLabDbContext db, CardId cardId, Func<CancellationToken, Task> flushUnitOfWork)
    {
        _db = db;
        _cardId = cardId;
        _flushUnitOfWork = flushUnitOfWork;
    }

    /// <summary>Applies the queued quote and settlement changes in call order (does not save).</summary>
    /// <param name="ct">Cancellation.</param>
    /// <returns>A task that completes when the changes are tracked.</returns>
    internal async Task ApplyPendingAsync(CancellationToken ct)
    {
        var queued = _pending.ToList();
        _pending.Clear();
        foreach (var apply in queued)
        {
            await apply(ct);
        }
    }

    /// <inheritdoc />
    public async Task<Money> SumApprovedPurchasesAsync(CancellationToken ct = default)
    {
        await _flushUnitOfWork(ct);
        var approved = Outcome.Approved.ToString();
        var sum = await _db.Transactions
            .Where(t => t.CardId == _cardId.Value && t.Outcome == approved)
            .SumAsync(t => (decimal?)t.Amount, ct) ?? 0m;
        return RowMapper.ToMoney(sum);
    }

    /// <inheritdoc />
    public async Task<Money> SumCompletedPaymentsAsync(CancellationToken ct = default)
    {
        await _flushUnitOfWork(ct);
        var sum = await PaymentMapping.SumCompletedAsync(_db, _cardId, ct);
        return RowMapper.ToMoney(sum);
    }

    /// <inheritdoc />
    public async Task<QuoteRecord?> GetQuoteAsync(PaymentReference reference, CancellationToken ct = default)
    {
        await _flushUnitOfWork(ct);
        var quote = await _db.PaymentQuotes.AsNoTracking().SingleOrDefaultAsync(q => q.Reference == reference.Value && q.CardId == _cardId.Value, ct);
        if (quote is null)
        {
            return null;
        }

        var payment = await _db.Payments.AsNoTracking().SingleOrDefaultAsync(p => p.Reference == reference.Value, ct);
        return new QuoteRecord(PaymentMapping.ToQuote(quote), Enum.Parse<QuoteStatus>(quote.Status), payment is null ? null : PaymentMapping.ToSnapshot(payment));
    }

    /// <inheritdoc />
    public async Task<PaymentSnapshot?> GetPendingPaymentAsync(CancellationToken ct = default)
    {
        await _flushUnitOfWork(ct);
        var pending = PaymentState.Pending.ToString();
        var row = await _db.Payments.AsNoTracking().SingleOrDefaultAsync(p => p.CardId == _cardId.Value && p.State == pending, ct);
        return row is null ? null : PaymentMapping.ToSnapshot(row);
    }

    /// <inheritdoc />
    public void AppendQuote(Quote quote) =>
        _pending.Add(async ct =>
        {
            var linkedAccountId = await (
                from card in _db.Cards.AsNoTracking()
                where card.Id == _cardId.Value
                join linked in _db.LinkedAccounts on card.AccountId equals linked.AccountId
                select linked.Id).SingleAsync(ct);
            _db.PaymentQuotes.Add(new PaymentQuoteRow
            {
                Reference = quote.Reference.Value,
                CardId = _cardId.Value,
                LinkedAccountId = linkedAccountId,
                Kind = quote.Kind.ToString(),
                Amount = quote.Amount.Amount,
                Fee = quote.Fee.Amount,
                Total = quote.Total.Amount,
                FromLabel = quote.FromLabel,
                ToLabel = quote.ToLabel,
                WordingVersion = quote.Shown.Version,
                Heading = quote.Shown.Heading,
                RenderedLines = string.Join('\n', quote.Shown.Lines),
                ButtonLabel = quote.Shown.ButtonLabel,
                ActorKind = quote.Actor.Kind.ToString(),
                ActorId = quote.Actor.Id,
                ActorName = quote.Actor.Name,
                AuthorityBasis = quote.AuthorityBasis,
                Origin = quote.Origin.ToString(),
                CreatedAtUtc = quote.CreatedAtUtc.UtcDateTime,
                Status = QuoteStatus.Open.ToString(),
            });
        });

    /// <inheritdoc />
    public void SetQuoteStatus(PaymentReference reference, QuoteStatus status, string? refusalReason, Money? balanceAtRefusal, DateTimeOffset atUtc) =>
        _pending.Add(async ct =>
        {
            var row = await _db.PaymentQuotes.FindAsync([reference.Value], ct);
            if (row is null || row.CardId != _cardId.Value)
            {
                throw new InvalidOperationException("The quote does not exist on the locked card.");
            }

            row.Status = status.ToString();
            if (status == QuoteStatus.Refused)
            {
                row.RefusedAtUtc = atUtc.UtcDateTime;
                row.RefusalReason = refusalReason;
                row.BalanceAtRefusal = balanceAtRefusal?.Amount;
            }
        });

    /// <inheritdoc />
    public PaymentId CreatePayment(NewPayment payment)
    {
        var row = new PaymentRow
        {
            Id = Guid.NewGuid(),
            Reference = payment.Reference.Value,
            CardId = _cardId.Value,
            Kind = payment.Kind.ToString(),
            Amount = payment.Amount.Amount,
            Fee = payment.Fee.Amount,
            State = PaymentState.Pending.ToString(),
            CreatedAtUtc = payment.CreatedAtUtc.UtcDateTime,
        };
        _db.Payments.Add(row);
        return new PaymentId(row.Id);
    }

    /// <inheritdoc />
    public void SettlePayment(PaymentId id, Settlement settlement) =>
        _pending.Add(async ct =>
        {
            var row = await _db.Payments.FindAsync([id.Value], ct);
            if (row is null || row.CardId != _cardId.Value)
            {
                throw new InvalidOperationException("The payment does not exist on the locked card.");
            }

            row.State = settlement.State.ToString();
            row.FailureReason = settlement.Reason?.ToString();
            row.SettledAtUtc = settlement.SettledAtUtc.UtcDateTime;
            row.EffectiveAtUtc = settlement.EffectiveAtUtc.UtcDateTime;
            row.BankDebitedAtUtc = settlement.BankDebitedAtUtc?.UtcDateTime;
            row.BalanceBefore = settlement.BalanceBefore.Amount;
            row.BalanceAfter = settlement.BalanceAfter.Amount;
            row.Simulated = settlement.Simulated;
        });

    /// <inheritdoc />
    public void AppendEvent(PaymentEvent paymentEvent) =>
        _db.PaymentEvents.Add(new PaymentEventRow
        {
            Reference = paymentEvent.Reference.Value,
            PaymentId = paymentEvent.Payment?.Value,
            AtUtc = paymentEvent.AtUtc.UtcDateTime,
            Kind = paymentEvent.Kind.ToString(),
            FromState = paymentEvent.From?.ToString(),
            ToState = paymentEvent.To?.ToString(),
            Detail = paymentEvent.Detail,
            Simulated = paymentEvent.Simulated,
        });
}

/// <summary>Row to domain mapping shared by the payment writer and the card store's payment reads.</summary>
internal static class PaymentMapping
{
    /// <summary>Sum of a card's payments in state Completed (0 when none).</summary>
    /// <param name="db">The context.</param>
    /// <param name="cardId">The card.</param>
    /// <param name="ct">Cancellation.</param>
    /// <returns>The sum.</returns>
    public static async Task<decimal> SumCompletedAsync(CardLabDbContext db, CardId cardId, CancellationToken ct)
    {
        var completed = PaymentState.Completed.ToString();
        return await db.Payments
            .Where(p => p.CardId == cardId.Value && p.State == completed)
            .SumAsync(p => (decimal?)p.Amount, ct) ?? 0m;
    }

    /// <summary>Maps a quote row to the shown-and-accepted quote.</summary>
    /// <param name="row">The row.</param>
    /// <returns>The quote.</returns>
    public static Quote ToQuote(PaymentQuoteRow row) => new(
        new PaymentReference(row.Reference),
        new CardId(row.CardId),
        Enum.Parse<PaymentKind>(row.Kind),
        RowMapper.ToMoney(row.Amount),
        RowMapper.ToMoney(row.Fee),
        RowMapper.ToMoney(row.Total),
        row.FromLabel,
        row.ToLabel,
        new PaymentActor(Enum.Parse<ActorKind>(row.ActorKind), row.ActorId, row.ActorName),
        row.AuthorityBasis,
        RowMapper.ToInstant(row.CreatedAtUtc),
        new RenderedWording(row.WordingVersion, row.Heading, row.RenderedLines.Split('\n'), row.ButtonLabel),
        Enum.Parse<PaymentOrigin>(row.Origin));

    /// <summary>Maps a payment row to its snapshot (settlement null while Pending).</summary>
    /// <param name="row">The row.</param>
    /// <returns>The snapshot.</returns>
    public static PaymentSnapshot ToSnapshot(PaymentRow row)
    {
        var state = Enum.Parse<PaymentState>(row.State);
        Settlement? settlement = state == PaymentState.Pending
            ? null
            : new Settlement(
                state,
                row.FailureReason is null ? null : Enum.Parse<FailureReason>(row.FailureReason),
                RowMapper.ToInstant(row.SettledAtUtc!.Value),
                RowMapper.ToInstant(row.EffectiveAtUtc!.Value),
                row.BankDebitedAtUtc is null ? null : RowMapper.ToInstant(row.BankDebitedAtUtc.Value),
                RowMapper.ToMoney(row.BalanceBefore!.Value),
                RowMapper.ToMoney(row.BalanceAfter!.Value),
                row.Simulated);
        return new PaymentSnapshot(new PaymentId(row.Id), new PaymentReference(row.Reference), state, RowMapper.ToMoney(row.Amount), RowMapper.ToInstant(row.CreatedAtUtc), settlement);
    }

    /// <summary>Maps an event row.</summary>
    /// <param name="row">The row.</param>
    /// <returns>The event.</returns>
    public static PaymentEvent ToEvent(PaymentEventRow row) => new(
        new PaymentReference(row.Reference),
        row.PaymentId is null ? null : new PaymentId(row.PaymentId.Value),
        RowMapper.ToInstant(row.AtUtc),
        Enum.Parse<PaymentEventKind>(row.Kind),
        row.FromState is null ? null : Enum.Parse<PaymentState>(row.FromState),
        row.ToState is null ? null : Enum.Parse<PaymentState>(row.ToState),
        row.Detail,
        row.Simulated);
}
