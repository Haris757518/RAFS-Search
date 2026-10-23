using System;
using System.ComponentModel;
using System.Windows.Forms;
internal sealed class DatasetGeneratorPanel : FlowLayoutPanel {
 private readonly BackgroundWorker worker=new BackgroundWorker{WorkerReportsProgress=true};
 private volatile bool cancelled;
 public bool IsGenerating {get{return worker.IsBusy;}}
 public void CancelGeneration(){cancelled=true;}
 public DatasetGeneratorPanel(Action<string> ready){
  Dock=DockStyle.Top;AutoSize=true;WrapContents=true;
  var kind=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Width=130};kind.Items.AddRange(new object[]{"Deep","Wide","Mixed","Small Files"});kind.SelectedIndex=2;
  var count=Theme.Number(10000,50000,100);var generate=Theme.Button("Generate workload");var cancel=Theme.Button("Cancel generation");cancel.Enabled=false;var status=Theme.Label("Synthetic local text; existing datasets are preserved.",true);
  Controls.Add(Theme.Field("Topology",kind));Controls.Add(Theme.Field("File count",count));Controls.Add(generate);Controls.Add(cancel);Controls.Add(status);
  generate.Click+=delegate{cancelled=false;generate.Enabled=kind.Enabled=count.Enabled=false;cancel.Enabled=true;worker.RunWorkerAsync(new object[]{kind.Text,(int)count.Value});};cancel.Click+=delegate{cancelled=true;};
  worker.DoWork+=delegate(object sender,DoWorkEventArgs e){var args=(object[])e.Argument;e.Result=DatasetGenerator.Generate((string)args[0],(int)args[1],p=>worker.ReportProgress(p),()=>cancelled);};
  worker.ProgressChanged+=delegate(object sender,ProgressChangedEventArgs e){if(!IsDisposed)status.Text="Generating "+e.ProgressPercentage+"%";};
  worker.RunWorkerCompleted+=delegate(object sender,RunWorkerCompletedEventArgs e){if(IsDisposed)return;generate.Enabled=kind.Enabled=count.Enabled=true;cancel.Enabled=false;if(e.Error!=null)status.Text=e.Error is OperationCanceledException?"Generation cancelled; partial folder retained.":e.Error.Message;else{status.Text="Ready: "+e.Result;ready((string)e.Result);}};
 }
 protected override void Dispose(bool disposing){cancelled=true;base.Dispose(disposing);}
}
