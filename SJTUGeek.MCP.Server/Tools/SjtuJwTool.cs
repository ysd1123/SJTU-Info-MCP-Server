using ModelContextProtocol.Protocol.Types;
using ModelContextProtocol.Server;
using SJTUGeek.MCP.Server.Modules;
using System.ComponentModel;
using System.Net;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace SJTUGeek.MCP.Server.Tools;

[McpServerToolType]
public class SjtuJwTool
{
    private readonly ILogger<SjtuJwTool> _logger;
    private readonly HttpClient _client;

    public SjtuJwTool(ILogger<SjtuJwTool> logger, HttpClientFactory clientFactory)
    {
        _logger = logger;
        _client = clientFactory.CreateClient();
    }

    [McpServerTool(Name = "personal_course_table"), Description("Get class schedules for a given semester.")]
    public async Task<object> ToolPersonalCourseTable(
        [Description("The specified semester, defaults to the current semester if left blank.")]
        string? semester = null
    )
    {
        await Login();
        var courses = await GetPersonalCourseTable(semester);
        if (courses.KbList.Length == 0)
            return new CallToolResponse() { IsError = false, Content = new List<Content>() { new Content() { Text = "指定的学期没有课程！" } } };
        var res = RenderPersonalCourseTable(courses);
        return res;
    }

    [McpServerTool(Name = "personal_course_score"), Description("Get course scores for a given semester.")]
    public async Task<object> ToolCourseScoreList(
        [Description("The specified semester, defaults to the current semester if left blank.")]
        string? semester = null
    )
    {
        await Login();
        var scores = await GetCourseScoreList(semester);
        if (scores.Items.Length == 0)
            return new CallToolResponse() { IsError = false, Content = new List<Content>() { new Content() { Text = "找不到数据，可能是成绩还没有出哦~" } } };
        var res = RenderCourseScoreList(scores);
        return res;
    }

    [McpServerTool(Name = "personal_gpa_and_ranking"), Description("Get personal GPA and rankings.")]
    public async Task<object> ToolGpaAndRanking(
        [Description("Starting semester of statistics, defaults to the current semester if left blank.")]
        string? start_semester = null,
        [Description("Ending semester of statistics, defaults to the current semester if left blank.")]
        string? end_semester = null,
        [Description("Types of courses included, 'core' or 'all'.")]
        string? type = "core"
    )
    {
        await Login();
        var tj = await RequestGpaTj(start_semester, end_semester, type);
        if (!tj.StartsWith("统计成功"))
            return new CallToolResponse() { IsError = true, Content = new List<Content>() { new Content() { Text = "统计失败！" } } };
        var stat = await GetGpaTjResult();
        if (stat.Items.Length == 0)
            return new CallToolResponse() { IsError = true, Content = new List<Content>() { new Content() { Text = "找不到数据，请检查统计范围！" } } };
        var res = RenderGrades(stat.Items[0]);
        return res;
    }

    [McpServerTool(Name = "personal_exam_info"), Description("Obtain exam time and location information.")]
    public async Task<object> ToolExamInfo(
    [Description("The specified semester, defaults to the current semester if left blank.")]
        string? semester = null
    )
    {
        await Login();
        var exams = await GetExamInfo(semester);
        if (exams.Items.Length == 0)
            return new CallToolResponse() { IsError = false, Content = new List<Content>() { new Content() { Text = "找不到数据，可能是考试安排还没有出哦~" } } };
        var res = RenderExamInfoList(exams);
        return res;
    }

    public async Task<bool> Login()
    {
        var res = await _client.GetAsync("https://i.sjtu.edu.cn/jaccountlogin");
        while (res.StatusCode == HttpStatusCode.Found && res.Headers.Location.Scheme == "http")
        {
            res = await _client.GetAsync(res.Headers.Location.OriginalString.Replace("http", "https"));
        }
        res.EnsureSuccessStatusCode();
        if (!res.RequestMessage.RequestUri.OriginalString.StartsWith("https://i.sjtu.edu.cn/"))
        {
            throw new Exception("认证失败");
        }
        return true;
    }

    public static (string xn, string xq) GetCurrentXnXq()
    {
        DateTime now = DateTime.Now;
        int mm = now.Month;
        int yy = now.Year;
        string xq, xn;

        if (mm >= 9 || mm <= 2)
        {
            xq = "3"; // 第一学期
            xn = mm >= 9 ? yy.ToString() : (yy - 1).ToString();
        }
        else if (mm >= 3 && mm <= 7)
        {
            xq = "12"; // 第二学期
            xn = (yy - 1).ToString();
        }
        else
        {
            xq = "16"; // 第三学期
            xn = (yy - 1).ToString();
        }

        return (xn, xq);
    }

