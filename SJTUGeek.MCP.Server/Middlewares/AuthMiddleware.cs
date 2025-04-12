using SJTUGeek.MCP.Server.Models;
using System.Text;

public class AuthMiddleware
{
    private readonly RequestDelegate _next;

    public AuthMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (AppCmdOption.Default.JaAuthCookie != null)
        {
            context.Items["JaAuthCookie"] = AppCmdOption.Default.JaAuthCookie;
        }
        await _next(context);
        return;
    }
}