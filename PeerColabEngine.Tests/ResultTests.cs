using System;
using System.Threading.Tasks;
using Xunit;

namespace PeerColabEngine.Tests
{
    public class ResultTests
    {
        [Fact]
        public void Ok_ReturnsSuccessResult()
        {
            var result = Result<object>.Ok();

            Assert.True(result.Success);
            Assert.True(result.IsSuccess());
            Assert.False(result.HasError());
            Assert.Equal(200, result.StatusCode);
            Assert.Null(result.Value);
            Assert.NotNull(result.Meta);
        }

        [Fact]
        public void Ok_WithValue_ReturnsSuccessResultWithValue()
        {
            var result = Result<string>.Ok("hello");

            Assert.True(result.Success);
            Assert.Equal("hello", result.Value);
            Assert.Equal(200, result.StatusCode);
        }

        [Fact]
        public void Ok_WithValueAndMeta_ReturnsBoth()
        {
            var meta = new Metavalues().SetHasMoreValues(true);
            var result = Result<int>.Ok(42, meta);

            Assert.True(result.Success);
            Assert.Equal(42, result.Value);
            Assert.True(result.Meta.HasMoreValues);
        }

        [Fact]
        public void OkStatus_ReturnsSuccessWithCustomCode()
        {
            var result = Result<object>.OkStatus(201);

            Assert.True(result.Success);
            Assert.Equal(201, result.StatusCode);
        }

        [Fact]
        public void NotFound_Returns404()
        {
            var result = Result<string>.NotFound("NOT_FOUND", "Item not found", "Could not find item");

            Assert.False(result.Success);
            Assert.Equal(404, result.StatusCode);
            Assert.True(result.HasError());
            Assert.Equal("NOT_FOUND", result.Error.Code);
            Assert.Equal("Item not found", result.Error.Details.TechnicalError);
            Assert.Equal("Could not find item", result.Error.Details.UserError);
        }

        [Fact]
        public void BadRequest_Returns400()
        {
            var result = Result<string>.BadRequest("BAD_REQUEST", "Invalid input");

            Assert.False(result.Success);
            Assert.Equal(400, result.StatusCode);
            Assert.Equal("BAD_REQUEST", result.Error.Code);
        }

        [Fact]
        public void InternalServerError_Returns500()
        {
            var result = Result<string>.InternalServerError("SERVER_ERROR");

            Assert.False(result.Success);
            Assert.Equal(500, result.StatusCode);
            Assert.Equal("SERVER_ERROR", result.Error.Code);
        }

        [Fact]
        public void Failed_ReturnsCustomStatusCode()
        {
            var result = Result<string>.Failed(403, "FORBIDDEN", "Access denied");

            Assert.False(result.Success);
            Assert.Equal(403, result.StatusCode);
            Assert.Equal("FORBIDDEN", result.Error.Code);
        }

        [Fact]
        public void Convert_SuccessResult_ConvertsType()
        {
            var result = Result<object>.Ok<object>("hello");
            var converted = result.Convert<string>();

            Assert.True(converted.Success);
            Assert.Equal("hello", converted.Value);
        }

        [Fact]
        public void Convert_ErrorResult_PreservesError()
        {
            var result = Result<string>.NotFound("NOT_FOUND");
            var converted = result.Convert<int>();

            Assert.False(converted.Success);
            Assert.Equal(404, converted.StatusCode);
            Assert.Equal("NOT_FOUND", converted.Error.Code);
        }

        [Fact]
        public void ConvertToEmpty_PreservesMetadata()
        {
            var result = Result<string>.Ok("hello");
            result.SetMeta(new Metavalues().SetHasMoreValues(true));
            var empty = result.ConvertToEmpty();

            Assert.True(empty.Success);
            Assert.True(empty.Meta.HasMoreValues);
        }

        [Fact]
        public void SetMeta_ReplacesMetadata()
        {
            var result = Result<string>.Ok("hello");
            var meta = new Metavalues().SetTotalValueCount(10);
            result.SetMeta(meta);

            Assert.Equal(10, result.Meta.TotalValueCount);
        }

