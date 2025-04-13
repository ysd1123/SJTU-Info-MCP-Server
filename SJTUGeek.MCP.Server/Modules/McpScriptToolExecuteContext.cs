using Microsoft.Extensions.Caching.Memory;

namespace SJTUGeek.MCP.Server.Modules
{
    public class McpScriptToolExecuteContext
    {
        public McpScriptToolExecuteContext(IServiceProvider serviceProvider)
        {
            ServiceProvider = serviceProvider;
        }

        public IServiceProvider ServiceProvider { get; set; }
        
        public JaCookieProvider ResolveJaCookieProvider()
        {
            return ServiceProvider.GetRequiredService<JaCookieProvider>();
        }
        
        public MemoryCacheWrapper ResolveMemoryCache()
        {
            return ServiceProvider.GetRequiredService<MemoryCacheWrapper>();
        }
    }
}
