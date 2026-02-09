using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace PeerColabEngine.Tests
{
    public class OutboundTests
    {
        [Fact]
        public void OutboundSessionBuilder_Build_CreatesFactory()
        {
            var builder = Transport.Session("inbound")
                .OutboundSessionBuilder("outbound-service");

            var factory = builder.Build();
            Assert.NotNull(factory);
        }

        [Fact]
        public void OutboundSessionBuilder_Intercept_AddsHandler()
        {
            var handler = GetTestOperation.Instance.Handle(async (input, ctx) =>
            {
                return Result<TestResultDto>.Ok(new TestResultDto { Result = "outbound" });
            });

            var factory = Transport.Session("inbound")
                .OutboundSessionBuilder("outbound-service")
                .Intercept(handler)
                .Build();

            Assert.NotNull(factory);
        }

        [Fact]
        public void OutboundSessionBuilder_InterceptPattern_AddsPatternHandler()
        {
            var factory = Transport.Session("inbound")
                .OutboundSessionBuilder("outbound")
                .InterceptPattern("prefix.", async (input, ctx) => Result<object>.Ok())
                .Build();

            Assert.NotNull(factory);
        }

        [Fact]
        public void OutboundSessionBuilder_InspectRequest_SetsInspector()
        {
            var factory = Transport.Session("inbound")
                .OutboundSessionBuilder("outbound")
                .InspectRequest(async (input, ctx) => null)
                .Build();

            Assert.NotNull(factory);
        }

        [Fact]
        public void OutboundSessionBuilder_InspectResponse_SetsInspector()
        {
            var factory = Transport.Session("inbound")
                .OutboundSessionBuilder("outbound")
                .InspectResponse(async (result, input, ctx) => result)
                .Build();

            Assert.NotNull(factory);
        }

        [Fact]
        public void OutboundClientFactory_AsIndependentRequests_CreatesClient()
        {
            var handler = GetTestOperation.Instance.Handle(async (input, ctx) =>
            {
                return Result<TestResultDto>.Ok(new TestResultDto());
            });

            var factory = Transport.Session("inbound")
                .OutboundSessionBuilder("outbound")
                .Intercept(handler)
                .Build();

            var client = factory.AsIndependentRequests();
            Assert.NotNull(client);
        }

        [Fact]
        public async Task OutboundClientFactory_ForIncomingRequest_CreatesClient()
        {
            var cache = new InMemoryContextCache();
            var txId = Guid.NewGuid();
            await cache.Put(txId, CallInformation.New("en-GB", "tenant1"));

            var handler = GetTestOperation.Instance.Handle(async (input, ctx) =>
            {
                return Result<TestResultDto>.Ok(new TestResultDto());
            });

            var factory = Transport.Session("inbound")
                .SetupOutboundContextCache(cache)
                .OutboundSessionBuilder("outbound")
                .Intercept(handler)
                .Build();

            var client = await factory.ForIncomingRequest(txId);
            Assert.NotNull(client);
        }

        [Fact]
        public async Task OutboundClientFactory_AsIndependentRequests_CanExecuteRequest()
        {
            var handler = GetTestOperation.Instance.Handle(async (input, ctx) =>
            {
                return Result<TestResultDto>.Ok(new TestResultDto
                {
                    Result = "outbound-result",
                    Processed = true
                });
            });

            var factory = Transport.Session("inbound")
                .OutboundSessionBuilder("outbound")
                .Intercept(handler)
                .Build();

            var client = factory.AsIndependentRequests();
            var request = new RequestOperationRequest<TestDto, TestResultDto>(
                "u1", GetTestOperation.Instance, new TestDto { Name = "test" }
            );

            var result = await client.Request(request);

            Assert.True(result.Success);
            Assert.Equal("outbound-result", result.Value.Result);
            Assert.True(result.Value.Processed);
        }

        [Fact]
        public async Task OutboundClientFactory_MessageHandler()
        {
            var msgHandler = NotifyTestOperation.Instance.Handle(async (input, ctx) =>
            {
                return Result<object>.Ok();
            });

            var factory = Transport.Session("inbound")
                .OutboundSessionBuilder("outbound")
                .Intercept(msgHandler)
                .Build();

            var client = factory.AsIndependentRequests();
            var request = new MessageOperationRequest<TestDto>(
                "u1", NotifyTestOperation.Instance, new TestDto { Name = "msg" }
            );

            var result = await client.Request(request);

            Assert.True(result.Success);
        }

        [Fact]
        public async Task OutboundClientFactory_PatternHandler_Works()
        {
            var factory = Transport.Session("inbound")
                .OutboundSessionBuilder("outbound")
                .InterceptPattern("service.", async (input, ctx) =>
                {
                    return Result<object>.Ok<object>(
                        new TestResultDto { Result = "pattern-matched", Processed = true });
                })
                .Build();

            var client = factory.AsIndependentRequests();

            // Using a custom operation that matches the pattern
            var op = new TransportOperation<TestDto, TestResultDto>(
                "request", "service.items.get", "GET");
            var request = new RequestOperationRequest<TestDto, TestResultDto>(
                "u1", op, new TestDto { Name = "test" }
            );

            var result = await client.Request(request);

            Assert.True(result.Success);
            Assert.True(result.Value.Processed);
        }
    }
}
