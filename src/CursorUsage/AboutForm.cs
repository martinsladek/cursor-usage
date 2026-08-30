using System.Diagnostics;

namespace CursorUsage;

sealed class AboutForm : Form
{
    public AboutForm()
    {
        Text = Strings.About;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        AutoScaleMode = AutoScaleMode.Font;
        Font = SystemFonts.MessageBoxFont;
        Padding = new Padding(20, 18, 20, 16);
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            AutoSize = true
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var title = new Label
        {
            Text = Strings.ProductName,
            AutoSize = true,
            Font = new Font(Font, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 10)
        };

        var tagline = new Label
        {
            Text = Strings.AboutTagline,
            AutoSize = true,
            MaximumSize = new Size(420, 0),
            Margin = new Padding(0, 0, 0, 16)
        };

        var credit = new Label
        {
            Text = Strings.AboutCredit,
            AutoSize = true,
            MaximumSize = new Size(420, 0),
            Margin = new Padding(0, 0, 0, 16)
        };

        var links = new TableLayoutPanel
        {
            ColumnCount = 2,
            RowCount = 2,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 12)
        };
        links.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        links.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        AddLinkRow(links, 0, Strings.Website, Strings.WebsiteUrl);
        AddLinkRow(links, 1, Strings.GitHub, Strings.GitHubUrl);

        var buttons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            WrapContents = false,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 8, 0, 0)
        };
        var ok = new Button
        {
            Text = Strings.Ok,
            DialogResult = DialogResult.OK,
            AutoSize = true,
            Padding = new Padding(12, 3, 12, 3)
        };
        buttons.Controls.Add(ok);

        layout.Controls.Add(title, 0, 0);
        layout.Controls.Add(tagline, 0, 1);
        layout.Controls.Add(credit, 0, 2);
        layout.Controls.Add(links, 0, 3);
        layout.Controls.Add(buttons, 0, 4);

        Controls.Add(layout);
        AcceptButton = ok;
        CancelButton = ok;
    }

    private static void AddLinkRow(TableLayoutPanel table, int row, string label, string url)
    {
        var name = new Label
        {
            Text = label,
            AutoSize = true,
            Margin = new Padding(0, 3, 16, 3),
            Anchor = AnchorStyles.Left
        };
        var link = new LinkLabel
        {
            Text = url,
            AutoSize = true,
            Margin = new Padding(0, 3, 0, 3),
            Anchor = AnchorStyles.Left
        };
        link.LinkClicked += (_, _) => OpenUrl(url);
        table.Controls.Add(name, 0, row);
        table.Controls.Add(link, 1, row);
    }

    private static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch
        {
        }
    }
}
