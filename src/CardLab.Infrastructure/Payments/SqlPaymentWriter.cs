using CardLab.Domain;
using CardLab.Domain.Common;
using CardLab.Domain.Payments;
using CardLab.Domain.Ports;
using CardLab.Infrastructure.Model;

namespace CardLab.Infrastructure.Payments;

/// <summary>
/// <see cref="IPaymentWriter"/> over the DbContext inside <c>SqlCardStore.WithCardLockAsync</c> (DEC-4), created by
/// <c>SqlCardWriter.Payments</c>. Tables <c>PaymentQuote</c>, <c>Payment</c>, <c>PaymentEvent</c> (design §6).
/// </summary>
/// <remarks>
/// Only <c>PaymentQuote.Status/RefusedAtUtc/RefusalReason/BalanceAtRefusal</c> and a pending <c>Payment</c>'s settlement
/// columns are ever updated; <c>PaymentEvent</c> is append-only; nothing is deleted (CD-5, CD-8). Sums use
/// <c>IX_Transaction_Spend</c> (approved) and <c>IX_Payment_Completed</c>. Never touches <c>Bank*</c> tables.
/// Implemented in PR-05.
/// </remarks>
internal sealed class SqlPaymentWriter : IPaymentWriter
{
    private readonly CardLabDbContext _db;
    private readonly CardId _cardId;

    /// <summary>Creates a writer for one locked card.</summary>
    /// <param name="db">The context whose transaction holds the card lock.</param>
    /// <param name="cardId">The locked card.</param>
    public SqlPaymentWriter(CardLabDbContext db, CardId cardId)
    {
        _db = db;
        _cardId = cardId;
    }

    /// <inheritdoc />
    public Task<Money> SumApprovedPurchasesAsync(CancellationToken ct = default) => throw new NotImplementedException("PR-05");

    /// <inheritdoc />
    public Task<Money> SumCompletedPaymentsAsync(CancellationToken ct = default) => throw new NotImplementedException("PR-05");

    /// <inheritdoc />
    public Task<QuoteRecord?> GetQuoteAsync(PaymentReference reference, CancellationToken ct = default) => throw new NotImplementedException("PR-05");

    /// <inheritdoc />
    public Task<PaymentSnapshot?> GetPendingPaymentAsync(CancellationToken ct = default) => throw new NotImplementedException("PR-05");

    /// <inheritdoc />
    public void AppendQuote(Quote quote) => throw new NotImplementedException("PR-05");

    /// <inheritdoc />
    public void SetQuoteStatus(PaymentReference reference, QuoteStatus status, string? refusalReason, Money? balanceAtRefusal, DateTimeOffset atUtc) =>
        throw new NotImplementedException("PR-05");

    /// <inheritdoc />
    public PaymentId CreatePayment(NewPayment payment) => throw new NotImplementedException("PR-05");

    /// <inheritdoc />
    public void SettlePayment(PaymentId id, Settlement settlement) => throw new NotImplementedException("PR-05");

    /// <inheritdoc />
    public void AppendEvent(PaymentEvent paymentEvent) => throw new NotImplementedException("PR-05");
}