    public static (string xn, string xq) ParseXnXq(string semester)
    {
        var regex = new Regex(@"\d+");
        var matches = regex.Matches(semester);
        var integers = matches.Cast<Match>().Select(m => m.Value).ToList();

        if (integers.Count == 0)
            throw new Exception("学期学年格式错误！请使用类似「2024-2025学年第一学期」的格式");

        if (!int.TryParse(integers[0], out int xn) || xn < 2000)
            throw new Exception("学期学年格式错误！请使用类似「2024-2025学年第一学期」的格式");

        bool parseChinese = false;
        int xq = 0;

        if (integers.Count >= 2)
        {
            if (!int.TryParse(integers[1], out int t))
                throw new Exception("学期学年格式错误！请使用类似「2024-2025学年第一学期」的格式");

            if (t > 2000)
            {
                if (integers.Count >= 3)
                {
                    if (!int.TryParse(integers[2], out xq))
                        throw new Exception("学期学年格式错误！请使用类似「2024-2025学年第一学期」的格式");
                }
                else
                {
                    parseChinese = true;
                }
            }
            else
            {
                xq = t;
            }
        }
        else
        {
            parseChinese = true;
        }

        if (parseChinese)
        {
            if (semester.Contains("一")) xq = 1;
            else if (semester.Contains("二")) xq = 2;
            else if (semester.Contains("三")) xq = 3;
        }

        if (xq < 1 || xq > 3)
            throw new Exception("学期学年格式错误！请使用类似「2024-2025学年第一学期」的格式");

        int[] xqMap = { 3, 12, 16 };
        return (xn.ToString(), xqMap[xq - 1].ToString());
    }

    public async Task<JwPersonalCourseList> GetPersonalCourseTable(string? semester = null)
    {
        var xnxq = GetCurrentXnXq();
        if (semester != null)
        {
            xnxq = ParseXnXq(semester);
        }

        var forms = new Dictionary<string, string>();
        forms.Add("xnm", xnxq.xn);
        forms.Add("xqm", xnxq.xq);
        forms.Add("kzlx", "ck");
        forms.Add("xsdm", "");

        var req = new HttpRequestMessage(HttpMethod.Post, "https://i.sjtu.edu.cn/kbcx/xskbcx_cxXsgrkb.html?gnmkdm=N2151");
        req.Content = new FormUrlEncodedContent(forms);

        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        var json = await res.Content.ReadFromJsonAsync<JwPersonalCourseList>();
        return json;
    }

    public async Task<JwCourseScoreList> GetCourseScoreList(string? semester = null)
    {
        var xnxq = GetCurrentXnXq();
        if (semester != null)
        {
            xnxq = ParseXnXq(semester);
        }

        var forms = new Dictionary<string, string>();
        forms.Add("xnm", xnxq.xn);
        forms.Add("xqm", xnxq.xq);
        forms.Add("_search", "false");
        forms.Add("queryModel.showCount", "200");
        forms.Add("queryModel.currentPage", "1");
        forms.Add("queryModel.sortName", "");
        forms.Add("queryModel.sortOrder", "asc");
        forms.Add("time", "3");

        var req = new HttpRequestMessage(HttpMethod.Post, "https://i.sjtu.edu.cn/cjcx/cjcx_cxXsKccjList.html?gnmkdm=N305007");
        req.Content = new FormUrlEncodedContent(forms);

        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        var json = await res.Content.ReadFromJsonAsync<JwCourseScoreList>();
        return json;
    }

