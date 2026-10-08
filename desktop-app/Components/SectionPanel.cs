using System.Drawing;
using System.Windows.Forms;
internal class SectionPanel : TableLayoutPanel
{
    public SectionPanel(string title)
    {
        ColumnCount = 1; BackColor = Theme.Surface; Padding = new Padding(10); Margin = new Padding(0, 8, 0, 0); AutoSize = true; AutoSizeMode = AutoSizeMode.GrowAndShrink; Dock = DockStyle.Fill;
        ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        if (!string.IsNullOrEmpty(title)) { var heading = Theme.Label(title); heading.Font = Theme.Ui(11, FontStyle.Bold); Theme.Row(this, heading, false); }
    }
    protected override void OnPaint(PaintEventArgs e) { base.OnPaint(e); using (var pen = new Pen(Theme.Border)) e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1); }
}
