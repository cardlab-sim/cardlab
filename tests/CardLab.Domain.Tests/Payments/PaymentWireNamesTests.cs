using CardLab.Domain.Payments;

namespace CardLab.Domain.Tests.Payments;

public sealed class PaymentWireNamesTests
{
    // T-U-35 · PR-01 · unit
    // Given every value of PaymentState, FailureReason, PayAvailability, ActivityKind, PaymentOrigin, QuoteStatus, ActorKind, PaymentKind and PaymentEventKind, When ToWire, Then the design §7 wire names (e.g. pending, insufficient_funds, nothing_to_pay, payment, deploy_smoke, refused, cardholder, full, status_check_not_received); and OriginFromHeader("deploy-smoke") is DeploySmoke while null, "", "Deploy-Smoke", " deploy-smoke" and "deploy_smoke" are CardholderView
    // Catches: enum names leaking to the wire, or case variants marking a real payment as smoke (DEC-15)
    [Fact]
    public void Payment_enums_map_to_wire_names()
    {
        Assert.Equal(["pending", "completed", "failed"], new[] { PaymentState.Pending, PaymentState.Completed, PaymentState.Failed }.Select(PaymentWireNames.ToWire));
        Assert.Equal(
            ["insufficient_funds", "account_unavailable", "not_received"],
            new[] { FailureReason.InsufficientFunds, FailureReason.AccountUnavailable, FailureReason.NotReceived }.Select(PaymentWireNames.ToWire));
        Assert.Equal(
            ["available", "nothing_to_pay", "above_limit", "payment_pending"],
            new[] { PayAvailability.Available, PayAvailability.NothingToPay, PayAvailability.AboveLimit, PayAvailability.PaymentPending }.Select(PaymentWireNames.ToWire));
        Assert.Equal(["purchase", "payment"], new[] { ActivityKind.Purchase, ActivityKind.Payment }.Select(PaymentWireNames.ToWire));
        Assert.Equal(["cardholder_view", "deploy_smoke"], new[] { PaymentOrigin.CardholderView, PaymentOrigin.DeploySmoke }.Select(PaymentWireNames.ToWire));
        Assert.Equal(["open", "confirmed", "refused"], new[] { QuoteStatus.Open, QuoteStatus.Confirmed, QuoteStatus.Refused }.Select(PaymentWireNames.ToWire));
        Assert.Equal(["admin", "cardholder"], new[] { ActorKind.Admin, ActorKind.Cardholder }.Select(PaymentWireNames.ToWire));
        Assert.Equal("full", PaymentWireNames.ToWire(PaymentKind.Full));
    }

    [Fact]
    public void Event_kinds_map_to_snake_case_wire_names()
    {
        var expected = new Dictionary<PaymentEventKind, string>
        {
            [PaymentEventKind.ConfirmCreated] = "confirm_created",
            [PaymentEventKind.ConfirmRepeated] = "confirm_repeated",
            [PaymentEventKind.ConfirmRefused] = "confirm_refused",
            [PaymentEventKind.DebitRequested] = "debit_requested",
            [PaymentEventKind.BankCompleted] = "bank_completed",
            [PaymentEventKind.BankRefused] = "bank_refused",
            [PaymentEventKind.BankUnanswered] = "bank_unanswered",
            [PaymentEventKind.StatusCheckCompleted] = "status_check_completed",
            [PaymentEventKind.StatusCheckRefused] = "status_check_refused",
            [PaymentEventKind.StatusCheckNotReceived] = "status_check_not_received",
            [PaymentEventKind.StatusCheckUnanswered] = "status_check_unanswered",
        };

        Assert.Equal(Enum.GetValues<PaymentEventKind>().Length, expected.Count);
        foreach (var (kind, wire) in expected)
        {
            Assert.Equal(wire, PaymentWireNames.ToWire(kind));
        }
    }

    [Fact]
    public void Unknown_enum_values_are_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PaymentWireNames.ToWire((PaymentState)99));
        Assert.Throws<ArgumentOutOfRangeException>(() => PaymentWireNames.ToWire((PaymentEventKind)99));
    }

    [Fact]
    public void Only_the_exact_header_value_marks_deploy_smoke()
    {
        Assert.Equal(PaymentOrigin.DeploySmoke, PaymentWireNames.OriginFromHeader("deploy-smoke"));
        foreach (var value in new string?[] { null, string.Empty, "Deploy-Smoke", " deploy-smoke", "deploy_smoke" })
        {
            Assert.Equal(PaymentOrigin.CardholderView, PaymentWireNames.OriginFromHeader(value));
        }
    }
}
