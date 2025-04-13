using Microsoft.Extensions.Caching.Memory;

namespace SJTUGeek.MCP.Server.Modules
{
    public class MemoryCacheWrapper
    {
        private readonly IMemoryCache _memoryCache;

        public MemoryCacheWrapper(IMemoryCache memoryCache)
        {
            _memoryCache = memoryCache;
        }

        public IMemoryCache MemoryCache { get { return _memoryCache; } }

        public object? Get(object key)
        {
            return _memoryCache.Get(key);
        }

        public void Set(object key, object value)
        {
            _memoryCache.Set(key, value);
        }

        public void Set(object key, object value, DateTimeOffset absoluteExpiration)
        {
            _memoryCache.Set(key, value, absoluteExpiration);
        }

        public void Set(object key, object value, TimeSpan absoluteExpirationRelativeToNow)
        {
            _memoryCache.Set(key, value, absoluteExpirationRelativeToNow);
        }
    }
}