    public async Task<string> RequestGpaTj(
        string? start_semester = null,
        string? end_semester = null,
        string? type = "core"
    )
    {
        var xnxq1 = GetCurrentXnXq();
        if (start_semester != null)
        {
            xnxq1 = ParseXnXq(start_semester);
        }
        var xnxq2 = GetCurrentXnXq();
        if (end_semester != null)
        {
            xnxq2 = ParseXnXq(end_semester);
        }

        var forms = new Dictionary<string, string>();
        forms.Add("qsXnxq", $"{xnxq1.xn}{xnxq1.xq}");
        forms.Add("zzXnxq", $"{xnxq2.xn}{xnxq2.xq}");
        forms.Add("tjgx", "0");
        forms.Add("alsfj", "");
        forms.Add("sspjfblws", "9");
        forms.Add("pjjdblws", "9");
        forms.Add("bjpjf", "缓考,缓考(重考),尚未修读,暂不记录,中期退课,重考报名");
        forms.Add("bjjd", "缓考,缓考(重考),尚未修读,暂不记录,中期退课,重考报名");
        forms.Add("kch_ids", "MARX1205,TH009,TH020,FCE62B4E084826EBE055F8163EE1DCCC");
        forms.Add("bcjkc_id", "");
        forms.Add("bcjkz_id", "");
        forms.Add("cjkz_id", "");
        forms.Add("cjxzm", "zhyccj");
        forms.Add("kcfw", type == "core" ? "hxkc" : "qbkc");
        forms.Add("tjfw", "njzy");
        forms.Add("xjzt", "1");

        var req = new HttpRequestMessage(HttpMethod.Post, "https://i.sjtu.edu.cn/cjpmtj/gpapmtj_tjGpapmtj.html?gnmkdm=N309131");
        req.Content = new FormUrlEncodedContent(forms);

        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        var text = await res.Content.ReadAsStringAsync();
        return text.Trim('"');
    }

    public async Task<JwGpaQueryResult> GetGpaTjResult()
    {
        var forms = new Dictionary<string, string>();
        forms.Add("tjfw", "njzy");
        forms.Add("queryModel.showCount", "200");
        forms.Add("queryModel.currentPage", "1");
        forms.Add("queryModel.sortName", "");
        forms.Add("queryModel.sortOrder", "asc");

        var req = new HttpRequestMessage(HttpMethod.Post, "https://i.sjtu.edu.cn/cjpmtj/gpapmtj_cxGpaxjfcxIndex.html?doType=query&gnmkdm=N309131");
        req.Content = new FormUrlEncodedContent(forms);

        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        var json = await res.Content.ReadFromJsonAsync<JwGpaQueryResult>();
        return json;
    }

    public async Task<JwExamInfoResult> GetExamInfo(string? semester = null)
    {
        var xnxq = GetCurrentXnXq();
        if (semester != null)
        {
            xnxq = ParseXnXq(semester);
        }

        var forms = new Dictionary<string, string>();
        forms.Add("xnm", xnxq.xn);
        forms.Add("xqm", xnxq.xq);
        forms.Add("ksmcdmb_id", "");
        forms.Add("kch", "");
        forms.Add("kc", "");
        forms.Add("ksrq", "");
        forms.Add("kkbm_id", "");
        forms.Add("_search", "false");
        forms.Add("time", "5");
        forms.Add("queryModel.showCount", "200");
        forms.Add("queryModel.currentPage", "1");
        forms.Add("queryModel.sortName", "");
        forms.Add("queryModel.sortOrder", "asc");

        var req = new HttpRequestMessage(HttpMethod.Post, "https://i.sjtu.edu.cn/kwgl/kscx_cxXsksxxIndex.html?doType=query&gnmkdm=N358105");
        req.Content = new FormUrlEncodedContent(forms);

        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        var json = await res.Content.ReadFromJsonAsync<JwExamInfoResult>();
        return json;
    }

    public string RenderPersonalCourse(KbList c)
    {
        var res =
        $"- {c.Kcmc}（{c.Kch}）" + "\n" +
        $"  周数：{c.Zcd}" + "\n" +
        $"  校区：{c.Xqmc}" + "\n" +
        $"  上课时间：{c.Xqjmc} {c.Jc}" + "\n" +
        $"  上课地点：{c.Cdmc}" + "\n" +
        $"  教师：{c.Xm}" + "\n" +
        $"  教学班：{c.Jxbmc}" + "\n" +
        $"  选课备注：{(c.Xkbz.Trim() == "" ? "无" : c.Xkbz.Trim())}" + "\n" +
        $"  学分：{c.Xf}" + "\n" +
        $"  课程标记：{c.Kcbj}" + "\n" +
        $"  是否专业核心课程：{c.Zyhxkcbj}" + "\n" +
        "";
        return res;
    }

    public string RenderPersonalCourseTable(JwPersonalCourseList list)
    {
        return string.Join('\n', list.KbList.Select(x => RenderPersonalCourse(x)));
    }

    public string RenderSingleCourseScore(List<JwCourseScoreItem> c)
    {
        var res =
        $"- {c[0].Kcmc}（课程号：{c[0].Kch}；学分：{c[0].Xf}）" + "\n" +
        string.Join('\n', c.Select(x => $"  - {x.Xmblmc}：{x.Xmcj}"));
        return res;
    }

