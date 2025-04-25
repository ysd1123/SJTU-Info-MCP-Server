using ModelContextProtocol.Server;
using System.ComponentModel;

namespace SJTUGeek.MCP.Server.Tools;

[McpServerToolType]
public class SjtuJwTool
{
    [McpServerTool(Name = "personal_course_table"), Description("Get class schedules for a given semester.")]
    public static string PersonalCourseTable(
        [Description("The specified semester, defaults to the current semester if left blank.")]
        string? semester = null
    )
    {
        return "";
    }
}
