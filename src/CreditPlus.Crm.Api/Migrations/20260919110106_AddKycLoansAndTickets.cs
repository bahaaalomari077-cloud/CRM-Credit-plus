using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CreditPlus.Crm.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddKycLoansAndTickets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "KycStatus",
                table: "leads",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "NotStarted");

            migrationBuilder.AddColumn<string>(
                name: "RiskRating",
                table: "leads",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Medium");

            migrationBuilder.CreateTable(
                name: "kyc_documents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LeadId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Reference = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IssuedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    ExpiresOn = table.Column<DateOnly>(type: "date", nullable: true),
                    Notes = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: true),
                    VerifiedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    VerifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_kyc_documents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_kyc_documents_leads_LeadId",
                        column: x => x.LeadId,
                        principalTable: "leads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "loan_applications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Reference = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    LeadId = table.Column<Guid>(type: "uuid", nullable: false),
                    Product = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TermMonths = table.Column<int>(type: "integer", nullable: false),
                    InterestRate = table.Column<decimal>(type: "numeric(6,3)", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Decision = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    DecisionNotes = table.Column<string>(type: "character varying(800)", maxLength: 800, nullable: true),
                    Owner = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DecidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_loan_applications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_loan_applications_leads_LeadId",
                        column: x => x.LeadId,
                        principalTable: "leads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tickets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Reference = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    LeadId = table.Column<Guid>(type: "uuid", nullable: true),
                    CustomerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Subject = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Category = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Priority = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AssignedTo = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Resolution = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DueAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tickets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tickets_leads_LeadId",
                        column: x => x.LeadId,
                        principalTable: "leads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_leads_KycStatus",
                table: "leads",
                column: "KycStatus");

            migrationBuilder.CreateIndex(
                name: "IX_kyc_documents_LeadId",
                table: "kyc_documents",
                column: "LeadId");

            migrationBuilder.CreateIndex(
                name: "IX_loan_applications_LeadId",
                table: "loan_applications",
                column: "LeadId");

            migrationBuilder.CreateIndex(
                name: "IX_loan_applications_Reference",
                table: "loan_applications",
                column: "Reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_loan_applications_Status",
                table: "loan_applications",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_tickets_LeadId",
                table: "tickets",
                column: "LeadId");

            migrationBuilder.CreateIndex(
                name: "IX_tickets_Reference",
                table: "tickets",
                column: "Reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tickets_Status",
                table: "tickets",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "kyc_documents");

            migrationBuilder.DropTable(
                name: "loan_applications");

            migrationBuilder.DropTable(
                name: "tickets");

            migrationBuilder.DropIndex(
                name: "IX_leads_KycStatus",
                table: "leads");

            migrationBuilder.DropColumn(
                name: "KycStatus",
                table: "leads");

            migrationBuilder.DropColumn(
                name: "RiskRating",
                table: "leads");
        }
    }
}
