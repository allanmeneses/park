using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Parking.Domain;

#nullable disable

namespace Parking.Infrastructure.Persistence.Tenant.Migrations
{
    /// <inheritdoc />
    public partial class InitialTenant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:public.cash_session_status", "OPEN,CLOSED")
                .Annotation("Npgsql:Enum:public.payment_method", "PIX,CARD,CASH")
                .Annotation("Npgsql:Enum:public.payment_status", "PENDING,PAID,FAILED,EXPIRED")
                .Annotation("Npgsql:Enum:public.ticket_status", "OPEN,AWAITING_PAYMENT,CLOSED");

            migrationBuilder.CreateTable(
                name: "alerts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "text", nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_alerts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "cash_sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<CashSessionStatus>(type: "cash_session_status", nullable: false),
                    opened_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    expected_amount = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    actual_amount = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cash_sessions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "client_wallets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_id = table.Column<Guid>(type: "uuid", nullable: false),
                    balance_hours = table.Column<int>(type: "integer", nullable: false),
                    expiration_date = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_client_wallets", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "clients",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    plate = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    lojista_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_clients", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "idempotency_store",
                columns: table => new
                {
                    key = table.Column<string>(type: "text", nullable: false),
                    route = table.Column<string>(type: "text", nullable: false),
                    response_json = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_idempotency_store", x => new { x.key, x.route });
                });

            migrationBuilder.CreateTable(
                name: "lojista_wallets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    lojista_id = table.Column<Guid>(type: "uuid", nullable: false),
                    balance_hours = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lojista_wallets", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "lojistas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    hour_price = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lojistas", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "operator_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_operator_events", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "package_orders",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    scope = table.Column<string>(type: "text", nullable: false),
                    client_id = table.Column<Guid>(type: "uuid", nullable: true),
                    lojista_id = table.Column<Guid>(type: "uuid", nullable: true),
                    package_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    settlement = table.Column<string>(type: "text", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    paid_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_package_orders", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "payments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ticket_id = table.Column<Guid>(type: "uuid", nullable: true),
                    package_order_id = table.Column<Guid>(type: "uuid", nullable: true),
                    method = table.Column<PaymentMethod>(type: "payment_method", nullable: true),
                    status = table.Column<PaymentStatus>(type: "payment_status", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    transaction_id = table.Column<string>(type: "text", nullable: true),
                    idempotency_key = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    paid_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    failed_reason = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_payments", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "pix_transactions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider_status = table.Column<string>(type: "text", nullable: false),
                    qr_code = table.Column<string>(type: "text", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    transaction_id = table.Column<string>(type: "text", nullable: true),
                    active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pix_transactions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "recharge_packages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    scope = table.Column<string>(type: "text", nullable: false),
                    hours = table.Column<int>(type: "integer", nullable: false),
                    price = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_recharge_packages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "schema_migrations",
                columns: table => new
                {
                    version = table.Column<string>(type: "text", nullable: false),
                    applied_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_schema_migrations", x => x.version);
                });

            migrationBuilder.CreateTable(
                name: "settings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    price_per_hour = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    capacity = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_settings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tickets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    plate = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    entry_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    exit_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<TicketStatus>(type: "ticket_status", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tickets", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "wallet_ledger",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_id = table.Column<Guid>(type: "uuid", nullable: true),
                    lojista_id = table.Column<Guid>(type: "uuid", nullable: true),
                    delta_hours = table.Column<int>(type: "integer", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    package_id = table.Column<Guid>(type: "uuid", nullable: true),
                    settlement = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_wallet_ledger", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "wallet_usages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ticket_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source = table.Column<string>(type: "text", nullable: false),
                    hours_used = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_wallet_usages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "webhook_receipts",
                columns: table => new
                {
                    transaction_id = table.Column<string>(type: "text", nullable: false),
                    payment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    processed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_webhook_receipts", x => x.transaction_id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_client_wallets_client_id",
                table: "client_wallets",
                column: "client_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_clients_plate",
                table: "clients",
                column: "plate",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_lojista_wallets_lojista_id",
                table: "lojista_wallets",
                column: "lojista_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_operator_events_user_id_created_at",
                table: "operator_events",
                columns: new[] { "user_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_payments_idempotency_key",
                table: "payments",
                column: "idempotency_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_payments_package_order_id",
                table: "payments",
                column: "package_order_id",
                unique: true,
                filter: "package_order_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_payments_ticket_id",
                table: "payments",
                column: "ticket_id",
                unique: true,
                filter: "ticket_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_pix_transactions_payment_id",
                table: "pix_transactions",
                column: "payment_id",
                unique: true,
                filter: "active = true");

            migrationBuilder.CreateIndex(
                name: "ix_pix_transactions_transaction_id",
                table: "pix_transactions",
                column: "transaction_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tickets_plate",
                table: "tickets",
                column: "plate",
                unique: true,
                filter: "status IN ('OPEN', 'AWAITING_PAYMENT')");

            migrationBuilder.CreateIndex(
                name: "ix_webhook_receipts_processed_at",
                table: "webhook_receipts",
                column: "processed_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "alerts");

            migrationBuilder.DropTable(
                name: "cash_sessions");

            migrationBuilder.DropTable(
                name: "client_wallets");

            migrationBuilder.DropTable(
                name: "clients");

            migrationBuilder.DropTable(
                name: "idempotency_store");

            migrationBuilder.DropTable(
                name: "lojista_wallets");

            migrationBuilder.DropTable(
                name: "lojistas");

            migrationBuilder.DropTable(
                name: "operator_events");

            migrationBuilder.DropTable(
                name: "package_orders");

            migrationBuilder.DropTable(
                name: "payments");

            migrationBuilder.DropTable(
                name: "pix_transactions");

            migrationBuilder.DropTable(
                name: "recharge_packages");

            migrationBuilder.DropTable(
                name: "schema_migrations");

            migrationBuilder.DropTable(
                name: "settings");

            migrationBuilder.DropTable(
                name: "tickets");

            migrationBuilder.DropTable(
                name: "wallet_ledger");

            migrationBuilder.DropTable(
                name: "wallet_usages");

            migrationBuilder.DropTable(
                name: "webhook_receipts");
        }
    }
}