    public string RenderCourseScoreList(JwCourseScoreList list)
    {
        var groups = list.Items.GroupBy(x => x.Kch, y => y);
        return string.Join('\n', groups.Select(x => RenderSingleCourseScore(x.ToList())));
    }

    public string RenderGrades(JwGpaStatistic stat)
    {
        var res =
        $"总分：{stat.Zf}" + "\n" +
        $"门数：{stat.Ms}" + "\n" +
        $"不及格门数：{stat.Bjgms}" + "\n" +
        $"总学分：{stat.Zxf}" + "\n" +
        $"获得学分：{stat.Hdxf}" + "\n" +
        $"不及格学分：{stat.Bjgxf}" + "\n" +
        $"通过率：{stat.Tgl}" + "\n" +
        $"学积分：{stat.Xjf}" + "\n" +
        $"学积分排名：{stat.Xjfpm}" + "\n" +
        $"绩点(gpa)：{stat.Gpa}" + "\n" +
        $"绩点排名：{stat.Gpapm}" + "\n" +
        $"全部课程不及格门次：{stat.Bjgmc}" + "\n" +
        "";
        return res;
    }

    public string RenderSingleExamInfo(JwExamInfoItem item)
    {
        var res =
        $"- {item.Kcmc}（课程号：{item.Kch}）" + "\n" +
        $"  考试时间：{item.Kssj}" + "\n" +
        $"  考试地点：{item.Cdmc}" + "\n" +
        $"  考试方式：{item.Ksfs}";
        return res;
    }

    public string RenderExamInfoList(JwExamInfoResult list)
    {
        return string.Join('\n', list.Items.Select(x => RenderSingleExamInfo(x)));
    }
}

#region Models
public partial class JwPersonalCourseList
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("djdzList")]
    public string[] DjdzList { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("jfckbkg")]
    public bool? Jfckbkg { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("jxhjkcList")]
    public string[] JxhjkcList { get; set; }

    [JsonPropertyName("kbList")]
    public KbList[] KbList { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("kblx")]
    public long? Kblx { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("qsxqj")]
    public string Qsxqj { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("rqazcList")]
    public string[] RqazcList { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("sfxsd")]
    public string Sfxsd { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("sjfwkg")]
    public bool? Sjfwkg { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("sjkList")]
    public string[] SjkList { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("xkkg")]
    public bool? Xkkg { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("xnxqsfkz")]
    public string Xnxqsfkz { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("xqbzxxszList")]
    public string[] XqbzxxszList { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("xqjmcMap")]
    public XqjmcMap XqjmcMap { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("xsbjList")]
    public XsbjList[] XsbjList { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("xskbsfxstkzt")]
    public string Xskbsfxstkzt { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("xsxx")]
    public Xsxx Xsxx { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("zckbsfxssj")]
    public string Zckbsfxssj { get; set; }
}

