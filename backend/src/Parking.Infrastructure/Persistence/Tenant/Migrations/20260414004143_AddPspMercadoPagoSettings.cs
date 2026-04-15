using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Parking.Infrastructure.Persistence.Tenant.Migrations
{
    /// <inheritdoc />
    public partial class AddPspMercadoPagoSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "psp_mercado_pago_settings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    use_tenant_credentials = table.Column<bool>(type: "boolean", nullable: false),
                    environment = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    access_token_cipher = table.Column<string>(type: "text", nullable: false),
                    webhook_secret_cipher = table.Column<string>(type: "text", nullable: false),
                    public_key = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    payer_email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    api_base_url = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    checkout_back_success_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    checkout_back_failure_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    checkout_back_pending_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    credentials_acknowledged_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by_user_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_psp_mercado_pago_settings", x => x.id);
                });

            migrationBuilder.Sql(
                """
                INSERT INTO psp_mercado_pago_settings (id, use_tenant_credentials, environment, access_token_cipher, webhook_secret_cipher, public_key, payer_email, updated_at)
                VALUES ('00000000-0000-0000-0000-000000000001'::uuid, false, 'PRODUCTION', '', '', '', '', NOW() AT TIME ZONE 'UTC')
                ON CONFLICT (id) DO NOTHING
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "psp_mercado_pago_settings");
        }
    }
}
