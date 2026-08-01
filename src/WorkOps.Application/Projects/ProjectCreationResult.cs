namespace WorkOps.Application.Projects;

public sealed record ProjectCreationResult(ProjectView Project, bool Replayed);
