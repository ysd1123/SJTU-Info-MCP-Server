using System.CommandLine.Binding;
using System.CommandLine;
using System;
using System.Net;

namespace SJTUGeek.MCP.Server.Models
{
    public class AppCmdOption
    {
        public static AppCmdOption Default { get; set; }
        public int Port { get; set; }
        public string Host { get; set; }
        public string? PythonDll { get; set; }
        public string? JavaScriptEngine { get; set; }
        public bool EnableSse { get; set; }
        public string? JaAuthCookie { get; set; }
        public List<string>? EnabledToolGroups { get; set; }
    }

    public class AppCmdOptionBinder : BinderBase<AppCmdOption>
    {
        private readonly Option<int> _portOption;
        private readonly Option<string> _hostOption;
        private readonly Option<string?> _pyDllOption;
        private readonly Option<string?> _jsEngineOption;
        private readonly Option<bool> _sseOption;
        private readonly Option<string?> _cookieOption;
        private readonly Option<List<string>> _toolGroupOption;

        public AppCmdOptionBinder(Option<int> portOption, Option<string> hostOption, Option<string?> pyDllOption, Option<string?> jsEngineOption, Option<bool> sseOption, Option<string?> cookieOption, Option<List<string>> toolGroupOption)
        {
            _portOption = portOption;
            _hostOption = hostOption;
            _pyDllOption = pyDllOption;
            _jsEngineOption = jsEngineOption;
            _sseOption = sseOption;
            _cookieOption = cookieOption;
            _toolGroupOption = toolGroupOption;
        }

        protected override AppCmdOption GetBoundValue(BindingContext bindingContext)
        {
            return ValidateValues(bindingContext);
        }

        private AppCmdOption ValidateValues(BindingContext bindingContext)
        {
            var opt = new AppCmdOption
            {
                Port = bindingContext.ParseResult.GetValueForOption(_portOption),
                Host = bindingContext.ParseResult.GetValueForOption(_hostOption),
                PythonDll = bindingContext.ParseResult.GetValueForOption(_pyDllOption),
                JavaScriptEngine = bindingContext.ParseResult.GetValueForOption(_jsEngineOption),
                EnableSse = bindingContext.ParseResult.GetValueForOption(_sseOption),
                JaAuthCookie = bindingContext.ParseResult.GetValueForOption(_cookieOption),
                EnabledToolGroups = bindingContext.ParseResult.GetValueForOption(_toolGroupOption),
            };

            return opt;
        }
    }
}
