using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Parking.Infrastructure.Persistence.Identity.Migrations
{
    /// <inheritdoc />
    public partial class AddLojistaInvites : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "lojista_invites",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    parking_id = table.Column<Guid>(type: "uuid", nullable: false),
                    lojista_id = table.Column<Guid>(type: "uuid", nullable: false),
                    merchant_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    activation_code_hash = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    activated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    activated_user_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lojista_invites", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_lojista_invites_merchant_code",
                table: "lojista_invites",
                column: "merchant_code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "lojista_invites");
        }
    }
}
