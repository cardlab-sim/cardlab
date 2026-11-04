using CardLab.Domain.Limits;

namespace CardLab.Domain.Authorisation;

/// <summary>An authorisation decision with the remaining amount of every limit in the transaction's category.</summary>
/// <param name="Outcome">Approved or declined.</param>
/// <param name="Reason">Exactly one reason code.</param>
/// <param name="Limits">One entry per limit on the card in the category (any order is acceptable; the
/// decider keeps the input order); empty for unknown cards and invalid requests.</param>
public sealed record Decision(Outcome Outcome, Reason Reason, IReadOnlyList<LimitRemaining> Limits);
