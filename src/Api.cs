using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;

namespace DeepSeekWhale
{
    public class CurrencyBalance
    {
        public decimal Total;
        public decimal Granted;
        public decimal Topped;
    }

    public class BalanceResult
    {
        public long T;
        public bool Available;
        public readonly Dictionary<string, CurrencyBalance> Cur = new Dictionary<string, CurrencyBalance>(StringComparer.OrdinalIgnoreCase);
        public string Error;
        public bool NetworkError;
        public bool Ok { get { return Error == null; } }
    }

    public static class Api
    {
        public static string BaseUrl = "https://api.deepseek.com";

        static Api()
        {
            try
            {
                ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072; // TLS 1.2
                ServicePointManager.Expect100Continue = false;
            }
            catch { }
        }

        public static BalanceResult Fetch(string key, bool useSystemProxy)
        {
            var r = FetchOnce(key, useSystemProxy);
            if (!r.Ok && r.NetworkError)
            {
                Log.Info("fetch failed via " + (useSystemProxy ? "system proxy" : "direct") + ", retrying the other way");
                var alt = FetchOnce(key, !useSystemProxy);
                if (alt.Ok) return alt;
            }
            return r;
        }

        static BalanceResult FetchOnce(string key, bool useSystemProxy)
        {
            var r = new BalanceResult();
            r.T = Fmt.NowUnix();
            if (string.IsNullOrEmpty(key))
            {
                r.Error = "还没有设置 API Key";
                return r;
            }
            try
            {
                string url = BaseUrl.TrimEnd('/') + "/user/balance";
                var req = (HttpWebRequest)WebRequest.Create(url);
                req.Method = "GET";
                req.Timeout = 15000;
                req.ReadWriteTimeout = 15000;
                req.KeepAlive = false;
                req.AllowAutoRedirect = true;
                req.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
                req.UserAgent = "DeepSeekWhale/1.0 (Windows)";
                req.Accept = "application/json";
                req.Headers["Authorization"] = "Bearer " + key.Trim();
                if (!useSystemProxy) req.Proxy = null;

                using (var resp = (HttpWebResponse)req.GetResponse())
                using (var stream = resp.GetResponseStream())
                using (var reader = new StreamReader(stream, Encoding.UTF8))
                {
                    string body = reader.ReadToEnd();
                    Parse(body, r);
                    return r;
                }
            }
            catch (WebException we)
            {
                r.Error = Describe(we);
                r.NetworkError = we.Response == null;
                Log.Warn("balance fetch failed: " + r.Error);
                return r;
            }
            catch (Exception ex)
            {
                r.Error = Friendly(ex.Message);
                r.NetworkError = true;
                Log.Warn("balance fetch failed: " + ex.Message);
                return r;
            }
        }

        internal static void Parse(string body, BalanceResult r)
        {
            var root = JsonUtil.Parse(body);
            r.Available = JsonUtil.Bool(root, "is_available", false);
            object infos;
            if (!root.TryGetValue("balance_infos", out infos) || infos == null)
            {
                r.Error = "接口返回格式不认识";
                return;
            }
            var list = infos as IEnumerable;
            if (list == null)
            {
                r.Error = "接口返回格式不认识";
                return;
            }
            foreach (object item in list)
            {
                var d = item as Dictionary<string, object>;
                if (d == null) continue;
                string cur = (JsonUtil.Str(d, "currency", "") ?? "").ToUpperInvariant();
                if (cur.Length == 0) continue;
                var cb = new CurrencyBalance();
                cb.Total = ParseMoney(JsonUtil.Str(d, "total_balance", "0"));
                cb.Granted = ParseMoney(JsonUtil.Str(d, "granted_balance", "0"));
                cb.Topped = ParseMoney(JsonUtil.Str(d, "topped_up_balance", "0"));
                r.Cur[cur] = cb;
            }
            if (r.Cur.Count == 0) r.Error = "接口没有返回余额信息";
        }

        static decimal ParseMoney(string s)
        {
            decimal v;
            if (decimal.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v)) return v;
            return 0m;
        }

        static string Describe(WebException we)
        {
            var resp = we.Response as HttpWebResponse;
            if (resp == null)
            {
                switch (we.Status)
                {
                    case WebExceptionStatus.Timeout: return "连接超时，稍后再试";
                    case WebExceptionStatus.NameResolutionFailure: return "域名解析失败，检查网络";
                    case WebExceptionStatus.ConnectFailure: return "连接不上 api.deepseek.com";
                    case WebExceptionStatus.ProxyNameResolutionFailure: return "代理设置有问题";
                    default: return Friendly(we.Message);
                }
            }
            int code = (int)resp.StatusCode;
            string detail = null;
            try
            {
                using (var stream = resp.GetResponseStream())
                using (var reader = new StreamReader(stream, Encoding.UTF8))
                {
                    string body = reader.ReadToEnd();
                    if (!string.IsNullOrEmpty(body))
                    {
                        try
                        {
                            var d = JsonUtil.Parse(body);
                            object errObj;
                            if (d.TryGetValue("error", out errObj))
                            {
                                var ed = errObj as Dictionary<string, object>;
                                if (ed != null) detail = JsonUtil.Str(ed, "message", null);
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }

            if (code == 401) return "API Key 无效或已过期，请重新设置";
            if (code == 402) return "账户余额不足";
            if (code == 429) return "请求太频繁，稍后再试";
            if (code >= 500) return "DeepSeek 服务暂时不可用（" + code + "）";
            if (!string.IsNullOrEmpty(detail)) return "接口错误：" + Fmt.Truncate(detail, 60);
            return "接口错误（HTTP " + code + "）";
        }

        static string Friendly(string msg)
        {
            if (string.IsNullOrEmpty(msg)) return "未知错误";
            return Fmt.Truncate(msg.Replace("\r", " ").Replace("\n", " "), 90);
        }
    }
}
