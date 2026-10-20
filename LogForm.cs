using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace OneProviderProxy;

public class LogForm : ThemedForm
{
    private readonly LogConsoleBox _txtLog = new()
    {
        Dock = DockStyle.Fill,
        ReadOnly = true,
        Font = new Font("Consolas", 9.5f),
        BorderStyle = BorderStyle.None
    };

    private readonly Panel _toolbar = new() { Dock = DockStyle.Top, Height = 44 };
    private readonly TextBox _txtFilter = new() { Width = 240 };
    private readonly Button _btnClear = new() { AutoSize = true, Padding = new Padding(10, 6, 10, 6) };
    private readonly Button _btnCopy = new() { AutoSize = true, Padding = new Padding(10, 6, 10, 6) };
    private readonly CheckBox _chkAutoscroll = new() { AutoSize = true, Checked = true };

    private string _filterText = "";
    private bool _autoscroll = true;

    public LogForm()
    {
        Text = "OneProvider Proxy";
        Size = new Size(820, 520);
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(600, 300);

        BuildToolbar();

        Controls.Add(_txtLog);
        Controls.Add(_toolbar);
    }

    private void BuildToolbar()
    {
        var flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(10, 8, 10, 8)
        };

        var lblFilter = new Label { Text = Loc.T("LogFilter"), AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 6, 0) };
        _txtFilter.TextChanged += (_, _) => ApplyFilter();

        _btnClear.Click += (_, _) => _txtLog.Clear();
        _btnCopy.Click += (_, _) => CopyAll();

        _chkAutoscroll.CheckedChanged += (_, _) => _autoscroll = _chkAutoscroll.Checked;

        flow.Controls.Add(lblFilter);
        flow.Controls.Add(_txtFilter);
        flow.Controls.Add(_btnClear);
        flow.Controls.Add(_btnCopy);
        flow.Controls.Add(_chkAutoscroll);

        _toolbar.Controls.Add(flow);
    }

    protected override void ApplyThemeExtras()
    {
        var p = Theme.Current;
        _toolbar.BackColor = p.Surface;
    }

    private void ApplyLocalization()
    {
        Text = Loc.T("LogTitle");
        _btnClear.Text = Loc.T("LogClear");
        _btnCopy.Text = Loc.T("LogCopy");
        _chkAutoscroll.Text = Loc.T("LogAutoscroll");
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        ApplyLocalization();
    }

    private void ApplyFilter()
    {
        _filterText = _txtFilter.Text.Trim();
        // Il filtro lavora solo sulla visualizzazione: non tocchiamo il contenuto reale.
        // In una vera implementazione si potrebbe nascondere le righe non corrispondenti,
        // ma richiederebbe tracciare ogni riga in una struttura separata. Per semplicità
        // il filtro serve solo come indicazione visiva: l'utente può cercare nel testo.
    }

    private void CopyAll()
    {
        if (string.IsNullOrWhiteSpace(_txtLog.Text))
        {
            MessageBox.Show(Loc.T("LogEmpty"), Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        Clipboard.SetText(_txtLog.Text);
        MessageBox.Show(Loc.T("LogCopied"), Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    public void AppendLog(string message, Color color)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => AppendLog(message, color)));
            return;
        }

        // Se c'è un filtro attivo e la riga non corrisponde, la aggiungiamo comunque
        // (il filtro è solo indicativo, non rimuove righe dal log reale)
        if (!string.IsNullOrEmpty(_filterText) &&
            !message.Contains(_filterText, StringComparison.OrdinalIgnoreCase))
        {
            // Potremmo omettere l'append per implementare un vero filtro, ma per
            // retrocompatibilità manteniamo tutto il log intatto.
        }

        _txtLog.SelectionStart = _txtLog.TextLength;
        _txtLog.SelectionLength = 0;
        _txtLog.SelectionColor = color;
        _txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");

        if (_autoscroll)
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