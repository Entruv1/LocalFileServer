using System.Diagnostics;
using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace LocalFileServer;

public partial class MainWindow : Window
{
    private readonly FileServer _server = new();
    private string _rootPath = Path.Combine(AppContext.BaseDirectory, "Shared");

    public string RootPath
    {
        get => _rootPath;
        set { _rootPath = value; _server.RootPath = value; }
    }

    public MainWindow()
    {
        InitializeComponent();
        _server.LogEvent += OnLog;
        LoadIPs();
        LoadSettings();
        TxtUrl.Text = "";
        Loaded += (_, _) => DoStart();   // 双击即用：窗口加载完成自动启动
    }

    private void TxtRoot_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(TxtRoot.Text))
        {
            RootPath = TxtRoot.Text.Trim();
        }
    }

    private void LoadIPs()
    {
        CmbIP.Items.Clear();
        foreach (var ip in FileServer.GetLocalIPv4s())
            CmbIP.Items.Add(ip);
        if (CmbIP.Items.Count > 0)
            CmbIP.SelectedIndex = 0;
        CmbIP.Items.Add("127.0.0.1 (仅本机)");
    }

    private void LoadSettings()
    {
        // 默认共享目录 = exe 所在目录（MT5700-Fix 交付包，含脚本与 socat ipk）
        // 优先取 exe 所在目录，这样整个交付文件夹拷贝到任何位置都能直接共享
        var exeDir = Path.GetDirectoryName(AppContext.BaseDirectory);
        if (!string.IsNullOrEmpty(exeDir) && Directory.Exists(exeDir))
        {
            RootPath = exeDir;
        }
        else if (Directory.Exists(_rootPath))
        {
            RootPath = _rootPath;
        }
        else
        {
            Directory.CreateDirectory(_rootPath);
        }
        TxtRoot.Text = RootPath;
    }

    private void OnLog(object? sender, LogEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            TxtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {e.Message}\r\n");
            TxtLog.ScrollToEnd();
        });
    }

    private void UpdateUrl()
    {
        var ip = CmbIP.SelectedItem?.ToString() ?? "127.0.0.1";
        if (ip.Contains("仅本机")) ip = "127.0.0.1";
        var port = ParsePort();
        TxtUrl.Text = $"http://{ip}:{port}";
    }

    private int ParsePort()
    {
        if (int.TryParse(TxtPort.Text, out var p) && p > 0 && p <= 65535) return p;
        return 8848;
    }

    private void DoStart()
    {
        _server.RootPath = RootPath;
        _server.Port = ParsePort();
        var token = TxtToken.Password.Trim();
        _server.AuthEnabled = !string.IsNullOrEmpty(token);
        _server.AuthToken = token;
        _server.Start();
        UpdateUrl();
        BtnStart.IsEnabled = !_server.IsRunning;
        BtnStop.IsEnabled = _server.IsRunning;
        StatusText.Text = _server.IsRunning ? "运行中" : "未运行";
        StatusDot.Fill = _server.IsRunning
            ? (System.Windows.Media.Brush)new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x0F, 0x6E, 0x56))
            : System.Windows.Media.Brushes.Gray;
        StatusBadge.Background = _server.IsRunning
            ? (System.Windows.Media.Brush)new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xE8, 0xF4, 0xF0))
            : System.Windows.Media.Brushes.Gray;
    }

    private void Start_Click(object sender, RoutedEventArgs e) => DoStart();

    private void Stop_Click(object sender, RoutedEventArgs e)
    {
        _server.Stop();
        BtnStart.IsEnabled = true;
        BtnStop.IsEnabled = false;
        StatusText.Text = "已停止";
        StatusDot.Fill = System.Windows.Media.Brushes.Gray;
    }

    /// <summary>应用端口：运行中则以新端口重启，未运行则记录端口供下次启动</summary>
    private void ApplyPort_Click(object sender, RoutedEventArgs e)
    {
        var port = ParsePort();
        _server.Port = port;
        if (_server.IsRunning)
        {
            _server.Stop();
            _server.Start();   // 以新端口重启
        }
        UpdateUrl();
        BtnStart.IsEnabled = !_server.IsRunning;
        BtnStop.IsEnabled = _server.IsRunning;
        StatusText.Text = _server.IsRunning ? "运行中" : "未运行";
        Log($"[端口] 已应用端口 {port}，当前服务状态: {( _server.IsRunning ? "运行中" : "未运行" )}");
    }

    private void ChooseDir_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog
        {
            Title = "选择共享目录",
            InitialDirectory = Directory.Exists(RootPath) ? RootPath : AppContext.BaseDirectory
        };
        if (dlg.ShowDialog() == true)
        {
            RootPath = dlg.FolderName;
            TxtRoot.Text = RootPath;
            Log("[设置] 共享目录: " + RootPath);
        }
    }

    private void OpenBrowser_Click(object sender, RoutedEventArgs e)
    {
        UpdateUrl();
        if (!string.IsNullOrEmpty(TxtUrl.Text))
            Process.Start(new ProcessStartInfo(TxtUrl.Text) { UseShellExecute = true });
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        if (!Directory.Exists(RootPath)) Directory.CreateDirectory(RootPath);
        Process.Start("explorer.exe", RootPath);
    }

    private void Log(string msg) => OnLog(null, new LogEventArgs(msg));

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        _server.Stop();
        base.OnClosing(e);
    }
}
