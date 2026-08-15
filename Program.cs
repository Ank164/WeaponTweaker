namespace WeaponTweaker;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Contains("--self-test", StringComparer.OrdinalIgnoreCase))
        {
            Environment.ExitCode = SelfTest.Run();
            return;
        }
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm(args.FirstOrDefault(File.Exists)));
    }
}
