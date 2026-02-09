using System;
using System.Collections.Generic;
using Xunit;

namespace PeerColabEngine.Tests
{
    public class ContextTests
    {
        private TransportContext CreateTestContext(
            List<Attribute> attributes = null,
            List<Attribute> pathParams = null)
        {
            var operation = new OperationInformation("test.get", "GET", "request", "client1", "usage1");
            var call = new CallInformation(
                "en-GB",
                "tenant1",
                new CharacterMetaValues(),
                attributes ?? new List<Attribute>(),
                pathParams ?? new List<Attribute>(),
                Guid.NewGuid()
            );
            return new TransportContext(operation, call, new DefaultTransportSerializer());
        }

        [Fact]
        public void TransportContext_Constructor_SetsProperties()
        {
            var ctx = CreateTestContext();

            Assert.NotNull(ctx.Operation);
            Assert.NotNull(ctx.Call);
            Assert.NotNull(ctx.Serializer);
            Assert.Equal("test.get", ctx.Operation.Id);
        }

        [Fact]
        public void TransportContext_HasAttribute_ReturnsTrueWhenExists()
        {
            var ctx = CreateTestContext(attributes: new List<Attribute>
            {
                new Attribute("key1", "value1")
            });

            Assert.True(ctx.HasAttribute("key1"));
            Assert.False(ctx.HasAttribute("key2"));
        }

        [Fact]
        public void TransportContext_GetAttribute_ReturnsValue()
        {
            var ctx = CreateTestContext(attributes: new List<Attribute>
            {
                new Attribute("name", "John")
            });

            Assert.Equal("John", ctx.GetAttribute<string>("name"));
        }

        [Fact]
        public void TransportContext_GetAttribute_ThrowsWhenMissing()
        {
            var ctx = CreateTestContext();

            Assert.Throws<Exception>(() => ctx.GetAttribute<string>("missing"));
        }

        [Fact]
        public void TransportContext_HasPathParameter_ReturnsTrueWhenExists()
        {
            var ctx = CreateTestContext(pathParams: new List<Attribute>
            {
                new Attribute("id", "123")
            });

            Assert.True(ctx.HasPathParameter("id"));
            Assert.False(ctx.HasPathParameter("other"));
        }

        [Fact]
        public void TransportContext_GetPathParameter_ReturnsValue()
        {
            var ctx = CreateTestContext(pathParams: new List<Attribute>
            {
                new Attribute("id", "123")
            });

            Assert.Equal("123", ctx.GetPathParameter<string>("id"));
        }

        [Fact]
        public void TransportContext_GetPathParameter_ThrowsWhenMissing()
        {
            var ctx = CreateTestContext();

            Assert.Throws<Exception>(() => ctx.GetPathParameter<string>("missing"));
        }

        [Fact]
        public void TransportContext_From_CreatesFromTransportRequest()
        {
            var serializer = new DefaultTransportSerializer();
            var request = new TransportRequest<object>(
                "op.get", "GET", "request", "client1", "usage1",
                Guid.NewGuid(), "tenant1", "en-GB",
                new Characters(),
                new List<Attribute> { new Attribute("a", "b") },
                new List<Attribute> { new Attribute("id", "1") },
                null
            ).AssignSerializer(serializer);

            var ctx = TransportContext.From(request);

            Assert.Equal("op.get", ctx.Operation.Id);
            Assert.Equal("GET", ctx.Operation.Verb);
            Assert.Equal("request", ctx.Operation.Type);
            Assert.Equal("client1", ctx.Operation.CallingClient);
            Assert.Equal("en-GB", ctx.Call.Locale);
            Assert.Equal("tenant1", ctx.Call.DataTenant);
            Assert.True(ctx.HasAttribute("a"));
            Assert.True(ctx.HasPathParameter("id"));
        }

        [Fact]
        public void TransportContext_From_ThrowsWithoutSerializer()
        {
            var request = new TransportRequest<object>(
                "op", "GET", "request", "c", "u",
                Guid.NewGuid(), "", "", new Characters(),
                new List<Attribute>(), new List<Attribute>(), null
            );

            Assert.Throws<Exception>(() => TransportContext.From(request));
        }

