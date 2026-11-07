namespace CardLab.Domain.Tests.Application;

public sealed class CardholderViewServiceTests
{
    // T-U-29 · PR-04 · unit (application)
    // Given Ops van with approved £120.00 and £30.50, a declined £500.00, the seeded linked account and a FakeBank at £25,000.00 with 12 entries, When GetAsync, Then card name, last four 0001, cardholder Priya Nair, balance £150.50, Available with no message, account name / 7731 / £25,000.00 / authority, at most 10 card rows newest first and 10 account entries; at £0.00 → NothingToPay with W-7; at £1,000,000.01 → AboveLimit with W-8
    // Catches: a view that misstates the balance or offers pay when it must not (AC-1, AC-4, AC-5, AC-24, R-16)
    [Fact(Skip = "todo")]
    public void View_shows_balance_account_and_availability()
    {
    }

    // T-U-40 · PR-04 · unit (application)
    // Given a pending £150.50 payment PAY-00000001, When GetAsync, Then availability PaymentPending with W-9 naming £150.50 and PAY-00000001, PendingPayment receipt with W-5, and the bank received no status check (the view is a pure read, DEC-13)
    // Catches: the pending payment hidden or the view writing (AC-11, AC-13)
    [Fact(Skip = "todo")]
    public void View_shows_the_pending_payment_without_checking()
    {
    }

    // T-U-41 · PR-04 · unit (application)
    // Given no card with the id, When GetAsync, Then CardNotFound
    // Catches: exceptions for unknown cards (API 404)
    [Fact(Skip = "todo")]
    public void Unknown_card_is_not_found()
    {
    }
}
