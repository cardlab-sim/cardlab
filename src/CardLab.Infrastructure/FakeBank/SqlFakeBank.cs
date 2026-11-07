using System.Data;
using CardLab.Domain;
using CardLab.Domain.Payments;
using CardLab.Domain.Ports;
using CardLab.Infrastructure.Model;
using CardLab.Infrastructure.Sql;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CardLab.Infrastructure.FakeBank;

/// <summary>
/// The persisted fake current account (R-15, DEC-6, ADR-007): tables <c>BankAccount</c>, <c>BankDebitRequest</c>,
/// <c>BankEntry</c>, <c>BankSteering</c> in the CardLab database, never joined with card tables (DEC-2). Shipped to every
/// environment. Must pass <c>CurrentAccountBankContractTests</c> (T-I-15).
/// </summary>
/// <remarks>
/// Debit (design §8 flow 6): own transaction with <c>sp_getapplock</c> on resource <c>bank:{accountId}</c> (5 s); a known
/// reference returns the recorded outcome (idempotent, steering untouched); else read-and-clear
/// <c>BankSteering.NextDebitOutcome</c> (Simulated = a value was set); refuse InsufficientFunds when steered or when
/// Amount &gt; AvailableBalance (the refusal is recorded so a status check can answer Refused); Complete and
/// TimeoutDebitApplied insert <c>BankDebitRequest</c> + <c>BankEntry</c> and decrement the balance, commit;
/// TimeoutNotReceived commits the steering clear and a simulated <c>NotReceived</c> request row (no entry, no balance change,
/// BUG-004); both timeout modes then <c>await Task.Delay(Timeout.Infinite, ct)</c>
/// (a timeout mode on a debit the balance cannot cover is refused instead, there is nothing to apply). Status:
/// read-and-clear <c>NextStatusCheck</c>; DoesNotAnswer waits on <c>ct</c>; else Completed(DebitedAtUtc) / Refused(reason)
/// from the request row (NotReceived is simulated when recorded under steering), NotReceived(not simulated) when none. Shares the
/// request's scoped <see cref="CardLabDbContext"/>.
/// </remarks>
public sealed class SqlFakeBank : ICurrentAccountBank, IFakeBankSteering
{
    private const int SteeringRowId = 1;

    private readonly CardLabDbContext _db;
    private readonly IClock _clock;