        [Fact]
        public void TransportContext_SerializeRequest_SerializesInput()
        {
            var ctx = CreateTestContext();
            var dto = new TestDto { Name = "test", Value = 1 };

            var json = ctx.SerializeRequest(dto);

            Assert.Contains("test", json);
        }

        [Fact]
        public void TransportContext_DeserializeResult_DeserializesResult()
        {
            var serializer = new DefaultTransportSerializer();
            var ctx = CreateTestContext();
            var result = Result<TestDto>.Ok(new TestDto { Name = "hello", Value = 5 });
            result.AssignSerializer(serializer);
            var json = result.Serialize();

            var deserialized = ctx.DeserializeResult<TestDto>(json);

            Assert.True(deserialized.Success);
            Assert.Equal("hello", deserialized.Value.Name);
        }

        // OperationInformation tests
        [Fact]
        public void OperationInformation_Constructor_SetsAllProperties()
        {
            var op = new OperationInformation("op.id", "GET", "request", "client", "usage");

            Assert.Equal("op.id", op.Id);
            Assert.Equal("GET", op.Verb);
            Assert.Equal("request", op.Type);
            Assert.Equal("client", op.CallingClient);
            Assert.Equal("usage", op.UsageId);
        }

        // CallInformation tests
        [Fact]
        public void CallInformation_New_CreatesWithDefaults()
        {
            var call = CallInformation.New("en-GB");

            Assert.Equal("en-GB", call.Locale);
            Assert.Equal("", call.DataTenant);
            Assert.NotNull(call.Characters);
            Assert.Empty(call.Attributes);
            Assert.Empty(call.PathParams);
            Assert.NotEqual(Guid.Empty, call.TransactionId);
        }

        [Fact]
        public void CallInformation_New_WithTenant()
        {
            var call = CallInformation.New("en-US", "my-tenant");

            Assert.Equal("my-tenant", call.DataTenant);
        }

        [Fact]
        public void CallInformation_New_WithTransactionId()
        {
            var txId = Guid.NewGuid();
            var call = CallInformation.New("en-GB", transactionId: txId);

            Assert.Equal(txId, call.TransactionId);
        }

        [Fact]
        public void CallInformation_Clone_CreatesDeepCopy()
        {
            var original = CallInformation.New("en-GB", "tenant1");
            original.Attributes.Add(new Attribute("key", "val"));
            original.PathParams.Add(new Attribute("id", "1"));
            original.Characters = CharacterMetaValues.FromPerformer("user", "1")
                .WithSubject(new Identifier("sub", "2"))
                .WithResponsible(new Identifier("resp", "3"));

            var clone = original.Clone();

            Assert.Equal(original.Locale, clone.Locale);
            Assert.Equal(original.DataTenant, clone.DataTenant);
            Assert.Equal(original.TransactionId, clone.TransactionId);

            // Verify deep copy - modifying clone doesn't affect original
            clone.Attributes.Add(new Attribute("extra", "val"));
            Assert.Single(original.Attributes);
            Assert.Equal(2, clone.Attributes.Count);

            clone.PathParams.Add(new Attribute("extra", "val"));
            Assert.Single(original.PathParams);

            // Characters should be cloned
            Assert.NotNull(clone.Characters);
            var cloneChars = clone.Characters as CharacterMetaValues;
            Assert.NotNull(cloneChars);
            Assert.Equal("user", cloneChars.Performer.Type);
            Assert.Equal("sub", cloneChars.Subject.Type);
            Assert.Equal("resp", cloneChars.Responsible.Type);
        }

        [Fact]
        public void CallInformation_Clone_HandlesNullCharacterFields()
        {
            var original = CallInformation.New("en-GB");
            var clone = original.Clone();

            var chars = clone.Characters as CharacterMetaValues;
            Assert.NotNull(chars);
            Assert.Null(chars.Subject);
            Assert.Null(chars.Responsible);
            Assert.Null(chars.Performer);
        }

        // Attribute tests
        [Fact]
        public void Attribute_Constructor_SetsProperties()
        {
            var attr = new Attribute("name", 42);

            Assert.Equal("name", attr.Name);
            Assert.Equal(42, attr.Value);
        }
    }
}
