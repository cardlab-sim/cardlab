using System.Globalization;
using CardLab.Domain.Controls;
using CardLab.Infrastructure.Model;
using CardLab.Infrastructure.Options;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CardLab.Infrastructure.Sql;

/// <summary>Database reachability for <c>GET /api/health</c> (R-14, design §12).</summary>
/// <remarks>Implemented in PR-09.</remarks>
public sealed class DatabaseProbe
{
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(5);

    private readonly CardLabDbContext _db;

    /// <summary>Creates the probe.</summary>
    /// <param name="db">Scoped context.</param>
    public DatabaseProbe(CardLabDbContext db) => _db = db;

    /// <summary>Runs <c>SELECT 1</c> with a 5 s timeout.</summary>
    /// <param name="ct">Cancellation.</param>
    /// <returns>True when the query succeeded; false on any connection or timeout error (never throws for those).</returns>
    public async Task<bool> CanConnectAsync(CancellationToken ct = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(ProbeTimeout);
        try
        {
            _db.Database.SetCommandTimeout(ProbeTimeout);
            await _db.Database.ExecuteSqlRawAsync("SELECT 1", timeout.Token);
            return true;
        }
        catch (Exception e) when (e is SqlException or InvalidOperationException || (e is OperationCanceledException && !ct.IsCancellationRequested))
        {
            return false;
        }
    }
}

/// <summary>Test-only reset behind <c>POST /api/test/reset</c> (DEC-16).</summary>
/// <remarks>Extended in 002 PR-05 for payments and the fake bank.</remarks>
public sealed class TestDataReset
{
    private readonly CardLabDbContext _db;
    private readonly SeedLinkedAccountOptions _linkedAccount;

    /// <summary>Creates the reset.</summary>
    /// <param name="db">Scoped context.</param>
    /// <param name="seed">Seed options (the fake bank account's opening balance).</param>
    public TestDataReset(CardLabDbContext db, IOptions<SeedOptions> seed)
    {
        _db = db;
        _linkedAccount = seed.Value.LinkedAccount;
    }

    /// <summary>
    /// Deletes every transaction, audit event, limit, payment, quote, payment event, bank debit request and bank entry,
    /// sets every card to Active, clears the fake bank's steering and returns its account to the opening balance
    /// (the linked account stays), in one transaction (DEC-16, AC-21).
    /// </summary>
    /// <param name="ct">Cancellation.</param>
    /// <returns>A task that completes when the reset is committed.</returns>
    public async Task ResetAsync(CancellationToken ct = default)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        await _db.PaymentEvents.ExecuteDeleteAsync(ct);
        await _db.Payments.ExecuteDeleteAsync(ct);
        await _db.PaymentQuotes.ExecuteDeleteAsync(ct);
        await _db.BankEntries.ExecuteDeleteAsync(ct);
        await _db.BankDebitRequests.ExecuteDeleteAsync(ct);
        await _db.BankSteerings.ExecuteUpdateAsync(s => s.SetProperty(b => b.NextDebitOutcome, (string?)null).SetProperty(b => b.NextStatusCheck, (string?)null), ct);
        var opening = decimal.Parse(_linkedAccount.OpeningBalance, CultureInfo.InvariantCulture);
        var bankAccount = _linkedAccount.BankAccountRef;
        await _db.BankAccounts.Where(b => b.Id == bankAccount).ExecuteUpdateAsync(s => s.SetProperty(b => b.AvailableBalance, opening), ct);
        await _db.Transactions.ExecuteDeleteAsync(ct);
        await _db.AuditEvents.ExecuteDeleteAsync(ct);
        await _db.SpendLimits.ExecuteDeleteAsync(ct);
        var active = nameof(CardStatus.Active);
        await _db.Cards.ExecuteUpdateAsync(s => s.SetProperty(c => c.Status, active), ct);
        await transaction.CommitAsync(ct);
    }
}
