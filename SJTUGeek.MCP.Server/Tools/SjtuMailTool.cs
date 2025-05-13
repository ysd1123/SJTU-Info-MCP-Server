using ModelContextProtocol.Protocol.Types;
using ModelContextProtocol.Server;
using SJTUGeek.MCP.Server.Modules;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Teru.Code.Zimbra;

namespace SJTUGeek.MCP.Server.Tools
{
    [McpServerToolType]
    public class SjtuMailTool
    {
        private readonly ILogger<SjtuMailTool> _logger;
        private readonly HttpClient _client;
        private readonly ZimbraClient _mail;
        private readonly CookieContainerProvider _ccProvider;
        private readonly JaCookieProvider _cookieProvider;

        private string? userToken;

        public SjtuMailTool(ILogger<SjtuMailTool> logger, HttpClientFactory clientFactory, CookieContainerProvider cc, JaCookieProvider cookieProvider)
        {
            _logger = logger;
            _client = clientFactory.CreateClient();
            _mail = new ZimbraClient("https://mail.sjtu.edu.cn/service/soap");
            _ccProvider = cc;
            _cookieProvider = cookieProvider;
        }

        [McpServerTool(Name = "get_personal_mails"), Description("Get the list of emails in the specified inbox in your personal mailbox.")]
        public async Task<object> ToolGetMails(
            [Description("The specified mailbox.")]
            MailBoxTypeEnum mailbox = MailBoxTypeEnum.Inbox,
            [Description("Page index.")]
            int page = 1
        )
        {
            await Login();
            var mails = await GetMails(mailbox, page);
            if (mails.M == null || mails.M.Length == 0)
                return new CallToolResponse() { IsError = false, Content = new List<Content>() { new Content() { Text = "找不到邮件！" } } };
            var res = RenderMailList(mails);
            return res;
        }

        [McpServerTool(Name = "get_mail"), Description("Get details of a single email.")]
        public async Task<object> ToolGetSingleMail(
            [Description("The id of the email, as returned by the mailing list result.")]
            int mailId
        )
        {
            await Login();
            var mail = await GetSingleMail(mailId, false);
            if (mail.M == null)
                return new CallToolResponse() { IsError = false, Content = new List<Content>() { new Content() { Text = "找不到邮件！" } } };
            var res = RenderSingleMail(mail.M.First());
            return res;
        }

        [McpServerTool(Name = "mark_mail"), Description("Mark a single email as read.")]
        public async Task<object> ToolMarkSingleMail(
            [Description("The id of the email, as returned by the mailing list result.")]
            int mailId
        )
        {
            await Login();
            var mail = await GetSingleMail(mailId, true);
            if (mail.M == null)
                return new CallToolResponse() { IsError = false, Content = new List<Content>() { new Content() { Text = "找不到邮件！" } } };
            return "标记成功！";
        }

        [McpServerTool(Name = "send_mail"), Description("Send an email to a specified user.")]
        public async Task<object> ToolSendMail(
            [Description("The recipient's email address.")]
            string receiver,
            [Description("The title of the email.")]
            string title,
            [Description("The content of the email, in plain text.")]
            string content,
            [Description("The email address of the CC recipient. Can be empty.")]
            string? cc = null
        )
        {
            await Login();
            var systemInfo = await GetSystemInfo();
            var selfInfo = systemInfo.Identities.Identity.First();
            List<ZimbraMailParticipant> participants = new List<ZimbraMailParticipant>();
            participants.Add(new ZimbraMailParticipant() { A = selfInfo.Attrs["zimbraPrefFromAddress"], P = selfInfo.Attrs["zimbraPrefFromDisplay"], T = "f" });
            participants.Add(new ZimbraMailParticipant() { A = receiver, P = receiver.Split('@').First(), T = "t" });
            if (cc != null)
            {
                participants.Add(new ZimbraMailParticipant() { A = cc, P = cc.Split('@').First(), T = "c" });
            }
            var mail = await SendMail(participants, title, content);
            return "发送成功！";
        }

