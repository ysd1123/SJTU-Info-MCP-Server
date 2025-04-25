using ModelContextProtocol.Server;
using SJTUGeek.MCP.Server.Helpers;
using SJTUGeek.MCP.Server.Models;
using System.Reflection;

namespace SJTUGeek.MCP.Server.Extensions
{
    public static class McpScriptBuilderExtensions
    {
        [Obsolete]
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

        public static void ConfigureMcpOptions(McpServerOptions options)
        {
            options.Capabilities ??= new();
            options.Capabilities.Tools ??= new();
            options.Capabilities.Tools.ToolCollection ??= new();

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
                            options.Capabilities.Tools.ToolCollection.Add(tool);
                }
            }
        }

        public static IMcpServerBuilder WithToolsFromCurrentAssembly(this IMcpServerBuilder builder)
        {
            var toolAssembly = Assembly.GetCallingAssembly();

            var toolTypes = from t in toolAssembly.GetTypes()
                        where t.GetCustomAttribute<McpServerToolTypeAttribute>() is not null
                        select t;

            var schemaCreateOptions = new Microsoft.Extensions.AI.AIJsonSchemaCreateOptions()
            {
                RequireAllProperties = false
            };

            foreach (var toolType in toolTypes)
            {
                if (toolType is not null)
                {
                    foreach (var toolMethod in toolType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance))
                    {
                        if (toolMethod.GetCustomAttribute<McpServerToolAttribute>() is not null)
                        {
                            builder.Services.AddSingleton((Func<IServiceProvider, McpServerTool>)(toolMethod.IsStatic ?
                                services => McpServerTool.Create(toolMethod, options: new() { Services = services, SchemaCreateOptions = schemaCreateOptions }) :
                                services => McpServerTool.Create(toolMethod, toolType, new() { Services = services, SchemaCreateOptions = schemaCreateOptions })));
                        }
                    }
                }
            }

            return builder;
        }
    }
}
