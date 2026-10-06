namespace GordoKore.Studio.Model;

/// <summary>
/// Modelos prontos da caixa de ferramentas: grupos de widgets comuns montados so com as pecas genericas (retangulos,
/// campo, botao com legenda, lista de dados, faixa). Depois de inseridos, tudo é editavel como qualquer widget.
/// </summary>
public static class Templates
{
    private const string Ui = "유저인터페이스\\";
    private const string Agency = Ui + "uiadventureragency\\btn_initialize";
    private const int StripeA = 0x767676, StripeB = 0x7F8291, Dark = 0x434343, FieldBorder = 0xD2D2D2, White = 0xFFFFFF;
    private const int Title = 17, BarHeight = 29, Footer = 21;

    public static readonly (string Label, string Tip, Action<WindowModel> Apply)[] All =
    {
        ("Barra de busca", "Faixa listrada da Agência de Aventura com campo \"Pesquise aqui\" e botão \"Limpar\", logo abaixo do título.",
            window => SearchBar(window, Title)),
        ("Rodapé com contagem", "Barra de botões do cliente no rodapé e \"N itens\" da primeira lista de dados da janela.",
            window => FooterBar(window, window.Widgets.FirstOrDefault(w => w.Type == WidgetType.DataList)?.Name)),
        ("Janela de itens do jogo", "A \"Itens do jogo\" do GordoKore inteira: busca, todos os itens com ícone, Drops e ID, arrastar, descrição, rodapé e redimensionável. Substitui os widgets da janela.",
            ItemsWindow)
    };

    /// <summary>Faixa de busca (29 px) em y = top. Devolve o nome do campo de texto.</summary>
    public static string SearchBar(WindowModel window, int top)
    {
        int width = window.Width;
        for (int i = 0; i < (BarHeight - 1) / 2; i++)
            Add(window, WidgetType.Rect, "faixa", 1, top + 2 * i, width - 2, 2, Anchor.Top, w => w.Color = i % 2 == 1 ? StripeB : StripeA);
        Add(window, WidgetType.Rect, "buscaEsquerda", 0, top, 1, BarHeight, Anchor.TopLeft, w => w.Color = Dark);
        Add(window, WidgetType.Rect, "buscaDireita", width - 1, top, 1, BarHeight, Anchor.TopRight, w => w.Color = Dark);
        Add(window, WidgetType.Rect, "buscaBase", 0, top + BarHeight - 1, width, 1, Anchor.Top, w => w.Color = Dark);

        // Campo branco com borda clara e sombra; "Limpar" na direita sobre o "poco" escuro (medidas da SearchBar)
        int reset = width - 8 - 44 - 1, right = reset - 6, field = top + 7;
        Add(window, WidgetType.Rect, "campoTopo", 8, field - 1, right - 8, 1, Anchor.Top, w => w.Color = FieldBorder);
        Add(window, WidgetType.Rect, "campoEsquerda", 8, field, 1, 14, Anchor.TopLeft, w => w.Color = FieldBorder);
        Add(window, WidgetType.Rect, "campoFundo", 9, field, right - 9, 14, Anchor.Top, w => w.Color = White);
        Add(window, WidgetType.Rect, "campoBase", 8, field + 14, right - 8, 1, Anchor.Top, w => w.Color = FieldBorder);
        Add(window, WidgetType.Rect, "campoSombra", 8, field + 15, right - 8, 1, Anchor.Top, w => w.Color = Dark);
        Add(window, WidgetType.Rect, "poco", reset, field, 45, 17, Anchor.TopRight, w => w.Color = Dark);
        var edit = Add(window, WidgetType.Edit, "busca", 10, field, right - 10, 14, Anchor.Top, w =>
        {
            w.MaxChars = 40;
            w.Text = new Texts("Pesquise aqui", "Busque aquí", "Search here");
        });
        Add(window, WidgetType.ImageButton, "limpar", reset, top + 5, 44, 18, Anchor.TopRight, w =>
        {
            (w.Image, w.ImageHover, w.ImagePress) = (Agency + ".bmp", Agency + "_over.bmp", Agency + "_press.bmp");
            w.Text = new Texts("Limpar", "Limpiar", "Clear");
            (w.Action, w.ActionValue) = (ActionKind.ClearField, edit.Name);
        });
        return edit.Name;
    }