        public async Task<bool> Login()
        {
            var res = await _client.GetAsync("https://mail.sjtu.edu.cn");
            while (res.StatusCode == HttpStatusCode.Found && res.Headers.Location.Scheme == "http")
            {
                res = await _client.GetAsync(res.Headers.Location.OriginalString.Replace("http", "https"));
            }
            res.EnsureSuccessStatusCode();
            if (!res.RequestMessage.RequestUri.OriginalString.StartsWith("https://mail.sjtu.edu.cn"))
            {
                throw new Exception("认证失败");
            }
            var cc = _ccProvider.GetCookieContainer(_cookieProvider.GetCookie());
            var cookies = cc.GetCookies(new Uri("https://mail.sjtu.edu.cn")).Cast<Cookie>().ToList();
            var tokenCookie = cookies.FirstOrDefault(x => x.Name == "ZM_AUTH_TOKEN");
            if (tokenCookie != null)
            {
                userToken = tokenCookie.Value;
            }
            else
            {
                throw new Exception("认证失败");
            }
            return true;
        }

        public async Task<ZimbraSearchResponse> GetMails(MailBoxTypeEnum mailbox, int page, int pageSize = 15)
        {
            JsonRequest request = _mail.GenRequest("json", userToken) as JsonRequest;
            request.AddRequest("SearchRequest", $$"""
                            {
                    "sortBy": "dateDesc",
                    "header": [
                        {
                        "n": "List-ID"
                        },
                        {
                        "n": "X-Zimbra-DL"
                        },
                        {
                        "n": "IN-REPLY-TO"
                        }
                    ],
                    "tz": {
                        "id": "Asia/Hong_Kong"
                    },
                    "locale": {
                        "_content": "zh_CN"
                    },
                    "offset": {{(page-1) * pageSize}},
                    "limit": {{pageSize}},
                    "query": "in:{{mailbox.ToString().ToLower()}}",
                    "types": "message",
                    "recip": "0",
                    "needExp": 1
                }
                """,
                "urn:zimbraMail");
            var resp = await _mail.SendRequest(request);

            if (resp.IsFault())
            {
                var message = resp.GetFaultMessage().First().Value;
                throw new Exception(message);
            }
            else
            {
                var res = JsonSerializer.Deserialize<Dictionary<string, ZimbraSearchResponse>>(resp.GetResponse());
                return res.First().Value;
            }
        }

        public async Task<ZimbraGetMsgResponse> GetSingleMail(int mailId, bool markRead)
        {
            JsonRequest request = _mail.GenRequest("json", userToken) as JsonRequest;
            request.AddRequest("GetMsgRequest", $$"""
                   {
                  "m": {
                    "id": "{{mailId}}",
                    "html": 1,
                    "read": {{(markRead ? 1 : 0)}},
                    "needExp": 1,
                    "header": [
                      {
                        "n": "List-ID"
                      },
                      {
                        "n": "X-Zimbra-DL"
                      },
                      {
                        "n": "IN-REPLY-TO"
                      }
                    ],
                    "max": 250000
                  }
                }
                """,
                "urn:zimbraMail");
            var resp = await _mail.SendRequest(request);

            if (resp.IsFault())
            {
                var message = resp.GetFaultMessage().First().Value;
                throw new Exception(message);
            }
            else
            {
                var res = JsonSerializer.Deserialize<Dictionary<string, ZimbraGetMsgResponse>>(resp.GetResponse());
                return res.First().Value;
            }
        }

        public async Task<ZimbraGetInfoResponse> GetSystemInfo()
        {
            JsonRequest request = _mail.GenRequest("json", userToken) as JsonRequest;
            request.AddRequest("GetInfoRequest", $$"""
                   {}
                """,
                "urn:zimbraAccount");
            var resp = await _mail.SendRequest(request);

            if (resp.IsFault())
            {
                var message = resp.GetFaultMessage().First().Value;
                throw new Exception(message);
            }
            else
            {
                var res = JsonSerializer.Deserialize<Dictionary<string, ZimbraGetInfoResponse>>(resp.GetResponse());
                return res.First().Value;
            }
        }

