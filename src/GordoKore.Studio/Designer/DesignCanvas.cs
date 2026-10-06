using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using GordoKore.Studio.Generation;
using GordoKore.Studio.Grf;
using GordoKore.Studio.Model;

namespace GordoKore.Studio.Designer;

/// <summary>
/// Previa da janela como o host do SDK desenha no jogo (titulo, corpo branco, X de fechar, controles com
/// as texturas da GRF). Clicar seleciona, arrastar move, o canto inferior direito redimensiona.
/// </summary>
public sealed class DesignCanvas : Control
{
    public const int MaxZoom = 6;
    private const int Pad = 16;
    private const int TitleHeight = 17;
    private const int Grip = 6;
    private static readonly Color Border = Color.FromArgb(0x9C, 0xA3, 0xB4);
    private static readonly Color Hint = Color.FromArgb(0x64, 0x64, 0x64);
    private const string Ui = "유저인터페이스\\";

    private WindowModel? _window;
    private Widget? _selected;
    private int _zoom = 2;
    private enum Drag { None, Move, ResizeWidget, ResizeWindow }
    private Drag _drag;
    private Point _dragStart;
    private Point _mouseDown; // na tela
    private bool _moved;
    private Rectangle _dragOriginal;
    private readonly List<(bool vertical, int at)> _guides = new(); // linhas guia do arrasto atual
    private static readonly Color GuideColor = Color.FromArgb(0xFF, 0x00, 0xAA);
    private const int SnapPixels = 6; // distancia (na tela) que puxa para a guia

    private readonly Font _font = new("Tahoma", 12f, GraphicsUnit.Pixel);
    private readonly Font _bold = new("Tahoma", 12f, FontStyle.Bold, GraphicsUnit.Pixel);
    private readonly Graphics _measure = Graphics.FromImage(new Bitmap(1, 1));

    /// <summary>Imagem relativa a data\texture\ (com cache de quem chama).</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<string, Bitmap?> Texture { get; set; } = _ => null;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<string, Bitmap?> SpriteImage { get; set; } = _ => null;
    /// <summary>Itens da GRF para as listas de dados de itens e os icones de item.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public ItemTable Items
    {
        get => _items;
        set
        {
            _items = value;
            _itemRows = null;
        }
    }
    private ItemTable _items = ItemTable.Empty;
    private List<string[]>? _itemRows; // registros da previa de itens (montados uma vez: sao dezenas de milhares)
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Language { get; set; }
    /// <summary>Passo da grade em px (0 = sem grade). Ctrl durante o arrasto solta grade e guias.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Grid { get; set; } = 4;

    public event Action? SelectionChanged;
    public event Action? Changed;
    /// <summary>Aviso para o log.</summary>
    public event Action<string>? Notice;

