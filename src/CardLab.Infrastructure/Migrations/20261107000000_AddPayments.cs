using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CardLab.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BankAccount",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LastFour = table.Column<string>(type: "char(4)", unicode: false, fixedLength: true, maxLength: 4, nullable: false),
                    AvailableBalance = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankAccount", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BankSteering",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    NextDebitOutcome = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    NextStatusCheck = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankSteering", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LinkedAccount",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    LastFour = table.Column<string>(type: "char(4)", unicode: false, fixedLength: true, maxLength: 4, nullable: false),
                    BankAccountRef = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuthorityBasis = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LinkedAccount", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LinkedAccount_BusinessAccount_AccountId",
                        column: x => x.AccountId,
                        principalTable: "BusinessAccount",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BankDebitRequest",
                columns: table => new
                {
                    Reference = table.Column<string>(type: "varchar(12)", unicode: false, maxLength: 12, nullable: false),
                    AccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    Outcome = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    ReceivedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    DebitedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    Simulated = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankDebitRequest", x => x.Reference);
                    table.ForeignKey(
                        name: "FK_BankDebitRequest_BankAccount_AccountId",
                        column: x => x.AccountId,
                        principalTable: "BankAccount",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BankEntry",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Reference = table.Column<string>(type: "varchar(12)", unicode: false, maxLength: 12, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    Narrative = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PostedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankEntry", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BankEntry_BankAccount_AccountId",
                        column: x => x.AccountId,
                        principalTable: "BankAccount",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PaymentQuote",
                columns: table => new
                {
                    Reference = table.Column<string>(type: "varchar(12)", unicode: false, maxLength: 12, nullable: false),
                    CardId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LinkedAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    Fee = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    Total = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    FromLabel = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ToLabel = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    WordingVersion = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    Heading = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RenderedLines = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ButtonLabel = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ActorKind = table.Column<string>(type: "varchar(12)", unicode: false, maxLength: 12, nullable: false),
                    ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AuthorityBasis = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Origin = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    Status = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    RefusedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    RefusalReason = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    BalanceAtRefusal = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentQuote", x => x.Reference);
                    table.ForeignKey(
                        name: "FK_PaymentQuote_Card_CardId",
                        column: x => x.CardId,
                        principalTable: "Card",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentQuote_LinkedAccount_LinkedAccountId",
                        column: x => x.LinkedAccountId,
                        principalTable: "LinkedAccount",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Payment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Reference = table.Column<string>(type: "varchar(12)", unicode: false, maxLength: 12, nullable: false),
                    CardId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    Fee = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    State = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    SettledAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    EffectiveAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    BankDebitedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    FailureReason = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    BalanceBefore = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: true),
                    BalanceAfter = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: true),
                    Simulated = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payment", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Payment_Card_CardId",
                        column: x => x.CardId,
                        principalTable: "Card",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Payment_PaymentQuote_Reference",
                        column: x => x.Reference,
                        principalTable: "PaymentQuote",
                        principalColumn: "Reference",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PaymentEvent",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Reference = table.Column<string>(type: "varchar(12)", unicode: false, maxLength: 12, nullable: false),
                    PaymentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    Kind = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    FromState = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: true),
                    ToState = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: true),
                    Detail = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    Simulated = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentEvent", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentEvent_PaymentQuote_Reference",
                        column: x => x.Reference,
                        principalTable: "PaymentQuote",
                        principalColumn: "Reference",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentEvent_Payment_PaymentId",
                        column: x => x.PaymentId,
                        principalTable: "Payment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BankDebitRequest_AccountId",
                table: "BankDebitRequest",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_BankEntry_AccountId_PostedAtUtc",
                table: "BankEntry",
                columns: new[] { "AccountId", "PostedAtUtc" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_BankEntry_Reference",
                table: "BankEntry",
                column: "Reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LinkedAccount_AccountId",
                table: "LinkedAccount",
                column: "AccountId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payment_CardId_CreatedAtUtc",
                table: "Payment",
                columns: new[] { "CardId", "CreatedAtUtc" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_Payment_Completed",
                table: "Payment",
                columns: new[] { "CardId", "State" })
                .Annotation("SqlServer:Include", new[] { "Amount" });

            migrationBuilder.CreateIndex(
                name: "IX_Payment_Reference",
                table: "Payment",
                column: "Reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Payment_OnePending",
                table: "Payment",
                column: "CardId",
                unique: true,
                filter: "[State] = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentEvent_PaymentId",
                table: "PaymentEvent",
                column: "PaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentEvent_Reference_AtUtc",
                table: "PaymentEvent",
                columns: new[] { "Reference", "AtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentQuote_CardId_CreatedAtUtc",
                table: "PaymentQuote",
                columns: new[] { "CardId", "CreatedAtUtc" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentQuote_LinkedAccountId",
                table: "PaymentQuote",
                column: "LinkedAccountId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BankDebitRequest");

            migrationBuilder.DropTable(
                name: "BankEntry");

            migrationBuilder.DropTable(
                name: "BankSteering");

            migrationBuilder.DropTable(
                name: "PaymentEvent");

            migrationBuilder.DropTable(
                name: "BankAccount");

            migrationBuilder.DropTable(
                name: "Payment");

            migrationBuilder.DropTable(
                name: "PaymentQuote");

            migrationBuilder.DropTable(
                name: "LinkedAccount");
        }
    }
}
