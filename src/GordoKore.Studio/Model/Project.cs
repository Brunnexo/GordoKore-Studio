using System.ComponentModel;
using System.Drawing.Design;
using System.Reflection;
using System.Text.Json;
using System.Windows.Forms.Design;
using System.Text.Json.Serialization;

namespace GordoKore.Studio.Model;

public enum WidgetType
{
    Label,
    Button,
    ImageButton,
    CheckBox,
    Edit,
    ListBox,
    Image,
    Sprite,
    Rect,
    ScrollArea, // area rolavel: os widgets com Parent = Id dela rolam juntos
    DataList,   // lista de dados: os widgets com Parent = Id dela sao o modelo da linha (celulas)
    Strip,      // faixa de 3 imagens (esquerda, meio repetido, direita)
    ItemIcon    // icone de item como no inventario
}

[TypeConverter(typeof(DescriptionEnumConverter))]
public enum ActionKind
{
    [Description("Nenhuma")] None,
    [Description("Comando do OpenKore")] Command, // console do OpenKore, pelo GordoKore.pl
    [Description("Lua (no jogo)")] Lua,           // funcao do script do modulo, roda no bridge.dll
    [Description("C++ (user.cpp)")] Cpp,          // on_event no user.cpp
    [Description("Arrastar o item")] DragItem,    // listas: o item do registro vai para o cursor
    [Description("Descrição do item")] ItemInfo,
    [Description("Drops do item (Divine Pride)")] ItemDrops,
    [Description("Limpar campo de texto")] ClearField,
    [Description("Esconder no mapa")] Hide        // so na tela do jogador; liga/desliga marcado esconde, o resto alterna
}

// Ancoras: como o widget acompanha a janela redimensionavel (nas celulas, so a largura da lista)
[TypeConverter(typeof(DescriptionEnumConverter))]
public enum Anchor
{
    [Description("Topo à esquerda (fixo)")] TopLeft,
    [Description("Topo à direita")] TopRight,
    [Description("Embaixo à esquerda")] BottomLeft,
    [Description("Embaixo à direita")] BottomRight,
    [Description("Topo, esticando na largura")] Top,
    [Description("Embaixo, esticando na largura")] Bottom,
    [Description("Esquerda, esticando na altura")] Left,
    [Description("Direita, esticando na altura")] Right,
    [Description("Esticar nos dois sentidos")] Fill
}

[TypeConverter(typeof(DescriptionEnumConverter))]
public enum DataSource
{
    [Description("Itens do jogo")] Items,
    [Description("Monstros do jogo")] Monsters,
    [Description("Mapas do jogo")] Maps,
    [Description("Lista fixa (propriedade Dados)")] Fixed,
    [Description("Preenchida pelo código (Lua/C++)")] Code,
    // Ao vivo (SDK 5): colunas id, nome, x, y, classe, classe_nome, distancia; o bridge.dll atualiza a cada meio segundo
    [Description("Jogadores no mapa (ao vivo)")] MapPlayers,
    [Description("NPCs no mapa (ao vivo)")] MapNpcs,
    [Description("Monstros no mapa (ao vivo)")] MapMonsters
}

[TypeConverter(typeof(ExpandableObjectConverter))]
public sealed class Texts
{
    [DisplayName("Português")] public string Pt { get; set; } = "";
    [DisplayName("Español")] public string Es { get; set; } = "";
    [DisplayName("English")] public string En { get; set; } = "";

    public Texts() { }
    public Texts(string pt, string es = "", string en = "") => (Pt, Es, En) = (pt, es, en);

    public string Get(int language) => language switch
    {
        1 when Es != "" => Es,
        2 when En != "" => En,
        _ => Pt
    };

    public override string ToString() => Pt;
}

/// <summary>Propriedade so aparece na grade para esses tipos (Cell: so nas celulas de lista; NotCell: fora delas).</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class ForAttribute(params WidgetType[] types) : Attribute
{
    public WidgetType[] Types { get; } = types;
    public bool Cell { get; init; }
    public bool NotCell { get; init; }
}

