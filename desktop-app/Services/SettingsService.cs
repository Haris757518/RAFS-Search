using System;
using System.IO;
using System.Xml.Serialization;
using System.Xml;

public sealed class AppSettings
{
    public int DefaultMethod = 1, DefaultDepth = 5, DefaultThreads = 2, ComparisonRuns = 5;
    public string LastFolder = "", LastPattern = "*";
    public bool RememberFolder = true;
    public int MaximumWorkers = 8;
    public bool DiagnosticLogging;
    public bool DarkTheme;
    public int TimeoutSeconds=300;
}
internal sealed class SettingsService
{
    public readonly string FilePath;
    public AppSettings Settings { get; private set; }
    public string LoadWarning { get; private set; }
    public SettingsService(string path)
    {
        FilePath = path; Settings = new AppSettings();
        string source=path;
        string legacy=Path.Combine(SearchRunner.Root,"target","desktop-settings.xml");
        bool migrate=!File.Exists(path) && Path.GetFullPath(path)==Path.GetFullPath(AppPaths.Settings) && String.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("RAFS_STATE_DIR")) && File.Exists(legacy);
        if(migrate)source=legacy;
        if (File.Exists(source)) {
            try {
                using(var input=new FileStream(source,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete))
                using (var reader = XmlReader.Create(input,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=512*1024})) Settings = (AppSettings)new XmlSerializer(typeof(AppSettings)).Deserialize(reader);
                if (Settings.DefaultMethod < 1 || Settings.DefaultMethod > 5 || Settings.DefaultDepth < 0 || Settings.DefaultDepth > 1000 ||
                    Settings.DefaultThreads < 0 || Settings.DefaultThreads > 256 || Settings.ComparisonRuns < 1 || Settings.ComparisonRuns > 15 || Settings.MaximumWorkers < 1 || Settings.MaximumWorkers > 256 || Settings.TimeoutSeconds<1 || Settings.TimeoutSeconds>3600)
                    throw new InvalidOperationException("Invalid preference values.");
            } catch (Exception e) {
                if (!(e is IOException || e is InvalidOperationException || e is UnauthorizedAccessException || e is XmlException)) throw;
                Settings = new AppSettings(); LoadWarning = "Saved preferences could not be loaded; safe defaults are active.";
            }
        }
        if(migrate && LoadWarning==null){try{Save();}catch(IOException){LoadWarning="Preferences loaded; migration could not be saved.";}catch(UnauthorizedAccessException){LoadWarning="Preferences loaded; the new settings location is not writable.";}}
    }
    public void Save()
    {
        string identity;
        using(var hash=System.Security.Cryptography.SHA256.Create())identity=BitConverter.ToString(hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(Path.GetFullPath(FilePath).ToUpperInvariant()))).Replace("-","");
        using(var mutex=new System.Threading.Mutex(false,"Local\\RAFS-Settings-"+identity)){
            bool acquired=false;
            try{try{acquired=mutex.WaitOne(10000);}catch(System.Threading.AbandonedMutexException){acquired=true;}if(!acquired)throw new IOException("Preferences are busy in another app instance.");SaveAtomic();}
            finally{if(acquired)mutex.ReleaseMutex();}
        }
    }
    private void SaveAtomic()
    {
        string full=Path.GetFullPath(FilePath);Directory.CreateDirectory(Path.GetDirectoryName(full));
        string temporary=full+"."+Guid.NewGuid().ToString("N")+".tmp";
        try {
            using(var stream=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None)) {
                using(var writer=new StreamWriter(stream,new System.Text.UTF8Encoding(false),4096,true)){new XmlSerializer(typeof(AppSettings)).Serialize(writer,Settings);writer.Flush();}
                stream.Flush(true);
            }
            if(File.Exists(full))File.Replace(temporary,full,null);
            else {try{File.Move(temporary,full);}catch(IOException){if(!File.Exists(full))throw;File.Replace(temporary,full,null);}}
        } finally {if(File.Exists(temporary))File.Delete(temporary);}
    }
}
