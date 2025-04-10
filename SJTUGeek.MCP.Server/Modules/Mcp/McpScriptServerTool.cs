using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol.Types;
using ModelContextProtocol.Utils;
using ModelContextProtocol.Utils.Json;
using Python.Runtime;
using SJTUGeek.MCP.Server.Models;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json;
using SJTUGeek.MCP.Server.Extensions;

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
                    //var metadata = module.GetAttr("METADATA");
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
    public override async Task<CallToolResponse> InvokeAsync(
        RequestContext<CallToolRequestParams> request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        //AIFunctionArguments arguments = new()
        //{
        //    Services = request.Server?.Services,
        //    Context = new Dictionary<object, object?>() { [typeof(RequestContext<CallToolRequestParams>)] = request }
        //};
        JsonElement? result;
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
                    PyObject module = Py.Import(
                        "scripts." + Path.GetFileNameWithoutExtension(ScriptName)
                    );
                    PyObject func = module.GetAttr(ToolInfo.EntryPoint);
                    var callResult = func.Invoke(arguments.ToArray());

                    dynamic pyJson = scope.Import("json");
                    var callResultJSON = pyJson.dumps(callResult);
                    result = JsonSerializer.Deserialize<JsonElement>(callResultJSON.ToString());
                }
            }
        }
        catch (PythonException ex)
        {
            // 异常处理逻辑
            //Console.WriteLine($"执行插件失败: {plugin.Name}\n{ex.Message}");
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

        return result.Value.ValueKind switch
        {
            JsonValueKind.Number => new () {
                Content = [new() { Text = result.Value.ToString(), Type = "text" }]
            },
            JsonValueKind.String => new () {
                Content = [new() { Text = result.Value.ToString(), Type = "text" }]
            },
            //null => new()
            //{
            //    Content = []
            //},
            
            //string text => new()
            //{
            //    Content = [new() { Text = text, Type = "text" }]
            //},
            
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
            
            //CallToolResponse callToolResponse => callToolResponse,

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

}