[TypeDescriptionProvider(typeof(WidgetProperties))]
public sealed class Widget
{
    private const WidgetType L = WidgetType.Label, B = WidgetType.Button, IB = WidgetType.ImageButton, C = WidgetType.CheckBox,
        E = WidgetType.Edit, LB = WidgetType.ListBox, I = WidgetType.Image, S = WidgetType.Sprite, R = WidgetType.Rect,
        A = WidgetType.ScrollArea, D = WidgetType.DataList, F = WidgetType.Strip, IC = WidgetType.ItemIcon;

    [DisplayName("Tipo"), Category("Geral"), ReadOnly(true)] public WidgetType Type { get; set; }
    [DisplayName("Id"), Category("Geral"), Description("Número único na janela (1 a 28672). O código usa para achar o widget.")]
    public int Id { get; set; }
    [DisplayName("Nome"), Category("Geral"), Description("Nome para o código gerado (constante k<Nome>) e para ligar widgets: \"{nome.total}\" num texto, filtro da lista, limpar campo.")]
    public string Name { get; set; } = "";

    [DisplayName("X"), Category("Posição"), Description("Dentro de uma área rolável: relativo ao conteúdo da área. Célula de lista: relativo à linha.")] public int X { get; set; }
    [DisplayName("Y"), Category("Posição"), Description("Dentro de uma área rolável: relativo ao conteúdo da área. Célula de lista: relativo à linha.")] public int Y { get; set; }
    [DisplayName("Dentro de"), Category("Posição"), ReadOnly(true),
     Description("Id da área rolável ou da lista de dados que contém o widget (0 = direto na janela). Duplo clique na área ou lista para editar o que tem dentro.")]
    public int Parent { get; set; }
    [DisplayName("Largura"), Category("Posição"), Description("Largura (botão: 0 = do tamanho do texto; texto de célula: a caixa onde ele é alinhado/cortado).")]
    [For(B, IB, E, LB, S, R, A, D, F)]
    public int Width { get; set; }
    [DisplayName("Largura"), Category("Posição"), Description("Caixa do texto na linha: alinhar à direita usa a borda direita dela; cortar usa a largura.")]
    [For(L, Cell = true)]
    [JsonIgnore]
    public int CellWidth { get => Width; set => Width = value; }
    [DisplayName("Altura"), Category("Posição")]
    [For(IB, E, LB, S, R, A, D)]
    public int Height { get; set; }
    [DisplayName("Âncora"), Category("Posição"),
     Description("Como o widget acompanha a janela redimensionável. Em célula de lista: à direita acompanha a borda direita da lista; esticando cresce com ela.")]
    public Anchor Anchor { get; set; }

