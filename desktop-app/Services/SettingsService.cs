using System;
using System.IO;
using System.Xml.Serialization;

public sealed class AppSettings
{
    public int DefaultMethod = 1, DefaultDepth = 5, DefaultThreads = 2, ComparisonRuns = 5;
    public string LastFolder = "", LastPattern = "*.rs";
    public bool RememberFolder = true;
    public int MaximumWorkers = 8;
    public bool DiagnosticLogging;
}
internal sealed class SettingsService
{
    public readonly string FilePath;
    public AppSettings Settings { get; private set; }
    public string LoadWarning { get; private set; }
    public SettingsService(string path)
    {
        FilePath = path; Settings = new AppSettings();
        if (File.Exists(path)) {
            try {
                using (var reader = new StreamReader(path)) Settings = (AppSettings)new XmlSerializer(typeof(AppSettings)).Deserialize(reader);
                if (Settings.DefaultMethod < 1 || Settings.DefaultMethod > 2 || Settings.DefaultDepth < 0 || Settings.DefaultDepth > 1000 ||
                    Settings.DefaultThreads < 0 || Settings.DefaultThreads > 256 || Settings.ComparisonRuns < 1 || Settings.ComparisonRuns > 15 || Settings.MaximumWorkers < 1 || Settings.MaximumWorkers > 256)
                    throw new InvalidOperationException("Invalid preference values.");
            } catch (Exception e) {
                if (!(e is IOException || e is InvalidOperationException || e is UnauthorizedAccessException)) throw;
                Settings = new AppSettings(); LoadWarning = "Saved preferences could not be loaded; safe defaults are active.";
            }
        }
    }
    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
        string temporary = FilePath + ".tmp";
        using (var writer = new StreamWriter(temporary)) new XmlSerializer(typeof(AppSettings)).Serialize(writer, Settings);
        if (File.Exists(FilePath)) File.Replace(temporary, FilePath, null); else File.Move(temporary, FilePath);
    }
}
