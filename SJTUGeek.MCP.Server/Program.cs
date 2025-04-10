using ModelContextProtocol.Protocol.Types;
using Python.Runtime;
using SJTUGeek.MCP.Server.Extensions;

namespace SJTUGeek.MCP.Server
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Runtime.PythonDLL = @"python310.dll";
            //PythonEngine.PythonPath = PythonEngine.PythonPath + ";" + Path.Combine(PathHelper.AppPath, "scripts");
            PythonEngine.Initialize();
            PythonEngine.BeginAllowThreads();

            var builder = WebApplication.CreateBuilder(args);

            builder.Logging.AddConsole(consoleLogOptions =>
            {
                // Configure all logs to go to stderr
                consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace;
            });

            builder.Services
                .AddMcpServer()
                //.WithTools<AddTool>()
                //.WithTools<TestTool>()
                ;

            builder.Services.AddMcpScripts();

            //builder.Services.AddControllers();

            builder.Services.AddSingleton<Func<LoggingLevel>>(_ => () => LoggingLevel.Debug);

            var app = builder.Build();

            //app.UseAuthorization();
            //app.MapControllers();
            app.MapMcp();
            app.Run();
        }
    }
}
