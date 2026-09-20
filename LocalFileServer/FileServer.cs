using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Collections.Specialized;

namespace LocalFileServer;

/// <summary>
/// HTTP 静态文件服务器核心（TcpListener 实现）。
/// 用 TcpListener 绑定 0.0.0.0 监听所有网卡，自解析 HTTP 请求，
/// 完全绕开 http.sys 的主机名校验（避免 HttpListener 的
/// "Bad Request - Invalid Hostname" 400 错误），无需管理员权限。
/// </summary>
public class FileServer
{
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private readonly object _lock = new();

    public event EventHandler<LogEventArgs>? LogEvent;

    /// <summary>共享根目录</summary>
    public string RootPath { get; set; } = AppContext.BaseDirectory;

    /// <summary>监听端口</summary>
    public int Port { get; set; } = 8848;

    /// <summary>是否启用访问口令</summary>
    public bool AuthEnabled { get; set; }

    /// <summary>访问口令</summary>
    public string AuthToken { get; set; } = "";

    public bool IsRunning => _listener != null;

    private void Log(string msg) => LogEvent?.Invoke(this, new LogEventArgs(msg));

    public static string[] GetLocalIPv4s()
    {
        var hosts = Dns.GetHostEntry(Dns.GetHostName());
        return hosts.AddressList
            .Where(a => a.AddressFamily == AddressFamily.InterNetwork)
            .Select(a => a.ToString())
            .ToArray();
    }

    public static string Hostname => Dns.GetHostName();

    public string BaseUrl => $"http://{GetPreferredIP()}:{Port}";

    private string GetPreferredIP()
    {
        var ips = GetLocalIPv4s();
        if (ips.Length == 0) return "127.0.0.1";
        // 优先选 192.168 / 10. / 172. 的私有地址
        var priv = ips.FirstOrDefault(i => i.StartsWith("192.168.") || i.StartsWith("10.") || i.StartsWith("172."));
        return priv ?? ips[0];
    }

    public void Start()
    {
        if (IsRunning) return;
        // 确保共享目录存在
        try { Directory.CreateDirectory(RootPath); } catch { }

        try
        {
            _listener = new TcpListener(IPAddress.Any, Port);
            _listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _listener.Start();
            _cts = new CancellationTokenSource();
            Log($"服务器已启动: {BaseUrl}  (监听 0.0.0.0:{Port})");
            Log($"共享目录: {RootPath}");
            Log($"可用地址: {string.Join(", ", GetLocalIPv4s().Select(ip => $"http://{ip}:{Port}"))}");
            if (AuthEnabled) Log("已启用访问口令");
            _ = Task.Run(AcceptLoop);
        }
        catch (Exception ex)
        {
            Log($"启动失败: {ex.Message}");
            _listener = null;
        }
    }

    public void Stop()
    {
        if (_listener == null) return;
        try
        {
            _cts?.Cancel();
            _listener.Stop();
            _listener.Server.Close();
        }
        catch { }
        _listener = null;
        Log("服务器已停止");
    }

    private async Task AcceptLoop()
    {
        while (IsRunning && _listener != null)
        {
            try
            {
                var client = await _listener.AcceptTcpClientAsync(_cts?.Token ?? CancellationToken.None);
                _ = Task.Run(() => HandleClient(client));
            }
            catch (Exception ex)
            {
                if (!IsRunning) break;
                if (ex is OperationCanceledException) break;
                Log($"接收连接异常: {ex.Message}");
            }
        }
    }

    // ---------------- 请求解析 ----------------

    private sealed class HttpRequest
    {
        public string Method = "GET";
        public string RawPath = "/";
        public string Path = "/";
        public NameValueCollection Query = new();
        public NameValueCollection Headers = new();
        public byte[] Body = Array.Empty<byte>();
    }

    private void HandleClient(TcpClient client)
    {
        try
        {
            using var stream = client.GetStream();
            stream.ReadTimeout = 30000;

            byte[] buffer = new byte[8192];
            var requestBytes = new MemoryStream();
            int total = 0;
            int headerEnd = -1;
            int contentLength = 0;

            while (headerEnd < 0)
            {
                int n = stream.Read(buffer, 0, buffer.Length);
                if (n <= 0) return;
                requestBytes.Write(buffer, 0, n);
                total += n;
                var text = Encoding.ASCII.GetString(requestBytes.GetBuffer(), 0, total);
                headerEnd = text.IndexOf("\r\n\r\n", StringComparison.Ordinal);
            }

            var headerText = Encoding.ASCII.GetString(requestBytes.GetBuffer(), 0, headerEnd);
            var headerLines = headerText.Split("\r\n");
            var requestLine = headerLines.Length > 0 ? headerLines[0] : "";

            var req = ParseRequestLine(requestLine);
            for (int i = 1; i < headerLines.Length; i++)
            {
                var line = headerLines[i];
                var idx = line.IndexOf(':');
                if (idx > 0)
                {
                    var k = line.Substring(0, idx).Trim();
                    var v = line.Substring(idx + 1).Trim();
                    req.Headers[k] = v;
                    if (k.Equals("Content-Length", StringComparison.OrdinalIgnoreCase))
                        int.TryParse(v, out contentLength);
                }
            }

            // 读取 body（若声明了 Content-Length）
            long bodySoFar = total - headerEnd - 4;
            while (bodySoFar < contentLength)
            {
                int n = stream.Read(buffer, 0, buffer.Length);
                if (n <= 0) break;
                requestBytes.Write(buffer, 0, n);
                bodySoFar += n;
            }
            if (contentLength > 0)
            {
                req.Body = requestBytes.ToArray();
                // 只保留 body 部分（请求头已解析）
                req.Body = req.Body.Skip(headerEnd + 4).Take(contentLength).ToArray();
            }

            var resp = HandleRequest(req);
            stream.Write(resp);
            stream.Flush();
        }
        catch (Exception ex)
        {
            Log($"处理连接异常: {ex.Message}");
        }
        finally
        {
            try { client.Close(); } catch { }
        }
    }

    private static HttpRequest ParseRequestLine(string line)
    {
        var req = new HttpRequest();
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2)
        {
            req.Method = parts[0].ToUpperInvariant();
            req.RawPath = parts[1];
        }
        var qIdx = req.RawPath.IndexOf('?');
        if (qIdx >= 0)
        {
            req.Path = Uri.UnescapeDataString(req.RawPath.Substring(0, qIdx));
            var queryStr = req.RawPath.Substring(qIdx + 1);
            foreach (var pair in queryStr.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var eq = pair.IndexOf('=');
                if (eq > 0)
                {
                    req.Query[pair.Substring(0, eq)] = Uri.UnescapeDataString(pair.Substring(eq + 1));
                }
                else
                {
                    req.Query[pair] = "";
                }
            }
        }
        else
        {
            req.Path = Uri.UnescapeDataString(req.RawPath);
        }
        return req;
    }

