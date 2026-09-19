using CreditPlus.Crm.Api.Domain;

namespace CreditPlus.Crm.Api.Application;

public interface IKycService
{
    KycSummary GetSummary(Guid leadId);
    IReadOnlyList<KycDocument> GetDocuments(Guid? leadId, string? status);
    KycDocument AddDocument(CreateKycDocumentRequest request);
    KycDocument Verify(Guid documentId, VerifyKycDocumentRequest request);
    bool DeleteDocument(Guid documentId);
}

public interface ILoanService
{
    IReadOnlyList<LoanApplication> GetLoans(string? status, Guid? leadId, string? search);
    LoanApplication? FindLoan(Guid id);
    LoanApplication Create(CreateLoanRequest request);
    LoanApplication Move(Guid id, MoveLoanRequest request);
    bool Delete(Guid id);
}

public interface ITicketService
{
    IReadOnlyList<Ticket> GetTickets(string? status, string? priority, Guid? leadId, string? search);
    Ticket? FindTicket(Guid id);
    Ticket Create(CreateTicketRequest request);
    Ticket Update(Guid id, UpdateTicketRequest request);
    bool Delete(Guid id);
}

public interface ICustomerService
{
    /// <summary>Assembles every record tied to one customer for the 360 screen.</summary>
    CustomerOverview? GetOverview(Guid leadId);

    /// <summary>Scores a lead from stored signals; returns the score and why.</summary>
    (int Score, string Band, IReadOnlyList<string> Reasons) Score(Guid leadId);
}

public interface IInsightService
{
    /// <summary>Conditions needing attention, derived fresh from stored data.</summary>
    IReadOnlyList<Alert> GetAlerts(int limit = 50);

    OperationsReport GetOperationsReport();
}

public interface IExportService
{
    /// <summary>Renders a dataset as CSV. Throws CrmValidationException for an unknown name.</summary>
    (string FileName, string Content) Export(string dataset);
}
