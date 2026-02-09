using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace PeerColabEngine.Tests
{
    public class IntegrationTests
    {
        [Fact]
        public async Task EndToEnd_InboundRequestHandling()
        {
            // Setup a full session with a request handler
            var session = Transport.Session("my-service")
                .Intercept(GetTestOperation.Instance.Handle(async (input, ctx) =>
                {
                    return Result<TestResultDto>.Ok(new TestResultDto
                    {
                        Result = $"Processed: {input.Name}",
                        Processed = true
                    });
                }))
                .Build();

            // Create a serialized request as if it came from a gateway
            var serializer = session.GetSerializer();
            var gatewayRequest = new TransportRequest<TestDto>(
                "test.get", "GET", "request", "gateway", "usage1",
                Guid.NewGuid(), "tenant1", "en-GB",
                new Characters { Performer = new Identifier("user", "u1") },
                new List<Attribute> { new Attribute("source", "gateway") },
                new List<Attribute>(),
                new TestDto { Name = "integration-test", Value = 42 }
            ).AssignSerializer(serializer);

            var json = gatewayRequest.Serialize();

            // Process through session
            var result = await session.AcceptIncomingRequest(json);

            Assert.True(result.Success);
            Assert.Equal(200, result.StatusCode);
        }

        [Fact]
        public async Task EndToEnd_ClientToSessionCommunication()
        {
            // Setup session with handlers
            var session = Transport.Session("service-a")
                .Intercept(GetTestOperation.Instance.Handle(async (input, ctx) =>
                {
                    return Result<TestResultDto>.Ok(new TestResultDto
                    {
                        Result = $"Hello {input.Name}",
                        Processed = true
                    });
                }))
                .Intercept(CreateTestOperation.Instance.Handle(async (input, ctx) =>
                {
                    return Result<TestResultDto>.Ok(new TestResultDto
                    {
                        Result = $"Created: {input.Name}",
                        Processed = true
                    });
                }))
                .Build();

            // Create client and make requests
            var client = session.CreateClient("web-app")
                .WithLocale("nb-NO")
                .WithDataTenant("acme-corp")
                .AddAttribute("apiVersion", "v2");

            // Request 1: GET
            var getResult = await client.Request(
                new RequestOperationRequest<TestDto, TestResultDto>(
                    "u1", GetTestOperation.Instance,
                    new TestDto { Name = "World", Value = 1 }
                ));

            Assert.True(getResult.Success);
            Assert.Equal("Hello World", getResult.Value.Result);

            // Request 2: CREATE
            var createResult = await client.Request(
                new RequestOperationRequest<TestDto, TestResultDto>(
                    "u2", CreateTestOperation.Instance,
                    new TestDto { Name = "NewItem", Value = 2 }
                ));

            Assert.True(createResult.Success);
            Assert.Equal("Created: NewItem", createResult.Value.Result);
        }

        [Fact]
        public async Task EndToEnd_OutboundSessionCommunication()
        {
            var cache = new InMemoryContextCache();

            // Inbound session
            var inboundBuilder = Transport.Session("inbound-service")
                .SetupOutboundContextCache(cache);

            // Outbound session pointing to a downstream service
            var outboundFactory = inboundBuilder
                .OutboundSessionBuilder("downstream-service")
                .Intercept(GetTestOperation.Instance.Handle(async (input, ctx) =>
                {
                    return Result<TestResultDto>.Ok(new TestResultDto
                    {
                        Result = $"Downstream: {input.Name}",
                        Processed = true
                    });
                }))
                .Build();

            // Simulate an independent outbound call
            var client = outboundFactory.AsIndependentRequests()
                .WithLocale("en-US")
                .WithDataTenant("downstream-tenant");

            var result = await client.Request(
                new RequestOperationRequest<TestDto, TestResultDto>(
                    "u1", GetTestOperation.Instance,
                    new TestDto { Name = "OutboundTest", Value = 1 }
                ));

            Assert.True(result.Success);
            Assert.Equal("Downstream: OutboundTest", result.Value.Result);
        }

        [Fact]
        public async Task EndToEnd_RequestAndResponseInspection()
        {
            var requestInspected = false;
            var responseInspected = false;

            var session = Transport.Session("inspected-service")
                .Intercept(GetTestOperation.Instance.Handle(async (input, ctx) =>
                {
                    return Result<TestResultDto>.Ok(new TestResultDto { Result = "inspected" });
                }))
                .InspectRequest(async (input, ctx) =>
                {
                    requestInspected = true;
                    return null; // Allow to pass through
                })
                .InspectResponse(async (result, input, ctx) =>
                {
                    responseInspected = true;
                    return result;
                })
                .Build();

            var client = session.CreateClient("test-client");
            await client.Request(
                new RequestOperationRequest<TestDto, TestResultDto>(
                    "u1", GetTestOperation.Instance,
                    new TestDto { Name = "test" }
                ));

            Assert.True(requestInspected);
            Assert.True(responseInspected);
        }

        [Fact]
        public async Task EndToEnd_ResultChaining_MaybePattern()
        {
            // Simulate a pipeline using Maybe pattern
            var result = Result<string>.Ok("hello")
                .Maybe<int>((val, meta) =>
                {
                    return Result<int>.Ok(val.Length);
                })
                .Maybe<string>((val, meta) =>
                {
                    return Result<string>.Ok($"Length is {val}");
                });

            Assert.True(result.Success);
            Assert.Equal("Length is 5", result.Value);
        }

        [Fact]
        public async Task EndToEnd_ResultChaining_MaybeStopsOnError()
        {
            var thirdCalled = false;

            var result = Result<string>.Ok("hello")
                .Maybe<int>((val, meta) =>
                {
                    return Result<int>.NotFound("NOT_FOUND");
                })
                .Maybe<string>((val, meta) =>
                {
                    thirdCalled = true;
                    return Result<string>.Ok("should not reach");
                });

            Assert.False(result.Success);
            Assert.Equal(404, result.StatusCode);
            Assert.False(thirdCalled);
        }

        [Fact]
        public async Task EndToEnd_MaybeAsync_Pipeline()
        {
            var result = await Result<string>.Ok("hello")
                .MaybeAsync<int>(async (val, meta) =>
                {
                    await Task.Delay(1);
                    return Result<int>.Ok(val.Length);
                });

            var final = await result.MaybeAsync<string>(async (val, meta) =>
            {
                await Task.Delay(1);
                return Result<string>.Ok($"Async length is {val}");
            });

            Assert.True(final.Success);
            Assert.Equal("Async length is 5", final.Value);
        }

        [Fact]
        public async Task EndToEnd_MetadataFlow()
        {
            var session = Transport.Session("service")
                .Intercept(GetTestOperation.Instance.Handle(async (input, ctx) =>
                {
                    return Result<TestResultDto>.Ok(
                        new TestResultDto { Result = "with-meta" },
                        new Metavalues()
                            .SetHasMoreValues(true)
                            .SetTotalValueCount(100)
                            .Add(Metavalue.With("item1", "tenant1",
                                new Identifier("user", "creator1"), DateTime.UtcNow,
                                new Identifier("user", "updater1"), DateTime.UtcNow))
                            .WithAttribute("page", 1)
                    );
                }))
                .Build();

            var client = session.CreateClient("web-app");
            var result = await client.Request(
                new RequestOperationRequest<TestDto, TestResultDto>(
                    "u1", GetTestOperation.Instance,
                    new TestDto { Name = "paginated" }
                ));

            Assert.True(result.Success);
            Assert.True(result.Meta.HasMoreValues);
            Assert.Equal(100, result.Meta.TotalValueCount);
            Assert.Single(result.Meta.Values);
            Assert.True(result.Meta.HasAttribute("page"));

            var metavalue = result.Meta.GetMetaValue("item1");
            Assert.NotNull(metavalue);
            Assert.Equal("tenant1", metavalue.DataTenant);
            Assert.True(metavalue.KnowsInitialCharacters());
            Assert.True(metavalue.KnowsCurrentCharacters());
        }

        [Fact]
        public async Task EndToEnd_ErrorPropagation()
        {
            var session = Transport.Session("service")
                .Intercept(GetTestOperation.Instance.Handle(async (input, ctx) =>
                {
                    if (string.IsNullOrEmpty(input.Name))
                        return Result<TestResultDto>.BadRequest("VALIDATION_ERROR",
                            "Name is required", "Please provide a name");

                    return Result<TestResultDto>.Ok(new TestResultDto { Result = input.Name });
                }))
                .Build();

            var client = session.CreateClient("web-app");

            // Successful request
            var success = await client.Request(
                new RequestOperationRequest<TestDto, TestResultDto>(
                    "u1", GetTestOperation.Instance,
                    new TestDto { Name = "valid", Value = 1 }
                ));
            Assert.True(success.Success);

            // Failing request
            var failure = await client.Request(
                new RequestOperationRequest<TestDto, TestResultDto>(
                    "u2", GetTestOperation.Instance,
                    new TestDto { Name = "", Value = 0 }
                ));
            Assert.False(failure.Success);
            Assert.Equal(400, failure.StatusCode);
            Assert.Equal("VALIDATION_ERROR", failure.Error.Code);
            Assert.Equal("Name is required", failure.Error.Details.TechnicalError);
            Assert.Equal("Please provide a name", failure.Error.Details.UserError);
        }

        [Fact]
        public async Task EndToEnd_MultiplePatternHandlers()
        {
            var session = Transport.Session("api-gateway")
                .InterceptPattern("users.", async (input, ctx) =>
                {
                    return Result<object>.Ok<object>("users-handler");
                })
                .InterceptPattern("products.", async (input, ctx) =>
                {
                    return Result<object>.Ok<object>("products-handler");
                })
                .InterceptPattern("products.inventory.", async (input, ctx) =>
                {
                    return Result<object>.Ok<object>("inventory-handler");
                })
                .Build();

            var serializer = session.GetSerializer();

            // Test users pattern
            var userReq = new TransportRequest<object>(
                "users.list", "GET", "request", "c", "u",
                Guid.NewGuid(), "", "en-GB", new Characters(),
                new List<Attribute>(), new List<Attribute>(), null
            ).AssignSerializer(serializer);

            var userResult = await session.AcceptIncomingRequest(userReq);
            Assert.True(userResult.Success);

            // Test products pattern
            var prodReq = new TransportRequest<object>(
                "products.list", "GET", "request", "c", "u",
                Guid.NewGuid(), "", "en-GB", new Characters(),
                new List<Attribute>(), new List<Attribute>(), null
            ).AssignSerializer(serializer);

            var prodResult = await session.AcceptIncomingRequest(prodReq);
            Assert.True(prodResult.Success);

            // Test longest-prefix match (inventory should match over products)
            var invReq = new TransportRequest<object>(
                "products.inventory.check", "GET", "request", "c", "u",
                Guid.NewGuid(), "", "en-GB", new Characters(),
                new List<Attribute>(), new List<Attribute>(), null
            ).AssignSerializer(serializer);

            var invResult = await session.AcceptIncomingRequest(invReq);
            Assert.True(invResult.Success);
        }

        [Fact]
        public async Task EndToEnd_ContextPropagation()
        {
            // Verify that context flows correctly through the system
            string capturedLocale = null;
            string capturedTenant = null;
            string capturedClient = null;
            string capturedUsageId = null;
            Guid capturedTxId = Guid.Empty;

            var session = Transport.Session("service")
                .Intercept(GetTestOperation.Instance.Handle(async (input, ctx) =>
                {
                    capturedLocale = ctx.Call.Locale;
                    capturedTenant = ctx.Call.DataTenant;
                    capturedClient = ctx.Operation.CallingClient;
                    capturedUsageId = ctx.Operation.UsageId;
                    capturedTxId = ctx.Call.TransactionId;
                    return Result<TestResultDto>.Ok(new TestResultDto());
                }))
                .Build();

            var client = session.CreateClient("web-frontend")
                .WithLocale("nb-NO")
                .WithDataTenant("acme");

            await client.Request(
                new RequestOperationRequest<TestDto, TestResultDto>(
                    "usage-abc", GetTestOperation.Instance,
                    new TestDto { Name = "context-test" }
                ));

            Assert.Equal("nb-NO", capturedLocale);
            Assert.Equal("acme", capturedTenant);
            Assert.Equal("web-frontend", capturedClient);
            Assert.Equal("usage-abc", capturedUsageId);
            Assert.NotEqual(Guid.Empty, capturedTxId);
        }
    }
}