public partial class KbList
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("bklxdjmc")]
    public string Bklxdjmc { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("cd_id")]
    public string CdId { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("cdbh")]
    public string Cdbh { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("cdlbmc")]
    public string Cdlbmc { get; set; }

    [JsonPropertyName("cdmc")]
    public string Cdmc { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("cxbj")]
    public string Cxbj { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("cxbjmc")]
    public string Cxbjmc { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("date")]
    public string Date { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("dateDigit")]
    public string DateDigit { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("dateDigitSeparator")]
    public string DateDigitSeparator { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("day")]
    public string Day { get; set; }

    [JsonPropertyName("jc")]
    public string Jc { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("jcor")]
    public string Jcor { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("jcs")]
    public string Jcs { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("jgh_id")]
    public string JghId { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("jgpxzd")]
    public string Jgpxzd { get; set; }

    [JsonPropertyName("jxb_id")]
    public string JxbId { get; set; }

    [JsonPropertyName("jxbmc")]
    public string Jxbmc { get; set; }

    [JsonPropertyName("jxbzc")]
    public string Jxbzc { get; set; }

    [JsonPropertyName("kcbj")]
    public string Kcbj { get; set; }

    [JsonPropertyName("kch")]
    public string Kch { get; set; }

    [JsonPropertyName("kch_id")]
    public string KchId { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("kclb")]
    public string Kclb { get; set; }

    [JsonPropertyName("kcmc")]
    public string Kcmc { get; set; }

    [JsonPropertyName("kcxszc")]
    public string Kcxszc { get; set; }

    [JsonPropertyName("kcxz")]
    public string Kcxz { get; set; }

    [JsonPropertyName("kczxs")]
    public string Kczxs { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("khfsmc")]
    public string Khfsmc { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("kkzt")]
    public string Kkzt { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("lh")]
    public string Lh { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("listnav")]
    public string Listnav { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("localeKey")]
    public string LocaleKey { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("month")]
    public string Month { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("oldjc")]
    public string Oldjc { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("oldzc")]
    public string Oldzc { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("pageable")]
    public bool? Pageable { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("pageTotal")]
    public long? PageTotal { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("pkbj")]
    public string Pkbj { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("px")]
    public string Px { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("qqqh")]
    public string Qqqh { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("queryModel")]
    public QueryModel QueryModel { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("rangeable")]
    public bool? Rangeable { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("rk")]
    public string Rk { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("rsdzjs")]
    public long? Rsdzjs { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("sfjf")]
    public string Sfjf { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("sfkckkb")]
    public bool? Sfkckkb { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("skfsmc")]
    public string Skfsmc { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("sxbj")]
    public string Sxbj { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("totalResult")]
    public string TotalResult { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("userModel")]
    public UserModel UserModel { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("xf")]
    public string Xf { get; set; }

    [JsonPropertyName("xkbz")]
    public string Xkbz { get; set; }

    [JsonPropertyName("xm")]
    public string Xm { get; set; }

    [JsonPropertyName("xnm")]
    public string Xnm { get; set; }

    [JsonPropertyName("xqdm")]
    public string Xqdm { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("xqh1")]
    public string Xqh1 { get; set; }

    [JsonPropertyName("xqh_id")]
    public string XqhId { get; set; }

    [JsonPropertyName("xqj")]
    public string Xqj { get; set; }

    [JsonPropertyName("xqjmc")]
    public string Xqjmc { get; set; }

    [JsonPropertyName("xqm")]
    public string Xqm { get; set; }

    [JsonPropertyName("xqmc")]
    public string Xqmc { get; set; }

    [JsonPropertyName("xsdm")]
    public string Xsdm { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("xslxbj")]
    public string Xslxbj { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("year")]
    public string Year { get; set; }

    [JsonPropertyName("zcd")]
    public string Zcd { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("zcmc")]
    public string Zcmc { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("zfjmc")]
    public string Zfjmc { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("zhxs")]
    public string Zhxs { get; set; }

    [JsonPropertyName("zxs")]
    public string Zxs { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("zxxx")]
    public string Zxxx { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("zyfxmc")]
    public string Zyfxmc { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("zyhxkcbj")]
    public string Zyhxkcbj { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("zzrl")]
    public string Zzrl { get; set; }
}

public partial class QueryModel
{
    [JsonPropertyName("currentPage")]
    public long CurrentPage { get; set; }

    [JsonPropertyName("currentResult")]
    public long CurrentResult { get; set; }

    [JsonPropertyName("entityOrField")]
    public bool EntityOrField { get; set; }

    [JsonPropertyName("limit")]
    public long Limit { get; set; }

    [JsonPropertyName("offset")]
    public long Offset { get; set; }

    [JsonPropertyName("pageNo")]
    public long PageNo { get; set; }

    [JsonPropertyName("pageSize")]
    public long PageSize { get; set; }

    [JsonPropertyName("showCount")]
    public long ShowCount { get; set; }

    [JsonPropertyName("sorts")]
    public string[] Sorts { get; set; }

    [JsonPropertyName("totalCount")]
    public long TotalCount { get; set; }

    [JsonPropertyName("totalPage")]
    public long TotalPage { get; set; }

    [JsonPropertyName("totalResult")]
    public long TotalResult { get; set; }
}

public partial class UserModel
{
    [JsonPropertyName("monitor")]
    public bool Monitor { get; set; }

    [JsonPropertyName("roleCount")]
    public long RoleCount { get; set; }

    [JsonPropertyName("roleKeys")]
    public string RoleKeys { get; set; }

    [JsonPropertyName("roleValues")]
    public string RoleValues { get; set; }

    [JsonPropertyName("status")]
    public long Status { get; set; }

    [JsonPropertyName("usable")]
    public bool Usable { get; set; }
}

public partial class XqjmcMap
{
    [JsonPropertyName("1")]
    public string The1 { get; set; }

    [JsonPropertyName("2")]
    public string The2 { get; set; }

    [JsonPropertyName("3")]
    public string The3 { get; set; }

    [JsonPropertyName("4")]
    public string The4 { get; set; }

    [JsonPropertyName("5")]
    public string The5 { get; set; }

    [JsonPropertyName("6")]
    public string The6 { get; set; }

