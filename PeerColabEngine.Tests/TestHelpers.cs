using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PeerColabEngine.Tests
{
    // Concrete operation types for testing
    public class TestDto
    {
        public string Name { get; set; }
        public int Value { get; set; }
    }

    public class TestResultDto
    {
        public string Result { get; set; }
        public bool Processed { get; set; }
    }

    public class GetTestOperation : RequestOperation<TestDto, TestResultDto>
    {
        public static readonly GetTestOperation Instance = new GetTestOperation();

        private GetTestOperation()
            : base("test.get", "GET") { }
    }

    public class CreateTestOperation : RequestOperation<TestDto, TestResultDto>
    {
        public static readonly CreateTestOperation Instance = new CreateTestOperation();

        private CreateTestOperation()
            : base("test.create", "CREATE") { }
    }

    public class NotifyTestOperation : MessageOperation<TestDto>
    {
        public static readonly NotifyTestOperation Instance = new NotifyTestOperation();

        private NotifyTestOperation()
            : base("test.notify", "PROCESS") { }
    }

    public class PatternTestOperation : RequestOperation<TestDto, TestResultDto>
    {
        public static readonly PatternTestOperation Instance = new PatternTestOperation();

        private PatternTestOperation()
            : base("myservice.items.get", "GET") { }
    }

    // A logger that captures messages for assertions
    public class TestLogger : TransportAbstractionLogger
    {
        public LogLevel LogLevel { get; set; } = LogLevel.Trace;
        public List<LogMessage> Messages { get; } = new List<LogMessage>();

        public void Write(LogMessage message)
        {
            if (message.IsWithin(LogLevel))
                Messages.Add(message);
        }

        public void Clear() => Messages.Clear();
    }

    // A context cache that can be configured to fail
    public class FailingContextCache : ContextCache
    {
        public Task<bool> Put(Guid transactionId, CallInformation ctx)
        {
            return Task.FromResult(false);
        }

        public Task<CallInformation> Get(Guid transactionId)
        {
            return Task.FromResult<CallInformation>(null);
        }
    }

    public class ThrowingContextCache : ContextCache
    {
        public Task<bool> Put(Guid transactionId, CallInformation ctx)
        {
            throw new InvalidOperationException("Cache put failed");
        }

        public Task<CallInformation> Get(Guid transactionId)
        {
            throw new InvalidOperationException("Cache get failed");
        }
    }
}
