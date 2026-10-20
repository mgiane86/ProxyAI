using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace OneProviderProxy;

public enum ThemeMode { Light, Dark }

/// <summary>
/// Colori della UI. Esistono solo due palette (chiara e scura): tutto il codice
/// legge i colori da <see cref="Theme.Current"/>, mai da valori hardcoded.
/// </summary>
public sealed class Palette
{
    public Color Window { get; init; }
    public Color Surface { get; init; }
    public Color SurfaceAlt { get; init; }
    public Color SurfaceHover { get; init; }
    public Color SurfacePressed { get; init; }
    public Color Border { get; init; }
    public Color Text { get; init; }
    public Color TextMuted { get; init; }
    public Color Accent { get; init; }
    public Color AccentText { get; init; }
    public Color AccentHover { get; init; }
    public Color AccentPressed { get; init; }
    public Color Success { get; init; }
    public Color Warning { get; init; }
    public Color Danger { get; init; }
    // Il log resta scuro in entrambi i temi: è una console e i colori emessi dal proxy
    // (LimeGreen, DeepSkyBlue, Orange) sono leggibili solo su fondo scuro.
    public Color LogBack { get; init; }
    public Color LogText { get; init; }

    public static readonly Palette Light = new()
    {
        Window = Color.FromArgb(245, 246, 248),
        Surface = Color.White,
        SurfaceAlt = Color.FromArgb(240, 242, 245),
        SurfaceHover = Color.FromArgb(232, 235, 239),
        SurfacePressed = Color.FromArgb(222, 226, 232),
        Border = Color.FromArgb(214, 218, 224),
        Text = Color.FromArgb(28, 32, 38),
        TextMuted = Color.FromArgb(108, 115, 125),
        Accent = Color.FromArgb(0, 110, 218),
        AccentText = Color.White,
        AccentHover = Color.FromArgb(0, 96, 192),
        AccentPressed = Color.FromArgb(0, 82, 168),
        Success = Color.FromArgb(20, 140, 70),
        Warning = Color.FromArgb(190, 110, 0),
        Danger = Color.FromArgb(200, 40, 45),
        LogBack = Color.FromArgb(24, 25, 28),
        LogText = Color.FromArgb(214, 216, 222)
    };

    public static readonly Palette Dark = new()
    {
        Window = Color.FromArgb(28, 29, 32),
        Surface = Color.FromArgb(40, 42, 46),
        SurfaceAlt = Color.FromArgb(52, 55, 61),
        SurfaceHover = Color.FromArgb(62, 66, 73),
        SurfacePressed = Color.FromArgb(72, 76, 84),
        Border = Color.FromArgb(68, 72, 80),
        Text = Color.FromArgb(234, 236, 239),
        TextMuted = Color.FromArgb(150, 156, 165),
        Accent = Color.FromArgb(74, 158, 255),
        AccentText = Color.White,
        AccentHover = Color.FromArgb(96, 172, 255),
        AccentPressed = Color.FromArgb(60, 140, 235),
        Success = Color.FromArgb(84, 196, 124),
        Warning = Color.FromArgb(235, 168, 70),
        Danger = Color.FromArgb(240, 110, 110),
        LogBack = Color.FromArgb(18, 19, 21),
        LogText = Color.FromArgb(216, 218, 222)
    };
}

/// <summary>
/// Tema corrente e applicazione dello stesso ai controlli. Un form che vuole seguire
/// il tema eredita da <see cref="ThemedForm"/>; i controlli figli vengono colorati da
/// <see cref="ApplyRecursive"/> in base al loro tipo.
/// </summary>
public static class Theme
{
    public static ThemeMode Mode { get; private set; } = ThemeMode.Light;

    public static Palette Current => Mode == ThemeMode.Dark ? Palette.Dark : Palette.Light;

    /// <summary>Sollevato quando cambia il tema: i form aperti si ridipingono da soli.</summary>
    public static event Action? Changed;

    public static void Set(ThemeMode mode)
    {
        if (Mode == mode) return;
        Mode = mode;
        Changed?.Invoke();
    }

    public static ThemeMode Parse(string? value) =>
        string.Equals(value, "dark", StringComparison.OrdinalIgnoreCase) ? ThemeMode.Dark : ThemeMode.Light;

    public static string ToSetting(ThemeMode mode) => mode == ThemeMode.Dark ? "dark" : "light";

    /// <summary>Colora il form (e tutti i figli) secondo il tema corrente.</summary>
    public static void Apply(Form form)
    {
        if (form.IsDisposed) return;
        ApplyRecursive(form);
        ApplyTitleBar(form);
    }

    public static void ApplyRecursive(Control root)
    {
        StyleControl(root);
        foreach (Control child in root.Controls) ApplyRecursive(child);
    }

