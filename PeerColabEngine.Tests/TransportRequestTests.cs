using System;
using System.Collections.Generic;
using Xunit;

namespace PeerColabEngine.Tests
{
    public class TransportRequestTests
    {
        [Fact]
        public void TransportRequest_Constructor_SetsAllProperties()
        {
            var txId = Guid.NewGuid();
            var chars = new Characters();
            var attrs = new List<Attribute> { new Attribute("a", "b") };
            var pathParams = new List<Attribute> { new Attribute("id", "1") };

            var request = new TransportRequest<TestDto>(
                "op.get", "GET", "request", "client1", "usage1",
                txId, "tenant1", "en-GB", chars, attrs, pathParams,
                new TestDto { Name = "test", Value = 1 },
                "raw-json"
            );

            Assert.Equal("op.get", request.OperationId);
            Assert.Equal("GET", request.OperationVerb);
            Assert.Equal("request", request.OperationType);
            Assert.Equal("client1", request.CallingClient);
            Assert.Equal("usage1", request.UsageId);
            Assert.Equal(txId, request.TransactionId);
            Assert.Equal("tenant1", request.DataTenant);
            Assert.Equal("en-GB", request.Locale);
            Assert.Same(chars, request.Characters);
            Assert.Same(attrs, request.Attributes);
            Assert.Same(pathParams, request.PathParams);
            Assert.Equal("test", request.RequestJson.Name);
            Assert.Equal("raw-json", request.Raw);
        }

        [Fact]
        public void TransportRequest_AssignSerializer_SetsSerializer()
        {
            var serializer = new DefaultTransportSerializer();
            var request = new TransportRequest<object>(
                "op", "GET", "request", "c", "u",
                Guid.NewGuid(), "", "", new Characters(),
                new List<Attribute>(), new List<Attribute>(), null
            );

            var returned = request.AssignSerializer(serializer);

            Assert.Same(request, returned);
            Assert.Same(serializer, request.Serializer);
        }

        [Fact]
        public void TransportRequest_Serialize_WithoutSerializer_Throws()
        {
            var request = new TransportRequest<object>(
                "op", "GET", "request", "c", "u",
                Guid.NewGuid(), "", "", new Characters(),
                new List<Attribute>(), new List<Attribute>(), null
            );

            Assert.Throws<Exception>(() => request.Serialize());
        }

        [Fact]
        public void TransportRequest_Serialize_WithSerializer_ReturnsJson()
        {
            var serializer = new DefaultTransportSerializer();
            var request = new TransportRequest<TestDto>(
                "op.get", "GET", "request", "client1", "usage1",
                Guid.NewGuid(), "tenant1", "en-GB",
                new Characters(),
                new List<Attribute>(),
                new List<Attribute>(),
                new TestDto { Name = "test", Value = 42 }
            ).AssignSerializer(serializer);

            var json = request.Serialize();

            Assert.Contains("op.get", json);
            Assert.Contains("test", json);
        }

        [Fact]
        public void TransportRequest_From_CreatesFromContext()
        {
            var serializer = new DefaultTransportSerializer();
            var op = new OperationInformation("op.get", "GET", "request", "client1", "usage1");
            var call = CallInformation.New("en-GB", "tenant1");
            var ctx = new TransportContext(op, call, serializer);

            var input = new TestDto { Name = "test", Value = 1 };
            var request = TransportRequest<TestDto>.From(input, ctx);

            Assert.Equal("op.get", request.OperationId);
            Assert.Equal("GET", request.OperationVerb);
            Assert.Equal("request", request.OperationType);
            Assert.Equal("client1", request.CallingClient);
            Assert.Equal("en-GB", request.Locale);
            Assert.Equal("tenant1", request.DataTenant);
            Assert.Equal("test", request.RequestJson.Name);
            Assert.NotNull(request.Serializer);
        }

        [Fact]
        public void TransportRequest_From_GeneratesTransactionIdWhenEmpty()
        {
            var serializer = new DefaultTransportSerializer();
            var op = new OperationInformation("op", "GET", "request", "c", "u");
            var call = new CallInformation("en-GB", "", new CharacterMetaValues(),
                new List<Attribute>(), new List<Attribute>(), Guid.Empty);
            var ctx = new TransportContext(op, call, serializer);

            var request = TransportRequest<TestDto>.From(new TestDto(), ctx);

            Assert.NotEqual(Guid.Empty, request.TransactionId);
        }

        [Fact]
        public void TransportRequest_FromSerialized_DeserializesCorrectly()
        {
            var serializer = new DefaultTransportSerializer();
            var original = new TransportRequest<TestDto>(
                "op.get", "GET", "request", "client1", "usage1",
                Guid.NewGuid(), "tenant1", "en-GB",
                new Characters(),
                new List<Attribute>(),
                new List<Attribute>(),
                new TestDto { Name = "hello", Value = 99 }
            ).AssignSerializer(serializer);

            var json = original.Serialize();
            var deserialized = TransportRequest<TestDto>.FromSerialized(serializer, json);

            Assert.Equal("op.get", deserialized.OperationId);
            Assert.Equal("hello", deserialized.RequestJson.Name);
            Assert.Equal(99, deserialized.RequestJson.Value);
            Assert.Equal(json, deserialized.Raw);
        }

        [Fact]
        public void TransportRequest_Deserialize_ToNewType()
        {
            var serializer = new DefaultTransportSerializer();
            var original = new TransportRequest<TestDto>(
                "op.get", "GET", "request", "client1", "usage1",
                Guid.NewGuid(), "tenant1", "en-GB",
                new Characters(),
                new List<Attribute>(),
                new List<Attribute>(),
                new TestDto { Name = "test", Value = 42 }
            ).AssignSerializer(serializer);

            var json = original.Serialize();
            var deserialized = original.Deserialize<TestDto>(json);

            Assert.Equal("op.get", deserialized.OperationId);
            Assert.Equal("test", deserialized.RequestJson.Name);
            Assert.NotNull(deserialized.Serializer);
        }

        [Fact]
        public void TransportRequest_Deserialize_WithoutSerializer_Throws()
        {
            var request = new TransportRequest<object>(
                "op", "GET", "request", "c", "u",
                Guid.NewGuid(), "", "", new Characters(),
                new List<Attribute>(), new List<Attribute>(), null
            );

            Assert.Throws<Exception>(() => request.Deserialize<object>("{}"));
        }
    }
}
