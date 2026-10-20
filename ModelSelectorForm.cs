using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace OneProviderProxy;

/// <summary>
/// Form dedicato alla selezione di un modello dall'elenco completo di OneProvider,
/// con raggruppamento per famiglia e ricerca rapida.
/// </summary>
public class ModelSelectorForm : ThemedForm
{
    private readonly TreeView _treeModels = new()
    {
        Dock = DockStyle.Fill,
        Font = new Font("Segoe UI", 9f),
        HideSelection = false,
        FullRowSelect = true,
        BorderStyle = BorderStyle.None
    };

    private readonly Panel _toolbar = new() { Dock = DockStyle.Top, Height = 44 };
    private readonly TextBox _txtSearch = new() { Width = 340 };
    private readonly Label _lblCount = new() { AutoSize = true, Font = new Font("Segoe UI", 8.5f) };
    private readonly Button _btnRefresh = new() { AutoSize = true, Padding = new Padding(10, 6, 10, 6) };

    private readonly Panel _bottomBar = new() { Dock = DockStyle.Bottom, Height = 60 };
    private readonly Label _lblSelected = new() { AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
    private readonly AccentButton _btnOk = new() { AutoSize = true, Padding = new Padding(16, 8, 16, 8), DialogResult = DialogResult.OK };
    private readonly Button _btnCancel = new() { AutoSize = true, Padding = new Padding(16, 8, 16, 8), DialogResult = DialogResult.Cancel };

    private readonly List<OneProviderModel> _allModels;
    private readonly AppSettings _settings;
    private string _selectedModelId = "";

    public string SelectedModelId => _selectedModelId;

    public ModelSelectorForm(List<OneProviderModel> models, string? currentModelId, AppSettings settings)
    {
        _allModels = models;
        _selectedModelId = currentModelId ?? "";
        _settings = settings;

        InitializeUI();
        PopulateTree(models);
        if (!string.IsNullOrWhiteSpace(currentModelId))
            SelectNodeByModelId(currentModelId);

        UpdateCount(models.Count, models.Count);
    }

    private void InitializeUI()
    {
        Text = Loc.T("SelectModel");
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(560, 500);
        ClientSize = new Size(600, 620);

        BuildToolbar();
        BuildBottomBar();

        _treeModels.AfterSelect += (_, e) =>
        {
            if (e.Node?.Tag is OneProviderModel model)
            {
                _selectedModelId = model.Id;
                _lblSelected.Text = Loc.T("SelectedModel", model.Id);
                _btnOk.Enabled = true;
            }
            else
            {
                _btnOk.Enabled = false;
                _lblSelected.Text = "";
            }
        };

        AcceptButton = _btnOk;
        CancelButton = _btnCancel;

        Controls.Add(_treeModels);
        Controls.Add(_toolbar);
        Controls.Add(_bottomBar);
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

        var lblSearch = new Label { Text = Loc.T("SearchModel"), AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 6, 0) };
        _txtSearch.TextChanged += (_, _) => FilterTree(_txtSearch.Text);

        _btnRefresh.Text = Loc.T("RefreshModels");
        _btnRefresh.Click += async (_, _) => await RefreshModelsAsync();

        flow.Controls.Add(lblSearch);
        flow.Controls.Add(_txtSearch);
        flow.Controls.Add(_btnRefresh);
        flow.Controls.Add(_lblCount);

        _toolbar.Controls.Add(flow);
    }

    private void BuildBottomBar()
    {
        var flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(10, 12, 10, 12)
        };

        _btnOk.Text = "OK";
        _btnOk.Enabled = false;

        _btnCancel.Text = Loc.T("Cancel");

        flow.Controls.Add(_lblSelected);

