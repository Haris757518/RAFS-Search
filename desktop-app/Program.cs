using System;
using System.Windows.Forms;
[assembly: System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8", FrameworkDisplayName = ".NET Framework 4.8")]
[assembly: System.Reflection.AssemblyTitle("RAFS Search")]
[assembly: System.Reflection.AssemblyCompany("Haris K")]
[assembly: System.Reflection.AssemblyVersion("1.1.0.0")]
internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        try {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            bool verification=args.Length>0;
            Application.ThreadException+=delegate(object sender,System.Threading.ThreadExceptionEventArgs e){AppDiagnostics.Report(e.Exception,!verification);if(verification)throw new InvalidOperationException("UI verification failed.",e.Exception);};
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            if(args.Length>0 && (args[0]=="--self-test"||args[0]=="--feature-test"||args[0]=="--production-test"||args[0]=="--layout-test"||args[0]=="--soak-test"))Environment.SetEnvironmentVariable("RAFS_STATE_DIR",System.IO.Path.Combine(System.IO.Path.GetTempPath(),"rafs-test-"+Guid.NewGuid().ToString("N").Substring(0,8)));
            Theme.SetDark(new SettingsService(AppPaths.Settings).Settings.DarkTheme);
            if (args.Length > 0 && args[0] == "--self-test") return Verification.Run();
            if(args.Length>0 && args[0]=="--production-test")return ProductionVerification.Run();
            if(args.Length>0 && args[0]=="--layout-test")return ReleaseUiVerification.Run();
            if(args.Length>0 && args[0]=="--soak-test")return SoakVerification.Run();
            if(args.Length>0 && args[0]=="--research-benchmark")return ResearchBenchmark.Run();
            if(args.Length>0 && args[0]=="--benchmark-release")return ProductionVerification.Benchmark();
            if(args.Length>0 && args[0]=="--feature-test"){FeatureVerification.Run(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"target","feature-verification"),false);return 0;}
            Application.Run(new AppShell()); return 0;
        } catch (Exception error) {
            AppDiagnostics.Report(error,args.Length==0);
            return 1;
        }
    }
}