    // ---------------- 响应构造 ----------------

    private static byte[] MakeResponse(int code, string reason, string contentType, byte[] body, string? extraHeader = null)
    {
        var sb = new StringBuilder();
        sb.Append($"HTTP/1.1 {code} {reason}\r\n");
        sb.Append("Server: LocalFileServer\r\n");
        sb.Append("Connection: close\r\n");
        sb.Append($"Content-Type: {contentType}\r\n");
        sb.Append($"Content-Length: {body.Length}\r\n");
        if (extraHeader != null) sb.Append(extraHeader).Append("\r\n");
        sb.Append("\r\n");
        var head = Encoding.ASCII.GetBytes(sb.ToString());
        var all = new byte[head.Length + body.Length];
        Buffer.BlockCopy(head, 0, all, 0, head.Length);
        Buffer.BlockCopy(body, 0, all, head.Length, body.Length);
        return all;
    }

    private static byte[] MakeText(int code, string reason, string text)
        => MakeResponse(code, reason, "text/plain; charset=utf-8", Encoding.UTF8.GetBytes(text));

    private static byte[] MakeRedirect(string location)
        => MakeResponse(302, "Found", "text/plain", Array.Empty<byte>(), $"Location: {location}\r\n");

    private byte[] HandleRequest(HttpRequest req)
    {
        try
        {
            var path = req.Path;

            // 访问口令校验
            if (AuthEnabled && !string.IsNullOrEmpty(AuthToken))
            {
                var token = req.Query["token"] ?? req.Headers["X-Auth-Token"];
                if (token != AuthToken && path != "/" && path != "/favicon.ico")
                {
                    return MakeText(401, "Unauthorized", "需要访问口令 token=xxx");
                }
            }

            if (req.Method == "GET" && path == "/")
                return ServeIndex();
            if (req.Method == "GET" && path == "/api/files")
                return ServeFileList();
            if (req.Method == "GET" && path.StartsWith("/api/download/"))
                return ServeDownload(path);
            if (req.Method == "POST" && path == "/api/upload")
                return ServeUpload(req);
            if (req.Method == "POST" && path == "/api/delete")
                return ServeDelete(req);
            if (req.Method == "GET" && path == "/favicon.ico")
                return MakeResponse(204, "No Content", "text/plain", Array.Empty<byte>());
            return ServeStatic(path);
        }
        catch (Exception ex)
        {
            Log($"处理请求异常: {ex.Message}");
            try { return MakeText(500, "Internal Server Error", "服务器内部错误"); }
            catch { return MakeText(500, "Internal Server Error", "error"); }
        }
    }

