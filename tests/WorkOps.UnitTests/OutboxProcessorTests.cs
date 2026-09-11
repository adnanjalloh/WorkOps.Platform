using Microsoft.Extensions.Logging;
using WorkOps.Application.Abstractions;
using WorkOps.Application.Messaging;
using WorkOps.Domain;
using WorkOps.Domain.Messaging;

namespace WorkOps.UnitTests;

[TestClass]
public sealed class OutboxProcessorTests
{
    [TestMethod]
    public async Task Successful_publish_marks_the_message_processed()
    {
        var now = new DateTimeOffset(2026, 8, 1, 12, 0, 0, TimeSpan.Zero);
        var store = new RecordingOutboxStore(CreateLease(attemptCount: 1));
        var processor = new OutboxProcessor(
            store,
            new SuccessfulPublisher(),
            new FixedTimeProvider(now),
            new RecordingLogger<OutboxProcessor>());

        var result = await processor.ProcessNextAsync(CancellationToken.None);

        Assert.AreEqual(OutboxProcessResult.Published, result);
        Assert.IsTrue(store.Processed);
        Assert.IsFalse(store.PublishFailed);
    }

    [TestMethod]
    public async Task Publish_failure_records_only_a_safe_error_code_and_schedules_retry()
    {
        var now = new DateTimeOffset(2026, 8, 1, 12, 0, 0, TimeSpan.Zero);
        const string sentinel = "TOKEN=secret-token;CONNECTION=secret-connection";
        var lease = CreateLease(
            attemptCount: 2,
            type: sentinel,
            payloadJson: $"{{\"payload\":\"{sentinel}\"}}");
        var store = new RecordingOutboxStore(lease);
        var logger = new RecordingLogger<OutboxProcessor>();
        var processor = new OutboxProcessor(
            store,
            new FailingPublisher(sentinel),
            new FixedTimeProvider(now),
            logger);

        var result = await processor.ProcessNextAsync(CancellationToken.None);

        Assert.AreEqual(OutboxProcessResult.RetryScheduled, result);
        Assert.IsTrue(store.PublishFailed);
        Assert.AreEqual("transport_publish_failed", store.ErrorCode);
        Assert.IsGreaterThan(now, store.NextAttemptAt);
        Assert.HasCount(1, logger.Messages);
        StringAssert.Contains(logger.Messages[0], lease.Id.ToString());
        StringAssert.Contains(logger.Messages[0], "unknown");
        StringAssert.Contains(logger.Messages[0], OutboxProcessResult.RetryScheduled.ToString());
        StringAssert.Contains(logger.Messages[0], "invalid_operation");
        Assert.IsFalse(logger.Messages[0].Contains(sentinel, StringComparison.Ordinal));
        Assert.IsNull(logger.Exceptions[0]);
    }

    [TestMethod]
    public void Retry_delay_is_deterministic_and_bounded()
    {
        var messageId = Guid.Parse("00112233-4455-6677-8899-aabbccddeeff");

        var first = OutboxRetryPolicy.GetDelay(messageId, 1);
        var repeated = OutboxRetryPolicy.GetDelay(messageId, 1);
        var final = OutboxRetryPolicy.GetDelay(messageId, 50);

        Assert.AreEqual(first, repeated);
        Assert.IsGreaterThanOrEqualTo(TimeSpan.FromSeconds(1), first);
        Assert.IsLessThan(TimeSpan.FromSeconds(2), first);
        Assert.IsGreaterThanOrEqualTo(TimeSpan.FromSeconds(16), final);
        Assert.IsLessThan(TimeSpan.FromSeconds(17), final);
    }

    [TestMethod]
    public async Task Empty_queue_does_not_publish_or_change_state()
    {
        var store = new RecordingOutboxStore(null);
        var publisher = new SuccessfulPublisher();
        var processor = new OutboxProcessor(store, publisher, TimeProvider.System,
            new RecordingLogger<OutboxProcessor>());

        Assert.AreEqual(OutboxProcessResult.NoMessage, await processor.ProcessNextAsync(CancellationToken.None));
        Assert.AreEqual(0, publisher.PublishCount);
        Assert.IsFalse(store.Processed);
        Assert.IsFalse(store.PublishFailed);
    }

    [TestMethod]
    public async Task Caller_cancellation_preserves_the_lease_for_recovery_without_recording_failure()
    {
        using var cancellation = new CancellationTokenSource();
        var store = new RecordingOutboxStore(CreateLease(1));
        var logger = new RecordingLogger<OutboxProcessor>();
        var processor = new OutboxProcessor(store, new CancellingPublisher(cancellation), TimeProvider.System, logger);

        await Assert.ThrowsAsync<OperationCanceledException>(() => processor.ProcessNextAsync(cancellation.Token));

        Assert.IsFalse(store.Processed);
        Assert.IsFalse(store.PublishFailed);
        Assert.IsEmpty(logger.Messages);
    }

    [TestMethod]
    public async Task Transport_cancellation_without_caller_cancellation_schedules_a_retry()
    {
        var store = new RecordingOutboxStore(CreateLease(1));
        var logger = new RecordingLogger<OutboxProcessor>();
        var processor = new OutboxProcessor(store,
            new ThrowingPublisher(new OperationCanceledException("synthetic transport cancellation")),
            TimeProvider.System, logger);

        Assert.AreEqual(OutboxProcessResult.RetryScheduled, await processor.ProcessNextAsync(CancellationToken.None));
        Assert.IsTrue(store.PublishFailed);
        StringAssert.Contains(logger.Messages.Single(), "transport_error");
    }

