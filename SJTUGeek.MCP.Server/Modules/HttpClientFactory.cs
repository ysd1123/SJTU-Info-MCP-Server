using System.Net;

namespace SJTUGeek.MCP.Server.Modules
{
    public class HttpClientFactory
    {
        private readonly ILogger<HttpClientFactory> _logger;
        private readonly JaCookieProvider _cookieProvider;
        private readonly CookieContainerProvider _cc;

        public HttpClientFactory(ILogger<HttpClientFactory> logger, JaCookieProvider cookieProvider, CookieContainerProvider cc)
        {
            _logger = logger;
            _cookieProvider = cookieProvider;
            _cc = cc;
        }

        public HttpClient CreateClient()
        {
            var cc = _cc.GetCookieContainer(_cookieProvider.GetCookie());
            var client = new HttpClient(new HttpClientHandler() { AllowAutoRedirect = true, AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip, UseCookies = true, CookieContainer = cc });
            
            return client;
        }
    }
}
