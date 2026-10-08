using System.Drawing;
using System.Windows.Forms;
internal sealed class MetricCard : TableLayoutPanel
{
    private readonly Label value, detail;
    public MetricCard(string title, string initial = "--")
    {
        ColumnCount = 1; Dock = DockStyle.Fill; AutoSize = true; BackColor = Theme.Surface; Padding = new Padding(12, 8, 12, 8); Margin = new Padding(0, 8, 10, 8);
        ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        Controls.Add(Theme.Label(title, true), 0, 0);
        value = Theme.Label(initial); value.Font = Theme.Mono(15); Controls.Add(value, 0, 1);
        detail = Theme.Label("", true); detail.Font = Theme.Ui(9); Controls.Add(detail, 0, 2);
        SizeChanged += delegate { detail.MaximumSize = new Size(System.Math.Max(1, ClientSize.Width - Padding.Horizontal - detail.Margin.Horizontal), 0); };
    }
    public void Set(string text, string hint = "") { value.Text = text; detail.Text = hint; }
    public static TableLayoutPanel Row(params MetricCard[] cards) {
        var row = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Top, ColumnCount = cards.Length, Margin = new Padding(0) };
        for (int i = 0; i < cards.Length; i++) { row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / cards.Length)); row.Controls.Add(cards[i], i, 0); } return row;
    }
}
