namespace GordoKore.Studio.Grf;

/// <summary>
/// Arquivos do jogo como o cliente ve: pasta data\ solta primeiro, depois os GRFs (event, maps, data).
/// Caminhos completos ("data\texture\...").
/// </summary>
public sealed class ResourceStore : IDisposable
{
    public const string TextureDir = "data\\texture\\";
    public const string UiDir = "유저인터페이스\\";

    private readonly string _gameDir;
    private readonly List<GrfArchive> _grfs = new();

    /// <summary>GRFs que ficaram de fora (versao antiga, como o event.grf 0x102), com o motivo.</summary>
    public List<string> Skipped { get; } = new();

    public ResourceStore(string gameDir)
    {
        _gameDir = gameDir;
        foreach (string name in new[] { "event.grf", "maps.grf", "data.grf" })
        {
            string path = System.IO.Path.Combine(gameDir, name);
            if (!File.Exists(path))
                continue;
            try
            {
                _grfs.Add(new GrfArchive(path));
            }
            catch (NotSupportedException e)
            {
                Skipped.Add(e.Message);
            }
        }
        if (_grfs.Count == 0)
            throw new FileNotFoundException($"Nenhum .grf legível em {gameDir}");
    }

    public byte[]? Read(string path)
    {
        string loose = System.IO.Path.Combine(_gameDir, path.Replace('/', '\\'));
        if (File.Exists(loose))
            return File.ReadAllBytes(loose);
        foreach (var grf in _grfs)
            if (grf.Read(path) is { } data)
                return data;
        return null;
    }

    /// <summary>Caminhos sob um prefixo (ex.: data\texture\유저인터페이스\), sem repetir, ordenados.</summary>
    public List<string> List(string prefix, params string[] extensions)
    {
        string normalized = GrfArchive.Normalize(prefix);
        return _grfs.SelectMany(g => g.Names)
            .Where(n => GrfArchive.Normalize(n).StartsWith(normalized, StringComparison.Ordinal)
                        && (extensions.Length == 0 || extensions.Any(e => n.EndsWith(e, StringComparison.OrdinalIgnoreCase))))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public void Dispose()
    {
        foreach (var grf in _grfs)
            grf.Dispose();
    }
}
