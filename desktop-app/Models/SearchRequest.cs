using System;
using System.IO;

internal sealed class SearchRequest
{
    public string Folder, Pattern;
    public int Method = 1, Depth = 5, Threads = 2;
    public bool Content, CaseSensitive;
    public SearchRequest Copy() { return (SearchRequest)MemberwiseClone(); }
    public string MethodName { get { return Method == 1 ? "Method 1 — WalkDir + Rayon" : "Method 2 — Recursive DFS"; } }
    public string ThreadLabel { get { return Method == 2 ? "1 · sequential" : Threads == 0 ? "Automatic" : Threads.ToString(); } }
    public void Validate()
    {
        if (String.IsNullOrWhiteSpace(Folder) || !Directory.Exists(Folder)) throw new ArgumentException("Choose an existing search folder.");
        if (String.IsNullOrWhiteSpace(Pattern)) throw new ArgumentException("Enter a filename pattern or text to search for.");
        if (Method != 1 && Method != 2) throw new ArgumentException("Choose a supported baseline method.");
        if (Depth < 0 || Depth > 1000 || Threads < 0 || Threads > 256) throw new ArgumentException("Search settings are outside the supported range.");
        Folder = Path.GetFullPath(Folder);
    }
}
