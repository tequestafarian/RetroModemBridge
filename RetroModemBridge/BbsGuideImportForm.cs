using System.ComponentModel;
using System.Diagnostics;

namespace RetroModemBridge;

public sealed class BbsGuideImportForm : Form
{
    private readonly TextBox _searchText = new();
    private readonly ComboBox _softwareFilter = new();
    private readonly DataGridView _grid = new();
    private readonly Label _sourceLabel = new();
    private readonly Label _countLabel = new();
    private readonly Button _loadBuiltInButton = new();
    private readonly Button _loadFileButton = new();
    private readonly Button _downloadPageButton = new();
    private readonly Button _addSelectedButton = new();
    private readonly Button _closeButton = new();
    private readonly BindingList<BbsGuideEntry> _visibleEntries = new();
    private List<BbsGuideEntry> _allEntries = [];

    public IReadOnlyList<BbsGuideEntry> SelectedEntries { get; private set; } = [];

    public BbsGuideImportForm()
    {
        InitializeComponent();
        WireEvents();
        LoadBuiltInList();
    }

    private void InitializeComponent()
    {
        Text = "Import from Telnet BBS Guide";
        Icon = AppIconHelper.LoadAppIcon();
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(920, 620);
        Size = new Size(1050, 720);
        Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 6,
            Padding = new Padding(12)
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(root);

        var title = new Label
        {
            Text = "Import BBSes from the Telnet BBS Guide",
            AutoSize = true,
            Font = new Font(Font.FontFamily, 13F, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 4)
        };
        root.Controls.Add(title, 0, 0);

        var help = new Label
        {
            Text = "Select BBSes from the bundled Telnet BBS Guide list or load a newer ZIP/CSV. Added entries become dial aliases, so your vintage computer can dial them with ATDT1, ATDT2, etc.",
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 10)
        };
        root.Controls.Add(help, 0, 1);