    private byte[] ServeIndex()
    {
        var html = WebResources.IndexHtml.Replace("__BASE__", BaseUrl);
        return MakeResponse(200, "OK", "text/html; charset=utf-8", Encoding.UTF8.GetBytes(html));
    }

    private byte[] ServeFileList()
    {
        var files = new List<object>();
        if (Directory.Exists(RootPath))
        {
            foreach (var f in Directory.GetFiles(RootPath))
            {
                var fi = new FileInfo(f);
                files.Add(new
                {
                    name = fi.Name,
                    size = fi.Length,
                    modified = fi.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss")
                });
            }
        }
        var json = JsonSerializer.Serialize(new { files });
        return MakeResponse(200, "OK", "application/json; charset=utf-8", Encoding.UTF8.GetBytes(json));
    }

    private byte[] ServeDownload(string path)
    {
        var name = path.Substring("/api/download/".Length);
        var safe = Path.GetFileName(name);
        var full = Path.Combine(RootPath, safe);
        if (!File.Exists(full))
            return MakeText(404, "Not Found", "文件不存在");
        var bytes = File.ReadAllBytes(full);
        var cdHeader = $"Content-Disposition: attachment; filename=\"{safe}\"\r\nAccept-Ranges: bytes";
        Log($"下载: {safe} ({bytes.Length} B)");
        return MakeResponse(200, "OK", GetMime(safe), bytes, cdHeader);
    }

    private byte[] ServeUpload(HttpRequest req)
    {
        var name = req.Query["name"];
        if (string.IsNullOrEmpty(name))
        {
            var cd = req.Headers["Content-Disposition"] ?? "";
            var m = System.Text.RegularExpressions.Regex.Match(cd, "filename=\"?([^\"]+)\"?");
            if (m.Success) name = m.Groups[1].Value;
        }
        if (string.IsNullOrEmpty(name))
            return MakeText(400, "Bad Request", "缺少文件名");
        if (req.Body.Length == 0)
            return MakeText(400, "Bad Request", "无数据");

        var safe = Path.GetFileName(name);
        var full = Path.Combine(RootPath, safe);
        File.WriteAllBytes(full, req.Body);
        Log($"上传: {safe} ({req.Body.Length} B)");
        return MakeText(200, "OK", "上传成功: " + safe);
    }

    private byte[] ServeDelete(HttpRequest req)
    {
        try
        {
            var j = JsonSerializer.Deserialize<JsonElement>(Encoding.UTF8.GetString(req.Body));
            var name = j.GetProperty("name").GetString();
            if (string.IsNullOrEmpty(name))
                return MakeText(400, "Bad Request", "缺少文件名");
            var safe = Path.GetFileName(name);
            var full = Path.Combine(RootPath, safe);
            if (File.Exists(full))
            {
                File.Delete(full);
                Log($"删除: {safe}");
                return MakeText(200, "OK", "已删除");
            }
            return MakeText(404, "Not Found", "文件不存在");
        }
        catch
        {
            return MakeText(400, "Bad Request", "请求格式错误");
        }
    }

    private byte[] ServeStatic(string path)
    {
        var name = Path.GetFileName(path);
        if (string.IsNullOrEmpty(name)) return MakeText(404, "Not Found", "not found");
        var full = Path.Combine(RootPath, name);
        if (!File.Exists(full)) return MakeText(404, "Not Found", "文件不存在");
        var bytes = File.ReadAllBytes(full);
        Log($"访问: {name}");
        return MakeResponse(200, "OK", GetMime(name), bytes);
    }

    private static string GetMime(string name)
    {
        var ext = Path.GetExtension(name).ToLower();
        return ext switch
        {
            ".html" => "text/html; charset=utf-8",
            ".txt" or ".log" or ".md" => "text/plain; charset=utf-8",
            ".js" => "application/javascript",
            ".css" => "text/css",
            ".json" => "application/json",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".sh" => "application/x-sh",
            _ => "application/octet-stream"
        };
    }
}

public class LogEventArgs : EventArgs
{
    public string Message { get; }
    public LogEventArgs(string msg) => Message = msg;
}