    [DisplayName("Texto"), Category("Conteúdo"), Description("Texto do label/botão, legenda do botão de imagem ou texto-guia do campo. Em célula: {coluna} vira o valor (ex.: {nome}, #{id}). Num texto: {lista.total} mostra quantos registros a lista tem.")]
    [For(L, B, IB, E)]
    public Texts Text { get; set; } = new();
    [DisplayName("Cor"), Category("Conteúdo"), Description("Cor 0xRRGGBB.")]
    [For(L, R)]
    [TypeConverter(typeof(HexConverter))]
    public int Color { get; set; }
    [DisplayName("Negrito"), Category("Conteúdo")]
    [For(L)]
    public bool Bold { get; set; }
    [DisplayName("Alinhar à direita"), Category("Conteúdo"), Description("Texto encostado na borda direita da caixa (Largura).")]
    [For(L, Cell = true)]
    public bool AlignRight { get; set; }
    [DisplayName("Cortar com \"...\""), Category("Conteúdo"), Description("Texto maior que a caixa (Largura) termina em \"...\".")]
    [For(L, Cell = true)]
    public bool Fit { get; set; }
    [DisplayName("Imagem"), Category("Conteúdo"), Description("Imagem (relativa a data\\texture\\). Botão de imagem: estado normal. Faixa: parte da esquerda. Em célula: {coluna} vale.")]
    [For(IB, I, F)]
    public string Image { get; set; } = "";
    [DisplayName("Imagem (mouse em cima)"), Category("Conteúdo"), Description("Botão de imagem: mouse em cima (vazio = normal).")]
    [For(IB)]
    public string ImageHover { get; set; } = "";
    [DisplayName("Imagem (apertado)"), Category("Conteúdo"), Description("Botão de imagem: apertado (vazio = normal).")]
    [For(IB)]
    public string ImagePress { get; set; } = "";
    [DisplayName("Imagem do meio"), Category("Conteúdo"), Description("Faixa: repetida entre a esquerda e a direita.")]
    [For(F)]
    public string ImageMid { get; set; } = "";
    [DisplayName("Imagem da direita"), Category("Conteúdo"), Description("Faixa: parte da direita.")]
    [For(F)]
    public string ImageRight { get; set; } = "";
    [DisplayName("Sprite"), Category("Conteúdo"), Description("Sprite de NPC ou monstro (nome do arquivo, sem .spr).")]
    [For(S)]
    public string Sprite { get; set; } = "";
    [DisplayName("Item"), Category("Conteúdo"), Description("ID do item (ex.: 501). Em célula de lista: a coluna do ID, ex.: {id}.")]
    [For(IC)]
    public string Item { get; set; } = "";
    [DisplayName("Ligado"), Category("Conteúdo")]
    [For(C)]
    public bool Checked { get; set; }
    [DisplayName("Máximo de caracteres"), Category("Conteúdo"), Description("Campo de texto: máximo de caracteres.")]
    [For(E)]
    public int MaxChars { get; set; } = 32;
    [DisplayName("Só com Divine Pride"), Category("Conteúdo"), Description("Botão de célula que só aparece com a apiKey do Divine Pride no GordoKore.ini (como o \"Drops\" da \"Itens do jogo\").")]
    [For(B, IB, Cell = true)]
    public bool DropsOnly { get; set; }

    // Lista de dados
    [DisplayName("Fonte de dados"), Category("Lista"), Description("De onde vêm os registros: tabelas do jogo (itens: id, nome; monstros: id, nome, mapas, sprite; mapas: nome), quem está no mapa agora (jogadores, NPCs, monstros: id, nome, x, y, classe, classe_nome, distancia; atualiza sozinho), a lista fixa (Dados) ou o código (gk.rows no Lua).")]
    [For(D)]
    public DataSource Source { get; set; }
    [DisplayName("Dados"), Category("Lista"), Description("Lista fixa: 1ª linha com as colunas, depois um registro por linha; valores separados por ponto e vírgula. Ex.: id;nome  ↵  1;Prontera")]
    [For(D)]
    [Editor("System.ComponentModel.Design.MultilineStringEditor, System.Windows.Forms.Design", typeof(UITypeEditor))]
    public string Data { get; set; } = "";
    [DisplayName("Altura da linha"), Category("Lista")]
    [For(D)]
    public int RowHeight { get; set; } = 32;
    [DisplayName("Filtrar pelo campo"), Category("Lista"), Description("Nome do campo de texto que filtra a lista ao digitar (vazio = sem busca).")]
    [For(D)]
    [TypeConverter(typeof(EditNameConverter))]
    public string FilterField { get; set; } = "";
    [DisplayName("Colunas da busca"), Category("Lista"), Description("Colunas onde a busca procura, separadas por ponto e vírgula. Só números também acham o começo da coluna id.")]
    [For(D)]
    public string FilterColumns { get; set; } = "nome";

    [DisplayName("Ação (clique)"), Category("Ação"), Description("Botões: clique. Liga/desliga: ao mudar. Lista: duplo clique. Lista de dados: clique numa linha. " +
        "Comando do OpenKore vai para o console (ex.: talknpc); em listas, {coluna} vira o valor do registro. Lua: função do script. Arrastar/descrição/drops: o item do registro. " +
        "Esconder no mapa: só na sua tela; o liga/desliga marcado esconde, os outros alternam (esconde/mostra).")]
    [For(B, IB, C, LB, D)]
    public ActionKind Action { get; set; }
    [DisplayName("Comando ou função"), Category("Ação"), Description("Comando do OpenKore (ex.: ai manual, talknpc {x} {y}), nome da função Lua, coluna do ID do item (vazio = id), nome do campo a limpar ou o que esconder no mapa (jogadores, efeitos dos jogadores ou os dois: jogadores,efeitos; vazio = jogadores).")]
    [For(B, IB, C, LB, D)]
    public string ActionValue { get; set; } = "";
    [DisplayName("Ação (clique direito)"), Category("Ação")]
    [For(D)]
    public ActionKind RightAction { get; set; }
    [DisplayName("Comando ou função (clique direito)"), Category("Ação")]
    [For(D)]
    public string RightValue { get; set; } = "";
    [DisplayName("Ação (duplo clique)"), Category("Ação")]
    [For(D)]
    public ActionKind DoubleAction { get; set; }
    [DisplayName("Comando ou função (duplo clique)"), Category("Ação")]
    [For(D)]
    public string DoubleValue { get; set; } = "";

