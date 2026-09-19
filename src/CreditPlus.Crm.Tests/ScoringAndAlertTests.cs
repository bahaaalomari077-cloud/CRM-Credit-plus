using CreditPlus.Crm.Api.Domain;
using CreditPlus.Crm.Api.Infrastructure;

namespace CreditPlus.Crm.Tests;

/// <summary>
/// Scoring has to be defensible: a salesperson acts on the band, so each signal
/// must move the number in the direction the business intends.
/// </summary>
public sealed class ScoringTests : IDisposable
{
    private readonly TestDatabase database = new();

    public void Dispose() => database.Dispose();

    [Fact]
    public void A_bare_lead_scores_low()
    {
        var context = database.CreateContext();
        var lead = database.SeedLead(context, email: "", phone: "", source: "Other", status: LeadStatuses.New);

        var (score, band, _) = database.CreateCustomers(context).Score(lead.Id);

        Assert.True(score < 40, $"expected a cold score, got {score}");
        Assert.Equal("Cold", band);
    }

    [Fact]
    public void Contact_details_raise_the_score()
    {
        var context = database.CreateContext();
        var bare = database.SeedLead(context, email: "", phone: "");
        var reachable = database.SeedLead(context, email: "a@b.com", phone: "+962 7 1");
        var customers = database.CreateCustomers(context);

        Assert.Equal(20, customers.Score(reachable.Id).Score - customers.Score(bare.Id).Score);
    }

    [Fact]
    public void A_referral_outscores_a_cold_source()
    {
        var context = database.CreateContext();
        var referral = database.SeedLead(context, source: "Referral");
        var other = database.SeedLead(context, source: "Other");
        var customers = database.CreateCustomers(context);

        Assert.True(customers.Score(referral.Id).Score > customers.Score(other.Id).Score);
    }

    [Fact]
    public void Verified_kyc_adds_and_expired_kyc_subtracts()
    {
        var context = database.CreateContext();
        var verified = database.SeedLead(context, kyc: KycStatuses.Verified);
        var neutral = database.SeedLead(context, kyc: KycStatuses.NotStarted);
        var expired = database.SeedLead(context, kyc: KycStatuses.Expired);
        var customers = database.CreateCustomers(context);

        Assert.Equal(15, customers.Score(verified.Id).Score - customers.Score(neutral.Id).Score);
        Assert.Equal(10, customers.Score(neutral.Id).Score - customers.Score(expired.Id).Score);
    }

    [Fact]
    public void A_high_risk_rating_lowers_the_score()
    {
        var context = database.CreateContext();
        var normal = database.SeedLead(context, risk: RiskRatings.Medium);
        var risky = database.SeedLead(context, risk: RiskRatings.High);
        var customers = database.CreateCustomers(context);

        Assert.Equal(10, customers.Score(normal.Id).Score - customers.Score(risky.Id).Score);
    }

    [Fact]
    public void An_open_ticket_lowers_the_score()
    {
        var context = database.CreateContext();
        var lead = database.SeedLead(context);
        var customers = database.CreateCustomers(context);
        var before = customers.Score(lead.Id).Score;

        database.CreateTickets(context).Create(new CreateTicketRequest(
            lead.Id, lead.FullName, "Complaint", "Details", "Complaint", TicketPriorities.High));

        Assert.Equal(5, before - customers.Score(lead.Id).Score);
    }

    [Fact]
    public void Every_point_is_explained()
    {
        var context = database.CreateContext();
        var lead = database.SeedLead(context, kyc: KycStatuses.Verified, source: "Referral");

        var (score, _, reasons) = database.CreateCustomers(context).Score(lead.Id);

        Assert.NotEmpty(reasons);

        // The stated reasons must add up to the score, or the explanation is a fiction.
        var total = reasons.Sum(reason =>
            int.Parse(reason.Split(' ')[0].TrimStart('+')));
        Assert.Equal(score, total);
    }

