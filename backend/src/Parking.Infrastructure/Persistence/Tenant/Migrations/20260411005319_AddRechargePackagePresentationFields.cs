using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Parking.Infrastructure.Persistence.Tenant.Migrations
{
    /// <inheritdoc />
    public partial class AddRechargePackagePresentationFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "display_name",
                table: "recharge_packages",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "is_promo",
                table: "recharge_packages",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "sort_order",
                table: "recharge_packages",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql(
                """
                UPDATE recharge_packages
                SET
                    display_name = CASE
                        WHEN id = '11111111-1111-1111-1111-111111111101'::uuid THEN 'Cliente 10h'
                        WHEN id = '11111111-1111-1111-1111-111111111102'::uuid THEN 'Cliente Promo 50h'
                        WHEN id = '22222222-2222-2222-2222-222222222201'::uuid THEN 'Convênio 20h'
                        WHEN id = '22222222-2222-2222-2222-222222222202'::uuid THEN 'Convênio Promo 100h'
                        ELSE scope || ' ' || hours || 'h'
                    END,
                    is_promo = CASE
                        WHEN id IN (
                            '11111111-1111-1111-1111-111111111102'::uuid,
                            '22222222-2222-2222-2222-222222222202'::uuid
                        ) THEN true
                        ELSE false
                    END,
                    sort_order = CASE
                        WHEN id IN (
                            '11111111-1111-1111-1111-111111111101'::uuid,
                            '22222222-2222-2222-2222-222222222201'::uuid
                        ) THEN 10
                        WHEN id IN (
                            '11111111-1111-1111-1111-111111111102'::uuid,
                            '22222222-2222-2222-2222-222222222202'::uuid
                        ) THEN 20
                        ELSE 1000 + hours
                    END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "display_name",
                table: "recharge_packages");

            migrationBuilder.DropColumn(
                name: "is_promo",
                table: "recharge_packages");

            migrationBuilder.DropColumn(
                name: "sort_order",
                table: "recharge_packages");
        }
    }
}