        var flowRight = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            Padding = new Padding(0)
        };
        flowRight.Controls.Add(_btnOk);
        flowRight.Controls.Add(_btnCancel);

        _bottomBar.Controls.Add(flow);
        _bottomBar.Controls.Add(flowRight);
    }

    protected override void ApplyThemeExtras()
    {
        var p = Theme.Current;
        _toolbar.BackColor = p.Surface;
        _bottomBar.BackColor = p.Surface;
        _lblSelected.ForeColor = p.Accent;

        // Ridipingi i nodi del TreeView con i colori del tema corrente
        foreach (TreeNode familyNode in _treeModels.Nodes)
        {
            familyNode.ForeColor = p.Accent;
            foreach (TreeNode modelNode in familyNode.Nodes)
            {
                modelNode.ForeColor = p.Text;
            }
        }
    }

    private void PopulateTree(List<OneProviderModel> models)
    {
        _treeModels.BeginUpdate();
        _treeModels.Nodes.Clear();

        if (models.Count == 0)
        {
            _treeModels.EndUpdate();
            return;
        }

        // Raggruppa i modelli per famiglia (claude, gpt, gemini, deepseek, ecc.)
        var grouped = models
            .GroupBy(m => GetModelFamily(m.Id))
            .OrderBy(g => g.Key);

        var p = Theme.Current;

        foreach (var group in grouped)
        {
            var familyNode = new TreeNode(FormatFamilyName(group.Key))
            {
                ForeColor = p.Accent,
                NodeFont = new Font("Segoe UI", 9f, FontStyle.Bold)
            };

            foreach (var model in group.OrderBy(m => m.Id))
            {
                var modelNode = new TreeNode(FormatModelDisplay(model))
                {
                    Tag = model,
                    ForeColor = p.Text
                };
                familyNode.Nodes.Add(modelNode);
            }

            _treeModels.Nodes.Add(familyNode);
        }

        _treeModels.ExpandAll();
        _treeModels.EndUpdate();
    }

    private void FilterTree(string filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
        {
            PopulateTree(_allModels);
            SelectNodeByModelId(_selectedModelId);
            UpdateCount(_allModels.Count, _allModels.Count);
            return;
        }

        var filtered = _allModels
            .Where(m => m.Id.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                        m.DisplayName.Contains(filter, StringComparison.OrdinalIgnoreCase))
            .ToList();

        PopulateTree(filtered);
        UpdateCount(filtered.Count, _allModels.Count);
    }

    private void UpdateCount(int visible, int total)
    {
        if (visible == total)
            _lblCount.Text = Loc.T("ModelsCount", total);
        else
            _lblCount.Text = Loc.T("ModelsFiltered", visible, total);
    }

    private void SelectNodeByModelId(string modelId)
    {
        if (string.IsNullOrWhiteSpace(modelId)) return;

        foreach (TreeNode familyNode in _treeModels.Nodes)
        {
            foreach (TreeNode modelNode in familyNode.Nodes)
            {
                if (modelNode.Tag is OneProviderModel model && model.Id.Equals(modelId, StringComparison.OrdinalIgnoreCase))
                {
                    _treeModels.SelectedNode = modelNode;
                    modelNode.EnsureVisible();
                    return;
                }
            }
        }
    }

    private async System.Threading.Tasks.Task RefreshModelsAsync()
    {
        _btnRefresh.Enabled = false;
        _treeModels.Enabled = false;
        _lblCount.Text = Loc.T("LoadingModels");

        try
        {
            var models = await ModelCatalog.FetchAsync(_settings.TargetEndpoint, _settings.ApiKey);
            if (models.Count > 0)
            {
                _allModels.Clear();
                _allModels.AddRange(models);
                PopulateTree(models);
                SelectNodeByModelId(_selectedModelId);
                UpdateCount(models.Count, models.Count);
            }
            else
            {
                _lblCount.Text = Loc.T("NoModels");
            }
        }
        catch
        {
            _lblCount.Text = Loc.T("ConnError");
        }
        finally
        {
            _btnRefresh.Enabled = true;
            _treeModels.Enabled = true;
        }
    }

    private static string GetModelFamily(string modelId)
    {
        var lower = modelId.ToLowerInvariant();
        if (lower.StartsWith("claude")) return "claude";
        if (lower.StartsWith("gpt")) return "gpt";
        if (lower.StartsWith("gemini")) return "gemini";
        if (lower.StartsWith("deepseek")) return "deepseek";
        if (lower.StartsWith("grok")) return "grok";
        if (lower.StartsWith("glm")) return "glm";
        if (lower.StartsWith("qwen")) return "qwen";
        if (lower.StartsWith("kimi")) return "kimi";
        if (lower.StartsWith("kling")) return "kling";
        return "other";
    }

    private static string FormatFamilyName(string family) => family switch
    {
        "claude" => "Claude (Anthropic)",
        "gpt" => "GPT (OpenAI)",
        "gemini" => "Gemini (Google)",
        "deepseek" => "DeepSeek",
        "grok" => "Grok (xAI)",
        "glm" => "GLM (Zhipu AI)",
        "qwen" => "Qwen (Alibaba)",
        "kimi" => "Kimi (Moonshot AI)",
        "kling" => "Kling (Kuaishou)",
        _ => Loc.T("OtherModels")
    };

    private static string FormatModelDisplay(OneProviderModel model)
    {
        if (string.IsNullOrWhiteSpace(model.DisplayName) ||
            model.DisplayName.Equals(model.Id, StringComparison.OrdinalIgnoreCase))
            return model.Id;

        return $"{model.DisplayName} ({model.Id})";
    }
}
