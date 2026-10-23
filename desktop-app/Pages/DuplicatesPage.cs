using System;
using System.IO;
using System.Windows.Forms;
internal sealed class DuplicatesPage : AppPage {
 private readonly SearchOptionsPanel options;private readonly ComboBox mode=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList};
 private readonly Button start=Theme.Button("Find duplicates",true),cancel=Theme.Button("Cancel"),export=Theme.Button("Export paths CSV");
 private readonly TextBox raw=new TextBox{Multiline=true,ReadOnly=true,Dock=DockStyle.Fill,ScrollBars=ScrollBars.Both,WordWrap=false};
 private readonly ResultGrid grid=new ResultGrid();private readonly Label status=Theme.Label("Read-only duplicate discovery. Files are never deleted.",true);private int revision=-1;
 public DuplicatesPage(SearchSession session,AppSettings settings):base("Duplicates","Find identical content, names or sizes without modifying files."){
  options=new SearchOptionsPanel(settings,false);options.Content.Visible=options.CaseSensitive.Visible=false;options.Query.Text="*";
  mode.Items.AddRange(new object[]{"content","name","size"});mode.SelectedIndex=0;
  Theme.Row(Body,options,false);Theme.Row(Body,Theme.Flow(Theme.Field("Duplicate mode",mode),Theme.Field("Maximum depth",options.Depth),start,cancel,export),false);
  var tabs=new TabControl{Dock=DockStyle.Fill};var paths=new TabPage("Duplicate files");paths.Controls.Add(grid);var groups=new TabPage("Groups and summary");raw.BackColor=Theme.Surface;raw.ForeColor=Theme.Text;raw.Font=Theme.Mono();groups.Controls.Add(raw);tabs.TabPages.Add(paths);tabs.TabPages.Add(groups);Theme.Row(Body,tabs,true);Theme.Row(Body,status,false);
  start.Click+=delegate{try{var request=options.GetRequest();request.Method=1;request.DuplicateMode=mode.Text;request.Content=false;session.StartDuplicates(request);}catch(Exception e){status.Text=e.Message;}};
  cancel.Click+=delegate{session.Cancel();};export.Click+=delegate{if(session.LastDuplicateReport==null)return;using(var dialog=new SaveFileDialog{Filter="CSV files|*.csv",FileName="duplicates.csv"})if(dialog.ShowDialog(FindForm())==DialogResult.OK){try{ReportExporter.Results(dialog.FileName,session.LastDuplicateReport);status.Text="Paths exported; inspect Groups and summary for group membership.";}catch(Exception e){status.Text=e.Message;}}};
 }
 public override void RefreshSession(SearchSession s){options.Enabled=mode.Enabled=start.Enabled=!s.IsBusy;cancel.Enabled=s.IsBusy&&s.Operation=="Duplicates";export.Enabled=!s.IsBusy&&s.LastDuplicateReport!=null;if(revision!=s.DuplicateRevision){revision=s.DuplicateRevision;grid.SetReport(s.LastDuplicateReport);raw.Text=s.LastDuplicateReport==null?"":s.LastDuplicateReport.Output+s.LastDuplicateReport.Error;}if(s.Operation=="Duplicates")status.Text=s.IsBusy?"Scanning for duplicates…":s.Message;}
}
