namespace CardLab.Domain.Payments;

/// <summary>
/// Wire names of the payment enums (design §7): states <c>pending|completed|failed</c>; failure reasons
/// <c>insufficient_funds|account_unavailable|not_received</c>; availability
/// <c>available|nothing_to_pay|above_limit|payment_pending</c>; activity kinds <c>purchase|payment</c>; origins
/// <c>cardholder_view|deploy_smoke</c>; quote status <c>open|confirmed|refused</c>; actor kinds <c>admin|cardholder</c>;
/// kind <c>full</c>; event kinds snake_case of the enum name (e.g. <c>confirm_created</c>, <c>status_check_not_received</c>).
/// </summary>
/// <remarks>Implemented in PR-01. Unknown enum values throw <see cref="ArgumentOutOfRangeException"/> like <c>WireNames</c>.</remarks>
public static class PaymentWireNames
{
    /// <summary>The only header value that marks a deploy-smoke quote (DEC-15).</summary>
    public const string DeploySmokeHeaderValue = "deploy-smoke";

    /// <summary>Wire name of a payment state.</summary>
    /// <param name="state">State.</param>
    /// <returns>e.g. <c>pending</c>.</returns>
    public static string ToWire(PaymentState state) => throw new NotImplementedException("PR-01");

    /// <summary>Wire name of a failure reason.</summary>
    /// <param name="reason">Reason.</param>
    /// <returns>e.g. <c>insufficient_funds</c>.</returns>
    public static string ToWire(FailureReason reason) => throw new NotImplementedException("PR-01");

    /// <summary>Wire name of a pay availability.</summary>
    /// <param name="availability">Availability.</param>
    /// <returns>e.g. <c>nothing_to_pay</c>.</returns>
    public static string ToWire(PayAvailability availability) => throw new NotImplementedException("PR-01");

    /// <summary>Wire name of an activity kind.</summary>
    /// <param name="kind">Kind.</param>
    /// <returns><c>purchase</c> or <c>payment</c>.</returns>
    public static string ToWire(ActivityKind kind) => throw new NotImplementedException("PR-01");

    /// <summary>Wire name of an origin.</summary>
    /// <param name="origin">Origin.</param>
    /// <returns><c>cardholder_view</c> or <c>deploy_smoke</c>.</returns>
    public static string ToWire(PaymentOrigin origin) => throw new NotImplementedException("PR-01");

    /// <summary>Wire name of a quote status.</summary>
    /// <param name="status">Status.</param>
    /// <returns><c>open</c>, <c>confirmed</c> or <c>refused</c>.</returns>
    public static string ToWire(QuoteStatus status) => throw new NotImplementedException("PR-01");

    /// <summary>Wire name of an actor kind.</summary>
    /// <param name="kind">Kind.</param>
    /// <returns><c>admin</c> or <c>cardholder</c>.</returns>
    public static string ToWire(ActorKind kind) => throw new NotImplementedException("PR-01");

    /// <summary>Wire name of a payment kind.</summary>
    /// <param name="kind">Kind.</param>
    /// <returns><c>full</c>.</returns>
    public static string ToWire(PaymentKind kind) => throw new NotImplementedException("PR-01");

    /// <summary>Wire name of a payment event kind.</summary>
    /// <param name="kind">Kind.</param>
    /// <returns>snake_case, e.g. <c>confirm_repeated</c>.</returns>
    public static string ToWire(PaymentEventKind kind) => throw new NotImplementedException("PR-01");

    /// <summary>Maps the optional <c>X-CardLab-Origin</c> header to an origin (DEC-15).</summary>
    /// <param name="headerValue">Header value or null.</param>
    /// <returns><see cref="PaymentOrigin.DeploySmoke"/> only when the value equals <see cref="DeploySmokeHeaderValue"/>
    /// exactly (ordinal); <see cref="PaymentOrigin.CardholderView"/> for anything else, including null.</returns>
    public static PaymentOrigin OriginFromHeader(string? headerValue) => throw new NotImplementedException("PR-01");
}
