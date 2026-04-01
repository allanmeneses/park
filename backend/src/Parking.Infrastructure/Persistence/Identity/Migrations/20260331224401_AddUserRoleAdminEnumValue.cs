using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Parking.Infrastructure.Persistence.Identity.Migrations
{
    /// <inheritdoc />
    public partial class AddUserRoleAdminEnumValue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Bases criadas antes de existir ADMIN no enum (volume Postgres antigo) falhavam em CreateTenant.
            migrationBuilder.Sql("""
                DO $ef$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM pg_enum e
                        INNER JOIN pg_type t ON e.enumtypid = t.oid
                        INNER JOIN pg_namespace n ON t.typnamespace = n.oid
                        WHERE n.nspname = 'public' AND t.typname = 'user_role' AND e.enumlabel = 'ADMIN'
                    ) THEN
                        ALTER TYPE public.user_role ADD VALUE 'ADMIN';
                    END IF;
                END
                $ef$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
