using System.Text;

namespace GordoKore.Studio.Grf;

/// <summary>
/// Itens do cliente pela GRF, para a previa das listas de dados: nome (data\idnum2itemdisplaynametable.txt, CP1252,
/// "_" no lugar do espaco) e icone (data\idnum2itemresnametable.txt, CP949 -> item\&lt;recurso&gt;.bmp). No jogo a lista vem
/// da tabela do cliente (itemInfo); os nomes podem diferir um pouco.
/// </summary>
public sealed class ItemTable
{
    public static readonly ItemTable Empty = new(new(), new());

    public List<(int Id, string Name)> Items { get; }
    private readonly Dictionary<int, string> _resources;

    private ItemTable(List<(int, string)> items, Dictionary<int, string> resources) => (Items, _resources) = (items, resources);

    /// <summary>Caminho do icone relativo a data\texture\ (ou null).</summary>
    public string? IconPath(int id) => _resources.TryGetValue(id, out var res) ? ResourceStore.UiDir + "item\\" + res + ".bmp" : null;

    public static ItemTable Load(ResourceStore store)
    {
        var names = Parse(store.Read("data\\idnum2itemdisplaynametable.txt"), Encoding.GetEncoding(1252));
        var resources = Parse(store.Read("data\\idnum2itemresnametable.txt"), Encoding.GetEncoding(949));
        var items = names.OrderBy(p => p.Key).Select(p => (p.Key, p.Value.Replace('_', ' '))).ToList();
        return new ItemTable(items, resources);
    }

    // "id#texto#" por linha; "//" comenta; o primeiro de cada ID vale
    private static Dictionary<int, string> Parse(byte[]? data, Encoding encoding)
    {
        var table = new Dictionary<int, string>();
        if (data == null)
            return table;
        foreach (string line in encoding.GetString(data).Split('\n'))
        {
            string[] parts = line.Trim().Split('#');
            if (parts.Length >= 2 && !parts[0].StartsWith("//") && int.TryParse(parts[0], out int id) && parts[1] != "")
                table.TryAdd(id, parts[1]);
        }
        return table;
    }
}
