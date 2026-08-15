using System.ComponentModel;
using System.Globalization;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Skyrim;

namespace WeaponTweaker;

internal sealed class MainForm : Form
{
    private readonly Label _pluginLabel = new() { Text = "No plugin loaded", AutoEllipsis = true, Dock = DockStyle.Fill };
    private readonly TextBox _search = new() { PlaceholderText = "Search name, EditorID, or FormID…", Dock = DockStyle.Fill };
    private readonly DataGridView _grid = new()
    {
        Dock = DockStyle.Fill, AutoGenerateColumns = false, AllowUserToAddRows = false,
        AllowUserToDeleteRows = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        MultiSelect = false, RowHeadersVisible = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
    };
    private readonly Button _save = new() { Text = "Save patch…", Enabled = false, AutoSize = true };
    private readonly Label _status = new() { Text = "Open a Skyrim plugin to begin.", Dock = DockStyle.Fill, AutoEllipsis = true };
    private readonly BindingList<WeaponRow> _visible = [];
    private readonly List<WeaponRow> _all = [];
    private ISkyrimModDisposableGetter? _source;
    private string? _sourcePath;

    public MainForm(string? initialPath)
    {
        Text = "Weapon Tweaker 1.0";
        Width = 1120;
        Height = 700;
        MinimumSize = new System.Drawing.Size(850, 500);
        StartPosition = FormStartPosition.CenterScreen;
        AllowDrop = true;

        BuildGrid();
        var open = new Button { Text = "Open plugin…", AutoSize = true };
        var top = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1 };
        top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 320));
        top.Controls.Add(open, 0, 0);
        top.Controls.Add(_pluginLabel, 1, 0);
        top.Controls.Add(_search, 2, 0);

        var bottom = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bottom.Controls.Add(_status, 0, 0);
        bottom.Controls.Add(_save, 1, 0);

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), RowCount = 3 };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(top, 0, 0);
        layout.Controls.Add(_grid, 0, 1);
        layout.Controls.Add(bottom, 0, 2);
        Controls.Add(layout);

        open.Click += (_, _) => Browse();
        _save.Click += async (_, _) => await SavePatchAsync();
        _search.TextChanged += (_, _) => ApplyFilter();
        _grid.CellValidating += ValidateCell;
        _grid.CellValueChanged += (_, _) => RefreshChangedState();
        _grid.DataError += (_, e) => { e.ThrowException = false; };
        DragEnter += (_, e) => { if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true) e.Effect = DragDropEffects.Copy; };
        DragDrop += async (_, e) =>
        {
            var file = ((string[]?)e.Data?.GetData(DataFormats.FileDrop))?.FirstOrDefault(IsPlugin);
            if (file is not null) await LoadPluginAsync(file);
        };

        if (initialPath is not null && IsPlugin(initialPath)) Shown += async (_, _) => await LoadPluginAsync(initialPath);
    }

    private void BuildGrid()
    {
        AddText("Name", nameof(WeaponRow.Name), true, 150);
        AddText("Editor ID", nameof(WeaponRow.EditorID), true, 130);
        AddText("Form ID", nameof(WeaponRow.FormKey), true, 120);
        AddText("Damage", nameof(WeaponRow.Damage), false, 70);
        AddText("Speed", nameof(WeaponRow.Speed), false, 70);
        AddText("Reach", nameof(WeaponRow.Reach), false, 70);
        AddText("Weight", nameof(WeaponRow.Weight), false, 70);
        AddText("Value", nameof(WeaponRow.Value), false, 70);
        AddText("Critical", nameof(WeaponRow.CriticalDamage), false, 70);
        _grid.DataSource = _visible;
    }

    private void AddText(string title, string property, bool readOnly, float weight)
    {
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = title, DataPropertyName = property, ReadOnly = readOnly,
            FillWeight = weight, SortMode = DataGridViewColumnSortMode.Automatic
        });
    }

    private void Browse()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "Skyrim plugins (*.esp;*.esm;*.esl)|*.esp;*.esm;*.esl", Title = "Open a Skyrim plugin"
        };
        if (dialog.ShowDialog(this) == DialogResult.OK) _ = LoadPluginAsync(dialog.FileName);
    }

    private async Task LoadPluginAsync(string path)
    {
        try
        {
            ToggleBusy(true, "Reading weapons…");
            var mod = await Task.Run(() => OpenPlugin(path));
            _source?.Dispose();
            _source = mod;
            _sourcePath = Path.GetFullPath(path);
            _all.Clear();
            foreach (var weapon in mod.Weapons)
            {
                var damage = weapon.BasicStats?.Damage ?? 0;
                var speed = weapon.Data?.Speed ?? 0;
                var reach = weapon.Data?.Reach ?? 0;
                var weight = weapon.BasicStats?.Weight ?? 0;
                var value = weapon.BasicStats?.Value ?? 0;
                var critical = weapon.Critical?.Damage ?? 0;
                _all.Add(new WeaponRow
                {
                    Source = weapon, FormKey = weapon.FormKey, Name = weapon.Name?.String ?? "",
                    EditorID = weapon.EditorID ?? "", Damage = damage, OriginalDamage = damage,
                    Speed = speed, OriginalSpeed = speed, Reach = reach, OriginalReach = reach,
                    Weight = weight, OriginalWeight = weight, Value = value, OriginalValue = value,
                    CriticalDamage = critical, OriginalCriticalDamage = critical
                });
            }
            _pluginLabel.Text = _sourcePath;
            ApplyFilter();
            _status.Text = $"{_all.Count:N0} weapon records loaded. Edit a value, then save a patch.";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Could not open plugin", MessageBoxButtons.OK, MessageBoxIcon.Error);
            _status.Text = "The plugin could not be opened.";
        }
        finally { ToggleBusy(false); }
    }

    private void ApplyFilter()
    {
        var query = _search.Text.Trim();
        var rows = string.IsNullOrEmpty(query) ? _all : _all.Where(x =>
            x.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            x.EditorID.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            x.FormKey.ToString().Contains(query, StringComparison.OrdinalIgnoreCase));
        _visible.RaiseListChangedEvents = false;
        _visible.Clear();
        foreach (var row in rows) _visible.Add(row);
        _visible.RaiseListChangedEvents = true;
        _visible.ResetBindings();
        PaintChangedRows();
        RefreshChangedState();
    }

    private void ValidateCell(object? sender, DataGridViewCellValidatingEventArgs e)
    {
        if (_grid.Columns[e.ColumnIndex].ReadOnly) return;
        var property = _grid.Columns[e.ColumnIndex].DataPropertyName;
        var text = Convert.ToString(e.FormattedValue, CultureInfo.InvariantCulture) ?? "";
        var valid = property switch
        {
            nameof(WeaponRow.Damage) or nameof(WeaponRow.CriticalDamage) => int.TryParse(text, out var n) && n is >= 0 and <= ushort.MaxValue,
            nameof(WeaponRow.Value) => uint.TryParse(text, out _),
            _ => float.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out var f) && float.IsFinite(f) && f >= 0
        };
        if (!valid)
        {
            e.Cancel = true;
            _grid.Rows[e.RowIndex].ErrorText = "Enter a non-negative number within the field's valid range.";
        }
        else _grid.Rows[e.RowIndex].ErrorText = "";
    }

    private void RefreshChangedState()
    {
        PaintChangedRows();
        var count = _all.Count(x => x.Changed);
        _save.Enabled = count > 0 && _source is not null;
        if (_source is not null) _status.Text = count == 0
            ? $"{_all.Count:N0} weapon records loaded. No changes yet."
            : $"{count:N0} changed weapon record{(count == 1 ? "" : "s")} ready to save.";
    }

    private void PaintChangedRows()
    {
        foreach (DataGridViewRow row in _grid.Rows)
            row.DefaultCellStyle.BackColor = row.DataBoundItem is WeaponRow item && item.Changed
                ? Color.FromArgb(255, 248, 210) : _grid.DefaultCellStyle.BackColor;
    }

    private async Task SavePatchAsync()
    {
        _grid.EndEdit();
        var changed = _all.Where(x => x.Changed).ToArray();
        if (_source is null || _sourcePath is null || changed.Length == 0) return;
        using var dialog = new SaveFileDialog
        {
            Filter = "Skyrim plugin (*.esp)|*.esp", FileName = Path.GetFileNameWithoutExtension(_sourcePath) + " - Weapon Tweaks.esp",
            InitialDirectory = Path.GetDirectoryName(_sourcePath), Title = "Save weapon tweak patch"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        if (Path.GetFullPath(dialog.FileName).Equals(_sourcePath, StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show(this, "Choose a different filename. Weapon Tweaker never overwrites the source plugin.", "Source plugin protected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        try
        {
            ToggleBusy(true, "Writing patch…");
            await Task.Run(() => WritePatch(dialog.FileName, changed, _source));
            _status.Text = $"Saved {changed.Length:N0} weapon overrides to {dialog.FileName}";
            MessageBox.Show(this, $"Patch created successfully.\n\n{dialog.FileName}\n\nEnable it after the source plugin in MO2.", "Patch saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.ToString(), "Could not save patch", MessageBoxButtons.OK, MessageBoxIcon.Error);
            _status.Text = "The patch could not be written.";
        }
        finally { ToggleBusy(false); }
    }

    internal static void WritePatch(string outputPath, IReadOnlyCollection<WeaponRow> changed, ISkyrimModGetter source)
    {
        var modKey = ModKey.FromFileName(Path.GetFileName(outputPath));
        var patch = new SkyrimMod(modKey, SkyrimRelease.SkyrimSE) { IsSmallMaster = true };
        foreach (var row in changed)
        {
            var weapon = patch.Weapons.GetOrAddAsOverride(row.Source);
            weapon.BasicStats ??= new WeaponBasicStats();
            weapon.Data ??= new WeaponData();
            weapon.Critical ??= new CriticalData();
            weapon.BasicStats.Damage = checked((ushort)row.Damage);
            weapon.BasicStats.Weight = row.Weight;
            weapon.BasicStats.Value = row.Value;
            weapon.Data.Speed = row.Speed;
            weapon.Data.Reach = row.Reach;
            weapon.Critical.Damage = checked((ushort)row.CriticalDamage);
        }
        patch.BeginWrite.ToPath(outputPath).WithLoadOrder(source).Write();
    }

    internal static ISkyrimModDisposableGetter OpenPlugin(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var dataFolder = Path.GetDirectoryName(fullPath)!;
        using var header = SkyrimMod.Create(SkyrimRelease.SkyrimSE)
            .FromPath(fullPath).WithLoadOrder(Array.Empty<ModKey>())
            .WithDataFolder(dataFolder).Construct();
        var masters = header.MasterReferences.Select(x => x.Master).ToArray();
        return SkyrimMod.Create(SkyrimRelease.SkyrimSE)
            .FromPath(fullPath).WithLoadOrder(masters)
            .WithDataFolder(dataFolder).Construct();
    }

    private void ToggleBusy(bool busy, string? text = null)
    {
        UseWaitCursor = busy;
        _grid.Enabled = !busy;
        _search.Enabled = !busy;
        _save.Enabled = !busy && _all.Any(x => x.Changed);
        if (text is not null) _status.Text = text;
    }

    private static bool IsPlugin(string path) => File.Exists(path) && new[] { ".esp", ".esm", ".esl" }.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);
}