        public async Task<ZimbraSendMsgResponse> SendMail(List<ZimbraMailParticipant> participants, string title, string content)
        {
            JsonRequest request = _mail.GenRequest("json", userToken) as JsonRequest;
            request.AddRequest("SendMsgRequest", $$"""
                  {
                  "m": {
                    "e": {{JsonSerializer.Serialize(participants)}},
                    "su": {
                      "_content": {{JsonSerializer.Serialize(title)}}
                    },
                    "mp": [
                      {
                        "ct": "text/plain",
                        "content": {
                          "_content": {{JsonSerializer.Serialize(content)}}
                        }
                      }
                    ]
                  }
                }
                """,
                "urn:zimbraMail");
            var resp = await _mail.SendRequest(request);

            if (resp.IsFault())
            {
                var message = resp.GetFaultMessage().First().Value;
                throw new Exception(message);
            }
            else
            {
                var res = JsonSerializer.Deserialize<Dictionary<string, ZimbraSendMsgResponse>>(resp.GetResponse());
                return res.First().Value;
            }
        }

        public string RenderSingleMail(ZimbraMailInfo m)
        {
            var sender = m.E.FirstOrDefault(x => x.T == "f");
            var res =
            $"- 来自 \"{sender?.P ?? "未知发件人"}\" <{sender?.A ?? "未知发件地址"}> 的邮件：" + "\n" +
            (m.E.Length > 1 ? ConvertOtherParticipants(m.E.Except([sender])) + "\n" : "") +
            $"  标题：{m.Su}" + "\n" +
            $"  id：{m.Id}" + "\n" +
            $"  时间：{DateTimeOffset.FromUnixTimeMilliseconds(m.D).DateTime.ToString("G")}" + "\n" +
            (m.F != null ? $"  属性：{ConvertMailFlags(m.F)}" + "\n" : "") +
            (m.Mp != null ? $"  内容：{m.Mp.First().Content}" : $"  摘要：{m.Fr}")
            ;
            return res;
        }

        public string ConvertOtherParticipants(IEnumerable<ZimbraMailParticipant?> participants)
        {
            IEnumerable<string> InternalConvert(IEnumerable<ZimbraMailParticipant?> participants)
            {
                foreach (var p in participants)
                {
                    //(f)rom, (t)o, (c)c, (b)cc, (r)eply-to, (s)ender, read-receipt (n)otification, (rf) resent-from
                    var type = p.T switch
                    {
                        "f" => "发件人",
                        "t" => "收件人",
                        "c" => "抄送",
                        "b" => "密送",
                        "r" => "回复",
                        "s" => "实际发送人",
                        "rf" => "重定向自",
                        _ => "未知类型参与人"
                    };
                    yield return $"  {type}：\"{p.P}\" <{p.A}>";
                }
            }
            return string.Join("\n", InternalConvert(participants));
        }

        public string ConvertMailFlags(string f)
        {
            //(u)nread, (f)lagged, has (a)ttachment, (r)eplied, (s)ent by me, for(w)arded, calendar in(v)ite, (d)raft, IMAP-\Deleted (x), (n)otification sent, urgent (!), low-priority (?), priority (+)
            string InternalStateConvert(char c)
            {
                return c switch
                {
                    'u' => "未读",
                    'f' => "标记",
                    'a' => "有附件",
                    'r' => "已回复",
                    's' => "我发送的邮件",
                    'w' => "已转发",
                    'v' => "日程邀请",
                    'd' => "草稿",
                    'x' => "已删除",
                    'n' => "通知已发送",
                    '!' => "紧急",
                    '?' => "低重要性",
                    '+' => "重要",
                    _ => "未知"
                };
            }
            return string.Join('，', f.Select(x => InternalStateConvert(x)));
        }

        public string RenderMailList(ZimbraSearchResponse list)
        {
            return string.Join('\n', list.M.Select(x => RenderSingleMail(x)));
        }
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum MailBoxTypeEnum
    {
        Inbox, Sent, Drafts, Junk, Trash
    }

    #region models
    public partial class ZimbraSingleResponseWrapper<T>
    {
        [JsonPropertyName("Body")]
        public Dictionary<string, T> Body { get; set; }

        [JsonPropertyName("Header")]
        public Dictionary<string, object> Header { get; set; }
    }

