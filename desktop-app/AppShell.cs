using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
internal sealed class AppShell : Form
{
    internal readonly SearchSession Session;
    internal readonly SearchPage Search;
    private readonly SettingsService settings;
    private readonly Sidebar sidebar = new Sidebar();
    private readonly Panel content = new Panel { Dock = DockStyle.Fill };
    private readonly Dictionary<string, AppPage> pages = new Dictionary<string, AppPage>();
    private readonly Timer timer = new Timer { Interval = 200 };
    private readonly Label footer = Theme.Label("Ready to search", true);
    private string pageName = "Search";
    private string lastLog;
    internal AppShell() : this(new BaselineCliBackend(), new SettingsService(AppPaths.Settings)) { }
    internal AppShell(ISearchBackend backend, SettingsService settings)
    {
        this.settings = settings;
        Text = "RAFS Search"; AccessibleName="RAFS Search"; KeyPreview=true; Font = Theme.Ui(); ForeColor = Theme.Text; BackColor = Theme.Background;
        AutoScaleDimensions = new SizeF(96, 96); AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(1440, 900); MinimumSize = new Size(1200, 750); StartPosition = FormStartPosition.CenterScreen;
        Session = new SearchSession(backend);
        Search = new SearchPage(Session, settings.Settings);
        pages.Add("Search", Search); pages.Add("Live Monitor", new LiveMonitorPage());
        pages.Add("Compare", new ComparePage(Session, settings.Settings, delegate { return Search.Options.GetRequest(); }));
        pages.Add("Duplicates",new DuplicatesPage(Session,settings.Settings));
        pages.Add("Benchmark Lab", new BenchmarkPage(Session, settings.Settings));
        var prefs = new SettingsPage(settings); pages.Add("Settings", prefs);
        Session.Log = Log;
        prefs.SettingsChanged += delegate { lastLog = null; };
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = new Padding(0) };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200)); root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.Controls.Add(sidebar, 0, 0);
        var right = Theme.Stack(); Theme.Row(right, content, true);
        footer.Padding = new Padding(24, 5, 16, 5); footer.Margin = new Padding(0); footer.Font = Theme.Mono(9); Theme.Row(right, footer, false);
        root.Controls.Add(right, 1, 0); Controls.Add(root);
        sidebar.Navigate += ShowPage; Session.Changed += delegate { RefreshPages(); };
        timer.Tick += delegate { RefreshPages(); }; timer.Start();
        ShowPage("Search");
        Theme.ApplyInputs(this);
    }
    internal void ShowPage(string name)
    {
        if (!pages.ContainsKey(name)) return;
        content.SuspendLayout(); content.Controls.Clear(); content.Controls.Add(pages[name]); pageName = name; sidebar.SelectPage(name); content.ResumeLayout();
        pages[name].RefreshSession(Session);
        Theme.ApplyInputs(pages[name]);
    }
    internal string CurrentPage { get { return pageName; } }
    internal bool NavigateKey(Keys keys)
    {
        string[] names={"Search","Live Monitor","Compare","Benchmark Lab","Duplicates","Settings"};
        for(int i=0;i<names.Length;i++)if(keys==(Keys.Alt|(Keys)((int)Keys.D1+i))){ShowPage(names[i]);return true;}
        if(keys==Keys.Escape && Session.IsBusy){Session.Cancel();return true;}
        if(keys==(Keys.Control|Keys.F)||keys==(Keys.Control|Keys.L)){ShowPage("Search");if(keys==(Keys.Control|Keys.F))Search.Options.Query.Select();else Search.Options.Folder.Select();return true;}
        if(keys==(Keys.Control|Keys.Enter)&&pageName=="Search"&&!Session.IsBusy){Search.Start();return true;}
        return false;
    }
    protected override bool ProcessCmdKey(ref Message message,Keys keys){return NavigateKey(keys)||base.ProcessCmdKey(ref message,keys);}
    private void RefreshPages()
    {
        if (IsDisposed) return;
        foreach (var page in pages.Values) page.RefreshSession(Session);
        footer.Text = Session.StatusText + (Session.StartTime.HasValue ? " · " + Session.Elapsed.TotalSeconds.ToString("0.0") + " s" : "") + "  |  Adaptive controller: Available";
    }
    private void Log(string message)
    {
        if (!settings.Settings.DiagnosticLogging || lastLog == message) return;
        lastLog = message;
        try { string path = Path.Combine(AppPaths.StateRoot, "desktop-diagnostics.log"); Directory.CreateDirectory(Path.GetDirectoryName(path));if(File.Exists(path)&&new FileInfo(path).Length>4*1024*1024)File.WriteAllText(path,"[Older diagnostics rotated]\n");File.AppendAllText(path, DateTime.Now.ToString("O") + " | " + message + Environment.NewLine); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        timer.Stop();
        Session.Cancel();
        var generatorPage=(BenchmarkPage)pages["Benchmark Lab"];generatorPage.CancelGeneration();
        var shutdown=System.Diagnostics.Stopwatch.StartNew();
        while((Session.IsBusy||generatorPage.IsGenerating) && shutdown.ElapsedMilliseconds<3000){Application.DoEvents();System.Threading.Thread.Sleep(10);}
        if (settings.Settings.RememberFolder) { settings.Settings.LastFolder = Search.Options.Folder.Text; settings.Settings.LastPattern = Search.Options.Query.Text; }
        else { settings.Settings.LastFolder = ""; }
        try { settings.Save(); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        Session.Dispose();
        base.OnFormClosing(e);
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing) { timer.Dispose(); foreach (var page in pages.Values) page.Dispose(); }
        base.Dispose(disposing);
    }
}
