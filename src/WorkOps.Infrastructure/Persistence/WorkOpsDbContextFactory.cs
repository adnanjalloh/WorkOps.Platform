using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using WorkOps.Application.Tenancy;

namespace WorkOps.Infrastructure.Persistence;

public sealed class WorkOpsDbContextFactory : IDesignTimeDbContextFactory<WorkOpsDbContext>
{
    public WorkOpsDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<WorkOpsDbContext>()
            .UseNpgsql("Host=localhost;Database=workops_design;Username=workops")
            .Options;

        return new WorkOpsDbContext(options, new WorkspaceContextAccessor());
    }
}