    /// <summary>Creates the adapter.</summary>
    /// <param name="db">Scoped context.</param>
    /// <param name="clock">Clock for receive, debit and posting times.</param>
    public SqlFakeBank(CardLabDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    /// <inheritdoc />
    public async Task<AccountSnapshot> GetAccountAsync(BankAccountRef account, CancellationToken ct = default)
    {
        var row = await _db.BankAccounts.AsNoTracking().SingleOrDefaultAsync(a => a.Id == account.Value, ct)
            ?? throw new InvalidOperationException("The bank has no such account.");
        return new AccountSnapshot(account, row.LastFour, RowMapper.ToMoney(row.AvailableBalance));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<BankEntry>> ListRecentEntriesAsync(BankAccountRef account, int take, CancellationToken ct = default)
    {
        var rows = await _db.BankEntries.AsNoTracking()
            .Where(e => e.AccountId == account.Value)
            .OrderByDescending(e => e.PostedAtUtc)
            .Take(take)
            .ToListAsync(ct);
        return rows.Select(e => new BankEntry(new PaymentReference(e.Reference), RowMapper.ToMoney(e.Amount), RowMapper.ToInstant(e.PostedAtUtc), e.Narrative)).ToList();
    }

    /// <inheritdoc />
    public async Task<BankOutcome> RequestDebitAsync(DebitRequest request, CancellationToken ct = default)
    {
        var (outcome, hang) = await DebitInTransactionAsync(request, ct);
        if (hang)
        {
            await Task.Delay(Timeout.Infinite, ct);
        }

        return outcome!;
    }

    /// <inheritdoc />
    public async Task<BankOutcome> GetDebitStatusAsync(BankAccountRef account, PaymentReference reference, CancellationToken ct = default)
    {
        BankOutcome? outcome;
        await using (var tx = await BeginLockedAsync(account, ct))
        {
            try
            {
                var steering = await ReadAndClearAsync(
                    row =>
                    {
                        var taken = row.NextStatusCheck;
                        row.NextStatusCheck = null;
                        return taken;
                    },
                    ct);
                if (steering is not null && Enum.Parse<NextStatusCheck>(steering) == NextStatusCheck.DoesNotAnswer)
                {
                    outcome = null;
                }
                else
                {
                    var known = await _db.BankDebitRequests.AsNoTracking().SingleOrDefaultAsync(r => r.Reference == reference.Value && r.AccountId == account.Value, ct);
                    outcome = known is null ? new BankOutcome.NotReceived(false) : ToOutcome(known);
                }

                await tx.CommitAsync(ct);
            }
            finally
            {
                _db.ChangeTracker.Clear();
            }
        }

        if (outcome is null)
        {
            await Task.Delay(Timeout.Infinite, ct);
        }

        return outcome!;
    }

    /// <inheritdoc />
    public async Task<Steering> GetAsync(CancellationToken ct = default)
    {
        var row = await _db.BankSteerings.AsNoTracking().SingleOrDefaultAsync(s => s.Id == SteeringRowId, ct);
        return new Steering(
            row?.NextDebitOutcome is null ? null : Enum.Parse<NextDebitOutcome>(row.NextDebitOutcome),
            row?.NextStatusCheck is null ? null : Enum.Parse<NextStatusCheck>(row.NextStatusCheck));
    }

    /// <inheritdoc />
    public async Task SetAsync(Steering steering, CancellationToken ct = default)
    {
        try
        {
            var row = await _db.BankSteerings.SingleOrDefaultAsync(s => s.Id == SteeringRowId, ct);
            if (row is null)
            {
                row = new BankSteeringRow { Id = SteeringRowId };
                _db.BankSteerings.Add(row);
            }

            row.NextDebitOutcome = steering.NextDebit?.ToString();
            row.NextStatusCheck = steering.NextStatusCheck?.ToString();
            await _db.SaveChangesAsync(ct);
        }
        finally
        {
            _db.ChangeTracker.Clear();
        }
    }

    private static BankOutcome ToOutcome(BankDebitRequestRow row) => row.Outcome switch
    {
        "Completed" => new BankOutcome.Completed(row.DebitedAtUtc is null ? null : RowMapper.ToInstant(row.DebitedAtUtc.Value), row.Simulated),
        "RefusedInsufficientFunds" => new BankOutcome.Refused(FailureReason.InsufficientFunds, row.Simulated),
        "RefusedAccountUnavailable" => new BankOutcome.Refused(FailureReason.AccountUnavailable, row.Simulated),
        "NotReceived" => new BankOutcome.NotReceived(row.Simulated),
        _ => throw new InvalidOperationException("The bank recorded an unknown outcome."),
    };

    // One debit in its own transaction under the account lock. Returns the outcome and whether the caller must now hang.
    private async Task<(BankOutcome? Outcome, bool Hang)> DebitInTransactionAsync(DebitRequest request, CancellationToken ct)
    {
        await using var tx = await BeginLockedAsync(request.Account, ct);
        try
        {
            var known = await _db.BankDebitRequests.AsNoTracking().SingleOrDefaultAsync(r => r.Reference == request.Reference.Value, ct);
            if (known is not null)
            {
                await tx.CommitAsync(ct);
                return (ToOutcome(known), false);
            }

            var steeredName = await ReadAndClearAsync(
                row =>
                {
                    var taken = row.NextDebitOutcome;
                    row.NextDebitOutcome = null;
                    return taken;
                },
                ct);
            var steered = steeredName is null ? (NextDebitOutcome?)null : Enum.Parse<NextDebitOutcome>(steeredName);
            var now = _clock.UtcNow.UtcDateTime;
            if (steered == NextDebitOutcome.TimeoutNotReceived)
            {
                // Recorded as a simulated not-received request (BUG-004): no entry, no balance change, so a status check
                // answers NotReceived(simulated) while a reference the bank never saw answers NotReceived(not simulated).
                _db.BankDebitRequests.Add(new BankDebitRequestRow
                {
                    Reference = request.Reference.Value,
                    AccountId = request.Account.Value,
                    Amount = request.Amount.Amount,
                    Outcome = "NotReceived",
                    ReceivedAtUtc = now,
                    DebitedAtUtc = null,
                    Simulated = true,
                });
                await _db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return (null, true);
            }

            var account = await _db.BankAccounts.SingleOrDefaultAsync(a => a.Id == request.Account.Value, ct)
                ?? throw new InvalidOperationException("The bank has no such account.");
            var refusal = steered switch
            {
                NextDebitOutcome.RefuseInsufficientFunds => "RefusedInsufficientFunds",
                NextDebitOutcome.RefuseAccountUnavailable => "RefusedAccountUnavailable",
                _ when request.Amount.Amount > account.AvailableBalance => "RefusedInsufficientFunds",
                _ => null,
            };
            var debit = new BankDebitRequestRow
            {
                Reference = request.Reference.Value,
                AccountId = account.Id,
                Amount = request.Amount.Amount,
                Outcome = refusal ?? "Completed",
                ReceivedAtUtc = now,
                DebitedAtUtc = refusal is null ? now : null,
                Simulated = steered is not null,
            };
            _db.BankDebitRequests.Add(debit);
            if (refusal is null)
            {
                account.AvailableBalance -= request.Amount.Amount;
                _db.BankEntries.Add(new BankEntryRow
                {
                    Id = Guid.NewGuid(),
                    AccountId = account.Id,
                    Reference = request.Reference.Value,
                    Amount = request.Amount.Amount,
                    Narrative = request.Narrative,
                    PostedAtUtc = now,
                });
            }

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return (ToOutcome(debit), steered == NextDebitOutcome.TimeoutDebitApplied && refusal is null);
        }
        finally
        {
            _db.ChangeTracker.Clear();
        }
    }

    // Reads the steering row, lets the caller take (and clear) one of its fields, and saves the clear.
    private async Task<string?> ReadAndClearAsync(Func<BankSteeringRow, string?> take, CancellationToken ct)
    {
        // UPDLOCK: the read and the clear are one atomic step across accounts (the account applock is per account only).
        var row = await _db.BankSteerings
            .FromSqlRaw("SELECT * FROM [BankSteering] WITH (UPDLOCK, ROWLOCK) WHERE [Id] = 1")
            .SingleOrDefaultAsync(ct);
        if (row is null)
        {
            return null;
        }

        var taken = take(row);
        await _db.SaveChangesAsync(ct);
        return taken;
    }

    private async Task<IDbContextTransaction> BeginLockedAsync(BankAccountRef account, CancellationToken ct)
    {
        var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var result = new SqlParameter("@r", SqlDbType.Int) { Direction = ParameterDirection.Output };
            var resource = new SqlParameter("@resource", SqlDbType.NVarChar, 255) { Value = $"bank:{account.Value}" };
            await _db.Database.ExecuteSqlRawAsync(
                "EXEC @r = sp_getapplock @Resource = @resource, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 5000",
                [result, resource],
                ct);
            if ((int)result.Value < 0)
            {
                throw new InvalidOperationException("The bank account was busy for more than 5 seconds.");
            }

            return tx;
        }
        catch
        {
            await tx.DisposeAsync();
            throw;
        }
    }
}
