using System;
using System.Collections.Generic;
using System.Web;

namespace MyCustomModules
{
    public class RateLimitModule : IHttpModule
    {
        // 限流时间间隔（秒）
        private const int THROTTLE_SECONDS = 5;
        
        // 定义一个静态 HashSet 用于存放需要限流保护的资源路径（全部转换为小写）
        private static readonly HashSet<string> ProtectedResources = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "/request/templatealllist.xml"
            // 以后新增受保护的资源，只需要在这里添加新路径即可，例如：
            // "/resource/flash/another_resource.png"
        };

        // 用于记录每个 (IP,资源) 请求的上次访问时间，确保原子性更新采用锁机制
        private static readonly Dictionary<string, DateTime> LastAccessTimes = new Dictionary<string, DateTime>();
        private static readonly object SyncLock = new object();

        public void Init(HttpApplication context)
        {
            context.BeginRequest += OnBeginRequest;
        }

        private void OnBeginRequest(object sender, EventArgs e)
        {
            HttpApplication app = (HttpApplication)sender;
            HttpContext context = app.Context;
            // 转换请求路径为小写，便于在 ProtectedResources 中比对
            string requestPath = context.Request.Path.ToLower();

            // 如果请求的资源在需要限流的集合中
            if (ProtectedResources.Contains(requestPath))
            {
                string ipAddress = GetClientIpAddress(context);
                // 以 IP+路径作为键值，方便针对同一资源、同一 IP 限流
                string key = ipAddress + ":" + requestPath;

                bool blockRequest = false;
                DateTime lastAccess = DateTime.MinValue;

                // 使用锁确保检查和更新是原子操作
                lock (SyncLock)
                {
                    if (LastAccessTimes.TryGetValue(key, out lastAccess))
                    {
                        TimeSpan elapsed = DateTime.Now - lastAccess;
                        if (elapsed.TotalSeconds < THROTTLE_SECONDS)
                        {
                            blockRequest = true;
                        }
                    }
                    // 无论是否阻断，都更新为当前时间
                    LastAccessTimes[key] = DateTime.Now;
                }

                if (blockRequest)
                {
                    int waitSeconds = Math.Max(1, (int)Math.Ceiling(THROTTLE_SECONDS - (DateTime.Now - lastAccess).TotalSeconds));
                    context.Response.Clear();
                    context.Response.StatusCode = 429;
                    context.Response.StatusDescription = "Too Many Requests";
                    context.Response.Write("Please Wait Another " + waitSeconds + " Seconds Before Next Request. Custom Module by @BigSmoke2T1");
                    context.Response.Flush();
                    // 结束当前请求，不再继续执行管道
                    app.CompleteRequest();
                }
            }
        }

        private string GetClientIpAddress(HttpContext context)
        {
            string ip = context.Request.ServerVariables["HTTP_X_FORWARDED_FOR"];
            if (string.IsNullOrEmpty(ip) || ip.ToLower() == "unknown")
                ip = context.Request.ServerVariables["HTTP_CLIENT_IP"];
            if (string.IsNullOrEmpty(ip) || ip.ToLower() == "unknown")
                ip = context.Request.ServerVariables["REMOTE_ADDR"];
            if (string.IsNullOrEmpty(ip) || ip.ToLower() == "unknown")
                ip = context.Request.UserHostAddress;
            if (!string.IsNullOrEmpty(ip) && ip.IndexOf(",") > 0)
                ip = ip.Split(',')[0].Trim();
            return ip;
        }

        public void Dispose()
        {
            // 如有需要，释放资源
        }
    }
}
