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
    internal AppShell() : this(new BaselineCliBackend(), new SettingsService(Path.Combine(SearchRunner.Root, "target", "desktop-settings.xml"))) { }
    internal AppShell(ISearchBackend backend, SettingsService settings)
    {
        this.settings = settings;
        Text = "RAFS Search"; Font = Theme.Ui(); ForeColor = Theme.Text; BackColor = Theme.Background;
        AutoScaleDimensions = new SizeF(96, 96); AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(1440, 900); MinimumSize = new Size(1200, 750); StartPosition = FormStartPosition.CenterScreen;
        Session = new SearchSession(backend);
        Search = new SearchPage(Session, settings.Settings);
        pages.Add("Search", Search); pages.Add("Live Monitor", new LiveMonitorPage());
        pages.Add("Compare", new ComparePage(Session, settings.Settings, delegate { return Search.Options.GetRequest(); }));
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
    }
    internal void ShowPage(string name)
    {
        if (!pages.ContainsKey(name)) return;
        content.SuspendLayout(); content.Controls.Clear(); content.Controls.Add(pages[name]); pageName = name; sidebar.SelectPage(name); content.ResumeLayout();
        pages[name].RefreshSession(Session);
    }
    internal string CurrentPage { get { return pageName; } }
    private void RefreshPages()
    {
        if (IsDisposed) return;
        foreach (var page in pages.Values) page.RefreshSession(Session);
        footer.Text = Session.StatusText + (Session.StartTime.HasValue ? " · " + Session.Elapsed.TotalSeconds.ToString("0.0") + " s" : "") + "  |  Adaptive controller: Planned";
    }
    private void Log(string message)
    {
        if (!settings.Settings.DiagnosticLogging || lastLog == message) return;
        lastLog = message;
        try { string path = Path.Combine(SearchRunner.Root, "target", "desktop-diagnostics.log"); Directory.CreateDirectory(Path.GetDirectoryName(path)); File.AppendAllText(path, DateTime.Now.ToString("O") + " | " + message + Environment.NewLine); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        timer.Stop();
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