        [Fact]
        public void WithMeta_ModifiesMetadata()
        {
            var result = Result<string>.Ok("hello");
            result.WithMeta(m => m.SetHasMoreValues(true));

            Assert.True(result.Meta.HasMoreValues);
        }

        [Fact]
        public void AddMetaValue_AddsToMetadata()
        {
            var result = Result<string>.Ok("hello");
            var metavalue = new Metavalue { ValueId = "v1" };
            result.AddMetaValue(metavalue);

            Assert.Single(result.Meta.Values);
            Assert.Equal("v1", result.Meta.Values[0].ValueId);
        }

        [Fact]
        public void AddMetaValues_AddsMultiple()
        {
            var result = Result<string>.Ok("hello");
            var values = new[] {
                new Metavalue { ValueId = "v1" },
                new Metavalue { ValueId = "v2" }
            };
            result.AddMetaValues(values);

            Assert.Equal(2, result.Meta.Values.Count);
        }

        [Fact]
        public void Maybe_OnSuccess_CallsHandler()
        {
            var result = Result<string>.Ok("hello");
            var mapped = result.Maybe<int>((val, meta) => Result<int>.Ok(val.Length));

            Assert.True(mapped.Success);
            Assert.Equal(5, mapped.Value);
        }

        [Fact]
        public void Maybe_OnFailure_SkipsHandler()
        {
            var result = Result<string>.NotFound("NOT_FOUND");
            var handlerCalled = false;
            var mapped = result.Maybe<int>((val, meta) =>
            {
                handlerCalled = true;
                return Result<int>.Ok(42);
            });

            Assert.False(handlerCalled);
            Assert.False(mapped.Success);
            Assert.Equal(404, mapped.StatusCode);
        }

        [Fact]
        public void Maybe_WithException_ReturnsError()
        {
            var result = Result<string>.Ok("hello");
            var mapped = result.Maybe<int>((val, meta) =>
            {
                throw new InvalidOperationException("Test error");
            });

            Assert.False(mapped.Success);
            Assert.Equal(500, mapped.StatusCode);
            Assert.Contains("Test error", mapped.Error.Details.TechnicalError);
        }

        [Fact]
        public void Maybe_WithThrowErrors_ThrowsException()
        {
            var result = Result<string>.Ok("hello");

            Assert.Throws<InvalidOperationException>(() =>
                result.Maybe<int>((val, meta) =>
                {
                    throw new InvalidOperationException("Test error");
                }, throwErrors: true)
            );
        }

        [Fact]
        public void MaybePassThrough_OnSuccess_ReturnsOriginal()
        {
            var result = Result<string>.Ok("hello");
            var passThrough = result.MaybePassThrough((val, meta) => Result<object>.Ok());

            Assert.True(passThrough.Success);
            Assert.Equal("hello", passThrough.Value);
        }

        [Fact]
        public void MaybePassThrough_OnFailure_SkipsHandler()
        {
            var result = Result<string>.NotFound("NOT_FOUND");
            var handlerCalled = false;
            var passThrough = result.MaybePassThrough((val, meta) =>
            {
                handlerCalled = true;
                return Result<object>.Ok();
            });

            Assert.False(handlerCalled);
            Assert.False(passThrough.Success);
        }

        [Fact]
        public void MaybePassThrough_HandlerFails_ReturnsHandlerError()
        {
            var result = Result<string>.Ok("hello");
            var passThrough = result.MaybePassThrough((val, meta) =>
                Result<object>.BadRequest("VALIDATION_ERROR"));

            Assert.False(passThrough.Success);
            Assert.Equal(400, passThrough.StatusCode);
        }

        [Fact]
        public void MaybePassThrough_HandlerThrows_ReturnsError()
        {
            var result = Result<string>.Ok("hello");
            var passThrough = result.MaybePassThrough((val, meta) =>
            {
                throw new Exception("Boom");
            });

            Assert.False(passThrough.Success);
            Assert.Equal(500, passThrough.StatusCode);
        }

