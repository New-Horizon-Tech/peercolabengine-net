using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace PeerColabEngine.Tests
{
    public class TransportSessionTests
    {
        private TransportSession BuildSession(
            Action<TransportSessionBuilder> configure = null)
        {
            var builder = Transport.Session("test-session");
            configure?.Invoke(builder);
            return builder.Build();
        }

        [Fact]
        public void Transport_Session_CreatesBuilder()
        {
            var builder = Transport.Session("my-session");
            Assert.NotNull(builder);
        }

        [Fact]
        public void TransportSessionBuilder_Build_CreatesSession()
        {
            var session = BuildSession();
            Assert.NotNull(session);
        }

        [Fact]
        public void TransportSession_GetSerializer_ReturnsSerializer()
        {
            var session = BuildSession();
            var serializer = session.GetSerializer();
            Assert.NotNull(serializer);
        }

        [Fact]
        public void TransportSession_WithLocale_SetsLocale()
        {
            var session = BuildSession();
            var withLocale = session.WithLocale("nb-NO");
            Assert.Same(session, withLocale);
        }

        [Fact]
        public void TransportSession_CreateClient_CreatesClient()
        {
            var session = BuildSession();
            var client = session.CreateClient("client1");
            Assert.NotNull(client);
        }

        [Fact]
        public void TransportSession_CreateClient_WithTenant()
        {
            var session = BuildSession();
            var client = session.CreateClient("client1", "tenant1");
            Assert.NotNull(client);
        }

        [Fact]
        public void TransportSessionBuilder_AssignSerializer_SetsSerializer()
        {
            var customSerializer = new DefaultTransportSerializer();
            var session = Transport.Session("test")
                .AssignSerializer(customSerializer)
                .Build();

            Assert.Same(customSerializer, session.GetSerializer());
        }

        [Fact]
        public void TransportSessionBuilder_SetupOutboundContextCache_SetsCache()
        {
            var cache = new InMemoryContextCache();
            var builder = Transport.Session("test")
                .SetupOutboundContextCache(cache);

            Assert.NotNull(builder);
        }

        [Fact]
        public void TransportSessionBuilder_OnLogMessage_AssignsLogger()
        {
            var logger = new TestLogger();
            Transport.Session("test").OnLogMessage(logger);
            logger.Clear();

            Logger.Info("test from session builder");
            Assert.Contains(logger.Messages, m => m.Message == "test from session builder");
        }

        [Fact]
        public async Task TransportSession_AcceptIncomingRequest_WithHandler()
        {
            var handler = GetTestOperation.Instance.Handle(async (input, ctx) =>
            {
                return Result<TestResultDto>.Ok(new TestResultDto
                {
                    Result = input.Name,
                    Processed = true
                });
            });

            var session = Transport.Session("test-session")
                .Intercept(handler)
                .Build();

            var serializer = session.GetSerializer();
            var request = new TransportRequest<TestDto>(
                "test.get", "GET", "request", "caller", "u1",
                Guid.NewGuid(), "tenant", "en-GB",
                new Characters(),
                new List<Attribute>(),
                new List<Attribute>(),
                new TestDto { Name = "hello", Value = 1 }
            ).AssignSerializer(serializer);

            var json = request.Serialize();
            var result = await session.AcceptIncomingRequest(json);

            Assert.True(result.Success);
            Assert.Equal(200, result.StatusCode);
        }

        [Fact]
        public async Task TransportSession_AcceptIncomingRequest_MessageHandler()
        {
            var handler = NotifyTestOperation.Instance.Handle(async (input, ctx) =>
            {
                return Result<object>.Ok();
            });

            var session = Transport.Session("test-session")
                .Intercept(handler)
                .Build();

            var serializer = session.GetSerializer();
            var request = new TransportRequest<TestDto>(
                "test.notify", "PROCESS", "message", "caller", "u1",
                Guid.NewGuid(), "tenant", "en-GB",
                new Characters(),
                new List<Attribute>(),
                new List<Attribute>(),
                new TestDto { Name = "notify", Value = 1 }
            ).AssignSerializer(serializer);

            var json = request.Serialize();
            var result = await session.AcceptIncomingRequest(json);

            Assert.True(result.Success);
        }

        [Fact]
        public async Task TransportSession_AcceptIncomingRequest_WithTransportRequest()
        {
            var handler = GetTestOperation.Instance.Handle(async (input, ctx) =>
            {
                return Result<TestResultDto>.Ok(new TestResultDto { Result = "ok" });
            });

            var session = Transport.Session("test-session")
                .Intercept(handler)
                .Build();

            var serializer = session.GetSerializer();
            var request = new TransportRequest<object>(
                "test.get", "GET", "request", "caller", "u1",
                Guid.NewGuid(), "tenant", "en-GB",
                new Characters(),
                new List<Attribute>(),
                new List<Attribute>(),
                new TestDto { Name = "hello" }
            ).AssignSerializer(serializer);

            var result = await session.AcceptIncomingRequest(request);

            Assert.True(result.Success);
        }

        [Fact]
        public async Task TransportSession_AcceptIncomingRequest_WithCustomAttributes()
        {
            string capturedAttr = null;
            var handler = GetTestOperation.Instance.Handle(async (input, ctx) =>
            {
                capturedAttr = ctx.GetAttribute<string>("custom");
                return Result<TestResultDto>.Ok(new TestResultDto());
            });

            var session = Transport.Session("test-session")
                .Intercept(handler)
                .Build();

            var serializer = session.GetSerializer();
            var request = new TransportRequest<object>(
                "test.get", "GET", "request", "caller", "u1",
                Guid.NewGuid(), "", "en-GB",
                new Characters(),
                new List<Attribute>(),
                new List<Attribute>(),
                new TestDto { Name = "test" }
            ).AssignSerializer(serializer);

            var customAttrs = new List<Attribute> { new Attribute("custom", "injected") };
            await session.AcceptIncomingRequest(request, customAttrs);

            Assert.Equal("injected", capturedAttr);
        }

        [Fact]
        public async Task TransportSession_AcceptIncomingRequest_CustomAttributeDoesNotOverride()
        {
            string capturedAttr = null;
            var handler = GetTestOperation.Instance.Handle(async (input, ctx) =>
            {
                capturedAttr = ctx.GetAttribute<string>("existing");
                return Result<TestResultDto>.Ok(new TestResultDto());
            });

            var session = Transport.Session("test-session")
                .Intercept(handler)
                .Build();

            var serializer = session.GetSerializer();
            var request = new TransportRequest<object>(
                "test.get", "GET", "request", "caller", "u1",
                Guid.NewGuid(), "", "en-GB",
                new Characters(),
                new List<Attribute> { new Attribute("existing", "original") },
                new List<Attribute>(),
                new TestDto()
            ).AssignSerializer(serializer);

            var customAttrs = new List<Attribute> { new Attribute("existing", "overridden") };
            await session.AcceptIncomingRequest(request, customAttrs);

            Assert.Equal("original", capturedAttr);
        }

        [Fact]
        public async Task TransportSession_AcceptIncomingRequest_NoHandler_ReturnsBadRequest()
        {
            var session = Transport.Session("test-session").Build();
            var serializer = session.GetSerializer();

            var request = new TransportRequest<object>(
                "unknown.op", "GET", "request", "caller", "u1",
                Guid.NewGuid(), "", "en-GB",
                new Characters(),
                new List<Attribute>(),
                new List<Attribute>(),
                null
            ).AssignSerializer(serializer);

            var result = await session.AcceptIncomingRequest(request);

            Assert.False(result.Success);
            Assert.Equal(400, result.StatusCode);
        }

        [Fact]
        public void TransportSessionBuilder_InterceptPattern_AddsPatternHandler()
        {
            var builder = Transport.Session("test")
                .InterceptPattern("myservice.", async (input, ctx) =>
                {
                    return Result<object>.Ok<object>("handled");
                });

            Assert.NotNull(builder);
        }

        [Fact]
        public async Task TransportSession_PatternHandler_MatchesPrefix()
        {
            var session = Transport.Session("test-session")
                .InterceptPattern("myservice.", async (input, ctx) =>
                {
                    return Result<object>.Ok<object>("pattern-matched");
                })
                .Build();

            var serializer = session.GetSerializer();
            var request = new TransportRequest<object>(
                "myservice.items.get", "GET", "request", "caller", "u1",
                Guid.NewGuid(), "", "en-GB",
                new Characters(),
                new List<Attribute>(),
                new List<Attribute>(),
                null
            ).AssignSerializer(serializer);

            var result = await session.AcceptIncomingRequest(request);

            Assert.True(result.Success);
        }

        [Fact]
        public void TransportSessionBuilder_InspectRequest_SetsInspector()
        {
            var builder = Transport.Session("test")
                .InspectRequest(async (input, ctx) =>
                {
                    return null;
                });

            Assert.NotNull(builder);
        }

        [Fact]
        public void TransportSessionBuilder_InspectResponse_SetsInspector()
        {
            var builder = Transport.Session("test")
                .InspectResponse(async (result, input, ctx) =>
                {
                    return result;
                });

            Assert.NotNull(builder);
        }

        [Fact]
        public async Task TransportSession_RequestInspector_CanShortCircuit()
        {
            var session = Transport.Session("test-session")
                .Intercept(GetTestOperation.Instance.Handle(async (input, ctx) =>
                {
                    return Result<TestResultDto>.Ok(new TestResultDto { Result = "should-not-reach" });
                }))
                .InspectRequest(async (input, ctx) =>
                {
                    return Result<object>.BadRequest("BLOCKED", "Blocked by inspector");
                })
                .Build();

            var serializer = session.GetSerializer();
            var request = new TransportRequest<object>(
                "test.get", "GET", "request", "caller", "u1",
                Guid.NewGuid(), "", "en-GB",
                new Characters(),
                new List<Attribute>(),
                new List<Attribute>(),
                new TestDto { Name = "blocked" }
            ).AssignSerializer(serializer);

            var result = await session.AcceptIncomingRequest(request);

            Assert.False(result.Success);
            Assert.Equal(400, result.StatusCode);
            Assert.Equal("BLOCKED", result.Error.Code);
        }

        [Fact]
        public async Task TransportSession_ResponseInspector_ModifiesResponse()
        {
            var session = Transport.Session("test-session")
                .Intercept(GetTestOperation.Instance.Handle(async (input, ctx) =>
                {
                    return Result<TestResultDto>.Ok(new TestResultDto { Result = "original" });
                }))
                .InspectResponse(async (result, input, ctx) =>
                {
                    result.WithMeta(m => m.WithAttribute("inspected", true));
                    return result;
                })
                .Build();

            var serializer = session.GetSerializer();
            var request = new TransportRequest<object>(
                "test.get", "GET", "request", "caller", "u1",
                Guid.NewGuid(), "", "en-GB",
                new Characters(),
                new List<Attribute>(),
                new List<Attribute>(),
                new TestDto { Name = "test" }
            ).AssignSerializer(serializer);

            var result = await session.AcceptIncomingRequest(request);

            Assert.True(result.Success);
            Assert.True(result.Meta.HasAttribute("inspected"));
        }

        [Fact]
        public void TransportSessionBuilder_OutboundSessionBuilder_Creates()
        {
            var builder = Transport.Session("test")
                .OutboundSessionBuilder("outbound-service");

            Assert.NotNull(builder);
        }
    }
}
