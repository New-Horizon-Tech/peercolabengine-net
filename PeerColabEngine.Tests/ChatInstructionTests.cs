using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace PeerColabEngine.Tests
{
    public class ChatInstructionTests
    {
        [Fact]
        public void ProcessChatInstruction_HasCorrectOperationId()
        {
            var op = new ProcessChatInstruction();
            Assert.Equal("PeerColab.Instructions.ProcessChatInstruction", op.Id);
        }

        [Fact]
        public void ProcessChatInstruction_HasCorrectVerb()
        {
            var op = new ProcessChatInstruction();
            Assert.Equal("PROCESS", op.Verb);
        }

        [Fact]
        public void ProcessChatInstruction_IsRequestType()
        {
            var op = new ProcessChatInstruction();
            Assert.Equal("request", op.Type);
        }

        [Fact]
        public void ProcessChatInstruction_RequiresTenant()
        {
            var op = new ProcessChatInstruction();
            Assert.True(op.Settings.RequiresTenant);
        }

        [Fact]
        public void ProcessChatInstruction_HasEmptyPathParameters()
        {
            var op = new ProcessChatInstruction();
            Assert.NotNull(op.PathParameters);
            Assert.Empty(op.PathParameters);
        }

        [Fact]
        public void ProcessChatInstruction_CreatesHandler()
        {
            var op = new ProcessChatInstruction();
            var handler = op.Handle(async (input, ctx) =>
            {
                return Result<ProcessChatInstructionOutput>.Ok(new ProcessChatInstructionOutput
                {
                    Message = "test",
                    Operations = new List<OutOfContextOperation>()
                });
            });
            Assert.NotNull(handler);
            Assert.Equal(op, handler.Operation);
        }

        [Fact]
        public void ChatInstruction_StoresAllProperties()
        {
            var instruction = new ChatInstruction
            {
                Type = "message",
                Role = "user",
                Content = "Hello"
            };
            Assert.Equal("message", instruction.Type);
            Assert.Equal("user", instruction.Role);
            Assert.Equal("Hello", instruction.Content);
        }

        [Fact]
        public void ProcessChatInstructionInput_StoresAllProperties()
        {
            var input = new ProcessChatInstructionInput
            {
                UsageInstructions = "Available operations: ...",
                CurrentStateSnapshot = "{}",
                Items = new List<ChatInstruction>
                {
                    new ChatInstruction { Type = "message", Role = "user", Content = "Hello" }
                }
            };
            Assert.Equal("Available operations: ...", input.UsageInstructions);
            Assert.Equal("{}", input.CurrentStateSnapshot);
            Assert.Single(input.Items);
            Assert.Equal("user", input.Items[0].Role);
        }

        [Fact]
        public void ProcessChatInstructionOutput_MessageIsOptional()
        {
            var output = new ProcessChatInstructionOutput
            {
                Operations = new List<OutOfContextOperation>()
            };
            Assert.Null(output.Message);
            Assert.Empty(output.Operations);
        }

        [Fact]
        public void ProcessChatInstructionOutput_SupportsOperationsWithPathParameters()
        {
            var output = new ProcessChatInstructionOutput
            {
                Message = "Created the resource",
                Operations = new List<OutOfContextOperation>
                {
                    new OutOfContextOperation
                    {
                        UsageId = "TestUsage",
                        OperationId = "TestApp.CreateResource",
                        OperationVerb = "CREATE",
                        OperationType = "request",
                        RequestJson = new { Name = "User" },
                        PathParameters = new List<OutOfContextOperationPathParameter>
                        {
                            new OutOfContextOperationPathParameter { Name = "SystemId", Value = "123" }
                        }
                    }
                }
            };
            Assert.Single(output.Operations);
            Assert.Equal("TestApp.CreateResource", output.Operations[0].OperationId);
            Assert.Single(output.Operations[0].PathParameters);
        }
    }
}
