using System.Globalization;
using System.Text;
using CreditPlus.Crm.Api.Application;
using CreditPlus.Crm.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace CreditPlus.Crm.Api.Infrastructure;

/// <summary>
/// Renders datasets as CSV for the regulator-facing exports a lender is usually
/// asked for. Every export is recorded in the audit trail, since who took a copy
/// of customer data is itself a compliance question.
/// </summary>
public sealed class ExportService(CrmDbContext db, IAuditLog audit) : IExportService
{
    public static readonly IReadOnlyList<string> Datasets =
        ["leads", "deals", "loans", "tickets", "kyc", "audit"];

    public (string FileName, string Content) Export(string dataset)
    {
        var name = (dataset ?? "").Trim().ToLowerInvariant();

        var (rows, header) = name switch
        {
            "leads" => (Leads(), "Name,Company,Email,Phone,Source,Status,KycStatus,RiskRating,Owner,CreatedAt"),
            "deals" => (Deals(), "Name,Company,Contact,Value,Stage,Probability,ExpectedCloseDate,Owner,CreatedAt"),
            "loans" => (Loans(), "Reference,Customer,Company,Product,Amount,TermMonths,InterestRate,MonthlyPayment,Status,Decision,Owner,CreatedAt,DecidedAt"),
            "tickets" => (Tickets(), "Reference,Customer,Subject,Category,Priority,Status,AssignedTo,CreatedAt,DueAt,ResolvedAt,Breached"),
            "kyc" => (Kyc(), "Customer,Company,DocumentType,Reference,Status,IssuedOn,ExpiresOn,VerifiedBy,VerifiedAt"),
            "audit" => (Audit(), "OccurredAt,User,Action,EntityType,EntityId,Summary,IpAddress"),
            _ => throw new CrmValidationException(
                $"Unknown dataset. Choose one of: {string.Join(", ", Datasets)}."),
        };

        var builder = new StringBuilder();
        builder.AppendLine(header);
        foreach (var row in rows)
        {
            builder.AppendLine(row);
        }

        audit.Record(AuditActions.Exported, "Export", name,
            $"Exported {name} ({rows.Count} rows)", new { dataset = name, rows = rows.Count });

        var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture);
        return ($"{name}-{stamp}.csv", builder.ToString());
    }

    private List<string> Leads() =>
        db.Leads.AsNoTracking()
            .OrderBy(lead => lead.FullName)
            .ToList()
            .Select(lead => Row(
                lead.FullName, lead.Company, lead.Email, lead.Phone, lead.Source,
                lead.Status, lead.KycStatus, lead.RiskRating, lead.Owner, Stamp(lead.CreatedAt)))
            .ToList();

    private List<string> Deals() =>
        db.Deals.AsNoTracking()
            .OrderByDescending(deal => deal.CreatedAt)
            .ToList()
            .Select(deal => Row(
                deal.Name, deal.Company, deal.Contact, Money(deal.Value), deal.Stage,
                deal.Probability.ToString(CultureInfo.InvariantCulture),
                deal.ExpectedCloseDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                deal.Owner, Stamp(deal.CreatedAt)))
            .ToList();

    private List<string> Loans() =>
        (from loan in db.LoanApplications.AsNoTracking()
         join lead in db.Leads.AsNoTracking() on loan.LeadId equals lead.Id
         orderby loan.Reference
         select new { loan, lead.FullName, lead.Company })
        .ToList()
        .Select(row => Row(
            row.loan.Reference, row.FullName, row.Company, row.loan.Product,
            Money(row.loan.Amount), row.loan.TermMonths.ToString(CultureInfo.InvariantCulture),
            Money(row.loan.InterestRate), Money(row.loan.MonthlyPayment),
            row.loan.Status, row.loan.Decision ?? "", row.loan.Owner,
            Stamp(row.loan.CreatedAt), Stamp(row.loan.DecidedAt)))
        .ToList();

    private List<string> Tickets() =>
        db.Tickets.AsNoTracking()
            .OrderBy(ticket => ticket.Reference)
            .ToList()
            .Select(ticket => Row(
                ticket.Reference, ticket.CustomerName, ticket.Subject, ticket.Category,
                ticket.Priority, ticket.Status, ticket.AssignedTo,
                Stamp(ticket.CreatedAt), Stamp(ticket.DueAt), Stamp(ticket.ResolvedAt),
                ticket.IsBreached ? "Yes" : "No"))
            .ToList();

    private List<string> Kyc() =>
        (from document in db.KycDocuments.AsNoTracking()
         join lead in db.Leads.AsNoTracking() on document.LeadId equals lead.Id
         orderby lead.FullName, document.DocumentType
         select new { document, lead.FullName, lead.Company })
        .ToList()
        .Select(row => Row(
            row.FullName, row.Company, row.document.DocumentType, row.document.Reference,
            row.document.EffectiveStatus,
            row.document.IssuedOn?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "",
            row.document.ExpiresOn?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "",
            row.document.VerifiedBy, Stamp(row.document.VerifiedAt)))
        .ToList();

    private List<string> Audit() =>
        db.AuditEntries.AsNoTracking()
            .OrderByDescending(entry => entry.OccurredAt)
            .Take(5000)
            .ToList()
            .Select(entry => Row(
                Stamp(entry.OccurredAt), entry.UserEmail, entry.Action, entry.EntityType,
                entry.EntityId, entry.Summary ?? "", entry.IpAddress ?? ""))
            .ToList();

    private static string Row(params string[] fields) => string.Join(",", fields.Select(Escape));

    /// <summary>
    /// RFC 4180 quoting. A leading =, +, - or @ is also prefixed with a quote so a
    /// spreadsheet treats the value as text rather than a formula.
    /// </summary>
    private static string Escape(string? value)
    {
        var text = value ?? "";

        if (text.Length > 0 && (text[0] is '=' or '+' or '-' or '@'))
        {
            text = "'" + text;
        }

        return text.Contains(',') || text.Contains('"') || text.Contains('\n') || text.Contains('\r')
            ? $"\"{text.Replace("\"", "\"\"")}\""
            : text;
    }

    private static string Money(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    private static string Stamp(DateTimeOffset? value) =>
        value?.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) ?? "";
}
