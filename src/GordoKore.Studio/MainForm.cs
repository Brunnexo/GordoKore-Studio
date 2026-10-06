using System.Diagnostics;
using System.Drawing;
using GordoKore.Studio.Generation;
using GordoKore.Studio.Designer;
using GordoKore.Studio.Grf;
using GordoKore.Studio.Model;

namespace GordoKore.Studio;

public sealed class MainForm : Form
{
    private const int MaxListed = 800;

    private readonly Settings _settings = Settings.Load();
    private ResourceStore? _store;
    private Project _project = NewProject();
    private bool _dirty;
    private readonly History _history = new();

    private readonly Dictionary<string, Bitmap?> _textures = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Bitmap?> _sprites = new(StringComparer.OrdinalIgnoreCase);
    private List<string> _texturePaths = new();
    private List<string> _spriteNames = new();

    private readonly DesignCanvas _canvas = new();
    private readonly PropertyGrid _properties = new() { Dock = DockStyle.Fill, ToolbarVisible = false, PropertySort = PropertySort.Categorized };
    private readonly ToolStripComboBox _windows = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 140 };
    private readonly TextBox _lua = Editor();
    private readonly TextBox _log = new() { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false, Font = new Font("Consolas", 9f), BorderStyle = BorderStyle.None };
    private readonly TextBox _resourceFilter = new() { Dock = DockStyle.Top, PlaceholderText = "Buscar imagem (ex.: btn_ok, item\\)" };
    private readonly ListBox _resources = new() { Dock = DockStyle.Fill, IntegralHeight = false, HorizontalScrollbar = true };
    private readonly PictureBox _resourcePreview = Preview();
    private readonly TextBox _spriteFilter = new() { Dock = DockStyle.Top, PlaceholderText = "Buscar sprite (ex.: poring, kafra)" };
    private readonly ListBox _spriteList = new() { Dock = DockStyle.Fill, IntegralHeight = false };
    private readonly PictureBox _spritePreview = Preview();

    public MainForm(string? projectPath)
    {
        Text = "GordoKore Studio";
        Icon = new Icon(typeof(MainForm).Assembly.GetManifestResourceStream("logo.ico")!);
        Width = 1400;
        Height = 860;
        StartPosition = FormStartPosition.CenterScreen;
        KeyPreview = true;

        BuildLayout();
        _canvas.Texture = Texture;
        _canvas.SpriteImage = SpriteImage;
        _canvas.Notice += Log;
        // Grade de propriedades: celula de lista ve as opcoes de celula; filtro lista os campos da janela aberta
        WidgetProperties.IsCell = _canvas.IsCell;
        EditNameConverter.Names = () => _canvas.Window?.Widgets.Where(w => w.Type == WidgetType.Edit && w.Name != "").Select(w => w.Name) ?? [];
        _canvas.SelectionChanged += () => _properties.SelectedObject = (object?)_canvas.Selected ?? _canvas.Window;
        _canvas.Changed += () =>
        {
            MarkDirty();
            _properties.Refresh();
        };
        _properties.PropertyValueChanged += (_, _) =>
        {
            MarkDirty();
            _canvas.UpdateSize();
            _canvas.Invalidate();
            RefreshWindowList();
        };
        _lua.TextChanged += (_, _) =>
        {
            if (_project.Lua != _lua.Text)
            {
                _project.Lua = _lua.Text;
                MarkDirty(typing: true);
            }
        };

        string? open = projectPath ?? _settings.LastProject;
        LoadProject(open != null && File.Exists(open) ? TryLoad(open) ?? _project : _project);
        Shown += (_, _) => OpenResources();
        FormClosing += (_, e) => e.Cancel = !ConfirmDiscard();
    }

    private static Project NewProject() => new()
    {
        Windows = { new WindowModel() },
        Lua = "-- Funções chamadas pelas ações \"Lua\" dos widgets: (id, evento, valor)\n-- gk.get(id), gk.set(id, texto), gk.command(\"ai manual\"), gk.close()...\n"
    };

    private static TextBox Editor() => new()
    {
        Dock = DockStyle.Fill,
        Multiline = true,
        AcceptsTab = true,
        AcceptsReturn = true,
        ScrollBars = ScrollBars.Both,
        WordWrap = false,
        Font = new Font("Consolas", 10f),
        BorderStyle = BorderStyle.None // no modo escuro a borda 3D fica branca
    };

    private static PictureBox Preview() => new()
    {
        Dock = DockStyle.Bottom,
        Height = 140,
        SizeMode = PictureBoxSizeMode.CenterImage,
        BackColor = Color.FromArgb(0x4A, 0x4D, 0x55)
    };

    // ---------------------------------------------------------------------------------------------
    // Layout
    // ---------------------------------------------------------------------------------------------

    private void BuildLayout()
    {
        var menu = new MenuStrip();
        var file = new ToolStripMenuItem("&Arquivo");
        file.DropDownItems.Add("&Novo", null, (_, _) => { if (ConfirmDiscard()) LoadProject(NewProject()); });
        file.DropDownItems.Add("&Abrir...", null, (_, _) => OpenProject());
        ((ToolStripMenuItem)file.DropDownItems.Add("&Salvar", null, (_, _) => SaveProject(false))).ShortcutKeys = Keys.Control | Keys.S;
        file.DropDownItems.Add("Salvar &como...", null, (_, _) => SaveProject(true));
        file.DropDownItems.Add(new ToolStripSeparator());
        file.DropDownItems.Add("Sai&r", null, (_, _) => Close());

        var edit = new ToolStripMenuItem("&Editar");
        var undo = edit.DropDownItems.Add("&Desfazer", null, (_, _) => Undo());
        var redo = edit.DropDownItems.Add("&Refazer", null, (_, _) => Redo());
        ((ToolStripMenuItem)undo).ShortcutKeyDisplayString = "Ctrl+Z";
        ((ToolStripMenuItem)redo).ShortcutKeyDisplayString = "Ctrl+Y";
        edit.DropDownOpening += (_, _) => (undo.Enabled, redo.Enabled) = (_history.CanUndo, _history.CanRedo);

        var window = new ToolStripMenuItem("&Janela");
        window.DropDownItems.Add("&Nova janela", null, (_, _) => AddWindow());
        window.DropDownItems.Add("&Remover janela", null, (_, _) => RemoveWindow());

        var module = new ToolStripMenuItem("&Módulo");
        module.DropDownItems.Add("&Gerar código", null, (_, _) => Generate());
        ((ToolStripMenuItem)module.DropDownItems.Add("&Compilar", null, async (_, _) => await BuildAsync(false))).ShortcutKeys = Keys.F6;
        ((ToolStripMenuItem)module.DropDownItems.Add("Compilar e &instalar no jogo", null, async (_, _) => await BuildAsync(true))).ShortcutKeys = Keys.F5;
        module.DropDownItems.Add(new ToolStripSeparator());
        module.DropDownItems.Add("Abrir &user.cpp", null, (_, _) => { if (Generate() is { } dir) Shell(Path.Combine(dir, "user.cpp")); });
        module.DropDownItems.Add("Abrir &pasta do código", null, (_, _) => { if (Generate() is { } dir) Shell(dir); });

        var settings = new ToolStripMenuItem("&Configurações");
        settings.DropDownItems.Add("Pasta do &jogo...", null, (_, _) => PickFolder("Pasta do jogo (com o data.grf)", v => _settings.GameDir = v, _settings.GameDir, OpenResources));
        settings.DropDownItems.Add("Pasta do &MinGW (bin)...", null, (_, _) => PickFolder("Pasta bin do MinGW 32 bits (i686)", v => _settings.MinGwDir = v, _settings.MinGwDir, null));

        var help = new ToolStripMenuItem("Aj&uda");
        ((ToolStripMenuItem)help.DropDownItems.Add("&Como usar", null, (_, _) => MessageBox.Show(this, HelpText, "Como usar"))).ShortcutKeys = Keys.F1;
        help.DropDownItems.Add("Página do &projeto", null, (_, _) => Shell(RepositoryUrl));
        help.DropDownItems.Add("&Sobre", null, (_, _) => MessageBox.Show(this,
            $"GordoKore Studio {Application.ProductVersion.Split('+')[0]}\nSDK {CodeGenerator.SdkVersion} (os módulos exigem o GordoKore com SDK {CodeGenerator.SdkVersion} ou mais novo)\n\nMonta janelas para os módulos do GordoKore.\n\nLicença MIT.\n{RepositoryUrl}",
            "Sobre"));

        menu.Items.AddRange(new ToolStripItem[] { file, edit, window, module, settings, help });
        MainMenuStrip = menu;

        // Esquerda: widgets, imagens da GRF, sprites
        var tabs = new TabControl { Dock = DockStyle.Fill };
        tabs.TabPages.Add(ToolboxPage());
        tabs.TabPages.Add(ResourcesPage());
        tabs.TabPages.Add(SpritesPage());

        // Centro: designer / Lua em cima, log embaixo
        var toolbar = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden };
        var language = new ToolStripComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 90 };
        language.Items.AddRange(new object[] { "Português", "Español", "English" });
        language.SelectedIndex = 0;
        language.SelectedIndexChanged += (_, _) =>
        {
            _canvas.Language = language.SelectedIndex;
            _canvas.Invalidate();
        };
        int[] grids = { 0, 2, 4, 8, 16 };
        var grid = new ToolStripComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 70 };
        grid.Items.AddRange(grids.Select(g => (object)(g == 0 ? "Sem" : $"{g} px")).ToArray());
        grid.SelectedIndex = Array.IndexOf(grids, _canvas.Grid);
        grid.SelectedIndexChanged += (_, _) =>
        {
            _canvas.Grid = grids[grid.SelectedIndex];
            _canvas.Invalidate();
        };
        _windows.SelectedIndexChanged += (_, _) =>
        {
            if (_windows.SelectedItem is WindowModel model && _canvas.Window != model)
            {
                _canvas.Window = model;
                _properties.SelectedObject = model;
            }
        };
        toolbar.Items.AddRange(new ToolStripItem[]
        {
            new ToolStripLabel("Janela:"), _windows, new ToolStripSeparator(),
            new ToolStripLabel("Idioma:"), language, new ToolStripSeparator(),
            new ToolStripLabel("Grade:"), grid, new ToolStripSeparator(),
            new ToolStripButton("Compilar e instalar (F5)", null, async (_, _) => await BuildAsync(true))
        });

        // Zoom no canto inferior direito; Ctrl + roda do mouse tambem
        var zoom = new TrackBar { Dock = DockStyle.Right, Width = 160, AutoSize = false, TickStyle = TickStyle.None, Minimum = 1, Maximum = DesignCanvas.MaxZoom, Value = _canvas.Zoom };
        var zoomText = new Label { Dock = DockStyle.Right, Width = 52, TextAlign = ContentAlignment.MiddleLeft, Text = $"{_canvas.Zoom * 100}%" };
        zoom.ValueChanged += (_, _) =>
        {
            _canvas.Zoom = zoom.Value;
            zoomText.Text = $"{zoom.Value * 100}%";
        };
        void ZoomStep(int step) => zoom.Value = Math.Clamp(zoom.Value + step, zoom.Minimum, zoom.Maximum);
        // Roda no slider: um nivel por clique (o TrackBar andaria 3)
        zoom.MouseWheel += (_, e) =>
        {
            ((HandledMouseEventArgs)e).Handled = true;
            ZoomStep(Math.Sign(e.Delta));
        };
        var zoomBar = new Panel { Dock = DockStyle.Bottom, Height = 28 };
        zoomBar.Controls.Add(zoom);
        zoomBar.Controls.Add(zoomText);

        var scroll = new ZoomPanel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = _canvas.BackColor, Zoom = ZoomStep };
        scroll.Controls.Add(_canvas);
        var designPage = new TabPage("Janela");
        designPage.Controls.Add(scroll);
        designPage.Controls.Add(toolbar);
        designPage.Controls.Add(zoomBar);
        var luaPage = new TabPage("Script Lua");
        luaPage.Controls.Add(_lua);
        var center = new TabControl { Dock = DockStyle.Fill };
        center.TabPages.Add(designPage);
        center.TabPages.Add(luaPage);

        var centerSplit = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 560 };
        centerSplit.Panel1.Controls.Add(center);
        centerSplit.Panel2.Controls.Add(_log);

        var rightSplit = new SplitContainer { Dock = DockStyle.Fill, FixedPanel = FixedPanel.Panel2 };
        rightSplit.Panel1.Controls.Add(centerSplit);
        rightSplit.Panel2.Controls.Add(_properties);

        var mainSplit = new SplitContainer { Dock = DockStyle.Fill, FixedPanel = FixedPanel.Panel1 };
        mainSplit.Panel1.Controls.Add(tabs);
        mainSplit.Panel2.Controls.Add(rightSplit);

        Controls.Add(mainSplit);
        Controls.Add(menu);
        Load += (_, _) =>
        {
            mainSplit.SplitterDistance = 300;
            rightSplit.SplitterDistance = rightSplit.Width - 320;
            centerSplit.SplitterDistance = centerSplit.Height - 160;
        };
    }

    private static readonly (WidgetType type, string label)[] Toolbox =
    {
        (WidgetType.Label, "Texto"),
        (WidgetType.Button, "Botão"),
        (WidgetType.ImageButton, "Botão de imagem"),
        (WidgetType.CheckBox, "Liga/desliga"),
        (WidgetType.Edit, "Campo de texto"),
        (WidgetType.ListBox, "Lista"),
        (WidgetType.Image, "Imagem"),
        (WidgetType.Sprite, "Sprite (NPC/monstro)"),
        (WidgetType.Rect, "Retângulo / linha"),
        (WidgetType.ScrollArea, "Área rolável"),
        (WidgetType.DataList, "Lista de dados"),
        (WidgetType.ItemIcon, "Ícone de item"),
        (WidgetType.Strip, "Faixa (3 imagens)")
    };

    private const string RepositoryUrl = "https://github.com/Brunnexo/GordoKore-Studio";

    private const string HelpText =
        "Widgets: clique para pôr na janela. Arraste para mover; o canto azul redimensiona. Setas movem 1 px (Shift: 8). " +
        "Delete apaga. Ctrl solta da grade e das guias (linha rosa).\n\n" +
        "Área rolável e lista de dados: o clique seleciona o conjunto; o duplo clique abre para editar o que tem dentro " +
        "(Esc ou clique fora fecha). Na lista de dados, a 1ª linha é o modelo: {coluna} nos textos vira o valor de cada " +
        "registro (ex.: {nome}, #{id}); \"{lista.total}\" num texto mostra quantos registros ela tem.\n\n" +
        "Janela redimensionável (propriedades da janela): cada widget segue a sua Âncora.\n\n" +
        "Ações: comando do OpenKore, Lua (aba Script Lua), C++ (user.cpp), arrastar/descrição/drops do item da linha, " +
        "limpar um campo ou esconder os outros jogadores no mapa (só na sua tela).\n\n" +
        "Lua: a aba Script Lua guarda as funções; a propriedade \"Ao abrir\" da janela chama uma delas quando ela abre. " +
        "No script há dados do mapa (gk.me, gk.players, gk.npcs, gk.monsters), timers (gk.every) e internet (gk.http_get, gk.json). " +
        "Manual: docs\\manual-lua.md.\n\n" +
        "Atalhos: Ctrl+Z desfaz, Ctrl+Y ou Ctrl+R refaz, Ctrl+S salva, Ctrl + roda muda o zoom, F6 compila, F5 compila e instala.";

    // Tipos que podem ser celula do modelo de linha de uma lista de dados
    private static readonly WidgetType[] CellTypes =
        { WidgetType.Label, WidgetType.Button, WidgetType.ImageButton, WidgetType.Image, WidgetType.ItemIcon, WidgetType.Rect };

    private TabPage ToolboxPage()
    {
        var page = new TabPage("Widgets");
        var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(8), AutoScroll = true };
        var tips = new ToolTip();
        static Label Header(string text) => new() { Text = text, AutoSize = true, Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold), Margin = new Padding(0, 6, 0, 2) };
        panel.Controls.Add(Header("Widgets"));
        foreach (var (type, label) in Toolbox)
        {
            var button = new Button { Text = label, Width = 260, Height = 28, TextAlign = ContentAlignment.MiddleLeft };
            button.Click += (_, _) => AddWidget(type);
            panel.Controls.Add(button);
        }
        panel.Controls.Add(Header("Modelos"));
        foreach (var (label, tip, apply) in Templates.All)
        {
            var button = new Button { Text = label, Width = 260, Height = 28, TextAlign = ContentAlignment.MiddleLeft };
            tips.SetToolTip(button, tip);
            button.Click += (_, _) => ApplyTemplate(label, apply);
            panel.Controls.Add(button);
        }
        page.Controls.Add(panel);
        return page;
    }

    private void ApplyTemplate(string label, Action<WindowModel> apply)
    {
        var window = _canvas.Window;
        if (window == null)
            return;
        // A "Janela de itens do jogo" troca a janela inteira
        if (label.StartsWith("Janela") && window.Widgets.Count > 0 &&
            MessageBox.Show(this, $"O modelo \"{label}\" substitui os widgets da janela \"{window.Key}\". Continuar?", Text, MessageBoxButtons.YesNo) != DialogResult.Yes)
            return;
        apply(window);
        _canvas.Window = window; // sai da edicao de conteiner e recalcula o tamanho
        _properties.SelectedObject = window;
        RefreshAfterEdit();
        Log($"Modelo \"{label}\" aplicado na janela \"{window.Key}\".");
    }

    private TabPage ResourcesPage()
    {
        var page = new TabPage("Imagens");
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 64, Padding = new Padding(2) };
        foreach (var (label, apply) in new (string, Action<string>)[]
                 {
                     ("Imagem", path => SetSelected(w => { w.Image = path; FitToImage(w, path); })),
                     ("Em cima", path => SetSelected(w => w.ImageHover = path)),
                     ("Apertado", path => SetSelected(w => w.ImagePress = path)),
                     ("Ícone do menu", path => { if (_canvas.Window != null) { _canvas.Window.MenuIcon = path; RefreshAfterEdit(); } })
                 })
        {
            var button = new Button { Text = label, AutoSize = true };
            button.Click += (_, _) => { if (_resources.SelectedItem is string path) apply(path); };
            buttons.Controls.Add(button);
        }
        _resourceFilter.TextChanged += (_, _) => FilterResources();
        _resources.SelectedIndexChanged += (_, _) => _resourcePreview.Image = _resources.SelectedItem is string path ? Texture(path) : null;
        _resources.DoubleClick += (_, _) => { if (_resources.SelectedItem is string path) SetSelected(w => { w.Image = path; FitToImage(w, path); }); };
        page.Controls.Add(_resources);
        page.Controls.Add(_resourceFilter);
        page.Controls.Add(_resourcePreview);
        page.Controls.Add(buttons);
        return page;
    }

    private TabPage SpritesPage()
    {
        var page = new TabPage("Sprites");
        var use = new Button { Text = "Usar no sprite selecionado", Dock = DockStyle.Bottom, Height = 30 };
        use.Click += (_, _) => { if (_spriteList.SelectedItem is string name) SetSelected(w => w.Sprite = name); };
        _spriteFilter.TextChanged += (_, _) => FilterSprites();
        _spriteList.SelectedIndexChanged += (_, _) => _spritePreview.Image = _spriteList.SelectedItem is string name ? SpriteImage(name) : null;
        _spriteList.DoubleClick += (_, _) => { if (_spriteList.SelectedItem is string name) SetSelected(w => w.Sprite = name); };
        page.Controls.Add(_spriteList);
        page.Controls.Add(_spriteFilter);
        page.Controls.Add(_spritePreview);
        page.Controls.Add(use);
        return page;
    }

    // ---------------------------------------------------------------------------------------------
    // Recursos da GRF
    // ---------------------------------------------------------------------------------------------

    private void OpenResources()
    {
        _store?.Dispose();
        _store = null;
        _textures.Clear();
        _sprites.Clear();
        try
        {
            Cursor = Cursors.WaitCursor;
            _store = new ResourceStore(_settings.GameDir);
            foreach (string skipped in _store.Skipped)
                Log(skipped);
            _texturePaths = _store.List(ResourceStore.TextureDir, ".bmp", ".tga").Select(p => p[ResourceStore.TextureDir.Length..]).ToList();
            _canvas.Items = ItemTable.Load(_store);
            _spriteNames = _store.List(Sprite.NpcDir, ".spr").Concat(_store.List(Sprite.MonsterDir, ".spr"))
                .Select(Path.GetFileNameWithoutExtension).OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase).Order().ToList();
            Log($"GRF: {_texturePaths.Count} imagens e {_spriteNames.Count} sprites em {_settings.GameDir}");
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            Log($"Sem acesso aos recursos do jogo ({e.Message}). Configurações > Pasta do jogo.");
            _texturePaths = new();
            _spriteNames = new();
        }
        finally
        {
            Cursor = Cursors.Default;
        }
        FilterResources();
        FilterSprites();
        _canvas.Invalidate();
    }

    private Bitmap? Texture(string path)
    {
        if (path == "")
            return null;
        if (!_textures.TryGetValue(path, out var image))
        {
            // Icone do menu pode vir de arquivo no disco
            image = Path.IsPathRooted(path) && File.Exists(path)
                ? Images.Load(File.ReadAllBytes(path), path) ?? TryLoadAny(path)
                : _store == null ? null : Images.Load(_store.Read(ResourceStore.TextureDir + path), path);
            _textures[path] = image;
        }
        return image;
    }

    private static Bitmap? TryLoadAny(string path)
    {
        try
        {
            return new Bitmap(path);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private Bitmap? SpriteImage(string name)
    {
        if (name == "" || _store == null)
            return null;
        if (!_sprites.TryGetValue(name, out var image))
            _sprites[name] = image = Sprite.Load(_store, name.ToLowerInvariant());
        return image;
    }

    private void FilterResources()
    {
        string filter = _resourceFilter.Text.Trim();
        _resources.BeginUpdate();
        _resources.Items.Clear();
        foreach (string path in _texturePaths.Where(p => filter == "" || p.Contains(filter, StringComparison.OrdinalIgnoreCase)).Take(MaxListed))
            _resources.Items.Add(path);
        _resources.EndUpdate();
    }

    private void FilterSprites()
    {
        string filter = _spriteFilter.Text.Trim();
        _spriteList.BeginUpdate();
        _spriteList.Items.Clear();
        foreach (string name in _spriteNames.Where(n => filter == "" || n.Contains(filter, StringComparison.OrdinalIgnoreCase)).Take(MaxListed))
            _spriteList.Items.Add(name);
        _spriteList.EndUpdate();
    }

    // ---------------------------------------------------------------------------------------------
    // Edicao
    // ---------------------------------------------------------------------------------------------

    private void LoadProject(Project project)
    {
        _project = project;
        if (_project.Windows.Count == 0)
            _project.Windows.Add(new WindowModel());
        _lua.Text = _project.Lua.ReplaceLineEndings("\r\n"); // o TextBox so quebra linha com \r\n
        RefreshWindowList();
        _windows.SelectedIndex = 0;
        _canvas.Window = _project.Windows[0];
        _properties.SelectedObject = _canvas.Window;
        _dirty = false;
        _history.Reset(_project.ToJson());
        UpdateTitle();
    }

    // Ctrl+Z / Ctrl+Y (ou Ctrl+R): historico do projeto; num campo de texto editavel vale o desfazer do proprio campo
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData is (Keys.Control | Keys.Z) or (Keys.Control | Keys.Y) or (Keys.Control | Keys.R)
            && FromHandle(msg.HWnd) is not TextBoxBase { ReadOnly: false })
        {
            if (keyData == (Keys.Control | Keys.Z))
                Undo();
            else
                Redo();
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void Undo() => Restore(_history.Undo());

    private void Redo() => Restore(_history.Redo());

    // Volta o projeto para uma foto do historico mantendo a janela, a selecao e o conteiner aberto (pelo id)
    private void Restore(string? json)
    {
        if (json == null)
            return;
        int window = Math.Max(0, _project.Windows.IndexOf(_canvas.Window!)), selected = _canvas.Selected?.Id ?? 0, editing = _canvas.EditingArea?.Id ?? 0;
        var before = _canvas.Window!.Widgets.Select(w => w.Id).ToHashSet();
        var project = Project.FromJson(json);
        (project.Path, project.Name, project.Lua) = (_project.Path, _project.Name, project.Lua.ReplaceLineEndings("\r\n"));
        _project = project;
        if (_lua.Text != project.Lua)
            _lua.Text = project.Lua; // igual ao projeto: o TextChanged nao conta como edicao
        RefreshWindowList();
        var model = project.Windows[Math.Min(window, project.Windows.Count - 1)];
        _windows.SelectedItem = model;
        _canvas.EditingArea = model.Widgets.FirstOrDefault(w => w.Id == editing && w.IsContainer);
        // widget que voltou (Delete desfeito) fica selecionado
        _canvas.Selected = model.Widgets.FirstOrDefault(w => w.Id == selected) ?? model.Widgets.FirstOrDefault(w => !before.Contains(w.Id));
        _canvas.UpdateSize();
        _dirty = true;
        UpdateTitle();
    }

    private void RefreshWindowList()
    {
        var selected = _windows.SelectedItem;
        _windows.Items.Clear();
        foreach (var window in _project.Windows)
            _windows.Items.Add(window);
        if (selected != null && _windows.Items.Contains(selected))
            _windows.SelectedItem = selected;
    }

    private void AddWindow()
    {
        var window = new WindowModel { Key = "janela" + (_project.Windows.Count + 1) };
        _project.Windows.Add(window);
        RefreshWindowList();
        _windows.SelectedItem = window;
        MarkDirty();
    }

    private void RemoveWindow()
    {
        if (_canvas.Window == null || _project.Windows.Count == 1)
            return;
        if (MessageBox.Show(this, $"Remover a janela \"{_canvas.Window.Key}\"?", Text, MessageBoxButtons.YesNo) != DialogResult.Yes)
            return;
        _project.Windows.Remove(_canvas.Window);
        RefreshWindowList();
        _windows.SelectedIndex = 0;
        MarkDirty();
    }

    private void AddWidget(WidgetType type)
    {
        var window = _canvas.Window;
        if (window == null)
            return;
        int id = window.Widgets.Count == 0 ? 1 : window.Widgets.Max(w => w.Id) + 1;
        var widget = new Widget { Type = type, Id = id, X = 12, Y = 26 + window.Widgets.Count * 6 % Math.Max(1, window.Height - 40) };
        widget.Name = Templates.UniqueName(window, type.ToString().ToLowerInvariant() + id);
        // Com um conteiner aberto ou selecionado (ou um widget dele), o novo entra nele
        var container = _canvas.EditingArea
                        ?? (_canvas.Selected is { IsContainer: true } selected ? selected : window.Widgets.FirstOrDefault(w => w.Id == _canvas.Selected?.Parent));
        bool cell = container is { Type: WidgetType.DataList };
        if (cell && !CellTypes.Contains(type))
        {
            Log("Na linha de uma lista de dados só entram texto, botão, botão de imagem, imagem, ícone de item e retângulo.");
            return;
        }
        if (container != null && (cell || !widget.IsContainer))
            (widget.Parent, widget.X, widget.Y) = (container.Id, 4, cell ? 4 : 4 + window.Widgets.Count(w => w.Parent == container.Id) * 6);
        switch (type)
        {
            case WidgetType.Label: widget.Text = cell ? new Texts("{nome}") : new Texts("Texto", "Texto", "Text"); break;
            case WidgetType.Button: widget.Text = new Texts("Botão", "Botón", "Button"); widget.Width = 80; widget.Height = 20; break;
            case WidgetType.ImageButton: widget.Width = widget.Height = 24; break;
            case WidgetType.Edit: widget.Width = 120; widget.Height = 18; break;
            case WidgetType.ListBox: widget.Width = 150; widget.Height = 80; break;
            case WidgetType.Sprite: widget.Width = 80; widget.Height = 100; widget.Sprite = "poring"; break;
            case WidgetType.Rect: widget.Width = 100; widget.Height = 1; widget.Color = 0x9CA3B4; break;
            case WidgetType.ScrollArea: widget.Width = 160; widget.Height = 100; break;
            case WidgetType.DataList:
                (widget.Width, widget.Height, widget.RowHeight, widget.Source) = (Math.Min(240, window.Width - 24), 128, 32, DataSource.Items);
                break;
            case WidgetType.ItemIcon: widget.Item = cell ? "{id}" : "501"; break;
            case WidgetType.Strip:
                (widget.Width, widget.Image, widget.ImageMid, widget.ImageRight) = (Math.Min(200, window.Width - 24),
                    ResourceStore.UiDir + "basic_interface\\btnbar_left2.bmp", ResourceStore.UiDir + "basic_interface\\btnbar_mid2.bmp",
                    ResourceStore.UiDir + "basic_interface\\btnbar_right2.bmp");
                break;
        }
        window.Widgets.Add(widget);
        _canvas.UpdateSize();
        _canvas.Selected = widget;
        MarkDirty();
    }

    private void SetSelected(Action<Widget> edit)
    {
        if (_canvas.Selected == null)
        {
            Log("Selecione um widget na janela primeiro.");
            return;
        }
        edit(_canvas.Selected);
        RefreshAfterEdit();
    }

    private void FitToImage(Widget widget, string path)
    {
        if (widget.Type == WidgetType.ImageButton && Texture(path) is { } image)
            (widget.Width, widget.Height) = (image.Width, image.Height);
    }

    private void RefreshAfterEdit()
    {
        _properties.Refresh();
        _canvas.Invalidate();
        MarkDirty();
    }

    // typing: digitacao seguida no script Lua vira um passo so no historico
    private void MarkDirty(bool typing = false)
    {
        _dirty = true;
        _history.Push(_project.ToJson(), typing);
        UpdateTitle();
    }

    private void UpdateTitle() =>
        Text = $"GordoKore Studio - {(_project.Path != null ? Path.GetFileName(_project.Path) : "sem nome")}{(_dirty ? " *" : "")}";

    // ---------------------------------------------------------------------------------------------
    // Arquivos, geracao e build
    // ---------------------------------------------------------------------------------------------

    private bool ConfirmDiscard()
    {
        if (!_dirty)
            return true;
        var answer = MessageBox.Show(this, "Salvar as alterações do projeto?", Text, MessageBoxButtons.YesNoCancel);
        return answer == DialogResult.No || (answer == DialogResult.Yes && SaveProject(false));
    }

    private void OpenProject()
    {
        if (!ConfirmDiscard())
            return;
        using var dialog = new OpenFileDialog { Filter = "Projeto do GordoKore Studio (*.gkproj)|*.gkproj" };
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;
        var project = TryLoad(dialog.FileName);
        if (project == null)
            return;
        LoadProject(project);
        _settings.LastProject = dialog.FileName;
        _settings.Save();
    }

    // projeto antigo ou quebrado nao derruba o estudio
    private Project? TryLoad(string path)
    {
        try
        {
            return Project.Load(path);
        }
        catch (Exception e) when (e is IOException or System.Text.Json.JsonException or UnauthorizedAccessException)
        {
            Log($"Não deu para abrir {path}: {e.Message}");
            return null;
        }
    }

    private bool SaveProject(bool askPath)
    {
        string? path = _project.Path;
        if (askPath || path == null)
        {
            using var dialog = new SaveFileDialog { Filter = "Projeto do GordoKore Studio (*.gkproj)|*.gkproj", FileName = _project.Name + ".gkproj" };
            if (dialog.ShowDialog(this) != DialogResult.OK)
                return false;
            path = dialog.FileName;
            _project.Name = CodeGenerator.Identifier(Path.GetFileNameWithoutExtension(path));
        }
        _project.Save(path);
        _settings.LastProject = path;
        _settings.Save();
        _dirty = false;
        UpdateTitle();
        return true;
    }

    private string? Generate()
    {
        if (!SaveProject(false))
            return null;
        string dir = Path.Combine(Path.GetDirectoryName(_project.Path!)!, _project.Name);
        var problems = CodeGenerator.Validate(_project);
        if (problems.Count > 0)
        {
            foreach (string problem in problems)
                Log(problem);
            return null;
        }
        CodeGenerator.Generate(_project, dir, Texture);
        Log($"Código gerado em {dir}");
        return dir;
    }

    private async Task BuildAsync(bool install)
    {
        string? dir = Generate();
        if (dir == null)
            return;
        Cursor = Cursors.WaitCursor;
        string? dll = await Builder.BuildAsync(_settings, dir, CodeGenerator.Identifier(_project.Name), Log);
        Cursor = Cursors.Default;
        if (dll == null)
        {
            Log("Falhou: veja as mensagens acima.");
            return;
        }
        Log($"Compilado: {dll}");
        if (!install)
            return;
        try
        {
            Directory.CreateDirectory(_settings.ModulesDir);
            string target = Path.Combine(_settings.ModulesDir, Path.GetFileName(dll));
            File.Copy(dll, target, true);
            Log($"Instalado: {target} (vale na próxima abertura do jogo)");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Log($"Não deu para copiar para {_settings.ModulesDir}: {e.Message}");
        }
    }

    private void PickFolder(string description, Action<string> set, string current, Action? after)
    {
        using var dialog = new FolderBrowserDialog { Description = description, SelectedPath = current, UseDescriptionForTitle = true };
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;
        set(dialog.SelectedPath);
        _settings.Save();
        after?.Invoke();
    }

    private static void Shell(string path) => Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });

    private void Log(string text)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => Log(text));
            return;
        }
        _log.AppendText(text + Environment.NewLine);
    }

    // Ctrl + roda vira zoom em vez de rolar (o canvas repassa a roda para o painel)
    private sealed class ZoomPanel : Panel
    {
        public Action<int>? Zoom;

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            if ((ModifierKeys & Keys.Control) != 0)
                Zoom?.Invoke(Math.Sign(e.Delta));
            else
                base.OnMouseWheel(e);
        }
    }
}
