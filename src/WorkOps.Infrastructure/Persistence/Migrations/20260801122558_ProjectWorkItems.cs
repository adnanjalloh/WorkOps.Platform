using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkOps.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class ProjectWorkItems : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "projects",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                Key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_projects", x => x.Id);
                table.UniqueConstraint("AK_projects_WorkspaceId_Id", x => new { x.WorkspaceId, x.Id });
                table.ForeignKey(
                    name: "FK_projects_workspaces_WorkspaceId",
                    column: x => x.WorkspaceId,
                    principalTable: "workspaces",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "work_items",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                Title = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                Priority = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                AssigneeUserId = table.Column<Guid>(type: "uuid", nullable: true),
                Labels = table.Column<string[]>(type: "text[]", nullable: false),
                xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_work_items", x => x.Id);
                table.ForeignKey(
                    name: "FK_work_items_identity_users_AssigneeUserId",
                    column: x => x.AssigneeUserId,
                    principalTable: "identity_users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_work_items_projects_WorkspaceId_ProjectId",
                    columns: x => new { x.WorkspaceId, x.ProjectId },
                    principalTable: "projects",
                    principalColumns: new[] { "WorkspaceId", "Id" },
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_projects_WorkspaceId_Key",
            table: "projects",
            columns: new[] { "WorkspaceId", "Key" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_projects_WorkspaceId_Status_Name",
            table: "projects",
            columns: new[] { "WorkspaceId", "Status", "Name" });

        migrationBuilder.CreateIndex(
            name: "IX_work_items_AssigneeUserId",
            table: "work_items",
            column: "AssigneeUserId");

        migrationBuilder.CreateIndex(
            name: "IX_work_items_WorkspaceId_AssigneeUserId_Status",
            table: "work_items",
            columns: new[] { "WorkspaceId", "AssigneeUserId", "Status" });

        migrationBuilder.CreateIndex(
            name: "IX_work_items_WorkspaceId_ProjectId_Status",
            table: "work_items",
            columns: new[] { "WorkspaceId", "ProjectId", "Status" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "work_items");

        migrationBuilder.DropTable(
            name: "projects");
    }
}
