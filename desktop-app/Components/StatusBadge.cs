using System.Drawing;
using System.Windows.Forms;
internal sealed class StatusBadge : Label
{
    public StatusBadge() { AutoSize = true; Padding = new Padding(10, 5, 10, 5); Margin = new Padding(0, 4, 12, 4); Font = Theme.Ui(10, FontStyle.Bold); Set("Ready to search", false); }
    public void Set(string text, bool error) { Text = text; ForeColor = error ? Theme.Error : Theme.Accent; BackColor = error ? Color.FromArgb(255, 235, 235) : Color.FromArgb(228, 242, 239); }
}
