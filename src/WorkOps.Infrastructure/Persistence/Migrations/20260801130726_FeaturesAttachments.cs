using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkOps.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class FeaturesAttachments : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddUniqueConstraint(
            name: "AK_work_items_WorkspaceId_Id",
            table: "work_items",
            columns: new[] { "WorkspaceId", "Id" });

        migrationBuilder.CreateTable(
            name: "attachments",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                WorkItemId = table.Column<Guid>(type: "uuid", nullable: false),
                UploadedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                FileName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                Size = table.Column<long>(type: "bigint", nullable: false),
                StorageName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                Sha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_attachments", x => x.Id);
                table.ForeignKey(
                    name: "FK_attachments_work_items_WorkspaceId_WorkItemId",
                    columns: x => new { x.WorkspaceId, x.WorkItemId },
                    principalTable: "work_items",
                    principalColumns: new[] { "WorkspaceId", "Id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_attachments_workspace_memberships_WorkspaceId_UploadedByUse~",
                    columns: x => new { x.WorkspaceId, x.UploadedByUserId },
                    principalTable: "workspace_memberships",
                    principalColumns: new[] { "WorkspaceId", "UserId" },
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "workspace_subscriptions",
            columns: table => new
            {
                WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                Plan = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                ActiveProjectCount = table.Column<int>(type: "integer", nullable: false),
                xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_workspace_subscriptions", x => x.WorkspaceId);
                table.ForeignKey(
                    name: "FK_workspace_subscriptions_workspaces_WorkspaceId",
                    column: x => x.WorkspaceId,
                    principalTable: "workspaces",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.Sql(
            """
            INSERT INTO workspace_subscriptions ("WorkspaceId", "Plan", "ActiveProjectCount", "CreatedAt")
            SELECT
                w."Id",
                'Starter',
                COUNT(p."Id") FILTER (WHERE p."Status" = 'Active')::integer,
                w."CreatedAt"
            FROM workspaces AS w
            LEFT JOIN projects AS p ON p."WorkspaceId" = w."Id"
            GROUP BY w."Id", w."CreatedAt";
            """);

        migrationBuilder.CreateIndex(
            name: "IX_attachments_WorkspaceId_StorageName",
            table: "attachments",
            columns: new[] { "WorkspaceId", "StorageName" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_attachments_WorkspaceId_UploadedByUserId",
            table: "attachments",
            columns: new[] { "WorkspaceId", "UploadedByUserId" });

        migrationBuilder.CreateIndex(
            name: "IX_attachments_WorkspaceId_WorkItemId_CreatedAt",
            table: "attachments",
            columns: new[] { "WorkspaceId", "WorkItemId", "CreatedAt" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "attachments");

        migrationBuilder.DropTable(
            name: "workspace_subscriptions");

        migrationBuilder.DropUniqueConstraint(
            name: "AK_work_items_WorkspaceId_Id",
            table: "work_items");
    }
}
