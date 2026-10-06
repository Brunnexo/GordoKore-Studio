using System.Drawing;
using GordoKore.Studio.Generation;
using GordoKore.Studio.Grf;
using GordoKore.Studio.Model;

namespace GordoKore.Studio;

/// <summary>
/// GordoKore.Studio.exe --check [pasta do jogo]: literais C, geracao do codigo e, com o jogo e o MinGW na
/// maquina, leitura da GRF e compilacao de um modulo de verdade. Saida 0 = ok.
/// </summary>
internal static class Checks
{
    private static void Expect(bool condition, string what)
    {
        if (!condition)
            throw new InvalidOperationException("falhou: " + what);
        Console.WriteLine("ok: " + what);
    }

    public static int Run(string? gameDir)
    {
        try
        {
            Literals();
            string dir = Path.Combine(Path.GetTempPath(), "gordokore_studio_check");
            if (Directory.Exists(dir))
                Directory.Delete(dir, true);
            var settings = new Settings();
            if (gameDir != null)
                settings.GameDir = gameDir;

            using var store = Directory.Exists(settings.GameDir) ? new ResourceStore(settings.GameDir) : null;
            if (store != null)
            {
                Expect(Images.Load(store.Read(ResourceStore.TextureDir + ResourceStore.UiDir + "win_msgbox.bmp"), "x.bmp") is { Width: 280 }, "GRF: win_msgbox.bmp 280 px");
                Expect(Sprite.Load(store, "poring") != null, "GRF: sprite poring (SPR/ACT com DES)");
            }

            var project = SampleProject();
            CodeGenerator.Generate(project, dir, path => store == null ? new Bitmap(10, 10) : Images.Load(store.Read(ResourceStore.TextureDir + path), path));
            string source = File.ReadAllText(Path.Combine(dir, "module.gen.cpp"));
            Expect(source.Contains("host->bind(g_windows[0], 2, GK_ACTION_COMMAND, \"ai manual\");"), "acao de comando");
            Expect(source.Contains("GK_ACTION_LUA, \"contar\""), "acao Lua");
            Expect(source.Contains("{\"Op\\xE7\\xE3o\", nullptr, \"Option\"}"), "texto em CP1252");
            Expect(source.Contains("kIcon0[]"), "icone do menu");
            int area = source.IndexOf("g_host->add_scroll_area(win, 11, 10, 200, 160, 60);", StringComparison.Ordinal);
            int child = source.IndexOf("g_host->add_label(win, 12, 4, 200,", StringComparison.Ordinal);
            int parent = source.IndexOf("g_host->set_parent(win, 12, 11);", StringComparison.Ordinal);
            Expect(area >= 0 && area < child && child < parent, "area rolavel antes dos widgets e set_parent depois");

            // Janela de itens montada so com pecas genericas (modelo)
            var items = project.Windows[1];
            var list = items.Find("itens")!;
            Expect(CodeGenerator.Validate(project).Count == 0, "modelos e pecas sem problemas de validacao");
            Expect(source.Contains($"g_host->add_data_list(win, {list.Id}, 0, 46, 380, 192, 32);"), "lista de dados");
            Expect(source.Contains($"g_host->set_source(win, {list.Id}, \"itens\");") &&
                   source.Contains($"g_host->set_filter(win, {list.Id}, {items.Find("busca")!.Id}, \"nome\");"), "fonte e filtro da lista");
            Expect(source.Contains("GK_CELL_ITEM_ICON") && source.Contains("\"{nome}\"") && source.Contains("GK_CELL_TEXT, 359, 12,"), "celulas: icone, nome e ID alinhado pela borda direita");
            Expect(source.Contains($"host->bind_event(g_windows[1], {list.Id}, GK_EVENT_CLICK, GK_ACTION_DRAG_ITEM") &&
                   source.Contains($"host->bind_event(g_windows[1], {list.Id}, GK_EVENT_RIGHT_CLICK, GK_ACTION_ITEM_INFO"), "clique arrasta, clique direito mostra a descricao");
            Expect(source.Contains($"GK_ACTION_CLEAR, \"{items.Find("busca")!.Id}\""), "limpar campo pelo id");
            Expect(source.Contains("g_host->add_caption_button(") && source.Contains("g_host->add_strip("), "botao com legenda e faixa");
            Expect(source.Contains("host->set_resizable(g_windows[1], 232, 163, 480, 515, 32);") && source.Contains("g_host->set_anchor(win, " + list.Id + ", 15);"), "janela redimensionavel e ancoras");
            Expect(source.Contains("{\"{" + list.Id + ".total} itens\""), "contagem da lista no texto pelo id");
            Expect(source.Contains("g_host->add_item_icon(win, 1, 10, 30, 501);"), "icone de item");
            Expect(source.Contains("g_host->set_rows(win, 2, \"nome\\x09x\\x09y\", \"prontera\\x09\" \"150\\x09\" \"180\\ngeffen\\x09\" \"119\\x09\" \"59\\n\");") ||
                   source.Contains("g_host->set_rows(win, 2,"), "lista fixa");
            Expect(CodeGenerator.FixedRows("nome;x\r\n a ; 1 \r\n\r\nb;2") == ("nome\tx", "a\t1\nb\t2\n"), "lista fixa no formato do host");
            Expect(source.Contains("host->bind(g_windows[2], 4, GK_ACTION_COMMAND, \"move {nome}\");"), "botao da linha com {coluna} no comando");
            var broken = new Project { Windows = { new WindowModel { Widgets = { new Widget { Type = WidgetType.DataList, Id = 1, FilterField = "nada" },
                new Widget { Type = WidgetType.Edit, Id = 2, Parent = 1 } } } } };
            Expect(CodeGenerator.Validate(broken).Count == 2, "validacao: filtro sem campo e campo de texto dentro da lista");
            Expect(source.Contains("host->bind(g_windows[2], 5, GK_ACTION_HIDE, \"jogadores\");") &&
                   source.Contains("host->bind(g_windows[2], 6, GK_ACTION_HIDE, \"jogadores\");"), "esconder no mapa (vazio = jogadores)");
            var unknownPart = new Project { Windows = { new WindowModel { Widgets = { new Widget { Type = WidgetType.Button, Id = 1, Action = ActionKind.Hide, ActionValue = "monstros" } } } } };
            Expect(CodeGenerator.Validate(unknownPart).Count == 1, "validacao: parte do mapa desconhecida");
            Expect(source.Contains("g_host->set_source(win, 7, \"jogadores_no_mapa\");"), "lista ao vivo dos jogadores do mapa");
            Expect(source.Contains("host->bind_event(g_windows[2], 0, GK_EVENT_OPEN, GK_ACTION_LUA, \"contar\");"), "Lua ao abrir a janela");
            var openWithoutLua = new Project { Windows = { new WindowModel { OnOpen = "preencher" } } };
            Expect(CodeGenerator.Validate(openWithoutLua).Count == 1, "validacao: ao abrir sem Script Lua");
            // Exemplos do repositorio continuam abrindo e validos (a pasta exemplos fica em algum pai do exe)
            var up = new DirectoryInfo(AppContext.BaseDirectory);
            while (up != null && !Directory.Exists(Path.Combine(up.FullName, "exemplos")))
                up = up.Parent;
            string examples = up == null ? "" : Path.Combine(up.FullName, "exemplos");
            if (Directory.Exists(examples))
                foreach (string file in Directory.GetFiles(examples, "*.gkproj"))
                    Expect(CodeGenerator.Validate(Project.Load(file)).Count == 0, "exemplo valido: " + Path.GetFileName(file));
            Templates.ItemsWindow(items); // aplicar de novo troca a janela inteira
            Expect(items.Widgets.Count == project.Windows[1].Widgets.Count && items.Widgets.Select(w => w.Name).Distinct().Count() == items.Widgets.Count, "modelo aplicado de novo, nomes unicos");
            project.Save(Path.Combine(dir, "itens_modelo.gkproj"));

            File.WriteAllText(Path.Combine(dir, "user.cpp"), "#include \"module.gen.h\"\nint user_init(const GkHost *) { return 7; }\nvoid user_event(int, GkWin, int, int, int) {}\n");
            CodeGenerator.Generate(project, dir, _ => null);
            Expect(File.ReadAllText(Path.Combine(dir, "user.cpp")).Contains("return 7"), "user.cpp fica como estava ao gerar de novo");

            // O estudio passa o icone do cache de texturas: gerar de novo usa o mesmo bitmap
            using (var cached = new Bitmap(10, 10))
            {
                CodeGenerator.Generate(project, dir, _ => cached);
                CodeGenerator.Generate(project, dir, _ => cached);
                Expect(cached.Width == 10, "gerar duas vezes nao descarta o icone do cache");
            }

            using var icon = new Bitmap(60, 20);
            Expect(CodeGenerator.MenuIcon(icon).Length == 54 + 132 * 43, "icone BMP 43x43 24 bits");

            // Grade mostra o nome em portugues, o .gkproj continua com o nome do enum
            var kinds = System.ComponentModel.TypeDescriptor.GetConverter(typeof(ActionKind));
            Expect(kinds.ConvertToString(ActionKind.Command) == "Comando do OpenKore" && (ActionKind?)kinds.ConvertFromString("Lua (no jogo)") == ActionKind.Lua, "acao com nome em portugues na grade");
            string saved = Path.Combine(dir, "acao.gkproj");
            project.Save(saved);
            Expect(File.ReadAllText(saved).Contains("\"Action\": \"Command\"") && Project.Load(saved).Windows[0].Widgets.Any(w => w.Action == ActionKind.Lua), "acao no .gkproj pelo nome do enum");

            // Historico: igual nao conta, digitacao seguida é um passo, edicao nova apaga o refazer, limite
            var history = new History(limit: 2);
            history.Reset("a");
            history.Push("a");
            history.Push("b");
            history.Push("bc", merge: true);
            history.Push("bcd", merge: true);
            Expect(history.Undo() == "b" && history.Undo() == "a" && history.Undo() == null && history.Redo() == "b", "historico: desfazer, refazer e digitacao junta");
            history.Push("x");
            Expect(!history.CanRedo && history.Undo() == "b" && history.Undo() == "a", "historico: edicao nova apaga o refazer");
            history.Push("1");
            history.Push("2");
            history.Push("3");
            Expect(history.Undo() == "2" && history.Undo() == "1" && !history.CanUndo, "historico: limite de passos");
            Expect(Project.FromJson(project.ToJson()).ToJson() == project.ToJson(), "historico: foto do projeto volta igual");
            Expect(project.ToJson().Contains("Opção") && project.ToJson().Contains("유저인터페이스"), ".gkproj com acentos e coreano legiveis");

            if (File.Exists(Path.Combine(settings.MinGwDir, "i686-w64-mingw32-g++.exe")))
            {
                string? dll = Builder.BuildAsync(settings, dir, project.Name, line => Console.WriteLine("  " + line)).GetAwaiter().GetResult();
                Expect(dll != null && File.Exists(dll), "compilou o modulo com MinGW");
            }
            Console.WriteLine("tudo ok");
            return 0;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return 1;
        }
    }

