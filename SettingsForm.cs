namespace WeaponTweaker;

internal sealed class SettingsForm : Form
{
    private readonly TextBox _output = new() { Dock = DockStyle.Fill };
    private readonly TextBox _profile = new() { Dock = DockStyle.Fill };

    public string? OutputFolder => Normalize(_output.Text);
    public string? ProfileFolder => Normalize(_profile.Text);

    public SettingsForm(AppSettings settings)
    {
        Text = "Weapon Tweaker Settings";
        Width = 720;
        Height = 245;
        MinimumSize = new Size(580, 245);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;

        _output.Text = settings.OutputDirectory ?? "";
        _profile.Text = settings.Mo2ProfileDirectory ?? "";

        var outputBrowse = new Button { Text = "Browse…", AutoSize = true };
        var profileBrowse = new Button { Text = "Browse…", AutoSize = true };
        outputBrowse.Click += (_, _) => Browse(_output, "Choose the default output folder for patches");
        profileBrowse.Click += (_, _) => Browse(_profile, "Choose the MO2 profile folder containing plugins.txt");

        var ok = new Button { Text = "Save", DialogResult = DialogResult.OK, AutoSize = true };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };
        AcceptButton = ok;
        CancelButton = cancel;

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(14), ColumnCount = 3, RowCount = 5 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        layout.Controls.Add(new Label { Text = "Output folder", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        layout.Controls.Add(_output, 1, 0);
        layout.Controls.Add(outputBrowse, 2, 0);
        layout.Controls.Add(new Label { Text = "Profile folder", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
        layout.Controls.Add(_profile, 1, 1);
        layout.Controls.Add(profileBrowse, 2, 1);
        var hint = new Label
        {
            Text = "Leave a field blank to use the default. Profile folder should be an MO2 profile containing plugins.txt.",
            AutoSize = true, ForeColor = SystemColors.GrayText, Padding = new Padding(0, 8, 0, 0)
        };
        layout.Controls.Add(hint, 0, 2);
        layout.SetColumnSpan(hint, 3);
        var buttons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill };
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(ok);
        layout.Controls.Add(buttons, 0, 4);
        layout.SetColumnSpan(buttons, 3);
        Controls.Add(layout);
    }

    private void Browse(TextBox target, string description)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = description, UseDescriptionForTitle = true,
            SelectedPath = Directory.Exists(target.Text) ? target.Text : ""
        };
        if (dialog.ShowDialog(this) == DialogResult.OK) target.Text = dialog.SelectedPath;
    }

    private static string? Normalize(string value) => string.IsNullOrWhiteSpace(value) ? null : Path.GetFullPath(value.Trim());
}
