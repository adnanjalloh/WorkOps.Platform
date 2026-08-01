using WorkOps.Application.Abstractions;
using WorkOps.Application.Audit;
using WorkOps.Application.Common.Pagination;
using WorkOps.Application.Common.Sanitization;
using WorkOps.Application.Common.Validation;
using WorkOps.Application.Tenancy;
using WorkOps.Domain.Projects;

namespace WorkOps.Application.Projects;

public sealed class ProjectService(
    IProjectStore projects,
    IUnitOfWork unitOfWork,
    AuditWriter auditWriter,
    IWorkspaceContextAccessor workspaceContext,
    IInputSanitizer sanitizer,
    TimeProvider timeProvider)
{
    public async Task<ProjectView> CreateAsync(
        string name,
        string key,
        CancellationToken cancellationToken)
    {
        var current = workspaceContext.Current
            ?? throw new InvalidOperationException("Workspace context is required.");
        var safeName = sanitizer.Apply(name, InputProfile.PlainText, "body.name");
        var safeKey = sanitizer.Apply(key, InputProfile.KeyPath, "body.key");

        if (await projects.KeyExistsAsync(safeKey, cancellationToken))
        {
            throw new DuplicateProjectKeyException();
        }

        var project = Project.Create(
            current.WorkspaceId,
            safeName,
            safeKey,
            timeProvider.GetUtcNow());
        projects.Add(project);
        auditWriter.Record(
            AuditActions.ProjectCreated,
            "project",
            project.Id,
            project.CreatedAt,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["status"] = project.Status.ToString(),
            });
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return await projects.GetAsync(project.Id, cancellationToken)
            ?? throw new InvalidOperationException("Created project could not be read.");
    }

    public Task<ProjectView?> GetAsync(Guid projectId, CancellationToken cancellationToken) =>
        projects.GetAsync(projectId, cancellationToken);

    public Task<PagedResult<ProjectView>> ListAsync(
        int page,
        int pageSize,
        string? search,
        string? status,
        CancellationToken cancellationToken)
    {
        if (page is < 1 or > 10_000 || pageSize is < 1 or > 100)
        {
            throw new RequestValidationException("invalid_pagination");
        }

        var safeSearch = string.IsNullOrWhiteSpace(search)
            ? null
            : sanitizer.Apply(search, InputProfile.SearchText, "query.search");
        ProjectStatus? parsedStatus = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            var safeStatus = sanitizer.Apply(status, InputProfile.Identifier, "query.status");
            if (!Enum.TryParse<ProjectStatus>(safeStatus, true, out var value) ||
                !Enum.IsDefined(value))
            {
                throw new RequestValidationException("invalid_project_status");
            }

            parsedStatus = value;
        }

        return projects.ListAsync(page, pageSize, safeSearch, parsedStatus, cancellationToken);
    }

    public async Task<bool> ArchiveAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var project = await projects.FindAsync(projectId, cancellationToken);
        if (project is null)
        {
            return false;
        }

        var now = timeProvider.GetUtcNow();
        if (project.Archive(now))
        {
            auditWriter.Record(
                AuditActions.ProjectArchived,
                "project",
                project.Id,
                now,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["currentStatus"] = project.Status.ToString(),
                    ["previousStatus"] = ProjectStatus.Active.ToString(),
                });
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return true;
    }
}
