// FIS Local Injector - Quick tool for testing local bundles
// Compile: csc /target:winexe /out:FISLocalInjector.exe fis-local-injector.cs
// Usage: Run, select a .js bundle file, it auto-finds Stremio and injects.

using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Windows.Forms;

class FISLocalInjector
{
    static readonly int CDP_PORT_START = 9222;
    static readonly int CDP_PORT_COUNT = 10;

    [STAThread]
    static void Main(string[] args)
    {
        Application.EnableVisualStyles();

        // 1. Select bundle file
        string bundlePath = null;
        if (args.Length > 0 && File.Exists(args[0]))
        {
            bundlePath = args[0];
        }
        else
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Title = "Select FIS Bundle";
                ofd.Filter = "JavaScript|*.js|All files|*.*";
                ofd.InitialDirectory = Path.GetDirectoryName(Application.ExecutablePath);
                if (ofd.ShowDialog() != DialogResult.OK) return;
                bundlePath = ofd.FileName;
            }
        }

        string bundle = File.ReadAllText(bundlePath, Encoding.UTF8);
        Log("Bundle loaded: " + (bundle.Length / 1024) + " KB from " + Path.GetFileName(bundlePath));

        // 2. Find Stremio CDP
        Log("Scanning ports " + CDP_PORT_START + "-" + (CDP_PORT_START + CDP_PORT_COUNT - 1) + "...");
        string pageId;
        int foundPort;
        string wsUrl = ScanForStremio(out pageId, out foundPort);

        if (wsUrl == null)
        {
            MessageBox.Show(
                "Stremio not found on CDP ports " + CDP_PORT_START + "-" + (CDP_PORT_START + CDP_PORT_COUNT - 1) + ".\n\n" +
                "Make sure Stremio is running with CDP enabled\n" +
                "(launched via FIS Installer or with --remote-debugging-port).",
                "FIS Local Injector", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        Log("Found Stremio on port " + foundPort + " (page: " + pageId + ")");

        // 3. Destroy existing FIS instance first
        string destroyScript = "if(window.__FIS__){window.__FIS__.destroy();window.__FIS__=null;}'destroyed'";
        InjectScript(wsUrl, destroyScript);
        Log("Cleared previous FIS instance");

        // 4. Inject bundle
        bool ok = InjectScript(wsUrl, bundle);
        if (ok)
        {
            Log("Injection OK!");
            MessageBox.Show(
                "Bundle injected successfully!\n\n" +
                "File: " + Path.GetFileName(bundlePath) + "\n" +
                "Port: " + foundPort,
                "FIS Local Injector", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        else
        {
            MessageBox.Show("Injection failed. Check that Stremio is on the player page.",
                "FIS Local Injector", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    static string ScanForStremio(out string pageId, out int foundPort)
    {
        pageId = null;
        foundPort = 0;
        for (int port = CDP_PORT_START; port < CDP_PORT_START + CDP_PORT_COUNT; port++)
        {
            if (!IsPortListening(port)) continue;
            string wsUrl = FindStremioPage(port, out pageId);
            if (wsUrl != null)
            {
                foundPort = port;
                return wsUrl;
            }
        }
        return null;
    }

    static bool IsPortListening(int port)
    {
        try
        {
            using (TcpClient client = new TcpClient())
            {
                IAsyncResult ar = client.BeginConnect("127.0.0.1", port, null, null);
                bool connected = ar.AsyncWaitHandle.WaitOne(500);
                if (connected)
                {
                    try { client.EndConnect(ar); } catch { }
                    return true;
                }
                return false;
            }
        }
        catch { return false; }
    }

    static string FindStremioPage(int port, out string pageId)
    {
        pageId = null;
        try
        {
            using (WebClient wc = new WebClient())
            {
                wc.Proxy = null;
                string json = wc.DownloadString("http://127.0.0.1:" + port + "/json");

                int searchPos = 0;
                while (true)
                {
                    int typeIdx = json.IndexOf("\"type\"", searchPos);
                    if (typeIdx == -1) break;

                    int objStart = json.LastIndexOf('{', typeIdx);
                    int objEnd = FindMatchingBrace(json, objStart);
                    if (objEnd == -1) break;

                    string obj = json.Substring(objStart, objEnd - objStart + 1);

                    if (obj.Contains("\"type\":\"page\"") || obj.Contains("\"type\": \"page\""))
                    {
                        if (obj.Contains("web.stremio.com") || obj.Contains("app.strem.io"))
                        {
                            string wsUrl = ExtractJsonValue(obj, "webSocketDebuggerUrl");
                            pageId = ExtractJsonValue(obj, "id");
                            return wsUrl;
                        }
                    }
                    searchPos = objEnd + 1;
                }
            }
        }
        catch { }
        return null;
    }

    static bool InjectScript(string wsUrl, string script)
    {
        ClientWebSocket ws = null;
        try
        {
            ws = new ClientWebSocket();
            CancellationTokenSource cts = new CancellationTokenSource(30000);

            try { ws.ConnectAsync(new Uri(wsUrl), cts.Token).Wait(); }
            catch { return false; }

            if (ws.State != WebSocketState.Open) return false;

            string escaped = EscapeForJson(script);
            string msg = "{\"id\":1,\"method\":\"Runtime.evaluate\",\"params\":{\"expression\":\"" + escaped + "\"}}";

            byte[] msgBytes = Encoding.UTF8.GetBytes(msg);
            try { ws.SendAsync(new ArraySegment<byte>(msgBytes), WebSocketMessageType.Text, true, cts.Token).Wait(); }
            catch { return false; }

            byte[] buf = new byte[65536];
            StringBuilder resp = new StringBuilder();
            WebSocketReceiveResult result;
            int chunks = 0;
            do
            {
                try { result = ws.ReceiveAsync(new ArraySegment<byte>(buf), cts.Token).Result; }
                catch { return false; }
                resp.Append(Encoding.UTF8.GetString(buf, 0, result.Count));
                chunks++;
                if (chunks >= 100) return false;
            } while (!result.EndOfMessage);

            try { ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None).Wait(); }
            catch { }

            return !resp.ToString().Contains("\"error\"");
        }
        catch { return false; }
        finally
        {
            if (ws != null) { try { ws.Dispose(); } catch { } }
        }
    }

    static string EscapeForJson(string s)
    {
        StringBuilder sb = new StringBuilder(s.Length + s.Length / 4);
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            switch (c)
            {
                case '\\': sb.Append("\\\\"); break;
                case '"': sb.Append("\\\""); break;
                case '\r': break;
                case '\n': sb.Append("\\n"); break;
                case '\t': sb.Append("\\t"); break;
                case '\b': sb.Append("\\b"); break;
                case '\f': sb.Append("\\f"); break;
                default:
                    if (c < (char)0x20) { sb.Append("\\u"); sb.Append(((int)c).ToString("x4")); }
                    else sb.Append(c);
                    break;
            }
        }
        return sb.ToString();
    }

    static int FindMatchingBrace(string json, int pos)
    {
        int depth = 0; bool inStr = false; bool esc = false;
        for (int i = pos; i < json.Length; i++)
        {
            char c = json[i];
            if (esc) { esc = false; continue; }
            if (c == '\\') { esc = true; continue; }
            if (c == '"') { inStr = !inStr; continue; }
            if (inStr) continue;
            if (c == '{') depth++;
            if (c == '}') { depth--; if (depth == 0) return i; }
        }
        return -1;
    }

    static string ExtractJsonValue(string json, string key)
    {
        string search = "\"" + key + "\"";
        int idx = json.IndexOf(search);
        if (idx == -1) return null;
        int colon = json.IndexOf(':', idx + search.Length);
        if (colon == -1) return null;
        int start = json.IndexOf('"', colon + 1);
        if (start == -1) return null;
        int end = json.IndexOf('"', start + 1);
        if (end == -1) return null;
        return json.Substring(start + 1, end - start - 1);
    }

    static void Log(string msg)
    {
        Console.WriteLine("[FIS] " + msg);
    }
}
