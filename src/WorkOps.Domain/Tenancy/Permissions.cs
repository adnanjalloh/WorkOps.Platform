namespace WorkOps.Domain.Tenancy;

public static class Permissions
{
    public const string WorkspacesRead = "workspaces.read";
    public const string WorkspacesManage = "workspaces.manage";
    public const string MembersRead = "members.read";
    public const string MembersManage = "members.manage";
    public const string ProjectsRead = "projects.read";
    public const string ProjectsWrite = "projects.write";
    public const string AuditRead = "audit.read";
    public const string NotificationsRead = "notifications.read";
    public const string OperationsManage = "operations.manage";

    public static IReadOnlySet<string> ForRole(WorkspaceRole role) => role switch
    {
        WorkspaceRole.Owner => Owner,
        WorkspaceRole.Administrator => Administrator,
        WorkspaceRole.ProjectContributor => Contributor,
        WorkspaceRole.Viewer => Viewer,
        _ => Empty,
    };

    private static readonly IReadOnlySet<string> Empty = new HashSet<string>(StringComparer.Ordinal);

    private static readonly IReadOnlySet<string> Owner = new HashSet<string>(StringComparer.Ordinal)
    {
        WorkspacesRead,
        WorkspacesManage,
        MembersRead,
        MembersManage,
        ProjectsRead,
        ProjectsWrite,
        AuditRead,
        NotificationsRead,
        OperationsManage,
    };

    private static readonly IReadOnlySet<string> Administrator = new HashSet<string>(StringComparer.Ordinal)
    {
        WorkspacesRead,
        MembersRead,
        MembersManage,
        ProjectsRead,
        ProjectsWrite,
        AuditRead,
        NotificationsRead,
        OperationsManage,
    };

    private static readonly IReadOnlySet<string> Contributor = new HashSet<string>(StringComparer.Ordinal)
    {
        WorkspacesRead,
        MembersRead,
        ProjectsRead,
        ProjectsWrite,
        NotificationsRead,
    };

    private static readonly IReadOnlySet<string> Viewer = new HashSet<string>(StringComparer.Ordinal)
    {
        WorkspacesRead,
        MembersRead,
        ProjectsRead,
        NotificationsRead,
    };
}