        [Fact]
        public async Task MaybeAsync_OnSuccess_CallsHandler()
        {
            var result = Result<string>.Ok("hello");
            var mapped = await result.MaybeAsync<int>(async (val, meta) =>
            {
                await Task.Delay(1);
                return Result<int>.Ok(val.Length);
            });

            Assert.True(mapped.Success);
            Assert.Equal(5, mapped.Value);
        }

        [Fact]
        public async Task MaybeAsync_OnFailure_SkipsHandler()
        {
            var result = Result<string>.NotFound("NOT_FOUND");
            var mapped = await result.MaybeAsync<int>(async (val, meta) =>
            {
                await Task.Delay(1);
                return Result<int>.Ok(42);
            });

            Assert.False(mapped.Success);
            Assert.Equal(404, mapped.StatusCode);
        }

        [Fact]
        public async Task MaybeAsync_WithException_ReturnsError()
        {
            var result = Result<string>.Ok("hello");
            var mapped = await result.MaybeAsync<int>(async (val, meta) =>
            {
                await Task.Delay(1);
                throw new Exception("Async error");
            });

            Assert.False(mapped.Success);
            Assert.Equal(500, mapped.StatusCode);
        }

        [Fact]
        public async Task MaybePassThroughAsync_OnSuccess_ReturnsOriginal()
        {
            var result = Result<string>.Ok("hello");
            var passThrough = await result.MaybePassThroughAsync(async (val, meta) =>
            {
                await Task.Delay(1);
                return Result<object>.Ok();
            });

            Assert.True(passThrough.Success);
            Assert.Equal("hello", passThrough.Value);
        }

        [Fact]
        public async Task MaybePassThroughAsync_HandlerFails_ReturnsHandlerError()
        {
            var result = Result<string>.Ok("hello");
            var passThrough = await result.MaybePassThroughAsync(async (val, meta) =>
            {
                await Task.Delay(1);
                return Result<object>.BadRequest("FAIL");
            });

            Assert.False(passThrough.Success);
            Assert.Equal(400, passThrough.StatusCode);
        }

        [Fact]
        public async Task MaybePassThroughAsync_HandlerThrows_ReturnsError()
        {
            var result = Result<string>.Ok("hello");
            var passThrough = await result.MaybePassThroughAsync(async (val, meta) =>
            {
                await Task.Delay(1);
                throw new Exception("Boom");
            });

            Assert.False(passThrough.Success);
            Assert.Equal(500, passThrough.StatusCode);
        }

        [Fact]
        public void CopyConstructor_CopiesAllFields()
        {
            var original = Result<string>.Ok("test");
            original.SetMeta(new Metavalues().SetTotalValueCount(5));

            var copy = new Result<string>(original);

            Assert.True(copy.Success);
            Assert.Equal("test", copy.Value);
            Assert.Equal(200, copy.StatusCode);
        }

        [Fact]
        public void CopyConstructor_WithError_SetsDefaults()
        {
            var original = new Result<string>
            {
                Value = default,
                StatusCode = 0,
                Error = new TransportError("ERR")
            };

            var copy = new Result<string>(original);

            Assert.Equal(500, copy.StatusCode);
            Assert.False(copy.Success);
            Assert.NotNull(copy.Error);
        }

        [Fact]
        public void Serialize_WithSerializer_SerializesResult()
        {
            var serializer = new DefaultTransportSerializer();
            var result = Result<string>.Ok("hello").AssignSerializer(serializer);

            var json = result.Serialize();

            Assert.Contains("hello", json);
        }

        [Fact]
        public void Serialize_WithoutSerializer_Throws()
        {
            var result = Result<string>.Ok("hello");

            Assert.Throws<Exception>(() => result.Serialize());
        }

        [Fact]
        public void Deserialize_WithSerializer_DeserializesResult()
        {
            var serializer = new DefaultTransportSerializer();
            var result = Result<string>.Ok("hello").AssignSerializer(serializer);
            var json = result.Serialize();

            var deserialized = result.Deserialize<string>(json);

            Assert.Equal("hello", deserialized.Value);
        }

        [Fact]
        public void ResultNonGeneric_InheritsFromResultObject()
        {
            var result = new Result();
            Assert.IsAssignableFrom<Result<object>>(result);
        }
    }
}
