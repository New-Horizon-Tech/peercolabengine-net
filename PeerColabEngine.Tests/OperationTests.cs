using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace PeerColabEngine.Tests
{
    public class OperationTests
    {
        [Fact]
        public void OperationVerb_AllValues()
        {
            Assert.Equal(OperationVerb.GET, (OperationVerb)0);
            Assert.Equal(12, Enum.GetValues(typeof(OperationVerb)).Length);
        }

        [Fact]
        public void OperationVerbs_All_ContainsExpectedVerbs()
        {
            var all = OperationVerbs.All;

            Assert.Contains(OperationVerb.GET, all);
            Assert.Contains(OperationVerb.CREATE, all);
            Assert.Contains(OperationVerb.ADD, all);
            Assert.Contains(OperationVerb.UPDATE, all);
            Assert.Contains(OperationVerb.PATCH, all);
            Assert.Contains(OperationVerb.REMOVE, all);
            Assert.Contains(OperationVerb.DELETE, all);
            Assert.Contains(OperationVerb.START, all);
            Assert.Contains(OperationVerb.STOP, all);
            Assert.Contains(OperationVerb.PROCESS, all);
        }

        [Fact]
        public void TransportOperation_Constructor_SetsProperties()
        {
            var op = new TransportOperation<TestDto, TestResultDto>(
                "request", "test.get", "GET",
                new List<string> { "id" },
                new TransportOperationSettings { RequiresTenant = true }
            );

            Assert.Equal("test.get", op.Id);
            Assert.Equal("request", op.Type);
            Assert.Equal("GET", op.Verb);
            Assert.Single(op.PathParameters);
            Assert.True(op.Settings.RequiresTenant);
        }

        [Fact]
        public void RequestOperation_HasRequestType()
        {
            Assert.Equal("request", GetTestOperation.Instance.Type);
            Assert.Equal("test.get", GetTestOperation.Instance.Id);
            Assert.Equal("GET", GetTestOperation.Instance.Verb);
        }

        [Fact]
        public void MessageOperation_HasMessageType()
        {
            Assert.Equal("message", NotifyTestOperation.Instance.Type);
            Assert.Equal("test.notify", NotifyTestOperation.Instance.Id);
            Assert.Equal("PROCESS", NotifyTestOperation.Instance.Verb);
        }

        [Fact]
        public void RequestOperation_Handle_CreatesHandler()
        {
            var handler = GetTestOperation.Instance.Handle(async (input, ctx) =>
            {
                return Result<TestResultDto>.Ok(new TestResultDto { Result = input.Name, Processed = true });
            });

            Assert.NotNull(handler);
            Assert.IsType<RequestOperationHandler<TestDto, TestResultDto>>(handler);
            Assert.Same(GetTestOperation.Instance, handler.Operation);
        }

        [Fact]
        public void MessageOperation_Handle_CreatesHandler()
        {
            var handler = NotifyTestOperation.Instance.Handle(async (input, ctx) =>
            {
                return Result<object>.Ok();
            });

            Assert.NotNull(handler);
            Assert.IsType<MessageOperationHandler<TestDto>>(handler);
            Assert.Same(NotifyTestOperation.Instance, handler.Operation);
        }

        [Fact]
        public void RequestOperationRequest_AsOperationInformation()
        {
            var request = new RequestOperationRequest<TestDto, TestResultDto>(
                "usage1",
                GetTestOperation.Instance,
                new TestDto { Name = "test" }
            );

            var opInfo = request.AsOperationInformation("client1");

            Assert.Equal("test.get", opInfo.Id);
            Assert.Equal("GET", opInfo.Verb);
            Assert.Equal("request", opInfo.Type);
            Assert.Equal("client1", opInfo.CallingClient);
            Assert.Equal("usage1", opInfo.UsageId);
        }

        [Fact]
        public void MessageOperationRequest_Properties()
        {
            var input = new TestDto { Name = "notify", Value = 1 };
            var request = new MessageOperationRequest<TestDto>(
                "usage2",
                NotifyTestOperation.Instance,
                input
            );

            Assert.Equal("usage2", request.UsageId);
            Assert.Same(input, request.Input);
            Assert.Same(NotifyTestOperation.Instance, request.Operation);
        }

        [Fact]
        public void TransportOperationSettings_Properties()
        {
            var setup = new TransportOperationCharacterSetup
            {
                Performer = new TransportOperationCharacter
                {
                    Required = true,
                    ValidTypes = new List<string> { "user", "system" }
                },
                Subject = new TransportOperationCharacter
                {
                    Required = false,
                    ValidTypes = new List<string> { "entity" }
                }
            };

            var settings = new TransportOperationSettings
            {
                RequiresTenant = true,
                CharacterSetup = setup
            };

            Assert.True(settings.RequiresTenant);
            Assert.True(settings.CharacterSetup.Performer.Required);
            Assert.Equal(2, settings.CharacterSetup.Performer.ValidTypes.Count);
            Assert.False(settings.CharacterSetup.Subject.Required);
        }

        [Fact]
        public void OutOfContextOperation_Properties()
        {
            var op = new OutOfContextOperation
            {
                UsageId = "u1",
                OperationId = "op.get",
                OperationVerb = "GET",
                OperationType = "request",
                RequestJson = new { Name = "test" },
                PathParameters = new List<OutOfContextOperationPathParameter>
                {
                    new OutOfContextOperationPathParameter { Name = "id", Value = "123" }
                }
            };

            Assert.Equal("u1", op.UsageId);
            Assert.Equal("op.get", op.OperationId);
            Assert.Equal("GET", op.OperationVerb);
            Assert.Equal("request", op.OperationType);
            Assert.Single(op.PathParameters);
            Assert.Equal("id", op.PathParameters[0].Name);
            Assert.Equal("123", op.PathParameters[0].Value);
        }

        [Fact]
        public void OutOfContextOperationPathParameter_Defaults()
        {
            var param = new OutOfContextOperationPathParameter();

            Assert.Equal(string.Empty, param.Name);
            Assert.Equal(string.Empty, param.Value);
        }
    }
}
