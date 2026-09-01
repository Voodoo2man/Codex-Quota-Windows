using System.Drawing;
using System.Windows.Forms;

namespace CodexQuota;

internal sealed class TrayMenuColorTable : ProfessionalColorTable
{
    public override Color ToolStripDropDownBackground => Color.FromArgb(32, 32, 32);
    public override Color MenuBorder => Color.FromArgb(48, 48, 48);
    public override Color MenuItemBorder => Color.Transparent;
    public override Color MenuItemSelected => Color.FromArgb(44, 44, 44);
    public override Color MenuItemSelectedGradientBegin => Color.FromArgb(44, 44, 44);
    public override Color MenuItemSelectedGradientEnd => Color.FromArgb(44, 44, 44);
    public override Color MenuItemPressedGradientBegin => Color.FromArgb(50, 50, 50);
    public override Color MenuItemPressedGradientEnd => Color.FromArgb(50, 50, 50);
    public override Color SeparatorDark => Color.FromArgb(82, 82, 82);
    public override Color SeparatorLight => Color.FromArgb(82, 82, 82);
}

internal sealed class TrayMenuRenderer : ToolStripProfessionalRenderer
{
    public TrayMenuRenderer() : base(new TrayMenuColorTable()) { }

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        if (e.Item.Selected && e.Item.Enabled)
        {
            using var brush = new SolidBrush(Color.FromArgb(44, 44, 44));
            e.Graphics.FillRectangle(brush, new Rectangle(0, 0, e.Item.Width, e.Item.Height));
            return;
        }
        base.OnRenderMenuItemBackground(e);
    }

    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
    {
        using var pen = new Pen(Color.FromArgb(48, 48, 48));
        e.Graphics.DrawRectangle(pen, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
    }
}
