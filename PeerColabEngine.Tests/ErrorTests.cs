using Xunit;

namespace PeerColabEngine.Tests
{
    public class ErrorTests
    {
        [Fact]
        public void TransportError_Constructor_SetsCode()
        {
            var error = new TransportError("ERR001");

            Assert.Equal("ERR001", error.Code);
            Assert.NotNull(error.Related);
            Assert.Empty(error.Related);
        }

        [Fact]
        public void TransportError_Constructor_WithDetails()
        {
            var details = new TransportErrorDetails
            {
                TechnicalError = "NPE",
                UserError = "Something went wrong"
            };
            var error = new TransportError("ERR001", details);

            Assert.Equal("ERR001", error.Code);
            Assert.Equal("NPE", error.Details.TechnicalError);
            Assert.Equal("Something went wrong", error.Details.UserError);
        }

        [Fact]
        public void TransportError_StringDetailsConstructor()
        {
            var error = new TransportError("ERR001", "Technical detail");

            Assert.Equal("ERR001", error.Code);
            Assert.Equal("Technical detail", error.Details.TechnicalError);
        }

        [Fact]
        public void TransportError_ToShortString_WithoutDetails()
        {
            var error = new TransportError("ERR001");

            Assert.Equal("ERR001", error.ToShortString());
        }

        [Fact]
        public void TransportError_ToShortString_WithDetails()
        {
            var error = new TransportError("ERR001", "Some technical error");

            Assert.Equal("ERR001 - Some technical error", error.ToShortString());
        }

        [Fact]
        public void TransportError_ToString_WithRelated()
        {
            var error = new TransportError("ERR001", "Main error");
            error.Related.Add(new TransportError("ERR002", "Related error"));

            var str = error.ToString();

            Assert.Contains("ERR001", str);
            Assert.Contains("ERR002", str);
            Assert.Contains("Related errors", str);
        }

        [Fact]
        public void TransportError_ToString_WithoutRelated()
        {
            var error = new TransportError("ERR001", "Main error");
            var str = error.ToString();

            Assert.Equal("ERR001 - Main error", str);
            Assert.DoesNotContain("Related", str);
        }

        [Fact]
        public void TransportError_ToLongString_IncludesAllDetails()
        {
            var details = new TransportErrorDetails
            {
                TechnicalError = "NPE",
                TransactionId = "tx-123",
                CalledOperation = "test.get",
                SessionIdentifier = "session-1",
                CallingClient = "client-1"
            };
            var error = new TransportError("ERR001", details);

            var str = error.ToLongString();

            Assert.Contains("tx-123", str);
            Assert.Contains("test.get", str);
            Assert.Contains("session-1", str);
            Assert.Contains("client-1", str);
            Assert.Contains("ERR001", str);
        }

        [Fact]
        public void TransportError_ToLongString_WithParent()
        {
            var parent = new TransportError("PARENT_ERR", "Parent failed");
            var error = new TransportError("CHILD_ERR", "Child failed")
            {
                Parent = parent
            };

            var str = error.ToLongString();

            Assert.Contains("CHILD_ERR", str);
            Assert.Contains("PARENT_ERR", str);
            Assert.Contains("Parent error", str);
        }

        [Fact]
        public void TransportErrorDetails_AllProperties()
        {
            var details = new TransportErrorDetails
            {
                TechnicalError = "tech",
                UserError = "user",
                SessionIdentifier = "sess",
                CallingClient = "client",
                CalledOperation = "op",
                TransactionId = "tx"
            };

            Assert.Equal("tech", details.TechnicalError);
            Assert.Equal("user", details.UserError);
            Assert.Equal("sess", details.SessionIdentifier);
            Assert.Equal("client", details.CallingClient);
            Assert.Equal("op", details.CalledOperation);
            Assert.Equal("tx", details.TransactionId);
        }

        [Fact]
        public void TransportError_DefaultConstructor()
        {
            var error = new TransportError();
            Assert.Null(error.Code);
            Assert.Null(error.Details);
        }
    }
}
