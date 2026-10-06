using System.Diagnostics;

namespace GordoKore.Studio.Generation;

/// <summary>Compila o projeto gerado com CMake + MinGW de 32 bits (o mesmo do bridge.dll).</summary>
public static class Builder
{
    public static async Task<string?> BuildAsync(Settings settings, string sourceDir, string name, Action<string> output)
    {
        string buildDir = Path.Combine(sourceDir, "build");
        string compiler = Tool(settings.MinGwDir, "i686-w64-mingw32-g++.exe", "g++.exe");
        string make = Tool(settings.MinGwDir, "gmake.exe", "mingw32-make.exe", "make.exe");
        if (!File.Exists(compiler) || !File.Exists(make))
        {
            output($"MinGW não encontrado em {settings.MinGwDir} (precisa do g++ e do make). Ajuste em Configurações.");
            return null;
        }

        if (!File.Exists(Path.Combine(buildDir, "CMakeCache.txt")))
        {
            int configured = await RunAsync(settings, output, "-S", sourceDir, "-B", buildDir, "-G", "MinGW Makefiles",
                $"-DCMAKE_CXX_COMPILER={compiler}", $"-DCMAKE_MAKE_PROGRAM={make}", "-DCMAKE_BUILD_TYPE=Release");
            if (configured != 0)
                return null;
        }
        if (await RunAsync(settings, output, "--build", buildDir) != 0)
            return null;

        string dll = Path.Combine(buildDir, name + ".dll");
        return File.Exists(dll) ? dll : null;
    }

    private static string Tool(string dir, params string[] names) =>
        names.Select(n => Path.Combine(dir, n)).FirstOrDefault(File.Exists) ?? Path.Combine(dir, names[0]);

    private static async Task<int> RunAsync(Settings settings, Action<string> output, params string[] args)
    {
        var info = new ProcessStartInfo(settings.CMake)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (string arg in args)
            info.ArgumentList.Add(arg);
        // O g++ do MinGW acha as DLLs dele pelo PATH
        info.Environment["PATH"] = settings.MinGwDir + ";" + Environment.GetEnvironmentVariable("PATH");

        output("> cmake " + string.Join(' ', args));
        using var process = new Process { StartInfo = info };
        process.OutputDataReceived += (_, e) => { if (e.Data != null) output(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data != null) output(e.Data); };
        try
        {
            process.Start();
        }
        catch (System.ComponentModel.Win32Exception e)
        {
            output($"CMake não encontrado ({settings.CMake}): {e.Message}");
            return -1;
        }
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync();
        return process.ExitCode;
    }
}
