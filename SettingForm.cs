using Microsoft.VisualBasic.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OneProviderProxy;

public class SettingsForm : Form
{
    private readonly AppSettings _settings;
    private readonly Action _onSettingsSaved;

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

    // Modelli & Preset
    private readonly GroupBox _grpModels = new();
    private readonly Label _lblSelectModel = new();
    private readonly ComboBox _cmbModels = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 230 };
    private readonly Button _btnCopyModelId = new() { Size = new Size(75, 26) };
    private readonly Button _btnCopyPreset = new() { Size = new Size(95, 26) };

    private readonly Label _lblPresetContext = new() { AutoSize = true, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), ForeColor = Color.FromArgb(0, 102, 204) };
    private readonly Label _lblPresetOutput = new() { AutoSize = true, Font = new Font("Segoe UI", 8.5f, FontStyle.Regular), ForeColor = Color.FromArgb(40, 40, 40) };
    private readonly Label _lblPresetFeatures = new() { AutoSize = true, Font = new Font("Segoe UI", 8.5f, FontStyle.Italic), ForeColor = Color.DarkSlateGray };

    // Guida & Azioni
    private readonly GroupBox _grpGuide = new();
    private readonly RichTextBox _txtGuide = new() { ReadOnly = true, Height = 95, Width = 465, BackColor = Color.FromArgb(245, 245, 245), BorderStyle = BorderStyle.FixedSingle };
    private readonly Button _btnSave = new() { Size = new Size(140, 32) };

    private readonly List<ModelPreset> _modelPresets = new()
    {
        new("Claude Fable 5.1 (Architecture / Design)", "claude-fable-5-1", 100000, 8192, true, true),
        new("Claude Opus 5.5 (Coding / Refactoring)", "claude-opus-5-5", 128000, 8192, true, true),
        new("Claude Sonnet 5.5 (Balanced)", "claude-sonnet-5-5", 128000, 8192, true, true),
        new("DeepSeek V4 Pro (Questions / Syntax)", "deepseek-v4-pro", 64000, 4096, true, false),
        new("DeepSeek V4.1 Flash (Fast / Light)", "deepseek-v4.1-flash", 64000, 4096, true, true),
        new("GPT-6.1 Sol (General Purpose)", "gpt-6.1-sol", 128000, 8192, true, true),
        new("GPT-6 Luna (Ultra-Fast)", "gpt-6-luna", 64000, 4096, true, true),
        new("Gemini 4 Argon (Multimodal)", "gemini-4-argon", 128000, 8192, true, true),
        new("Gemini 3.1 Pro High", "gemini-3.1-pro-high", 128000, 8192, true, true),
        new("GLM 5.3", "glm-5.3", 96000, 4096, true, false),
        new("Grok 4.7", "grok-4.7", 100000, 4096, true, true)
    };

    public SettingsForm(AppSettings settings, Action onSettingsSaved)
    {
        _settings = settings;
        _onSettingsSaved = onSettingsSaved;

        if (!string.IsNullOrEmpty(_settings.Language))
        {
            Loc.CurrentLanguage = _settings.Language;
        }

        InitializeUI();
        LoadValues();
        PopulateDefaultModels();
        ApplyLocalization();
    }

    private void InitializeUI()
    {
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(520, 680);

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

        // Modelli
        _grpModels.Location = new Point(15, 285);
        _grpModels.Size = new Size(490, 135);

        _lblSelectModel.Location = new Point(15, 24);
        _lblSelectModel.AutoSize = true;
        _cmbModels.Location = new Point(70, 21);
        _cmbModels.SelectedIndexChanged += (_, _) => UpdateSelectedPresetDisplay();

        _btnCopyModelId.Location = new Point(305, 20);
        _btnCopyModelId.Click += (_, _) =>
        {
            if (_cmbModels.SelectedItem is ModelPreset selected)
            {
                Clipboard.SetText(selected.Id);
                MessageBox.Show(Loc.T("IdCopied", selected.Id), Loc.T("CopiedTitle"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        };

        _btnCopyPreset.Location = new Point(385, 20);
        _btnCopyPreset.Click += (_, _) => CopyFullPreset();

        _lblPresetContext.Location = new Point(15, 60);
        _lblPresetOutput.Location = new Point(15, 83);
        _lblPresetFeatures.Location = new Point(15, 106);

        _grpModels.Controls.AddRange(new Control[] {
            _lblSelectModel, _cmbModels, _btnCopyModelId, _btnCopyPreset,
            _lblPresetContext, _lblPresetOutput, _lblPresetFeatures
        });

        // Guida
        _grpGuide.Location = new Point(15, 430);
        _grpGuide.Size = new Size(490, 190);

        _txtGuide.Location = new Point(12, 22);
        _txtGuide.Height = 155;
        _grpGuide.Controls.Add(_txtGuide);

        // Salva
        _btnSave.Location = new Point(365, 630);
        _btnSave.Click += BtnSave_Click;

        Controls.AddRange(new Control[] { _grpConn, _grpModels, _grpGuide, _btnSave });
    }

    private void ApplyLocalization()
    {
        _chkStartup.Text = Loc.T("RunStartup");
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
        _lblSelectModel.Text = Loc.T("Model");
        _btnCopyModelId.Text = Loc.T("CopyId");
        _btnCopyPreset.Text = Loc.T("CopyPreset");

        _grpGuide.Text = Loc.T("GrpGuide");
        _btnSave.Text = Loc.T("SaveRestart");

        UpdateSelectedPresetDisplay();
        UpdateGuideText();
    }

    private void UpdateSelectedPresetDisplay()
    {
        if (_cmbModels.SelectedItem is ModelPreset preset)
        {
            _lblPresetContext.Text = Loc.T("ContextLimit", preset.SuggestedContext);
            _lblPresetOutput.Text = Loc.T("OutputLimit", preset.SuggestedOutput);
            _lblPresetFeatures.Text = Loc.T("Features", preset.SupportsTools ? "X" : " ", preset.SupportsVision ? "X" : " ");
        }
    }

    private void CopyFullPreset()
    {
        if (_cmbModels.SelectedItem is ModelPreset preset)
        {
            var text =
                $"Display Name: {preset.DisplayName.Split('(')[0].Trim()}\n" +
                $"Model ID: {preset.Id}\n" +
                $"Endpoint: http://localhost:{(int)_numPort.Value}/v1\n" +
                $"Context Limit: {preset.SuggestedContext}\n" +
                $"Output Limit: {preset.SuggestedOutput}\n" +
                $"Tool calling: {(preset.SupportsTools ? Loc.T("Yes") : Loc.T("No"))}\n" +
                $"Vision support: {(preset.SupportsVision ? Loc.T("Yes") : Loc.T("No"))}";

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
            $"{Loc.T("GuideStep6")}";
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

    private void PopulateDefaultModels()
    {
        _cmbModels.Items.Clear();
        foreach (var m in _modelPresets) _cmbModels.Items.Add(m);
        if (_cmbModels.Items.Count > 0)
        {
            _cmbModels.SelectedIndex = 0;
            UpdateSelectedPresetDisplay();
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
        _settings.ApiKey = _txtApiKey.Text.Trim();
        _settings.TargetEndpoint = _txtEndpoint.Text.Trim().TrimEnd('/');
        _settings.Port = (int)_numPort.Value;
        _settings.Language = Loc.CurrentLanguage;
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

    private record ModelPreset(
        string DisplayName,
        string Id,
        int SuggestedContext,
        int SuggestedOutput,
        bool SupportsTools,
        bool SupportsVision)
    {
        public override string ToString() => DisplayName;
    }
}