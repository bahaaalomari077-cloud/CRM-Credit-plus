using CreditPlus.Crm.Api.Application;
using CreditPlus.Crm.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace CreditPlus.Crm.Api.Infrastructure;

/// <summary>
/// Tracks the documents held for each customer and rolls them up into one KYC
/// position. The rolled-up status is written back onto the lead so lists and
/// filters do not need to recompute it.
/// </summary>
public sealed class KycService(CrmDbContext db, ICurrentUser currentUser, IAuditLog audit) : IKycService
{
    /// <summary>Documents every customer must hold before KYC counts as complete.</summary>
    private static readonly string[] RequiredDocuments = ["NationalId", "ProofOfAddress"];

    public KycSummary GetSummary(Guid leadId)
    {
        var lead = db.Leads.AsNoTracking().FirstOrDefault(candidate => candidate.Id == leadId)
            ?? throw new KeyNotFoundException($"Lead {leadId} was not found.");

        var documents = db.KycDocuments
            .AsNoTracking()
            .Where(document => document.LeadId == leadId)
            .OrderBy(document => document.DocumentType)
            .ToList();

        return Summarise(lead, documents);
    }

    public IReadOnlyList<KycDocument> GetDocuments(Guid? leadId, string? status)
    {
        var query = db.KycDocuments.AsNoTracking();

        if (leadId.HasValue)
        {
            query = query.Where(document => document.LeadId == leadId.Value);
        }

        var documents = query.OrderByDescending(document => document.CreatedAt).ToList();

        // Expiry is derived, so filtering on it happens after the query.
        if (!string.IsNullOrWhiteSpace(status))
        {
            var wanted = KycStatuses.Normalize(status);
            documents = documents.Where(document => document.EffectiveStatus == wanted).ToList();
        }

        return documents.Select(document => document.ToRecord()).ToList();
    }

    public KycDocument AddDocument(CreateKycDocumentRequest request)
    {
        var lead = db.Leads.FirstOrDefault(candidate => candidate.Id == request.LeadId)
            ?? throw new KeyNotFoundException($"Lead {request.LeadId} was not found.");

        var type = ValidDocumentType(request.DocumentType);

        if (db.KycDocuments.Any(document => document.LeadId == lead.Id && document.DocumentType == type))
        {
            throw new CrmValidationException($"A {type} document is already on file for this customer.");
        }

        if (request.ExpiresOn is { } expiry && request.IssuedOn is { } issued && expiry <= issued)
        {
            throw new CrmValidationException("Expiry date must be after the issue date.");
        }

        var entity = new KycDocumentEntity
        {
            Id = Guid.NewGuid(),
            LeadId = lead.Id,
            DocumentType = type,
            Reference = Text(request.Reference),
            Status = KycStatuses.Pending,
            IssuedOn = request.IssuedOn,
            ExpiresOn = request.ExpiresOn,
            Notes = Text(request.Notes),
            CreatedAt = DateTimeOffset.UtcNow,
        };

        db.KycDocuments.Add(entity);
        db.SaveChanges();
        Recalculate(lead.Id);

        audit.Record(AuditActions.Created, "KycDocument", entity.Id.ToString(),
            $"Added {type} for {lead.FullName}",
            new { entity.DocumentType, entity.Reference, entity.ExpiresOn });

        return entity.ToRecord();
    }

    public KycDocument Verify(Guid documentId, VerifyKycDocumentRequest request)
    {
        var entity = db.KycDocuments.FirstOrDefault(document => document.Id == documentId)
            ?? throw new KeyNotFoundException($"Document {documentId} was not found.");

        var status = ValidStatus(request.Status);
        if (status is KycStatuses.NotStarted)
        {
            throw new CrmValidationException("A document cannot be set back to NotStarted.");
        }

        var before = entity.Status;
        entity.Status = status;
        entity.Notes = Text(request.Notes, entity.Notes ?? "");
        entity.VerifiedBy = currentUser.Name;
        entity.VerifiedAt = DateTimeOffset.UtcNow;

        db.SaveChanges();
        Recalculate(entity.LeadId);

        audit.Record(AuditActions.Updated, "KycDocument", entity.Id.ToString(),
            $"{entity.DocumentType}: {before} to {status}",
            new { from = before, to = status, notes = entity.Notes });

        return entity.ToRecord();
    }

    public bool DeleteDocument(Guid documentId)
    {
        var entity = db.KycDocuments.FirstOrDefault(document => document.Id == documentId);
        if (entity is null)
        {
            return false;
        }

        var leadId = entity.LeadId;
        db.KycDocuments.Remove(entity);
        db.SaveChanges();
        Recalculate(leadId);

        audit.Record(AuditActions.Deleted, "KycDocument", documentId.ToString(),
            $"Removed {entity.DocumentType}");
        return true;
    }

    /// <summary>
    /// Re-derives the lead's KYC status from its documents. Verified requires every
    /// required document verified and none expired.
    /// </summary>
    private void Recalculate(Guid leadId)
    {
        var lead = db.Leads.FirstOrDefault(candidate => candidate.Id == leadId);
        if (lead is null)
        {
            return;
        }

        var documents = db.KycDocuments.Where(document => document.LeadId == leadId).ToList();
        lead.KycStatus = Derive(documents);
        db.SaveChanges();
    }

    private static string Derive(List<KycDocumentEntity> documents)
    {
        if (documents.Count == 0)
        {
            return KycStatuses.NotStarted;
        }

        var statuses = documents.ToDictionary(document => document.DocumentType, document => document.EffectiveStatus);

        if (documents.Any(document => document.EffectiveStatus == KycStatuses.Expired))
        {
            return KycStatuses.Expired;
        }

        if (documents.Any(document => document.EffectiveStatus == KycStatuses.Rejected))
        {
            return KycStatuses.Rejected;
        }

        var allRequiredVerified = RequiredDocuments.All(required =>
            statuses.TryGetValue(required, out var status) && status == KycStatuses.Verified);

        return allRequiredVerified ? KycStatuses.Verified : KycStatuses.Pending;
    }

    private static KycSummary Summarise(LeadEntity lead, List<KycDocumentEntity> documents)
    {
        var missing = RequiredDocuments.Count(required =>
            !documents.Any(document => document.DocumentType == required));

        return new KycSummary(
            lead.Id,
            Derive(documents),
            lead.RiskRating,
            documents.Count(document => document.EffectiveStatus == KycStatuses.Verified),
            documents.Count(document => document.EffectiveStatus == KycStatuses.Pending),
            documents.Count(document => document.EffectiveStatus == KycStatuses.Expired),
            missing,
            documents.Select(document => document.ToRecord()).ToList());
    }

    private static string Text(string? value, string fallback = "") =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private static string ValidDocumentType(string? value)
    {
        var type = Text(value);
        return KycDocumentTypes.All.Contains(type, StringComparer.OrdinalIgnoreCase)
            ? KycDocumentTypes.All.First(known => known.Equals(type, StringComparison.OrdinalIgnoreCase))
            : throw new CrmValidationException(
                $"Document type must be one of: {string.Join(", ", KycDocumentTypes.All)}.");
    }

    private static string ValidStatus(string? value)
    {
        var status = Text(value);
        return KycStatuses.IsKnown(status)
            ? KycStatuses.Normalize(status)
            : throw new CrmValidationException($"Status must be one of: {string.Join(", ", KycStatuses.All)}.");
    }
}
