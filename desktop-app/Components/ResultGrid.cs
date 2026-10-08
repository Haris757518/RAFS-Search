using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
internal sealed class ResultGrid : DataGridView
{
    private IList<string> paths = new List<string>();
    public int MatchCount { get { return paths.Count; } }
    public string SelectedPath { get { return CurrentCell == null || CurrentCell.RowIndex >= paths.Count ? null : paths[CurrentCell.RowIndex]; } }
    public ResultGrid()
    {
        Dock = DockStyle.Fill; ReadOnly = true; VirtualMode = true; AllowUserToAddRows = false; AllowUserToDeleteRows = false; RowHeadersVisible = false;
        SelectionMode = DataGridViewSelectionMode.FullRowSelect; MultiSelect = false; BackgroundColor = Theme.Surface; BorderStyle = BorderStyle.None;
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill; EnableHeadersVisualStyles = false;
        ColumnHeadersDefaultCellStyle.BackColor = Theme.Background; ColumnHeadersDefaultCellStyle.Font = Theme.Ui(10, System.Drawing.FontStyle.Bold);
        DefaultCellStyle.SelectionBackColor = Theme.Accent; AlternatingRowsDefaultCellStyle.BackColor = Theme.Background;
        Columns.Add("Filename", "Filename"); Columns.Add("FullPath", "Full Path"); Columns.Add("Type", "Type");
        Columns[0].FillWeight = 24; Columns[1].FillWeight = 62; Columns[2].FillWeight = 14;
        foreach (DataGridViewColumn column in Columns) column.SortMode = DataGridViewColumnSortMode.NotSortable;
        CellValueNeeded += delegate(object sender, DataGridViewCellValueEventArgs e) {
            if (e.RowIndex >= paths.Count) return;
            string path = paths[e.RowIndex]; e.Value = e.ColumnIndex == 0 ? Path.GetFileName(path) : e.ColumnIndex == 1 ? path : ReportExporter.FileType(path);
        };
    }
    public void SetReport(SearchReport report) { RowCount = 0; paths = report == null ? new List<string>() : report.Paths; RowCount = paths.Count; Invalidate(); }
}
