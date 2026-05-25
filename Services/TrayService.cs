using System;
using System.Windows;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace ApexAuth.Services;

public sealed class TrayService : IDisposable
{
    private readonly Forms.NotifyIcon _notify;
    private readonly Forms.ContextMenuStrip _menu;
    private Drawing.Icon _icon;

    public TrayService(Action onShow, Action onLock, Action onExit)
    {
        _icon = IconFactory.CreateTrayIcon();

        _menu = new Forms.ContextMenuStrip();
        _menu.Items.Add("Open ApexAuth", null, (_, _) => Dispatch(onShow));
        _menu.Items.Add("Lock",          null, (_, _) => Dispatch(onLock));
        _menu.Items.Add("Exit",          null, (_, _) => Dispatch(onExit));

        _notify = new Forms.NotifyIcon
        {
            Text = "ApexAuth",
            Icon = _icon,
            Visible = true,
            ContextMenuStrip = _menu
        };
        _notify.DoubleClick += (_, _) => Dispatch(onShow);
    }

    public void RefreshIcon()
    {
        var old = _icon;
        _icon = IconFactory.CreateTrayIcon();
        _notify.Icon = _icon;
        old.Dispose();
    }

    public void Dispose()
    {
        _notify.Visible = false;
        _notify.Dispose();
        _menu.Dispose();
        _icon.Dispose();
    }

    private static void Dispatch(Action action) =>
        Application.Current.Dispatcher.Invoke(action);
}
