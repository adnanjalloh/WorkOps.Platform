using Microsoft.Extensions.Logging;
using WorkOps.Application.Abstractions;
using WorkOps.Application.Audit;
using WorkOps.Application.Common.Pagination;
using WorkOps.Application.Common.Sanitization;
using WorkOps.Application.Files;
using WorkOps.Application.Tenancy;
using WorkOps.Application.WorkItems;
using WorkOps.Domain;
using WorkOps.Domain.Audit;
using WorkOps.Domain.Files;
using WorkOps.Domain.Tenancy;
using WorkOps.Domain.WorkItems;

namespace WorkOps.UnitTests;

[TestClass]
public sealed class AttachmentServiceTests
{
    [TestMethod]
    public async Task Cleanup_failure_is_logged_without_masking_the_persistence_failure()
    {
        var persistenceFailure = new InjectedPersistenceException();
        var cleanupFailure = new IOException("injected cleanup failure");
        var storage = new RecordingFileStorage { DeleteFailure = cleanupFailure };
        var logger = new RecordingLogger<AttachmentService>();
        var service = CreateService(
            FileScanResult.Clean,
            storage,
            new RecordingUnitOfWork(persistenceFailure),
            logger);

        var exception = await Assert.ThrowsExactlyAsync<InjectedPersistenceException>(
            () => service.UploadAsync(
                TestWorkItem.Id,
                "notes.txt",
                "text/plain",
                4,
                new MemoryStream("safe"u8.ToArray()),
                CancellationToken.None));

        Assert.AreSame(persistenceFailure, exception);
        Assert.IsTrue(storage.SaveCalled);
        Assert.IsTrue(storage.DeleteCalled);
        CollectionAssert.Contains(logger.Exceptions.ToArray(), cleanupFailure);
    }

    [TestMethod]
    public async Task Scanner_unavailability_fails_closed_before_storage()
    {
        var storage = new RecordingFileStorage();
        var service = CreateService(
            FileScanResult.Unavailable,
            storage,
            new RecordingUnitOfWork(),
            new RecordingLogger<AttachmentService>());

        await Assert.ThrowsExactlyAsync<FileScannerUnavailableException>(
            () => service.UploadAsync(
                TestWorkItem.Id,
                "notes.txt",
                "text/plain",
                4,
                new MemoryStream("safe"u8.ToArray()),
                CancellationToken.None));

        Assert.IsFalse(storage.SaveCalled);
        Assert.IsFalse(storage.DeleteCalled);
    }

    private static readonly WorkspaceId TestWorkspaceId = WorkspaceId.New();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly WorkItem TestWorkItem = WorkItem.Create(
        TestWorkspaceId,
        Guid.NewGuid(),
        "Attachment test",
        WorkItemPriority.Normal,
        UserId,
        [],
        DateTimeOffset.UtcNow);

    private static AttachmentService CreateService(
        FileScanResult scanResult,
        RecordingFileStorage storage,
        IUnitOfWork unitOfWork,
        ILogger<AttachmentService> logger)
    {
        var workspaceContext = new WorkspaceContextAccessor();
        workspaceContext.Establish(new WorkspaceContext(
            UserId,
            TestWorkspaceId,
            WorkspaceRole.Owner,
            WorkspaceStatus.Active));
        var auditWriter = new AuditWriter(
            new RecordingAuditStore(),
            workspaceContext,
            new TestCorrelationContext());

        return new AttachmentService(
            new TestWorkItemStore(),
            new RecordingAttachmentStore(),
            new FixedScanner(scanResult),
            storage,
            unitOfWork,
            workspaceContext,
            auditWriter,
            new InputSanitizer(),
            TimeProvider.System,
            logger);
    }

    private sealed class TestWorkItemStore : IWorkItemStore
    {
        public void Add(WorkItem workItem) => throw new NotSupportedException();

        public Task<WorkItem?> FindAsync(Guid workItemId, CancellationToken cancellationToken)
        {
            _ = cancellationToken;
            return Task.FromResult<WorkItem?>(workItemId == TestWorkItem.Id ? TestWorkItem : null);
        }

        public Task<WorkItemView?> GetAsync(Guid workItemId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class RecordingAttachmentStore : IAttachmentStore
    {
        public Attachment? Added { get; private set; }

        public void Add(Attachment attachment) => Added = attachment;

        public Task<Attachment?> FindAsync(Guid attachmentId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<AttachmentView?> GetAsync(Guid attachmentId, CancellationToken cancellationToken) =>
            Task.FromResult<AttachmentView?>(null);
    }

    private sealed class FixedScanner(FileScanResult result) : IFileScanner
    {
        public Task<FileScanResult> ScanAsync(
            ReadOnlyMemory<byte> content,
            CancellationToken cancellationToken)
        {
            _ = content;
            _ = cancellationToken;
            return Task.FromResult(result);
        }
    }

    private sealed class RecordingFileStorage : IFileStorage
    {
        public Exception? DeleteFailure { get; init; }

        public bool SaveCalled { get; private set; }

        public bool DeleteCalled { get; private set; }

        public Task SaveAsync(
            WorkspaceId workspaceId,
            string storageName,
            ReadOnlyMemory<byte> content,
            CancellationToken cancellationToken)
        {
            _ = workspaceId;
            _ = storageName;
            _ = content;
            _ = cancellationToken;
            SaveCalled = true;
            return Task.CompletedTask;
        }

        public Task<Stream> OpenReadAsync(
            WorkspaceId workspaceId,
            string storageName,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task DeleteAsync(
            WorkspaceId workspaceId,
            string storageName,
            CancellationToken cancellationToken)
        {
            _ = workspaceId;
            _ = storageName;
            _ = cancellationToken;
            DeleteCalled = true;
            return DeleteFailure is null
                ? Task.CompletedTask
                : Task.FromException(DeleteFailure);
        }
    }

    private sealed class RecordingUnitOfWork(Exception? saveFailure = null) : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
        {
            _ = cancellationToken;
            return saveFailure is null
                ? Task.FromResult(1)
                : Task.FromException<int>(saveFailure);
        }

        public Task<T> ExecuteInTransactionAsync<T>(
            Func<CancellationToken, Task<T>> operation,
            CancellationToken cancellationToken) => operation(cancellationToken);
    }

    private sealed class RecordingAuditStore : IAuditStore
    {
        public void Add(AuditEvent auditEvent)
        {
        }

        public Task<PagedResult<AuditEventView>> ListAsync(
            int page,
            int pageSize,
            string? action,
            string? entityType,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class TestCorrelationContext : ICorrelationContext
    {
        public string CorrelationId => "attachment-test";
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<Exception> Exceptions { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            _ = logLevel;
            _ = eventId;
            _ = state;
            _ = formatter;
            if (exception is not null)
            {
                Exceptions.Add(exception);
            }
        }
    }

    private sealed class InjectedPersistenceException : Exception
    {
    }
}