    /// <summary>Barra de botoes do cliente (estilo 2) no rodape e a contagem da lista.</summary>
    public static void FooterBar(WindowModel window, string? list)
    {
        Add(window, WidgetType.Strip, "rodape", 0, window.Height - 29, window.Width, 0, Anchor.Bottom, w =>
            (w.Image, w.ImageMid, w.ImageRight) = (Ui + "basic_interface\\btnbar_left2.bmp", Ui + "basic_interface\\btnbar_mid2.bmp", Ui + "basic_interface\\btnbar_right2.bmp"));
        string count = list == null ? "0" : "{" + list + ".total}";
        Add(window, WidgetType.Label, "total", 12, window.Height - Footer + 2, 0, 0, Anchor.BottomLeft, w =>
            w.Text = new Texts(count + " itens", count + " objetos", count + " items"));
    }

    /// <summary>A "Itens do jogo" do GordoKore, so com pecas genericas (medidas da ItemBrowserWindow do Kore-Bridge).</summary>
    public static void ItemsWindow(WindowModel window)
    {
        window.Widgets.Clear();
        (window.Width, window.Height) = (380, 259); // 6 linhas de 32 px
        (window.Resizable, window.MinWidth, window.MaxWidth, window.MinHeight, window.MaxHeight, window.HeightStep) = (true, 232, 480, 163, 515, 32);
        window.Title = new Texts("Itens disponíveis", "Objetos disponibles", "Available items");

        string search = SearchBar(window, Title);
        int top = Title + BarHeight, rowWidth = window.Width - 15;
        var list = Add(window, WidgetType.DataList, "itens", 0, top, window.Width, window.Height - top - Footer, Anchor.Fill, w =>
        {
            (w.Source, w.RowHeight, w.FilterField, w.FilterColumns) = (DataSource.Items, 32, search, "nome");
            (w.Action, w.RightAction) = (ActionKind.DragItem, ActionKind.ItemInfo);
        });
        Add(window, WidgetType.ItemIcon, "icone", 12, 4, 0, 0, Anchor.TopLeft, w => w.Item = "{id}", list);
        Add(window, WidgetType.Label, "nome", 44, 10, rowWidth - 6 - 50 - 44 - 44 - 6, 0, Anchor.Top, w =>
            (w.Text, w.Fit) = (new Texts("{nome}"), true), list);
        Add(window, WidgetType.ImageButton, "drops", rowWidth - 6 - 50 - 44, 7, 44, 18, Anchor.TopRight, w =>
        {
            (w.Image, w.ImageHover, w.ImagePress) = (Agency + ".bmp", Agency + "_over.bmp", Agency + "_press.bmp");
            (w.Text, w.Action, w.DropsOnly) = (new Texts("Drops"), ActionKind.ItemDrops, true);
        }, list);
        Add(window, WidgetType.Label, "id", rowWidth - 6 - 50, 12, 50, 0, Anchor.TopRight, w =>
            (w.Text, w.AlignRight) = (new Texts("#{id}"), true), list);

        FooterBar(window, list.Name);
    }

    private static Widget Add(WindowModel window, WidgetType type, string name, int x, int y, int width, int height, Anchor anchor,
        Action<Widget> setup, Widget? parent = null)
    {
        var widget = new Widget
        {
            Type = type,
            Id = window.Widgets.Count == 0 ? 1 : window.Widgets.Max(w => w.Id) + 1,
            Name = UniqueName(window, name),
            X = x,
            Y = y,
            Width = width,
            Height = height,
            Anchor = anchor,
            Parent = parent?.Id ?? 0
        };
        setup(widget);
        window.Widgets.Add(widget);
        return widget;
    }

    public static string UniqueName(WindowModel window, string name)
    {
        if (window.Find(name) == null)
            return name;
        for (int i = 2; ; i++)
            if (window.Find(name + i) == null)
                return name + i;
    }
}