    private static void StyleControl(Control c)
    {
        var p = Current;

        // L'ordine dei case conta: le sottoclassi vanno prima della classe base.
        switch (c)
        {
            case CardPanel card:
                card.BackColor = p.Surface;
                card.ForeColor = p.Text;
                card.Invalidate();
                return;

            case AccentButton accent:
                accent.FlatStyle = FlatStyle.Flat;
                accent.FlatAppearance.BorderSize = 0;
                accent.FlatAppearance.MouseOverBackColor = p.AccentHover;
                accent.FlatAppearance.MouseDownBackColor = p.AccentPressed;
                accent.BackColor = p.Accent;
                accent.ForeColor = p.AccentText;
                accent.UseVisualStyleBackColor = false;
                // Bordi leggermente arrotondati per un effetto più morbido
                accent.Region = new Region(RoundedRect(accent.ClientRectangle, 6));
                return;

            case NavButton nav:
                nav.BackColor = c.Parent?.BackColor ?? p.Window;
                nav.ForeColor = nav.Selected ? p.Text : p.TextMuted;
                nav.Invalidate();
                return;

            case Button b:
                b.FlatStyle = FlatStyle.Flat;
                b.FlatAppearance.BorderSize = 1;
                b.FlatAppearance.BorderColor = p.Border;
                b.FlatAppearance.MouseOverBackColor = p.SurfaceHover;
                b.FlatAppearance.MouseDownBackColor = p.SurfacePressed;
                b.BackColor = p.SurfaceAlt;
                b.ForeColor = p.Text;
                b.UseVisualStyleBackColor = false;
                return;

            case TextBox tb:
                tb.BorderStyle = BorderStyle.FixedSingle;
                tb.BackColor = tb.ReadOnly ? p.SurfaceAlt : p.Surface;
                tb.ForeColor = tb.ReadOnly ? p.TextMuted : p.Text;
                return;

            case NumericUpDown num:
                num.BorderStyle = BorderStyle.FixedSingle;
                num.BackColor = p.Surface;
                num.ForeColor = p.Text;
                return;

            case ComboBox cb:
                cb.FlatStyle = FlatStyle.Flat;
                cb.BackColor = p.Surface;
                cb.ForeColor = p.Text;
                return;

            case ListBox lb:
                lb.BorderStyle = BorderStyle.FixedSingle;
                lb.BackColor = p.Surface;
                lb.ForeColor = p.Text;
                return;

            case TreeView tv:
                tv.BorderStyle = BorderStyle.FixedSingle;
                tv.BackColor = p.Surface;
                tv.ForeColor = p.Text;
                tv.LineColor = p.Border;
                return;

            case LogConsoleBox console:
                console.BorderStyle = BorderStyle.None;
                console.BackColor = p.LogBack;
                console.ForeColor = p.LogText;
                return;

            case CheckBox or RadioButton or Label or LinkLabel:
                c.BackColor = c.Parent?.BackColor ?? p.Window;
                c.ForeColor = p.Text;
                return;

            default:
                c.BackColor = c.Parent?.BackColor ?? p.Window;
                c.ForeColor = p.Text;
                return;
        }
    }

    private static System.Drawing.Drawing2D.GraphicsPath RoundedRect(Rectangle bounds, int radius)
    {
        int diameter = radius * 2;
        var path = new System.Drawing.Drawing2D.GraphicsPath();
        var arc = new Rectangle(bounds.Location, new Size(diameter, diameter));

        // angolo in alto a sinistra
        path.AddArc(arc, 180, 90);

        // angolo in alto a destra
        arc.X = bounds.Right - diameter;
        path.AddArc(arc, 270, 90);

        // angolo in basso a destra
        arc.Y = bounds.Bottom - diameter;
        path.AddArc(arc, 0, 90);

        // angolo in basso a sinistra
        arc.X = bounds.Left;
        path.AddArc(arc, 90, 90);

        path.CloseFigure();
        return path;
    }

    // ---------- Title bar scura ----------

    private const int DwmwaUseImmersiveDarkMode = 20;      // Windows 10 2004+
    private const int DwmwaUseImmersiveDarkModeLegacy = 19; // Windows 10 1809-1909

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    /// <summary>
    /// Rende scura la barra del titolo quando il tema è scuro. Su Windows datati la
    /// chiamata fallisce e la barra resta chiara: nessuna eccezione, solo un degrado.
    /// </summary>
    public static void ApplyTitleBar(Form form)
    {
        if (!form.IsHandleCreated) return;
        try
        {
            int value = Mode == ThemeMode.Dark ? 1 : 0;
            if (DwmSetWindowAttribute(form.Handle, DwmwaUseImmersiveDarkMode, ref value, sizeof(int)) != 0)
            {
                DwmSetWindowAttribute(form.Handle, DwmwaUseImmersiveDarkModeLegacy, ref value, sizeof(int));
            }
        }
        catch { }
    }

    // ---------- Menu tray ----------

