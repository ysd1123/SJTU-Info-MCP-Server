using SJTUGeek.MCP.Server.Helpers;
using System.Net;

namespace SJTUGeek.MCP.Server.Modules
{
    public class CookieContainerProvider
    {
        private readonly ILogger<CookieContainerProvider> _logger;
        private readonly Dictionary<string, CookieContainer> _dic;

        public CookieContainerProvider(ILogger<CookieContainerProvider> logger)
        {
            _logger = logger;
            _dic = new Dictionary<string, CookieContainer>();
        }

        public CookieContainer GetCookieContainer(string cookie)
        {
            lock (_dic)
            {
                var key = HashHelper.CRC32Hash(cookie);
                if (!_dic.TryGetValue(key, out CookieContainer? cc))
                {
                    cc = new CookieContainer();
                    _dic.Add(key, cc);
                    cc.Add(new Cookie("JAAuthCookie", cookie, "/jaccount", "jaccount.sjtu.edu.cn"));
                }
                return cc;
            }
        }
    }
}
