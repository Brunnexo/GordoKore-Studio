using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
using System.Text;
using GordoKore.Studio.Grf;
using GordoKore.Studio.Model;

namespace GordoKore.Studio.Generation;

/// <summary>
/// Projeto do modulo em C++: module.gen.cpp/.h (sempre reescritos), user.cpp (criado uma vez, do usuario),
/// CMakeLists.txt e gordokore_sdk.h. Textos em CP1252 e caminhos da GRF em CP949, como o cliente le.
/// </summary>
public static class CodeGenerator
{
    public const int IconSize = 43;

    private static readonly Encoding Cp1252 = Encoding.GetEncoding(1252);

    public static void Generate(Project project, string outDir, Func<string, Bitmap?> loadImage)
    {
        Directory.CreateDirectory(outDir);
        string name = Identifier(project.Name);
        File.WriteAllText(Path.Combine(outDir, "gordokore_sdk.h"), SdkHeader());
        File.WriteAllText(Path.Combine(outDir, "CMakeLists.txt"), CMakeLists(name));
        File.WriteAllText(Path.Combine(outDir, "module.gen.h"), Header(project));
        File.WriteAllText(Path.Combine(outDir, "module.gen.cpp"), Source(project, name, loadImage));
        string user = Path.Combine(outDir, "user.cpp");
        if (!File.Exists(user))
            File.WriteAllText(user, UserSource());
    }

