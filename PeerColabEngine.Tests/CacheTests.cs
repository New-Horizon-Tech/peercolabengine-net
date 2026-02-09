using System;
using System.Threading.Tasks;
using Xunit;

namespace PeerColabEngine.Tests
{
    public class CacheTests
    {
        [Fact]
        public async Task InMemoryContextCache_PutAndGet_RoundTrips()
        {
            var cache = new InMemoryContextCache();
            var txId = Guid.NewGuid();
            var callInfo = CallInformation.New("en-GB", "tenant1");

            var putResult = await cache.Put(txId, callInfo);
            Assert.True(putResult);

            var retrieved = await cache.Get(txId);
            Assert.NotNull(retrieved);
            Assert.Equal("en-GB", retrieved.Locale);
            Assert.Equal("tenant1", retrieved.DataTenant);
        }

        [Fact]
        public async Task InMemoryContextCache_Get_ReturnsNullForMissing()
        {
            var cache = new InMemoryContextCache();
            var result = await cache.Get(Guid.NewGuid());
            Assert.Null(result);
        }

        [Fact]
        public async Task InMemoryContextCache_Put_OverwritesExisting()
        {
            var cache = new InMemoryContextCache();
            var txId = Guid.NewGuid();

            await cache.Put(txId, CallInformation.New("en-GB", "tenant1"));
            await cache.Put(txId, CallInformation.New("en-US", "tenant2"));

            var retrieved = await cache.Get(txId);
            Assert.Equal("en-US", retrieved.Locale);
            Assert.Equal("tenant2", retrieved.DataTenant);
        }

        [Fact]
        public async Task InMemoryContextCache_ExpiredEntries_ReturnNull()
        {
            // Create cache with very short TTL (1ms)
            var cache = new InMemoryContextCache(maxLifetimeMs: 1);
            var txId = Guid.NewGuid();

            await cache.Put(txId, CallInformation.New("en-GB"));
            await Task.Delay(10);

            var result = await cache.Get(txId);
            Assert.Null(result);
        }

        [Fact]
        public async Task InMemoryContextCache_MultipleEntries()
        {
            var cache = new InMemoryContextCache();
            var txId1 = Guid.NewGuid();
            var txId2 = Guid.NewGuid();

            await cache.Put(txId1, CallInformation.New("en-GB", "t1"));
            await cache.Put(txId2, CallInformation.New("en-US", "t2"));

            var r1 = await cache.Get(txId1);
            var r2 = await cache.Get(txId2);

            Assert.Equal("en-GB", r1.Locale);
            Assert.Equal("en-US", r2.Locale);
        }

        [Fact]
        public async Task FailingContextCache_Put_ReturnsFalse()
        {
            var cache = new FailingContextCache();
            var result = await cache.Put(Guid.NewGuid(), CallInformation.New("en-GB"));
            Assert.False(result);
        }

        [Fact]
        public async Task FailingContextCache_Get_ReturnsNull()
        {
            var cache = new FailingContextCache();
            var result = await cache.Get(Guid.NewGuid());
            Assert.Null(result);
        }

        [Fact]
        public async Task ThrowingContextCache_Put_Throws()
        {
            var cache = new ThrowingContextCache();
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => cache.Put(Guid.NewGuid(), CallInformation.New("en-GB")));
        }

        [Fact]
        public async Task ThrowingContextCache_Get_Throws()
        {
            var cache = new ThrowingContextCache();
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => cache.Get(Guid.NewGuid()));
        }
    }
}