    /// <summary>Applica il tema a un menu contestuale (tray).</summary>
    public static void ApplyMenu(ContextMenuStrip menu)
    {
        var p = Current;
        menu.BackColor = p.Surface;
        menu.ForeColor = p.Text;
        menu.Renderer = Mode == ThemeMode.Dark
            ? new ToolStripProfessionalRenderer(new DarkColorTable()) { RoundedEdges = false }
            : new ToolStripProfessionalRenderer();

        foreach (ToolStripItem item in menu.Items)
        {
            if (item is ToolStripSeparator) continue;
            item.ForeColor = p.Text;
        }
    }

    private sealed class DarkColorTable : ProfessionalColorTable
    {
        private static Palette P => Palette.Dark;

        public override Color ToolStripDropDownBackground => P.Surface;
        public override Color MenuBorder => P.Border;
        public override Color MenuItemBorder => P.Accent;
        public override Color MenuItemSelected => P.SurfaceHover;
        public override Color MenuItemSelectedGradientBegin => P.SurfaceHover;
        public override Color MenuItemSelectedGradientEnd => P.SurfaceHover;
        public override Color ImageMarginGradientBegin => P.Surface;
        public override Color ImageMarginGradientMiddle => P.Surface;
        public override Color ImageMarginGradientEnd => P.Surface;
        public override Color SeparatorDark => P.Border;
        public override Color SeparatorLight => P.Border;
    }
}

/// <summary>Form che segue il tema: si ridipinge da solo quando il tema cambia.</summary>
public class ThemedForm : Form
{
    public ThemedForm()
    {
        Theme.Changed += OnThemeChanged;
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        Theme.Apply(this);
        ApplyThemeExtras();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Theme.ApplyTitleBar(this);
    }

    private void OnThemeChanged()
    {
        if (IsDisposed || Disposing) return;
        Theme.Apply(this);
        ApplyThemeExtras();
        Invalidate(true);
    }

    /// <summary>Punto di estensione: ridipinture che il tema generico non può fare (nodi di un TreeView, ecc.).</summary>
    protected virtual void ApplyThemeExtras() { }

    protected override void Dispose(bool disposing)
    {
        if (disposing) Theme.Changed -= OnThemeChanged;
        base.Dispose(disposing);
    }
}

/// <summary>Riquadro con bordo e intestazione: sostituisce i vecchi GroupBox.</summary>
public class CardPanel : Panel
{
    private string _header = "";

    public CardPanel()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw, true);
        Padding = new Padding(14, 14, 14, 14);
    }

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public string Header
    {
        get => _header;
        set
        {
            _header = value ?? "";
            Padding = new Padding(14, string.IsNullOrEmpty(_header) ? 14 : HeaderHeight + 10, 14, 14);
            Invalidate();
        }
    }

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public int HeaderHeight { get; set; } = 32;

    protected override void OnPaint(PaintEventArgs e)
    {
        var p = Theme.Current;
        e.Graphics.Clear(BackColor);

        using (var pen = new Pen(p.Border))
        {
            e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);

            if (!string.IsNullOrEmpty(_header))
            {
                using var font = new Font(Font, FontStyle.Bold);
                TextRenderer.DrawText(e.Graphics, _header, font, new Point(14, 9), p.TextMuted,
                    TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
                e.Graphics.DrawLine(pen, 14, HeaderHeight + 2, Width - 15, HeaderHeight + 2);
            }
        }

        base.OnPaint(e);
    }
}

/// <summary>Pulsante dell'azione principale (Salva): pieno, colore d'accento.</summary>
public class AccentButton : Button { }

/// <summary>
/// Console di log: resta scura in entrambi i temi perché i colori emessi dal proxy
/// (LimeGreen, DeepSkyBlue, Orange) sono leggibili solo su fondo scuro.
/// </summary>
public class LogConsoleBox : RichTextBox { }

/// <summary>Voce della navigazione laterale: testo a sinistra e barretta d'accento quando è selezionata.</summary>
public class NavButton : Button
{
    private bool _selected;
    private bool _hover;

    public NavButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw, true);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        TextAlign = ContentAlignment.MiddleLeft;
        Padding = new Padding(18, 0, 8, 0);
        Height = 40;
        Cursor = Cursors.Hand;
        TabStop = true;
    }

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool Selected
    {
        get => _selected;
        set { _selected = value; Invalidate(); }
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var p = Theme.Current;
        var back = _selected ? p.Surface : _hover ? p.SurfaceAlt : Parent?.BackColor ?? p.Window;

        using (var brush = new SolidBrush(back))
            e.Graphics.FillRectangle(brush, ClientRectangle);

        if (_selected)
        {
            using var accent = new SolidBrush(p.Accent);
            e.Graphics.FillRectangle(accent, new Rectangle(0, 4, 3, Math.Max(1, Height - 8)));
        }

        var textRect = new Rectangle(Padding.Left, 0, Math.Max(1, Width - Padding.Horizontal), Height);
        TextRenderer.DrawText(e.Graphics, Text, Font, textRect, _selected ? p.Text : p.TextMuted,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
    }
}
