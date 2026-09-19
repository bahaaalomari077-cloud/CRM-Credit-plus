using CreditPlus.Crm.Api.Application;
using CreditPlus.Crm.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace CreditPlus.Crm.Api.Infrastructure;

public sealed class LoanService(CrmDbContext db, ICurrentUser currentUser, IAuditLog audit) : ILoanService
{
    public IReadOnlyList<LoanApplication> GetLoans(string? status, Guid? leadId, string? search)
    {
        var query =
            from loan in db.LoanApplications.AsNoTracking()
            join lead in db.Leads.AsNoTracking() on loan.LeadId equals lead.Id
            select new { loan, lead.FullName, lead.Company };

        if (!string.IsNullOrWhiteSpace(status))
        {
            var wanted = LoanStatuses.Normalize(status);
            query = query.Where(row => row.loan.Status == wanted);
        }

        if (leadId.HasValue)
        {
            query = query.Where(row => row.loan.LeadId == leadId.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = $"%{search.Trim()}%";
            query = query.Where(row =>
                EF.Functions.ILike(row.loan.Reference, term) ||
                EF.Functions.ILike(row.FullName, term) ||
                EF.Functions.ILike(row.Company, term));
        }

        return query
            .OrderByDescending(row => row.loan.CreatedAt)
            .ToList()
            .Select(row => row.loan.ToRecord(row.FullName, row.Company))
            .ToList();
    }

    public LoanApplication? FindLoan(Guid id) =>
        (from loan in db.LoanApplications.AsNoTracking()
         join lead in db.Leads.AsNoTracking() on loan.LeadId equals lead.Id
         where loan.Id == id
         select new { loan, lead.FullName, lead.Company })
        .ToList()
        .Select(row => row.loan.ToRecord(row.FullName, row.Company))
        .FirstOrDefault();

    public LoanApplication Create(CreateLoanRequest request)
    {
        var lead = db.Leads.AsNoTracking().FirstOrDefault(candidate => candidate.Id == request.LeadId)
            ?? throw new KeyNotFoundException($"Lead {request.LeadId} was not found.");

        var entity = new LoanApplicationEntity
        {
            Id = Guid.NewGuid(),
            Reference = NextReference("LN"),
            LeadId = lead.Id,
            Product = ValidProduct(request.Product),
            Amount = PositiveAmount(request.Amount),
            TermMonths = ValidTerm(request.TermMonths),
            InterestRate = ValidRate(request.InterestRate),
            Status = LoanStatuses.Draft,
            Owner = currentUser.IsAuthenticated ? currentUser.Name : "System",
            CreatedAt = DateTimeOffset.UtcNow,
        };

        db.LoanApplications.Add(entity);
        db.SaveChanges();

        audit.Record(AuditActions.Created, "Loan", entity.Id.ToString(),
            $"Created {entity.Reference} for {lead.FullName}",
            new { entity.Reference, entity.Product, entity.Amount, entity.TermMonths, entity.InterestRate });

        return entity.ToRecord(lead.FullName, lead.Company);
    }

    public LoanApplication Move(Guid id, MoveLoanRequest request)
    {
        var entity = db.LoanApplications.FirstOrDefault(loan => loan.Id == id)
            ?? throw new KeyNotFoundException($"Loan {id} was not found.");

        var target = ValidStatus(request.Status);

        if (!LoanStatuses.CanMove(entity.Status, target))
        {
            var allowed = LoanStatuses.Transitions[entity.Status];
            throw new CrmValidationException(allowed.Length == 0
                ? $"A {entity.Status} application cannot change status."
                : $"A {entity.Status} application can only move to: {string.Join(", ", allowed)}.");
        }

        var lead = db.Leads.AsNoTracking().FirstOrDefault(candidate => candidate.Id == entity.LeadId)
            ?? throw new KeyNotFoundException($"Lead {entity.LeadId} was not found.");

        // The compliance gate: money cannot be approved against unverified identity.
        if (target == LoanStatuses.Approved && lead.KycStatus != KycStatuses.Verified)
        {
            throw new CrmValidationException(
                $"KYC must be verified before approval. This customer is currently {lead.KycStatus}.");
        }

        var before = entity.Status;
        entity.Status = target;
        entity.DecisionNotes = Text(request.Notes, entity.DecisionNotes ?? "");

        if (target is LoanStatuses.Approved or LoanStatuses.Rejected)
        {
            entity.Decision = target;
            entity.DecidedAt = DateTimeOffset.UtcNow;
        }

        db.SaveChanges();

        audit.Record(AuditActions.StageChanged, "Loan", entity.Id.ToString(),
            $"{entity.Reference}: {before} to {target}",
            new { from = before, to = target, entity.Amount, notes = entity.DecisionNotes });

        return entity.ToRecord(lead.FullName, lead.Company);
    }

    public bool Delete(Guid id)
    {
        var entity = db.LoanApplications.FirstOrDefault(loan => loan.Id == id);
        if (entity is null)
        {
            return false;
        }

        if (entity.Status is LoanStatuses.Approved or LoanStatuses.Disbursed)
        {
            throw new CrmValidationException("An approved or disbursed application cannot be deleted.");
        }

        db.LoanApplications.Remove(entity);
        db.SaveChanges();

        audit.Record(AuditActions.Deleted, "Loan", id.ToString(), $"Deleted {entity.Reference}");
        return true;
    }

    /// <summary>Sequential, human-readable reference such as LN-2026-0007.</summary>
    private string NextReference(string prefix)
    {
        var year = DateTime.UtcNow.Year;
        var stem = $"{prefix}-{year}-";
        var last = db.LoanApplications
            .Where(loan => loan.Reference.StartsWith(stem))
            .OrderByDescending(loan => loan.Reference)
            .Select(loan => loan.Reference)
            .FirstOrDefault();

        var next = 1;
        if (last is not null && int.TryParse(last[stem.Length..], out var parsed))
        {
            next = parsed + 1;
        }

        return $"{stem}{next:D4}";
    }

    private static string Text(string? value, string fallback = "") =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private static decimal PositiveAmount(decimal value) =>
        value > 0 ? value : throw new CrmValidationException("Loan amount must be greater than zero.");

    private static int ValidTerm(int months) =>
        months is > 0 and <= 480
            ? months
            : throw new CrmValidationException("Term must be between 1 and 480 months.");

    private static decimal ValidRate(decimal rate) =>
        rate is >= 0 and <= 100
            ? rate
            : throw new CrmValidationException("Interest rate must be between 0 and 100.");

    private static string ValidProduct(string? value)
    {
        var product = Text(value);
        return LoanProducts.All.Contains(product, StringComparer.OrdinalIgnoreCase)
            ? LoanProducts.All.First(known => known.Equals(product, StringComparison.OrdinalIgnoreCase))
            : throw new CrmValidationException($"Product must be one of: {string.Join(", ", LoanProducts.All)}.");
    }

    private static string ValidStatus(string? value)
    {
        var status = Text(value);
        return LoanStatuses.IsKnown(status)
            ? LoanStatuses.Normalize(status)
            : throw new CrmValidationException($"Status must be one of: {string.Join(", ", LoanStatuses.All)}.");
    }
}
