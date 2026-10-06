using System.IO.Compression;
using System.Text;

namespace GordoKore.Studio.Grf;

/// <summary>
/// Leitor de GRF 0x200 e 0x300 ("Event Horizon", offsets de 64 bits do data.grf do LATAM). Port do
/// GordoExtractor/extractor/grf/reader.py.
/// </summary>
public sealed class GrfArchive : IDisposable
{
    private const int HeaderSize = 0x2E;
    private const byte FlagFile = 0x01;
    private const byte FlagMixed = 0x02;  // DES legado: 20 blocos + ciclo
    private const byte FlagHeader = 0x04; // DES legado: so os 20 primeiros blocos

    public static readonly Encoding Cp949;

    private readonly FileStream _file;
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);

    public string Path { get; }

    public IEnumerable<string> Names => _entries.Values.Select(e => e.Name);

    private readonly record struct Entry(string Name, int SizeCompressed, int SizeAligned, int SizeDecompressed, byte Flags, long Offset);

    static GrfArchive()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        Cp949 = Encoding.GetEncoding(949);
    }

    public GrfArchive(string path)
    {
        Path = path;
        _file = File.OpenRead(path);
        var header = new byte[HeaderSize];
        _file.ReadExactly(header);

        uint version = BitConverter.ToUInt32(header, 0x2A);
        if (version >> 8 is not (2 or 3))
        {
            _file.Dispose();
            throw new NotSupportedException($"{path}: GRF versão 0x{version:X} não suportada (só 0x200 e 0x300)");
        }
        bool is300 = version >> 8 == 3;
        // No 0x300 o offset da tabela é int64 em 0x1E (o antigo "seed" vira a parte alta)
        long tableOffset = is300 && header[35] == 0 && header[36] == 0 && header[37] == 0
            ? BitConverter.ToInt64(header, 0x1E)
            : BitConverter.ToUInt32(header, 0x1E);

        _file.Position = tableOffset + HeaderSize;
        var sizes = new byte[is300 ? 12 : 8];
        _file.ReadExactly(sizes);
        int skip = is300 ? 4 : 0;
        int compressed = BitConverter.ToInt32(sizes, skip);
        int real = BitConverter.ToInt32(sizes, skip + 4);
        var packed = new byte[compressed];
        _file.ReadExactly(packed);
        byte[] table = Inflate(packed, real);
        if (table.Length != real)
            throw new InvalidDataException($"{path}: tabela com tamanho inesperado (GRF criptografado?)");
        ParseTable(table, is300);
    }

    public static string Normalize(string name) => name.Replace('/', '\\').Trim().ToLowerInvariant();

    public bool Contains(string name) => _entries.ContainsKey(Normalize(name));

    public byte[]? Read(string name)
    {
        if (!_entries.TryGetValue(Normalize(name), out var entry))
            return null;
        var raw = new byte[entry.SizeAligned];
        lock (_file)
        {
            _file.Position = entry.Offset;
            _file.ReadExactly(raw);
        }
        if ((entry.Flags & FlagMixed) != 0)
            GrfDes.DecodeFull(raw, entry.SizeCompressed);
        else if ((entry.Flags & FlagHeader) != 0)
            GrfDes.DecodeHeader(raw);
        return Inflate(raw, entry.SizeDecompressed);
    }

    private void ParseTable(byte[] table, bool is300)
    {
        int tail = is300 ? 21 : 17;
        int o = 0;
        while (o < table.Length)
        {
            int end = Array.IndexOf(table, (byte)0, o);
            if (end < 0)
                break;
            string name = Cp949.GetString(table, o, end - o).Replace('/', '\\');
            o = end + 1;
            if (o + 13 > table.Length)
                break;
            int sizeCompressed = BitConverter.ToInt32(table, o);
            int sizeAligned = BitConverter.ToInt32(table, o + 4);
            int sizeDecompressed = BitConverter.ToInt32(table, o + 8);
            byte flags = table[o + 12];
            long offset = (is300 ? BitConverter.ToInt64(table, o + 13) : BitConverter.ToUInt32(table, o + 13)) + HeaderSize;
            o += tail;
            if ((flags & FlagFile) != 0)
                _entries[Normalize(name)] = new Entry(name, sizeCompressed, sizeAligned, sizeDecompressed, flags, offset);
        }
    }

    // Tolerante a stream sem o marcador de fim (alguns arquivos do GRF)
    private static byte[] Inflate(byte[] data, int size)
    {
        var output = new byte[size];
        using var zlib = new ZLibStream(new MemoryStream(data), CompressionMode.Decompress);
        int read = 0;
        try
        {
            while (read < size)
            {
                int n = zlib.Read(output, read, size - read);
                if (n == 0)
                    break;
                read += n;
            }
        }
        catch (InvalidDataException) when (read > 0)
        {
        }
        return read == size ? output : output[..read];
    }

    public void Dispose() => _file.Dispose();
}
