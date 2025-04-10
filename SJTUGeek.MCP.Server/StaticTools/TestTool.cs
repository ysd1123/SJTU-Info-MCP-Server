using ModelContextProtocol.Protocol.Types;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace SJTUGeek.MCP.Server.StaticTools;

[McpServerToolType]
public class TestTool
{
    [McpServerTool(Name = "test"), Description("Test system.")]
    public static string Test(RequestContext<CallToolRequestParams> context)
    {
        return "ok";
    }
}
