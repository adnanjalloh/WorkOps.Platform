using Microsoft.Extensions.DependencyInjection;
using WorkOps.Application.Common.Sanitization;
using WorkOps.Application.Identity;
using WorkOps.Application.Tenancy;

namespace WorkOps.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddWorkOpsApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IInputSanitizer, InputSanitizer>();
        services.AddScoped<IWorkspaceContextAccessor, WorkspaceContextAccessor>();
        services.AddScoped<IdentityService>();
        services.AddScoped<WorkspaceAccessService>();
        services.AddScoped<WorkspaceService>();
        return services;
    }
}
