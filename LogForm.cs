using Microsoft.VisualBasic.Logging;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace OneProviderProxy;

public class LogForm : Form
{
    private readonly RichTextBox _txtLog = new()
    {
        Dock = DockStyle.Fill,
        ReadOnly = true,
        BackColor = Color.FromArgb(28, 28, 28),
        ForeColor = Color.Gainsboro,
        Font = new Font("Consolas", 9.5f),
        BorderStyle = BorderStyle.None
    };

    public LogForm()
    {
        Text = Loc.T("LogTitle");
        Text = "OneProvider Proxy";
        Size = new Size(720, 420);
        StartPosition = FormStartPosition.CenterScreen;

        var panelTop = new Panel { Dock = DockStyle.Top, Height = 40, BackColor = Color.FromArgb(40, 40, 40) };
        var btnClear = new Button
        {
            Text = Loc.T("LogClear"),
            Location = new Point(10, 7),
            Size = new Size(100, 26),
            BackColor = Color.FromArgb(60, 60, 60),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        btnClear.FlatAppearance.BorderSize = 0;
        btnClear.Click += (_, _) => _txtLog.Clear();

        panelTop.Controls.Add(btnClear);
        Controls.Add(_txtLog);
        Controls.Add(panelTop);
    }

    public void AppendLog(string message, Color color)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => AppendLog(message, color)));
            return;
        }

        _txtLog.SelectionStart = _txtLog.TextLength;
        _txtLog.SelectionLength = 0;
        _txtLog.SelectionColor = color;
        _txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
        _txtLog.ScrollToCaret();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
        }
        base.OnFormClosing(e);
    }
}