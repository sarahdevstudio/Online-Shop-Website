using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shop.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StoreSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PaymentBankName",
                table: "Orders",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentCardHolder",
                table: "Orders",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentCardNumber",
                table: "Orders",
                type: "varchar(16)",
                unicode: false,
                maxLength: 16,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SettingsAuditLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AdminUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Field = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    OldValue = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    NewValue = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SettingsAuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StoreSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    ShippingFlatFee = table.Column<long>(type: "bigint", nullable: false),
                    FreeShippingThreshold = table.Column<long>(type: "bigint", nullable: true),
                    CardToCardEnabled = table.Column<bool>(type: "bit", nullable: false),
                    CashOnDeliveryEnabled = table.Column<bool>(type: "bit", nullable: false),
                    CardNumber = table.Column<string>(type: "varchar(16)", unicode: false, maxLength: 16, nullable: false),
                    CardHolderName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    BankName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    MaxQuantityPerLine = table.Column<int>(type: "int", nullable: false),
                    StorePhone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    StoreAddress = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    WorkingHours = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    InstagramHandle = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoreSettings", x => x.Id);
                    table.CheckConstraint("CK_StoreSettings_Amounts", "[ShippingFlatFee] >= 0 AND ([FreeShippingThreshold] IS NULL OR [FreeShippingThreshold] >= 0)");
                    table.CheckConstraint("CK_StoreSettings_MaxQuantity", "[MaxQuantityPerLine] BETWEEN 1 AND 100");
                    table.CheckConstraint("CK_StoreSettings_Singleton", "[Id] = 1");
                });

            migrationBuilder.CreateIndex(
                name: "IX_SettingsAuditLogs_AtUtc",
                table: "SettingsAuditLogs",
                column: "AtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SettingsAuditLogs");

            migrationBuilder.DropTable(
                name: "StoreSettings");

            migrationBuilder.DropColumn(
                name: "PaymentBankName",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PaymentCardHolder",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PaymentCardNumber",
                table: "Orders");
        }
    }
}
