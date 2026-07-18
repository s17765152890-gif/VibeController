using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace VibeController.App.Services;

public sealed class TrayIconService : IDisposable
{
    private readonly Forms.NotifyIcon _icon;

    public TrayIconService(Action open, Action toggle, Action exit)
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("打开 VibeController", null, (_, _) => open());
        menu.Items.Add("启用 / 暂停映射", null, (_, _) => toggle());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) => exit());

        _icon = new Forms.NotifyIcon
        {
            Text = "VibeController",
            Icon = Drawing.SystemIcons.Application,
            ContextMenuStrip = menu,
            Visible = true,
        };
        _icon.DoubleClick += (_, _) => open();
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.ContextMenuStrip?.Dispose();
        _icon.Dispose();
    }
}
