using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shop.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class VariantCostPriceAndOrderItemUnitCost : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "CostPrice",
                table: "ProductVariants",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "UnitCost",
                table: "OrderItems",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProductVariants_CostPrice",
                table: "ProductVariants",
                sql: "[CostPrice] IS NULL OR [CostPrice] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_OrderItems_UnitCost",
                table: "OrderItems",
                sql: "[UnitCost] IS NULL OR [UnitCost] >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ProductVariants_CostPrice",
                table: "ProductVariants");

            migrationBuilder.DropCheckConstraint(
                name: "CK_OrderItems_UnitCost",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "CostPrice",
                table: "ProductVariants");

            migrationBuilder.DropColumn(
                name: "UnitCost",
                table: "OrderItems");
        }
    }
}