    public partial class ZimbraSearchResponse
    {
        [JsonPropertyName("m")]
        public ZimbraMailInfo[] M { get; set; }

        [JsonPropertyName("more")]
        public bool More { get; set; }

        [JsonPropertyName("offset")]
        public long Offset { get; set; }

        [JsonPropertyName("sortBy")]
        public string SortBy { get; set; }
    }

    public partial class ZimbraGetMsgResponse
    {
        [JsonPropertyName("m")]
        public ZimbraMailInfo[] M { get; set; }
    }

    public partial class ZimbraSendMsgResponse
    {
        [JsonPropertyName("m")]
        public ZimbraMailInfo[] M { get; set; }
    }

    public partial class ZimbraGetInfoResponse
    {
        [JsonPropertyName("accessed")]
        public long Accessed { get; set; }

        [JsonPropertyName("attSizeLimit")]
        public long AttSizeLimit { get; set; }

        [JsonPropertyName("crumb")]
        public string Crumb { get; set; }

        [JsonPropertyName("docSizeLimit")]
        public long DocSizeLimit { get; set; }

        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("identities")]
        public ZimbraIdentities Identities { get; set; }

        [JsonPropertyName("isSpellCheckAvailable")]
        public bool IsSpellCheckAvailable { get; set; }

        [JsonPropertyName("isTrackingIMAP")]
        public bool IsTrackingImap { get; set; }

        [JsonPropertyName("lifetime")]
        public long Lifetime { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("pasteitcleanedEnabled")]
        public bool PasteitcleanedEnabled { get; set; }

        [JsonPropertyName("prevSession")]
        public long PrevSession { get; set; }

        [JsonPropertyName("publicURL")]
        public string PublicUrl { get; set; }

        [JsonPropertyName("recent")]
        public long Recent { get; set; }

        [JsonPropertyName("rest")]
        public string Rest { get; set; }

        [JsonPropertyName("soapURL")]
        public string SoapUrl { get; set; }

        [JsonPropertyName("used")]
        public long Used { get; set; }

        [JsonPropertyName("version")]
        public string Version { get; set; }
    }

    public partial class ZimbraIdentities
    {
        [JsonPropertyName("identity")]
        public ZimbraIdentity[] Identity { get; set; }
    }

    public partial class ZimbraIdentity
    {
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [JsonPropertyName("_attrs")]
        public Dictionary<string, string> Attrs { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [JsonPropertyName("name")]
        public string Name { get; set; }
    }

    public partial class ZimbraMailInfo
    {
        [JsonPropertyName("cid")]
        public string Cid { get; set; }

        [JsonPropertyName("cm")]
        public bool Cm { get; set; }

        [JsonPropertyName("d")]
        public long D { get; set; }

        [JsonPropertyName("e")]
        public ZimbraMailParticipant[] E { get; set; }

        [JsonPropertyName("f")]
        public string? F { get; set; }

        [JsonPropertyName("fr")]
        public string Fr { get; set; }

        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("l")]
        public string L { get; set; }

        [JsonPropertyName("rev")]
        public long Rev { get; set; }

        [JsonPropertyName("s")]
        public long S { get; set; }

        [JsonPropertyName("sf")]
        public string Sf { get; set; }

        [JsonPropertyName("su")]
        public string Su { get; set; }

        [JsonPropertyName("mp")]
        public ZimbraMailContent[] Mp { get; set; }
    }

    public partial class ZimbraMailContent
    {
        [JsonPropertyName("body")]
        public bool Body { get; set; }

        [JsonPropertyName("content")]
        public string Content { get; set; }

        [JsonPropertyName("ct")]
        public string Ct { get; set; }

        [JsonPropertyName("part")]
        public string Part { get; set; }

        [JsonPropertyName("s")]
        public int S { get; set; }
    }

    public partial class ZimbraMailParticipant
    {
        [JsonPropertyName("a")]
        public string A { get; set; }

        [JsonPropertyName("d")]
        public string D { get; set; }

        [JsonPropertyName("p")]
        public string P { get; set; }

        [JsonPropertyName("t")]
        public string T { get; set; }
    }
    #endregion
}
