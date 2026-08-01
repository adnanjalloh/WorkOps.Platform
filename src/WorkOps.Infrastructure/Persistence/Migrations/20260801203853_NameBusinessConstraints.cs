using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkOps.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class NameBusinessConstraints : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.RenameIndex(
            name: "IX_workspaces_Slug",
            table: "workspaces",
            newName: "UX_workspaces_slug");

        migrationBuilder.RenameIndex(
            name: "IX_projects_WorkspaceId_Key",
            table: "projects",
            newName: "UX_projects_workspace_key");

        migrationBuilder.RenameIndex(
            name: "IX_notification_deliveries_WorkspaceId_SourceMessageId_Recipie~",
            table: "notification_deliveries",
            newName: "UX_notification_deliveries_deduplication");

        migrationBuilder.RenameIndex(
            name: "IX_identity_users_Subject",
            table: "identity_users",
            newName: "UX_identity_users_subject");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.RenameIndex(
            name: "UX_workspaces_slug",
            table: "workspaces",
            newName: "IX_workspaces_Slug");

        migrationBuilder.RenameIndex(
            name: "UX_projects_workspace_key",
            table: "projects",
            newName: "IX_projects_WorkspaceId_Key");

        migrationBuilder.RenameIndex(
            name: "UX_notification_deliveries_deduplication",
            table: "notification_deliveries",
            newName: "IX_notification_deliveries_WorkspaceId_SourceMessageId_Recipie~");

        migrationBuilder.RenameIndex(
            name: "UX_identity_users_subject",
            table: "identity_users",
            newName: "IX_identity_users_Subject");
    }
}