    [Fact]
    public void The_score_never_leaves_the_zero_to_hundred_range()
    {
        var context = database.CreateContext();
        var best = database.SeedLead(context,
            status: LeadStatuses.Converted, kyc: KycStatuses.Verified,
            risk: RiskRatings.Low, source: "Referral");
        var worst = database.SeedLead(context,
            email: "", phone: "", status: LeadStatuses.Lost,
            kyc: KycStatuses.Rejected, risk: RiskRatings.High, source: "Other");
        var customers = database.CreateCustomers(context);

        Assert.InRange(customers.Score(best.Id).Score, 0, 100);
        Assert.InRange(customers.Score(worst.Id).Score, 0, 100);
    }
}

/// <summary>Alerts are derived, so they must appear and disappear with the data.</summary>
public sealed class AlertTests : IDisposable
{
    private readonly TestDatabase database = new();

    public void Dispose() => database.Dispose();

    [Fact]
    public void A_clean_database_raises_no_alerts()
    {
        var context = database.CreateContext();

        Assert.Empty(database.CreateInsights(context).GetAlerts());
    }

    [Fact]
    public void An_expired_document_raises_a_critical_alert()
    {
        var context = database.CreateContext();
        var lead = database.SeedLead(context);
        var kyc = database.CreateKyc(context);

        var document = kyc.AddDocument(new CreateKycDocumentRequest(
            lead.Id, "NationalId", "1", null, DateOnly.FromDateTime(DateTime.Today.AddDays(-1)), null));
        kyc.Verify(document.Id, new VerifyKycDocumentRequest(KycStatuses.Verified, null));

        var alert = Assert.Single(
            database.CreateInsights(context).GetAlerts(),
            candidate => candidate.Kind == AlertKinds.KycExpired);

        Assert.Equal(AlertSeverities.Critical, alert.Severity);
    }

    [Fact]
    public void A_document_expiring_soon_warns_but_one_expiring_later_does_not()
    {
        var context = database.CreateContext();
        var soon = database.SeedLead(context, name: "Soon");
        var later = database.SeedLead(context, name: "Later");
        var kyc = database.CreateKyc(context);

        foreach (var (lead, days) in new[] { (soon, 10), (later, 200) })
        {
            var document = kyc.AddDocument(new CreateKycDocumentRequest(
                lead.Id, "NationalId", "1", null, DateOnly.FromDateTime(DateTime.Today.AddDays(days)), null));
            kyc.Verify(document.Id, new VerifyKycDocumentRequest(KycStatuses.Verified, null));
        }

        var expiring = database.CreateInsights(context).GetAlerts()
            .Where(alert => alert.Kind == AlertKinds.KycExpiring)
            .ToList();

        var single = Assert.Single(expiring);
        Assert.Equal(soon.Id.ToString(), single.EntityId);
    }

    [Fact]
    public void A_breached_ticket_raises_a_critical_alert_and_resolving_it_clears_it()
    {
        var context = database.CreateContext();
        var tickets = database.CreateTickets(context);
        var insights = database.CreateInsights(context);

        var ticket = tickets.Create(new CreateTicketRequest(
            null, "Walk-in", "Late", "Details", "Complaint", TicketPriorities.Urgent));

        // Push the target into the past to simulate a breach.
        var entity = context.Tickets.Single(row => row.Id == ticket.Id);
        entity.DueAt = DateTimeOffset.UtcNow.AddHours(-1);
        context.SaveChanges();

        Assert.Contains(insights.GetAlerts(), alert => alert.Kind == AlertKinds.SlaBreached);

        tickets.Update(ticket.Id, new UpdateTicketRequest(
            "Late", "Details", "Complaint", TicketPriorities.Urgent,
            TicketStatuses.Resolved, null, "Handled"));

        // Nothing was deleted; the condition simply stopped being true.
        Assert.DoesNotContain(insights.GetAlerts(), alert => alert.Kind == AlertKinds.SlaBreached);
    }