    [JsonPropertyName("7")]
    public string The7 { get; set; }
}

public partial class XsbjList
{
    [JsonPropertyName("xsdm")]
    public string Xsdm { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("xslxbj")]
    public string Xslxbj { get; set; }

    [JsonPropertyName("xsmc")]
    public string Xsmc { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("ywxsmc")]
    public string Ywxsmc { get; set; }
}

public partial class Xsxx
{
    [JsonPropertyName("BJMC")]
    public string Bjmc { get; set; }

    [JsonPropertyName("JFZT")]
    public long Jfzt { get; set; }

    [JsonPropertyName("KCMS")]
    public long Kcms { get; set; }

    [JsonPropertyName("KXKXXQ")]
    public string Kxkxxq { get; set; }

    [JsonPropertyName("NJDM_ID")]
    public string NjdmId { get; set; }

    [JsonPropertyName("XH")]
    public string Xh { get; set; }

    [JsonPropertyName("XH_ID")]
    public string XhId { get; set; }

    [JsonPropertyName("XKKG")]
    public string Xkkg { get; set; }

    [JsonPropertyName("XKKGXQ")]
    public string Xkkgxq { get; set; }

    [JsonPropertyName("XM")]
    public string Xm { get; set; }

    [JsonPropertyName("XNM")]
    public string Xnm { get; set; }

    [JsonPropertyName("XNMC")]
    public string Xnmc { get; set; }

    [JsonPropertyName("XQM")]
    public string Xqm { get; set; }

    [JsonPropertyName("XQMMC")]
    public string Xqmmc { get; set; }

    [JsonPropertyName("YWXM")]
    public string Ywxm { get; set; }

    [JsonPropertyName("ZYH_ID")]
    public string ZyhId { get; set; }

    [JsonPropertyName("ZYMC")]
    public string Zymc { get; set; }
}

public partial class JwCourseScoreList
{
    [JsonPropertyName("currentPage")]
    public long CurrentPage { get; set; }

    [JsonPropertyName("currentResult")]
    public long CurrentResult { get; set; }

    [JsonPropertyName("entityOrField")]
    public bool EntityOrField { get; set; }

    [JsonPropertyName("items")]
    public JwCourseScoreItem[] Items { get; set; }

    [JsonPropertyName("limit")]
    public long Limit { get; set; }

    [JsonPropertyName("offset")]
    public long Offset { get; set; }

    [JsonPropertyName("pageNo")]
    public long PageNo { get; set; }

    [JsonPropertyName("pageSize")]
    public long PageSize { get; set; }

    [JsonPropertyName("showCount")]
    public long ShowCount { get; set; }

    [JsonPropertyName("sortName")]
    public string SortName { get; set; }

    [JsonPropertyName("sortOrder")]
    public string SortOrder { get; set; }

    [JsonPropertyName("sorts")]
    public string[] Sorts { get; set; }

    [JsonPropertyName("totalCount")]
    public long TotalCount { get; set; }

    [JsonPropertyName("totalPage")]
    public long TotalPage { get; set; }

    [JsonPropertyName("totalResult")]
    public long TotalResult { get; set; }
}

public partial class JwCourseScoreItem
{
    [JsonPropertyName("date")]
    public string Date { get; set; }

    [JsonPropertyName("dateDigit")]
    public string DateDigit { get; set; }

    [JsonPropertyName("dateDigitSeparator")]
    public string DateDigitSeparator { get; set; }

    [JsonPropertyName("day")]
    public string Day { get; set; }

    [JsonPropertyName("jgpxzd")]
    public string Jgpxzd { get; set; }

    [JsonPropertyName("jxb_id")]
    public string JxbId { get; set; }

    [JsonPropertyName("jxbmc")]
    public string Jxbmc { get; set; }

    [JsonPropertyName("kch")]
    public string Kch { get; set; }

    [JsonPropertyName("kch_id")]
    public string KchId { get; set; }

    [JsonPropertyName("kcmc")]
    public string Kcmc { get; set; }

    [JsonPropertyName("kkbm_id")]
    public string KkbmId { get; set; }

    [JsonPropertyName("kkbmmc")]
    public string Kkbmmc { get; set; }

    [JsonPropertyName("listnav")]
    public string Listnav { get; set; }

    [JsonPropertyName("localeKey")]
    public string LocaleKey { get; set; }

    [JsonPropertyName("month")]
    public string Month { get; set; }

    [JsonPropertyName("pageable")]
    public bool Pageable { get; set; }

