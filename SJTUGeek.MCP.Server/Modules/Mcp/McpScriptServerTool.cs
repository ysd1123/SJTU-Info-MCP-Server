using Microsoft.Extensions.AI;
using ModelContextProtocol.Protocol.Types;
using ModelContextProtocol.Utils.Json;
using Python.Runtime;
using SJTUGeek.MCP.Server.Models;
using System.Text.Json;
using SJTUGeek.MCP.Server.Extensions;
using SJTUGeek.MCP.Server.Modules;

namespace ModelContextProtocol.Server;

public class McpScriptServerTool : McpServerTool
{
    public static List<McpScriptServerTool> CreateBatch(string scriptName)
    {
        try
        {
            List<McpScriptServerTool> list = new List<McpScriptServerTool>();
            using (Py.GIL())
            {
                using (PyModule scope = Py.CreateScope())
                {
                    scope.Import(
                        "scripts." + Path.GetFileNameWithoutExtension(scriptName), "tool"
                    );
                    scope.Import("json");
                    var metadata = scope.Eval("json.dumps(tool.METADATA)");
                    var metadata_json = JsonSerializer.Deserialize<ScriptInfo>(metadata.ToString());

                    foreach (var toolInfo in metadata_json.Tools)
                    {
                        
                        Tool toolDef = new Tool();
                        toolDef.Name = toolInfo.Name;
                        toolDef.Description = toolInfo.Description;
                        toolDef.InputSchema = toolInfo.Schema;

                        list.Add(new McpScriptServerTool(scriptName, toolInfo, toolDef));
                    }
                }
            }
            return list;
        }
        catch (PythonException ex)
        {
            throw;
        }
    }

    /// <summary>Gets the <see cref="AIFunction"/> wrapped by this tool.</summary>
    internal string ScriptName { get; }
    internal ScriptToolInfo ToolInfo { get; }

    /// <summary>Initializes a new instance of the <see cref="McpServerTool"/> class.</summary>
    private McpScriptServerTool(string scriptName, ScriptToolInfo toolInfo, Tool toolDef)
    {
        ScriptName = scriptName;
        ToolInfo = toolInfo;
        ProtocolTool = toolDef;
    }

    /// <inheritdoc />
    public override string ToString() => ProtocolTool.Name.ToString();

    /// <inheritdoc />
    public override Tool ProtocolTool { get; }

    /// <inheritdoc />
    public override async ValueTask<CallToolResponse> InvokeAsync(
        RequestContext<CallToolRequestParams> request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        object? result;
        try
        {
            using (Py.GIL())
            {
                var arguments = new List<PyObject>();
                var argDict = request.Params?.Arguments;
                if (argDict is not null)
                {
                    foreach (var kvp in argDict)
                    {
                        arguments.Add(kvp.Value.GetCommonValue().ToPython());
                    }
                }

                using (PyModule scope = Py.CreateScope())
                {
                    //PyModule context_module = Py.CreateScope("mcp_context");
                    //context_module.Set("JaAuthCookie", cookieProvider.GetCookie());
                    //context_module.Set("ServiceProvider", request.Services);

                    //scope.Import("scripts.base");
                    PyObject context_module = scope.Import("scripts.base.mcp_context");
                    McpScriptToolExecuteContext context = new McpScriptToolExecuteContext(request.Services!);
                    context_module.SetAttr("CONTEXT", context.ToPython());

                    PyObject tool_module = scope.Import(
                        "scripts." + Path.GetFileNameWithoutExtension(ScriptName)
                    );
                    var callResult = tool_module.InvokeMethod(ToolInfo.EntryPoint, arguments.ToArray());

                    if (PyTuple.IsTupleType(callResult))
                    {
                        if (callResult.GetItem(0).As<bool>() == false)
                        {
                            dynamic pyJson = scope.Import("json");
                            var callResultJSON = pyJson.dumps(callResult.GetItem(1));
                            return new CallToolResponse()
                            {
                                IsError = true,
                                Content = [new() { Text = callResultJSON.ToString(), Type = "text" }],
                            };
                        }
                        callResult = callResult.GetItem(1);
                    }
                    if (PyString.IsStringType(callResult))
                    {
                        result = callResult.As<string>();
                    }
                    else if (callResult == PyType.None)
                    {
                        result = null;
                    }
                    else
                    {
                        dynamic pyJson = scope.Import("json");
                        var callResultJSON = pyJson.dumps(callResult);
                        result = JsonSerializer.Deserialize<JsonElement>(callResultJSON.ToString());
                    }
                }
            }
        }
        catch (PythonException ex)
        {
            return new CallToolResponse()
            {
                IsError = true,
                Content = [new() { Text = ex.Message, Type = "text" }],
            };
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return new CallToolResponse()
            {
                IsError = true,
                Content = [new() { Text = $"An error occurred invoking '{request.Params?.Name}'.", Type = "text" }],
            };
        }

        return result switch
        {
            JsonElement json => ConvertJsonElementToCallToolResponse(json),
            null => new()
            {
                Content = []
            },

            string text => new()
            {
                Content = [new() { Text = text, Type = "text" }]
            },

            //Content content => new()
            //{
            //    Content = [content]
            //},

            //IEnumerable<string> texts => new()
            //{
            //    Content = [.. texts.Select(x => new Content() { Type = "text", Text = x ?? string.Empty })]
            //},

            //IEnumerable<Content> contents => new()
            //{
            //    Content = [.. contents]
            //},

            CallToolResponse callToolResponse => callToolResponse,

            _ => new()
            {
                Content = [new()
                {
                    Text = JsonSerializer.Serialize(result, McpJsonUtilities.DefaultOptions.GetTypeInfo(typeof(object))),
                    Type = "text"
                }]
            },
        };
    }

    private CallToolResponse ConvertJsonElementToCallToolResponse(JsonElement json)
    {
        if (json.ValueKind == JsonValueKind.Number)
            return new()
            {
                Content = [new() { Text = json.ToString(), Type = "text" }]
            };
        else if (json.ValueKind == JsonValueKind.String)
            return new()
            {
                Content = [new() { Text = json.ToString(), Type = "text" }]
            };
        else
        {
            return new()
            {
                Content = [new()
                {
                    Text = JsonSerializer.Serialize(json, McpJsonUtilities.DefaultOptions.GetTypeInfo(typeof(object))),
                    Type = "text"
                }]
            };
        }
    }

}