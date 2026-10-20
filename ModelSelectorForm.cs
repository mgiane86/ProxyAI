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
public class ModelSelectorForm : Form
{
    private readonly TreeView _treeModels = new()
    {
        Width = 480,
        Height = 420,
        Font = new Font("Segoe UI", 9f),
        HideSelection = false,
        FullRowSelect = true
    };

    private readonly TextBox _txtSearch = new() { Width = 480 };
    private readonly Label _lblSearch = new() { AutoSize = true };
    private readonly Label _lblSelected = new() { AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold), ForeColor = Color.DarkGreen };
    private readonly Button _btnOk = new() { Size = new Size(100, 32), DialogResult = DialogResult.OK };
    private readonly Button _btnCancel = new() { Size = new Size(100, 32), DialogResult = DialogResult.Cancel };
    private readonly Button _btnRefresh = new() { Size = new Size(100, 32) };

    private readonly List<OneProviderModel> _allModels;
    private string _selectedModelId = "";

    public string SelectedModelId => _selectedModelId;

    public ModelSelectorForm(List<OneProviderModel> models, string? currentModelId)
    {
        _allModels = models;
        _selectedModelId = currentModelId ?? "";

        InitializeUI();
        PopulateTree(models);
        if (!string.IsNullOrWhiteSpace(currentModelId))
            SelectNodeByModelId(currentModelId);
    }

    private void InitializeUI()
    {
        Text = Loc.T("SelectModel");
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(520, 560);

        _lblSearch.Location = new Point(15, 15);
        _lblSearch.Text = Loc.T("SearchModel");

        _txtSearch.Location = new Point(15, 35);
        _txtSearch.TextChanged += (_, _) => FilterTree(_txtSearch.Text);

        _treeModels.Location = new Point(15, 65);
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

        _lblSelected.Location = new Point(15, 495);
        _lblSelected.Text = "";

        _btnRefresh.Location = new Point(15, 520);
        _btnRefresh.Text = Loc.T("RefreshModels");
        _btnRefresh.Click += async (_, _) => await RefreshModelsAsync();

        _btnOk.Location = new Point(310, 520);
        _btnOk.Text = "OK";
        _btnOk.Enabled = false;

        _btnCancel.Location = new Point(415, 520);
        _btnCancel.Text = Loc.T("Cancel");

        AcceptButton = _btnOk;
        CancelButton = _btnCancel;

        Controls.AddRange(new Control[] {
            _lblSearch, _txtSearch, _treeModels, _lblSelected,
            _btnRefresh, _btnOk, _btnCancel
        });
    }

    private void PopulateTree(List<OneProviderModel> models)
    {
        _treeModels.BeginUpdate();
        _treeModels.Nodes.Clear();

        // Raggruppa i modelli per famiglia (claude, gpt, gemini, deepseek, ecc.)
        var grouped = models
            .GroupBy(m => GetModelFamily(m.Id))
            .OrderBy(g => g.Key);

        foreach (var group in grouped)
        {
            var familyNode = new TreeNode(FormatFamilyName(group.Key))
            {
                ForeColor = Color.DarkBlue,
                NodeFont = new Font("Segoe UI", 9f, FontStyle.Bold)
            };

            foreach (var model in group.OrderBy(m => m.Id))
            {
                var modelNode = new TreeNode(FormatModelDisplay(model))
                {
                    Tag = model
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
            return;
        }

        var filtered = _allModels
            .Where(m => m.Id.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                        m.DisplayName.Contains(filter, StringComparison.OrdinalIgnoreCase))
            .ToList();

        PopulateTree(filtered);
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
        try
        {
            // Richiede endpoint e chiave dalla form principale (passati tramite tag o proprietà)
            var settings = (this.Tag as AppSettings);
            if (settings == null) return;

            var models = await ModelCatalog.FetchAsync(settings.TargetEndpoint, settings.ApiKey);
            if (models.Count > 0)
            {
                _allModels.Clear();
                _allModels.AddRange(models);
                PopulateTree(models);
                SelectNodeByModelId(_selectedModelId);
            }
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
