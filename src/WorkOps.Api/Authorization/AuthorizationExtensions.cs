using Microsoft.AspNetCore.Authorization;
using WorkOps.Domain.Tenancy;

namespace WorkOps.Api.Authorization;

internal static class AuthorizationExtensions
{
    public static IServiceCollection AddWorkOpsAuthorization(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            options.DefaultPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .RequireClaim("sub")
                .Build();

            AddPermissionPolicy(options, Permissions.WorkspacesRead);
            AddPermissionPolicy(options, Permissions.WorkspacesManage);
            AddPermissionPolicy(options, Permissions.MembersRead);
            AddPermissionPolicy(options, Permissions.MembersManage);
            AddPermissionPolicy(options, Permissions.ProjectsRead);
            AddPermissionPolicy(options, Permissions.ProjectsWrite);
            AddPermissionPolicy(options, Permissions.AuditRead);
            AddPermissionPolicy(options, Permissions.NotificationsRead);
            AddPermissionPolicy(options, Permissions.OperationsManage);
        });
        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
        return services;
    }

    private static void AddPermissionPolicy(AuthorizationOptions options, string permission) =>
        options.AddPolicy(permission, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireClaim("sub");
            policy.AddRequirements(new PermissionRequirement(permission));
        });
}
