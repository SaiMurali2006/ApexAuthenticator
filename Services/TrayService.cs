using System;
using System.Windows;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace ApexAuth.Services;

public sealed class TrayService : IDisposable
{
    private readonly Drawing.Icon _icon;
    private readonly Forms.NotifyIcon _notify;

    public TrayService(Action onShow, Action onLock, Action onExit)
    {
        _icon = IconFactory.CreateTrayIcon();

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Open ApexAuth", null, (_, _) => Dispatch(onShow));
        menu.Items.Add("Lock",          null, (_, _) => Dispatch(onLock));
        menu.Items.Add("Exit",          null, (_, _) => Dispatch(onExit));

        _notify = new Forms.NotifyIcon
        {
            Text = "ApexAuth",
            Icon = _icon,
            Visible = true,
            ContextMenuStrip = menu
        };
        _notify.DoubleClick += (_, _) => Dispatch(onShow);
    }

    public void Dispose()
    {
        _notify.Visible = false;
        _notify.Dispose();
        _icon.Dispose();
    }

    private static void Dispatch(Action action) =>
        Application.Current.Dispatcher.Invoke(action);
}
