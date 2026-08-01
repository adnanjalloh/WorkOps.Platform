using WorkOps.Application.Abstractions;
using WorkOps.Application.Audit;
using WorkOps.Application.Common.Sanitization;
using WorkOps.Application.Common.Validation;
using WorkOps.Application.Tenancy;
using WorkOps.Domain.Features;

namespace WorkOps.Application.Features;

public sealed class FeatureService(
    IWorkspaceSubscriptionStore subscriptions,
    IFeatureCache cache,
    IUnitOfWork unitOfWork,
    IWorkspaceContextAccessor workspaceContext,
    AuditWriter auditWriter,
    IInputSanitizer sanitizer,
    TimeProvider timeProvider)
{
    public Task<FeatureSnapshot> GetCurrentAsync(CancellationToken cancellationToken)
    {
        var current = workspaceContext.Current
            ?? throw new InvalidOperationException("An interactive workspace context is required.");
        return cache.GetOrCreateAsync(
            current.WorkspaceId,
            async innerCancellationToken =>
                await subscriptions.GetCurrentSnapshotAsync(innerCancellationToken)
                ?? throw new InvalidOperationException("Workspace subscription is missing."),
            cancellationToken);
    }

    public async Task ReserveProjectSlotAsync(CancellationToken cancellationToken)
    {
        var subscription = await subscriptions.FindCurrentAsync(cancellationToken)
            ?? throw new InvalidOperationException("Workspace subscription is missing.");
        subscription.ReserveProjectSlot(
            FeatureCatalog.MaximumActiveProjects(subscription.Plan),
            timeProvider.GetUtcNow());
    }

    public async Task ReleaseProjectSlotAsync(CancellationToken cancellationToken)
    {
        var subscription = await subscriptions.FindCurrentAsync(cancellationToken)
            ?? throw new InvalidOperationException("Workspace subscription is missing.");
        subscription.ReleaseProjectSlot(timeProvider.GetUtcNow());
    }

    public async Task<bool> UpdatePlanAsync(
        string plan,
        CancellationToken cancellationToken)
    {
        var safePlan = sanitizer.Apply(plan, InputProfile.Identifier, "body.plan");
        if (!Enum.TryParse<WorkspacePlan>(safePlan, true, out var parsedPlan) ||
            !Enum.IsDefined(parsedPlan))
        {
            throw new RequestValidationException("invalid_workspace_plan");
        }

        var subscription = await subscriptions.FindCurrentAsync(cancellationToken);
        if (subscription is null)
        {
            return false;
        }

        var previousPlan = subscription.Plan;
        var now = timeProvider.GetUtcNow();
        if (!subscription.ChangePlan(parsedPlan, now))
        {
            return true;
        }

        auditWriter.Record(
            AuditActions.WorkspacePlanChanged,
            "workspace_subscription",
            subscription.WorkspaceId.Value,
            now,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["currentPlan"] = parsedPlan.ToString(),
                ["previousPlan"] = previousPlan.ToString(),
            });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await cache.InvalidateAsync(subscription.WorkspaceId, cancellationToken);
        return true;
    }

    public Task InvalidateAsync(CancellationToken cancellationToken)
    {
        var workspaceId = workspaceContext.CurrentWorkspaceId
            ?? throw new InvalidOperationException("Workspace context is required.");
        return cache.InvalidateAsync(workspaceId, cancellationToken);
    }
}
