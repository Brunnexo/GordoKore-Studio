using System.Text;

namespace GordoKore.Studio;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        if (args.Length > 0 && args[0] == "--check")
            return Checks.Run(args.Length > 1 ? args[1] : null);

        ApplicationConfiguration.Initialize();
        Application.SetColorMode(SystemColorMode.Dark);
        Application.Run(new MainForm(args.FirstOrDefault()));
        return 0;
    }
}