    [JsonPropertyName("pageTotal")]
    public long PageTotal { get; set; }

    [JsonPropertyName("queryModel")]
    public QueryModel QueryModel { get; set; }

    [JsonPropertyName("rangeable")]
    public bool Rangeable { get; set; }

    [JsonPropertyName("row_id")]
    public string RowId { get; set; }

    [JsonPropertyName("totalResult")]
    public string TotalResult { get; set; }

    [JsonPropertyName("userModel")]
    public UserModel UserModel { get; set; }

    [JsonPropertyName("xf")]
    public string Xf { get; set; }

    [JsonPropertyName("xh_id")]
    public string XhId { get; set; }

    [JsonPropertyName("xmblmc")]
    public string Xmblmc { get; set; }

    [JsonPropertyName("xmcj")]
    public string Xmcj { get; set; }

    [JsonPropertyName("xnm")]
    public string Xnm { get; set; }

    [JsonPropertyName("xnmmc")]
    public string Xnmmc { get; set; }

    [JsonPropertyName("xqm")]
    public string Xqm { get; set; }

    [JsonPropertyName("xqmmc")]
    public string Xqmmc { get; set; }

    [JsonPropertyName("year")]
    public string Year { get; set; }
}

public partial class JwGpaQueryResult
{
    [JsonPropertyName("currentPage")]
    public long CurrentPage { get; set; }

    [JsonPropertyName("currentResult")]
    public long CurrentResult { get; set; }

    [JsonPropertyName("entityOrField")]
    public bool EntityOrField { get; set; }

    [JsonPropertyName("items")]
    public JwGpaStatistic[] Items { get; set; }

    [JsonPropertyName("limit")]
    public long Limit { get; set; }

    [JsonPropertyName("offset")]
    public long Offset { get; set; }

    [JsonPropertyName("pageNo")]
    public long PageNo { get; set; }

    [JsonPropertyName("pageSize")]
    public long PageSize { get; set; }

    [JsonPropertyName("showCount")]
    public long ShowCount { get; set; }

    [JsonPropertyName("sortName")]
    public string SortName { get; set; }

    [JsonPropertyName("sortOrder")]
    public string SortOrder { get; set; }

    [JsonPropertyName("sorts")]
    public string[] Sorts { get; set; }

    [JsonPropertyName("totalCount")]
    public long TotalCount { get; set; }

    [JsonPropertyName("totalPage")]
    public long TotalPage { get; set; }

    [JsonPropertyName("totalResult")]
    public long TotalResult { get; set; }
}

public partial class JwGpaStatistic
{
    [JsonPropertyName("bj")]
    public string Bj { get; set; }

    [JsonPropertyName("bjgmc")]
    public string Bjgmc { get; set; }

    [JsonPropertyName("bjgms")]
    public string Bjgms { get; set; }

    [JsonPropertyName("bjgxf")]
    public string Bjgxf { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("date")]
    public string Date { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("dateDigit")]
    public string DateDigit { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("dateDigitSeparator")]
    public string DateDigitSeparator { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("day")]
    public string Day { get; set; }

    [JsonPropertyName("gpa")]
    public string Gpa { get; set; }

    [JsonPropertyName("gpapm")]
    public string Gpapm { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("hdxf")]
    public string Hdxf { get; set; }

    [JsonPropertyName("jgmc")]
    public string Jgmc { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("jgpxzd")]
    public string Jgpxzd { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("kcfw")]
    public string Kcfw { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("listnav")]
    public string Listnav { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("localeKey")]
    public string LocaleKey { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("month")]
    public string Month { get; set; }

    [JsonPropertyName("ms")]
    public string Ms { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("njmc")]
    public string Njmc { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("pageable")]
    public bool? Pageable { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("pageTotal")]
    public long? PageTotal { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("pm1")]
    public string Pm1 { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("pm2")]
    public string Pm2 { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("queryModel")]
    public QueryModel QueryModel { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("rangeable")]
    public bool? Rangeable { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("row_id")]
    public string RowId { get; set; }

    [JsonPropertyName("tgl")]
    public string Tgl { get; set; }

    [JsonPropertyName("tj_id")]
    public string TjId { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("totalResult")]
    public string TotalResult { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("userModel")]
    public UserModel UserModel { get; set; }

    [JsonPropertyName("xh")]
    public string Xh { get; set; }

    [JsonPropertyName("xh_id")]
    public string XhId { get; set; }

    [JsonPropertyName("xjf")]
    public string Xjf { get; set; }

