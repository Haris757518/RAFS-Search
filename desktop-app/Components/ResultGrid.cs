using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
internal sealed class ResultGrid : DataGridView
{
    private IList<string> paths = new List<string>();
    private string emptyMessage = "Enter a pattern and folder, then select Start Search.";
    public string EmptyMessage { set { if (emptyMessage != value) { emptyMessage = value; Invalidate(); } } }
    public int MatchCount { get { return paths.Count; } }
    public string SelectedPath { get { return CurrentCell == null || CurrentCell.RowIndex >= paths.Count ? null : paths[CurrentCell.RowIndex]; } }
    public ResultGrid()
    {
        Dock = DockStyle.Fill; ReadOnly = true; VirtualMode = true; AllowUserToAddRows = false; AllowUserToDeleteRows = false; RowHeadersVisible = false;
        SelectionMode = DataGridViewSelectionMode.FullRowSelect; MultiSelect = false; BackgroundColor = Theme.Surface; BorderStyle = BorderStyle.None;
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill; EnableHeadersVisualStyles = false;
        Theme.StyleGrid(this);
        Columns.Add("Filename", "Filename"); Columns.Add("FullPath", "Path"); Columns.Add("Type", "Type");
        Columns[0].FillWeight = 24; Columns[1].FillWeight = 62; Columns[2].FillWeight = 14;
        Columns[0].MinimumWidth = 150; Columns[1].MinimumWidth = 280; Columns[2].MinimumWidth = 90;
        SizeChanged += delegate { if (Columns.Count == 3) { Columns[0].FillWeight = 24; Columns[1].FillWeight = 62; Columns[2].FillWeight = 14; } };
        foreach (DataGridViewColumn column in Columns) column.SortMode = DataGridViewColumnSortMode.NotSortable;
        CellValueNeeded += delegate(object sender, DataGridViewCellValueEventArgs e) {
            if (e.RowIndex >= paths.Count) return;
            string path = paths[e.RowIndex]; e.Value = e.ColumnIndex == 0 ? Path.GetFileName(path) : e.ColumnIndex == 1 ? path : ReportExporter.FileType(path);
        };
    }
    public void SetReport(SearchReport report) { RowCount = 0; paths = report == null ? new List<string>() : report.Paths; RowCount = paths.Count; Invalidate(); }
    protected override void OnPaint(PaintEventArgs e) {
        base.OnPaint(e);
        if (paths.Count == 0) {
            var area = new System.Drawing.Rectangle(16, ColumnHeadersHeight + 16, System.Math.Max(1, ClientSize.Width - 32), System.Math.Max(1, ClientSize.Height - ColumnHeadersHeight - 32));
            TextRenderer.DrawText(e.Graphics, emptyMessage, Font, area, Theme.Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
        }
    }
}
