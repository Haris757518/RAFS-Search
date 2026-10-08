using System;
using System.Windows.Forms;
[assembly: System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8", FrameworkDisplayName = ".NET Framework 4.8")]
[assembly: System.Reflection.AssemblyTitle("RAFS Search")]
[assembly: System.Reflection.AssemblyCompany("Haris K")]
[assembly: System.Reflection.AssemblyVersion("0.1.0.0")]
internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        try {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            if (args.Length > 0 && args[0] == "--self-test") return Verification.Run();
            Application.Run(new AppShell()); return 0;
        } catch (Exception error) {
            string directory = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "target", "desktop-verification");
            System.IO.Directory.CreateDirectory(directory);
            System.IO.File.WriteAllText(System.IO.Path.Combine(directory, "startup-error.txt"), error.ToString());
            if (args.Length == 0) MessageBox.Show("RAFS could not start. See target/desktop-verification/startup-error.txt for details.", "RAFS Search", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
    }
}
