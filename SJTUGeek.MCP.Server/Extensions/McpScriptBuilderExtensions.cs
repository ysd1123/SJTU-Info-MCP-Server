using ModelContextProtocol.Server;
using SJTUGeek.MCP.Server.Helpers;
using SJTUGeek.MCP.Server.Models;
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
                if (file.Extension == ".py" && AppCmdOption.Default.PythonDll != null)
                {
                    foreach (var tool in McpScriptServerTool.CreateBatch(file.Name))
                        if (AppCmdOption.Default.EnabledToolGroups == null ||
                            AppCmdOption.Default.EnabledToolGroups.Count == 0 || 
                            AppCmdOption.Default.EnabledToolGroups.Contains(tool.CategoryName))
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
