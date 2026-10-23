using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

internal static class WindowsIndexSearchRunner
{
    private static string Literal(string value) { return "'" + value.Replace("'", "''") + "'"; }
    public static SearchReport Run(SearchRequest request, Action<Process> processChanged, Func<bool> cancelled)
    {
        request.Validate();
        if (request.Content) throw new ArgumentException("Windows Search Index baseline supports filename patterns only. Uncheck Search file contents.");
        if (request.Pattern.IndexOfAny(new[] { '[', ']' }) >= 0) throw new ArgumentException("Windows Search Index baseline supports * and ? filename wildcards; bracket patterns are not supported.");
        string pattern = request.Pattern;
        if (!pattern.Contains("*") && !pattern.Contains("?")) pattern = "*" + pattern + "*";
        string regex = "^" + Regex.Escape(pattern).Replace(@"\*", ".*").Replace(@"\?", ".") + "$";
        string root = request.Folder.TrimEnd('\\', '/') + "\\";
        string scope = "file:" + request.Folder.Replace('\\', '/').TrimEnd('/') + "/";
        string sql = "SELECT System.ItemPathDisplay, System.FileName, System.FileAttributes FROM SYSTEMINDEX WHERE SCOPE='" + scope.Replace("'", "''") + "'";
        string script = "$ErrorActionPreference='Stop'; [Console]::OutputEncoding=New-Object System.Text.UTF8Encoding($false); " +
            "$root=" + Literal(root) + "; $rx=New-Object System.Text.RegularExpressions.Regex(" + Literal(regex) + ", " + (request.CaseSensitive ? "0" : "1") + "); " +
            "$cn=New-Object System.Data.OleDb.OleDbConnection('Provider=Search.CollatorDSO;Extended Properties=\"Application=Windows\";'); " +
            "try { $cn.Open(); $cmd=$cn.CreateCommand(); $cmd.CommandTimeout=15; $cmd.CommandText=" + Literal(sql) + "; " +
            "$reader=$cmd.ExecuteReader(); try { while($reader.Read()) { " +
            "$path=[string]$reader['System.ItemPathDisplay']; $name=[string]$reader['System.FileName']; " +
            "if($reader['System.FileAttributes'] -is [DBNull]) { throw 'Index entry has no file attributes; cannot verify files-only results.' }; " +
            "if(([int64]$reader['System.FileAttributes'] -band 16) -ne 0) { continue }; " +
            "if(!$path.StartsWith($root,[StringComparison]::OrdinalIgnoreCase)) { continue }; " +
            "$relative=$path.Substring($root.Length); $depth=($relative.Split([char]92)).Length-1; " +
            "if($depth -le " + request.Depth + " -and $rx.IsMatch($name)) { [Console]::WriteLine([IO.Path]::GetFullPath($path)) } " +
            "} } finally { $reader.Dispose(); $cmd.Dispose() } } finally { $cn.Dispose() }";
        var report=PowerShellSearchRunner.RunScript(script, processChanged, cancelled,request.TimeoutSeconds);
        if(!report.Succeeded)report.Error="Windows Search Index query failed. Check Windows Search availability and folder indexing. Details: "+report.Error;
        return report;
    }
}