    [Browsable(false), JsonIgnore] public bool Clickable => Type is WidgetType.Button or WidgetType.ImageButton or WidgetType.CheckBox or WidgetType.ListBox or WidgetType.DataList;
    [Browsable(false), JsonIgnore] public bool IsContainer => Type is WidgetType.ScrollArea or WidgetType.DataList;
}

public sealed class WindowModel
{
    [DisplayName("Chave"), Category("Geral"), Description("Identificador único entre todos os módulos (guarda a posição da janela).")]
    public string Key { get; set; } = "janela";
    [DisplayName("Título"), Category("Geral")] public Texts Title { get; set; } = new("Nova janela", "Nueva ventana", "New window");
    [DisplayName("Largura"), Category("Tamanho")] public int Width { get; set; } = 240;
    [DisplayName("Altura"), Category("Tamanho")] public int Height { get; set; } = 160;
    [DisplayName("Redimensionável"), Category("Tamanho"), Description("O jogador muda o tamanho pelo canto; os widgets seguem a âncora de cada um.")]
    public bool Resizable { get; set; }
    [DisplayName("Largura mínima"), Category("Tamanho")] public int MinWidth { get; set; } = 120;
    [DisplayName("Largura máxima"), Category("Tamanho")] public int MaxWidth { get; set; } = 640;
    [DisplayName("Altura mínima"), Category("Tamanho")] public int MinHeight { get; set; } = 80;
    [DisplayName("Altura máxima"), Category("Tamanho")] public int MaxHeight { get; set; } = 480;
    [DisplayName("Passo da altura"), Category("Tamanho"), Description("A altura cresce de tantos em tantos px a partir da mínima (ex.: 32 = linhas inteiras de uma lista). 0 = livre.")]
    public int HeightStep { get; set; }
    [DisplayName("Ícone do menu"), Category("Menu"), Description("Imagem do botão no menu do GordoKore: da GRF (aba Imagens) ou um arquivo do PC (botão \"...\"). Vai embutida na DLL. Vazio = fora do menu.")]
    [Editor(typeof(ImageFileEditor), typeof(UITypeEditor))]
    public string MenuIcon { get; set; } = "";
    [DisplayName("Legenda no menu"), Category("Menu"), Description("Legenda no menu (vazio = título).")]
    public Texts MenuLabel { get; set; } = new();
    [DisplayName("Ao abrir (função Lua)"), Category("Lua"), Description("Função do Script Lua chamada sempre que a janela abre, com os widgets prontos (ex.: preencher textos e listas). Vazio = nada.")]
    public string OnOpen { get; set; } = "";

    [Browsable(false)] public List<Widget> Widgets { get; set; } = new();
    public override string ToString() => Key;

    public Widget? Find(string name) => name == "" ? null : Widgets.FirstOrDefault(w => w.Name == name);
}

public sealed class Project
{
    public string Name { get; set; } = "meu_modulo";
    public List<WindowModel> Windows { get; set; } = new();
    public string Lua { get; set; } = "";

    [JsonIgnore] public string? Path { get; set; }

    // Acentos, coreano (caminhos da GRF) e aspas do Lua ficam legiveis no .gkproj (o padrao escreve é, '...);
    // o arquivo nunca vai para HTML, entao o escape relaxado nao tem risco
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() }
    };

    public static Project Load(string path)
    {
        var project = FromJson(File.ReadAllText(path));
        project.Path = path;
        return project;
    }

    public void Save(string path)
    {
        File.WriteAllText(path, ToJson());
        Path = path;
    }

    public string ToJson() => JsonSerializer.Serialize(this, Json);

    public static Project FromJson(string json) => JsonSerializer.Deserialize<Project>(json, Json) ?? new Project();
}

