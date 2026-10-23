using System;
using System.IO;
using System.Text;
internal static class DatasetGenerator {
 public static string Generate(string kind,int count,Action<int> progress,Func<bool> cancelled){
  if(count<100||count>50000)throw new ArgumentException("Choose 100–50,000 files.");
  if(kind!="Deep"&&kind!="Wide"&&kind!="Mixed"&&kind!="Small Files")throw new ArgumentException("Unknown topology.");
  string root=Path.Combine(AppPaths.Workloads,kind.Replace(" ","-")+"-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N").Substring(0,6));
  // Keep generated workloads usable by legacy Windows PowerShell as well as Rust.
  int deepLevels=Math.Min(24,(240-root.Length-24)/4);
  if(root.Length>190||(kind=="Deep"&&deepLevels<8))throw new ArgumentException("The benchmark-data path is too long for all comparison tools. Set RAFS_STATE_DIR to a shorter writable folder before starting RAFS.");
  Directory.CreateDirectory(root);
  File.WriteAllText(Path.Combine(root,"DATASET_MANIFEST.md"),"Generated RAFS benchmark workload. Synthetic text, no academic/private content.\nTopology: "+kind+"\nFiles: "+count+"\nQuery: *.txt\nDepth: 100\n",Encoding.UTF8);
  var payload=new StringBuilder();for(int line=0;line<32;line++)payload.AppendLine("algorithm process memory network java: synthetic operating systems benchmark material.");
  for(int i=0;i<count;i++){
   if(cancelled()){File.AppendAllText(Path.Combine(root,"DATASET_MANIFEST.md"),"CANCELLED: partial dataset retained.\n");throw new OperationCanceledException();}
   string relative="";
   if(kind=="Wide")relative="Topic_"+(i%Math.Min(2000,count)).ToString("D4");
   else if(kind=="Deep"){int depth=1+i%deepLevels;for(int d=0;d<depth;d++)relative=Path.Combine(relative,"D"+d.ToString("D2"));}
   else if(kind=="Mixed"){relative="Course_"+(i%100).ToString("D2");for(int d=0;d<i%8;d++)relative=Path.Combine(relative,"U"+d);}
   string dir=Path.Combine(root,relative);Directory.CreateDirectory(dir);File.WriteAllText(Path.Combine(dir,"material_"+i.ToString("D6")+".txt"),"File "+i+"\n"+payload,Encoding.UTF8);
   if(i%100==0)progress((i+1)*100/count);
  }progress(100);return root;
 }
}
