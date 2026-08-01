using WorkOps.Domain;
using WorkOps.Domain.Tenancy;

namespace WorkOps.Application.Identity;

public sealed record MembershipView(
    WorkspaceId WorkspaceId,
    string WorkspaceName,
    string WorkspaceSlug,
    WorkspaceStatus WorkspaceStatus,
    WorkspaceRole Role);
