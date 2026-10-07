using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarGarage.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "text", nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: false),
                    first_name = table.Column<string>(type: "text", nullable: false),
                    last_name = table.Column<string>(type: "text", nullable: false),
                    phone = table.Column<string>(type: "text", nullable: true),
                    audience = table.Column<string>(type: "text", nullable: false),
                    roles = table.Column<string[]>(type: "text[]", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    deleted = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                    table.CheckConstraint("ck_users_audience", "audience IN ('ADMIN', 'WORKSHOP', 'CUSTOMER')");
                    table.CheckConstraint("ck_users_email", "email <> '' AND email = lower(btrim(email))");
                    table.CheckConstraint("ck_users_required_fields", "btrim(password_hash) <> '' AND btrim(first_name) <> '' AND btrim(last_name) <> ''");
                    table.CheckConstraint("ck_users_roles", "roles <@ ARRAY['CUSTOMER', 'WORKSHOP_MEMBER', 'WORKSHOP_ADMIN', 'PLATFORM_ADMIN']::text[] AND array_position(roles, NULL) IS NULL");
                });

            migrationBuilder.CreateIndex(
                name: "ux_users_email_audience_active",
                table: "users",
                columns: new[] { "email", "audience" },
                unique: true,
                filter: "deleted = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