    public DesignCanvas()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.Selectable, true);
        BackColor = Color.FromArgb(0x4A, 0x4D, 0x55);
        _measure.TextRenderingHint = TextRenderingHint.SingleBitPerPixelGridFit;
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public WindowModel? Window
    {
        get => _window;
        set
        {
            _window = value;
            _selected = null;
            _scroll.Clear();
            _editing = null;
            UpdateSize();
            Invalidate();
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Widget? Selected
    {
        get => _selected;
        set
        {
            _selected = value;
            Invalidate();
            SelectionChanged?.Invoke();
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Zoom
    {
        get => _zoom;
        set
        {
            _zoom = Math.Clamp(value, 1, MaxZoom);
            UpdateSize();
            Invalidate();
        }
    }

    public void UpdateSize()
    {
        if (_window == null)
            return;
        // Janela redimensionavel: o tamanho do projeto tambem fica nos limites e no passo, como o host faz
        if (_window.Resizable)
        {
            _window.Width = Math.Clamp(_window.Width, _window.MinWidth, Math.Max(_window.MinWidth, _window.MaxWidth));
            int height = Math.Clamp(_window.Height, _window.MinHeight, Math.Max(_window.MinHeight, _window.MaxHeight));
            _window.Height = _window.HeightStep > 0 ? _window.MinHeight + (height - _window.MinHeight) / _window.HeightStep * _window.HeightStep : height;
        }
        Size = new Size((_window.Width + 2 * Pad) * _zoom, (_window.Height + 2 * Pad) * _zoom);
    }

    // ---------------------------------------------------------------------------------------------
    // Medidas
    // ---------------------------------------------------------------------------------------------

    private int TextWidth(string text, bool bold) =>
        text == "" ? 0 : (int)Math.Ceiling(_measure.MeasureString(text, bold ? _bold : _font, PointF.Empty, StringFormat.GenericTypographic).Width);

    private Size SizeOf(Widget w) => w.Type switch
    {
        WidgetType.Label when IsCell(w) && (w.AlignRight || w.Fit) && w.Width > 0 => new Size(w.Width, 14),
        WidgetType.Label => new Size(Math.Max(8, TextWidth(Counts(w.Text.Get(Language)), w.Bold)), 14),
        WidgetType.Button => new Size(w.Width > 0 ? w.Width : TextWidth(w.Text.Get(Language), false) + 16, 20),
        WidgetType.ImageButton when w.Text.Pt != "" && Texture(w.Image) is { } caption => caption.Size, // botao com legenda: tamanho do bitmap
        WidgetType.CheckBox => new Size(34, 15),
        WidgetType.Image => Texture(w.Image) is { } image ? image.Size : new Size(24, 24),
        WidgetType.ItemIcon => new Size(24, 24),
        WidgetType.Strip => new Size(Math.Max(1, w.Width), Texture(w.Image)?.Height ?? 20),
        _ => new Size(Math.Max(1, w.Width), Math.Max(1, w.Height))
    };

    /// <summary>Retangulo na janela (filho de area rolavel: pela area e pela rolagem; celula: na 1a linha da lista).</summary>
    public Rectangle WidgetBounds(Widget w) => new(Origin(w) + new Size(w.X, w.Y), SizeOf(w));

    private static bool Sizable(Widget w) => w.Type is not (WidgetType.Label or WidgetType.CheckBox or WidgetType.Image or WidgetType.ItemIcon);

    private Point ToWindow(Point p) => new(p.X / _zoom - Pad, p.Y / _zoom - Pad);

    // ---------------------------------------------------------------------------------------------
    // Conteineres: area rolavel (moldura de 1 px, barra de 15 px na direita, conteudo ate o widget mais baixo + 4,
    // filho que nao cabe inteiro some, como o host) e lista de dados (os filhos sao o modelo da linha)
    // ---------------------------------------------------------------------------------------------

    private const int ScrollBarWidth = 15;
    private const int WheelStep = 24; // 3 linhas de 8 px, como a roda no jogo
    private readonly Dictionary<Widget, int> _scroll = new(); // rolagem da previa: area em px, lista em linhas
    private Widget? _editing; // conteiner aberto com duplo clique: os cliques vao para os widgets dele

    /// <summary>Area rolavel ou lista de dados aberta com duplo clique (null = nenhuma).</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Widget? EditingArea
    {
        get => _editing;
        set
        {
            _editing = value;
            Invalidate();
        }
    }

    private Widget? ContainerOf(Widget w) =>
        w.Parent == 0 ? null : _window?.Widgets.FirstOrDefault(c => c.IsContainer && c.Id == w.Parent);

    /// <summary>Celula do modelo de linha de uma lista de dados.</summary>
    public bool IsCell(Widget w) => ContainerOf(w) is { Type: WidgetType.DataList };

    // Area: dentro da moldura e antes da barra. Lista: a 1a linha (onde o modelo é editado)
    private static Rectangle ViewOf(Widget c) => c.Type == WidgetType.DataList
        ? new Rectangle(c.X, c.Y, Math.Max(0, c.Width - ScrollBarWidth), Math.Max(1, c.RowHeight))
        : new Rectangle(c.X + 1, c.Y + 1, Math.Max(0, c.Width - 2 - ScrollBarWidth), Math.Max(0, c.Height - 2));

    private int ContentOf(Widget area)
    {
        int lowest = _window!.Widgets.Where(w => w.Parent == area.Id && !w.IsContainer)
            .Select(w => w.Y + SizeOf(w).Height).DefaultIfEmpty(0).Max();
        return lowest > 0 ? lowest + 4 : 0;
    }

    private int MaxScroll(Widget c) => c.Type == WidgetType.DataList
        ? Math.Max(0, PreviewData(c).Rows.Count - VisibleRows(c))
        : Math.Max(0, ContentOf(c) - (c.Height - 2));

    private int ScrollOf(Widget c) => Math.Clamp(_scroll.GetValueOrDefault(c), 0, MaxScroll(c));

    private Point Origin(Widget w) => ContainerOf(w) switch
    {
        { Type: WidgetType.DataList } list => new Point(list.X, list.Y),
        { } area => new Point(area.X + 1, area.Y + 1 - ScrollOf(area)),
        _ => Point.Empty
    };

    private bool Shown(Widget w)
    {
        switch (ContainerOf(w))
        {
            case null:
                return true;
            case { Type: WidgetType.DataList } list:
                return list == _editing; // fora da edicao as linhas sao desenhadas com os registros
            case var area:
                var r = WidgetBounds(w);
                var view = ViewOf(area);
                return r.Top >= view.Top && r.Bottom <= view.Bottom;
        }
    }

    private static int VisibleRows(Widget list) => Math.Max(1, list.Height / Math.Max(1, list.RowHeight));

    // ---------------------------------------------------------------------------------------------
    // Registros da previa: itens da GRF, exemplos de monstros e mapas, a lista fixa
    // ---------------------------------------------------------------------------------------------

    private static readonly string[][] SampleMonsters =
    {
        new[] { "1002", "Poring", "prt_fild08, pay_fild01", "poring" },
        new[] { "1113", "Drops", "prt_fild08, moc_fild02", "drops" },
        new[] { "1063", "Lunático", "prt_fild07, gef_fild07", "lunatic" },
        new[] { "1007", "Fabre", "prt_fild01, pay_fild01", "fabre" },
        new[] { "1031", "Poporing", "prt_fild02, gef_fild05", "poporing" },
        new[] { "1004", "Zangão", "mjolnir_04, prt_fild05", "hornet" },
        new[] { "1001", "Escorpião", "moc_fild03, moc_fild04", "scorpion" },
        new[] { "1005", "Familiar", "prt_sewb1, moc_pryd01", "farmiliar" }
    };

    private static readonly string[] SampleMaps = { "alberta", "aldebaran", "geffen", "izlude", "morocc", "payon", "prontera", "yuno" };

    private static readonly string[][] SampleItems =
    {
        new[] { "501", "Poção Vermelha" }, new[] { "502", "Poção Laranja" }, new[] { "503", "Poção Amarela" },
        new[] { "504", "Poção Branca" }, new[] { "505", "Poção Azul" }, new[] { "512", "Maçã" }, new[] { "909", "Jellopy" }
    };

    // Fontes ao vivo do mapa: as colunas do host (world_table) e exemplos para a previa
    private static readonly List<string> MapColumns = new() { "id", "nome", "x", "y", "classe", "classe_nome", "distancia" };

    private static readonly string[][] SamplePlayers =
    {
        new[] { "2000101", "Bruxa Sincera", "152", "181", "4010", "Arquimago", "2" },
        new[] { "2000102", "Paladino Leal", "148", "176", "4015", "Paladino", "5" },
        new[] { "2000103", "Ninja do Leste", "160", "190", "25", "Ninja", "9" },
        new[] { "2000104", "Mercador Feliz", "141", "170", "5", "Mercador", "11" }
    };

    private static readonly string[][] SampleNpcs =
    {
        new[] { "110001", "Funcionária Kafra", "151", "29", "113", "", "4" },
        new[] { "110002", "Guarda", "155", "40", "105", "", "8" },
        new[] { "110003", "Ferreiro", "170", "60", "86", "", "15" }
    };

    private static readonly string[][] SampleMapMonsters =
    {
        new[] { "300001", "Poring", "102", "90", "1002", "Poring", "3" },
        new[] { "300002", "Lunático", "98", "84", "1063", "Lunático", "6" },
        new[] { "300003", "Fabre", "110", "95", "1007", "Fabre", "7" }
    };

    private (List<string> Columns, List<string[]> Rows) PreviewData(Widget list)
    {
        switch (list.Source)
        {
            case DataSource.Items:
                _itemRows ??= _items.Items.Count > 0 ? _items.Items.Select(i => new[] { i.Id.ToString(), i.Name }).ToList() : SampleItems.ToList();
                return (new() { "id", "nome" }, _itemRows);
            case DataSource.Monsters:
                return (new() { "id", "nome", "mapas", "sprite" }, SampleMonsters.ToList());
            case DataSource.Maps:
                return (new() { "nome" }, SampleMaps.Select(m => new[] { m }).ToList());
            case DataSource.Fixed:
            {
                var (columns, rows) = CodeGenerator.FixedRows(list.Data);
                return (columns.Split('\t').ToList(), rows.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(r => r.Split('\t')).ToList());
            }
            case DataSource.MapPlayers:
                return (MapColumns, SamplePlayers.ToList());
            case DataSource.MapNpcs:
                return (MapColumns, SampleNpcs.ToList());
            case DataSource.MapMonsters:
                return (MapColumns, SampleMapMonsters.ToList());
            default:
                return (new(), new());
        }
    }

    private static string Fill(string text, List<string> columns, string[] row)
    {
        for (int i = 0; i < columns.Count && i < row.Length; i++)
            text = text.Replace("{" + columns[i] + "}", row[i]);
        return text;
    }

    // "{lista.total}" / "{lista.all}" nos textos: quantos registros a lista tem na previa
    private string Counts(string text) => _window == null || !text.Contains('{')
        ? text
        : System.Text.RegularExpressions.Regex.Replace(text, @"\{([^{}.]+)\.(total|all)\}", m =>
            _window.Find(m.Groups[1].Value) is { Type: WidgetType.DataList } list ? PreviewData(list).Rows.Count.ToString() : m.Value);

    // ---------------------------------------------------------------------------------------------
    // Desenho
    // ---------------------------------------------------------------------------------------------

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (_window == null)
            return;
        var g = e.Graphics;
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        g.TextRenderingHint = TextRenderingHint.SingleBitPerPixelGridFit;
        g.ScaleTransform(_zoom, _zoom);
        g.TranslateTransform(Pad, Pad);

        DrawFrame(g, _window);
        g.SetClip(new Rectangle(0, 0, _window.Width, _window.Height));
        if (Grid > 0 && Grid * _zoom >= 8) // grade no corpo, so quando da para ver
        {
            using var gridPen = new Pen(Color.FromArgb(0x24, 0x30, 0x90, 0xFF), 1f / _zoom);
            for (int x = Grid; x < _window.Width; x += Grid)
                g.DrawLine(gridPen, x, TitleHeight, x, _window.Height);
            for (int y = (TitleHeight / Grid + 1) * Grid; y < _window.Height; y += Grid)
                g.DrawLine(gridPen, 0, y, _window.Width, y);
        }
        // Areas antes, como o host: os widgets delas ficam por cima, cortados na parte visivel
        foreach (var area in _window.Widgets.Where(w => w.Type == WidgetType.ScrollArea))
            DrawArea(g, area);
        foreach (var widget in _window.Widgets.Where(w => w.Type != WidgetType.ScrollArea))
        {
            if (widget.Type == WidgetType.DataList)
                DrawDataList(g, widget);
            else if (ContainerOf(widget) is not { } container)
                DrawWidget(g, widget);
            else if (Shown(widget) || widget == _selected || container == _editing) // aberto ou selecionado: aparece cortado
            {
                var state = g.Save();
                g.IntersectClip(ViewOf(container));
                DrawWidget(g, widget);
                g.Restore(state);
            }
        }
        g.ResetClip();

        using (var guide = new Pen(GuideColor, 1f / _zoom))
            foreach (var (vertical, at) in _guides)
                if (vertical)
                    g.DrawLine(guide, at, -Pad, at, _window.Height + Pad);
                else
                    g.DrawLine(guide, -Pad, at, _window.Width + Pad, at);

        if (_editing != null) // conteiner aberto: o resto da janela escurece
        {
            using var outside = new Region(new Rectangle(-Pad, -Pad, _window.Width + 2 * Pad, _window.Height + 2 * Pad));
            outside.Exclude(WidgetBounds(_editing));
            using (var dim = new SolidBrush(Color.FromArgb(0x70, 0x20, 0x22, 0x28)))
                g.FillRegion(dim, outside);
            var r = WidgetBounds(_editing);
            using var edge = new Pen(Color.DodgerBlue, 2f / _zoom);
            g.DrawRectangle(edge, r.X - 1, r.Y - 1, r.Width + 1, r.Height + 1);
            if (_editing.Type == WidgetType.DataList) // a linha do modelo
            {
                var row = ViewOf(_editing);
                using var model = new Pen(Color.DodgerBlue, 1f / _zoom) { DashStyle = DashStyle.Dot };
                g.DrawRectangle(model, row.X, row.Y, row.Width - 1, row.Height - 1);
            }
        }

        using var pen = new Pen(Color.FromArgb(0x30, 0x90, 0xFF), 1f / _zoom) { DashStyle = DashStyle.Dash };
        if (_selected != null)
        {
            var r = WidgetBounds(_selected);
            g.DrawRectangle(pen, r.X - 0.5f, r.Y - 0.5f, r.Width, r.Height);
            if (Sizable(_selected))
                g.FillRectangle(Brushes.DodgerBlue, r.Right - 3, r.Bottom - 3, 4, 4);
        }
        g.FillRectangle(Brushes.DodgerBlue, _window.Width - 2, _window.Height - 2, 4, 4); // redimensionar a janela
    }

    private void DrawScrollBar(Graphics g, Rectangle bar, int page, int total, int first)
    {
        Fill(g, bar.X, bar.Y, bar.Width, bar.Height, 0xEEEEEE);
        if (total <= page)
            return;
        int thumb = Math.Max(10, bar.Height * page / total);
        int top = bar.Y + (bar.Height - thumb) * first / Math.Max(1, total - page);
        Fill(g, bar.X + 3, top, bar.Width - 6, thumb, 0xB4B9C6);
    }

    private void DrawArea(Graphics g, Widget area)
    {
        var r = WidgetBounds(area);
        using (var pen = new Pen(Border))
            g.DrawRectangle(pen, r.X, r.Y, r.Width - 1, r.Height - 1);
        // Barra so quando o conteudo passa da area (o host esconde a nativa)
        int view = r.Height - 2, content = ContentOf(area);
        if (content > view)
            DrawScrollBar(g, new Rectangle(r.Right - 1 - ScrollBarWidth, r.Y + 1, ScrollBarWidth, view), view, content, ScrollOf(area));
    }

    // Lista de dados: as celulas repetidas para os registros da previa (aberta: a 1a linha é o modelo editavel)
    private void DrawDataList(Graphics g, Widget list)
    {
        var (columns, rows) = PreviewData(list);
        int visible = VisibleRows(list), first = ScrollOf(list);
        var cells = _window!.Widgets.Where(w => w.Parent == list.Id).ToList();
        var state = g.Save();
        g.IntersectClip(new Rectangle(list.X, list.Y, list.Width - ScrollBarWidth, list.Height));
        for (int r = 0; r < visible && first + r < rows.Count; r++)
        {
            if (r == 0 && list == _editing)
                continue;
            var record = rows[first + r];
            foreach (var cell in cells)
            {
                var bounds = WidgetBounds(cell);
                bounds.Offset(0, r * list.RowHeight);
                DrawWidget(g, cell, bounds, text => Fill(text, columns, record));
            }
        }
        if (rows.Count == 0)
            PaintText(g, list.Source == DataSource.Code ? "(preenchida pelo código: gk.rows)" : "(sem registros)", list.X + 6, list.Y + 6, Hint);
        g.Restore(state);
        DrawScrollBar(g, new Rectangle(list.X + list.Width - ScrollBarWidth, list.Y, ScrollBarWidth, list.Height), visible, rows.Count, first);
    }

    private static void Fill(Graphics g, int x, int y, int w, int h, int rgb)
    {
        using var brush = new SolidBrush(Color.FromArgb(rgb | unchecked((int)0xFF000000)));
        g.FillRectangle(brush, x, y, w, h);
    }

    private void PaintText(Graphics g, string text, int x, int y, Color color, bool bold = false)
    {
        using var brush = new SolidBrush(color);
        g.DrawString(text, bold ? _bold : _font, brush, x, y, StringFormat.GenericTypographic);
    }

    // Corta com "..." ate caber na largura (como o fit do host)
    private string FitText(string text, int width, bool bold)
    {
        if (TextWidth(text, bold) <= width)
            return text;
        while (text.Length > 0 && TextWidth(text + "...", bold) > width)
            text = text[..^1];
        return text + "...";
    }

    private void DrawImage(Graphics g, string path, int x, int y)
    {
        if (Texture(path) is { } image)
            g.DrawImage(image, x, y, image.Width, image.Height);
    }

    private void DrawFrame(Graphics g, WindowModel window)
    {
        int w = window.Width, h = window.Height;
        // Barra de titulo nativa: esquerda, meio repetido, direita
        var left = Texture(Ui + "basic_interface\\titlebar_left.bmp");
        var mid = Texture(Ui + "basic_interface\\titlebar_mid.bmp");
        var right = Texture(Ui + "basic_interface\\titlebar_right.bmp");
        if (left != null && mid != null && right != null)
        {
            for (int x = left.Width; x < w - right.Width; x += mid.Width)
                g.DrawImage(mid, x, 0, Math.Min(mid.Width, w - right.Width - x), mid.Height);
            g.DrawImage(left, 0, 0, left.Width, left.Height);
            g.DrawImage(right, w - right.Width, 0, right.Width, right.Height);
        }
        else
            g.FillRectangle(Brushes.LightSteelBlue, 0, 0, w, TitleHeight);
        g.DrawString(window.Title.Get(Language), _bold, Brushes.Black, 17, 3, StringFormat.GenericTypographic);
        DrawImage(g, Ui + "navigation_interface\\sys_close_a.bmp", w - 17, 4);

        g.FillRectangle(Brushes.White, 0, TitleHeight, w, h - TitleHeight);
        using var border = new SolidBrush(Border);
        g.FillRectangle(border, 0, TitleHeight, 1, h - TitleHeight);
        g.FillRectangle(border, w - 1, TitleHeight, 1, h - TitleHeight);
        g.FillRectangle(border, 0, h - 1, w, 1);
    }

    private void DrawWidget(Graphics g, Widget w) => DrawWidget(g, w, WidgetBounds(w), text => text);

    // fill troca "{coluna}" pelos valores do registro (celulas); fora das listas devolve o texto como esta
    private void DrawWidget(Graphics g, Widget w, Rectangle r, Func<string, string> fill)
    {
        string text = fill(w.Text.Get(Language));
        switch (w.Type)
        {
            case WidgetType.Label:
            {
                text = Counts(text);
                bool cellBox = IsCell(w) && w.Width > 0;
                if (cellBox && w.Fit)
                    text = FitText(text, w.Width, w.Bold);
                int x = cellBox && w.AlignRight ? r.Right - TextWidth(text, w.Bold) : r.X;
                PaintText(g, text, x, r.Y, Color.FromArgb(w.Color | unchecked((int)0xFF000000)), w.Bold);
                break;
            }
            case WidgetType.Button:
            {
                var left = Texture(Ui + "basic_interface\\btn_out_left.bmp");
                var mid = Texture(Ui + "basic_interface\\btn_out_mid.bmp");
                var right = Texture(Ui + "basic_interface\\btn_out_right.bmp");
                if (left != null && mid != null && right != null)
                {
                    g.DrawImage(mid, r.X + left.Width, r.Y, r.Width - left.Width - right.Width, mid.Height);
                    g.DrawImage(left, r.X, r.Y, left.Width, left.Height);
                    g.DrawImage(right, r.Right - right.Width, r.Y, right.Width, right.Height);
                }
                else
                    g.FillRectangle(Brushes.Gainsboro, r);
                PaintText(g, text, r.X + (r.Width - TextWidth(text, false)) / 2, r.Y + 4, Color.Black);
                break;
            }
            case WidgetType.ImageButton:
                if (Texture(fill(w.Image)) is { } normal)
                    g.DrawImage(normal, r.X, r.Y, normal.Width, normal.Height);
                else
                    g.DrawRectangle(Pens.Gray, r.X, r.Y, r.Width - 1, r.Height - 1);
                if (text != "") // legenda centralizada, como o botao da Agencia
                    PaintText(g, text, r.X + (r.Width - TextWidth(text, false)) / 2, r.Y + (r.Height - 12) / 2, Color.Black);
                break;
            case WidgetType.CheckBox:
                DrawImage(g, Ui + (w.Checked ? "gamesettingsui\\toggle_on.bmp" : "gamesettingsui\\toggle_off.bmp"), r.X, r.Y);
                break;
            case WidgetType.Edit:
                g.FillRectangle(Brushes.White, r);
                using (var pen = new Pen(Border))
                    g.DrawRectangle(pen, r.X, r.Y, r.Width - 1, r.Height - 1);
                PaintText(g, text, r.X + 2, r.Y + (r.Height - 12) / 2, Hint);
                break;
            case WidgetType.ListBox:
                Fill(g, r.X, r.Y, r.Width, r.Height, 0xF0F0F0);
                break;
            case WidgetType.Image:
                if (Texture(fill(w.Image)) is { } image)
                    g.DrawImage(image, r.X, r.Y, image.Width, image.Height);
                else
                    g.DrawRectangle(Pens.Gray, r.X, r.Y, r.Width - 1, r.Height - 1);
                break;
            case WidgetType.ItemIcon:
                if (int.TryParse(fill(w.Item).Trim(), out int item) && Items.IconPath(item) is { } icon && Texture(icon) is { } bitmap)
                    g.DrawImage(bitmap, r.X, r.Y, bitmap.Width, bitmap.Height);
                else
                {
                    using (var pen = new Pen(Color.Silver) { DashStyle = DashStyle.Dot })
                        g.DrawRectangle(pen, r.X, r.Y, r.Width - 1, r.Height - 1);
                    PaintText(g, w.Item == "" ? "?" : fill(w.Item), r.X + 2, r.Y + 6, Hint);
                }
                break;
            case WidgetType.Strip:
            {
                // Esquerda, meio repetido e direita (o meio que passa fica atras da direita, como o host)
                var left = Texture(w.Image);
                var mid = Texture(w.ImageMid);
                var right = Texture(w.ImageRight);
                int lw = left?.Width ?? 0, rw = right?.Width ?? 0;
                for (int x = r.X + lw; mid != null && mid.Width > 0 && x < r.Right - rw; x += mid.Width)
                    g.DrawImage(mid, Math.Min(x, r.Right - rw - mid.Width), r.Y, mid.Width, mid.Height);
                if (left != null)
                    g.DrawImage(left, r.X, r.Y, left.Width, left.Height);
                if (right != null)
                    g.DrawImage(right, r.Right - rw, r.Y, right.Width, right.Height);
                if (left == null && mid == null && right == null)
                    g.DrawRectangle(Pens.Gray, r.X, r.Y, r.Width - 1, r.Height - 1);
                break;
            }
            case WidgetType.Sprite:
                if (SpriteImage(w.Sprite) is { } sprite) // centralizado, apoiado na base da caixa (como o host)
                    g.DrawImage(sprite, r.X + (r.Width - sprite.Width) / 2, r.Bottom - sprite.Height, sprite.Width, sprite.Height);
                using (var pen = new Pen(Color.Silver) { DashStyle = DashStyle.Dot })
                    g.DrawRectangle(pen, r.X, r.Y, r.Width - 1, r.Height - 1);
                break;
            case WidgetType.Rect:
                Fill(g, r.X, r.Y, r.Width, r.Height, w.Color);
                break;
        }
    }

    // ---------------------------------------------------------------------------------------------
    // Mouse e teclado
    // ---------------------------------------------------------------------------------------------

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();
        if (_window == null || e.Button != MouseButtons.Left)
            return;
        var p = ToWindow(e.Location);
        _dragStart = p;
        _mouseDown = e.Location;
        _moved = false;

        if (Near(p, new Point(_window.Width, _window.Height)))
        {
            _drag = Drag.ResizeWindow;
            _dragOriginal = new Rectangle(0, 0, _window.Width, _window.Height);
            return;
        }
        if (_selected != null && Sizable(_selected) && Near(p, new Point(WidgetBounds(_selected).Right, WidgetBounds(_selected).Bottom)))
        {
            _drag = Drag.ResizeWidget;
            _dragOriginal = WidgetBounds(_selected);
            return;
        }

        if (_editing != null && !WidgetBounds(_editing).Contains(p))
            _editing = null; // clique fora do conteiner aberto: sai dele
        var hit = HitTest(p);
        Selected = hit;
        if (hit != null)
        {
            _drag = Drag.Move;
            _dragOriginal = WidgetBounds(hit);
        }
    }

    // Conteiner aberto: so os widgets dele (na parte visivel). Senao: o conteiner pega o clique inteiro, e o widget
    // solto ganha do conteiner por baixo dele
    private Widget? HitTest(Point p)
    {
        if (_editing is { } open)
            return ViewOf(open).Contains(p)
                ? _window!.Widgets.LastOrDefault(w => w.Parent == open.Id && Rectangle.Inflate(WidgetBounds(w), 2, 2).Contains(p))
                : null;
        return _window!.Widgets
            .Where(w => ContainerOf(w) == null && Rectangle.Inflate(WidgetBounds(w), 2, 2).Contains(p))
            .OrderBy(w => w.IsContainer ? 0 : 1)
            .LastOrDefault();
    }

    // Duplo clique numa area rolavel ou lista de dados: abre para editar o que tem dentro (Esc ou clique fora fecha)
    protected override void OnMouseDoubleClick(MouseEventArgs e)
    {
        base.OnMouseDoubleClick(e);
        if (_window == null || e.Button != MouseButtons.Left || _editing != null)
            return;
        var p = ToWindow(e.Location);
        if (HitTest(p) is not { IsContainer: true } container)
            return;
        _editing = container;
        _drag = Drag.None;
        _scroll.Remove(container); // a lista volta para o topo: o modelo é a 1a linha
        Selected = HitTest(p) ?? container;
        if (container.Type == WidgetType.DataList)
            Notice?.Invoke("Modelo da linha: o que estiver na 1ª linha se repete para cada registro. {coluna} nos textos vira o valor (ex.: {nome}, #{id}).");
    }

    private static bool Near(Point p, Point corner) => Math.Abs(p.X - corner.X) <= Grip && Math.Abs(p.Y - corner.Y) <= Grip;

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_window == null)
            return;
        var p = ToWindow(e.Location);
        if (_drag == Drag.None)
        {
            bool corner = Near(p, new Point(_window.Width, _window.Height))
                          || (_selected != null && Sizable(_selected) && Near(p, new Point(WidgetBounds(_selected).Right, WidgetBounds(_selected).Bottom)));
            Cursor = corner ? Cursors.SizeNWSE : Cursors.Default;
            return;
        }

        // Clique sem arrastar nao mexe (a grade puxaria o widget so de selecionar)
        if (!_moved && Math.Abs(e.X - _mouseDown.X) * 2 < SystemInformation.DragSize.Width && Math.Abs(e.Y - _mouseDown.Y) * 2 < SystemInformation.DragSize.Height)
            return;
        _moved = true;

        int dx = p.X - _dragStart.X, dy = p.Y - _dragStart.Y;
        var o = _dragOriginal;
        _guides.Clear();
        switch (_drag)
        {
            case Drag.Move when _selected != null:
            {
                // Borda esquerda, centro ou direita (topo, meio ou base) encosta na guia mais perto
                int x = Math.Clamp(Snap(o.X + dx, true, 0, o.Width / 2, o.Width), 0, Math.Max(0, _window.Width - 4));
                int y = Math.Clamp(Snap(o.Y + dy, false, 0, o.Height / 2, o.Height), 0, Math.Max(0, _window.Height - 4));
                // Entra na area rolavel sob o mouse e sai quando o mouse sai dela (celula fica na lista dela)
                if (!_selected.IsContainer && !IsCell(_selected))
                    _selected.Parent = _window.Widgets.LastOrDefault(a => a.Type == WidgetType.ScrollArea && ViewOf(a).Contains(p))?.Id ?? 0;
                var origin = Origin(_selected);
                _selected.X = Math.Max(0, x - origin.X);
                _selected.Y = Math.Max(0, y - origin.Y);
                break;
            }
            case Drag.ResizeWidget when _selected != null:
                _selected.Width = Math.Max(8, Snap(o.Right + dx, true, 0) - o.X);
                _selected.Height = Math.Max(8, Snap(o.Bottom + dy, false, 0) - o.Y);
                break;
            case Drag.ResizeWindow:
                _window.Width = Math.Max(80, OnGrid(o.Width + dx));
                _window.Height = Math.Max(40, OnGrid(o.Height + dy));
                UpdateSize();
                break;
        }
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        _guides.Clear();
        if (_editing != null && _selected != null && _selected != _editing && _selected.Parent != _editing.Id)
            _editing = null; // o widget saiu da area aberta
        if (_drag != Drag.None)
        {
            _drag = Drag.None;
            Invalidate();
            if (_moved)
                Changed?.Invoke();
        }
    }

    // ---------------------------------------------------------------------------------------------
    // Grade e guias
    // ---------------------------------------------------------------------------------------------

    private static bool Free => (ModifierKeys & Keys.Control) != 0;

    private int OnGrid(int value) => Grid > 0 && !Free ? (int)Math.Round((double)value / Grid) * Grid : value;

    // Bordas e centros da janela e dos outros widgets: x (vertical) ou y
    private IEnumerable<int> GuideLines(bool vertical)
    {
        var window = _window!;
        var lines = vertical ? new List<int> { 0, window.Width / 2, window.Width } : new List<int> { 0, TitleHeight, window.Height / 2, window.Height };
        foreach (var r in window.Widgets.Where(w => w != _selected && Shown(w)).Select(WidgetBounds))
            lines.AddRange(vertical ? new[] { r.Left, r.Left + r.Width / 2, r.Right } : new[] { r.Top, r.Top + r.Height / 2, r.Bottom });
        return lines;
    }

    /// <summary>
    /// Ajusta value para que value + algum offset caia numa guia perto (e marca a guia); sem guia, value vai para a grade.
    /// </summary>
    private int Snap(int value, bool vertical, params int[] offsets)
    {
        if (Free)
            return value;
        int tolerance = Math.Max(1, SnapPixels / _zoom), best = int.MaxValue, delta = 0, line = 0;
        foreach (int candidate in GuideLines(vertical))
            foreach (int offset in offsets)
                if (Math.Abs(candidate - (value + offset)) < best)
                    (best, delta, line) = (Math.Abs(candidate - (value + offset)), candidate - (value + offset), candidate);
        if (best > tolerance)
            return OnGrid(value);
        _guides.Add((vertical, line));
        return value + delta;
    }

    // Roda sobre uma area ou lista rola a previa dela; com Ctrl vai para o painel (zoom)
    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        if (_window == null || Free)
            return;
        var p = ToWindow(e.Location);
        var container = _window.Widgets.LastOrDefault(c => c.IsContainer && c != _editing && WidgetBounds(c).Contains(p));
        if (container == null || MaxScroll(container) == 0)
            return;
        int step = container.Type == WidgetType.DataList ? 3 : WheelStep; // lista: 3 linhas, como no jogo
        _scroll[container] = Math.Clamp(ScrollOf(container) - Math.Sign(e.Delta) * step, 0, MaxScroll(container));
        ((HandledMouseEventArgs)e).Handled = true;
        Invalidate();
    }

    protected override bool IsInputKey(Keys keyData) =>
        (keyData & Keys.KeyCode) is Keys.Left or Keys.Right or Keys.Up or Keys.Down || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode == Keys.Escape && _editing != null)
        {
            Selected = _editing;
            _editing = null;
            return;
        }
        if (_window == null || _selected == null)
            return;
        int step = e.Shift ? 8 : 1;
        switch (e.KeyCode)
        {
            case Keys.Delete:
                // Conteiner leva junto o que tem dentro
                var gone = _selected;
                _window.Widgets.RemoveAll(w => w == gone || (gone.IsContainer && w.Parent == gone.Id));
                if (gone == _editing)
                    _editing = null;
                Selected = null;
                break;
            case Keys.Left: _selected.X = Math.Max(0, _selected.X - step); break;
            case Keys.Right: _selected.X += step; break;
            case Keys.Up: _selected.Y = Math.Max(0, _selected.Y - step); break;
            case Keys.Down: _selected.Y += step; break;
            default: return;
        }
        e.Handled = true;
        Invalidate();
        Changed?.Invoke();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _font.Dispose();
            _bold.Dispose();
            _measure.Dispose();
        }
        base.Dispose(disposing);
    }
}
