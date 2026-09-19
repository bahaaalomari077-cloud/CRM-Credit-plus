using CreditPlus.Crm.Api.Application;
using CreditPlus.Crm.Api.Domain;
using CreditPlus.Crm.Api.Infrastructure;

namespace CreditPlus.Crm.Tests;

/// <summary>KYC rules: what counts as verified, and when a document lapses.</summary>
public sealed class KycTests : IClassFixture<TestDatabase>
{
    private readonly TestDatabase database;

    public KycTests(TestDatabase database) => this.database = database;

    private (KycService Kyc, CrmDbContext Context, Guid LeadId) Arrange()
    {
        var context = database.CreateContext();
        var lead = database.SeedLead(context);
        return (database.CreateKyc(context), context, lead.Id);
    }

    [Fact]
    public void A_customer_with_no_documents_has_not_started()
    {
        var (kyc, _, leadId) = Arrange();

        var summary = kyc.GetSummary(leadId);

        Assert.Equal(KycStatuses.NotStarted, summary.Status);
        Assert.Equal(2, summary.Missing);
    }

    [Fact]
    public void Verifying_only_some_required_documents_leaves_it_pending()
    {
        var (kyc, _, leadId) = Arrange();
        var document = kyc.AddDocument(new CreateKycDocumentRequest(
            leadId, "NationalId", "123", null, null, null));

        kyc.Verify(document.Id, new VerifyKycDocumentRequest(KycStatuses.Verified, null));

        // Proof of address is still missing, so the customer is not yet verified.
        Assert.Equal(KycStatuses.Pending, kyc.GetSummary(leadId).Status);
    }

    [Fact]
    public void Verifying_every_required_document_verifies_the_customer()
    {
        var (kyc, context, leadId) = Arrange();

        foreach (var type in new[] { "NationalId", "ProofOfAddress" })
        {
            var document = kyc.AddDocument(new CreateKycDocumentRequest(leadId, type, "ref", null, null, null));
            kyc.Verify(document.Id, new VerifyKycDocumentRequest(KycStatuses.Verified, null));
        }

        Assert.Equal(KycStatuses.Verified, kyc.GetSummary(leadId).Status);

        // The rolled-up status is written back onto the lead, not just computed.
        Assert.Equal(KycStatuses.Verified, context.Leads.Single(lead => lead.Id == leadId).KycStatus);
    }

    [Fact]
    public void A_verified_document_past_its_expiry_reports_as_expired()
    {
        var (kyc, _, leadId) = Arrange();
        var yesterday = DateOnly.FromDateTime(DateTime.Today.AddDays(-1));

        var document = kyc.AddDocument(new CreateKycDocumentRequest(
            leadId, "NationalId", "123", DateOnly.FromDateTime(DateTime.Today.AddYears(-5)), yesterday, null));
        kyc.Verify(document.Id, new VerifyKycDocumentRequest(KycStatuses.Verified, null));

        var summary = kyc.GetSummary(leadId);

        Assert.Equal(KycStatuses.Expired, summary.Status);
        Assert.Equal(1, summary.Expired);
        Assert.Equal(0, summary.Verified);
    }

    [Fact]
    public void A_rejected_document_makes_the_customer_rejected()
    {
        var (kyc, _, leadId) = Arrange();
        var document = kyc.AddDocument(new CreateKycDocumentRequest(leadId, "Passport", "P1", null, null, null));

        kyc.Verify(document.Id, new VerifyKycDocumentRequest(KycStatuses.Rejected, "Illegible"));

        Assert.Equal(KycStatuses.Rejected, kyc.GetSummary(leadId).Status);
    }

    [Fact]
    public void The_same_document_type_cannot_be_filed_twice()
    {
        var (kyc, _, leadId) = Arrange();
        kyc.AddDocument(new CreateKycDocumentRequest(leadId, "NationalId", "A", null, null, null));

        Assert.Throws<CrmValidationException>(
            () => kyc.AddDocument(new CreateKycDocumentRequest(leadId, "NationalId", "B", null, null, null)));
    }

