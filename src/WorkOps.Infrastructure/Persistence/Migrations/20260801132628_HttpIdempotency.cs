using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkOps.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class HttpIdempotency : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "idempotency_records",
            columns: table => new
            {
                WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                Method = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                Route = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                Key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                RequestHash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                StatusCode = table.Column<int>(type: "integer", nullable: false),
                ResponseBodyJson = table.Column<string>(type: "jsonb", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_idempotency_records", x => new { x.WorkspaceId, x.UserId, x.Method, x.Route, x.Key });
                table.ForeignKey(
                    name: "FK_idempotency_records_identity_users_UserId",
                    column: x => x.UserId,
                    principalTable: "identity_users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_idempotency_records_workspace_memberships_WorkspaceId_UserId",
                    columns: x => new { x.WorkspaceId, x.UserId },
                    principalTable: "workspace_memberships",
                    principalColumns: new[] { "WorkspaceId", "UserId" },
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_idempotency_records_workspaces_WorkspaceId",
                    column: x => x.WorkspaceId,
                    principalTable: "workspaces",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_idempotency_records_ExpiresAt",
            table: "idempotency_records",
            column: "ExpiresAt");

        migrationBuilder.CreateIndex(
            name: "IX_idempotency_records_UserId",
            table: "idempotency_records",
            column: "UserId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "idempotency_records");
    }
}