/// <summary>
/// Grade de propriedades: so as do tipo do widget ([For]); celula de lista de dados ve as de celula. O estudio diz quem é
/// celula (a janela aberta).
/// </summary>
public sealed class WidgetProperties() : TypeDescriptionProvider(TypeDescriptor.GetProvider(typeof(object)))
{
    public static Func<Widget, bool> IsCell { get; set; } = _ => false;

    public override ICustomTypeDescriptor GetTypeDescriptor(Type objectType, object? instance) =>
        new Filtered(base.GetTypeDescriptor(objectType, instance)!, instance as Widget);

    private sealed class Filtered(ICustomTypeDescriptor parent, Widget? widget) : CustomTypeDescriptor(parent)
    {
        public override PropertyDescriptorCollection GetProperties() => Filter(base.GetProperties());
        public override PropertyDescriptorCollection GetProperties(Attribute[]? attributes) => Filter(base.GetProperties(attributes));

        private PropertyDescriptorCollection Filter(PropertyDescriptorCollection all)
        {
            if (widget == null)
                return all;
            bool cell = IsCell(widget);
            return new PropertyDescriptorCollection(all.Cast<PropertyDescriptor>().Where(p => p.Attributes[typeof(ForAttribute)] is not ForAttribute only ||
                (only.Types.Contains(widget.Type) && (!only.Cell || cell) && (!only.NotCell || !cell))).ToArray());
        }
    }
}

/// <summary>Lista os campos de texto da janela aberta (filtro da lista de dados).</summary>
public sealed class EditNameConverter : StringConverter
{
    public static Func<IEnumerable<string>> Names { get; set; } = () => [];

    public override bool GetStandardValuesSupported(ITypeDescriptorContext? context) => true;
    public override bool GetStandardValuesExclusive(ITypeDescriptorContext? context) => false;
    public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext? context) => new(Names().Prepend("").ToArray());
}

// Enum na grade de propriedades pelo [Description]; o JSON continua com o nome
public sealed class DescriptionEnumConverter(Type type) : EnumConverter(type)
{
    public override object? ConvertTo(ITypeDescriptorContext? context, System.Globalization.CultureInfo? culture, object? value, Type destinationType)
        => destinationType == typeof(string) && value is Enum e ? Label(e) : base.ConvertTo(context, culture, value, destinationType);

    public override object? ConvertFrom(ITypeDescriptorContext? context, System.Globalization.CultureInfo? culture, object value)
        => value is string s && Enum.GetValues(EnumType).Cast<Enum>().FirstOrDefault(e => Label(e) == s) is { } match
            ? match
            : base.ConvertFrom(context, culture, value);

    private string Label(Enum value) =>
        EnumType.GetField(value.ToString())?.GetCustomAttribute<DescriptionAttribute>()?.Description ?? value.ToString();
}

public sealed class ImageFileEditor : FileNameEditor
{
    protected override void InitializeDialog(OpenFileDialog dialog)
    {
        base.InitializeDialog(dialog);
        dialog.Filter = "Imagens (*.bmp;*.png;*.tga)|*.bmp;*.png;*.tga|Todos os arquivos (*.*)|*.*";
        dialog.Title = "Ícone do menu";
    }
}

public sealed class HexConverter : Int32Converter
{
    public override object? ConvertTo(ITypeDescriptorContext? context, System.Globalization.CultureInfo? culture, object? value, Type destinationType)
        => destinationType == typeof(string) && value is int i ? $"0x{i:X6}" : base.ConvertTo(context, culture, value, destinationType);

    public override object? ConvertFrom(ITypeDescriptorContext? context, System.Globalization.CultureInfo? culture, object value)
    {
        if (value is string s && s.Trim().StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return Convert.ToInt32(s.Trim()[2..], 16);
        return base.ConvertFrom(context, culture, value);
    }
}
