using Microsoft.AspNetCore.Authorization;
using WorkOps.Application.Tenancy;
using WorkOps.Domain.Tenancy;

namespace WorkOps.Api.Authorization;

internal sealed class PermissionAuthorizationHandler(
    IWorkspaceContextAccessor workspaceContext) : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        var current = workspaceContext.Current;
        if (current is not null && Permissions.ForRole(current.Role).Contains(requirement.Permission))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
