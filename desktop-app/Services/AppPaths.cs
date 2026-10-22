using System;
using System.IO;

internal static class AppPaths
{
    public static string StateRoot {
        get {
            string overridden=Environment.GetEnvironmentVariable("RAFS_STATE_DIR");
            return Path.GetFullPath(String.IsNullOrWhiteSpace(overridden)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"RAFS-Search")
                : overridden);
        }
    }
    public static string Runs { get { return Path.Combine(StateRoot,"runs"); } }
    public static string Settings { get { return Path.Combine(StateRoot,"settings.xml"); } }
    public static string Workloads { get { return Path.Combine(StateRoot,"benchmark-data"); } }
}
