using System;
using System.IO;

internal sealed class SearchRequest
{
    public string Folder, Pattern;
    public int Method = 1, Depth = 5, Threads = 2;
    public int MaximumWorkers = 8;
    public int TimeoutSeconds = 300;
    public string DuplicateMode;
    public bool Content, CaseSensitive;
    public SearchRequest Copy() { return (SearchRequest)MemberwiseClone(); }
    public string MethodName { get { return Method == 5 ? "RAFS Adaptive" : Method == 4 ? "Windows Search Index (Explorer backend)" : Method == 3 ? "Traditional PowerShell Search" : Method == 1 ? "Method 1 — WalkDir + Rayon" : "Method 2 — Recursive DFS"; } }
    public string ThreadLabel { get { return Method == 5 ? "Adaptive, cap " + MaximumWorkers : Method == 4 ? "Windows index service" : Method != 1 ? "1 · sequential" : Threads == 0 ? "Automatic" : Threads.ToString(); } }
    public void Validate()
    {
        if (String.IsNullOrWhiteSpace(Folder) || !Directory.Exists(Folder)) throw new ArgumentException("Choose an existing search folder.");
        if (String.IsNullOrWhiteSpace(Pattern)) throw new ArgumentException("Enter a filename pattern or text to search for.");
        if (Method < 1 || Method > 5) throw new ArgumentException("Choose a supported search method.");
        if (MaximumWorkers < 1 || MaximumWorkers > 256) throw new ArgumentException("Adaptive worker cap must be 1–256.");
        if(TimeoutSeconds<1||TimeoutSeconds>3600)throw new ArgumentException("Search timeout must be 1–3600 seconds.");
        if(DuplicateMode!=null && DuplicateMode!="content" && DuplicateMode!="name" && DuplicateMode!="size")throw new ArgumentException("Choose a supported duplicate mode.");
        if(Content && (Method==3||Method==4))throw new ArgumentException("PowerShell and Windows Search Index support filename patterns only; uncheck Search file contents.");
        if (Depth < 0 || Depth > 1000 || Threads < 0 || Threads > 256) throw new ArgumentException("Search settings are outside the supported range.");
        Folder = Path.GetFullPath(Folder);
    }
}