    [TestMethod]
    public async Task Exhausted_publish_attempts_record_terminal_failure()
    {
        var store = new RecordingOutboxStore(CreateLease(OutboxRetryPolicy.MaximumAttempts));
        var logger = new RecordingLogger<OutboxProcessor>();
        var processor = new OutboxProcessor(store, new ThrowingPublisher(new TimeoutException()),
            TimeProvider.System, logger);

        Assert.AreEqual(OutboxProcessResult.Failed, await processor.ProcessNextAsync(CancellationToken.None));
        Assert.AreEqual(OutboxRetryPolicy.MaximumAttempts, store.MaximumAttempts);
        Assert.IsTrue(store.PublishFailed);
        Assert.IsFalse(store.Processed);
        StringAssert.Contains(logger.Messages.Single(), "timeout");
    }

    [TestMethod]
    public async Task Published_message_is_retried_when_completion_cannot_be_persisted()
    {
        var store = new RecordingOutboxStore(CreateLease(1))
        {
            CompletionFailure = new IOException("synthetic database failure"),
        };
        var publisher = new SuccessfulPublisher();
        var processor = new OutboxProcessor(store, publisher, TimeProvider.System,
            new RecordingLogger<OutboxProcessor>());

        Assert.AreEqual(OutboxProcessResult.RetryScheduled, await processor.ProcessNextAsync(CancellationToken.None));
        Assert.AreEqual(1, publisher.PublishCount);
        Assert.IsFalse(store.Processed);
        Assert.IsTrue(store.PublishFailed);
        Assert.AreEqual("transport_publish_failed", store.ErrorCode);
    }

    [TestMethod]
    public async Task Failure_to_persist_retry_is_propagated_for_worker_recovery()
    {
        var failure = new IOException("synthetic retry persistence failure");
        var store = new RecordingOutboxStore(CreateLease(1)) { RetryFailure = failure };
        var processor = new OutboxProcessor(store, new ThrowingPublisher(new TimeoutException()),
            TimeProvider.System, new RecordingLogger<OutboxProcessor>());

        var actual = await Assert.ThrowsExactlyAsync<IOException>(() => processor.ProcessNextAsync(CancellationToken.None));
        Assert.AreSame(failure, actual);
        Assert.IsFalse(store.Processed);
        Assert.IsFalse(store.PublishFailed);
    }

    private static OutboxLease CreateLease(
        int attemptCount,
        string type = WorkItemStatusChangedMessage.MessageType,
        string payloadJson = "{}") => new(
        Guid.Parse("00112233-4455-6677-8899-aabbccddeeff"),
        WorkspaceId.New(),
        type,
        payloadJson,
        attemptCount,
        new DateTimeOffset(2026, 8, 1, 11, 0, 0, TimeSpan.Zero));

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class SuccessfulPublisher : IMessagePublisher
    {
        public int PublishCount { get; private set; }

        public Task PublishAsync(OutboxLease message, CancellationToken cancellationToken)
        {
            _ = message;
            PublishCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingPublisher(Exception exception) : IMessagePublisher
    {
        public Task PublishAsync(OutboxLease message, CancellationToken cancellationToken) => Task.FromException(exception);
    }

    private sealed class CancellingPublisher(CancellationTokenSource cancellation) : IMessagePublisher
    {
        public Task PublishAsync(OutboxLease message, CancellationToken cancellationToken)
        {
            cancellation.Cancel();
            return Task.FromCanceled(cancellationToken);
        }
    }

    private sealed class FailingPublisher(string sentinel) : IMessagePublisher
    {
        public Task PublishAsync(OutboxLease message, CancellationToken cancellationToken)
        {
            _ = message;
            return Task.FromException(new InvalidOperationException(sentinel));
        }
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public List<Exception?> Exceptions { get; } = [];

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
            Messages.Add(formatter(state, exception));
            Exceptions.Add(exception);
        }
    }

    private sealed class RecordingOutboxStore(OutboxLease? lease) : IOutboxStore
    {
        private bool _leased;

        public bool Processed { get; private set; }

        public bool PublishFailed { get; private set; }

        public string? ErrorCode { get; private set; }

        public DateTimeOffset NextAttemptAt { get; private set; }

        public int MaximumAttempts { get; private set; }

        public Exception? CompletionFailure { get; init; }

        public Exception? RetryFailure { get; init; }

        public void Add(OutboxMessage message) => throw new NotSupportedException();

        public Task<OutboxLease?> LeaseNextAsync(
            DateTimeOffset now,
            DateTimeOffset lockedUntil,
            CancellationToken cancellationToken)
        {
            _ = now;
            _ = lockedUntil;
            if (_leased)
            {
                return Task.FromResult<OutboxLease?>(null);
            }

            _leased = true;
            return Task.FromResult<OutboxLease?>(lease);
        }

        public Task MarkProcessedAsync(
            Guid messageId,
            DateTimeOffset processedAt,
            CancellationToken cancellationToken)
        {
            _ = messageId;
            _ = processedAt;
            if (CompletionFailure is not null)
            {
                return Task.FromException(CompletionFailure);
            }

            Processed = true;
            return Task.CompletedTask;
        }

        public Task MarkPublishFailureAsync(
            Guid messageId,
            DateTimeOffset failedAt,
            DateTimeOffset nextAttemptAt,
            int maximumAttempts,
            string errorCode,
            CancellationToken cancellationToken)
        {
            _ = messageId;
            _ = failedAt;
            if (RetryFailure is not null)
            {
                return Task.FromException(RetryFailure);
            }

            MaximumAttempts = maximumAttempts;
            PublishFailed = true;
            NextAttemptAt = nextAttemptAt;
            ErrorCode = errorCode;
            return Task.CompletedTask;
        }

        public Task<OutboxMessage?> FindCurrentAsync(
            Guid messageId,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<long> CountBacklogAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
