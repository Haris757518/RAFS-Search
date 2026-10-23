using System;
using System.IO;
using System.Windows.Forms;
internal static class AppDiagnostics {
    public static void Report(Exception error,bool show){
        string target=Path.Combine(AppPaths.StateRoot,"diagnostics","last-error.txt");bool saved=false;
        try{Directory.CreateDirectory(Path.GetDirectoryName(target));string text=DateTime.UtcNow.ToString("o")+"\n"+error;if(text.Length>65536)text=text.Substring(0,65536);File.WriteAllText(target,text);saved=true;}catch(IOException){}catch(UnauthorizedAccessException){}
        if(show)MessageBox.Show(error.Message+(saved?"\n\nDetails: "+target:"\n\nCould not save diagnostics."),"RAFS Search",MessageBoxButtons.OK,MessageBoxIcon.Error);
    }
}
