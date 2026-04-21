using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace PeerColabEngine.Tests
{
    public class EventTests
    {
        [Fact]
        public void TransportEvent_SerializeDeserialize_RoundTrip()
        {
            var serializer = new DefaultTransportSerializer();
            var tx = Guid.Parse("00000000-0000-0000-0000-000000000001");
            var te = new TransportEvent<ItemDto>(
                "items.itemCreated", "action", "client-1", "usage-1",
                tx, "tenant-1", "en-GB",
                new Characters(),
                new List<Attribute>(),
                new List<Attribute>(),
                new ItemDto { ItemId = "123" },
                null,
                "corr-1"
            ).AssignSerializer(serializer);

            var serialized = te.Serialize();
            var restored = TransportEvent<ItemDto>.FromSerialized(serializer, serialized);

            Assert.Equal("items.itemCreated", restored.EventId);
            Assert.Equal("action", restored.EventType);
            Assert.Equal("client-1", restored.CallingClient);
            Assert.Equal("usage-1", restored.UsageId);
            Assert.Equal(tx, restored.TransactionId);
            Assert.Equal("tenant-1", restored.DataTenant);
            Assert.Equal("corr-1", restored.CorrelationId);
            Assert.Equal("123", restored.RequestJson.ItemId);
        }

        [Fact]
        public void TransportEvent_CorrelationId_IsOptional()
        {
            var serializer = new DefaultTransportSerializer();
            var te = new TransportEvent<ItemDto>(
                "e", "action", "c", "u",
                Guid.NewGuid(), "dt", "en-GB",
                new Characters(),
                new List<Attribute>(),
                new List<Attribute>(),
                new ItemDto { ItemId = "x" },
                null
            ).AssignSerializer(serializer);

            var restored = TransportEvent<ItemDto>.FromSerialized(serializer, te.Serialize());
            Assert.Null(restored.CorrelationId);
        }

        [Fact]
        public void TransportEvent_From_CarriesCorrelationIdFromCallInfo()
        {
            var serializer = new DefaultTransportSerializer();
            var request = new EventDispatchRequest<ItemDto>(
                "u1",
                ItemCreatedEvent.Instance,
                new ItemDto { ItemId = "123" }
            );
            var callInfo = new CallInformation(
                "en-GB", "t",
                new CharacterMetaValues(),
                new List<Attribute>(),
                new List<Attribute>(),
                Guid.NewGuid(),
                "corr-xyz"
            );
            var ctx = new TransportContext(
                request.AsOperationInformation("client-1"),
                callInfo,
                serializer
            );

            var te = TransportEvent<ItemDto>.From(new ItemDto { ItemId = "123" }, ctx);
            Assert.Equal("corr-xyz", te.CorrelationId);
        }

        [Fact]
        public async Task Subscribe_SingleSubscriber_ReceivesDispatchedEvent()
        {
            ItemDto received = null;
            var session = Transport.Session("svc")
                .Subscribe(ItemCreatedEvent.Instance.Handle(async (input, ctx) =>
                {
                    received = input;
                    return Result.Ok();
                }))
                .Build();

            var client = session.CreateClient("c1");
            var result = await client.Dispatch(
                new EventDispatchRequest<ItemDto>("u1", ItemCreatedEvent.Instance, new ItemDto { ItemId = "123" })
            );

            Assert.True(result.IsSuccess());
            Assert.NotNull(received);
            Assert.Equal("123", received.ItemId);
        }

        [Fact]
        public async Task Dispatch_NoSubscribers_ReturnsHandlerNotFound()
        {
            var session = Transport.Session("svc").Build();
            var client = session.CreateClient("c1");
            var result = await client.Dispatch(
                new EventDispatchRequest<ItemDto>("u1", ItemCreatedEvent.Instance, new ItemDto { ItemId = "123" })
            );

            Assert.False(result.IsSuccess());
            Assert.Equal("TransportAbstraction.HandlerNotFound", result.Error.Code);
        }

        [Fact]
        public async Task Subscribe_FanOut_AllSubscribersInvoked()
        {
            var calls = new List<string>();
            var gate = new object();
            void Record(string s) { lock (gate) { calls.Add(s); } }

            var session = Transport.Session("svc")
                .Subscribe(ItemCreatedEvent.Instance.Handle(async (input, ctx) => { Record("a"); return Result.Ok(); }))
                .Subscribe(ItemCreatedEvent.Instance.Handle(async (input, ctx) => { Record("b"); return Result.Ok(); }))
                .Subscribe(ItemCreatedEvent.Instance.Handle(async (input, ctx) => { Record("c"); return Result.Ok(); }))
                .Build();

            var client = session.CreateClient("c1");
            var result = await client.Dispatch(
                new EventDispatchRequest<ItemDto>("u1", ItemCreatedEvent.Instance, new ItemDto { ItemId = "1" })
            );

            Assert.True(result.IsSuccess());
            calls.Sort();
            Assert.Equal(new[] { "a", "b", "c" }, calls.ToArray());
        }

        [Fact]
        public async Task Subscribe_FailingSubscribers_AggregateAsRelatedErrors()
        {
            var okRan = false;
            var session = Transport.Session("svc")
                .Subscribe(ItemCreatedEvent.Instance.Handle(async (input, ctx) =>
                {
                    return Result<object>.Failed(500, "Subscriber.A.Failed", "a failed");
                }))
                .Subscribe(ItemCreatedEvent.Instance.Handle(async (input, ctx) =>
                {
                    okRan = true;
                    return Result.Ok();
                }))
                .Subscribe(ItemCreatedEvent.Instance.Handle(async (input, ctx) =>
                {
                    return Result<object>.Failed(500, "Subscriber.B.Failed", "b failed");
                }))
                .Build();

            var client = session.CreateClient("c1");
            var result = await client.Dispatch(
                new EventDispatchRequest<ItemDto>("u1", ItemCreatedEvent.Instance, new ItemDto { ItemId = "1" })
            );

            Assert.True(okRan);
            Assert.False(result.IsSuccess());
            Assert.Equal("TransportAbstraction.DispatchPartialFailure", result.Error.Code);
            Assert.Equal(2, result.Error.Related.Count);
            var codes = result.Error.Related.Select(e => e.Code).OrderBy(c => c).ToArray();
            Assert.Equal(new[] { "Subscriber.A.Failed", "Subscriber.B.Failed" }, codes);
        }

        [Fact]
        public async Task Subscribe_ThrowingSubscriber_CapturedAsUnhandledError()
        {
            var session = Transport.Session("svc")
                .Subscribe(ItemCreatedEvent.Instance.Handle(async (input, ctx) =>
                {
                    throw new Exception("boom");
                }))
                .Subscribe(ItemCreatedEvent.Instance.Handle(async (input, ctx) => Result.Ok()))
                .Build();

            var client = session.CreateClient("c1");
            var result = await client.Dispatch(
                new EventDispatchRequest<ItemDto>("u1", ItemCreatedEvent.Instance, new ItemDto { ItemId = "1" })
            );

            Assert.False(result.IsSuccess());
            Assert.Single(result.Error.Related);
            Assert.Equal("TransportAbstraction.UnhandledError", result.Error.Related[0].Code);
        }

        [Fact]
        public async Task SubscribePattern_MatchesPrefixedEventIds()
        {
            var received = new List<string>();
            var gate = new object();
            var session = Transport.Session("svc")
                .SubscribePattern("items.", async (input, ctx) =>
                {
                    lock (gate) { received.Add(ctx.Operation.Id); }
                    return Result.Ok();
                })
                .Build();

            var client = session.CreateClient("c1");
            await client.Dispatch(new EventDispatchRequest<ItemDto>("u1", ItemCreatedEvent.Instance, new ItemDto { ItemId = "1" }));
            await client.Dispatch(new EventDispatchRequest<ItemDto>("u1", ItemUpdatedEvent.Instance, new ItemDto { ItemId = "2" }));

            received.Sort();
            Assert.Equal(new[] { "items.itemCreated", "items.itemUpdated" }, received.ToArray());
        }

        [Fact]
        public async Task Subscribe_SpecificAndPattern_BothFire()
        {
            var calls = new List<string>();
            var gate = new object();
            var session = Transport.Session("svc")
                .Subscribe(ItemCreatedEvent.Instance.Handle(async (input, ctx) =>
                {
                    lock (gate) { calls.Add("specific"); }
                    return Result.Ok();
                }))
                .SubscribePattern("items.", async (input, ctx) =>
                {
                    lock (gate) { calls.Add("pattern"); }
                    return Result.Ok();
                })
                .Build();

            var client = session.CreateClient("c1");
            var result = await client.Dispatch(
                new EventDispatchRequest<ItemDto>("u1", ItemCreatedEvent.Instance, new ItemDto { ItemId = "1" })
            );

            Assert.True(result.IsSuccess());
            calls.Sort();
            Assert.Equal(new[] { "pattern", "specific" }, calls.ToArray());
        }

        [Fact]
        public async Task AcceptIncomingEvent_RoutesSerializedEvent()
        {
            ItemDto received = null;
            var serializer = new DefaultTransportSerializer();
            var session = Transport.Session("svc")
                .AssignSerializer(serializer)
                .Subscribe(ItemCreatedEvent.Instance.Handle(async (input, ctx) =>
                {
                    received = input;
                    return Result.Ok();
                }))
                .Build();

            var incoming = new TransportEvent<ItemDto>(
                ItemCreatedEvent.Instance.Id,
                ItemCreatedEvent.Instance.Verb,
                "remote-client", "u-99",
                Guid.NewGuid(), "tenant", "en-GB",
                new Characters(),
                new List<Attribute>(),
                new List<Attribute>(),
                new ItemDto { ItemId = "999" },
                null,
                "corr-99"
            ).AssignSerializer(serializer);

            var result = await session.AcceptIncomingEvent(incoming.Serialize());

            Assert.True(result.IsSuccess());
            Assert.NotNull(received);
            Assert.Equal("999", received.ItemId);
        }

        [Fact]
        public async Task AcceptIncomingEvent_PreservesCorrelationId()
        {
            string capturedCorrelation = null;
            var serializer = new DefaultTransportSerializer();
            var session = Transport.Session("svc")
                .AssignSerializer(serializer)
                .Subscribe(ItemCreatedEvent.Instance.Handle(async (input, ctx) =>
                {
                    capturedCorrelation = ctx.Call.CorrelationId;
                    return Result.Ok();
                }))
                .Build();

            var incoming = new TransportEvent<ItemDto>(
                ItemCreatedEvent.Instance.Id,
                ItemCreatedEvent.Instance.Verb,
                "rc", "u",
                Guid.NewGuid(), "dt", "en-GB",
                new Characters(),
                new List<Attribute>(),
                new List<Attribute>(),
                new ItemDto { ItemId = "x" },
                null,
                "my-correlation"
            ).AssignSerializer(serializer);

            await session.AcceptIncomingEvent(incoming.Serialize());
            Assert.Equal("my-correlation", capturedCorrelation);
        }

        [Fact]
        public async Task AcceptEvent_OutOfContext_Routes()
        {
            ItemDto received = null;
            var session = Transport.Session("svc")
                .Subscribe(ItemCreatedEvent.Instance.Handle(async (input, ctx) =>
                {
                    received = input;
                    return Result.Ok();
                }))
                .Build();

            var ooce = new OutOfContextEvent
            {
                UsageId = "u-1",
                EventId = ItemCreatedEvent.Instance.Id,
                EventType = ItemCreatedEvent.Instance.Verb,
                RequestJson = new ItemDto { ItemId = "abc" },
                CorrelationId = "corr-1"
            };

            var result = await session.AcceptEvent(ooce);

            Assert.True(result.IsSuccess());
            Assert.NotNull(received);
            Assert.Equal("abc", received.ItemId);
        }

        [Fact]
        public async Task Dispatch_Timeout_ReturnsDispatchTimeout()
        {
            var session = Transport.Session("svc")
                .Subscribe(ItemCreatedEvent.Instance.Handle(async (input, ctx) =>
                {
                    await Task.Delay(500);
                    return Result.Ok();
                }))
                .Build();

            var client = session.CreateClient("c1");
            var result = await client.Dispatch(
                new EventDispatchRequest<ItemDto>("u1", ItemCreatedEvent.Instance, new ItemDto { ItemId = "x" }),
                50
            );

            Assert.False(result.IsSuccess());
            Assert.Equal("TransportAbstraction.DispatchTimeout", result.Error.Code);
        }

        [Fact]
        public async Task Dispatch_FastHandler_CompletesBeforeTimeout()
        {
            var session = Transport.Session("svc")
                .Subscribe(ItemCreatedEvent.Instance.Handle(async (input, ctx) => Result.Ok()))
                .Build();

            var client = session.CreateClient("c1");
            var result = await client.Dispatch(
                new EventDispatchRequest<ItemDto>("u1", ItemCreatedEvent.Instance, new ItemDto { ItemId = "x" }),
                5000
            );

            Assert.True(result.IsSuccess());
        }

        [Fact]
        public void WithCorrelationId_ReturnsNewClient()
        {
            var session = Transport.Session("svc").Build();
            var c1 = session.CreateClient("c1");
            var c2 = c1.WithCorrelationId("corr-42");
            Assert.NotSame(c1, c2);
        }

        [Fact]
        public async Task WithCorrelationId_FlowsIntoSubscriberContext()
        {
            string captured = null;
            var session = Transport.Session("svc")
                .Subscribe(ItemCreatedEvent.Instance.Handle(async (input, ctx) =>
                {
                    captured = ctx.Call.CorrelationId;
                    return Result.Ok();
                }))
                .Build();

            var client = session.CreateClient("c1").WithCorrelationId("corr-42");
            await client.Dispatch(
                new EventDispatchRequest<ItemDto>("u1", ItemCreatedEvent.Instance, new ItemDto { ItemId = "x" })
            );

            Assert.Equal("corr-42", captured);
        }
    }
}
