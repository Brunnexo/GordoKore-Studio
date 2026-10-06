using System.Text.Json;

namespace GordoKore.Studio;

/// <summary>Pastas do jogo e das ferramentas de build (%APPDATA%\GordoKore Studio\settings.json).</summary>
public sealed class Settings
{
    public string GameDir { get; set; } = @"C:\Gravity\Ragnarok OpenKore";
    public string MinGwDir { get; set; } = @"C:\Strawberry\c\bin";
    public string CMake { get; set; } = File.Exists(@"C:\Program Files\CMake\bin\cmake.exe") ? @"C:\Program Files\CMake\bin\cmake.exe" : "cmake";
    public string? LastProject { get; set; }

    private static string FilePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GordoKore Studio", "settings.json");

    public static Settings Load()
    {
        try
        {
            return JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) ?? new Settings();
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
            return new Settings();
        }
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }

    public string ModulesDir => Path.Combine(GameDir, "GordoKore", "modules");
}
