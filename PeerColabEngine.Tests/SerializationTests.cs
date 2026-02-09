using System;
using Xunit;

namespace PeerColabEngine.Tests
{
    public class SerializationTests
    {
        [Fact]
        public void DefaultSerializer_Serialize_UsesCamelCase()
        {
            var serializer = new DefaultTransportSerializer();
            var dto = new TestDto { Name = "test", Value = 42 };

            var json = serializer.Serialize(dto);

            Assert.Contains("\"name\"", json);
            Assert.Contains("\"value\"", json);
            Assert.DoesNotContain("\"Name\"", json);
        }

        [Fact]
        public void DefaultSerializer_Deserialize_HandlesCamelCase()
        {
            var serializer = new DefaultTransportSerializer();
            var json = "{\"name\":\"test\",\"value\":42}";

            var dto = serializer.Deserialize<TestDto>(json);

            Assert.Equal("test", dto.Name);
            Assert.Equal(42, dto.Value);
        }

        [Fact]
        public void DefaultSerializer_RoundTrip_PreservesData()
        {
            var serializer = new DefaultTransportSerializer();
            var original = new TestDto { Name = "roundtrip", Value = 99 };

            var json = serializer.Serialize(original);
            var deserialized = serializer.Deserialize<TestDto>(json);

            Assert.Equal(original.Name, deserialized.Name);
            Assert.Equal(original.Value, deserialized.Value);
        }

        [Fact]
        public void GlobalSerializer_ReturnsDefaultSerializer()
        {
            var serializer = GlobalSerializer.GetSerializer();
            Assert.NotNull(serializer);
            Assert.IsType<DefaultTransportSerializer>(serializer);
        }

        [Fact]
        public void GlobalSerializer_SetSerializer_ChangesGlobalSerializer()
        {
            var original = GlobalSerializer.GetSerializer();
            try
            {
                var custom = new DefaultTransportSerializer();
                GlobalSerializer.SetSerializer(custom);

                Assert.Same(custom, GlobalSerializer.GetSerializer());
            }
            finally
            {
                GlobalSerializer.SetSerializer(original);
            }
        }

        [Fact]
        public void DefaultSerializer_Serialize_NullValue_ReturnsNullJson()
        {
            var serializer = new DefaultTransportSerializer();
            var json = serializer.Serialize<TestDto>(null);
            Assert.Equal("null", json);
        }

        [Fact]
        public void DefaultSerializer_ComplexObject_RoundTrips()
        {
            var serializer = new DefaultTransportSerializer();
            var result = Result<TestDto>.Ok(new TestDto { Name = "complex", Value = 7 });
            result.AssignSerializer(serializer);

            var json = result.Serialize();
            var deserialized = result.Deserialize<TestDto>(json);

            Assert.True(deserialized.Success);
            Assert.Equal("complex", deserialized.Value.Name);
        }
    }
}