    public static string SdkHeader()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("gordokore_sdk.h")!;
        return new StreamReader(stream).ReadToEnd();
    }

    // ---------------------------------------------------------------------------------------------
    // Literais C
    // ---------------------------------------------------------------------------------------------

    /// <summary>Bytes como literal C: ASCII visivel direto, \" \\ \n, o resto em \xNN (fechando a string antes de um digito hex).</summary>
    public static string CString(byte[] bytes)
    {
        var sb = new StringBuilder("\"");
        bool afterHex = false;
        foreach (byte b in bytes)
        {
            if (b >= 0x20 && b < 0x7F && b != '"' && b != '\\' && b != '?')
            {
                if (afterHex && Uri.IsHexDigit((char)b))
                    sb.Append("\" \"");
                sb.Append((char)b);
                afterHex = false;
            }
            else if (b is (byte)'"' or (byte)'\\' or (byte)'\n')
            {
                sb.Append(b == '\n' ? "\\n" : "\\" + (char)b);
                afterHex = false;
            }
            else
            {
                sb.Append($"\\x{b:X2}");
                afterHex = true;
            }
        }
        return sb.Append('"').ToString();
    }

    public static string Text(string text) => CString(Cp1252.GetBytes(text));

    public static string GrfPath(string path) => CString(GrfArchive.Cp949.GetBytes(path));

    private static string GkText(Texts text) =>
        $"{{{Text(text.Pt)}, {(text.Es == "" ? "nullptr" : Text(text.Es))}, {(text.En == "" ? "nullptr" : Text(text.En))}}}";

    public static string Identifier(string text)
    {
        var sb = new StringBuilder();
        foreach (char c in text)
            sb.Append(char.IsAsciiLetterOrDigit(c) ? c : '_');
        if (sb.Length == 0 || char.IsAsciiDigit(sb[0]))
            sb.Insert(0, '_');
        return sb.ToString();
    }

    private static string ConstantName(Widget widget) =>
        "k" + Identifier(widget.Name != "" ? char.ToUpperInvariant(widget.Name[0]) + widget.Name[1..] : widget.Type.ToString() + widget.Id);

    // ---------------------------------------------------------------------------------------------
    // Arquivos
    // ---------------------------------------------------------------------------------------------

    private static string CMakeLists(string name) => $$"""
        # Gerado pelo GordoKore Studio. DLL de 32 bits sem dependencias: copiar para <jogo>\GordoKore\modules\
        cmake_minimum_required(VERSION 3.20)
        project({{name}} LANGUAGES CXX)

        set(CMAKE_CXX_STANDARD 17)
        add_library({{name}} SHARED module.gen.cpp user.cpp)
        set_target_properties({{name}} PROPERTIES PREFIX "")

        if(MINGW)
            target_compile_options({{name}} PRIVATE -m32 -O2)
            target_link_options({{name}} PRIVATE -m32 -static -static-libgcc -static-libstdc++)
        endif()

        """;

    private static string Header(Project project)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// Gerado pelo GordoKore Studio: reescrito a cada geracao. Codigo proprio vai no user.cpp.");
        sb.AppendLine("#pragma once");
        sb.AppendLine();
        sb.AppendLine("#include \"gordokore_sdk.h\"");
        sb.AppendLine();
        sb.AppendLine("extern const GkHost *g_host;");
        sb.AppendLine($"extern int g_windows[{Math.Max(1, project.Windows.Count)}]; // handles do host (open/close/toggle)");
        sb.AppendLine();
        for (int i = 0; i < project.Windows.Count; i++)
        {
            var window = project.Windows[i];
            sb.AppendLine($"// Janela \"{window.Key}\" (indice {i} em g_windows)");
            sb.AppendLine($"namespace {Identifier(window.Key)}");
            sb.AppendLine("{");
            sb.AppendLine($"    constexpr int kWindow = {i};");
            foreach (var widget in window.Widgets)
                sb.AppendLine($"    constexpr int {ConstantName(widget)} = {widget.Id}; // {widget.Type}");
            sb.AppendLine("}");
            sb.AppendLine();
        }
        sb.AppendLine("// user.cpp: fim do gk_module_init (0 = ok) e todos os eventos dos widgets (depois das acoes de comando/Lua)");
        sb.AppendLine("int user_init(const GkHost *host);");
        sb.AppendLine("void user_event(int window, GkWin win, int id, int event, int value);");
        return sb.ToString();
    }

    private static string Source(Project project, string name, Func<string, Bitmap?> loadImage)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// Gerado pelo GordoKore Studio: reescrito a cada geracao. Codigo proprio vai no user.cpp.");
        sb.AppendLine("#include \"module.gen.h\"");
        sb.AppendLine();
        sb.AppendLine("const GkHost *g_host = nullptr;");
        sb.AppendLine($"int g_windows[{Math.Max(1, project.Windows.Count)}] = {{}};");
        sb.AppendLine();
        sb.AppendLine("namespace");
        sb.AppendLine("{");

        var icons = new HashSet<int>();
        for (int i = 0; i < project.Windows.Count; i++)
        {
            var window = project.Windows[i];
            // A imagem é do chamador (o estudio guarda em cache): nao descartar
            if (window.MenuIcon != "" && loadImage(window.MenuIcon) is { } image)
            {
                sb.AppendLine($"    const unsigned char kIcon{i}[] = {{{string.Join(",", MenuIcon(image))}}};");
                icons.Add(i);
            }

            var widgets = window.Widgets.Where(w => !IsCell(window, w)).ToList();
            var lists = widgets.Where(w => w.Type == WidgetType.DataList).ToList();
            sb.AppendLine($"    void open_{i}(GkWin win, void *)");
            sb.AppendLine("    {");
            // Areas rolaveis antes (moldura por baixo); depois quem vai dentro delas
            foreach (var w in widgets.OrderBy(w => w.Type == WidgetType.ScrollArea ? 0 : 1))
                sb.AppendLine("        " + AddCall(window, w));
            foreach (var list in lists)
                foreach (var cell in window.Widgets.Where(w => w.Parent == list.Id))
                    sb.Append(AddCell(window, list, cell));
            foreach (var w in widgets.Where(w => w.Parent != 0 && w.Type != WidgetType.ScrollArea))
                sb.AppendLine($"        g_host->set_parent(win, {w.Id}, {w.Parent});");
            // Registros depois dos campos: o filtro le o texto do campo
            foreach (var list in lists)
            {
                if (SourceName(list.Source) is { } source)
                    sb.AppendLine($"        g_host->set_source(win, {list.Id}, \"{source}\");");
                else if (list.Source == DataSource.Fixed)
                {
                    var (columns, rows) = FixedRows(list.Data);
                    sb.AppendLine($"        g_host->set_rows(win, {list.Id}, {Text(columns)}, {Text(rows)});");
                }
                if (window.Find(list.FilterField) is { Type: WidgetType.Edit } field)
                    sb.AppendLine($"        g_host->set_filter(win, {list.Id}, {field.Id}, {Text(list.FilterColumns)});");
            }
            foreach (var w in widgets.Where(w => w.Anchor != Anchor.TopLeft && w.Parent == 0))
                sb.AppendLine($"        g_host->set_anchor(win, {w.Id}, {AnchorFlags(w.Anchor)});");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine($"    void event_{i}(GkWin win, int id, int event, int value, void *)");
            sb.AppendLine("    {");
            sb.AppendLine($"        user_event({i}, win, id, event, value);");
            sb.AppendLine("    }");
            sb.AppendLine();
        }

        if (project.Lua.Trim() != "")
        {
            sb.AppendLine("    const char *const kLua =");
            foreach (string line in project.Lua.Replace("\r\n", "\n").Split('\n'))
                sb.AppendLine("        " + CString(Cp1252.GetBytes(line + "\n")));
            sb.AppendLine("        ;");
        }
        sb.AppendLine("}");
        sb.AppendLine();

        sb.AppendLine("GK_EXPORT int gk_module_init(const GkHost *host)");
        sb.AppendLine("{");
        sb.AppendLine("    GK_CHECK_HOST(host);");
        sb.AppendLine("    g_host = host;");
        for (int i = 0; i < project.Windows.Count; i++)
        {
            var window = project.Windows[i];
            sb.AppendLine();
            sb.AppendLine("    {");
            sb.AppendLine("        GkWindowDef def = {};");
            sb.AppendLine($"        def.key = {Text(window.Key)};");
            sb.AppendLine($"        def.title = {GkText(window.Title)};");
            sb.AppendLine($"        def.width = {window.Width};");
            sb.AppendLine($"        def.height = {window.Height};");
            if (icons.Contains(i))
            {
                sb.AppendLine($"        def.menu_icon_bmp = kIcon{i};");
                sb.AppendLine($"        def.menu_icon_size = sizeof(kIcon{i});");
                if (window.MenuLabel.Pt != "")
                    sb.AppendLine($"        def.menu_label = {GkText(window.MenuLabel)};");
            }
            sb.AppendLine($"        def.on_open = open_{i};");
            sb.AppendLine($"        def.on_event = event_{i};");
            sb.AppendLine($"        g_windows[{i}] = host->define_window(&def);");
            sb.AppendLine($"        if (g_windows[{i}] < 0)");
            sb.AppendLine("            return 1;");
            if (window.Resizable)
                sb.AppendLine($"        host->set_resizable(g_windows[{i}], {window.MinWidth}, {window.MinHeight}, {window.MaxWidth}, {window.MaxHeight}, {window.HeightStep});");
            if (window.OnOpen.Trim() != "")
                sb.AppendLine($"        host->bind_event(g_windows[{i}], 0, GK_EVENT_OPEN, GK_ACTION_LUA, {Text(window.OnOpen.Trim())});");
            foreach (var w in window.Widgets)
            {
                if (w.Type == WidgetType.DataList) // linha: uma acao por evento
                {
                    sb.Append(Bind(window, i, w.Id, "GK_EVENT_CLICK", w.Action, w.ActionValue));
                    sb.Append(Bind(window, i, w.Id, "GK_EVENT_RIGHT_CLICK", w.RightAction, w.RightValue));
                    sb.Append(Bind(window, i, w.Id, "GK_EVENT_DOUBLE_CLICK", w.DoubleAction, w.DoubleValue));
                }
                else if (w.Clickable)
                    sb.Append(Bind(window, i, w.Id, null, w.Action, w.ActionValue));
            }
            sb.AppendLine("    }");
        }
        if (project.Lua.Trim() != "")
        {
            sb.AppendLine();
            sb.AppendLine($"    if (host->lua_load(kLua, {Text(name + ".lua")}) != 0)");
            sb.AppendLine("        return 2;");
        }
        sb.AppendLine("    return user_init(host);");
        sb.AppendLine("}");
        return sb.ToString();
    }

    /// <summary>Problemas que impedem gerar o codigo (mensagens para o log).</summary>
    public static List<string> Validate(Project project)
    {
        var problems = new List<string>();
        foreach (var window in project.Windows)
        {
            string at = $"Janela \"{window.Key}\"";
            if (project.Windows.Count(w => w.Key == window.Key) > 1)
                problems.Add($"Duas janelas com a chave \"{window.Key}\".");
            foreach (var group in window.Widgets.GroupBy(w => w.Id).Where(g => g.Count() > 1))
                problems.Add($"{at}: id {group.Key} repetido.");
            foreach (var group in window.Widgets.Where(w => w.Name != "").GroupBy(w => w.Name).Where(g => g.Count() > 1))
                problems.Add($"{at}: nome \"{group.Key}\" repetido.");
            foreach (var widget in window.Widgets.Where(w => w.Id is < 1 or > 0x7000))
                problems.Add($"{at}: id {widget.Id} fora de 1..28672.");
            foreach (var widget in window.Widgets.Where(w => w.Parent != 0))
            {
                var container = window.Widgets.FirstOrDefault(c => c.Id == widget.Parent && c != widget && c.IsContainer);
                if (container == null)
                    problems.Add($"{at}: widget {widget.Id} dentro de {widget.Parent}, que não existe ou não é área/lista (mudou o id dela?).");
                else if (widget.IsContainer)
                    problems.Add($"{at}: {widget.Type} {widget.Id} não pode ficar dentro de outra área ou lista.");
                else if (container.Type == WidgetType.DataList && widget.Type is not (WidgetType.Label or WidgetType.Button or WidgetType.ImageButton
                             or WidgetType.Image or WidgetType.ItemIcon or WidgetType.Rect))
                    problems.Add($"{at}: {widget.Type} {widget.Id} não pode ser célula de lista de dados.");
            }
            foreach (var widget in window.Widgets.Where(w => w.Type is WidgetType.Image or WidgetType.ImageButton && w.Image == ""))
                problems.Add($"{at}: {widget.Type} {widget.Id} sem imagem.");
            foreach (var list in window.Widgets.Where(w => w.Type == WidgetType.DataList))
                if (list.FilterField != "" && window.Find(list.FilterField) is not { Type: WidgetType.Edit })
                    problems.Add($"{at}: a lista \"{list.Name}\" filtra pelo campo \"{list.FilterField}\", que não é um campo de texto da janela.");
            foreach (var widget in window.Widgets)
                foreach (var (kind, value) in new[] { (widget.Action, widget.ActionValue), (widget.RightAction, widget.RightValue), (widget.DoubleAction, widget.DoubleValue) })
                    if (kind == ActionKind.ClearField && window.Find(value.Trim()) is not { Type: WidgetType.Edit })
                        problems.Add($"{at}: {widget.Type} {widget.Id} limpa o campo \"{value}\", que não é um campo de texto da janela.");
                    else if (kind == ActionKind.Hide && value.Trim() != "" && !WorldParts.Contains(value.Trim()))
                        problems.Add($"{at}: {widget.Type} {widget.Id} esconde \"{value}\"; o que dá para esconder: {string.Join(", ", WorldParts)}.");
            if (window.OnOpen.Trim() != "" && project.Lua.Trim() == "")
                problems.Add($"{at}: chama \"{window.OnOpen.Trim()}\" ao abrir, mas o Script Lua está vazio.");
        }
        return problems;
    }

    /// <summary>Partes do mapa da acao "Esconder no mapa" (GkWorldPart do host, na mesma ordem); vazio = a primeira.</summary>
    public static readonly string[] WorldParts = { "jogadores" };

    /// <summary>Widget que é celula (modelo da linha) de uma lista de dados.</summary>
    public static bool IsCell(WindowModel window, Widget widget) =>
        widget.Parent != 0 && window.Widgets.Any(w => w.Id == widget.Parent && w.Type == WidgetType.DataList);

    public static string? SourceName(DataSource source) => source switch
    {
        DataSource.Items => "itens",
        DataSource.Monsters => "monstros",
        DataSource.Maps => "mapas",
        DataSource.MapPlayers => "jogadores_no_mapa",
        DataSource.MapNpcs => "npcs_no_mapa",
        DataSource.MapMonsters => "monstros_no_mapa",
        _ => null
    };

    /// <summary>Lista fixa do estudio (";" e uma linha por registro) no formato do host (\t e \n).</summary>
    public static (string Columns, string Rows) FixedRows(string data)
    {
        var lines = data.Replace("\r\n", "\n").Split('\n').Select(l => l.Trim()).Where(l => l != "").ToList();
        if (lines.Count == 0)
            return ("", "");
        static string Row(string line) => string.Join('\t', line.Split(';').Select(v => v.Trim()));
        return (Row(lines[0]), string.Concat(lines.Skip(1).Select(l => Row(l) + "\n")));
    }

    public static int AnchorFlags(Anchor anchor) => anchor switch
    {
        Anchor.TopLeft => 1 | 2,
        Anchor.TopRight => 4 | 2,
        Anchor.BottomLeft => 1 | 8,
        Anchor.BottomRight => 4 | 8,
        Anchor.Top => 1 | 4 | 2,
        Anchor.Bottom => 1 | 4 | 8,
        Anchor.Left => 1 | 2 | 8,
        Anchor.Right => 4 | 2 | 8,
        _ => 1 | 2 | 4 | 8
    };

    // Celula: so a largura da lista conta (direita acompanha, esticando cresce)
    private static int CellAnchor(Anchor anchor) => anchor switch
    {
        Anchor.TopRight or Anchor.BottomRight or Anchor.Right => 4,
        Anchor.Top or Anchor.Bottom or Anchor.Fill => 1 | 4,
        _ => 0
    };

    // "{lista.total}" / "{lista.all}" pelo nome da lista vira o id dela ("{12.total}"), como o host le
    private static string Counts(WindowModel window, string text) =>
        System.Text.RegularExpressions.Regex.Replace(text, @"\{([^{}.]+)\.(total|all)\}", m =>
            window.Find(m.Groups[1].Value) is { Type: WidgetType.DataList } list ? "{" + list.Id + "." + m.Groups[2].Value + "}" : m.Value);

    private static string CountsText(WindowModel window, Texts text) =>
        GkText(new Texts(Counts(window, text.Pt), Counts(window, text.Es), Counts(window, text.En)));

    private static string OptPath(string path) => path == "" ? "nullptr" : GrfPath(path);

    private static string AddCall(WindowModel window, Widget w) => w.Type switch
    {
        WidgetType.Label => $"g_host->add_label(win, {w.Id}, {w.X}, {w.Y}, {CountsText(window, w.Text)}, 0x{w.Color:X6}, {(w.Bold ? 1 : 0)});",
        WidgetType.Button => $"g_host->add_button(win, {w.Id}, {w.X}, {w.Y}, {w.Width}, {GkText(w.Text)});",
        WidgetType.ImageButton when w.Text.Pt != "" =>
            $"g_host->add_caption_button(win, {w.Id}, {w.X}, {w.Y}, {GrfPath(w.Image)}, {OptPath(w.ImageHover)}, {OptPath(w.ImagePress)}, {GkText(w.Text)});",
        WidgetType.ImageButton => $"g_host->add_image_button(win, {w.Id}, {w.X}, {w.Y}, {w.Width}, {w.Height}, {GrfPath(w.Image)}, {OptPath(w.ImageHover)}, {OptPath(w.ImagePress)});",
        WidgetType.CheckBox => $"g_host->add_checkbox(win, {w.Id}, {w.X}, {w.Y}, {(w.Checked ? 1 : 0)});",
        WidgetType.Edit => $"g_host->add_edit(win, {w.Id}, {w.X}, {w.Y}, {w.Width}, {w.Height}, {w.MaxChars}, {GkText(w.Text)});",
        WidgetType.ListBox => $"g_host->add_listbox(win, {w.Id}, {w.X}, {w.Y}, {w.Width}, {w.Height});",
        WidgetType.Image => $"g_host->add_image(win, {w.Id}, {w.X}, {w.Y}, {GrfPath(w.Image)});",
        WidgetType.Sprite => $"g_host->add_sprite(win, {w.Id}, {w.X}, {w.Y}, {w.Width}, {w.Height}, {Text(w.Sprite)});",
        WidgetType.Rect => $"g_host->add_rect(win, {w.Id}, {w.X}, {w.Y}, {w.Width}, {w.Height}, 0x{w.Color:X6});",
        WidgetType.ScrollArea => $"g_host->add_scroll_area(win, {w.Id}, {w.X}, {w.Y}, {w.Width}, {w.Height});",
        WidgetType.DataList => $"g_host->add_data_list(win, {w.Id}, {w.X}, {w.Y}, {w.Width}, {w.Height}, {Math.Max(1, w.RowHeight)});",
        WidgetType.Strip => $"g_host->add_strip(win, {w.Id}, {w.X}, {w.Y}, {w.Width}, {OptPath(w.Image)}, {OptPath(w.ImageMid)}, {OptPath(w.ImageRight)});",
        WidgetType.ItemIcon => $"g_host->add_item_icon(win, {w.Id}, {w.X}, {w.Y}, {(int.TryParse(w.Item.Trim(), out int item) ? item : 0)});",
        _ => throw new ArgumentOutOfRangeException(nameof(w))
    };

    // Celula do modelo da linha: GkCell na ordem do header (size, id, kind, x, y, width, height, text, image, hover, press, item, color, flags, anchor)
    private static string AddCell(WindowModel window, Widget list, Widget c)
    {
        string kind, image = "nullptr", hover = "nullptr", press = "nullptr", item = "nullptr";
        int x = c.X, width = c.Width, height = c.Height, flags = 0;
        switch (c.Type)
        {
            case WidgetType.Label:
                kind = "GK_CELL_TEXT";
                flags = (c.Bold ? 1 : 0) | (c.AlignRight ? 2 : 0) | (c.Fit ? 4 : 0);
                if (c.AlignRight)
                    x = c.X + c.Width; // o host alinha pela borda direita
                break;
            case WidgetType.ItemIcon:
                (kind, item, width, height) = ("GK_CELL_ITEM_ICON", Text(c.Item.Trim()), 24, 24);
                break;
            case WidgetType.Image:
                (kind, image) = ("GK_CELL_IMAGE", GrfPath(c.Image));
                break;
            case WidgetType.Button:
                kind = "GK_CELL_BUTTON";
                break;
            case WidgetType.ImageButton:
                (kind, image, hover, press) = ("GK_CELL_BUTTON", GrfPath(c.Image), OptPath(c.ImageHover), OptPath(c.ImagePress));
                break;
            case WidgetType.Rect:
                kind = "GK_CELL_RECT";
                break;
            default:
                return $"        // {c.Type} {c.Id} nao pode ser celula de lista\r\n";
        }
        if (c.DropsOnly && c.Type is WidgetType.Button or WidgetType.ImageButton)
            flags |= 8;
        return "        {\r\n" +
               $"            GkCell cell = {{sizeof(GkCell), {c.Id}, {kind}, {x}, {c.Y}, {width}, {height}, {GkText(c.Text)}, {image}, {hover}, {press}, {item}, 0x{c.Color:X6}, {flags}, {CellAnchor(c.Anchor)}}};\r\n" +
               $"            g_host->add_cell(win, {list.Id}, &cell);\r\n" +
               "        }\r\n";
    }

    // host->bind (todos os eventos) ou host->bind_event; nada para "Nenhuma", C++ ou valor que falta
    private static string Bind(WindowModel window, int index, int id, string? evt, ActionKind action, string value)
    {
        value = value.Trim();
        string? kind = action switch
        {
            ActionKind.Command => "GK_ACTION_COMMAND",
            ActionKind.Lua => "GK_ACTION_LUA",
            ActionKind.DragItem => "GK_ACTION_DRAG_ITEM",
            ActionKind.ItemInfo => "GK_ACTION_ITEM_INFO",
            ActionKind.ItemDrops => "GK_ACTION_ITEM_DROPS",
            ActionKind.ClearField => "GK_ACTION_CLEAR",
            ActionKind.Hide => "GK_ACTION_HIDE",
            _ => null
        };
        if (kind == null || (action is ActionKind.Command or ActionKind.Lua && value == ""))
            return "";
        if (action == ActionKind.Hide && value == "")
            value = WorldParts[0];
        if (action == ActionKind.ClearField)
        {
            if (window.Find(value) is not { Type: WidgetType.Edit } field)
                return "";
            value = field.Id.ToString();
        }
        return evt == null
            ? $"        host->bind(g_windows[{index}], {id}, {kind}, {Text(value)});\r\n"
            : $"        host->bind_event(g_windows[{index}], {id}, {evt}, {kind}, {Text(value)});\r\n";
    }

    private static string UserSource() => """
        // Codigo do modulo: o GordoKore Studio cria este arquivo uma vez e nunca reescreve.
        // Ids dos widgets e indices das janelas: module.gen.h. API: gordokore_sdk.h.
        #include "module.gen.h"

        int user_init(const GkHost *)
        {
            return 0;
        }

        void user_event(int window, GkWin win, int id, int event, int value)
        {
            (void)window, (void)win, (void)id, (void)event, (void)value;
        }

        """;

    /// <summary>BMP 43x43 de 24 bits para o menu: a imagem centralizada (reduzida se maior) sobre #FF00FF.</summary>
    public static byte[] MenuIcon(Bitmap image)
    {
        using var canvas = new Bitmap(IconSize, IconSize, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(canvas))
        {
            float scale = Math.Min(1f, Math.Min((float)IconSize / image.Width, (float)IconSize / image.Height));
            int w = (int)(image.Width * scale), h = (int)(image.Height * scale);
            g.InterpolationMode = scale < 1 ? System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic : System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
            g.DrawImage(image, (IconSize - w) / 2, (IconSize - h) / 2, w, h);
        }
        // O cliente so conhece o rosa: meio transparente vira rosa, o resto cor cheia
        Images.Edit(canvas, pixels =>
        {
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = (pixels[i] >>> 24) < 128 ? unchecked((int)0xFFFF00FF) : pixels[i] | unchecked((int)0xFF000000);
        });
        using var icon = canvas.Clone(new Rectangle(0, 0, IconSize, IconSize), PixelFormat.Format24bppRgb);
        using var stream = new MemoryStream();
        icon.Save(stream, ImageFormat.Bmp);
        return stream.ToArray();
    }
}