        var buttonPanel = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Top,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0, 0, 0, 8)
        };
        _loadBuiltInButton.Text = "Load Bundled List";
        _loadFileButton.Text = "Load ZIP/CSV";
        _downloadPageButton.Text = "Open Download Page";
        foreach (var button in new[] { _loadBuiltInButton, _loadFileButton, _downloadPageButton })
        {
            button.AutoSize = true;
            button.Height = 30;
            button.Margin = new Padding(0, 0, 8, 0);
            buttonPanel.Controls.Add(button);
        }
        root.Controls.Add(buttonPanel, 0, 2);

        var filterPanel = new TableLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Top,
            ColumnCount = 4,
            RowCount = 2,
            Margin = new Padding(0, 0, 0, 8)
        };
        filterPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        filterPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        filterPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        filterPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210));
        filterPanel.Controls.Add(CreateLabel("Search"), 0, 0);
        _searchText.Dock = DockStyle.Fill;
        filterPanel.Controls.Add(_searchText, 1, 0);
        filterPanel.Controls.Add(CreateLabel("Software"), 2, 0);
        _softwareFilter.DropDownStyle = ComboBoxStyle.DropDownList;
        _softwareFilter.Dock = DockStyle.Fill;
        filterPanel.Controls.Add(_softwareFilter, 3, 0);
        _sourceLabel.AutoSize = true;
        filterPanel.SetColumnSpan(_sourceLabel, 4);
        filterPanel.Controls.Add(_sourceLabel, 0, 1);
        root.Controls.Add(filterPanel, 0, 3);

        _grid.Dock = DockStyle.Fill;
        _grid.AutoGenerateColumns = false;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.MultiSelect = true;
        _grid.DataSource = _visibleEntries;
        _grid.Columns.Add(new DataGridViewCheckBoxColumn { DataPropertyName = nameof(BbsGuideEntry.Selected), HeaderText = "Add", Width = 50 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(BbsGuideEntry.Name), HeaderText = "Name", Width = 220, ReadOnly = true });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(BbsGuideEntry.Host), HeaderText = "Host", Width = 240, ReadOnly = true });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(BbsGuideEntry.Port), HeaderText = "Port", Width = 65, ReadOnly = true });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(BbsGuideEntry.Software), HeaderText = "Software", Width = 130, ReadOnly = true });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(BbsGuideEntry.Location), HeaderText = "Location", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, ReadOnly = true });
        root.Controls.Add(_grid, 0, 4);

        var bottom = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 3,
            RowCount = 1,
            Margin = new Padding(0, 8, 0, 0)
        };
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _countLabel.AutoSize = true;
        _countLabel.Anchor = AnchorStyles.Left;
        bottom.Controls.Add(_countLabel, 0, 0);
        _addSelectedButton.Text = "Add Checked or Highlighted to Directory";
        _addSelectedButton.AutoSize = true;
        _addSelectedButton.Height = 32;
        bottom.Controls.Add(_addSelectedButton, 1, 0);
        _closeButton.Text = "Close";
        _closeButton.AutoSize = true;
        _closeButton.Height = 32;
        _closeButton.Margin = new Padding(8, 0, 0, 0);
        bottom.Controls.Add(_closeButton, 2, 0);
        root.Controls.Add(bottom, 0, 5);
    }

    private static Label CreateLabel(string text) => new()
    {
        Text = text,
        AutoSize = true,
        Anchor = AnchorStyles.Left,
        Margin = new Padding(0, 4, 8, 0)
    };

    private void WireEvents()
    {
        _loadBuiltInButton.Click += (_, _) => LoadBuiltInList();
        _loadFileButton.Click += (_, _) => LoadExternalFile();
        _downloadPageButton.Click += (_, _) => OpenDownloadPage();
        _searchText.TextChanged += (_, _) => ApplyFilters();
        _softwareFilter.SelectedIndexChanged += (_, _) => ApplyFilters();
        _addSelectedButton.Click += (_, _) => AddSelectedAndClose();
        _closeButton.Click += (_, _) => Close();
        _grid.CellDoubleClick += (_, _) => ToggleCurrentSelection();
    }

    private void LoadBuiltInList()
    {
        try
        {
            _allEntries = BbsGuideParser.LoadBundledList();
            foreach (var entry in _allEntries)
                entry.Selected = false;
            PopulateSoftwareFilter();
            _sourceLabel.Text = "Source: bundled bbslist.csv from The Telnet & Dial-Up BBS Guide, https://www.telnetbbsguide.com/";
            ApplyFilters();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void LoadExternalFile()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "Telnet BBS Guide ZIP or CSV (*.zip;*.csv)|*.zip;*.csv|All files (*.*)|*.*"
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        try
        {
            _allEntries = BbsGuideParser.LoadFile(dialog.FileName);
            foreach (var entry in _allEntries)
                entry.Selected = false;
            PopulateSoftwareFilter();
            _sourceLabel.Text = "Source: " + dialog.FileName;
            ApplyFilters();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void PopulateSoftwareFilter()
    {
        var current = _softwareFilter.SelectedItem?.ToString();
        _softwareFilter.Items.Clear();
        _softwareFilter.Items.Add("All");
        foreach (var software in _allEntries.Select(e => e.Software).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(s => s))
            _softwareFilter.Items.Add(software);

        _softwareFilter.SelectedItem = !string.IsNullOrWhiteSpace(current) && _softwareFilter.Items.Contains(current) ? current : "All";
    }

    private void ApplyFilters()
    {
        _grid.EndEdit();
        var query = _searchText.Text.Trim();
        var software = _softwareFilter.SelectedItem?.ToString() ?? "All";
        IEnumerable<BbsGuideEntry> filtered = _allEntries;

        if (!string.IsNullOrWhiteSpace(query))
        {
            filtered = filtered.Where(e =>
                e.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                e.Host.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                e.Software.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                e.Location.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.Equals(software, "All", StringComparison.OrdinalIgnoreCase))
            filtered = filtered.Where(e => string.Equals(e.Software, software, StringComparison.OrdinalIgnoreCase));

        _visibleEntries.RaiseListChangedEvents = false;
        _visibleEntries.Clear();
        foreach (var entry in filtered.Take(500))
            _visibleEntries.Add(entry);
        _visibleEntries.RaiseListChangedEvents = true;
        _visibleEntries.ResetBindings();
        _countLabel.Text = $"Showing {_visibleEntries.Count} of {_allEntries.Count} Telnet entries. Check boxes or highlight rows, then add them to your dial alias directory.";
    }

    private void ToggleCurrentSelection()
    {
        if (_grid.CurrentRow?.DataBoundItem is BbsGuideEntry entry)
        {
            entry.Selected = !entry.Selected;
            _grid.Refresh();
        }
    }

    private void AddSelectedAndClose()
    {
        _grid.EndEdit();

        var selected = new List<BbsGuideEntry>();
        selected.AddRange(_allEntries.Where(e => e.Selected));

        foreach (DataGridViewRow row in _grid.SelectedRows)
        {
            if (row.DataBoundItem is BbsGuideEntry entry && !selected.Contains(entry))
                selected.Add(entry);
        }

        SelectedEntries = selected;
        if (SelectedEntries.Count == 0)
        {
            MessageBox.Show(this, "Check one or more BBS entries, or highlight one or more rows first.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        DialogResult = DialogResult.OK;
        Close();
    }

    private static void OpenDownloadPage()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://www.telnetbbsguide.com/lists/download-list/",
                UseShellExecute = true
            });
        }
        catch
        {
        }
    }
}
