using Microsoft.EntityFrameworkCore;
using WorkOps.Application.Abstractions;
using WorkOps.Application.Messaging;
using WorkOps.Application.Tenancy;
using WorkOps.Domain.Messaging;
using WorkOps.Infrastructure.Persistence;

namespace WorkOps.Infrastructure.Messaging;

internal sealed class OutboxStore(
    WorkOpsDbContext dbContext,
    IWorkspaceContextAccessor workspaceContext) : IOutboxStore
{
    public void Add(OutboxMessage message) => dbContext.OutboxMessages.Add(message);

    public async Task<OutboxLease?> LeaseNextAsync(
        DateTimeOffset now,
        DateTimeOffset lockedUntil,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var message = await dbContext.OutboxMessages
            .FromSqlInterpolated($"""
                SELECT *
                FROM outbox_messages
                WHERE ("Status" = 'Pending' AND "NextAttemptAt" <= {now})
                   OR ("Status" = 'Processing' AND "LockedUntil" <= {now})
                ORDER BY "OccurredAt", "Id"
                LIMIT 1
                FOR UPDATE SKIP LOCKED
                """)
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(cancellationToken);
        if (message is null)
        {
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        workspaceContext.EstablishBackground(message.WorkspaceId);
        message.Lease(lockedUntil);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new OutboxLease(
            message.Id,
            message.WorkspaceId,
            message.Type,
            message.PayloadJson,
            message.AttemptCount,
            message.OccurredAt);
    }

    public async Task MarkProcessedAsync(
        Guid messageId,
        DateTimeOffset processedAt,
        CancellationToken cancellationToken)
    {
        var message = await FindForProcessingAsync(messageId, cancellationToken);
        message.MarkProcessed(processedAt);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkPublishFailureAsync(
        Guid messageId,
        DateTimeOffset failedAt,
        DateTimeOffset nextAttemptAt,
        int maximumAttempts,
        string errorCode,
        CancellationToken cancellationToken)
    {
        var message = await FindForProcessingAsync(messageId, cancellationToken);
        message.MarkPublishFailure(
            failedAt,
            nextAttemptAt,
            maximumAttempts,
            errorCode);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<OutboxMessage?> FindCurrentAsync(
        Guid messageId,
        CancellationToken cancellationToken) => dbContext.OutboxMessages.SingleOrDefaultAsync(
            message => message.Id == messageId,
            cancellationToken);

    public Task<long> CountBacklogAsync(CancellationToken cancellationToken) =>
        dbContext.OutboxMessages
            .IgnoreQueryFilters()
            .LongCountAsync(
                message => message.Status == OutboxMessageStatus.Pending ||
                           message.Status == OutboxMessageStatus.Processing,
                cancellationToken);

    private async Task<OutboxMessage> FindForProcessingAsync(
        Guid messageId,
        CancellationToken cancellationToken) =>
        dbContext.OutboxMessages.Local.SingleOrDefault(message => message.Id == messageId)
        ?? await dbContext.OutboxMessages
            .IgnoreQueryFilters()
            .SingleAsync(message => message.Id == messageId, cancellationToken);
}
