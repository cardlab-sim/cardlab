namespace CardLab.Domain.Authorisation;

/// <summary>Result of an authorisation (R-7).</summary>
public enum Outcome
{
    /// <summary>The transaction may proceed; it counts towards spend.</summary>
    Approved,

    /// <summary>The transaction is refused; it never counts towards spend (R-5).</summary>
    Declined,
}

/// <summary>The single reason code of a decision (R-7).</summary>
public enum Reason
{
    /// <summary><c>approved</c>.</summary>
    Approved,

    /// <summary><c>card_frozen</c>: the card is frozen (R-3).</summary>
    CardFrozen,

    /// <summary><c>limit_exceeded</c>: some limit on the category would be exceeded (R-5).</summary>
    LimitExceeded,

    /// <summary><c>card_not_found</c>: no card has this number or id; nothing stored.</summary>
    CardNotFound,

    /// <summary><c>invalid_request</c>: malformed request; nothing stored.</summary>
    InvalidRequest,
}
