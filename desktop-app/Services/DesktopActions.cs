using System.Diagnostics;
using System.IO;
internal static class DesktopActions
{
    public static Process Reveal(string path) { return Process.Start(new ProcessStartInfo("explorer.exe", "/select," + SearchRunner.Quote(path)) { UseShellExecute = true }); }
    public static Process OpenFolder() { return Process.Start(new ProcessStartInfo(SearchRunner.Root) { UseShellExecute = true }); }
    public static Process ViewLicense() { return Process.Start(new ProcessStartInfo("notepad.exe", SearchRunner.Quote(Path.Combine(SearchRunner.Root, "LICENSE"))) { UseShellExecute = true }); }
}
