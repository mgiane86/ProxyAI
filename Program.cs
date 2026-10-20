using Microsoft.VisualBasic.Logging;
using System;
using System.Drawing;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OneProviderProxy;

static class Program
{
    private const string AppGuid = "Global\\OneProviderProxy_SingleInstance_Mutex_B6A8E2";

    public const string ShowAppMessageName = "WM_SHOW_ONEPROVIDER_PROXY_SETTINGS";
    public static readonly int WmShowApp = RegisterWindowMessage(ShowAppMessageName);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern int RegisterWindowMessage(string lpString);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

    private const int HWND_BROADCAST = 0xFFFF;

    [STAThread]
    static void Main()
    {
        using var mutex = new Mutex(true, AppGuid, out bool isFirstInstance);

        if (!isFirstInstance)
        {
            if (WmShowApp != 0)
            {
                PostMessage((IntPtr)HWND_BROADCAST, WmShowApp, IntPtr.Zero, IntPtr.Zero);
            }
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new TrayApplicationContext());
    }
}

public class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _trayIcon;
    private readonly AppSettings _settings;
    private ProxyServer _server;
    private SettingsForm? _settingsForm;
    private readonly LogForm _logForm = new();
    private readonly MessageReceiverWindow _messageReceiver;

    public TrayApplicationContext()
    {
        _settings = AppSettings.Load();

        // Applica il tema salvato prima di creare il server e le finestre
        if (!string.IsNullOrEmpty(_settings.Language))
            Loc.CurrentLanguage = _settings.Language;

        Theme.Set(Theme.Parse(_settings.Theme));

        // Forza la creazione dell'handle del LogForm anche se nascosto: necessario per
        // BeginInvoke in AppendLog (altrimenti le prime richieste non vengono loggate
        // e il proxy sembra non rispondere finché la finestra non viene aperta)
        _ = _logForm.Handle;

        _server = CreateProxyServer();

        var contextMenu = new ContextMenuStrip();
        contextMenu.Items.Add(Loc.T("TraySettings"), null, (_, _) => ShowSettings());
        contextMenu.Items.Add(Loc.T("TrayLog"), null, (_, _) => ShowLog());
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(Loc.T("TrayExit"), null, (_, _) => Exit());

        Theme.ApplyMenu(contextMenu);

        _trayIcon = new NotifyIcon
        {
            Icon = SystemIcons.Shield,
            ContextMenuStrip = contextMenu,
            Visible = true
        };

        UpdateTrayToolTip();

        _trayIcon.DoubleClick += (_, _) => ShowSettings();
        _trayIcon.BalloonTipClicked += (_, _) => ShowSettings();

        // Aggiorna il tooltip dinamicamente ogni volta che il mouse passa sopra l'icona
        _trayIcon.MouseMove += (_, _) => UpdateTrayToolTip();

        // Riaggancia il tema al menu quando il tema cambia
        Theme.Changed += () =>
        {
            if (_trayIcon.ContextMenuStrip != null)
                Theme.ApplyMenu(_trayIcon.ContextMenuStrip);
        };

        _messageReceiver = new MessageReceiverWindow(ShowSettings);

        StartServer(isInitialStart: true);

        // Fetch iniziale del saldo in background all'avvio
        _ = RefreshBalanceInBackgroundAsync();
    }

    private void UpdateTrayToolTip()
    {
        var (inTok, outTok, balance, cost) = UsageTracker.GetSnapshot();

        string balStr = balance.HasValue ? $"${balance.Value:F2}" : "--";
        string inStr = UsageTracker.FormatCompactNumber(inTok);
        string outStr = UsageTracker.FormatCompactNumber(outTok);

        // Limite Windows NotifyIcon.Text = max 63 caratteri esatti
        string tip = $"OP:{_settings.Port} | {Loc.T("TipBalance")}: {balStr}\n{Loc.T("TipToday")}: {inStr} in, {outStr} out (${cost:F2})";
        if (tip.Length > 63)
        {
            tip = tip.Substring(0, 60) + "...";
        }

        if (_trayIcon.Text != tip)
        {
            _trayIcon.Text = tip;
        }
    }

    private async Task RefreshBalanceInBackgroundAsync()
    {
        if (string.IsNullOrWhiteSpace(_settings.ApiKey) || string.IsNullOrWhiteSpace(_settings.TargetEndpoint))
            return;

        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(6) };
            using var req = new HttpRequestMessage(HttpMethod.Get, $"{_settings.TargetEndpoint.TrimEnd('/')}/v1/dashboard/balance");
            req.Headers.TryAddWithoutValidation("Authorization", $"Bearer {_settings.ApiKey.Trim()}");

            using var res = await client.SendAsync(req);
            if (res.IsSuccessStatusCode)
            {
                var json = await res.Content.ReadAsStringAsync();
                var balanceData = JsonSerializer.Deserialize<OneProviderBalance>(json);
                if (balanceData != null)
                {
                    UsageTracker.UpdateBalance(balanceData.BalanceUsd);
                    UpdateTrayToolTip();
                }
            }
        }
        catch { }
    }

    private ProxyServer CreateProxyServer()
    {
        var server = new ProxyServer(_settings);
        server.OnLog += (msg, color) => _logForm.AppendLog(msg, color);
        return server;
    }

    private void StartServer(bool isInitialStart = false)
    {
        try
        {
            _server.Start();
            UpdateTrayToolTip();

            if (isInitialStart)
            {
                _trayIcon.ShowBalloonTip(
                    3000,
                    Loc.T("BalloonTitle"),
                    Loc.T("BalloonText", _settings.Port),
                    ToolTipIcon.Info
                );
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Impossibile avviare il proxy: {ex.Message}", "Errore", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    public void ShowSettings()
    {
        if (_settingsForm == null || _settingsForm.IsDisposed)
        {
            _settingsForm = new SettingsForm(_settings, () =>
            {
                _server.Dispose();
                _server = CreateProxyServer();
                StartServer(isInitialStart: false);
                _ = RefreshBalanceInBackgroundAsync();
            });
        }

        if (!_settingsForm.Visible)
            _settingsForm.Show();

        if (_settingsForm.WindowState == FormWindowState.Minimized)
            _settingsForm.WindowState = FormWindowState.Normal;

        _settingsForm.BringToFront();
        _settingsForm.Activate();
    }

    public void ShowLog()
    {
        if (!_logForm.Visible)
            _logForm.Show();

        if (_logForm.WindowState == FormWindowState.Minimized)
            _logForm.WindowState = FormWindowState.Normal;

        _logForm.BringToFront();
        _logForm.Activate();
    }

    private void Exit()
    {
        _server.Dispose();
        _trayIcon.Visible = false;
        _messageReceiver.DestroyHandle();
        Application.Exit();
    }

    private class MessageReceiverWindow : NativeWindow
    {
        private readonly Action _onShowRequested;

        public MessageReceiverWindow(Action onShowRequested)
        {
            _onShowRequested = onShowRequested;
            CreateHandle(new CreateParams());
        }

        protected override void WndProc(ref Message m)
        {
            if (Program.WmShowApp != 0 && m.Msg == Program.WmShowApp)
            {
                _onShowRequested();
                return;
            }
            base.WndProc(ref m);
        }
    }
}