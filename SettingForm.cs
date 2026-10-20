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

public class SettingsForm : ThemedForm
{
    private readonly AppSettings _settings;
    private readonly Action _onSettingsSaved;

    // Copia di lavoro degli agenti: le modifiche diventano definitive solo con "Salva e Riavvia"
    private readonly List<AgentProfile> _agents;
    private int _currentIndex = -1;
    private bool _loadingAgent; // true mentre riempiamo i controlli da codice: evita che gli eventi riscrivano i dati

    // Navigazione laterale
    private readonly Panel _navPanel = new() { Dock = DockStyle.Left, Width = 180 };
    private readonly NavButton _navConnection = new();
    private readonly NavButton _navAgents = new();
    private readonly NavButton _navGuide = new();
    private readonly Panel _contentPanel = new() { Dock = DockStyle.Fill };

    // Connessione
    private readonly CardPanel _cardConn = new() { Dock = DockStyle.Fill };
    private readonly TextBox _txtApiKey = new() { Width = 400, UseSystemPasswordChar = true };
    private readonly CheckBox _chkShowKey = new() { AutoSize = true };
    private readonly TextBox _txtEndpoint = new() { Width = 400 };
    private readonly NumericUpDown _numPort = new() { Width = 110, Minimum = 1024, Maximum = 65535 };
    private readonly CheckBox _chkStartup = new() { AutoSize = true };
    private readonly ComboBox _cmbLang = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 130 };
    private readonly ComboBox _cmbTheme = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 130 };
    private readonly Button _btnCheckBalance = new() { AutoSize = true, Padding = new Padding(14, 8, 14, 8), Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) };
    private readonly Label _lblBalanceInfo = new() { AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
    private readonly Label _lblExpiryInfo = new() { AutoSize = true, ForeColor = Color.DimGray };

    // Label localizzabili per la card connessione
    private readonly Label _lblApiKeyLabel = new() { AutoSize = true, Anchor = AnchorStyles.Left };
    private readonly Label _lblEndpointLabel = new() { AutoSize = true, Anchor = AnchorStyles.Left };
    private readonly Label _lblPortLabel = new() { AutoSize = true, Anchor = AnchorStyles.Left };
    private readonly Label _lblLanguageLabel = new() { AutoSize = true, Anchor = AnchorStyles.Left };
    private readonly Label _lblThemeLabel = new() { AutoSize = true, Anchor = AnchorStyles.Left };
    private readonly Label _lblBalanceLabel = new() { AutoSize = true, Anchor = AnchorStyles.Left };

    // Agenti
    private readonly CardPanel _cardAgents = new() { Dock = DockStyle.Fill };
    private readonly ListBox _lstAgents = new() { Dock = DockStyle.Fill, IntegralHeight = false };
    private readonly Button _btnNewAgent = new() { Dock = DockStyle.Top, Height = 36, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) };
    private readonly Button _btnDeleteAgent = new() { Dock = DockStyle.Top, Height = 36, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) };
    private readonly TextBox _txtAgentName = new() { Dock = DockStyle.Fill };
    private readonly TextBox _txtSelectedModel = new() { Dock = DockStyle.Fill, ReadOnly = true };
    private readonly Button _btnSelectModel = new() { AutoSize = true, Padding = new Padding(12, 8, 12, 8), Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) };
    private readonly Button _btnCopyModelId = new() { AutoSize = true, Padding = new Padding(12, 8, 12, 8), Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) };
    private readonly Button _btnCopyPreset = new() { AutoSize = true, Padding = new Padding(12, 8, 12, 8), Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) };
    private readonly Label _lblPresetContext = new() { AutoSize = true, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
    private readonly NumericUpDown _numMaxInput = new() { Width = 120, Minimum = 0, Maximum = 2_000_000, Increment = 1000 };
    private readonly NumericUpDown _numMaxOutput = new() { Width = 120, Minimum = 0, Maximum = 2_000_000, Increment = 1000 };
    private readonly CheckBox _chkTools = new() { AutoSize = true };
    private readonly CheckBox _chkVision = new() { AutoSize = true };
    private readonly CheckBox _chkCache = new() { AutoSize = true };
    private readonly TextBox _txtMarkdown = new()
    {
        Multiline = true,
        AcceptsReturn = true,
        ScrollBars = ScrollBars.Vertical,
        Font = new Font("Consolas", 9f),
        Dock = DockStyle.Fill,
        MaxLength = int.MaxValue
    };
    private readonly Button _btnLoadMd = new() { AutoSize = true, Padding = new Padding(14, 9, 14, 9), Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) };
    private readonly Button _btnClearMd = new() { AutoSize = true, Padding = new Padding(14, 9, 14, 9), Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) };

    // Label localizzabili per la card agenti
    private readonly Label _lblAgentNameLabel = new() { AutoSize = true, Anchor = AnchorStyles.Left };
    private readonly Label _lblRealModelLabel = new() { AutoSize = true, Anchor = AnchorStyles.Left };
    private readonly Label _lblPresetLabel = new() { AutoSize = true, Anchor = AnchorStyles.Left };
    private readonly Label _lblTokenLimitsLabel = new() { AutoSize = true, Anchor = AnchorStyles.Left };
    private readonly Label _lblMaxInputLabel = new() { AutoSize = true, Anchor = AnchorStyles.Left };
    private readonly Label _lblMaxOutputLabel = new() { AutoSize = true, Anchor = AnchorStyles.Left };
    private readonly Label _lblFeaturesLabel = new() { AutoSize = true, Anchor = AnchorStyles.Left };
    private readonly Label _lblMarkdownLabel = new() { AutoSize = true, Anchor = AnchorStyles.Left };

    // Guida
    private readonly CardPanel _cardGuide = new() { Dock = DockStyle.Fill };
    private readonly RichTextBox _txtGuide = new() { ReadOnly = true, Dock = DockStyle.Fill, BorderStyle = BorderStyle.None };

    // Barra inferiore
    private readonly Panel _bottomBar = new() { Dock = DockStyle.Bottom, Height = 70 };
    private readonly AccentButton _btnSave = new() { AutoSize = true, Padding = new Padding(28, 12, 28, 12), Font = new Font("Segoe UI", 10f, FontStyle.Bold) };

    private AgentProfile? CurrentAgent =>
        _currentIndex >= 0 && _currentIndex < _agents.Count ? _agents[_currentIndex] : null;

    public SettingsForm(AppSettings settings, Action onSettingsSaved)
    {
        _settings = settings;
        _onSettingsSaved = onSettingsSaved;
        _agents = settings.Agents.Select(a => a.Clone()).ToList();

        if (!string.IsNullOrEmpty(_settings.Language))
            Loc.CurrentLanguage = _settings.Language;

        Theme.Set(Theme.Parse(_settings.Theme));

        InitializeUI();
        LoadValues();
        RefreshAgentList(0);
        ApplyLocalization();
        ShowPanel(_cardConn);
    }

    private void InitializeUI()
    {
        Text = "OneProvider Proxy";
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        MinimizeBox = true;
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(920, 600);
        ClientSize = new Size(1000, 650);

        // Navigazione
        _navPanel.Padding = new Padding(0, 12, 0, 12);
        _navConnection.Text = "Connection";
        _navConnection.Dock = DockStyle.Top;
        _navConnection.Click += (_, _) => ShowPanel(_cardConn);
        _navAgents.Text = "Agents";
        _navAgents.Dock = DockStyle.Top;
        _navAgents.Click += (_, _) => ShowPanel(_cardAgents);
        _navGuide.Text = "Setup Guide";
        _navGuide.Dock = DockStyle.Top;
        _navGuide.Click += (_, _) => ShowPanel(_cardGuide);

        _navPanel.Controls.Add(_navGuide);
        _navPanel.Controls.Add(_navAgents);
        _navPanel.Controls.Add(_navConnection);

        // Card Connessione
        BuildConnectionCard();

        // Card Agenti
        BuildAgentsCard();

        // Card Guida
        BuildGuideCard();

        // Barra inferiore
        _btnSave.Text = "Save & Restart";
        _btnSave.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
        _btnSave.Click += BtnSave_Click;
        _bottomBar.Controls.Add(_btnSave);

        Controls.Add(_contentPanel);
        Controls.Add(_navPanel);
        Controls.Add(_bottomBar);
    }

    private void BuildConnectionCard()
    {
        _cardConn.Header = "Connection Parameters & Balance";

        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 9,
            AutoSize = true,
            Padding = new Padding(4)
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140f));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

        for (int i = 0; i < 9; i++)
            grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _chkShowKey.CheckedChanged += (_, _) => _txtApiKey.UseSystemPasswordChar = !_chkShowKey.Checked;

        _cmbLang.Items.Clear();
        _cmbLang.Items.Add("English");
        _cmbLang.Items.Add("Italiano");
        _cmbLang.SelectedIndex = (Loc.CurrentLanguage == "it") ? 1 : 0;
        _cmbLang.SelectedIndexChanged += (_, _) =>
        {
            Loc.CurrentLanguage = (_cmbLang.SelectedIndex == 1) ? "it" : "en";
            ApplyLocalization();
        };

        _cmbTheme.Items.Clear();
        _cmbTheme.Items.Add("Light");
        _cmbTheme.Items.Add("Dark");
        _cmbTheme.SelectedIndex = (Theme.Mode == ThemeMode.Dark) ? 1 : 0;
        _cmbTheme.SelectedIndexChanged += (_, _) =>
        {
            Theme.Set((_cmbTheme.SelectedIndex == 1) ? ThemeMode.Dark : ThemeMode.Light);
            ApplyLocalization();
        };

        _btnCheckBalance.Click += async (_, _) => await CheckBalanceAsync();

        var flowLang = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = false };
        flowLang.Controls.Add(_lblLanguageLabel);
        _lblLanguageLabel.Margin = new Padding(0, 6, 8, 0);
        flowLang.Controls.Add(_cmbLang);
        flowLang.Controls.Add(_lblThemeLabel);
        _lblThemeLabel.Margin = new Padding(12, 6, 8, 0);
        flowLang.Controls.Add(_cmbTheme);

        var flowBal = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        flowBal.Controls.Add(_lblBalanceInfo);
        flowBal.Controls.Add(_lblExpiryInfo);

        _lblApiKeyLabel.Margin = new Padding(0, 6, 0, 0);
        _lblEndpointLabel.Margin = new Padding(0, 6, 0, 0);
        _lblPortLabel.Margin = new Padding(0, 6, 0, 0);
        _lblBalanceLabel.Margin = new Padding(0, 6, 0, 0);

        grid.Controls.Add(_lblApiKeyLabel, 0, 0);
        grid.Controls.Add(_txtApiKey, 1, 0);
        grid.Controls.Add(new Label { Text = "", AutoSize = true }, 0, 1);
        grid.Controls.Add(_chkShowKey, 1, 1);

        grid.Controls.Add(_lblEndpointLabel, 0, 2);
        grid.Controls.Add(_txtEndpoint, 1, 2);

        grid.Controls.Add(_lblPortLabel, 0, 3);
        grid.Controls.Add(_numPort, 1, 3);

        grid.Controls.Add(new Label { Text = "", AutoSize = true }, 0, 4);
        grid.Controls.Add(_chkStartup, 1, 4);

        grid.Controls.Add(new Label { Text = "", AutoSize = true }, 0, 5);
        grid.Controls.Add(flowLang, 1, 5);

        grid.Controls.Add(_lblBalanceLabel, 0, 6);
        grid.Controls.Add(_btnCheckBalance, 1, 6);

        grid.Controls.Add(new Label { Text = "", AutoSize = true }, 0, 7);
        grid.Controls.Add(flowBal, 1, 7);

        _cardConn.Controls.Add(grid);
    }

    private void BuildAgentsCard()
    {
        _cardAgents.Header = "Agents (shown as models in Visual Studio)";

        var split = new SplitContainer { Dock = DockStyle.Fill, FixedPanel = FixedPanel.Panel1, SplitterDistance = 200 };

        // Pannello sinistro: lista agenti + pulsanti
        var leftPanel = new Panel { Dock = DockStyle.Fill };
        var leftGrid = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1 };
        leftGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        leftGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 40f));
        leftGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 40f));

        _lstAgents.SelectedIndexChanged += (_, _) =>
        {
            if (_loadingAgent) return;
            _currentIndex = _lstAgents.SelectedIndex;
            LoadAgentToEditor();
        };

        _btnNewAgent.Click += (_, _) => NewAgent();
        _btnDeleteAgent.Click += (_, _) => DeleteCurrentAgent();

        leftGrid.Controls.Add(_lstAgents, 0, 0);
        leftGrid.Controls.Add(_btnNewAgent, 0, 1);
        leftGrid.Controls.Add(_btnDeleteAgent, 0, 2);
        leftPanel.Controls.Add(leftGrid);
        split.Panel1.Controls.Add(leftPanel);

        // Pannello destro: editor dell'agente
        var rightGrid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 7, Padding = new Padding(8) };
        rightGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160f));  // Più spazio per le label
        rightGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        rightGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 60f));   // Agent Name
        rightGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 60f));   // Real Model
        rightGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 55f));   // Copy Preset
        rightGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 60f));   // Token Limits
        rightGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 105f));  // Features (3 checkbox)
        rightGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));   // Markdown (espandibile)
        rightGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 55f));   // Pulsanti Markdown

        _txtAgentName.TextChanged += (_, _) => OnAgentNameChanged();

        _btnSelectModel.Click += (_, _) => OpenModelSelector();
        _btnCopyModelId.Click += (_, _) =>
        {
            if (CurrentAgent is { } agent)
            {
                Clipboard.SetText(agent.Name.Trim());
                MessageBox.Show(Loc.T("IdCopied", agent.Name.Trim()), Loc.T("CopiedTitle"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        };
        _btnCopyPreset.Click += (_, _) => CopyFullPreset();

        _numMaxInput.ValueChanged += (_, _) => OnLimitsChanged();
        _numMaxOutput.ValueChanged += (_, _) => OnLimitsChanged();
        _chkTools.CheckedChanged += (_, _) => OnFeaturesChanged();
        _chkVision.CheckedChanged += (_, _) => OnFeaturesChanged();
        _chkCache.CheckedChanged += (_, _) => OnFeaturesChanged();

        _txtMarkdown.TextChanged += (_, _) =>
        {
            if (_loadingAgent || CurrentAgent is not { } agent) return;
            agent.Markdown = _txtMarkdown.Text.ReplaceLineEndings("\n");
        };

        _btnLoadMd.Click += (_, _) => LoadMarkdownFromFile();
        _btnClearMd.Click += (_, _) => _txtMarkdown.Clear();

        var flowModel = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = true, Anchor = AnchorStyles.Left | AnchorStyles.Top };
        flowModel.Controls.Add(_txtSelectedModel);
        flowModel.Controls.Add(_btnSelectModel);
        flowModel.Controls.Add(_btnCopyModelId);

        var flowPreset = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = true, Anchor = AnchorStyles.Left | AnchorStyles.Top };
        flowPreset.Controls.Add(_btnCopyPreset);
        flowPreset.Controls.Add(_lblPresetContext);

        var flowLimits = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = true, Anchor = AnchorStyles.Left | AnchorStyles.Top };
        flowLimits.Controls.Add(_lblMaxInputLabel);
        _lblMaxInputLabel.Margin = new Padding(0, 8, 6, 0);
        flowLimits.Controls.Add(_numMaxInput);
        flowLimits.Controls.Add(_lblMaxOutputLabel);
        _lblMaxOutputLabel.Margin = new Padding(12, 8, 6, 0);
        flowLimits.Controls.Add(_numMaxOutput);

        var flowFeatures = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, FlowDirection = FlowDirection.TopDown, WrapContents = false, Anchor = AnchorStyles.Left | AnchorStyles.Top };
        flowFeatures.Controls.Add(_chkTools);
        flowFeatures.Controls.Add(_chkVision);
        flowFeatures.Controls.Add(_chkCache);

        var flowMd = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = true, Anchor = AnchorStyles.Left | AnchorStyles.Top };
        flowMd.Controls.Add(_btnLoadMd);
        flowMd.Controls.Add(_btnClearMd);

        _lblAgentNameLabel.Margin = new Padding(0, 16, 0, 0);
        _lblRealModelLabel.Margin = new Padding(0, 16, 0, 0);
        _lblPresetLabel.Margin = new Padding(0, 14, 0, 0);
        _lblTokenLimitsLabel.Margin = new Padding(0, 16, 0, 0);
        _lblFeaturesLabel.Margin = new Padding(0, 16, 0, 0);
        _lblMarkdownLabel.Margin = new Padding(0, 16, 0, 0);

        rightGrid.Controls.Add(_lblAgentNameLabel, 0, 0);
        rightGrid.Controls.Add(_txtAgentName, 1, 0);

        rightGrid.Controls.Add(_lblRealModelLabel, 0, 1);
        rightGrid.Controls.Add(flowModel, 1, 1);

        rightGrid.Controls.Add(_lblPresetLabel, 0, 2);
        rightGrid.Controls.Add(flowPreset, 1, 2);

        rightGrid.Controls.Add(_lblTokenLimitsLabel, 0, 3);
        rightGrid.Controls.Add(flowLimits, 1, 3);

        rightGrid.Controls.Add(_lblFeaturesLabel, 0, 4);
        rightGrid.Controls.Add(flowFeatures, 1, 4);

        rightGrid.Controls.Add(_lblMarkdownLabel, 0, 5);
        rightGrid.Controls.Add(_txtMarkdown, 1, 5);

        rightGrid.Controls.Add(new Label { Text = "", AutoSize = true }, 0, 6);
        rightGrid.Controls.Add(flowMd, 1, 6);

        split.Panel2.Controls.Add(rightGrid);
        _cardAgents.Controls.Add(split);
    }

    private void BuildGuideCard()
    {
        _cardGuide.Header = "Visual Studio Setup Guide (Bring Your Own Model)";
        _cardGuide.Controls.Add(_txtGuide);
    }

    private void ShowPanel(CardPanel card)
    {
        _navConnection.Selected = (card == _cardConn);
        _navAgents.Selected = (card == _cardAgents);
        _navGuide.Selected = (card == _cardGuide);

        _contentPanel.Controls.Clear();
        _contentPanel.Controls.Add(card);

        if (card == _cardGuide) UpdateGuideText();
    }

    protected override void ApplyThemeExtras()
    {
        var p = Theme.Current;
        _navPanel.BackColor = p.Window;
        _bottomBar.BackColor = p.Window;
        _lblPresetContext.ForeColor = p.Accent;

        // Posiziona il pulsante Salva nella barra inferiore
        _btnSave.Location = new Point(_bottomBar.Width - _btnSave.Width - 20, (_bottomBar.Height - _btnSave.Height) / 2);
    }

    private void ApplyLocalization()
    {
        Text = Loc.T("Title");
        _navConnection.Text = Loc.T("NavConnection");
        _navAgents.Text = Loc.T("NavAgents");
        _navGuide.Text = Loc.T("NavGuide");

        _cardConn.Header = Loc.T("GrpConn");
        _lblApiKeyLabel.Text = Loc.T("ApiKeyLabel");
        _lblEndpointLabel.Text = Loc.T("EndpointLabel");
        _lblPortLabel.Text = Loc.T("PortLabel");
        _lblLanguageLabel.Text = Loc.T("LanguageLabel");
        _lblThemeLabel.Text = Loc.T("ThemeLabel");
        _lblBalanceLabel.Text = Loc.T("BalanceLabel");
        _chkShowKey.Text = Loc.T("ShowKey");
        _chkStartup.Text = Loc.T("RunStartup");
        _btnCheckBalance.Text = Loc.T("CheckBalance");

        _cardAgents.Header = Loc.T("GrpModels");
        _btnNewAgent.Text = Loc.T("NewAgent");
        _btnDeleteAgent.Text = Loc.T("DeleteAgent");
        _lblAgentNameLabel.Text = Loc.T("AgentNameLabel");
        _lblRealModelLabel.Text = Loc.T("RealModelLabel");
        _lblPresetLabel.Text = Loc.T("PresetLabel");
        _lblTokenLimitsLabel.Text = Loc.T("TokenLimitsLabel");
        _lblMaxInputLabel.Text = Loc.T("MaxInputLabel");
        _lblMaxOutputLabel.Text = Loc.T("MaxOutputLabel");
        _lblFeaturesLabel.Text = Loc.T("FeaturesLabel");
        _lblMarkdownLabel.Text = Loc.T("MarkdownLabel");
        _btnSelectModel.Text = Loc.T("SelectModel");
        _btnCopyModelId.Text = Loc.T("CopyId");
        _btnCopyPreset.Text = Loc.T("CopyPreset");
        _chkTools.Text = Loc.T("SupportsTools");
        _chkVision.Text = Loc.T("SupportsVision");
        _chkCache.Text = Loc.T("UsePromptCaching");
        _btnLoadMd.Text = Loc.T("LoadFile");
        _btnClearMd.Text = Loc.T("ClearMd");

        _cardGuide.Header = Loc.T("GrpGuide");
        _btnSave.Text = Loc.T("SaveRestart");

        UpdateSelectedPresetDisplay();
        UpdateGuideText();
    }

    // ---------- Gestione agenti ----------

    private void OpenModelSelector()
    {
        var models = ModelCatalog.Cached;
        using var selector = new ModelSelectorForm(models, CurrentAgent?.ModelId ?? "", _settings);

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

    private void RefreshAgentList(int selectIndex)
    {
        _loadingAgent = true;
        _lstAgents.Items.Clear();
        foreach (var a in _agents) _lstAgents.Items.Add(a);
        _currentIndex = _agents.Count == 0 ? -1 : Math.Clamp(selectIndex, 0, _agents.Count - 1);
        _lstAgents.SelectedIndex = _currentIndex;
        _loadingAgent = false;

        LoadAgentToEditor();
    }

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

        _loadingAgent = true;
        _lstAgents.Items[_currentIndex] = agent;
        _lstAgents.SelectedIndex = _currentIndex;
        _loadingAgent = false;
    }

    private void OnLimitsChanged()
    {
        if (_loadingAgent || CurrentAgent is not { } agent) return;
        agent.MaxInputTokens = (int)_numMaxInput.Value;
        agent.MaxOutputTokens = (int)_numMaxOutput.Value;
        UpdateSelectedPresetDisplay();
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
        _settings.Theme = Theme.ToSetting(Theme.Mode);
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
}
