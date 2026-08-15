using System.ComponentModel;
using System.Globalization;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Environments;
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
    private readonly AppSettings _settings = AppSettings.Load();
    private ISkyrimModDisposableGetter? _source;
    private string? _sourcePath;
    private ModKey[] _writeLoadOrder = [];
    private string? _dataFolder;
    private string _suggestedPatchName = "Weapon Tweaks.esp";
    private string? _sortProperty;
    private bool _sortAscending = true;

    public MainForm(string? initialPath)
    {
        Text = "Weapon Tweaker 1.2.2";
        Width = 1120;
        Height = 700;
        MinimumSize = new System.Drawing.Size(850, 500);
        StartPosition = FormStartPosition.CenterScreen;
        AllowDrop = true;
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

        BuildGrid();
        var open = new Button { Text = "Open plugin…", AutoSize = true };
        var loadAll = new Button { Text = "Load all weapons…", AutoSize = true };
        var top = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1 };
        top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 320));
        var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = false };
        buttons.Controls.Add(open);
        buttons.Controls.Add(loadAll);
        top.Controls.Add(buttons, 0, 0);
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
        var menu = BuildMenu();
        var shell = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
        shell.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        shell.Controls.Add(menu, 0, 0);
        shell.Controls.Add(layout, 0, 1);
        Controls.Add(shell);
        MainMenuStrip = menu;

        open.Click += (_, _) => Browse();
        loadAll.Click += async (_, _) => await LoadAllWeaponsAsync();
        _save.Click += async (_, _) => await SavePatchAsync();
        _search.TextChanged += (_, _) => ApplyFilter();
        _grid.CellValidating += ValidateCell;
        _grid.CellValueChanged += (_, _) => RefreshChangedState();
        _grid.ColumnHeaderMouseClick += (_, e) => SortByColumn(e.ColumnIndex);
        _grid.DataError += (_, e) => { e.ThrowException = false; };
        DragEnter += (_, e) => { if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true) e.Effect = DragDropEffects.Copy; };
        DragDrop += async (_, e) =>
        {
            var file = ((string[]?)e.Data?.GetData(DataFormats.FileDrop))?.FirstOrDefault(IsPlugin);
            if (file is not null) await LoadPluginAsync(file);
        };

        if (initialPath is not null && IsPlugin(initialPath)) Shown += async (_, _) => await LoadPluginAsync(initialPath);
    }

    private MenuStrip BuildMenu()
    {
        var menu = new MenuStrip();
        var file = new ToolStripMenuItem("File");
        file.DropDownItems.Add("Open plugin…", null, (_, _) => Browse());
        file.DropDownItems.Add("Load all winning weapons from MO2…", null, async (_, _) => await LoadAllWeaponsAsync());
        file.DropDownItems.Add(new ToolStripSeparator());
        file.DropDownItems.Add("Save patch…", null, async (_, _) => await SavePatchAsync());
        file.DropDownItems.Add(new ToolStripSeparator());
        file.DropDownItems.Add("Exit", null, (_, _) => Close());

        var settings = new ToolStripMenuItem("Settings");
        settings.Click += (_, _) => ShowSettings();
        menu.Items.Add(file);
        menu.Items.Add(settings);
        return menu;
    }

    private void ShowSettings()
    {
        using var dialog = new SettingsForm(_settings);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        if (dialog.ProfileFolder is not null && !File.Exists(Path.Combine(dialog.ProfileFolder, "plugins.txt")))
        {
            MessageBox.Show(this, "That folder does not contain plugins.txt. Choose a folder inside MO2's profiles directory.",
                "Not an MO2 profile", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        _settings.OutputDirectory = dialog.OutputFolder;
        _settings.Mo2ProfileDirectory = dialog.ProfileFolder;
        _settings.Save();
        _status.Text = "Settings saved. Blank fields use the default.";
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
            FillWeight = weight, SortMode = DataGridViewColumnSortMode.Programmatic
        });
    }

    private void SortByColumn(int columnIndex)
    {
        _grid.EndEdit();
        var property = _grid.Columns[columnIndex].DataPropertyName;
        if (string.IsNullOrEmpty(property)) return;
        if (_sortProperty == property) _sortAscending = !_sortAscending;
        else
        {
            _sortProperty = property;
            _sortAscending = true;
        }
        ApplyFilter();
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
            var resolvedPath = ResolveActivePluginPath(path, _settings.Mo2ProfileDirectory);
            var mod = await Task.Run(() => OpenPlugin(resolvedPath));
            _source?.Dispose();
            _source = mod;
            _sourcePath = resolvedPath;
            _dataFolder = Path.GetDirectoryName(_sourcePath)!;
            _writeLoadOrder = mod.MasterReferences.Select(x => x.Master).Append(mod.ModKey).ToArray();
            _suggestedPatchName = Path.GetFileNameWithoutExtension(_sourcePath) + " - Weapon Tweaks.esp";
            PopulateRows(mod.Weapons);
            _pluginLabel.Text = Path.GetFullPath(path).Equals(_sourcePath, StringComparison.OrdinalIgnoreCase)
                ? _sourcePath
                : $"MO2 active version: {_sourcePath}";
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

    private async Task LoadAllWeaponsAsync()
    {
        var profileDescription = Directory.Exists(_settings.Mo2ProfileDirectory)
            ? $"Configured profile:\n{_settings.Mo2ProfileDirectory}"
            : "No MO2 profile is configured; automatic detection will be used.";
        var answer = MessageBox.Show(this,
            $"This will load every winning weapon record from the active MO2 load order. It can take a while on large modlists.\n\n{profileDescription}\n\nContinue?",
            "Load all weapons", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (answer != DialogResult.Yes) return;
        try
        {
            ToggleBusy(true, "Reading the active MO2 load order…");
            var result = await Task.Run(() =>
            {
                using var automatic = GameEnvironment.Typical.Skyrim(SkyrimRelease.SkyrimSE);
                var dataFolder = automatic.DataFolderPath.ToString();
                if (Directory.Exists(_settings.Mo2ProfileDirectory))
                {
                    var automaticKeys = automatic.LoadOrder.ListedOrder
                        .Where(x => x.Mod is not null).Select(x => x.Mod!.ModKey).ToArray();
                    var profileKeys = ReadMo2ProfileLoadOrder(_settings.Mo2ProfileDirectory!, automaticKeys);
                    dataFolder = ResolveMo2DataFolder(_settings.Mo2ProfileDirectory!) ?? dataFolder;
                    var missing = profileKeys.Where(x => !File.Exists(Path.Combine(dataFolder, x.FileName.String))).ToArray();
                    if (missing.Length > 0)
                    {
                        var sample = string.Join("\n", missing.Take(8).Select(x => "• " + x.FileName.String));
                        throw new FileNotFoundException(
                            $"{missing.Length:N0} active profile plugins are not visible in:\n{dataFolder}\n\n{sample}\n\nLaunch Weapon Tweaker through this MO2 instance, then try again.");
                    }
                    using var configured = GameEnvironment.Typical
                        .Builder<ISkyrimMod, ISkyrimModGetter>(GameRelease.SkyrimSE)
                        .WithTargetDataFolder(dataFolder)
                        .WithLoadOrder(profileKeys)
                        .Build();
                    return SnapshotWeapons(configured, dataFolder);
                }
                return SnapshotWeapons(automatic, dataFolder);
            });
            _source?.Dispose();
            _source = null;
            _sourcePath = null;
            _dataFolder = result.dataFolder;
            _writeLoadOrder = result.order;
            _suggestedPatchName = "Weapon Tweaks - Load Order.esp";
            PopulateRows(result.weapons);
            _pluginLabel.Text = "Active MO2 load order — winning weapon records";
            ApplyFilter();
            _status.Text = $"{_all.Count:N0} winning weapon records loaded from {result.order.Length:N0} active plugins.";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message + "\n\nMake sure Weapon Tweaker is launched through MO2.",
                "Could not load MO2 load order", MessageBoxButtons.OK, MessageBoxIcon.Error);
            _status.Text = "The active MO2 load order could not be loaded.";
        }
        finally { ToggleBusy(false); }
    }

    private static (IWeaponGetter[] weapons, ModKey[] order, string dataFolder) SnapshotWeapons(
        IGameEnvironment<ISkyrimMod, ISkyrimModGetter> environment, string dataFolder)
    {
        var weapons = environment.LoadOrder.PriorityOrder.Weapon().WinningOverrides()
            .Where(x => !x.IsDeleted).Select(x => (IWeaponGetter)x.DeepCopy()).ToArray();
        var order = environment.LoadOrder.ListedOrder
            .Where(x => x.Mod is not null).Select(x => x.Mod!.ModKey).ToArray();
        return (weapons, order, dataFolder);
    }

    internal static ModKey[] ReadMo2ProfileLoadOrder(string profileDirectory, IReadOnlyCollection<ModKey> automaticKeys)
    {
        var pluginsPath = Path.Combine(profileDirectory, "plugins.txt");
        if (!File.Exists(pluginsPath)) throw new FileNotFoundException("The configured MO2 profile has no plugins.txt.", pluginsPath);

        var active = File.ReadLines(pluginsPath)
            .Select(x => x.Trim())
            .Where(x => x.StartsWith('*') && x.Length > 1)
            .Select(x => x[1..].Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var automatic = automaticKeys.Select(x => x.FileName.String).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var loadOrderPath = Path.Combine(profileDirectory, "loadorder.txt");
        var orderedNames = File.Exists(loadOrderPath)
            ? File.ReadLines(loadOrderPath).Select(x => x.Trim()).Where(x => x.Length > 0 && !x.StartsWith('#'))
            : automaticKeys.Select(x => x.FileName.String).Concat(active);

        var result = new List<ModKey>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in orderedNames)
        {
            if ((!active.Contains(name) && !automatic.Contains(name)) || !seen.Add(name)) continue;
            try { result.Add(ModKey.FromFileName(name)); } catch { }
        }
        foreach (var name in active.Where(seen.Add))
        {
            try { result.Add(ModKey.FromFileName(name)); } catch { }
        }
        if (result.Count == 0) throw new InvalidDataException("The configured MO2 profile contains no active plugins.");
        return result.ToArray();
    }

    internal static string? ResolveMo2DataFolder(string profileDirectory)
    {
        var profilesDirectory = Directory.GetParent(Path.GetFullPath(profileDirectory));
        var instanceDirectory = profilesDirectory?.Parent?.FullName;
        if (instanceDirectory is null) return null;
        var iniPath = Path.Combine(instanceDirectory, "ModOrganizer.ini");
        if (!File.Exists(iniPath)) return null;
        var line = File.ReadLines(iniPath).FirstOrDefault(x => x.StartsWith("gamePath=", StringComparison.OrdinalIgnoreCase));
        if (line is null) return null;
        var value = line[(line.IndexOf('=') + 1)..].Trim();
        const string prefix = "@ByteArray(";
        if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && value.EndsWith(')'))
            value = value[prefix.Length..^1];
        value = value.Replace("\\\\", "\\");
        if (!Path.IsPathRooted(value)) value = Path.Combine(instanceDirectory, value);
        var dataFolder = Path.Combine(value, "Data");
        return Directory.Exists(dataFolder) ? Path.GetFullPath(dataFolder) : null;
    }

    internal static string ResolveActivePluginPath(string selectedPath, string? profileDirectory)
    {
        var fullSelectedPath = Path.GetFullPath(selectedPath);
        if (!Directory.Exists(profileDirectory)) return fullSelectedPath;

        var dataFolder = ResolveMo2DataFolder(profileDirectory!);
        if (string.IsNullOrWhiteSpace(dataFolder)) return fullSelectedPath;

        var activePath = Path.Combine(dataFolder, Path.GetFileName(fullSelectedPath));
        return File.Exists(activePath) ? Path.GetFullPath(activePath) : fullSelectedPath;
    }

    private void PopulateRows(IEnumerable<IWeaponGetter> weapons)
    {
        _all.Clear();
        foreach (var weapon in weapons)
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
    }

    private void ApplyFilter()
    {
        var query = _search.Text.Trim();
        IEnumerable<WeaponRow> rows = string.IsNullOrEmpty(query) ? _all : _all.Where(x =>
            x.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            x.EditorID.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            x.FormKey.ToString().Contains(query, StringComparison.OrdinalIgnoreCase));
        rows = ApplySort(rows);
        _visible.RaiseListChangedEvents = false;
        _visible.Clear();
        foreach (var row in rows) _visible.Add(row);
        _visible.RaiseListChangedEvents = true;
        _visible.ResetBindings();
        UpdateSortGlyph();
        PaintChangedRows();
        RefreshChangedState();
    }

    private IEnumerable<WeaponRow> ApplySort(IEnumerable<WeaponRow> rows)
    {
        if (_sortProperty is null) return rows;
        return (_sortProperty, _sortAscending) switch
        {
            (nameof(WeaponRow.Name), true) => rows.OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase),
            (nameof(WeaponRow.Name), false) => rows.OrderByDescending(x => x.Name, StringComparer.CurrentCultureIgnoreCase),
            (nameof(WeaponRow.EditorID), true) => rows.OrderBy(x => x.EditorID, StringComparer.OrdinalIgnoreCase),
            (nameof(WeaponRow.EditorID), false) => rows.OrderByDescending(x => x.EditorID, StringComparer.OrdinalIgnoreCase),
            (nameof(WeaponRow.FormKey), true) => rows.OrderBy(x => x.FormKey.ToString(), StringComparer.OrdinalIgnoreCase),
            (nameof(WeaponRow.FormKey), false) => rows.OrderByDescending(x => x.FormKey.ToString(), StringComparer.OrdinalIgnoreCase),
            (nameof(WeaponRow.Damage), true) => rows.OrderBy(x => x.Damage),
            (nameof(WeaponRow.Damage), false) => rows.OrderByDescending(x => x.Damage),
            (nameof(WeaponRow.Speed), true) => rows.OrderBy(x => x.Speed),
            (nameof(WeaponRow.Speed), false) => rows.OrderByDescending(x => x.Speed),
            (nameof(WeaponRow.Reach), true) => rows.OrderBy(x => x.Reach),
            (nameof(WeaponRow.Reach), false) => rows.OrderByDescending(x => x.Reach),
            (nameof(WeaponRow.Weight), true) => rows.OrderBy(x => x.Weight),
            (nameof(WeaponRow.Weight), false) => rows.OrderByDescending(x => x.Weight),
            (nameof(WeaponRow.Value), true) => rows.OrderBy(x => x.Value),
            (nameof(WeaponRow.Value), false) => rows.OrderByDescending(x => x.Value),
            (nameof(WeaponRow.CriticalDamage), true) => rows.OrderBy(x => x.CriticalDamage),
            (nameof(WeaponRow.CriticalDamage), false) => rows.OrderByDescending(x => x.CriticalDamage),
            _ => rows
        };
    }

    private void UpdateSortGlyph()
    {
        foreach (DataGridViewColumn column in _grid.Columns)
            column.HeaderCell.SortGlyphDirection = column.DataPropertyName == _sortProperty
                ? (_sortAscending ? SortOrder.Ascending : SortOrder.Descending)
                : SortOrder.None;
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
        _save.Enabled = count > 0 && _writeLoadOrder.Length > 0;
        if (_writeLoadOrder.Length > 0) _status.Text = count == 0
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
        if (_dataFolder is null || _writeLoadOrder.Length == 0 || changed.Length == 0) return;
        var initialDirectory = Directory.Exists(_settings.OutputDirectory)
            ? _settings.OutputDirectory
            : _sourcePath is not null ? Path.GetDirectoryName(_sourcePath) : _dataFolder;
        using var dialog = new SaveFileDialog
        {
            Filter = "Skyrim plugin (*.esp)|*.esp", FileName = _suggestedPatchName,
            InitialDirectory = initialDirectory, Title = "Create or append to a weapon tweak patch",
            OverwritePrompt = false
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        if (_sourcePath is not null && Path.GetFullPath(dialog.FileName).Equals(_sourcePath, StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show(this, "Choose a different filename. Weapon Tweaker never overwrites the source plugin.", "Source plugin protected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        var appending = File.Exists(dialog.FileName);
        if (appending && MessageBox.Show(this,
                $"Append these {changed.Length:N0} changed weapon record{(changed.Length == 1 ? "" : "s")} to the existing patch?\n\n{dialog.FileName}\n\nThe existing plugin will be backed up first.",
                "Append to existing patch", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        try
        {
            ToggleBusy(true, appending ? "Appending to patch…" : "Writing patch…");
            var backupPath = await Task.Run(() => WritePatch(dialog.FileName, changed, _writeLoadOrder, _dataFolder));
            _status.Text = $"{(appending ? "Appended" : "Saved")} {changed.Length:N0} weapon overrides to {dialog.FileName}";
            var backupText = backupPath is null ? "" : $"\n\nBackup created:\n{backupPath}";
            MessageBox.Show(this,
                $"Patch {(appending ? "updated" : "created")} successfully.\n\n{dialog.FileName}{backupText}\n\nEnable it after the source plugin in MO2.",
                appending ? "Patch updated" : "Patch saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.ToString(), "Could not save patch", MessageBoxButtons.OK, MessageBoxIcon.Error);
            _status.Text = "The patch could not be written.";
        }
        finally { ToggleBusy(false); }
    }

    internal static string? WritePatch(string outputPath, IReadOnlyCollection<WeaponRow> changed, IReadOnlyCollection<ModKey> loadOrder, string dataFolder)
    {
        var modKey = ModKey.FromFileName(Path.GetFileName(outputPath));
        var appending = File.Exists(outputPath);
        var patch = appending
            ? OpenMutablePlugin(outputPath, dataFolder)
            : new SkyrimMod(modKey, SkyrimRelease.SkyrimSE) { IsSmallMaster = true };
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
        // Existing master positions are save-sensitive. Preserve their exact order and
        // append only newly needed candidates so existing raw FormID master indices do not move.
        var writeOrder = patch.MasterReferences.Select(x => x.Master).Concat(loadOrder).Distinct().ToArray();
        var directory = Path.GetDirectoryName(Path.GetFullPath(outputPath))!;
        var tempDirectory = Path.Combine(directory, $".WeaponTweaker-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);
        var tempPath = Path.Combine(tempDirectory, Path.GetFileName(outputPath));
        string? backupPath = null;
        try
        {
            patch.BeginWrite.ToPath(tempPath).WithLoadOrder(writeOrder).WithDataFolder(dataFolder).Write();
            if (appending)
            {
                backupPath = Path.Combine(directory,
                    $"{Path.GetFileNameWithoutExtension(outputPath)}.WeaponTweakerBackup-{DateTime.Now:yyyyMMdd-HHmmssfff}{Path.GetExtension(outputPath)}");
                File.Replace(tempPath, outputPath, backupPath);
            }
            else File.Move(tempPath, outputPath);
            return backupPath;
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
            if (Directory.Exists(tempDirectory)) Directory.Delete(tempDirectory);
        }
    }

    private static ISkyrimMod OpenMutablePlugin(string path, string dataFolder)
    {
        using var header = SkyrimMod.Create(SkyrimRelease.SkyrimSE)
            .FromPath(path).WithLoadOrder(Array.Empty<ModKey>())
            .WithDataFolder(dataFolder).Construct();
        var masters = header.MasterReferences.Select(x => x.Master).ToArray();
        return SkyrimMod.Create(SkyrimRelease.SkyrimSE)
            .FromPath(path).WithLoadOrder(masters)
            .WithDataFolder(dataFolder).Mutable().Construct();
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
        _save.Enabled = !busy && _writeLoadOrder.Length > 0 && _all.Any(x => x.Changed);
        if (text is not null) _status.Text = text;
    }

    private static bool IsPlugin(string path) => File.Exists(path) && new[] { ".esp", ".esm", ".esl" }.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);
}
