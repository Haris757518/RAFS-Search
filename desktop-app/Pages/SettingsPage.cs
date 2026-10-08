using System;
using System.IO;
using System.Windows.Forms;
internal sealed class SettingsPage : AppPage
{
    private readonly SettingsService service;
    private readonly ComboBox method = new ComboBox();
    private readonly NumericUpDown depth, threads, maxWorkers, runs;
    private readonly CheckBox remember = new CheckBox { Text = "Remember search folder / query", AutoSize = true }, logging = new CheckBox { Text = "Application diagnostic logging", AutoSize = true };
    private readonly Label status = Theme.Label("", true);
    private readonly Button save = Theme.Button("Save Settings", true);
    public event Action SettingsChanged;
    public SettingsPage(SettingsService service) : base("Settings / About", "Application preferences and project information.")
    {
        this.service = service; var settings = service.Settings;
        Theme.MethodCombo(method);
        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true }; var sections = Theme.Stack(); sections.AutoSize = true; sections.Dock = DockStyle.Top; scroll.Controls.Add(sections);
        var prefs = new SectionPanel("Settings");
        Theme.Row(prefs, Theme.Label("Theme: Light / system fonts. Default search mode: Manual / Research. Adaptive mode: Planned.", true), false);
        method.DropDownStyle = ComboBoxStyle.DropDownList; method.Width = 270; method.Items.AddRange(new object[] { "Method 1 — WalkDir + Rayon", "Method 2 — Recursive DFS" }); method.SelectedIndex = settings.DefaultMethod - 1;
        depth = Theme.Number(settings.DefaultDepth, 1000); threads = Theme.Number(settings.DefaultThreads, 256); maxWorkers = Theme.Number(settings.MaximumWorkers, 256, 1); runs = Theme.Number(settings.ComparisonRuns, 15, 1);
        Theme.Row(prefs, Theme.Flow(Theme.Field("Default method", method), Theme.Field("Default depth", depth), Theme.Field("Default threads", threads)), false);
        Theme.Row(prefs, Theme.Flow(Theme.Field("Maximum workers (future)", maxWorkers), Theme.Field("Default measured runs", runs)), false);
        Theme.Row(prefs, Theme.Label("Maximum workers is stored for the future adaptive controller; it does not override current manual thread settings.", true), false);
        remember.Checked = settings.RememberFolder; logging.Checked = settings.DiagnosticLogging;
        Theme.Row(prefs, Theme.Flow(remember, logging, new CheckBox { Text = "Runtime Statistics — unavailable", Enabled = false, AutoSize = true }), false);
        Theme.Row(prefs, Theme.Flow(save), false); Theme.Row(prefs, status, false); Theme.Row(sections, prefs, false);
        var about = new SectionPanel("RAFS Search");
        Theme.Row(about, Theme.Label("Runtime-Adaptive File System Search Using\nWorkload-Aware Traversal and Concurrency Selection"), false);
        Theme.Row(about, Theme.Label("Operating Systems Project"), false);
        Theme.Row(about, Theme.Label("Developed by Haris K"), false);
        Theme.Row(about, Theme.Label("Built upon the open-source fsearch baseline.\nOriginal MIT attribution retained.", true), false);
        Theme.Row(about, Theme.Label("Current engine: fixed WalkDir + Rayon / recursive DFS. Adaptive algorithms and runtime instrumentation are not implemented.", true), false);
        var license = Theme.Button("View License"); var folder = Theme.Button("Open Project Folder");
        license.Click += delegate { try { DesktopActions.ViewLicense(); } catch (Exception e) { status.Text = e.Message; } };
        folder.Click += delegate { try { DesktopActions.OpenFolder(); } catch (Exception e) { status.Text = e.Message; } };
        Theme.Row(about, Theme.Flow(license, folder), false);
        var backend = new TextBox { Text = "Engine: " + SearchRunner.Binary + "\r\nSettings: " + service.FilePath + "\r\nUI font: " + Theme.UiFamily + " | Metric/log font: " + Theme.MonoFamily + "\r\nDPI: PerMonitorV2 manifest + .NET Framework 4.8 configuration", ReadOnly = true, Multiline = true, Dock = DockStyle.Fill, Font = Theme.Mono(9), Height = 90, ScrollBars = ScrollBars.Horizontal, WordWrap = false };
        Theme.Row(about, backend, false); Theme.Row(sections, about, false); Theme.Row(Body, scroll, true);
        if (!String.IsNullOrEmpty(service.LoadWarning)) status.Text = service.LoadWarning;
        save.Click += delegate {
            settings.DefaultMethod = method.SelectedIndex + 1; settings.DefaultDepth = (int)depth.Value; settings.DefaultThreads = (int)threads.Value; settings.MaximumWorkers = (int)maxWorkers.Value; settings.ComparisonRuns = (int)runs.Value; settings.RememberFolder = remember.Checked; settings.DiagnosticLogging = logging.Checked;
            try { service.Save(); status.Text = "Preferences saved. Defaults apply to the next app launch; logging applies now."; if (SettingsChanged != null) SettingsChanged(); } catch (Exception e) { status.Text = "Could not save preferences: " + e.Message; }
        };
    }
    public override void RefreshSession(SearchSession session) { save.Enabled = !session.IsBusy; }
}
