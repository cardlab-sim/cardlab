using CardLab.Api.Contracts;
using CardLab.Integration.Tests.Harness;

namespace CardLab.Integration.Tests.Payments;

[Collection("sql")]
public sealed class PaymentRecordEndpointTests : IClassFixture<ApiFactoryWithShortBankTimeout>
{
    private readonly ApiFactoryWithShortBankTimeout _fixture;

    public PaymentRecordEndpointTests(ApiFactoryWithShortBankTimeout fixture) => _fixture = fixture;

    // T-I-10 · PR-07 · integration
    // Given a completed, a failed (steered refuse) and a pending payment and a refused reference on Ops van (arranged through PaymentService from a host scope and the steering port), When GET /api/cards/{id}/payments/{reference} for each, Then quote {amount, fee "0.00", total, from, to, wording {version v1, heading, lines, button}}, acceptance {at, actor {kind cardholder, name Priya Nair}, authorityBasis, origin cardholder_view}, status, refusal fields for the refused one, payment {state, balanceBefore/After where settled, simulated true for the steered one} and events with times
    // Catches: an incomplete compliance record for any state (AC-20, R-14, OB-9)
    [Fact(Skip = "todo")]
    public void Record_shows_shown_accepted_and_outcome_for_every_state()
    {
    }

    // T-I-10 · PR-07 · integration
    // Given those records, When a status check, a repeated confirm and a host restart (CreateRestartedHost on the same database) happen, Then each record's quote and acceptance parts are byte-for-byte unchanged and only events/payment state grew
    // Catches: the shown-and-accepted part being rewritten (AC-20, R-14)
    [Fact(Skip = "todo")]
    public void Shown_part_never_changes()
    {
    }

    // T-I-10 · PR-07 · integration
    // Given another card's reference, a malformed reference and an unknown card, When GET the record, Then 404
    // Catches: records leaking across cards (design §10)
    [Fact(Skip = "todo")]
    public void Record_of_another_card_is_not_found()
    {
    }
}
