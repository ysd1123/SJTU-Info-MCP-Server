using System.Text.Json;

namespace SJTUGeek.MCP.Server.Extensions
{
    public static class JsonElementExtension
    {
        public static object GetCommonValue(this JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.String)
            {
                return element.GetString();
            }
            else if (element.ValueKind == JsonValueKind.Number)
            {
                return element.GetInt64();
            }
            else if (element.ValueKind == JsonValueKind.True)
            {
                return true;
            }
            else if (element.ValueKind == JsonValueKind.False)
            {
                return false;
            }
            else if (element.ValueKind == JsonValueKind.Null)
            {
                return null;
            }
            else
            {
                throw new NotSupportedException();
            }
        }
    }
}