    private static void Literals()
    {
        Expect(CodeGenerator.CString(new byte[] { (byte)'a', 0xE9, (byte)'b', (byte)'g' }) == "\"a\\xE9\" \"bg\"", "hex seguido de digito hex fecha a string");
        Expect(CodeGenerator.CString(new byte[] { 0xE9, (byte)'x' }) == "\"\\xE9x\"", "hex seguido de letra comum");
        Expect(CodeGenerator.CString("q\"r\\s\n"u8.ToArray()) == "\"q\\\"r\\\\s\\n\"", "aspas, barra e quebra de linha");
        Expect(CodeGenerator.GrfPath("유저인터페이스\\a.bmp").StartsWith("\"\\xC0\\xAF\\xC0\\xFA"), "caminho em CP949");
        Expect(CodeGenerator.Identifier("9 mod-x") == "_9_mod_x", "identificador C");
    }

    private static Project SampleProject() => new()
    {
        Name = "studio_check",
        Lua = "function contar(id, event, value)\n  gk.set(3, \"ok\")\nend\n",
        Windows =
        {
            new WindowModel
            {
                Key = "teste",
                MenuIcon = "유저인터페이스\\navigation_interface\\sys_close_a.bmp",
                Widgets =
                {
                    new Widget { Type = WidgetType.Label, Id = 1, X = 10, Y = 30, Text = new Texts("Op\u00e7\u00e3o", "", "Option") },
                    new Widget { Type = WidgetType.Button, Id = 2, X = 10, Y = 50, Width = 80, Text = new Texts("IA"), Action = ActionKind.Command, ActionValue = "ai manual" },
                    new Widget { Type = WidgetType.Button, Id = 3, X = 100, Y = 50, Width = 80, Text = new Texts("Contar"), Action = ActionKind.Lua, ActionValue = "contar" },
                    new Widget { Type = WidgetType.ImageButton, Id = 4, X = 10, Y = 80, Width = 11, Height = 11, Image = "유저인터페이스\\sys_close_a.bmp" },
                    new Widget { Type = WidgetType.CheckBox, Id = 5, X = 10, Y = 100 },
                    new Widget { Type = WidgetType.Edit, Id = 6, X = 50, Y = 100, Width = 100, Height = 18, Text = new Texts("Digite") },
                    new Widget { Type = WidgetType.ListBox, Id = 7, X = 10, Y = 120, Width = 100, Height = 40 },
                    new Widget { Type = WidgetType.Image, Id = 8, X = 120, Y = 120, Image = "유저인터페이스\\sys_close_b.bmp" },
                    new Widget { Type = WidgetType.Sprite, Id = 9, X = 150, Y = 120, Width = 60, Height = 60, Sprite = "poring" },
                    new Widget { Type = WidgetType.Rect, Id = 10, X = 0, Y = 190, Width = 240, Height = 1, Color = 0x9CA3B4 },
                    new Widget { Type = WidgetType.Label, Id = 12, Parent = 11, X = 4, Y = 200, Text = new Texts("Fundo") }, // antes da area na lista
                    new Widget { Type = WidgetType.ScrollArea, Id = 11, X = 10, Y = 200, Width = 160, Height = 60 }
                }
            },
            ItemsWindow(),
            new WindowModel
            {
                Key = "pecas",
                OnOpen = "contar",
                Widgets =
                {
                    new Widget { Type = WidgetType.ItemIcon, Id = 1, X = 10, Y = 30, Item = "501" },
                    new Widget { Type = WidgetType.DataList, Id = 2, Name = "mapas", X = 10, Y = 60, Width = 200, Height = 64, RowHeight = 16, Source = DataSource.Fixed,
                                 Data = "nome;x;y\r\nprontera;150;180\r\ngeffen;119;59\r\n", Action = ActionKind.Command, ActionValue = "move {nome} {x} {y}",
                                 DoubleAction = ActionKind.Lua, DoubleValue = "contar" },
                    new Widget { Type = WidgetType.Label, Id = 3, Parent = 2, X = 4, Y = 2, Text = new Texts("{nome} ({x}, {y})") },
                    new Widget { Type = WidgetType.Button, Id = 4, Parent = 2, X = 150, Y = 0, Width = 40, Text = new Texts("Ir"), Action = ActionKind.Command, ActionValue = "move {nome}" },
                    new Widget { Type = WidgetType.Button, Id = 5, X = 10, Y = 130, Width = 80, Text = new Texts("Esconder"), Action = ActionKind.Hide },
                    new Widget { Type = WidgetType.CheckBox, Id = 6, X = 100, Y = 132, Action = ActionKind.Hide, ActionValue = "jogadores" },
                    new Widget { Type = WidgetType.DataList, Id = 7, Name = "perto", X = 10, Y = 150, Width = 200, Height = 64, RowHeight = 16, Source = DataSource.MapPlayers },
                    new Widget { Type = WidgetType.Label, Id = 8, Parent = 7, X = 4, Y = 2, Text = new Texts("{nome} ({distancia})") }
                }
            }
        }
    };

    // A "Janela de itens do jogo" dos modelos (so pecas genericas)
    public static WindowModel ItemsWindow()
    {
        var window = new WindowModel { Key = "itens", MenuLabel = new Texts("Itens do jogo", "Objetos del juego", "Game items") };
        Templates.ItemsWindow(window);
        return window;
    }
}