    [JsonPropertyName("xjfpm")]
    public string Xjfpm { get; set; }

    [JsonPropertyName("xm")]
    public string Xm { get; set; }

    [JsonPropertyName("year")]
    public string Year { get; set; }

    [JsonPropertyName("zf")]
    public string Zf { get; set; }

    [JsonPropertyName("zxf")]
    public string Zxf { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("zymc")]
    public string Zymc { get; set; }
}

public partial class JwExamInfoResult
{
    [JsonPropertyName("currentPage")]
    public long CurrentPage { get; set; }

    [JsonPropertyName("currentResult")]
    public long CurrentResult { get; set; }

    [JsonPropertyName("entityOrField")]
    public bool EntityOrField { get; set; }

    [JsonPropertyName("items")]
    public JwExamInfoItem[] Items { get; set; }

    [JsonPropertyName("limit")]
    public long Limit { get; set; }

    [JsonPropertyName("offset")]
    public long Offset { get; set; }

    [JsonPropertyName("pageNo")]
    public long PageNo { get; set; }

    [JsonPropertyName("pageSize")]
    public long PageSize { get; set; }

    [JsonPropertyName("showCount")]
    public long ShowCount { get; set; }

    [JsonPropertyName("sortName")]
    public string SortName { get; set; }

    [JsonPropertyName("sortOrder")]
    public string SortOrder { get; set; }

    [JsonPropertyName("totalCount")]
    public long TotalCount { get; set; }

    [JsonPropertyName("totalPage")]
    public long TotalPage { get; set; }

    [JsonPropertyName("totalResult")]
    public long TotalResult { get; set; }
}

public partial class JwExamInfoItem
{
    [JsonPropertyName("bj")]
    public string Bj { get; set; }

    [JsonPropertyName("cdbh")]
    public string Cdbh { get; set; }

    [JsonPropertyName("cdjc")]
    public string Cdjc { get; set; }

    [JsonPropertyName("cdmc")]
    public string Cdmc { get; set; }

    [JsonPropertyName("cdxqmc")]
    public string Cdxqmc { get; set; }

    [JsonPropertyName("cxbj")]
    public string Cxbj { get; set; }

    [JsonPropertyName("jgmc")]
    public string Jgmc { get; set; }

    [JsonPropertyName("jsxx")]
    public string Jsxx { get; set; }

    [JsonPropertyName("jxbmc")]
    public string Jxbmc { get; set; }

    [JsonPropertyName("jxbzc")]
    public string Jxbzc { get; set; }

    [JsonPropertyName("jxdd")]
    public string Jxdd { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("kccc")]
    public string Kccc { get; set; }

    [JsonPropertyName("kch")]
    public string Kch { get; set; }

    [JsonPropertyName("kcmc")]
    public string Kcmc { get; set; }

    [JsonPropertyName("khfs")]
    public string Khfs { get; set; }

    [JsonPropertyName("kkxy")]
    public string Kkxy { get; set; }

    [JsonPropertyName("ksfs")]
    public string Ksfs { get; set; }

    [JsonPropertyName("ksmc")]
    public string Ksmc { get; set; }

    [JsonPropertyName("kssj")]
    public string Kssj { get; set; }

    [JsonPropertyName("njmc")]
    public string Njmc { get; set; }

    [JsonPropertyName("row_id")]
    public long RowId { get; set; }

    [JsonPropertyName("sjbh")]
    public string Sjbh { get; set; }

    [JsonPropertyName("sksj")]
    public string Sksj { get; set; }

    [JsonPropertyName("totalresult")]
    public long Totalresult { get; set; }

    [JsonPropertyName("xb")]
    public string Xb { get; set; }

    [JsonPropertyName("xf")]
    public string Xf { get; set; }

    [JsonPropertyName("xh")]
    public string Xh { get; set; }

    [JsonPropertyName("xh_id")]
    public string XhId { get; set; }

    [JsonPropertyName("xm")]
    public string Xm { get; set; }

    [JsonPropertyName("xnm")]
    public string Xnm { get; set; }

    [JsonPropertyName("xnmc")]
    public string Xnmc { get; set; }

    [JsonPropertyName("xqm")]
    public string Xqm { get; set; }

    [JsonPropertyName("xqmc")]
    public string Xqmc { get; set; }

    [JsonPropertyName("xqmmc")]
    public string Xqmmc { get; set; }

    [JsonPropertyName("zxbj")]
    public string Zxbj { get; set; }

    [JsonPropertyName("zymc")]
    public string Zymc { get; set; }
}
#endregion