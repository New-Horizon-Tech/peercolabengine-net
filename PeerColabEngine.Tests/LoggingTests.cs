using System;
using Xunit;

namespace PeerColabEngine.Tests
{
    public class LoggingTests
    {
        [Fact]
        public void LogMessage_Constructor_SetsAllProperties()
        {
            var now = DateTime.UtcNow;
            var error = new Exception("test");
            var msg = new LogMessage("source", now, LogLevel.Error, "test message", error);

            Assert.Equal("source", msg.Source);
            Assert.Equal(now, msg.Timestamp);
            Assert.Equal(LogLevel.Error, msg.Level);
            Assert.Equal("test message", msg.Message);
            Assert.Same(error, msg.Error);
        }

        [Fact]
        public void LogMessage_IsWithin_FiltersByLevel()
        {
            var msg = new LogMessage("src", DateTime.Now, LogLevel.Warning, "warn");

            Assert.True(msg.IsWithin(LogLevel.Warning));
            Assert.True(msg.IsWithin(LogLevel.Info));
            Assert.True(msg.IsWithin(LogLevel.Trace));
            Assert.False(msg.IsWithin(LogLevel.Error));
            Assert.False(msg.IsWithin(LogLevel.Fatal));
        }

        [Fact]
        public void LogMessage_ToString_FormatsCorrectly()
        {
            var msg = new LogMessage("src", DateTime.Now, LogLevel.Info, "hello");
            var str = msg.ToString();

            Assert.Contains("Info", str);
            Assert.Contains("hello", str);
        }

        [Fact]
        public void LogMessage_ToString_WithError_IncludesErrorMessage()
        {
            var msg = new LogMessage("src", DateTime.Now, LogLevel.Error, "context", new Exception("details"));
            var str = msg.ToString();

            Assert.Contains("context", str);
            Assert.Contains("details", str);
        }

        [Fact]
        public void LogMessage_ToJson_ReturnsSameAsToString()
        {
            var msg = new LogMessage("src", DateTime.Now, LogLevel.Info, "test");
            Assert.Equal(msg.ToString(), msg.ToJson());
        }

        [Fact]
        public void DefaultLogger_Write_FiltersBasedOnLevel()
        {
            var logger = new DefaultLogger { LogLevel = LogLevel.Warning };

            // Should not throw even for filtered messages
            logger.Write(new LogMessage("src", DateTime.Now, LogLevel.Debug, "debug msg"));
            logger.Write(new LogMessage("src", DateTime.Now, LogLevel.Warning, "warning msg"));
        }

        [Fact]
        public void TestLogger_CapturesMessages()
        {
            var logger = new TestLogger();
            Logger.AssignLogger(logger);

            Logger.Info("test message");

            Assert.Single(logger.Messages);
            Assert.Equal("test message", logger.Messages[0].Message);
            Assert.Equal(LogLevel.Info, logger.Messages[0].Level);
        }

        [Fact]
        public void Logger_AllLevels_WriteCorrectLevel()
        {
            var logger = new TestLogger();
            Logger.AssignLogger(logger);

            Logger.Trace("trace");
            Logger.Debug("debug");
            Logger.Info("info");
            Logger.Warning("warning");
            Logger.Error("error");
            Logger.Fatal("fatal");

            Assert.Equal(6, logger.Messages.Count);
            Assert.Equal(LogLevel.Trace, logger.Messages[0].Level);
            Assert.Equal(LogLevel.Debug, logger.Messages[1].Level);
            Assert.Equal(LogLevel.Info, logger.Messages[2].Level);
            Assert.Equal(LogLevel.Warning, logger.Messages[3].Level);
            Assert.Equal(LogLevel.Error, logger.Messages[4].Level);
            Assert.Equal(LogLevel.Fatal, logger.Messages[5].Level);
        }

        [Fact]
        public void Logger_UpdateSource_SetsSource()
        {
            var logger = new TestLogger();
            Logger.AssignLogger(logger);
            Logger.UpdateSource("MyService");

            Logger.Info("test");

            var msg = logger.Messages.Find(m => m.Message == "test");
            Assert.NotNull(msg);
            Assert.Equal("MyService", msg.Source);
        }

        [Fact]
        public void Logger_WithError_PassesException()
        {
            var logger = new TestLogger();
            Logger.AssignLogger(logger);
            var ex = new Exception("boom");

            Logger.Error("something failed", ex);

            Assert.Same(ex, logger.Messages[0].Error);
        }

        [Fact]
        public void TestLogger_Clear_RemovesAllMessages()
        {
            var logger = new TestLogger();
            Logger.AssignLogger(logger);

            Logger.Info("msg1");
            Logger.Info("msg2");
            logger.Clear();

            Assert.Empty(logger.Messages);
        }

        [Fact]
        public void TestLogger_LogLevelFiltering()
        {
            var logger = new TestLogger { LogLevel = LogLevel.Error };
            Logger.AssignLogger(logger);

            Logger.Trace("trace");
            Logger.Info("info");
            Logger.Error("error");
            Logger.Fatal("fatal");

            Assert.Equal(2, logger.Messages.Count);
            Assert.All(logger.Messages, m =>
                Assert.True(m.Level <= LogLevel.Error));
        }

        [Fact]
        public void LogLevel_Ordering()
        {
            Assert.True(LogLevel.Fatal < LogLevel.Error);
            Assert.True(LogLevel.Error < LogLevel.Warning);
            Assert.True(LogLevel.Warning < LogLevel.Info);
            Assert.True(LogLevel.Info < LogLevel.Debug);
            Assert.True(LogLevel.Debug < LogLevel.Trace);
        }
    }
}
