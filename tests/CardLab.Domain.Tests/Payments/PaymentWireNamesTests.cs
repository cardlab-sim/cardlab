namespace CardLab.Domain.Tests.Payments;

public sealed class PaymentWireNamesTests
{
    // T-U-35 · PR-01 · unit
    // Given every value of PaymentState, FailureReason, PayAvailability, ActivityKind, PaymentOrigin, QuoteStatus, ActorKind, PaymentKind and PaymentEventKind, When ToWire, Then the design §7 wire names (e.g. pending, insufficient_funds, nothing_to_pay, payment, deploy_smoke, refused, cardholder, full, status_check_not_received); and OriginFromHeader("deploy-smoke") is DeploySmoke while null, "", "Deploy-Smoke", " deploy-smoke" and "deploy_smoke" are CardholderView
    // Catches: enum names leaking to the wire, or case variants marking a real payment as smoke (DEC-15)
    [Fact(Skip = "todo")]
    public void Payment_enums_map_to_wire_names()
    {
    }
}
