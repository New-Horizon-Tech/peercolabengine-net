using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace PeerColabEngine.Tests
{
    public class DispatcherTests
    {
        private TransportContext CreateContext(string operationId, string type = "request")
        {
            return new TransportContext(
                new OperationInformation(operationId, "GET", type, "client", "usage"),
                CallInformation.New("en-GB"),
                new DefaultTransportSerializer()
            );
        }

        [Fact]
        public void TransportDispatcher_Constructor_SetsProperties()
        {
            var cache = new InMemoryContextCache();
            var dispatcher = new TransportDispatcher("session1", cache, false);

            Assert.Equal("session1", dispatcher.SessionIdentifier);
            Assert.Same(cache, dispatcher.ContextCache);
            Assert.False(dispatcher.CacheReads);
        }

        [Fact]
        public void TransportDispatcher_AddRequestHandler_RegistersHandler()
        {
            var dispatcher = new TransportDispatcher("s", new InMemoryContextCache(), false);

            dispatcher.AddRequestHandler("op.get", async (input, ctx) =>
            {
                return Result<object>.Ok<object>("handled");
            });

            // No exception means success
        }

        [Fact]
        public void TransportDispatcher_AddMessageHandler_RegistersHandler()
        {
            var dispatcher = new TransportDispatcher("s", new InMemoryContextCache(), false);

            dispatcher.AddMessageHandler("op.notify", async (input, ctx) =>
            {
                return Result<object>.Ok();
            });
        }

        [Fact]
        public void TransportDispatcher_AddPatternHandler_RegistersHandler()
        {
            var dispatcher = new TransportDispatcher("s", new InMemoryContextCache(), false);

            dispatcher.AddPatternHandler("myservice.", async (input, ctx) =>
            {
                return Result<object>.Ok();
            });
        }

        [Fact]
        public void TransportDispatcher_DuplicateHandler_ThrowsException()
        {
            var dispatcher = new TransportDispatcher("s", new InMemoryContextCache(), false);

            dispatcher.AddRequestHandler("op.get", async (input, ctx) => Result<object>.Ok());

            Assert.Throws<Exception>(() =>
                dispatcher.AddRequestHandler("op.get", async (input, ctx) => Result<object>.Ok()));
        }

        [Fact]
        public void TransportDispatcher_DuplicateMessageHandler_ThrowsException()
        {
            var dispatcher = new TransportDispatcher("s", new InMemoryContextCache(), false);

            dispatcher.AddMessageHandler("op.notify", async (input, ctx) => Result<object>.Ok());

            Assert.Throws<Exception>(() =>
                dispatcher.AddMessageHandler("op.notify", async (input, ctx) => Result<object>.Ok()));
        }

        [Fact]
        public void TransportDispatcher_DuplicatePatternHandler_ThrowsException()
        {
            var dispatcher = new TransportDispatcher("s", new InMemoryContextCache(), false);

            dispatcher.AddPatternHandler("prefix.", async (input, ctx) => Result<object>.Ok());

            Assert.Throws<Exception>(() =>
                dispatcher.AddPatternHandler("prefix.", async (input, ctx) => Result<object>.Ok()));
        }

        [Fact]
        public void TransportDispatcher_DuplicateAcrossTypes_ThrowsException()
        {
            var dispatcher = new TransportDispatcher("s", new InMemoryContextCache(), false);

            dispatcher.AddRequestHandler("op.do", async (input, ctx) => Result<object>.Ok());

            Assert.Throws<Exception>(() =>
                dispatcher.AddMessageHandler("op.do", async (input, ctx) => Result<object>.Ok()));
        }

        [Fact]
        public async Task TransportDispatcher_HandleAsRequest_CallsHandler()
        {
            var dispatcher = new TransportDispatcher("s", new InMemoryContextCache(), false);
            var handled = false;

            dispatcher.AddRequestHandler("op.get", async (input, ctx) =>
            {
                handled = true;
                return Result<object>.Ok<object>("result");
            });

            var ctx = CreateContext("op.get");
            var result = await dispatcher.HandleAsRequest(null, ctx);

            Assert.True(handled);
            Assert.True(result.Success);
        }

        [Fact]
        public async Task TransportDispatcher_HandleAsMessage_CallsHandler()
        {
            var dispatcher = new TransportDispatcher("s", new InMemoryContextCache(), false);
            var handled = false;

            dispatcher.AddMessageHandler("op.notify", async (input, ctx) =>
            {
                handled = true;
                return Result<object>.Ok();
            });

            var ctx = CreateContext("op.notify", "message");
            var result = await dispatcher.HandleAsMessage(null, ctx);

            Assert.True(handled);
            Assert.True(result.Success);
        }

        [Fact]
        public async Task TransportDispatcher_HandleAsRequest_PatternFallback()
        {
            var dispatcher = new TransportDispatcher("s", new InMemoryContextCache(), false);

            dispatcher.AddPatternHandler("myservice.", async (input, ctx) =>
            {
                return Result<object>.Ok<object>("pattern-handled");
            });

            var ctx = CreateContext("myservice.items.get");
            var result = await dispatcher.HandleAsRequest(null, ctx);

            Assert.True(result.Success);
        }

        [Fact]
        public async Task TransportDispatcher_PatternHandler_LongestPrefixWins()
        {
            var dispatcher = new TransportDispatcher("s", new InMemoryContextCache(), false);

            dispatcher.AddPatternHandler("myservice.", async (input, ctx) =>
            {
                return Result<object>.Ok<object>("short");
            });

            dispatcher.AddPatternHandler("myservice.items.", async (input, ctx) =>
            {
                return Result<object>.Ok<object>("long");
            });

            var ctx = CreateContext("myservice.items.get");
            var result = await dispatcher.HandleAsRequest(null, ctx);

            Assert.True(result.Success);
            Assert.Equal("long", result.Value);
        }

        [Fact]
        public async Task TransportDispatcher_NoMatchingHandler_ReturnsBadRequest()
        {
            var dispatcher = new TransportDispatcher("s", new InMemoryContextCache(), false);

            var ctx = CreateContext("unknown.op");
            var result = await dispatcher.HandleAsRequest(null, ctx);

            Assert.False(result.Success);
            Assert.Equal(400, result.StatusCode);
            Assert.Contains("HandlerNotFound", result.Error.Code);
        }

        [Fact]
        public async Task TransportDispatcher_HandlerThrows_ReturnsError()
        {
            var dispatcher = new TransportDispatcher("s", new InMemoryContextCache(), false);

            dispatcher.AddRequestHandler("op.get", async (input, ctx) =>
            {
                throw new InvalidOperationException("Handler exploded");
            });

            var ctx = CreateContext("op.get");
            var result = await dispatcher.HandleAsRequest(null, ctx);

            Assert.False(result.Success);
            Assert.Equal(500, result.StatusCode);
            Assert.Contains("UnhandledError", result.Error.Code);
        }

        [Fact]
        public async Task TransportDispatcher_MessageHandlerThrows_ReturnsError()
        {
            var dispatcher = new TransportDispatcher("s", new InMemoryContextCache(), false);

            dispatcher.AddMessageHandler("op.notify", async (input, ctx) =>
            {
                throw new Exception("Message handler failed");
            });

            var ctx = CreateContext("op.notify", "message");
            var result = await dispatcher.HandleAsMessage(null, ctx);

            Assert.False(result.Success);
            Assert.Equal(500, result.StatusCode);
        }

        [Fact]
        public async Task TransportDispatcher_RouteFromGatewayRequest_RoutesRequest()
        {
            var dispatcher = new TransportDispatcher("s", new InMemoryContextCache(), false);

            dispatcher.AddRequestHandler("op.get", async (input, ctx) =>
            {
                return Result<object>.Ok<object>("routed");
            });

            var ctx = CreateContext("op.get", "request");
            var result = await dispatcher.RouteFromGatewayRequest(null, ctx);

            Assert.True(result.Success);
        }

        [Fact]
        public async Task TransportDispatcher_RouteFromGatewayRequest_RoutesMessage()
        {
            var dispatcher = new TransportDispatcher("s", new InMemoryContextCache(), false);

            dispatcher.AddMessageHandler("op.notify", async (input, ctx) =>
            {
                return Result<object>.Ok();
            });

            var ctx = CreateContext("op.notify", "message");
            var result = await dispatcher.RouteFromGatewayRequest(null, ctx);

            Assert.True(result.Success);
        }

        [Fact]
        public async Task TransportDispatcher_RequestInspector_ShortCircuits()
        {
            var dispatcher = new TransportDispatcher("s", new InMemoryContextCache(), false);
            dispatcher.RequestsInspector = async (input, ctx) =>
            {
                return Result<object>.BadRequest("BLOCKED");
            };

            dispatcher.AddRequestHandler("op.get", async (input, ctx) =>
            {
                return Result<object>.Ok<object>("should-not-reach");
            });

            var ctx = CreateContext("op.get");
            var result = await dispatcher.HandleAsRequest(null, ctx);

            Assert.False(result.Success);
            Assert.Equal("BLOCKED", result.Error.Code);
        }

        [Fact]
        public async Task TransportDispatcher_RequestInspector_ReturnsNull_ContinuesNormally()
        {
            var dispatcher = new TransportDispatcher("s", new InMemoryContextCache(), false);
            dispatcher.RequestsInspector = async (input, ctx) => null;

            dispatcher.AddRequestHandler("op.get", async (input, ctx) =>
            {
                return Result<object>.Ok<object>("passed");
            });

            var ctx = CreateContext("op.get");
            var result = await dispatcher.HandleAsRequest(null, ctx);

            Assert.True(result.Success);
            Assert.Equal("passed", result.Value);
        }

        [Fact]
        public async Task TransportDispatcher_ResponseInspector_ModifiesResult()
        {
            var dispatcher = new TransportDispatcher("s", new InMemoryContextCache(), false);
            dispatcher.ResponsesInspector = async (result, input, ctx) =>
            {
                result.WithMeta(m => m.WithAttribute("inspected", true));
                return result;
            };

            dispatcher.AddRequestHandler("op.get", async (input, ctx) =>
            {
                return Result<object>.Ok<object>("result");
            });

            var ctx = CreateContext("op.get");
            var result = await dispatcher.HandleAsRequest(null, ctx);

            Assert.True(result.Success);
            Assert.True(result.Meta.HasAttribute("inspected"));
        }

        [Fact]
        public async Task TransportDispatcher_InspectMessageResponse_ModifiesResult()
        {
            var dispatcher = new TransportDispatcher("s", new InMemoryContextCache(), false);
            dispatcher.ResponsesInspector = async (result, input, ctx) =>
            {
                result.WithMeta(m => m.WithAttribute("msg-inspected", true));
                return result;
            };

            var ctx = CreateContext("op", "message");
            var originalResult = Result<object>.Ok();
            var inspected = await dispatcher.InspectMessageResponse(originalResult, null, ctx);

            Assert.True(inspected.Meta.HasAttribute("msg-inspected"));
        }

        [Fact]
        public async Task TransportDispatcher_InspectResponse_WithoutInspector_ReturnsOriginal()
        {
            var dispatcher = new TransportDispatcher("s", new InMemoryContextCache(), false);

            var ctx = CreateContext("op");
            var originalResult = Result<object>.Ok<object>("original");
            var result = await dispatcher.InspectResponse(originalResult, null, ctx);

            Assert.Same(originalResult, result);
        }

        [Fact]
        public async Task TransportDispatcher_RequestInspector_ThrowsException_ContinuesNormally()
        {
            var dispatcher = new TransportDispatcher("s", new InMemoryContextCache(), false);
            var logger = new TestLogger();
            Logger.AssignLogger(logger);

            dispatcher.RequestsInspector = async (input, ctx) =>
            {
                throw new Exception("Inspector failed");
            };

            dispatcher.AddRequestHandler("op.get", async (input, ctx) =>
            {
                return Result<object>.Ok<object>("handled");
            });

            var ctx = CreateContext("op.get");
            var result = await dispatcher.HandleAsRequest(null, ctx);

            Assert.True(result.Success);
        }

        [Fact]
        public async Task TransportDispatcher_ResponseInspector_ThrowsException_ReturnsOriginal()
        {
            var dispatcher = new TransportDispatcher("s", new InMemoryContextCache(), false);
            var logger = new TestLogger();
            Logger.AssignLogger(logger);

            dispatcher.ResponsesInspector = async (result, input, ctx) =>
            {
                throw new Exception("Inspector failed");
            };

            dispatcher.AddRequestHandler("op.get", async (input, ctx) =>
            {
                return Result<object>.Ok<object>("original");
            });

            var ctx = CreateContext("op.get");
            var result = await dispatcher.HandleAsRequest(null, ctx);

            Assert.True(result.Success);
        }

        [Fact]
        public async Task TransportDispatcher_CacheReads_SkipsCachePut()
        {
            var dispatcher = new TransportDispatcher("s", new InMemoryContextCache(), true);

            dispatcher.AddRequestHandler("op.get", async (input, ctx) =>
            {
                return Result<object>.Ok<object>("ok");
            });

            var ctx = CreateContext("op.get");
            var result = await dispatcher.HandleAsRequest(null, ctx);

            Assert.True(result.Success);
        }

        [Fact]
        public async Task TransportDispatcher_GetCallInfoFromCache_WithCacheReadsAndMatchSessions()
        {
            var cache = new InMemoryContextCache();
            var txId = Guid.NewGuid();
            var cached = CallInformation.New("nb-NO", "cached-tenant");
            await cache.Put(txId, cached);

            var dispatcher = new TransportDispatcher("s", cache, true);

            var fallback = CallInformation.New("en-GB", "fallback");
            var result = await dispatcher.GetCallInfoFromCache(txId, fallback, true);

            Assert.Equal("nb-NO", result.Locale);
            Assert.Equal("cached-tenant", result.DataTenant);
        }

        [Fact]
        public async Task TransportDispatcher_GetCallInfoFromCache_WithoutCacheReads_ReturnsFallback()
        {
            var dispatcher = new TransportDispatcher("s", new InMemoryContextCache(), false);

            var fallback = CallInformation.New("en-GB", "fallback");
            var result = await dispatcher.GetCallInfoFromCache(Guid.NewGuid(), fallback, true);

            Assert.Same(fallback, result);
        }

        [Fact]
        public async Task TransportDispatcher_GetCallInfoFromCache_WithoutMatchSessions_ReturnsFallback()
        {
            var dispatcher = new TransportDispatcher("s", new InMemoryContextCache(), true);

            var fallback = CallInformation.New("en-GB", "fallback");
            var result = await dispatcher.GetCallInfoFromCache(Guid.NewGuid(), fallback, false);

            Assert.Same(fallback, result);
        }

        [Fact]
        public async Task TransportDispatcher_GetCallInfoFromCache_CacheMiss_ReturnsFallback()
        {
            var dispatcher = new TransportDispatcher("s", new InMemoryContextCache(), true);

            var fallback = CallInformation.New("en-GB", "fallback");
            var result = await dispatcher.GetCallInfoFromCache(Guid.NewGuid(), fallback, true);

            Assert.Same(fallback, result);
        }

        [Fact]
        public async Task TransportDispatcher_GetCallInfoFromCache_CacheThrows_ReturnsFallback()
        {
            var dispatcher = new TransportDispatcher("s", new ThrowingContextCache(), true);
            var logger = new TestLogger();
            Logger.AssignLogger(logger);

            var fallback = CallInformation.New("en-GB", "fallback");
            var result = await dispatcher.GetCallInfoFromCache(Guid.NewGuid(), fallback, true);

            Assert.Same(fallback, result);
        }
    }
}
