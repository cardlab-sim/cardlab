using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CardLab.Api.Contracts;
using CardLab.Domain.Authorisation;

namespace CardLab.Integration.Tests.Harness;

/// <summary>An HTTP response with its status, parsed body (when the status has one) and raw text (for PAN scans).</summary>
/// <typeparam name="T">Body type.</typeparam>
/// <param name="Status">HTTP status.</param>
/// <param name="Body">Parsed body, or default when the response has no JSON body of this type.</param>
/// <param name="Raw">Raw response text.</param>
public sealed record ApiResponse<T>(HttpStatusCode Status, T? Body, string Raw)
{
    /// <summary>Parses <see cref="Raw"/> as another shape, e.g. the 409 <c>PaymentProblemDto</c> fields of a ProblemDetails body (002).</summary>
    /// <typeparam name="TProblem">Shape to read.</typeparam>
    /// <returns>The parsed value, or default when the body is empty.</returns>
    public TProblem? ProblemAs<TProblem>() =>
        Raw.Length == 0 ? default : JsonSerializer.Deserialize<TProblem>(Raw, new JsonSerializerOptions(JsonSerializerDefaults.Web));
}

/// <summary>
/// Typed client for every endpoint in design §7 (shared by all integration PRs; complete in PR-00). Returns the
/// status and body without throwing on 4xx/5xx so tests can assert error paths.
/// </summary>
public sealed class ApiClient
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _http;

    /// <summary>Wraps an <see cref="HttpClient"/> created by <c>ApiFactory.CreateClient()</c>.</summary>
    /// <param name="http">Client whose base address is the test server.</param>
    public ApiClient(HttpClient http) => _http = http;

    /// <summary><c>GET /api/cards</c>.</summary>
    /// <returns>Status and cards.</returns>
    public Task<ApiResponse<List<CardSummaryDto>>> ListCardsAsync() => SendAsync<List<CardSummaryDto>>(HttpMethod.Get, "/api/cards");

    /// <summary><c>GET /api/cards/{id}</c>.</summary>
    /// <param name="id">Card id.</param>
    /// <returns>Status and card detail.</returns>
    public Task<ApiResponse<CardDetailDto>> GetCardAsync(Guid id) => SendAsync<CardDetailDto>(HttpMethod.Get, $"/api/cards/{id}");

    /// <summary><c>POST /api/cards/{id}/freeze</c>.</summary>
    /// <param name="id">Card id.</param>
    /// <returns>Status and new status.</returns>
    public Task<ApiResponse<StatusDto>> FreezeAsync(Guid id) => SendAsync<StatusDto>(HttpMethod.Post, $"/api/cards/{id}/freeze");

    /// <summary><c>POST /api/cards/{id}/unfreeze</c>.</summary>
    /// <param name="id">Card id.</param>
    /// <returns>Status and new status.</returns>
    public Task<ApiResponse<StatusDto>> UnfreezeAsync(Guid id) => SendAsync<StatusDto>(HttpMethod.Post, $"/api/cards/{id}/unfreeze");

    /// <summary><c>PUT /api/cards/{id}/limits/{category}/{period}</c> with <c>{amount}</c>.</summary>
    /// <param name="id">Card id.</param>
    /// <param name="category">Category wire name.</param>
    /// <param name="period">Period wire name.</param>
    /// <param name="amount">Amount text (sent as given, may be invalid).</param>
    /// <returns>Status and limit.</returns>
    public Task<ApiResponse<LimitDto>> SetLimitAsync(Guid id, string category, string period, string? amount) =>
        SendAsync<LimitDto>(HttpMethod.Put, $"/api/cards/{id}/limits/{category}/{period}", new SetLimitRequest(amount));

    /// <summary><c>DELETE /api/cards/{id}/limits/{category}/{period}</c>.</summary>
    /// <param name="id">Card id.</param>
    /// <param name="category">Category wire name.</param>
    /// <param name="period">Period wire name.</param>
    /// <returns>Status (204 expected).</returns>
    public Task<ApiResponse<object>> RemoveLimitAsync(Guid id, string category, string period) =>
        SendAsync<object>(HttpMethod.Delete, $"/api/cards/{id}/limits/{category}/{period}");

    /// <summary><c>GET /api/cards/{id}/transactions</c>.</summary>
    /// <param name="id">Card id.</param>
    /// <returns>Status and transactions, newest first.</returns>
    public Task<ApiResponse<List<TransactionDto>>> ListTransactionsAsync(Guid id) => SendAsync<List<TransactionDto>>(HttpMethod.Get, $"/api/cards/{id}/transactions");

    /// <summary><c>GET /api/cards/{id}/audit</c>.</summary>
    /// <param name="id">Card id.</param>
    /// <returns>Status and audit events, newest first.</returns>
    public Task<ApiResponse<List<AuditDto>>> ListAuditAsync(Guid id) => SendAsync<List<AuditDto>>(HttpMethod.Get, $"/api/cards/{id}/audit");

    /// <summary><c>POST /api/authorisations</c>.</summary>
    /// <param name="request">Raw request (fields may be null to simulate missing ones).</param>
    /// <returns>Status and decision body.</returns>
    public Task<ApiResponse<DecisionDto>> AuthoriseAsync(AuthorisationRequest request) => SendAsync<DecisionDto>(HttpMethod.Post, "/api/authorisations", request);

    /// <summary><c>POST /api/cards/{id}/simulate-purchase</c>.</summary>
    /// <param name="id">Card id.</param>
    /// <param name="purchase">Raw purchase.</param>
    /// <returns>Status and decision body.</returns>
    public Task<ApiResponse<DecisionDto>> SimulatePurchaseAsync(Guid id, SimulatedPurchase purchase) =>
        SendAsync<DecisionDto>(HttpMethod.Post, $"/api/cards/{id}/simulate-purchase", purchase);

    /// <summary><c>GET /api/health</c>.</summary>
    /// <returns>Status and health body.</returns>
    public Task<ApiResponse<HealthDto>> HealthAsync() => SendAsync<HealthDto>(HttpMethod.Get, "/api/health");

    /// <summary><c>POST /api/test/reset</c>.</summary>
    /// <returns>Status (204 when enabled, 404 when not).</returns>
    public Task<ApiResponse<object>> ResetAsync() => SendAsync<object>(HttpMethod.Post, "/api/test/reset");

    /// <summary>002: <c>GET /api/cardholder/cards/{id}</c>.</summary>
    /// <param name="id">Card id.</param>
    /// <returns>Status and view.</returns>
    public Task<ApiResponse<CardholderViewDto>> GetCardholderViewAsync(Guid id) => SendAsync<CardholderViewDto>(HttpMethod.Get, $"/api/cardholder/cards/{id}");

    /// <summary>002: <c>POST /api/cardholder/cards/{id}/quotes</c>, optionally with <c>X-CardLab-Origin</c>.</summary>
    /// <param name="id">Card id.</param>
    /// <param name="originHeader">Header value to send, or null for none.</param>
    /// <returns>Status (201 / 409 / 404) and quote; read 409 fields with <c>ProblemAs&lt;PaymentProblemDto&gt;()</c>.</returns>
    public Task<ApiResponse<QuoteDto>> CreateQuoteAsync(Guid id, string? originHeader = null) =>
        SendAsync<QuoteDto>(HttpMethod.Post, $"/api/cardholder/cards/{id}/quotes", headers: originHeader is null ? null : new Dictionary<string, string> { ["X-CardLab-Origin"] = originHeader });

    /// <summary>002: <c>POST /api/cardholder/cards/{id}/payments/{reference}/confirm</c>.</summary>
    /// <param name="id">Card id.</param>
    /// <param name="reference">Reference (sent as given, may be malformed).</param>
    /// <returns>Status and receipt.</returns>
    public Task<ApiResponse<ReceiptDto>> ConfirmAsync(Guid id, string reference) =>
        SendAsync<ReceiptDto>(HttpMethod.Post, $"/api/cardholder/cards/{id}/payments/{Uri.EscapeDataString(reference)}/confirm");

    /// <summary>002: <c>POST /api/cardholder/cards/{id}/payments/{reference}/check</c>.</summary>
    /// <param name="id">Card id.</param>
    /// <param name="reference">Reference.</param>
    /// <returns>Status and receipt.</returns>
    public Task<ApiResponse<ReceiptDto>> CheckAsync(Guid id, string reference) =>
        SendAsync<ReceiptDto>(HttpMethod.Post, $"/api/cardholder/cards/{id}/payments/{Uri.EscapeDataString(reference)}/check");

    /// <summary>002: <c>GET /api/cardholder/cards/{id}/payments/{reference}</c>.</summary>
    /// <param name="id">Card id.</param>
    /// <param name="reference">Reference.</param>
    /// <returns>Status and receipt.</returns>
    public Task<ApiResponse<ReceiptDto>> GetReceiptAsync(Guid id, string reference) =>
        SendAsync<ReceiptDto>(HttpMethod.Get, $"/api/cardholder/cards/{id}/payments/{Uri.EscapeDataString(reference)}");

    /// <summary>002: <c>GET /api/fake-bank</c>.</summary>
    /// <returns>Status and fake bank state.</returns>
    public Task<ApiResponse<FakeBankDto>> GetFakeBankAsync() => SendAsync<FakeBankDto>(HttpMethod.Get, "/api/fake-bank");

    /// <summary>002: <c>PUT /api/fake-bank/steering</c>.</summary>
    /// <param name="nextDebitOutcome">Wire value or null (unchanged).</param>
    /// <param name="nextStatusCheck">Wire value or null (unchanged).</param>
    /// <returns>Status and fake bank state.</returns>
    public Task<ApiResponse<FakeBankDto>> SetSteeringAsync(string? nextDebitOutcome, string? nextStatusCheck = null) =>
        SendAsync<FakeBankDto>(HttpMethod.Put, "/api/fake-bank/steering", new SteeringRequest(nextDebitOutcome, nextStatusCheck));

    /// <summary>002: <c>GET /api/cards/{id}/activity</c>.</summary>
    /// <param name="id">Card id.</param>
    /// <param name="take">Optional <c>take</c>.</param>
    /// <returns>Status and rows, newest first.</returns>
    public Task<ApiResponse<List<ActivityDto>>> ListActivityAsync(Guid id, int? take = null) =>
        SendAsync<List<ActivityDto>>(HttpMethod.Get, take is null ? $"/api/cards/{id}/activity" : $"/api/cards/{id}/activity?take={take}");

    /// <summary>002: <c>GET /api/cards/{id}/payments</c>.</summary>
    /// <param name="id">Card id.</param>
    /// <returns>Status and items, newest first.</returns>
    public Task<ApiResponse<List<PaymentListItemDto>>> ListPaymentsAsync(Guid id) => SendAsync<List<PaymentListItemDto>>(HttpMethod.Get, $"/api/cards/{id}/payments");

    /// <summary>002: <c>GET /api/cards/{id}/payments/{reference}</c>.</summary>
    /// <param name="id">Card id.</param>
    /// <param name="reference">Reference.</param>
    /// <returns>Status and record.</returns>
    public Task<ApiResponse<PaymentRecordDto>> GetPaymentRecordAsync(Guid id, string reference) =>
        SendAsync<PaymentRecordDto>(HttpMethod.Get, $"/api/cards/{id}/payments/{Uri.EscapeDataString(reference)}");

    /// <summary>Sends any request (for header and raw-route tests).</summary>
    /// <param name="request">The request.</param>
    /// <returns>The raw response.</returns>
    public Task<HttpResponseMessage> SendRawAsync(HttpRequestMessage request) => _http.SendAsync(request);

    private async Task<ApiResponse<T>> SendAsync<T>(HttpMethod method, string path, object? body = null, IReadOnlyDictionary<string, string>? headers = null)
    {
        using var request = new HttpRequestMessage(method, path);
        foreach (var (name, value) in headers ?? new Dictionary<string, string>())
        {
            request.Headers.TryAddWithoutValidation(name, value);
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body, body.GetType(), options: Json);
        }

        using var response = await _http.SendAsync(request);
        var raw = await response.Content.ReadAsStringAsync();
        T? parsed = default;
        var isJson = response.Content.Headers.ContentType?.MediaType is "application/json";
        if (isJson && raw.Length > 0 && typeof(T) != typeof(object))
        {
            parsed = JsonSerializer.Deserialize<T>(raw, Json);
        }

        return new ApiResponse<T>(response.StatusCode, parsed, raw);
    }
}
