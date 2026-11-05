using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CardLab.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCardControls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BusinessAccount",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TimeZoneId = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessAccount", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AdminUser",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminUser", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AdminUser_BusinessAccount_AccountId",
                        column: x => x.AccountId,
                        principalTable: "BusinessAccount",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Card",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CardholderName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    LastFour = table.Column<string>(type: "char(4)", unicode: false, fixedLength: true, maxLength: 4, nullable: false),
                    LookupHash = table.Column<byte[]>(type: "binary(32)", fixedLength: true, maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Card", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Card_BusinessAccount_AccountId",
                        column: x => x.AccountId,
                        principalTable: "BusinessAccount",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AuditEvent",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CardId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    Action = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    OldValue = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    NewValue = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditEvent", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuditEvent_Card_CardId",
                        column: x => x.CardId,
                        principalTable: "Card",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SpendLimit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CardId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Category = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: false),
                    Period = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpendLimit", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SpendLimit_Card_CardId",
                        column: x => x.CardId,
                        principalTable: "Card",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Transaction",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CardId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    Category = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: false),
                    Merchant = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", nullable: false),
                    DecidedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    Outcome = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    Reason = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Transaction", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Transaction_Card_CardId",
                        column: x => x.CardId,
                        principalTable: "Card",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdminUser_AccountId_DisplayName",
                table: "AdminUser",
                columns: new[] { "AccountId", "DisplayName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvent_CardId_AtUtc",
                table: "AuditEvent",
                columns: new[] { "CardId", "AtUtc" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_BusinessAccount_Name",
                table: "BusinessAccount",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Card_AccountId_Name",
                table: "Card",
                columns: new[] { "AccountId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Card_LookupHash",
                table: "Card",
                column: "LookupHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SpendLimit_CardId_Category_Period",
                table: "SpendLimit",
                columns: new[] { "CardId", "Category", "Period" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Transaction_CardId_DecidedAtUtc",
                table: "Transaction",
                columns: new[] { "CardId", "DecidedAtUtc" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_Transaction_Spend",
                table: "Transaction",
                columns: new[] { "CardId", "Category", "Outcome", "OccurredAt" })
                .Annotation("SqlServer:Include", new[] { "Amount" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdminUser");

            migrationBuilder.DropTable(
                name: "AuditEvent");

            migrationBuilder.DropTable(
                name: "SpendLimit");

            migrationBuilder.DropTable(
                name: "Transaction");

            migrationBuilder.DropTable(
                name: "Card");

            migrationBuilder.DropTable(
                name: "BusinessAccount");
        }
    }
}
