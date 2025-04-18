using System.Text.Json;
using System.Text.Json.Serialization;

namespace SJTUGeek.MCP.Server.Models
{
    public class ScriptInfo
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("version")]
        public string Version { get; set; }

        [JsonPropertyName("author")]
        public string Author { get; set; }

        [JsonPropertyName("category")]
        public string Category { get; set; }

        [JsonPropertyName("require_auth")]
        public bool RequireAuth { get; set; }

        [JsonPropertyName("tools")]
        public List<ScriptToolInfo> Tools { get; set; }
    }

    public class ScriptToolInfo
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("entrypoint")]
        public string EntryPoint { get; set; }

        [JsonPropertyName("schema")]
        public JsonElement Schema { get; set; }
    }

    public class ScriptToolSchema
    {
        [JsonPropertyName("title")]
        public string Title { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("properties")]
        public Dictionary<string, ScriptToolProperty>? Properties { get; set; }

        [JsonPropertyName("required")]
        public List<string>? Required { get; set; }
    }

    public class ScriptToolProperty
    {
        [JsonPropertyName("descriptionype")]
        public string? Description { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("enum")]
        public List<string>? Enum { get; set; }
    }
}
