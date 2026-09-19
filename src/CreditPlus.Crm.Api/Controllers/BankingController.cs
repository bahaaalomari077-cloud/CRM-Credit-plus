using CreditPlus.Crm.Api.Application;
using CreditPlus.Crm.Api.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CreditPlus.Crm.Api.Controllers;

[ApiController]
[Route("api/crm/customers")]
[Produces("application/json")]
[Authorize]
public sealed class CustomersController(ICustomerService customers) : ControllerBase
{
    /// <summary>Everything held about one customer, for the 360 screen.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<CustomerOverview>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<CustomerOverview> Get(Guid id) =>
        customers.GetOverview(id) is { } overview ? Ok(overview) : NotFound();

    [HttpGet("{id:guid}/score")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetScore(Guid id)
    {
        var (score, band, reasons) = customers.Score(id);
        return Ok(new { score, band, reasons });
    }
}

[ApiController]
[Route("api/crm/kyc")]
[Produces("application/json")]
[Authorize]
public sealed class KycController(IKycService kyc) : ControllerBase
{
    [HttpGet("documents")]
    [ProducesResponseType<IReadOnlyList<KycDocument>>(StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<KycDocument>> GetDocuments(
        [FromQuery] Guid? leadId,
        [FromQuery] string? status) =>
        Ok(kyc.GetDocuments(leadId, status));

    [HttpGet("types")]
    [ProducesResponseType<IReadOnlyList<string>>(StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<string>> GetTypes() => Ok(KycDocumentTypes.All);

    [HttpGet("{leadId:guid}")]
    [ProducesResponseType<KycSummary>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<KycSummary> GetSummary(Guid leadId) => Ok(kyc.GetSummary(leadId));

    [HttpPost("documents")]
    [Authorize(Policy = Roles.CanWrite)]
    [ProducesResponseType<KycDocument>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<KycDocument> AddDocument(CreateKycDocumentRequest request)
    {
        var document = kyc.AddDocument(request);
        return Created($"api/crm/kyc/documents/{document.Id}", document);
    }

    /// <summary>Verifying or rejecting a document is a compliance decision, so it needs a Manager.</summary>
    [HttpPatch("documents/{id:guid}/verify")]
    [Authorize(Policy = Roles.CanReadAudit)]
    [ProducesResponseType<KycDocument>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<KycDocument> Verify(Guid id, VerifyKycDocumentRequest request) =>
        Ok(kyc.Verify(id, request));

    [HttpDelete("documents/{id:guid}")]
    [Authorize(Policy = Roles.CanWrite)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult DeleteDocument(Guid id) => kyc.DeleteDocument(id) ? NoContent() : NotFound();
}

[ApiController]
[Route("api/crm/loans")]
[Produces("application/json")]
[Authorize]
public sealed class LoansController(ILoanService loans) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<LoanApplication>>(StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<LoanApplication>> Get(
        [FromQuery] string? status,
        [FromQuery] Guid? leadId,
        [FromQuery] string? search) =>
        Ok(loans.GetLoans(status, leadId, search));

    [HttpGet("products")]
    [ProducesResponseType<IReadOnlyList<string>>(StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<string>> GetProducts() => Ok(LoanProducts.All);

    [HttpGet("statuses")]
    [ProducesResponseType<IReadOnlyList<string>>(StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<string>> GetStatuses() => Ok(LoanStatuses.All);

    [HttpGet("{id:guid}")]
    [ProducesResponseType<LoanApplication>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<LoanApplication> GetById(Guid id) =>
        loans.FindLoan(id) is { } loan ? Ok(loan) : NotFound();

    [HttpPost]
    [Authorize(Policy = Roles.CanWrite)]
    [ProducesResponseType<LoanApplication>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<LoanApplication> Create(CreateLoanRequest request)
    {
        var loan = loans.Create(request);
        return CreatedAtAction(nameof(GetById), new { id = loan.Id }, loan);
    }

    /// <summary>Approving or rejecting an application is a credit decision, so it needs a Manager.</summary>
    [HttpPatch("{id:guid}/status")]
    [Authorize(Policy = Roles.CanReadAudit)]
    [ProducesResponseType<LoanApplication>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<LoanApplication> Move(Guid id, MoveLoanRequest request) => Ok(loans.Move(id, request));

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Roles.CanWrite)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult Delete(Guid id) => loans.Delete(id) ? NoContent() : NotFound();
}

[ApiController]
[Route("api/crm/tickets")]
[Produces("application/json")]
[Authorize]
public sealed class TicketsController(ITicketService tickets) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<Ticket>>(StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<Ticket>> Get(
        [FromQuery] string? status,
        [FromQuery] string? priority,
        [FromQuery] Guid? leadId,
        [FromQuery] string? search) =>
        Ok(tickets.GetTickets(status, priority, leadId, search));

    [HttpGet("categories")]
    [ProducesResponseType<IReadOnlyList<string>>(StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<string>> GetCategories() => Ok(TicketCategories.All);

    [HttpGet("{id:guid}")]
    [ProducesResponseType<Ticket>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<Ticket> GetById(Guid id) =>
        tickets.FindTicket(id) is { } ticket ? Ok(ticket) : NotFound();

    [HttpPost]
    [Authorize(Policy = Roles.CanWrite)]
    [ProducesResponseType<Ticket>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<Ticket> Create(CreateTicketRequest request)
    {
        var ticket = tickets.Create(request);
        return CreatedAtAction(nameof(GetById), new { id = ticket.Id }, ticket);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Roles.CanWrite)]
    [ProducesResponseType<Ticket>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<Ticket> Update(Guid id, UpdateTicketRequest request) => Ok(tickets.Update(id, request));

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Roles.CanWrite)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult Delete(Guid id) => tickets.Delete(id) ? NoContent() : NotFound();
}
