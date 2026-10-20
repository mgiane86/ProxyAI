using Microsoft.VisualBasic.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OneProviderProxy;

public class SettingsForm : Form
{
    private readonly AppSettings _settings;
    private readonly Action _onSettingsSaved;

    // Copia di lavoro degli agenti: le modifiche diventano definitive solo con "Salva e Riavvia"
    private readonly List<AgentProfile> _agents;
    private int _currentIndex = -1;
    private bool _loadingAgent; // true mentre riempiamo i controlli da codice: evita che gli eventi riscrivano i dati

    // Connessione
    private readonly GroupBox _grpConn = new();
    private readonly Label _lblKey = new();
    private readonly TextBox _txtApiKey = new() { Width = 455, UseSystemPasswordChar = true };
    private readonly CheckBox _chkShowKey = new() { AutoSize = true };
    private readonly Label _lblEndpoint = new();
    private readonly TextBox _txtEndpoint = new() { Width = 455 };
    private readonly Label _lblPort = new();
    private readonly NumericUpDown _numPort = new() { Width = 90, Minimum = 1024, Maximum = 65535 };
    private readonly CheckBox _chkStartup = new() { AutoSize = true };
    private readonly Label _lblLang = new() { AutoSize = true };
    private readonly ComboBox _cmbLang = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 110 };

    // Saldo
    private readonly Button _btnCheckBalance = new() { Size = new Size(130, 32) };
    private readonly Label _lblBalanceInfo = new() { AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
    private readonly Label _lblExpiryInfo = new() { AutoSize = true, Font = new Font("Segoe UI", 8.5f, FontStyle.Regular), ForeColor = Color.DimGray };

    // Agenti & Modelli
    private readonly GroupBox _grpModels = new();
    private readonly Label _lblAgent = new() { AutoSize = true };
    private readonly ComboBox _cmbAgents = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 230 };
    private readonly Button _btnNewAgent = new() { Size = new Size(75, 26) };
    private readonly Button _btnDeleteAgent = new() { Size = new Size(95, 26) };
    private readonly Label _lblAgentName = new() { AutoSize = true };
    private readonly TextBox _txtAgentName = new() { Width = 455 };
    private readonly Label _lblSelectModel = new();
    private readonly TextBox _txtSelectedModel = new() { Width = 230, ReadOnly = true, BackColor = Color.White };
    private readonly Button _btnSelectModel = new() { Size = new Size(95, 26) };
    private readonly Button _btnCopyModelId = new() { Size = new Size(75, 26) };
    private readonly Button _btnCopyPreset = new() { Size = new Size(95, 26) };

    private readonly Label _lblPresetContext = new() { AutoSize = true, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), ForeColor = Color.FromArgb(0, 102, 204) };

    // Limiti token e funzionalità dell'agente
    private readonly Label _lblMaxInput = new() { AutoSize = false, Width = 118, Height = 16 };
    private readonly NumericUpDown _numMaxInput = new() { Width = 110, Minimum = 0, Maximum = 2_000_000, Increment = 1000 };
    private readonly Label _lblMaxOutput = new() { AutoSize = false, Width = 118, Height = 16 };
    private readonly NumericUpDown _numMaxOutput = new() { Width = 110, Minimum = 0, Maximum = 2_000_000, Increment = 1000 };
    private readonly CheckBox _chkTools = new() { AutoSize = true };
    private readonly CheckBox _chkVision = new() { AutoSize = true };
    private readonly CheckBox _chkCache = new() { AutoSize = true };

    // Markdown dell'agente
    private readonly Label _lblMarkdown = new() { AutoSize = true };
    private readonly Button _btnLoadMd = new() { Size = new Size(85, 26) };
    private readonly Button _btnClearMd = new() { Size = new Size(85, 26) };
    private readonly TextBox _txtMarkdown = new()
    {
        Multiline = true,
        AcceptsReturn = true,
        ScrollBars = ScrollBars.Vertical,
        Font = new Font("Consolas", 9f),
        Width = 455,
        Height = 90,
        MaxLength = int.MaxValue
    };

    // Guida & Azioni
    private readonly GroupBox _grpGuide = new();
    private readonly RichTextBox _txtGuide = new() { ReadOnly = true, Height = 110, Width = 465, BackColor = Color.FromArgb(245, 245, 245), BorderStyle = BorderStyle.FixedSingle };
    private readonly Button _btnSave = new() { Size = new Size(140, 32) };

    private AgentProfile? CurrentAgent =>
        _currentIndex >= 0 && _currentIndex < _agents.Count ? _agents[_currentIndex] : null;

    public SettingsForm(AppSettings settings, Action onSettingsSaved)
    {
        _settings = settings;
        _onSettingsSaved = onSettingsSaved;
        _agents = settings.Agents.Select(a => a.Clone()).ToList();

        if (!string.IsNullOrEmpty(_settings.Language))
        {
            Loc.CurrentLanguage = _settings.Language;
        }

        InitializeUI();
        LoadValues();
        RefreshAgentList(0);
        ApplyLocalization();
    }

    private void InitializeUI()
    {
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(520, 878);

        // Dimensioni gruppo connessione
        _grpConn.Location = new Point(15, 12);
        _grpConn.Size = new Size(490, 275);

        _lblKey.Location = new Point(15, 22);
        _lblKey.AutoSize = true;
        _txtApiKey.Location = new Point(15, 42);

        _chkShowKey.Location = new Point(15, 70);
        _chkShowKey.CheckedChanged += (_, _) => _txtApiKey.UseSystemPasswordChar = !_chkShowKey.Checked;

        _lblEndpoint.Location = new Point(15, 96);
        _lblEndpoint.AutoSize = true;
        _txtEndpoint.Location = new Point(15, 116);

        // Riga Porta e Lingua
        _lblPort.Location = new Point(15, 146);
        _lblPort.AutoSize = true;
        _numPort.Location = new Point(15, 166);
        _numPort.ValueChanged += (_, _) => UpdateGuideText();

        _lblLang.Location = new Point(340, 146);
        _lblLang.AutoSize = true;
        _cmbLang.Location = new Point(340, 166);
        _cmbLang.Items.Clear();
        _cmbLang.Items.Add("English");
        _cmbLang.Items.Add("Italiano");
        _cmbLang.SelectedIndex = (Loc.CurrentLanguage == "it") ? 1 : 0;
        _cmbLang.SelectedIndexChanged += (_, _) =>
        {
            Loc.CurrentLanguage = (_cmbLang.SelectedIndex == 1) ? "it" : "en";
            ApplyLocalization();
        };

        // Riga Avvio Automatico (ben visibile su riga dedicata)
        _chkStartup.Location = new Point(15, 200);
        _chkStartup.AutoSize = true;

        // Riga Test Saldo
        _btnCheckBalance.Location = new Point(15, 230);
        _btnCheckBalance.Click += async (_, _) => await CheckBalanceAsync();

        _lblBalanceInfo.Location = new Point(155, 230);
        _lblBalanceInfo.Text = "";

        _lblExpiryInfo.Location = new Point(155, 250);
        _lblExpiryInfo.Text = "";

        _grpConn.Controls.AddRange(new Control[] {
            _lblKey, _txtApiKey, _chkShowKey,
            _lblEndpoint, _txtEndpoint,
            _lblPort, _numPort, _chkStartup,
            _lblLang, _cmbLang,
            _btnCheckBalance, _lblBalanceInfo, _lblExpiryInfo
        });

        // Agenti
        _grpModels.Location = new Point(15, 290);
        _grpModels.Size = new Size(490, 384);

        // Riga 1: selezione agente + Nuovo / Elimina
        _lblAgent.Location = new Point(15, 27);
        _cmbAgents.Location = new Point(70, 21);
        _cmbAgents.SelectedIndexChanged += (_, _) =>
        {
            if (_loadingAgent) return;
            _currentIndex = _cmbAgents.SelectedIndex;
            LoadAgentToEditor();
        };
        _btnNewAgent.Location = new Point(305, 20);
        _btnNewAgent.Click += (_, _) => NewAgent();
        _btnDeleteAgent.Location = new Point(385, 20);
        _btnDeleteAgent.Click += (_, _) => DeleteCurrentAgent();

        // Riga 2: nome agente
        _lblAgentName.Location = new Point(15, 56);
        _txtAgentName.Location = new Point(15, 74);
        _txtAgentName.TextChanged += (_, _) => OnAgentNameChanged();

        // Riga 3: modello reale + Seleziona / Copia ID / Copia Preset
        _lblSelectModel.Location = new Point(15, 110);
        _lblSelectModel.AutoSize = true;
        _txtSelectedModel.Location = new Point(70, 105);

        _btnSelectModel.Location = new Point(305, 104);
        _btnSelectModel.Click += (_, _) => OpenModelSelector();

        _btnCopyModelId.Location = new Point(405, 104);
        _btnCopyModelId.Click += (_, _) =>
        {
            if (CurrentAgent is { } agent)
            {
                var name = agent.Name.Trim();
                Clipboard.SetText(name);
                MessageBox.Show(Loc.T("IdCopied", name), Loc.T("CopiedTitle"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        };

        _btnCopyPreset.Location = new Point(15, 134);
        _btnCopyPreset.Click += (_, _) => CopyFullPreset();

        _lblPresetContext.Location = new Point(115, 138);

        // Riga 4: limiti token + funzionalità
        _lblMaxInput.Location = new Point(15, 168);
        _numMaxInput.Location = new Point(15, 186);
        _numMaxInput.ValueChanged += (_, _) => OnLimitsChanged();

        _lblMaxOutput.Location = new Point(140, 168);
        _numMaxOutput.Location = new Point(140, 186);
        _numMaxOutput.ValueChanged += (_, _) => OnLimitsChanged();

        _chkTools.Location = new Point(270, 185);
        _chkTools.CheckedChanged += (_, _) => OnFeaturesChanged();
        _chkVision.Location = new Point(270, 207);
        _chkVision.CheckedChanged += (_, _) => OnFeaturesChanged();
        _chkCache.Location = new Point(270, 229);
        _chkCache.CheckedChanged += (_, _) => OnFeaturesChanged();

        // Riga 5: markdown
        _lblMarkdown.Location = new Point(15, 258);
        _lblMarkdown.AutoSize = true;
        _btnLoadMd.Location = new Point(295, 252);
        _btnLoadMd.Click += (_, _) => LoadMarkdownFromFile();
        _btnClearMd.Location = new Point(385, 252);
        _btnClearMd.Click += (_, _) => _txtMarkdown.Clear();

        _txtMarkdown.Location = new Point(15, 280);
        _txtMarkdown.TextChanged += (_, _) =>
        {
            if (_loadingAgent || CurrentAgent is not { } agent) return;
            // Nel TextBox le righe finiscono con \r\n; nei dati salviamo \n (formato standard per le API)
            agent.Markdown = _txtMarkdown.Text.ReplaceLineEndings("\n");
        };

        _grpModels.Controls.AddRange(new Control[] {
            _lblAgent, _cmbAgents, _btnNewAgent, _btnDeleteAgent,
            _lblAgentName, _txtAgentName,
            _lblSelectModel, _txtSelectedModel, _btnSelectModel, _btnCopyModelId, _btnCopyPreset,
            _lblPresetContext,
            _lblMaxInput, _numMaxInput, _lblMaxOutput, _numMaxOutput, _chkTools, _chkVision, _chkCache,
            _lblMarkdown, _btnLoadMd, _btnClearMd, _txtMarkdown
        });

        // Guida
        _grpGuide.Location = new Point(15, 686);
        _grpGuide.Size = new Size(490, 140);

        _txtGuide.Location = new Point(12, 20);
        _txtGuide.Height = 110;
        _grpGuide.Controls.Add(_txtGuide);

        // Salva
        _btnSave.Location = new Point(365, 834);
        _btnSave.Click += BtnSave_Click;

        Controls.AddRange(new Control[] { _grpConn, _grpModels, _grpGuide, _btnSave });
    }

    private void ApplyLocalization()
    {
        Text = Loc.T("Title");
        _grpConn.Text = Loc.T("GrpConn");
        _lblKey.Text = Loc.T("ApiKey");
        _chkShowKey.Text = Loc.T("ShowKey");
        _lblEndpoint.Text = Loc.T("TargetBaseUrl");
        _lblPort.Text = Loc.T("LocalPort");
        _chkStartup.Text = Loc.T("RunStartup");
        _lblLang.Text = Loc.T("Language");
        _btnCheckBalance.Text = Loc.T("CheckBalance");

        _grpModels.Text = Loc.T("GrpModels");
        _lblAgent.Text = Loc.T("Agent");
        _btnNewAgent.Text = Loc.T("NewAgent");
        _btnDeleteAgent.Text = Loc.T("DeleteAgent");
        _lblAgentName.Text = Loc.T("AgentName");
        _lblSelectModel.Text = Loc.T("Model");
        _btnCopyModelId.Text = Loc.T("CopyId");
        _btnCopyPreset.Text = Loc.T("CopyPreset");
        _lblMarkdown.Text = Loc.T("Markdown");
        _btnLoadMd.Text = Loc.T("LoadFile");
        _btnClearMd.Text = Loc.T("ClearMd");

        _lblMaxInput.Text = Loc.T("MaxInput");
        _lblMaxOutput.Text = Loc.T("MaxOutput");
        _chkTools.Text = Loc.T("SupportsTools");
        _chkVision.Text = Loc.T("SupportsVision");
        _chkCache.Text = Loc.T("UsePromptCaching");
        _btnSelectModel.Text = Loc.T("SelectModel");

        _grpGuide.Text = Loc.T("GrpGuide");
        _btnSave.Text = Loc.T("SaveRestart");

        UpdateSelectedPresetDisplay();
        UpdateGuideText();
    }

    // ---------- Gestione agenti ----------

    private void OpenModelSelector()
    {
        // Carica i modelli in modo asincrono se non sono già in cache
        var models = ModelCatalog.Cached;
        if (models.Count == 0)
        {
            // Tentativo di caricamento sincrono dalla cache su disco
            models = ModelCatalog.Cached;
        }

        using var selector = new ModelSelectorForm(models, CurrentAgent?.ModelId ?? "")
        {
            Tag = _settings // Passa le impostazioni per il refresh
        };

        if (selector.ShowDialog(this) == DialogResult.OK)
        {
            if (CurrentAgent is { } agent)
            {
                agent.ModelId = selector.SelectedModelId;
                _txtSelectedModel.Text = selector.SelectedModelId;
                UpdateSelectedPresetDisplay();
            }
        }
    }

    /// <summary>Ricostruisce l'elenco degli agenti e seleziona quello all'indice indicato.</summary>
    private void RefreshAgentList(int selectIndex)
    {
        _loadingAgent = true;
        _cmbAgents.Items.Clear();
        foreach (var a in _agents) _cmbAgents.Items.Add(a);
        _currentIndex = _agents.Count == 0 ? -1 : Math.Clamp(selectIndex, 0, _agents.Count - 1);
        _cmbAgents.SelectedIndex = _currentIndex;
        _loadingAgent = false;

        LoadAgentToEditor();
    }

    /// <summary>Copia i dati dell'agente selezionato nei controlli (nome, modello, markdown).</summary>
    private void LoadAgentToEditor()
    {
        _loadingAgent = true;
        try
        {
            var agent = CurrentAgent;
            SetEditorEnabled(agent != null);

            if (agent == null)
            {
                _txtAgentName.Text = "";
                _txtSelectedModel.Text = "";
                _txtMarkdown.Text = "";
                _numMaxInput.Value = 0;
                _numMaxOutput.Value = 0;
                _chkTools.Checked = true;
                _chkVision.Checked = true;
                _chkCache.Checked = false;
            }
            else
            {
                _txtAgentName.Text = agent.Name;
                _txtSelectedModel.Text = string.IsNullOrWhiteSpace(agent.ModelId) ? Loc.T("NoModelSelected") : agent.ModelId;

                _numMaxInput.Value = Math.Clamp(agent.MaxInputTokens, 0, (int)_numMaxInput.Maximum);
                _numMaxOutput.Value = Math.Clamp(agent.MaxOutputTokens, 0, (int)_numMaxOutput.Maximum);
                _chkTools.Checked = agent.SupportsTools;
                _chkVision.Checked = agent.SupportsVision;
                _chkCache.Checked = agent.UsePromptCaching;

                // Il TextBox vuole \r\n come fine riga, altrimenti mostra tutto su una riga
                _txtMarkdown.Text = agent.Markdown.ReplaceLineEndings();
            }
        }
        finally
        {
            _loadingAgent = false;
        }
        UpdateSelectedPresetDisplay();
    }

    private void SetEditorEnabled(bool enabled)
    {
        _txtAgentName.Enabled = enabled;
        _txtSelectedModel.Enabled = enabled;
        _btnSelectModel.Enabled = enabled;
        _btnCopyModelId.Enabled = enabled;
        _btnCopyPreset.Enabled = enabled;
        _btnDeleteAgent.Enabled = enabled;
        _btnLoadMd.Enabled = enabled;
        _btnClearMd.Enabled = enabled;
        _txtMarkdown.Enabled = enabled;
        _numMaxInput.Enabled = enabled;
        _numMaxOutput.Enabled = enabled;
        _chkTools.Enabled = enabled;
        _chkVision.Enabled = enabled;
        _chkCache.Enabled = enabled;
    }

    private void OnAgentNameChanged()
    {
        if (_loadingAgent || CurrentAgent is not { } agent) return;
        agent.Name = _txtAgentName.Text;

        // Aggiorna il testo mostrato nell'elenco a tendina
        _loadingAgent = true;
        _cmbAgents.Items[_currentIndex] = agent;
        _cmbAgents.SelectedIndex = _currentIndex;
        _loadingAgent = false;
    }

    private void OnLimitsChanged()
    {
        if (_loadingAgent || CurrentAgent is not { } agent) return;
        agent.MaxInputTokens = (int)_numMaxInput.Value;
        agent.MaxOutputTokens = (int)_numMaxOutput.Value;
    }

    private void OnFeaturesChanged()
    {
        if (_loadingAgent || CurrentAgent is not { } agent) return;
        agent.SupportsTools = _chkTools.Checked;
        agent.SupportsVision = _chkVision.Checked;
        agent.UsePromptCaching = _chkCache.Checked;
    }

    private void NewAgent()
    {
        int n = _agents.Count + 1;
        string name;
        do { name = $"agent-{n++}"; }
        while (_agents.Any(a => a.Name.Equals(name, StringComparison.OrdinalIgnoreCase)));

        _agents.Add(new AgentProfile
        {
            Name = name,
            ModelId = ""
        });
        RefreshAgentList(_agents.Count - 1);

        _txtAgentName.Focus();
        _txtAgentName.SelectAll();
    }

    private void DeleteCurrentAgent()
    {
        if (CurrentAgent is not { } agent) return;

        var answer = MessageBox.Show(Loc.T("ConfirmDelete", agent.Name), Loc.T("Title"),
            MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (answer != DialogResult.Yes) return;

        _agents.RemoveAt(_currentIndex);
        RefreshAgentList(_currentIndex);
    }

    private void LoadMarkdownFromFile()
    {
        using var dlg = new OpenFileDialog
        {
            Filter = "Markdown (*.md;*.markdown;*.txt)|*.md;*.markdown;*.txt|*.*|*.*"
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            _txtMarkdown.Text = File.ReadAllText(dlg.FileName).ReplaceLineEndings();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    // ---------- Preset / guida ----------

    private void UpdateSelectedPresetDisplay()
    {
        var agent = CurrentAgent;
        if (agent != null && agent.MaxInputTokens > 0)
        {
            _lblPresetContext.Text = Loc.T("ContextLimit", agent.MaxInputTokens);
        }
        else
        {
            _lblPresetContext.Text = "";
        }
    }

    private void CopyFullPreset()
    {
        if (CurrentAgent is { } agent)
        {
            var text =
                $"Display Name: {agent.Name.Trim()}\n" +
                $"Model ID: {agent.Name.Trim()}\n" +
                $"Endpoint: http://localhost:{(int)_numPort.Value}/v1\n" +
                $"Context Limit: {agent.MaxInputTokens}\n" +
                $"Output Limit: {agent.MaxOutputTokens}\n" +
                $"Tool calling: {(agent.SupportsTools ? Loc.T("Yes") : Loc.T("No"))}\n" +
                $"Vision support: {(agent.SupportsVision ? Loc.T("Yes") : Loc.T("No"))}";

            Clipboard.SetText(text);
            MessageBox.Show(Loc.T("PresetCopied", text), Loc.T("CopiedTitle"), MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private void UpdateGuideText()
    {
        var p = (int)_numPort.Value;
        _txtGuide.Text =
            $"{Loc.T("GuideStep1")}\n" +
            $"{Loc.T("GuideStep2")}\n" +
            $"{Loc.T("GuideStep3", p)}\n" +
            $"{Loc.T("GuideStep4")}\n" +
            $"{Loc.T("GuideStep5")}\n" +
            $"{Loc.T("GuideStep6")}\n" +
            $"{Loc.T("GuideCache")}";
    }

    private async Task CheckBalanceAsync()
    {
        var apiKey = _txtApiKey.Text.Trim();
        var baseUrl = _txtEndpoint.Text.Trim().TrimEnd('/');

        if (string.IsNullOrEmpty(apiKey))
        {
            MessageBox.Show("Please enter an API key first.", "OneProvider Proxy", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _btnCheckBalance.Enabled = false;
        _lblBalanceInfo.ForeColor = Color.DimGray;
        _lblBalanceInfo.Text = Loc.T("BalanceChecking");
        _lblExpiryInfo.Text = "";

        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            using var req = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/v1/dashboard/balance");
            req.Headers.TryAddWithoutValidation("Authorization", $"Bearer {apiKey}");

            var sw = Stopwatch.StartNew();
            using var res = await client.SendAsync(req);
            sw.Stop();

            if (res.IsSuccessStatusCode)
            {
                var json = await res.Content.ReadAsStringAsync();
                var balanceData = JsonSerializer.Deserialize<OneProviderBalance>(json);

                if (balanceData != null)
                {
                    UsageTracker.UpdateBalance(balanceData.BalanceUsd);

                    _lblBalanceInfo.ForeColor = balanceData.BalanceUsd > 0 ? Color.ForestGreen : Color.Crimson;
                    var status = balanceData.IsActive ? Loc.T("StatusActive") : Loc.T("StatusDisabled");
                    _lblBalanceInfo.Text = Loc.T("BalanceAvailable", balanceData.BalanceUsd, status);

                    var expiryText = balanceData.ExpiresAt.HasValue
                        ? Loc.T("KeyExpiry", balanceData.ExpiresAt.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm"))
                        : Loc.T("NoExpiry");

                    _lblExpiryInfo.Text = $"{expiryText} | Ping: {sw.ElapsedMilliseconds} ms";
                }
            }
            else if ((int)res.StatusCode is 401 or 403)
            {
                _lblBalanceInfo.ForeColor = Color.Crimson;
                _lblBalanceInfo.Text = Loc.T("InvalidKey");
            }
            else if ((int)res.StatusCode == 429)
            {
                _lblBalanceInfo.ForeColor = Color.DarkOrange;
                _lblBalanceInfo.Text = Loc.T("RateLimit");
            }
            else
            {
                _lblBalanceInfo.ForeColor = Color.Crimson;
                _lblBalanceInfo.Text = $"HTTP {(int)res.StatusCode} {res.ReasonPhrase}";
            }
        }
        catch (Exception ex)
        {
            _lblBalanceInfo.ForeColor = Color.Crimson;
            _lblBalanceInfo.Text = Loc.T("ConnError");
            MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _btnCheckBalance.Enabled = true;
        }
    }

    private void LoadValues()
    {
        _txtApiKey.Text = _settings.ApiKey;
        _txtEndpoint.Text = _settings.TargetEndpoint;
        _numPort.Value = _settings.Port;
        _chkStartup.Checked = StartupHelper.IsRunAtStartupEnabled();
    }

    private void BtnSave_Click(object? sender, EventArgs e)
    {
        // Pulizia e controllo dei nomi: devono essere non vuoti e unici (senza distinguere maiuscole/minuscole)
        foreach (var a in _agents) a.Name = a.Name.Trim();

        bool invalid = _agents.Any(a => string.IsNullOrWhiteSpace(a.Name)) ||
                       _agents.GroupBy(a => a.Name, StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1);
        if (invalid)
        {
            MessageBox.Show(Loc.T("AgentNameInvalid"), "OneProvider Proxy", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _settings.ApiKey = _txtApiKey.Text.Trim();
        _settings.TargetEndpoint = _txtEndpoint.Text.Trim().TrimEnd('/');
        _settings.Port = (int)_numPort.Value;
        _settings.Language = Loc.CurrentLanguage;
        _settings.Agents = _agents.Select(a => a.Clone()).ToList();
        _settings.Save();

        StartupHelper.SetRunAtStartup(_chkStartup.Checked);
        _onSettingsSaved();

        MessageBox.Show(Loc.T("SavedMsg"), "OneProvider Proxy", MessageBoxButtons.OK, MessageBoxIcon.Information);
        Hide();
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

    private record ModelPreset(string DisplayName, string Id)
    {
        public override string ToString() => DisplayName;
    }
}
