using CreditPlus.Crm.Api.Application;
using CreditPlus.Crm.Api.Domain;
using CreditPlus.Crm.Api.Infrastructure;

namespace CreditPlus.Crm.Tests;

/// <summary>
/// The lending rules a bank cannot afford to have silently break: the KYC gate,
/// the status workflow, and the payment arithmetic.
/// </summary>
public sealed class LoanWorkflowTests : IClassFixture<TestDatabase>
{
    private readonly TestDatabase database;

    public LoanWorkflowTests(TestDatabase database) => this.database = database;

    private (LoanService Loans, Guid LeadId) Arrange(string kycStatus)
    {
        var context = database.CreateContext();
        var lead = database.SeedLead(context, kyc: kycStatus);
        return (database.CreateLoans(context), lead.Id);
    }

    [Fact]
    public void Approving_without_verified_kyc_is_refused()
    {
        var (loans, leadId) = Arrange(KycStatuses.Pending);
        var loan = loans.Create(new CreateLoanRequest(leadId, "WorkingCapital", 50_000m, 36, 7.5m));

        loans.Move(loan.Id, new MoveLoanRequest(LoanStatuses.Submitted, null));
        loans.Move(loan.Id, new MoveLoanRequest(LoanStatuses.UnderReview, null));

        var failure = Assert.Throws<CrmValidationException>(
            () => loans.Move(loan.Id, new MoveLoanRequest(LoanStatuses.Approved, null)));

        Assert.Contains("KYC must be verified", failure.Message);
        Assert.Equal(LoanStatuses.UnderReview, loans.FindLoan(loan.Id)!.Status);
    }

    [Fact]
    public void Approving_with_verified_kyc_succeeds()
    {
        var (loans, leadId) = Arrange(KycStatuses.Verified);
        var loan = loans.Create(new CreateLoanRequest(leadId, "WorkingCapital", 50_000m, 36, 7.5m));

        loans.Move(loan.Id, new MoveLoanRequest(LoanStatuses.Submitted, null));
        loans.Move(loan.Id, new MoveLoanRequest(LoanStatuses.UnderReview, null));
        var approved = loans.Move(loan.Id, new MoveLoanRequest(LoanStatuses.Approved, "Checked"));

        Assert.Equal(LoanStatuses.Approved, approved.Status);
        Assert.Equal(LoanStatuses.Approved, approved.Decision);
        Assert.NotNull(approved.DecidedAt);
    }

    [Theory]
    [InlineData(LoanStatuses.Draft, LoanStatuses.Approved)]
    [InlineData(LoanStatuses.Draft, LoanStatuses.Disbursed)]
    [InlineData(LoanStatuses.Submitted, LoanStatuses.Disbursed)]
    public void Skipping_a_workflow_step_is_refused(string from, string to)
    {
        var (loans, leadId) = Arrange(KycStatuses.Verified);
        var loan = loans.Create(new CreateLoanRequest(leadId, "WorkingCapital", 10_000m, 12, 5m));

        // Walk to the starting status the case needs.
        if (from != LoanStatuses.Draft)
        {
            loans.Move(loan.Id, new MoveLoanRequest(LoanStatuses.Submitted, null));
        }

        Assert.Throws<CrmValidationException>(
            () => loans.Move(loan.Id, new MoveLoanRequest(to, null)));
    }

    [Fact]
    public void A_rejected_application_cannot_move_again()
    {
        var (loans, leadId) = Arrange(KycStatuses.Verified);
        var loan = loans.Create(new CreateLoanRequest(leadId, "PersonalLoan", 5_000m, 12, 9m));

        loans.Move(loan.Id, new MoveLoanRequest(LoanStatuses.Submitted, null));
        loans.Move(loan.Id, new MoveLoanRequest(LoanStatuses.Rejected, "Insufficient income"));

        Assert.Throws<CrmValidationException>(
            () => loans.Move(loan.Id, new MoveLoanRequest(LoanStatuses.Approved, null)));
    }

    [Fact]
    public void An_approved_application_cannot_be_deleted()
    {
        var (loans, leadId) = Arrange(KycStatuses.Verified);
        var loan = loans.Create(new CreateLoanRequest(leadId, "TradeFinance", 20_000m, 24, 6m));

        loans.Move(loan.Id, new MoveLoanRequest(LoanStatuses.Submitted, null));
        loans.Move(loan.Id, new MoveLoanRequest(LoanStatuses.UnderReview, null));
        loans.Move(loan.Id, new MoveLoanRequest(LoanStatuses.Approved, null));

        Assert.Throws<CrmValidationException>(() => loans.Delete(loan.Id));
        Assert.NotNull(loans.FindLoan(loan.Id));
    }

    [Fact]
    public void Closing_a_deal_settles_its_probability()
    {
        var (loans, leadId) = Arrange(KycStatuses.Verified);
        var loan = loans.Create(new CreateLoanRequest(leadId, "MortgageLoan", 200_000m, 240, 5.5m));

        loans.Move(loan.Id, new MoveLoanRequest(LoanStatuses.Submitted, null));
        var rejected = loans.Move(loan.Id, new MoveLoanRequest(LoanStatuses.Rejected, "No collateral"));

        Assert.Equal(LoanStatuses.Rejected, rejected.Decision);
        Assert.NotNull(rejected.DecidedAt);
    }

    [Theory]
    // amount, months, annual rate, expected monthly payment
    [InlineData(95_000, 48, 6.75, 2263.89)]
    [InlineData(45_000, 36, 7.5, 1399.78)]
    [InlineData(120_000, 60, 6.25, 2333.91)]
    public void Monthly_payment_matches_the_amortisation_formula(
        decimal amount, int months, decimal rate, decimal expected)
    {
        var (loans, leadId) = Arrange(KycStatuses.Verified);
        var loan = loans.Create(new CreateLoanRequest(leadId, "EquipmentFinance", amount, months, rate));

        Assert.Equal(expected, loan.MonthlyPayment);
    }

    [Fact]
    public void A_zero_rate_loan_divides_the_principal_evenly()
    {
        var (loans, leadId) = Arrange(KycStatuses.Verified);
        var loan = loans.Create(new CreateLoanRequest(leadId, "PersonalLoan", 12_000m, 12, 0m));

        Assert.Equal(1_000m, loan.MonthlyPayment);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5000)]
    public void A_non_positive_amount_is_refused(decimal amount)
    {
        var (loans, leadId) = Arrange(KycStatuses.Verified);

        Assert.Throws<CrmValidationException>(
            () => loans.Create(new CreateLoanRequest(leadId, "WorkingCapital", amount, 12, 5m)));
    }

    [Fact]
    public void An_unknown_product_is_refused()
    {
        var (loans, leadId) = Arrange(KycStatuses.Verified);

        Assert.Throws<CrmValidationException>(
            () => loans.Create(new CreateLoanRequest(leadId, "CryptoLoan", 10_000m, 12, 5m)));
    }

    [Fact]
    public void References_are_sequential_and_unique()
    {
        var (loans, leadId) = Arrange(KycStatuses.Verified);

        var first = loans.Create(new CreateLoanRequest(leadId, "WorkingCapital", 1_000m, 12, 5m));
        var second = loans.Create(new CreateLoanRequest(leadId, "WorkingCapital", 2_000m, 12, 5m));

        Assert.NotEqual(first.Reference, second.Reference);
        Assert.StartsWith($"LN-{DateTime.UtcNow.Year}-", first.Reference);
    }
}
