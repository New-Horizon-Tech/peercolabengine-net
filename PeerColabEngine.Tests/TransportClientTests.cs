using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace PeerColabEngine.Tests
{
    public class TransportClientTests
    {
        private (TransportSession session, TransportClient client) CreateSessionAndClient()
        {
            var handler = GetTestOperation.Instance.Handle(async (input, ctx) =>
            {
                return Result<TestResultDto>.Ok(new TestResultDto
                {
                    Result = input.Name,
                    Processed = true
                });
            });

            var msgHandler = NotifyTestOperation.Instance.Handle(async (input, ctx) =>
            {
                return Result<object>.Ok();
            });

            var session = Transport.Session("test-session")
                .Intercept(handler)
                .Intercept(msgHandler)
                .Build();

            return (session, session.CreateClient("client1"));
        }

        [Fact]
        public void TransportClient_WithLocale_ReturnsNewClient()
        {
            var (_, client) = CreateSessionAndClient();
            var newClient = client.WithLocale("nb-NO");

            Assert.NotNull(newClient);
            Assert.NotSame(client, newClient);
        }

        [Fact]
        public void TransportClient_WithDataTenant_ReturnsNewClient()
        {
            var (_, client) = CreateSessionAndClient();
            var newClient = client.WithDataTenant("my-tenant");

            Assert.NotNull(newClient);
            Assert.NotSame(client, newClient);
        }

        [Fact]
        public void TransportClient_WithCharacters_ReturnsNewClient()
        {
            var (_, client) = CreateSessionAndClient();
            var chars = CharacterMetaValues.FromPerformer("user", "1");
            var newClient = client.WithCharacters(chars);

            Assert.NotNull(newClient);
            Assert.NotSame(client, newClient);
        }

        [Fact]
        public void TransportClient_AddAttribute_ReturnsNewClient()
        {
            var (_, client) = CreateSessionAndClient();
            var newClient = client.AddAttribute("key", "value");

            Assert.NotNull(newClient);
            Assert.NotSame(client, newClient);
        }

        [Fact]
        public void TransportClient_RemoveAttribute_ReturnsNewClient()
        {
            var (_, client) = CreateSessionAndClient();
            var withAttr = client.AddAttribute("key", "value");
            var withoutAttr = withAttr.RemoveAttribute("key");

            Assert.NotNull(withoutAttr);
            Assert.NotSame(withAttr, withoutAttr);
        }

        [Fact]
        public void TransportClient_AddPathParam_ReturnsNewClient()
        {
            var (_, client) = CreateSessionAndClient();
            var newClient = client.AddPathParam("id", "123");

            Assert.NotNull(newClient);
            Assert.NotSame(client, newClient);
        }

        [Fact]
        public void TransportClient_RemovePathParam_ReturnsNewClient()
        {
            var (_, client) = CreateSessionAndClient();
            var withParam = client.AddPathParam("id", "123");
            var withoutParam = withParam.RemovePathParam("id");

            Assert.NotNull(withoutParam);
            Assert.NotSame(withParam, withoutParam);
        }

        [Fact]
        public async Task TransportClient_WithTransactionId_ReturnsNewClient()
        {
            var (_, client) = CreateSessionAndClient();
            var txId = Guid.NewGuid();
            var newClient = await client.WithTransactionId(txId);

            Assert.NotNull(newClient);
            Assert.NotSame(client, newClient);
        }

        [Fact]
        public async Task TransportClient_Request_SendsRequestOperation()
        {
            var (_, client) = CreateSessionAndClient();
            var request = new RequestOperationRequest<TestDto, TestResultDto>(
                "usage1",
                GetTestOperation.Instance,
                new TestDto { Name = "hello", Value = 42 }
            );

            var result = await client.Request(request);

            Assert.True(result.Success);
            Assert.Equal("hello", result.Value.Result);
            Assert.True(result.Value.Processed);
        }

        [Fact]
        public async Task TransportClient_Request_SendsMessageOperation()
        {
            var (_, client) = CreateSessionAndClient();
            var request = new MessageOperationRequest<TestDto>(
                "usage1",
                NotifyTestOperation.Instance,
                new TestDto { Name = "notify", Value = 1 }
            );

            var result = await client.Request(request);

            Assert.True(result.Success);
        }

        [Fact]
        public async Task TransportClient_Request_WithLocaleAndTenant()
        {
            string capturedLocale = null;
            string capturedTenant = null;

            var handler = GetTestOperation.Instance.Handle(async (input, ctx) =>
            {
                capturedLocale = ctx.Call.Locale;
                capturedTenant = ctx.Call.DataTenant;
                return Result<TestResultDto>.Ok(new TestResultDto());
            });

            var session = Transport.Session("test")
                .Intercept(handler)
                .Build();

            var client = session.CreateClient("client1")
                .WithLocale("nb-NO")
                .WithDataTenant("my-tenant");

            var request = new RequestOperationRequest<TestDto, TestResultDto>(
                "u1", GetTestOperation.Instance, new TestDto { Name = "test" }
            );

            await client.Request(request);

            Assert.Equal("nb-NO", capturedLocale);
            Assert.Equal("my-tenant", capturedTenant);
        }

        [Fact]
        public async Task TransportClient_Request_WithAttributes()
        {
            string capturedAttr = null;

            var handler = GetTestOperation.Instance.Handle(async (input, ctx) =>
            {
                capturedAttr = ctx.GetAttribute<string>("myAttr");
                return Result<TestResultDto>.Ok(new TestResultDto());
            });

            var session = Transport.Session("test")
                .Intercept(handler)
                .Build();

            var client = session.CreateClient("client1")
                .AddAttribute("myAttr", "myValue");

            var request = new RequestOperationRequest<TestDto, TestResultDto>(
                "u1", GetTestOperation.Instance, new TestDto { Name = "test" }
            );

            await client.Request(request);

            Assert.Equal("myValue", capturedAttr);
        }

        [Fact]
        public async Task TransportClient_Request_WithPathParams()
        {
            string capturedParam = null;

            var handler = GetTestOperation.Instance.Handle(async (input, ctx) =>
            {
                capturedParam = ctx.GetPathParameter<string>("itemId");
                return Result<TestResultDto>.Ok(new TestResultDto());
            });

            var session = Transport.Session("test")
                .Intercept(handler)
                .Build();

            var client = session.CreateClient("client1")
                .AddPathParam("itemId", "abc-123");

            var request = new RequestOperationRequest<TestDto, TestResultDto>(
                "u1", GetTestOperation.Instance, new TestDto { Name = "test" }
            );

            await client.Request(request);

            Assert.Equal("abc-123", capturedParam);
        }

        [Fact]
        public async Task TransportClient_Request_WithCharacters()
        {
            ICharacters capturedChars = null;

            var handler = GetTestOperation.Instance.Handle(async (input, ctx) =>
            {
                capturedChars = ctx.Call.Characters;
                return Result<TestResultDto>.Ok(new TestResultDto());
            });

            var session = Transport.Session("test")
                .Intercept(handler)
                .Build();

            var chars = CharacterMetaValues.FromPerformer("user", "u1")
                .WithSubject("entity", "e1");

            var client = session.CreateClient("client1")
                .WithCharacters(chars);

            var request = new RequestOperationRequest<TestDto, TestResultDto>(
                "u1", GetTestOperation.Instance, new TestDto { Name = "test" }
            );

            await client.Request(request);

            Assert.NotNull(capturedChars);
            Assert.Equal("user", capturedChars.Performer.Type);
            Assert.Equal("entity", capturedChars.Subject.Type);
        }

        [Fact]
        public async Task TransportClient_AddAttribute_UpdatesExistingAttribute()
        {
            string capturedAttr = null;

            var handler = GetTestOperation.Instance.Handle(async (input, ctx) =>
            {
                capturedAttr = ctx.GetAttribute<string>("key");
                return Result<TestResultDto>.Ok(new TestResultDto());
            });

            var session = Transport.Session("test")
                .Intercept(handler)
                .Build();

            var client = session.CreateClient("client1")
                .AddAttribute("key", "old")
                .AddAttribute("key", "new");

            var request = new RequestOperationRequest<TestDto, TestResultDto>(
                "u1", GetTestOperation.Instance, new TestDto { Name = "test" }
            );

            await client.Request(request);

            Assert.Equal("new", capturedAttr);
        }

        [Fact]
        public async Task TransportClient_AddPathParam_UpdatesExistingParam()
        {
            string capturedParam = null;

            var handler = GetTestOperation.Instance.Handle(async (input, ctx) =>
            {
                capturedParam = ctx.GetPathParameter<string>("id");
                return Result<TestResultDto>.Ok(new TestResultDto());
            });

            var session = Transport.Session("test")
                .Intercept(handler)
                .Build();

            var client = session.CreateClient("client1")
                .AddPathParam("id", "old")
                .AddPathParam("id", "new");

            var request = new RequestOperationRequest<TestDto, TestResultDto>(
                "u1", GetTestOperation.Instance, new TestDto { Name = "test" }
            );

            await client.Request(request);

            Assert.Equal("new", capturedParam);
        }

        [Fact]
        public async Task TransportClient_AcceptOperationAsync_Request()
        {
            var handler = GetTestOperation.Instance.Handle(async (input, ctx) =>
            {
                return Result<TestResultDto>.Ok(new TestResultDto { Result = "ok", Processed = true });
            });

            var session = Transport.Session("test")
                .Intercept(handler)
                .Build();

            var client = session.CreateClient("client1");

            var operation = new OutOfContextOperation
            {
                UsageId = "u1",
                OperationId = "test.get",
                OperationVerb = "GET",
                OperationType = "request",
                RequestJson = new TestDto { Name = "oop", Value = 1 },
                PathParameters = new List<OutOfContextOperationPathParameter>
                {
                    new OutOfContextOperationPathParameter { Name = "id", Value = "123" }
                }
            };

            var result = await client.AcceptOperationAsync(operation);

            Assert.True(result.Success);
        }

        [Fact]
        public async Task TransportClient_AcceptOperationAsync_Message()
        {
            var handler = NotifyTestOperation.Instance.Handle(async (input, ctx) =>
            {
                return Result<object>.Ok();
            });

            var session = Transport.Session("test")
                .Intercept(handler)
                .Build();

            var client = session.CreateClient("client1");

            var operation = new OutOfContextOperation
            {
                UsageId = "u1",
                OperationId = "test.notify",
                OperationVerb = "PROCESS",
                OperationType = "message",
                RequestJson = new TestDto { Name = "msg" }
            };

            var result = await client.AcceptOperationAsync(operation);

            Assert.True(result.Success);
        }

        [Fact]
        public async Task TransportClient_AcceptOperationAsync_WithCustomAttributes()
        {
            string capturedAttr = null;

            var handler = GetTestOperation.Instance.Handle(async (input, ctx) =>
            {
                capturedAttr = ctx.GetAttribute<string>("custom");
                return Result<TestResultDto>.Ok(new TestResultDto());
            });

            var session = Transport.Session("test")
                .Intercept(handler)
                .Build();

            var client = session.CreateClient("client1");

            var operation = new OutOfContextOperation
            {
                UsageId = "u1",
                OperationId = "test.get",
                OperationVerb = "GET",
                OperationType = "request",
                RequestJson = new TestDto { Name = "test" }
            };

            var attrs = new List<Attribute> { new Attribute("custom", "value") };
            await client.AcceptOperationAsync(operation, attrs);

            Assert.Equal("value", capturedAttr);
        }

        [Fact]
        public async Task TransportClient_AcceptOperationAsync_WithPathParameters()
        {
            string capturedParam = null;

            var handler = GetTestOperation.Instance.Handle(async (input, ctx) =>
            {
                capturedParam = ctx.GetPathParameter<string>("entityId");
                return Result<TestResultDto>.Ok(new TestResultDto());
            });

            var session = Transport.Session("test")
                .Intercept(handler)
                .Build();

            var client = session.CreateClient("client1");

            var operation = new OutOfContextOperation
            {
                UsageId = "u1",
                OperationId = "test.get",
                OperationVerb = "GET",
                OperationType = "request",
                RequestJson = new TestDto { Name = "test" },
                PathParameters = new List<OutOfContextOperationPathParameter>
                {
                    new OutOfContextOperationPathParameter { Name = "entityId", Value = "abc" }
                }
            };

            await client.AcceptOperationAsync(operation);

            Assert.Equal("abc", capturedParam);
        }

        [Fact]
        public async Task TransportClient_Immutability_DoesNotShareState()
        {
            string locale1 = null, locale2 = null;
            int callCount = 0;

            var handler = GetTestOperation.Instance.Handle(async (input, ctx) =>
            {
                callCount++;
                if (callCount == 1) locale1 = ctx.Call.Locale;
                else locale2 = ctx.Call.Locale;
                return Result<TestResultDto>.Ok(new TestResultDto());
            });

            var session = Transport.Session("test")
                .Intercept(handler)
                .Build();

            var baseClient = session.CreateClient("client1");
            var client1 = baseClient.WithLocale("en-US");
            var client2 = baseClient.WithLocale("nb-NO");

            var request = new RequestOperationRequest<TestDto, TestResultDto>(
                "u1", GetTestOperation.Instance, new TestDto { Name = "test" }
            );

            await client1.Request(request);
            await client2.Request(request);

            Assert.Equal("en-US", locale1);
            Assert.Equal("nb-NO", locale2);
        }
    }
}
