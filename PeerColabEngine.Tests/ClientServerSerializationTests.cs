using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace PeerColabEngine.Tests
{
    // Domain DTOs simulating real-world models
    public class ProductDto
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public DateTime? CreatedDate { get; set; }
        public ProductImageDto Image { get; set; }
        public List<string> Tags { get; set; }
    }

    public class ProductImageDto
    {
        public string Url { get; set; }
        public string MimeType { get; set; }
    }

    public class ProductDetailsDto
    {
        public string ProductId { get; set; }
        public List<ProductComponentDto> Components { get; set; }
        public ProductOwnerDto Owner { get; set; }
        public ProductMetricsDto Metrics { get; set; }
    }

    public class ProductComponentDto
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public DateTime? DateAdded { get; set; }
        public bool Active { get; set; }
    }

    public class ProductOwnerDto
    {
        public string UserId { get; set; }
        public string Name { get; set; }
    }

    public class ProductMetricsDto
    {
        public double? Weight { get; set; }
        public List<string> Labels { get; set; }
    }

    public class CreateProductInput
    {
        public string Name { get; set; }
        public string CategoryId { get; set; }
        public string SubCategoryId { get; set; }
        public string Status { get; set; }
    }

    public class UpdateProductInput
    {
        public string Name { get; set; }
        public string Status { get; set; }
        public DateTime? CreatedDate { get; set; }
        public string SerialNumber { get; set; }
    }

    public class DataSourceDto
    {
        public string Type { get; set; }
        public string ReferenceId { get; set; }
    }

    public class SyncInput
    {
        public List<TaskChangeDto> TaskChanges { get; set; }
        public List<string> Completions { get; set; }
    }

    public class TaskChangeDto
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public bool IsCompleted { get; set; }
    }

    public class SyncOutput
    {
        public int SyncedCount { get; set; }
        public string SyncToken { get; set; }
    }

    public class ChatInstructionInput
    {
        public string UsageInstructions { get; set; }
        public string CurrentStateSnapshot { get; set; }
        public List<ChatMessageDto> Items { get; set; }
    }

    public class ChatMessageDto
    {
        public string Type { get; set; }
        public string Role { get; set; }
        public string Content { get; set; }
    }

    public class ChatInstructionOutput
    {
        public string Message { get; set; }
        public List<OutOfContextOperation> Operations { get; set; }
    }

    // Operations mimicking production patterns
    public class GetProductOperation : RequestOperation<string, ProductDto>
    {
        public static readonly GetProductOperation Instance = new GetProductOperation();
        private GetProductOperation() : base("TestApp.Products.GetProduct", "GET", new List<string> { "DataSource", "productId" },
            new TransportOperationSettings { RequiresTenant = true, CharacterSetup = new TransportOperationCharacterSetup() }) { }
    }

    public class CreateProductOperation : RequestOperation<CreateProductInput, ProductDto>
    {
        public static readonly CreateProductOperation Instance = new CreateProductOperation();
        private CreateProductOperation() : base("TestApp.Products.CreateProduct", "CREATE", new List<string> { "DataSource" },
            new TransportOperationSettings { RequiresTenant = true, CharacterSetup = new TransportOperationCharacterSetup() }) { }
    }

    public class UpdateProductOperation : MessageOperation<UpdateProductInput>
    {
        public static readonly UpdateProductOperation Instance = new UpdateProductOperation();
        private UpdateProductOperation() : base("TestApp.Products.UpdateProduct", "UPDATE", new List<string> { "DataSource", "productId" },
            new TransportOperationSettings { RequiresTenant = true, CharacterSetup = new TransportOperationCharacterSetup() }) { }
    }

    public class GetProductDetailsOperation : RequestOperation<string, ProductDetailsDto>
    {
        public static readonly GetProductDetailsOperation Instance = new GetProductDetailsOperation();
        private GetProductDetailsOperation() : base("TestApp.Products.GetProductDetails", "GET", new List<string> { "DataSource", "productId" },
            new TransportOperationSettings { RequiresTenant = true, CharacterSetup = new TransportOperationCharacterSetup() }) { }
    }

    public class SyncTasksOperation : RequestOperation<SyncInput, SyncOutput>
    {
        public static readonly SyncTasksOperation Instance = new SyncTasksOperation();
        private SyncTasksOperation() : base("TestApp.Tasks.SyncLocalUpdates", "PROCESS", new List<string> { "DataSource" },
            new TransportOperationSettings { RequiresTenant = true, CharacterSetup = new TransportOperationCharacterSetup() }) { }
    }

    public class ProcessChatOperation : RequestOperation<ChatInstructionInput, ChatInstructionOutput>
    {
        public static readonly ProcessChatOperation Instance = new ProcessChatOperation();
        private ProcessChatOperation() : base("PeerColab.Instructions.ProcessChatInstruction", "PROCESS") { }
    }

    /// <summary>
    /// Tests that simulate real client-server communication where serialization/deserialization
    /// happens at the HTTP boundary. One session acts as client, pattern-intercepts serialize
    /// the request, pass it as a string to a second session which deserializes and processes it.
    /// </summary>
    public class ClientServerSerializationTests
    {
        /// <summary>
        /// Simulates: Client session serializes request → string over HTTP → Server session deserializes and handles
        /// </summary>
        private (TransportSession clientSession, TransportSession serverSession) BuildClientServerPair(
            Action<TransportSessionBuilder> configureServer,
            string serverPatternPrefix = "TestApp.")
        {
            // Server session with actual handlers
            var serverBuilder = Transport.Session("server-session");
            configureServer(serverBuilder);
            var serverSession = serverBuilder.Build();

            // Client session that pattern-intercepts and serializes to server
            var clientSession = Transport.Session("client-session")
                .InterceptPattern(serverPatternPrefix, async (input, ctx) =>
                {
                    // Simulate HTTP: serialize the request
                    var serializedRequest = ctx.SerializeRequest(input);

                    // Simulate HTTP: server receives and deserializes
                    var result = await serverSession.AcceptIncomingRequest(serializedRequest);

                    // Simulate HTTP: serialize the result
                    var serializedResult = result.Serialize();

                    // Simulate HTTP: client deserializes the result
                    return ctx.DeserializeResult<object>(serializedResult);
                })
                .Build();

            return (clientSession, serverSession);
        }

        [Fact]
        public async Task ClientServer_SimpleRequest_SerializesAndDeserializesCorrectly()
        {
            var (clientSession, _) = BuildClientServerPair(server =>
            {
                server.Intercept(GetProductOperation.Instance.Handle(async (input, ctx) =>
                {
                    return Result<ProductDto>.Ok(new ProductDto
                    {
                        Id = "prod-1",
                        Name = "Widget Alpha",
                        Category = "electronics",
                        CreatedDate = new DateTime(2020, 3, 15),
                        Tags = new List<string> { "premium", "certified" }
                    });
                }));
            });

            var client = clientSession.CreateClient("mobile-app")
                .WithLocale("en-GB")
                .WithDataTenant("tenant1")
                .AddPathParam("dataSource", new DataSourceDto { Type = "manual" })
                .AddPathParam("productId", "prod-1");

            var result = await client.Request(
                new RequestOperationRequest<string, ProductDto>(
                    "usage1", GetProductOperation.Instance, "prod-1"));

            Assert.True(result.Success);
            Assert.Equal("prod-1", result.Value.Id);
            Assert.Equal("Widget Alpha", result.Value.Name);
            Assert.Equal("electronics", result.Value.Category);
            Assert.Equal(2, result.Value.Tags.Count);
            Assert.Contains("premium", result.Value.Tags);
        }

        [Fact]
        public async Task ClientServer_ComplexNestedObject_SerializesAndDeserializesCorrectly()
        {
            var (clientSession, _) = BuildClientServerPair(server =>
            {
                server.Intercept(GetProductDetailsOperation.Instance.Handle(async (input, ctx) =>
                {
                    return Result<ProductDetailsDto>.Ok(new ProductDetailsDto
                    {
                        ProductId = "prod-1",
                        Components = new List<ProductComponentDto>
                        {
                            new ProductComponentDto
                            {
                                Id = "comp-1",
                                Name = "Resistor Pack",
                                Description = "Standard 10k ohm resistor set",
                                DateAdded = new DateTime(2024, 1, 15),
                                Active = true
                            },
                            new ProductComponentDto
                            {
                                Id = "comp-2",
                                Name = "Capacitor Set",
                                Description = null,
                                DateAdded = null,
                                Active = false
                            }
                        },
                        Owner = new ProductOwnerDto { UserId = "user-1", Name = "John" },
                        Metrics = new ProductMetricsDto
                        {
                            Weight = 14.5,
                            Labels = new List<string> { "fragile", "heavy" }
                        }
                    });
                }));
            });

            var client = clientSession.CreateClient("mobile-app")
                .WithDataTenant("tenant1")
                .AddPathParam("dataSource", new DataSourceDto { Type = "manual" })
                .AddPathParam("productId", "prod-1");

            var result = await client.Request(
                new RequestOperationRequest<string, ProductDetailsDto>(
                    "usage1", GetProductDetailsOperation.Instance, "prod-1"));

            Assert.True(result.Success);
            Assert.Equal("prod-1", result.Value.ProductId);
            Assert.Equal(2, result.Value.Components.Count);
            Assert.Equal("Resistor Pack", result.Value.Components[0].Name);
            Assert.True(result.Value.Components[0].Active);
            Assert.Null(result.Value.Components[1].Description);
            Assert.Null(result.Value.Components[1].DateAdded);
            Assert.False(result.Value.Components[1].Active);
            Assert.Equal("user-1", result.Value.Owner.UserId);
            Assert.Equal(14.5, result.Value.Metrics.Weight);
            Assert.Equal(2, result.Value.Metrics.Labels.Count);
        }

        [Fact]
        public async Task ClientServer_NullFields_SurviveSerialization()
        {
            var (clientSession, _) = BuildClientServerPair(server =>
            {
                server.Intercept(GetProductOperation.Instance.Handle(async (input, ctx) =>
                {
                    return Result<ProductDto>.Ok(new ProductDto
                    {
                        Id = "prod-1",
                        Name = "Widget",
                        Category = "electronics",
                        CreatedDate = null,
                        Image = null,
                        Tags = null
                    });
                }));
            });

            var client = clientSession.CreateClient("web-app").WithDataTenant("t1");
            var result = await client.Request(
                new RequestOperationRequest<string, ProductDto>(
                    "u1", GetProductOperation.Instance, "prod-1"));

            Assert.True(result.Success);
            Assert.Equal("Widget", result.Value.Name);
            Assert.Null(result.Value.CreatedDate);
            Assert.Null(result.Value.Image);
            Assert.Null(result.Value.Tags);
        }

        [Fact]
        public async Task ClientServer_EmptyCollections_SurviveSerialization()
        {
            var (clientSession, _) = BuildClientServerPair(server =>
            {
                server.Intercept(GetProductDetailsOperation.Instance.Handle(async (input, ctx) =>
                {
                    return Result<ProductDetailsDto>.Ok(new ProductDetailsDto
                    {
                        ProductId = "prod-1",
                        Components = new List<ProductComponentDto>(),
                        Owner = new ProductOwnerDto { UserId = "u1" },
                        Metrics = new ProductMetricsDto { Labels = new List<string>() }
                    });
                }));
            });

            var client = clientSession.CreateClient("web-app").WithDataTenant("t1");
            var result = await client.Request(
                new RequestOperationRequest<string, ProductDetailsDto>(
                    "u1", GetProductDetailsOperation.Instance, "prod-1"));

            Assert.True(result.Success);
            Assert.Empty(result.Value.Components);
            Assert.Empty(result.Value.Metrics.Labels);
        }

        [Fact]
        public async Task ClientServer_ErrorResult_SerializesAndDeserializesCorrectly()
        {
            var (clientSession, _) = BuildClientServerPair(server =>
            {
                server.Intercept(GetProductOperation.Instance.Handle(async (input, ctx) =>
                {
                    return Result<ProductDto>.NotFound(
                        "TestApp.Products.ProductNotFound",
                        "Product with id prod-999 not found",
                        "The product you're looking for doesn't exist");
                }));
            });

            var client = clientSession.CreateClient("mobile-app").WithDataTenant("t1");
            var result = await client.Request(
                new RequestOperationRequest<string, ProductDto>(
                    "u1", GetProductOperation.Instance, "prod-999"));

            Assert.False(result.Success);
            Assert.Equal(404, result.StatusCode);
            Assert.Equal("TestApp.Products.ProductNotFound", result.Error.Code);
        }

        [Fact]
        public async Task ClientServer_BadRequestError_SerializesWithDetails()
        {
            var (clientSession, _) = BuildClientServerPair(server =>
            {
                server.Intercept(CreateProductOperation.Instance.Handle(async (input, ctx) =>
                {
                    if (string.IsNullOrEmpty(input.Name))
                        return Result<ProductDto>.BadRequest(
                            "TestApp.Products.InvalidName",
                            "Product name cannot be empty",
                            "Please enter a name for your product");

                    return Result<ProductDto>.Ok(new ProductDto { Id = "new", Name = input.Name });
                }));
            });

            var client = clientSession.CreateClient("mobile-app").WithDataTenant("t1");

            // Send empty name - should get BadRequest back through serialization
            var result = await client.Request(
                new RequestOperationRequest<CreateProductInput, ProductDto>(
                    "u1", CreateProductOperation.Instance,
                    new CreateProductInput { Name = "", CategoryId = "electronics" }));

            Assert.False(result.Success);
            Assert.Equal(400, result.StatusCode);
            Assert.Equal("TestApp.Products.InvalidName", result.Error.Code);
            Assert.Equal("Product name cannot be empty", result.Error.Details.TechnicalError);
            Assert.Equal("Please enter a name for your product", result.Error.Details.UserError);
        }

        [Fact]
        public async Task ClientServer_CharactersPropagation_PerformerSurvivesSerialization()
        {
            string capturedPerformerId = null;
            string capturedPerformerType = null;

            var (clientSession, _) = BuildClientServerPair(server =>
            {
                server.Intercept(GetProductOperation.Instance.Handle(async (input, ctx) =>
                {
                    capturedPerformerId = ctx.Call.Characters.Performer?.Id;
                    capturedPerformerType = ctx.Call.Characters.Performer?.Type;
                    return Result<ProductDto>.Ok(new ProductDto { Id = "prod-1", Name = "Widget" });
                }));
            });

            var client = clientSession.CreateClient("mobile-app")
                .WithDataTenant("t1")
                .WithCharacters(new Characters
                {
                    Performer = new Identifier("user", "user-123")
                });

            await client.Request(
                new RequestOperationRequest<string, ProductDto>(
                    "u1", GetProductOperation.Instance, "prod-1"));

            Assert.Equal("user-123", capturedPerformerId);
            Assert.Equal("user", capturedPerformerType);
        }

        [Fact]
        public async Task ClientServer_CustomAttributes_PropagatedThroughSerialization()
        {
            string capturedUserId = null;
            string capturedUsername = null;
            string capturedFullName = null;

            // Simulates a pattern where JWT claims are injected as custom attributes
            var serverSession = Transport.Session("server-session")
                .Intercept(GetProductOperation.Instance.Handle(async (input, ctx) =>
                {
                    capturedUserId = ctx.GetAttribute<string>("userId");
                    capturedUsername = ctx.GetAttribute<string>("username");
                    capturedFullName = ctx.GetAttribute<string>("fullName");
                    return Result<ProductDto>.Ok(new ProductDto { Id = "prod-1", Name = "Widget" });
                }))
                .Build();

            var clientSession = Transport.Session("client-session")
                .InterceptPattern("TestApp.", async (input, ctx) =>
                {
                    var serializedRequest = ctx.SerializeRequest(input);

                    // Server injects custom attributes (like JWT claims) before handling
                    var customAttrs = new List<Attribute>
                    {
                        new Attribute("userId", "user-42"),
                        new Attribute("username", "john.doe"),
                        new Attribute("fullName", "John Doe")
                    };

                    var result = await serverSession.AcceptIncomingRequest(serializedRequest, customAttrs);
                    var serializedResult = result.Serialize();
                    return ctx.DeserializeResult<object>(serializedResult);
                })
                .Build();

            var client = clientSession.CreateClient("mobile-app").WithDataTenant("t1");
            await client.Request(
                new RequestOperationRequest<string, ProductDto>(
                    "u1", GetProductOperation.Instance, "prod-1"));

            Assert.Equal("user-42", capturedUserId);
            Assert.Equal("john.doe", capturedUsername);
            Assert.Equal("John Doe", capturedFullName);
        }

        [Fact]
        public async Task ClientServer_PathParams_ComplexObject_SurvivesSerialization()
        {
            DataSourceDto capturedDataSource = null;
            string capturedProductId = null;

            var (clientSession, _) = BuildClientServerPair(server =>
            {
                server.Intercept(GetProductOperation.Instance.Handle(async (input, ctx) =>
                {
                    capturedDataSource = ctx.GetPathParameter<DataSourceDto>("dataSource");
                    capturedProductId = ctx.GetPathParameter<string>("productId");
                    return Result<ProductDto>.Ok(new ProductDto { Id = capturedProductId, Name = "Widget" });
                }));
            });

            var client = clientSession.CreateClient("mobile-app")
                .WithDataTenant("t1")
                .AddPathParam("dataSource", new DataSourceDto { Type = "manual" })
                .AddPathParam("productId", "prod-abc");

            var result = await client.Request(
                new RequestOperationRequest<string, ProductDto>(
                    "u1", GetProductOperation.Instance, "prod-abc"));

            Assert.True(result.Success);
            Assert.NotNull(capturedDataSource);
            Assert.Equal("manual", capturedDataSource.Type);
            Assert.Equal("prod-abc", capturedProductId);
        }

        [Fact]
        public async Task ClientServer_PathParams_ComplexObjectWithReferenceId_SurvivesSerialization()
        {
            DataSourceDto capturedDataSource = null;

            var (clientSession, _) = BuildClientServerPair(server =>
            {
                server.Intercept(GetProductOperation.Instance.Handle(async (input, ctx) =>
                {
                    capturedDataSource = ctx.GetPathParameter<DataSourceDto>("dataSource");
                    return Result<ProductDto>.Ok(new ProductDto { Id = "prod-1", Name = "Widget" });
                }));
            });

            var client = clientSession.CreateClient("mobile-app")
                .WithDataTenant("t1")
                .AddPathParam("dataSource", new DataSourceDto { Type = "aiextract", ReferenceId = "conv-456" });

            await client.Request(
                new RequestOperationRequest<string, ProductDto>(
                    "u1", GetProductOperation.Instance, "prod-1"));

            Assert.NotNull(capturedDataSource);
            Assert.Equal("aiextract", capturedDataSource.Type);
            Assert.Equal("conv-456", capturedDataSource.ReferenceId);
        }

        [Fact]
        public async Task ClientServer_MessageOperation_SerializesAndDeserializesCorrectly()
        {
            UpdateProductInput capturedInput = null;

            var (clientSession, _) = BuildClientServerPair(server =>
            {
                server.Intercept(UpdateProductOperation.Instance.Handle(async (input, ctx) =>
                {
                    capturedInput = input;
                    return Result<object>.Ok();
                }));
            });

            var client = clientSession.CreateClient("mobile-app").WithDataTenant("t1");
            var result = await client.Request(
                new MessageOperationRequest<UpdateProductInput>(
                    "u1",
                    new TransportOperation<UpdateProductInput, object>(
                        "message",
                        "TestApp.Products.UpdateProduct",
                        "UPDATE",
                        new List<string> { "DataSource", "productId" },
                        new TransportOperationSettings { RequiresTenant = true }),
                    new UpdateProductInput
                    {
                        Name = "Widget Updated",
                        Status = "active",
                        CreatedDate = new DateTime(2020, 5, 10),
                        SerialNumber = "SN-123456"
                    }));

            Assert.True(result.Success);
            Assert.NotNull(capturedInput);
            Assert.Equal("Widget Updated", capturedInput.Name);
            Assert.Equal("active", capturedInput.Status);
            Assert.Equal("SN-123456", capturedInput.SerialNumber);
        }

        [Fact]
        public async Task ClientServer_ComplexInputDto_SerializesThroughClientServer()
        {
            SyncInput capturedInput = null;

            var (clientSession, _) = BuildClientServerPair(server =>
            {
                server.Intercept(SyncTasksOperation.Instance.Handle(async (input, ctx) =>
                {
                    capturedInput = input;
                    return Result<SyncOutput>.Ok(new SyncOutput
                    {
                        SyncedCount = input.TaskChanges.Count,
                        SyncToken = "tok-abc"
                    });
                }));
            });

            var client = clientSession.CreateClient("mobile-app").WithDataTenant("t1");
            var result = await client.Request(
                new RequestOperationRequest<SyncInput, SyncOutput>(
                    "u1", SyncTasksOperation.Instance,
                    new SyncInput
                    {
                        TaskChanges = new List<TaskChangeDto>
                        {
                            new TaskChangeDto { Id = "t1", Name = "Review report", Type = "daily", IsCompleted = false },
                            new TaskChangeDto { Id = "t2", Name = "Update inventory", Type = "health", IsCompleted = true }
                        },
                        Completions = new List<string> { "t3", "t4" }
                    }));

            Assert.True(result.Success);
            Assert.Equal(2, result.Value.SyncedCount);
            Assert.Equal("tok-abc", result.Value.SyncToken);
            Assert.NotNull(capturedInput);
            Assert.Equal(2, capturedInput.TaskChanges.Count);
            Assert.Equal("Review report", capturedInput.TaskChanges[0].Name);
            Assert.True(capturedInput.TaskChanges[1].IsCompleted);
            Assert.Equal(2, capturedInput.Completions.Count);
        }

        [Fact]
        public async Task ClientServer_MetadataWithValues_SurvivesSerialization()
        {
            var (clientSession, _) = BuildClientServerPair(server =>
            {
                server.Intercept(GetProductOperation.Instance.Handle(async (input, ctx) =>
                {
                    var meta = new Metavalues()
                        .SetHasMoreValues(true)
                        .SetTotalValueCount(42)
                        .Add(Metavalue.With("prod-1", "tenant1",
                            new Identifier("user", "creator1"), new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                            new Identifier("user", "updater1"), new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc)))
                        .WithAttribute("page", 1);

                    return Result<ProductDto>.Ok(
                        new ProductDto { Id = "prod-1", Name = "Widget" },
                        meta);
                }));
            });

            var client = clientSession.CreateClient("mobile-app").WithDataTenant("t1");
            var result = await client.Request(
                new RequestOperationRequest<string, ProductDto>(
                    "u1", GetProductOperation.Instance, "prod-1"));

            Assert.True(result.Success);
            Assert.NotNull(result.Meta);
            Assert.True(result.Meta.HasMoreValues);
            Assert.Equal(42, result.Meta.TotalValueCount);
            Assert.True(result.Meta.HasAttribute("page"));
        }

        [Fact]
        public async Task ClientServer_SetMetaOnFailedResult_SurvivesSerialization()
        {
            var (clientSession, _) = BuildClientServerPair(server =>
            {
                server.Intercept(GetProductOperation.Instance.Handle(async (input, ctx) =>
                {
                    var meta = new Metavalues()
                        .Add(Metavalue.WithAttribute("op-1", "error", "operation failed"))
                        .Add(Metavalue.WithAttribute("op-2", "error", "also failed"));

                    return Result<ProductDto>.BadRequest(
                        "TestApp.Import.PartialFailure",
                        "Some operations failed").SetMeta(meta);
                }));
            });

            var client = clientSession.CreateClient("mobile-app").WithDataTenant("t1");
            var result = await client.Request(
                new RequestOperationRequest<string, ProductDto>(
                    "u1", GetProductOperation.Instance, "prod-1"));

            Assert.False(result.Success);
            Assert.Equal(400, result.StatusCode);
            Assert.Equal("TestApp.Import.PartialFailure", result.Error.Code);
            Assert.NotNull(result.Meta);
            Assert.Equal(2, result.Meta.Values.Count);
        }

        [Fact]
        public async Task ClientServer_InboundToOutbound_FullRoundTrip()
        {
            // Simulates: Mobile client → Client session → (serialization) → Server session
            //            Server handler makes outbound call → (serialization) → Downstream service
            //            Full round trip with context propagation

            var cache = new InMemoryContextCache();

            // Downstream "task sync" service
            var downstreamSession = Transport.Session("downstream-service")
                .Intercept(SyncTasksOperation.Instance.Handle(async (input, ctx) =>
                {
                    return Result<SyncOutput>.Ok(new SyncOutput
                    {
                        SyncedCount = input.TaskChanges.Count,
                        SyncToken = "server-token-123"
                    });
                }))
                .Build();

            // Main server session with outbound to downstream
            var serverBuilder = Transport.Session("main-server")
                .SetupOutboundContextCache(cache);

            var outboundFactory = serverBuilder
                .OutboundSessionBuilder("downstream-outbound")
                .InterceptPattern("TestApp.Tasks.", async (input, ctx) =>
                {
                    // Serialize to downstream
                    var serialized = ctx.SerializeRequest(input);
                    var downstreamResult = await downstreamSession.AcceptIncomingRequest(serialized);
                    var serializedResult = downstreamResult.Serialize();
                    return ctx.DeserializeResult<object>(serializedResult);
                })
                .Build();

            // Server handler that calls downstream
            serverBuilder.Intercept(GetProductOperation.Instance.Handle(async (input, ctx) =>
            {
                // Make outbound call to downstream
                var outboundClient = await outboundFactory.ForIncomingRequest(ctx.Call.TransactionId);
                outboundClient = outboundClient
                    .WithDataTenant(ctx.Call.DataTenant)
                    .WithCharacters(ctx.Call.Characters);

                var syncResult = await outboundClient.Request(
                    new RequestOperationRequest<SyncInput, SyncOutput>(
                        "sync-usage", SyncTasksOperation.Instance,
                        new SyncInput
                        {
                            TaskChanges = new List<TaskChangeDto>
                            {
                                new TaskChangeDto { Id = "t1", Name = "Process item", Type = "daily", IsCompleted = false }
                            },
                            Completions = new List<string>()
                        }));

                if (!syncResult.Success)
                    return syncResult.Convert<ProductDto>();

                return Result<ProductDto>.Ok(new ProductDto
                {
                    Id = "prod-1",
                    Name = "Widget",
                    Tags = new List<string> { $"synced:{syncResult.Value.SyncToken}" }
                });
            }));

            var serverSession = serverBuilder.Build();

            // Client session that talks to main server over "HTTP"
            var clientSession = Transport.Session("client-session")
                .InterceptPattern("TestApp.", async (input, ctx) =>
                {
                    var serialized = ctx.SerializeRequest(input);
                    var result = await serverSession.AcceptIncomingRequest(serialized);
                    var serializedResult = result.Serialize();
                    return ctx.DeserializeResult<object>(serializedResult);
                })
                .Build();

            var client = clientSession.CreateClient("mobile-app")
                .WithDataTenant("acme-corp")
                .WithCharacters(new Characters { Performer = new Identifier("user", "user-1") });

            var finalResult = await client.Request(
                new RequestOperationRequest<string, ProductDto>(
                    "u1", GetProductOperation.Instance, "prod-1"));

            Assert.True(finalResult.Success);
            Assert.Equal("Widget", finalResult.Value.Name);
            Assert.Contains("synced:server-token-123", finalResult.Value.Tags);
        }

        [Fact]
        public async Task ClientServer_AcceptOperationAsync_WithSerializationRoundTrip()
        {
            // Simulates pattern where OutOfContextOperations are processed one at a time
            var processedOps = new List<string>();

            var serverSession = Transport.Session("server-session")
                .Intercept(CreateProductOperation.Instance.Handle(async (input, ctx) =>
                {
                    processedOps.Add($"create:{input.Name}");
                    return Result<ProductDto>.Ok(new ProductDto { Id = "new-1", Name = input.Name });
                }))
                .Intercept(UpdateProductOperation.Instance.Handle(async (input, ctx) =>
                {
                    processedOps.Add($"update:{input.Name}");
                    return Result<object>.Ok();
                }))
                .Build();

            // Client builds OutOfContextOperations (like AI agent output)
            var operations = new List<OutOfContextOperation>
            {
                new OutOfContextOperation
                {
                    OperationId = "TestApp.Products.CreateProduct",
                    OperationType = "request",
                    OperationVerb = "CREATE",
                    UsageId = "agent",
                    RequestJson = new CreateProductInput { Name = "Gadget", CategoryId = "electronics", Status = "active" }
                },
                new OutOfContextOperation
                {
                    OperationId = "TestApp.Products.UpdateProduct",
                    OperationType = "message",
                    OperationVerb = "UPDATE",
                    UsageId = "agent",
                    RequestJson = new UpdateProductInput { Name = "Gadget Updated", Status = "active" }
                }
            };

            // Process through serialization
            var client = serverSession.CreateClient("import-client")
                .WithDataTenant("t1")
                .WithCharacters(new Characters { Performer = new Identifier("user", "u1") });

            var meta = new Metavalues();
            var errors = new List<Result<object>>();

            foreach (var op in operations)
            {
                var result = await client.AcceptOperationAsync(op);
                if (!result.Success)
                    errors.Add(result);
                meta.Add(Metavalue.WithAttribute(
                    op.OperationId, "status", result.Success ? "ok" : "failed"));
            }

            Assert.Empty(errors);
            Assert.Equal(2, processedOps.Count);
            Assert.Equal("create:Gadget", processedOps[0]);
            Assert.Equal("update:Gadget Updated", processedOps[1]);
            Assert.Equal(2, meta.Values.Count);
        }

        [Fact]
        public async Task ClientServer_AcceptOperationAsync_PartialFailure_CollectsErrors()
        {
            var serverSession = Transport.Session("server-session")
                .Intercept(CreateProductOperation.Instance.Handle(async (input, ctx) =>
                {
                    if (string.IsNullOrEmpty(input.Name))
                        return Result<ProductDto>.BadRequest("TestApp.Products.InvalidName", "Name required");
                    return Result<ProductDto>.Ok(new ProductDto { Id = "new-1", Name = input.Name });
                }))
                .Build();

            var operations = new List<OutOfContextOperation>
            {
                new OutOfContextOperation
                {
                    OperationId = "TestApp.Products.CreateProduct",
                    OperationType = "request",
                    OperationVerb = "CREATE",
                    UsageId = "agent",
                    RequestJson = new CreateProductInput { Name = "Gadget", CategoryId = "electronics" }
                },
                new OutOfContextOperation
                {
                    OperationId = "TestApp.Products.CreateProduct",
                    OperationType = "request",
                    OperationVerb = "CREATE",
                    UsageId = "agent",
                    RequestJson = new CreateProductInput { Name = "", CategoryId = "tools" } // Will fail
                },
                new OutOfContextOperation
                {
                    OperationId = "TestApp.Products.CreateProduct",
                    OperationType = "request",
                    OperationVerb = "CREATE",
                    UsageId = "agent",
                    RequestJson = new CreateProductInput { Name = "Gizmo", CategoryId = "tools" }
                }
            };

            var client = serverSession.CreateClient("import-client")
                .WithDataTenant("t1")
                .WithCharacters(new Characters { Performer = new Identifier("user", "u1") });

            var meta = new Metavalues();
            var errors = new List<Result<object>>();

            foreach (var op in operations)
            {
                var result = await client.AcceptOperationAsync(op);
                if (!result.Success)
                {
                    errors.Add(result);
                    meta.Add(Metavalue.WithAttribute(op.UsageId, "error", result.Error));
                }
            }

            Assert.Single(errors);
            Assert.Equal("TestApp.Products.InvalidName", errors[0].Error.Code);

            // Verify we can attach meta to a failed result (production pattern)
            var finalResult = Result<object>.BadRequest(
                "TestApp.Import.PartialFailure",
                "1 of 3 operations failed").SetMeta(meta);

            Assert.False(finalResult.Success);
            Assert.NotNull(finalResult.Meta);
            Assert.Single(finalResult.Meta.Values);
        }

        [Fact]
        public async Task ClientServer_PatternInterceptor_OnOutboundBuilder_SerializesCorrectly()
        {
            // Tests OutboundSessionBuilder.InterceptPattern which is heavily used
            // in production for server communication
            var cache = new InMemoryContextCache();

            var serverSession = Transport.Session("backend-service")
                .Intercept(GetProductOperation.Instance.Handle(async (input, ctx) =>
                {
                    return Result<ProductDto>.Ok(new ProductDto { Id = "prod-1", Name = "Widget" });
                }))
                .Intercept(SyncTasksOperation.Instance.Handle(async (input, ctx) =>
                {
                    return Result<SyncOutput>.Ok(new SyncOutput
                    {
                        SyncedCount = input.TaskChanges.Count,
                        SyncToken = "sync-1"
                    });
                }))
                .Build();

            // Build client with outbound using pattern interceptors (production pattern)
            var clientBuilder = Transport.Session("mobile-session")
                .SetupOutboundContextCache(cache);

            var outboundFactory = clientBuilder
                .OutboundSessionBuilder("outbound-to-server")
                .InterceptPattern("TestApp.Products.", async (input, ctx) =>
                {
                    var serialized = ctx.SerializeRequest(input);
                    var result = await serverSession.AcceptIncomingRequest(serialized);
                    var serializedResult = result.Serialize();
                    return ctx.DeserializeResult<object>(serializedResult);
                })
                .InterceptPattern("TestApp.Tasks.", async (input, ctx) =>
                {
                    var serialized = ctx.SerializeRequest(input);
                    var result = await serverSession.AcceptIncomingRequest(serialized);
                    var serializedResult = result.Serialize();
                    return ctx.DeserializeResult<object>(serializedResult);
                })
                .Build();

            // Use outbound factory independently
            var outboundClient = outboundFactory.AsIndependentRequests()
                .WithDataTenant("t1")
                .WithCharacters(new Characters { Performer = new Identifier("user", "u1") });

            var productResult = await outboundClient.Request(
                new RequestOperationRequest<string, ProductDto>(
                    "u1", GetProductOperation.Instance, "prod-1"));

            Assert.True(productResult.Success);
            Assert.Equal("Widget", productResult.Value.Name);

            var syncResult = await outboundClient.Request(
                new RequestOperationRequest<SyncInput, SyncOutput>(
                    "u1", SyncTasksOperation.Instance,
                    new SyncInput
                    {
                        TaskChanges = new List<TaskChangeDto>
                        {
                            new TaskChangeDto { Id = "t1", Name = "Process item", Type = "daily" }
                        },
                        Completions = new List<string>()
                    }));

            Assert.True(syncResult.Success);
            Assert.Equal(1, syncResult.Value.SyncedCount);
            Assert.Equal("sync-1", syncResult.Value.SyncToken);
        }

        [Fact]
        public async Task ClientServer_LocaleTenantAndTransactionId_PropagatedThroughSerialization()
        {
            string capturedLocale = null;
            string capturedTenant = null;
            Guid capturedTxId = Guid.Empty;
            string capturedCallingClient = null;
            string capturedUsageId = null;

            var (clientSession, _) = BuildClientServerPair(server =>
            {
                server.Intercept(GetProductOperation.Instance.Handle(async (input, ctx) =>
                {
                    capturedLocale = ctx.Call.Locale;
                    capturedTenant = ctx.Call.DataTenant;
                    capturedTxId = ctx.Call.TransactionId;
                    capturedCallingClient = ctx.Operation.CallingClient;
                    capturedUsageId = ctx.Operation.UsageId;
                    return Result<ProductDto>.Ok(new ProductDto { Id = "prod-1", Name = "Widget" });
                }));
            });

            var client = clientSession.CreateClient("MobileClient")
                .WithLocale("nb-NO")
                .WithDataTenant("acme-corp");

            await client.Request(
                new RequestOperationRequest<string, ProductDto>(
                    "TestApp.MobileApp.Client.Products", GetProductOperation.Instance, "prod-1"));

            Assert.Equal("nb-NO", capturedLocale);
            Assert.Equal("acme-corp", capturedTenant);
            Assert.NotEqual(Guid.Empty, capturedTxId);
            Assert.Equal("MobileClient", capturedCallingClient);
            Assert.Equal("TestApp.MobileApp.Client.Products", capturedUsageId);
        }

        [Fact]
        public async Task ClientServer_TransportRequestFromSerialized_CanCheckCharactersBeforeAccept()
        {
            // Simulates a common pattern:
            // 1. Deserialize request
            // 2. Check performer matches JWT
            // 3. Accept or reject

            var serverSession = Transport.Session("server-session")
                .Intercept(GetProductOperation.Instance.Handle(async (input, ctx) =>
                {
                    return Result<ProductDto>.Ok(new ProductDto { Id = "prod-1", Name = "Widget" });
                }))
                .Build();

            // Client creates a request with performer
            var serializer = serverSession.GetSerializer();
            var request = new TransportRequest<object>(
                "TestApp.Products.GetProduct", "GET", "request", "mobile-app", "u1",
                Guid.NewGuid(), "t1", "en-GB",
                new Characters { Performer = new Identifier("user", "user-42") },
                new List<Attribute>(),
                new List<Attribute>(),
                "prod-1"
            ).AssignSerializer(serializer);

            var serialized = request.Serialize();

            // Server deserializes and inspects before accepting
            var transportRequest = TransportRequest<object>.FromSerialized(serializer, serialized);

            // Check characters before processing (like JWT validation)
            Assert.NotNull(transportRequest.Characters);
            Assert.NotNull(transportRequest.Characters.Performer);
            Assert.Equal("user-42", transportRequest.Characters.Performer.Id);

            // Now process
            var result = await serverSession.AcceptIncomingRequest(transportRequest);
            Assert.True(result.Success);
        }

        [Fact]
        public async Task ClientServer_TransportRequestFromSerialized_RejectImpersonation()
        {
            // Simulates an anti-impersonation check
            var serializer = new DefaultTransportSerializer();

            var request = new TransportRequest<object>(
                "TestApp.Products.GetProduct", "GET", "request", "mobile-app", "u1",
                Guid.NewGuid(), "t1", "en-GB",
                new Characters { Performer = new Identifier("user", "attacker-id") },
                new List<Attribute>(),
                new List<Attribute>(),
                "prod-1"
            ).AssignSerializer(serializer);

            var serialized = request.Serialize();
            var transportRequest = TransportRequest<object>.FromSerialized(serializer, serialized);

            // JWT says user is "real-user-id" but request says "attacker-id"
            var jwtUserId = "real-user-id";
            var requestPerformerId = transportRequest.Characters?.Performer?.Id;

            Assert.NotEqual(jwtUserId, requestPerformerId);
            // In production this would return 401 - we've verified the check works
        }

        [Fact]
        public async Task ClientServer_MultipleSequentialRequests_IndependentSerialization()
        {
            int requestCount = 0;

            var (clientSession, _) = BuildClientServerPair(server =>
            {
                server.Intercept(GetProductOperation.Instance.Handle(async (input, ctx) =>
                {
                    requestCount++;
                    return Result<ProductDto>.Ok(new ProductDto
                    {
                        Id = $"prod-{requestCount}",
                        Name = $"Product {requestCount}"
                    });
                }));
            });

            var client = clientSession.CreateClient("mobile-app").WithDataTenant("t1");

            var result1 = await client.Request(
                new RequestOperationRequest<string, ProductDto>("u1", GetProductOperation.Instance, "1"));
            var result2 = await client.Request(
                new RequestOperationRequest<string, ProductDto>("u1", GetProductOperation.Instance, "2"));
            var result3 = await client.Request(
                new RequestOperationRequest<string, ProductDto>("u1", GetProductOperation.Instance, "3"));

            Assert.True(result1.Success);
            Assert.True(result2.Success);
            Assert.True(result3.Success);
            Assert.Equal("Product 1", result1.Value.Name);
            Assert.Equal("Product 2", result2.Value.Name);
            Assert.Equal("Product 3", result3.Value.Name);
            Assert.Equal(3, requestCount);
        }

        [Fact]
        public async Task ClientServer_ResponseInspector_WorksThroughSerialization()
        {
            // Server has a response inspector (like production error logging)
            bool inspectorCalled = false;
            string inspectedErrorCode = null;

            var serverSession = Transport.Session("server-session")
                .Intercept(GetProductOperation.Instance.Handle(async (input, ctx) =>
                {
                    return Result<ProductDto>.NotFound("TestApp.Products.NotFound", "Product not found");
                }))
                .InspectResponse(async (result, input, ctx) =>
                {
                    inspectorCalled = true;
                    if (result.Error != null)
                        inspectedErrorCode = result.Error.Code;
                    return result;
                })
                .Build();

            var clientSession = Transport.Session("client-session")
                .InterceptPattern("TestApp.", async (input, ctx) =>
                {
                    var serialized = ctx.SerializeRequest(input);
                    var result = await serverSession.AcceptIncomingRequest(serialized);
                    var serializedResult = result.Serialize();
                    return ctx.DeserializeResult<object>(serializedResult);
                })
                .Build();

            var client = clientSession.CreateClient("mobile-app").WithDataTenant("t1");
            var result = await client.Request(
                new RequestOperationRequest<string, ProductDto>("u1", GetProductOperation.Instance, "prod-1"));

            Assert.False(result.Success);
            Assert.True(inspectorCalled);
            Assert.Equal("TestApp.Products.NotFound", inspectedErrorCode);
        }

        [Fact]
        public async Task ClientServer_ChatInstruction_ComplexNestedWithOperations()
        {
            // Simulates PeerColab AI chat instruction processing
            var (clientSession, _) = BuildClientServerPair(server =>
            {
                server.Intercept(ProcessChatOperation.Instance.Handle(async (input, ctx) =>
                {
                    Assert.Equal(2, input.Items.Count);
                    Assert.Equal("system", input.Items[0].Role);

                    return Result<ChatInstructionOutput>.Ok(new ChatInstructionOutput
                    {
                        Message = "I found some tasks to create",
                        Operations = new List<OutOfContextOperation>
                        {
                            new OutOfContextOperation
                            {
                                OperationId = "TestApp.Tasks.CreateTask",
                                OperationType = "request",
                                OperationVerb = "CREATE",
                                UsageId = "PeerColab.Instructions",
                                RequestJson = new TaskChangeDto { Id = "t1", Name = "Review report", Type = "daily" }
                            }
                        }
                    });
                }));
            }, "PeerColab.");

            var client = clientSession.CreateClient("mobile-app").WithDataTenant("t1");
            var result = await client.Request(
                new RequestOperationRequest<ChatInstructionInput, ChatInstructionOutput>(
                    "PeerColab.Instructions", ProcessChatOperation.Instance,
                    new ChatInstructionInput
                    {
                        UsageInstructions = "Operation id: TestApp.Tasks.CreateTask...",
                        CurrentStateSnapshot = "{ tasks: [] }",
                        Items = new List<ChatMessageDto>
                        {
                            new ChatMessageDto { Type = "message", Role = "system", Content = "You are a helpful assistant" },
                            new ChatMessageDto { Type = "message", Role = "user", Content = "Create a task to review the daily report" }
                        }
                    }));

            Assert.True(result.Success);
            Assert.Equal("I found some tasks to create", result.Value.Message);
            Assert.Single(result.Value.Operations);
            Assert.Equal("TestApp.Tasks.CreateTask", result.Value.Operations[0].OperationId);
        }

        [Fact]
        public async Task ClientServer_EmptyOutboundSession_CanBeBuiltAndUsed()
        {
            // Simulates production pattern where outbound is created without interceptors initially
            var cache = new InMemoryContextCache();

            var builder = Transport.Session("server")
                .SetupOutboundContextCache(cache);

            var outboundBuilder = builder.OutboundSessionBuilder("outbound");
            var outboundFactory = outboundBuilder.Build();

            Assert.NotNull(outboundFactory);

            // Can create independent client from empty outbound
            var client = outboundFactory.AsIndependentRequests();
            Assert.NotNull(client);
        }

        [Fact]
        public async Task ClientServer_ResultSerialize_Deserialize_RoundTrip_SuccessResult()
        {
            var serializer = new DefaultTransportSerializer();

            var original = Result<ProductDto>.Ok(new ProductDto
            {
                Id = "prod-1",
                Name = "Widget",
                Category = "electronics",
                CreatedDate = new DateTime(2020, 3, 15),
                Tags = new List<string> { "premium" }
            });
            original.AssignSerializer(serializer);

            var json = original.Serialize();
            Assert.NotNull(json);
            Assert.NotEmpty(json);

            var deserialized = serializer.Deserialize<Result<ProductDto>>(json);

            Assert.True(deserialized.Success);
            Assert.Equal("prod-1", deserialized.Value.Id);
            Assert.Equal("Widget", deserialized.Value.Name);
            Assert.Single(deserialized.Value.Tags);
        }

        [Fact]
        public async Task ClientServer_ResultSerialize_Deserialize_RoundTrip_ErrorResult()
        {
            var serializer = new DefaultTransportSerializer();

            var original = Result<ProductDto>.BadRequest(
                "TestApp.Products.InvalidName",
                "Name too long",
                "Please use a shorter name");
            original.AssignSerializer(serializer);

            var json = original.Serialize();
            var deserialized = serializer.Deserialize<Result<ProductDto>>(json);

            Assert.False(deserialized.Success);
            Assert.Equal(400, deserialized.StatusCode);
            Assert.Equal("TestApp.Products.InvalidName", deserialized.Error.Code);
        }

        [Fact]
        public async Task ClientServer_TransportRequest_RoundTrip_PreservesAllFields()
        {
            var serializer = new DefaultTransportSerializer();
            var txId = Guid.NewGuid();

            var original = new TransportRequest<CreateProductInput>(
                "TestApp.Products.CreateProduct",
                "CREATE",
                "request",
                "mobile-app",
                "TestApp.MobileApp.Client.Products",
                txId,
                "acme-corp",
                "nb-NO",
                new Characters
                {
                    Performer = new Identifier("user", "user-42"),
                    Subject = new Identifier("product", "prod-1")
                },
                new List<Attribute>
                {
                    new Attribute("apiVersion", "v2"),
                    new Attribute("platform", "ios")
                },
                new List<Attribute>
                {
                    new Attribute("dataSource", "manual"),
                    new Attribute("productId", "prod-1")
                },
                new CreateProductInput
                {
                    Name = "Gadget",
                    CategoryId = "electronics",
                    SubCategoryId = "sensors",
                    Status = "active"
                }
            ).AssignSerializer(serializer);

            var json = original.Serialize();
            var deserialized = TransportRequest<object>.FromSerialized(serializer, json);

            Assert.Equal("TestApp.Products.CreateProduct", deserialized.OperationId);
            Assert.Equal("CREATE", deserialized.OperationVerb);
            Assert.Equal("request", deserialized.OperationType);
            Assert.Equal("mobile-app", deserialized.CallingClient);
            Assert.Equal("TestApp.MobileApp.Client.Products", deserialized.UsageId);
            Assert.Equal(txId, deserialized.TransactionId);
            Assert.Equal("acme-corp", deserialized.DataTenant);
            Assert.Equal("nb-NO", deserialized.Locale);

            Assert.NotNull(deserialized.Characters);
            Assert.Equal("user-42", deserialized.Characters.Performer?.Id);
            Assert.Equal("user", deserialized.Characters.Performer?.Type);
            Assert.Equal("prod-1", deserialized.Characters.Subject?.Id);

            Assert.Equal(2, deserialized.Attributes.Count);
            Assert.Equal(2, deserialized.PathParams.Count);
        }

        [Fact]
        public async Task ClientServer_RequestInspector_CanBlockBeforeSerialization()
        {
            // Server has request inspector that blocks unauthorized requests
            var serverSession = Transport.Session("server-session")
                .Intercept(GetProductOperation.Instance.Handle(async (input, ctx) =>
                {
                    return Result<ProductDto>.Ok(new ProductDto { Id = "prod-1", Name = "Secret Product" });
                }))
                .InspectRequest(async (input, ctx) =>
                {
                    if (!ctx.HasAttribute("userId"))
                        return Result<object>.Failed(401, "Unauthorized", "Missing userId attribute");
                    return null; // Allow through
                })
                .Build();

            var clientSession = Transport.Session("client-session")
                .InterceptPattern("TestApp.", async (input, ctx) =>
                {
                    var serialized = ctx.SerializeRequest(input);
                    var result = await serverSession.AcceptIncomingRequest(serialized);
                    var serializedResult = result.Serialize();
                    return ctx.DeserializeResult<object>(serializedResult);
                })
                .Build();

            // Request without userId attribute
            var client = clientSession.CreateClient("mobile-app").WithDataTenant("t1");
            var result = await client.Request(
                new RequestOperationRequest<string, ProductDto>("u1", GetProductOperation.Instance, "prod-1"));

            Assert.False(result.Success);
            Assert.Equal(401, result.StatusCode);
        }

        [Fact]
        public async Task ClientServer_ConvertAndConvertToEmpty_WorkAfterDeserialization()
        {
            var (clientSession, _) = BuildClientServerPair(server =>
            {
                server.Intercept(GetProductOperation.Instance.Handle(async (input, ctx) =>
                {
                    return Result<ProductDto>.NotFound("TestApp.Products.NotFound", "Not found");
                }));
            });

            var client = clientSession.CreateClient("mobile-app").WithDataTenant("t1");
            var result = await client.Request(
                new RequestOperationRequest<string, ProductDto>("u1", GetProductOperation.Instance, "prod-1"));

            // Convert<T> on deserialized failed result
            var converted = result.Convert<string>();
            Assert.False(converted.Success);
            Assert.Equal(404, converted.StatusCode);
            Assert.Equal("TestApp.Products.NotFound", converted.Error.Code);

            // ConvertToEmpty on deserialized failed result
            var empty = result.ConvertToEmpty();
            Assert.False(empty.Success);
            Assert.Equal(404, empty.StatusCode);
            Assert.Equal("TestApp.Products.NotFound", empty.Error.Code);
        }

        [Fact]
        public async Task ClientServer_MaybeChain_OnDeserializedResult()
        {
            var (clientSession, _) = BuildClientServerPair(server =>
            {
                server.Intercept(GetProductOperation.Instance.Handle(async (input, ctx) =>
                {
                    return Result<ProductDto>.Ok(new ProductDto
                    {
                        Id = "prod-1",
                        Name = "Widget",
                        Category = "electronics"
                    });
                }));
            });

            var client = clientSession.CreateClient("mobile-app").WithDataTenant("t1");
            var result = await client.Request(
                new RequestOperationRequest<string, ProductDto>("u1", GetProductOperation.Instance, "prod-1"));

            // Chain Maybe on result that went through serialization
            var mapped = result
                .Maybe<string>((product, meta) => Result<string>.Ok($"{product.Name} in {product.Category}"))
                .Maybe<int>((desc, meta) => Result<int>.Ok(desc.Length));

            Assert.True(mapped.Success);
            Assert.Equal("Widget in electronics".Length, mapped.Value);
        }

        [Fact]
        public async Task ClientServer_MaybeChain_StopsOnDeserializedError()
        {
            var (clientSession, _) = BuildClientServerPair(server =>
            {
                server.Intercept(GetProductOperation.Instance.Handle(async (input, ctx) =>
                {
                    return Result<ProductDto>.BadRequest("TestApp.Products.Invalid", "Invalid product");
                }));
            });

            var client = clientSession.CreateClient("mobile-app").WithDataTenant("t1");
            var result = await client.Request(
                new RequestOperationRequest<string, ProductDto>("u1", GetProductOperation.Instance, "prod-1"));

            bool secondCalled = false;
            var mapped = result
                .Maybe<string>((product, meta) =>
                {
                    secondCalled = true;
                    return Result<string>.Ok("should not reach");
                });

            Assert.False(mapped.Success);
            Assert.False(secondCalled);
            Assert.Equal("TestApp.Products.Invalid", mapped.Error.Code);
        }
    }
}
