namespace CardLab.Domain.Authorisation;

/// <summary>
/// A raw processor authorisation request exactly as received on <c>POST /api/authorisations</c>
/// (every field optional text so each malformed case becomes <c>invalid_request</c>, AC-20).
/// </summary>
/// <remarks>Also the JSON request body bound by the endpoint (camelCase names).</remarks>
/// <param name="CardNumber">16-digit card number (PAN). Never logged, stored or echoed (NFR-1).</param>
/// <param name="Amount">Decimal text, e.g. <c>"20.00"</c>.</param>
/// <param name="Category">Category wire name, e.g. <c>"fuel"</c>.</param>
/// <param name="Merchant">Merchant name.</param>
/// <param name="Timestamp">ISO-8601 instant with offset, e.g. <c>"2026-10-24T23:30:00Z"</c>.</param>
public sealed record AuthorisationRequest(string? CardNumber, string? Amount, string? Category, string? Merchant, string? Timestamp)
{
    /// <summary>Text form with the card number masked to its last four digits (NFR-1, design §10).</summary>
    /// <returns>e.g. <c>AuthorisationRequest { CardNumber = ************0001, Amount = 20.00, ... }</c>; never 16 consecutive digits.</returns>
    public override string ToString() => throw new NotImplementedException("PR-04");
}

/// <summary>
/// A simulated purchase from the admin UI on <c>POST /api/cards/{id}/simulate-purchase</c>: the card is
/// identified by id, so no card number exists on this path (DEC-14).
/// </summary>
/// <param name="Amount">Decimal text.</param>
/// <param name="Category">Category wire name.</param>
/// <param name="Merchant">Merchant name.</param>
/// <param name="Timestamp">Optional ISO-8601 instant with offset; defaults to <c>IClock.UtcNow</c>.</param>
public sealed record SimulatedPurchase(string? Amount, string? Category, string? Merchant, string? Timestamp);