    [Fact]
    public void An_application_waiting_on_a_decision_is_flagged()
    {
        var context = database.CreateContext();
        var lead = database.SeedLead(context, kyc: KycStatuses.Verified);
        var loans = database.CreateLoans(context);

        var loan = loans.Create(new CreateLoanRequest(lead.Id, "WorkingCapital", 10_000m, 12, 5m));

        // A draft has not been submitted, so nobody is waiting on it yet.
        Assert.DoesNotContain(database.CreateInsights(context).GetAlerts(),
            alert => alert.Kind == AlertKinds.LoanAwaitingDecision);

        loans.Move(loan.Id, new MoveLoanRequest(LoanStatuses.Submitted, null));

        Assert.Contains(database.CreateInsights(context).GetAlerts(),
            alert => alert.Kind == AlertKinds.LoanAwaitingDecision);
    }

    [Fact]
    public void Critical_alerts_are_listed_before_warnings_and_information()
    {
        var context = database.CreateContext();
        var lead = database.SeedLead(context);
        var kyc = database.CreateKyc(context);

        // One expired document (critical) and one expiring soon (warning).
        var expired = kyc.AddDocument(new CreateKycDocumentRequest(
            lead.Id, "NationalId", "1", null, DateOnly.FromDateTime(DateTime.Today.AddDays(-2)), null));
        kyc.Verify(expired.Id, new VerifyKycDocumentRequest(KycStatuses.Verified, null));

        var soon = kyc.AddDocument(new CreateKycDocumentRequest(
            lead.Id, "Passport", "2", null, DateOnly.FromDateTime(DateTime.Today.AddDays(5)), null));
        kyc.Verify(soon.Id, new VerifyKycDocumentRequest(KycStatuses.Verified, null));

        var alerts = database.CreateInsights(context).GetAlerts();

        Assert.Equal(AlertSeverities.Critical, alerts[0].Severity);
    }
}

/// <summary>The operations report feeds management decisions, so its arithmetic matters.</summary>
public sealed class OperationsReportTests : IDisposable
{
    private readonly TestDatabase database = new();

    public void Dispose() => database.Dispose();

    [Fact]
    public void An_empty_database_reports_zeroes_not_errors()
    {
        var context = database.CreateContext();

        var report = database.CreateInsights(context).GetOperationsReport();

        Assert.Equal(0, report.TotalLoans);
        Assert.Equal(0m, report.ApprovalRate);
        Assert.Equal(100m, report.SlaComplianceRate); // nothing breached yet
    }

    [Fact]
    public void Approval_rate_counts_only_decided_applications()
    {
        var context = database.CreateContext();
        var lead = database.SeedLead(context, kyc: KycStatuses.Verified);
        var loans = database.CreateLoans(context);

        // One approved, one rejected, one still a draft.
        var approved = loans.Create(new CreateLoanRequest(lead.Id, "WorkingCapital", 10_000m, 12, 5m));
        loans.Move(approved.Id, new MoveLoanRequest(LoanStatuses.Submitted, null));
        loans.Move(approved.Id, new MoveLoanRequest(LoanStatuses.UnderReview, null));
        loans.Move(approved.Id, new MoveLoanRequest(LoanStatuses.Approved, null));

        var rejected = loans.Create(new CreateLoanRequest(lead.Id, "PersonalLoan", 5_000m, 12, 5m));
        loans.Move(rejected.Id, new MoveLoanRequest(LoanStatuses.Submitted, null));
        loans.Move(rejected.Id, new MoveLoanRequest(LoanStatuses.Rejected, "No"));

        loans.Create(new CreateLoanRequest(lead.Id, "TradeFinance", 1_000m, 12, 5m));

        var report = database.CreateInsights(context).GetOperationsReport();

        Assert.Equal(3, report.TotalLoans);
        Assert.Equal(50m, report.ApprovalRate); // one of two decided
        Assert.Equal(10_000m, report.TotalApproved);
        Assert.Equal(16_000m, report.TotalRequested);
    }
}
