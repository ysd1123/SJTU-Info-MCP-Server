using ModelContextProtocol.Server;
using SJTUGeek.MCP.Server.Helpers;
using SJTUGeek.MCP.Server.StaticTools;

namespace SJTUGeek.MCP.Server.Extensions
{
    public static class McpScriptBuilderExtensions
    {
        public static IServiceCollection AddMcpScripts(this IServiceCollection services)
        {
            var scriptPath = Path.Combine(PathHelper.AppPath, "scripts");
            DirectoryInfo dirInfo = new DirectoryInfo(scriptPath);
            foreach (var file in dirInfo.GetFiles())
            {
                if (file.Extension == ".py")
                {
                    foreach (var tool in McpScriptServerTool.CreateBatch(file.Name))
                        services.AddSingleton(
                            (Func<IServiceProvider, McpServerTool>)(
                                services => tool
                            )
                        );
                }
            }
            
            return services;
        }
    }
}