    [Fact]
    public void An_expiry_before_the_issue_date_is_refused()
    {
        var (kyc, _, leadId) = Arrange();
        var today = DateOnly.FromDateTime(DateTime.Today);

        Assert.Throws<CrmValidationException>(() => kyc.AddDocument(new CreateKycDocumentRequest(
            leadId, "NationalId", "A", today, today.AddDays(-1), null)));
    }

    [Fact]
    public void An_unknown_document_type_is_refused()
    {
        var (kyc, _, leadId) = Arrange();

        Assert.Throws<CrmValidationException>(
            () => kyc.AddDocument(new CreateKycDocumentRequest(leadId, "SelfiePhoto", "A", null, null, null)));
    }
}

/// <summary>Ticket targets and the resolution rule.</summary>
public sealed class TicketTests : IClassFixture<TestDatabase>
{
    private readonly TestDatabase database;

    public TicketTests(TestDatabase database) => this.database = database;

    private TicketService Arrange() => database.CreateTickets(database.CreateContext());

    [Theory]
    [InlineData(TicketPriorities.Urgent, 4)]
    [InlineData(TicketPriorities.High, 8)]
    [InlineData(TicketPriorities.Normal, 24)]
    [InlineData(TicketPriorities.Low, 72)]
    public void Each_priority_sets_its_own_response_target(string priority, int hours)
    {
        var tickets = Arrange();

        var ticket = tickets.Create(new CreateTicketRequest(
            null, "Walk-in", "Subject", "Details", "Inquiry", priority));

        var window = (ticket.DueAt - ticket.CreatedAt).TotalHours;
        Assert.Equal(hours, window, precision: 1);
    }

    [Fact]
    public void Resolving_without_a_note_is_refused()
    {
        var tickets = Arrange();
        var ticket = tickets.Create(new CreateTicketRequest(null, "Walk-in", "S", "D", "Inquiry", null));

        Assert.Throws<CrmValidationException>(() => tickets.Update(ticket.Id, new UpdateTicketRequest(
            "S", "D", "Inquiry", TicketPriorities.Normal, TicketStatuses.Resolved, null, null)));
    }

    [Fact]
    public void Resolving_with_a_note_succeeds_and_stamps_the_time()
    {
        var tickets = Arrange();
        var ticket = tickets.Create(new CreateTicketRequest(null, "Walk-in", "S", "D", "Inquiry", null));

        var resolved = tickets.Update(ticket.Id, new UpdateTicketRequest(
            "S", "D", "Inquiry", TicketPriorities.Normal, TicketStatuses.Resolved, null, "Explained the fee"));

        Assert.Equal(TicketStatuses.Resolved, resolved.Status);
        Assert.NotNull(resolved.ResolvedAt);
        Assert.False(resolved.IsBreached);
    }

    [Fact]
    public void Reopening_clears_the_resolution_timestamp()
    {
        var tickets = Arrange();
        var ticket = tickets.Create(new CreateTicketRequest(null, "Walk-in", "S", "D", "Inquiry", null));

        tickets.Update(ticket.Id, new UpdateTicketRequest(
            "S", "D", "Inquiry", TicketPriorities.Normal, TicketStatuses.Resolved, null, "Done"));
        var reopened = tickets.Update(ticket.Id, new UpdateTicketRequest(
            "S", "D", "Inquiry", TicketPriorities.Normal, TicketStatuses.Open, null, "Done"));

        Assert.Null(reopened.ResolvedAt);
    }

    [Fact]
    public void Raising_the_priority_pulls_the_target_in()
    {
        var tickets = Arrange();
        var ticket = tickets.Create(new CreateTicketRequest(
            null, "Walk-in", "S", "D", "Inquiry", TicketPriorities.Low));

        var raised = tickets.Update(ticket.Id, new UpdateTicketRequest(
            "S", "D", "Inquiry", TicketPriorities.Urgent, TicketStatuses.Open, null, null));

        // Measured from when the ticket was raised, not from now.
        Assert.Equal(4, (raised.DueAt - raised.CreatedAt).TotalHours, precision: 1);
    }

    [Fact]
    public void A_ticket_with_no_customer_and_no_name_is_refused()
    {
        var tickets = Arrange();

        Assert.Throws<CrmValidationException>(
            () => tickets.Create(new CreateTicketRequest(null, "", "S", "D", "Inquiry", null)));
    }
}
