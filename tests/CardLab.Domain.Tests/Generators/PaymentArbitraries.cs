namespace CardLab.Domain.Tests.Generators;

/// <summary>
/// 002 FsCheck arbitraries for the payment properties (design §15.2, T-P-1..T-P-4): <c>PaymentReference</c> (valid
/// format, small pool so repeats are common), <c>PaymentState</c>, <c>BankOutcome</c> (all four kinds, simulated or not)
/// and a <c>ConfirmScript</c> record (a sequence of steps on one card: quote, confirm(reference index), approved purchase
/// (amount), bank outcome for the in-flight payment) that shrinks by dropping steps. Implemented in PR-01 as public
/// static methods returning <c>Arbitrary&lt;T&gt;</c>, like <c>CardLabArbitraries</c>.
/// </summary>
public static class PaymentArbitraries
{
}
