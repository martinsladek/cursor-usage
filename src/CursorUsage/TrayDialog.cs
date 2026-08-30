namespace CursorUsage;

/// <summary>
/// Tray apps keep pumping window messages while a modal is open, so a second
/// click on the notification icon can open another copy of About or the dashboard.
/// Show the dialog once; if it is already open, just activate it.
/// </summary>
static class TrayDialog
{
    public static DialogResult ShowOnce(ref Form? open, IWin32Window? owner, Func<Form> create)
    {
        if (open is { IsDisposed: false })
        {
            Activate(open);
            return DialogResult.None;
        }

        Form dialog = create();
        open = dialog;
        try
        {
            return owner is null ? dialog.ShowDialog() : dialog.ShowDialog(owner);
        }
        finally
        {
            dialog.Dispose();
            if (ReferenceEquals(open, dialog))
                open = null;
        }
    }

    public static void ShowModelessOnce(ref Form? open, Func<Form> create, Action? onClosed = null)
    {
        if (open is { IsDisposed: false })
        {
            Activate(open);
            return;
        }

        Form dialog = create();
        open = dialog;
        dialog.FormClosed += (_, _) =>
        {
            dialog.Dispose();
            onClosed?.Invoke();
        };
        dialog.Show();
    }

    public static void Activate(Form form)
    {
        if (form.WindowState == FormWindowState.Minimized)
            form.WindowState = FormWindowState.Normal;

        form.Show();
        form.Activate();
        form.BringToFront();
    }
}
