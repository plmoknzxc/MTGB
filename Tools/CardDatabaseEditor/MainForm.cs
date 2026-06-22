using System.Drawing;

namespace MTGB.CardDatabaseEditor;

internal sealed class MainForm : Form
{
    private static readonly Color WindowColor = Color.FromArgb(29, 29, 31);
    private static readonly Color PanelColor = Color.FromArgb(40, 40, 43);
    private static readonly Color InputColor = Color.FromArgb(23, 23, 25);
    private static readonly Color BorderColor = Color.FromArgb(72, 72, 77);
    private static readonly Color TextColor = Color.FromArgb(232, 237, 244);
    private static readonly Color MutedColor = Color.FromArgb(161, 172, 187);
    private static readonly Color AccentColor = Color.FromArgb(65, 128, 236);
    private static readonly Color DangerColor = Color.FromArgb(194, 67, 67);

    private readonly TextBox searchBox = new();
    private readonly ListView cardList = new();
    private readonly NumericUpDown cardIdInput = new();
    private readonly TextBox cardNameInput = new();
    private readonly TextBox oracleIdInput = new();
    private readonly TextBox setCodeInput = new();
    private readonly TextBox collectorInput = new();
    private readonly CheckedListBox cardTypesInput = new();
    private readonly TextBox manaCostInput = new();
    private readonly NumericUpDown powerInput = new();
    private readonly NumericUpDown toughnessInput = new();
    private readonly TextBox scriptPathInput = new();
    private readonly TextBox rulesTextInput = new();
    private readonly CheckBox enabledInput = new();
    private readonly DataGridView effectsGrid = new();
    private readonly ToolStrip toolStrip = new();
    private readonly ToolStripStatusLabel statusLabel = new();

    private CardDatabaseService? database;
    private List<CardSummary> summaries = new();
    private string? currentCardKey;
    private bool dirty;
    private bool loading;
    private bool suppressSelection;

    public MainForm(string? initialDatabase)
    {
        Text = "MTGB Card Database Editor";
        MinimumSize = new Size(1180, 760);
        Size = new Size(1440, 900);
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        WindowState = FormWindowState.Maximized;
        Font = new Font("Segoe UI", 10f);
        BackColor = WindowColor;
        ForeColor = TextColor;

        BuildUi();
        HookEvents();
        ApplyTheme(this);

        if (initialDatabase != null) OpenDatabase(initialDatabase);
        else UpdateStatus("打开或新建一个 .mtgbdb 数据库。", false);
    }

    private void BuildUi()
    {
        BuildToolbar();

        var statusStrip = new StatusStrip
        {
            BackColor = PanelColor,
            ForeColor = MutedColor,
            SizingGrip = false
        };
        statusStrip.Items.Add(statusLabel);

        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            FixedPanel = FixedPanel.Panel1,
            SplitterDistance = 390,
            SplitterWidth = 6,
            BackColor = BorderColor
        };
        split.Panel1.Padding = new Padding(12);
        split.Panel2.Padding = new Padding(12);
        split.Panel1.BackColor = WindowColor;
        split.Panel2.BackColor = WindowColor;
        split.Panel1.Controls.Add(BuildCardBrowser());
        split.Panel2.Controls.Add(BuildEditor());

        Controls.Add(split);
        Controls.Add(statusStrip);
        Controls.Add(toolStrip);
    }

    private void BuildToolbar()
    {
        toolStrip.Dock = DockStyle.Top;
        toolStrip.GripStyle = ToolStripGripStyle.Hidden;
        toolStrip.Padding = new Padding(8, 5, 8, 5);
        toolStrip.BackColor = PanelColor;
        toolStrip.ForeColor = TextColor;
        toolStrip.RenderMode = ToolStripRenderMode.System;

        toolStrip.Items.Add(CreateToolButton("打开", OpenDatabaseDialog));
        toolStrip.Items.Add(CreateToolButton("新建数据库", CreateDatabaseDialog));
        toolStrip.Items.Add(new ToolStripSeparator());
        toolStrip.Items.Add(CreateToolButton("新卡", NewCard));
        toolStrip.Items.Add(CreateToolButton("保存", () => SaveCurrentCard(), true));
        toolStrip.Items.Add(CreateToolButton("删除", DeleteCurrentCard, false, true));
        toolStrip.Items.Add(new ToolStripSeparator());
        toolStrip.Items.Add(CreateToolButton("校验数据库", ValidateCurrentDatabase));
    }

    private Control BuildCardBrowser()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = WindowColor
        };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        panel.Controls.Add(new Label
        {
            Text = "卡牌",
            Dock = DockStyle.Fill,
            Font = new Font(Font, FontStyle.Bold),
            ForeColor = TextColor,
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);

        searchBox.Dock = DockStyle.Fill;
        searchBox.PlaceholderText = "搜索名称、ID、系列或收藏编号";
        panel.Controls.Add(searchBox, 0, 1);

        cardList.Dock = DockStyle.Fill;
        cardList.View = View.Details;
        cardList.FullRowSelect = true;
        cardList.HideSelection = false;
        cardList.MultiSelect = false;
        cardList.Columns.Add("ID", 70);
        cardList.Columns.Add("名称", 175);
        cardList.Columns.Add("印刷", 105);
        panel.Controls.Add(cardList, 0, 2);
        return panel;
    }

    private Control BuildEditor()
    {
        var editor = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = WindowColor
        };
        editor.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        editor.RowStyles.Add(new RowStyle(SizeType.Percent, 62));
        editor.RowStyles.Add(new RowStyle(SizeType.Percent, 38));

        editor.Controls.Add(new Label
        {
            Text = "卡牌定义",
            Dock = DockStyle.Fill,
            Font = new Font(Font, FontStyle.Bold),
            ForeColor = TextColor,
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);
        editor.Controls.Add(BuildFields(), 0, 1);
        editor.Controls.Add(BuildEffects(), 0, 2);
        return editor;
    }

    private Control BuildFields()
    {
        var fields = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 8,
            Padding = new Padding(0, 0, 0, 10),
            BackColor = WindowColor
        };
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 116));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 116));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        for (int i = 0; i < 6; i++) fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));
        fields.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        cardIdInput.Minimum = 1;
        cardIdInput.Maximum = int.MaxValue;
        powerInput.Minimum = toughnessInput.Minimum = -999;
        powerInput.Maximum = toughnessInput.Maximum = 999;
        enabledInput.Text = "启用";
        enabledInput.Checked = true;

        AddField(fields, "内部 ID", cardIdInput, 0, 0);
        AddField(fields, "卡名", cardNameInput, 0, 2);
        AddField(fields, "Oracle ID", oracleIdInput, 1, 0);
        AddField(fields, "系列代号", setCodeInput, 1, 2);
        AddField(fields, "收藏编号", collectorInput, 2, 0);
        AddField(fields, "法术力费用", manaCostInput, 2, 2);
        AddField(fields, "力量", powerInput, 3, 0);
        AddField(fields, "防御", toughnessInput, 3, 2);
        AddField(fields, "脚本路径", scriptPathInput, 4, 0, 3);
        AddField(fields, "状态", enabledInput, 5, 0);

        cardTypesInput.CheckOnClick = true;
        cardTypesInput.MultiColumn = true;
        cardTypesInput.ColumnWidth = 132;
        foreach (CardTypeFlags type in Enum.GetValues<CardTypeFlags>())
        {
            if (type != CardTypeFlags.None) cardTypesInput.Items.Add(type);
        }
        AddField(fields, "卡牌类型", cardTypesInput, 6, 0, 3);

        rulesTextInput.Multiline = true;
        rulesTextInput.ScrollBars = ScrollBars.Vertical;
        AddField(fields, "规则文本", rulesTextInput, 7, 0, 3);
        return fields;
    }

    private Control BuildEffects()
    {
        var group = new GroupBox
        {
            Text = "效果（可添加多条）",
            Dock = DockStyle.Fill,
            ForeColor = TextColor,
            Padding = new Padding(10)
        };

        effectsGrid.Dock = DockStyle.Fill;
        effectsGrid.AllowUserToAddRows = true;
        effectsGrid.AllowUserToDeleteRows = true;
        effectsGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        effectsGrid.RowHeadersVisible = false;
        effectsGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        effectsGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Order", HeaderText = "顺序", FillWeight = 16 });
        effectsGrid.Columns.Add(new DataGridViewComboBoxColumn
        {
            Name = "Trigger",
            HeaderText = "触发时机",
            FillWeight = 28,
            DataSource = new[] { "on_play", "activated", "triggered", "replacement", "static" }
        });
        effectsGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "EffectKey", HeaderText = "效果键", FillWeight = 42 });
        effectsGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Parameters", HeaderText = "参数 JSON", FillWeight = 62 });
        group.Controls.Add(effectsGrid);
        return group;
    }

    private void HookEvents()
    {
        searchBox.TextChanged += (_, _) => RefreshCardList();
        cardList.SelectedIndexChanged += (_, _) => CardSelectionChanged();

        foreach (TextBox input in new[] { cardNameInput, oracleIdInput, setCodeInput, collectorInput, manaCostInput, scriptPathInput, rulesTextInput })
            input.TextChanged += (_, _) => MarkDirty();
        foreach (NumericUpDown input in new[] { cardIdInput, powerInput, toughnessInput })
            input.ValueChanged += (_, _) => MarkDirty();
        enabledInput.CheckedChanged += (_, _) => MarkDirty();
        cardTypesInput.ItemCheck += (_, _) => MarkDirty();
        effectsGrid.CellValueChanged += (_, _) => MarkDirty();
        effectsGrid.RowsAdded += (_, _) => MarkDirty();
        effectsGrid.RowsRemoved += (_, _) => MarkDirty();
        FormClosing += (_, eventArgs) =>
        {
            if (!ConfirmDiscardOrSave()) eventArgs.Cancel = true;
            else database?.Dispose();
        };
    }

    private void OpenDatabaseDialog()
    {
        if (!ConfirmDiscardOrSave()) return;
        using var dialog = new OpenFileDialog
        {
            Filter = "MTGB 卡牌数据库 (*.mtgbdb)|*.mtgbdb|所有文件 (*.*)|*.*",
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) == DialogResult.OK) OpenDatabase(dialog.FileName);
    }

    private void CreateDatabaseDialog()
    {
        if (!ConfirmDiscardOrSave()) return;
        using var dialog = new SaveFileDialog
        {
            Filter = "MTGB 卡牌数据库 (*.mtgbdb)|*.mtgbdb",
            DefaultExt = "mtgbdb",
            AddExtension = true,
            FileName = "custom.mtgbdb"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            string schemaPath = Path.Combine(AppContext.BaseDirectory, "schema.sql");
            CardDatabaseService.Create(dialog.FileName, schemaPath);
            OpenDatabase(dialog.FileName);
        }
        catch (Exception exception)
        {
            ShowError("无法创建数据库", exception);
        }
    }

    private void OpenDatabase(string path)
    {
        try
        {
            var opened = new CardDatabaseService(path);
            database?.Dispose();
            database = opened;
            currentCardKey = null;
            dirty = false;
            Text = $"MTGB Card Database Editor - {Path.GetFileName(path)}";
            LoadSummaries();
            if (summaries.Count > 0)
            {
                LoadCard(opened.LoadCard(summaries[0].CardKey));
                RefreshCardList(currentCardKey);
            }
            else
            {
                ClearForm();
            }
            UpdateStatus($"已打开 {path}", false);
        }
        catch (Exception exception)
        {
            ShowError("无法打开数据库", exception);
        }
    }

    private void LoadSummaries(string? selectKey = null)
    {
        summaries = database?.LoadSummaries() ?? new List<CardSummary>();
        RefreshCardList(selectKey);
    }

    private void RefreshCardList(string? selectKey = null)
    {
        string filter = searchBox.Text.Trim();
        suppressSelection = true;
        cardList.BeginUpdate();
        cardList.Items.Clear();
        foreach (CardSummary summary in summaries)
        {
            if (!Matches(summary, filter)) continue;
            var item = new ListViewItem(summary.CardId.ToString()) { Tag = summary };
            item.SubItems.Add(summary.Name);
            item.SubItems.Add($"{summary.SetCode}/{summary.CollectorNumber}");
            cardList.Items.Add(item);
            if (selectKey != null && summary.CardKey.Equals(selectKey, StringComparison.OrdinalIgnoreCase)) item.Selected = true;
        }
        cardList.EndUpdate();
        suppressSelection = false;
    }

    private void CardSelectionChanged()
    {
        if (suppressSelection || cardList.SelectedItems.Count == 0 || database == null) return;
        if (cardList.SelectedItems[0].Tag is not CardSummary summary) return;
        if (summary.CardKey == currentCardKey) return;

        if (!ConfirmDiscardOrSave())
        {
            RefreshCardList(currentCardKey);
            return;
        }

        try { LoadCard(database.LoadCard(summary.CardKey)); }
        catch (Exception exception) { ShowError("无法读取卡牌", exception); }
    }

    private void LoadCard(CardRecord card)
    {
        loading = true;
        currentCardKey = card.OriginalCardKey;
        cardIdInput.Value = Math.Clamp(card.CardId, 1, int.MaxValue);
        cardNameInput.Text = card.Name;
        oracleIdInput.Text = card.OracleId;
        setCodeInput.Text = card.SetCode;
        collectorInput.Text = card.CollectorNumber;
        manaCostInput.Text = card.ManaCost;
        powerInput.Value = Math.Clamp(card.Power, -999, 999);
        toughnessInput.Value = Math.Clamp(card.Toughness, -999, 999);
        scriptPathInput.Text = card.ScriptPath;
        rulesTextInput.Text = card.RulesText;
        enabledInput.Checked = card.Enabled;

        for (int i = 0; i < cardTypesInput.Items.Count; i++)
        {
            var type = (CardTypeFlags)cardTypesInput.Items[i];
            cardTypesInput.SetItemChecked(i, card.Types.HasFlag(type));
        }

        effectsGrid.Rows.Clear();
        foreach (CardEffectRecord effect in card.Effects)
            effectsGrid.Rows.Add(effect.Order, effect.Trigger, effect.EffectKey, effect.ParametersJson);

        loading = false;
        dirty = false;
        UpdateStatus($"正在编辑 {card.CardKey}", false);
    }

    private void NewCard()
    {
        if (database == null)
        {
            MessageBox.Show(this, "请先打开数据库。", "没有数据库", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (!ConfirmDiscardOrSave()) return;
        currentCardKey = null;
        suppressSelection = true;
        cardList.SelectedItems.Clear();
        suppressSelection = false;
        ClearForm();
        cardIdInput.Value = summaries.Count == 0 ? 1 : Math.Min(int.MaxValue, summaries.Max(card => card.CardId) + 1L);
        setCodeInput.Text = "CUSTOM";
        collectorInput.Text = "1";
        cardTypesInput.SetItemChecked(IndexOfType(CardTypeFlags.Creature), true);
        enabledInput.Checked = true;
        dirty = true;
        cardNameInput.Focus();
        UpdateStatus("新卡牌尚未保存。", true);
    }

    private bool SaveCurrentCard()
    {
        if (database == null) return false;
        try
        {
            CardRecord card = ReadForm();
            database.Save(card);
            currentCardKey = card.OriginalCardKey;
            dirty = false;
            LoadSummaries(currentCardKey);
            UpdateStatus($"已保存 {currentCardKey}", false);
            return true;
        }
        catch (Exception exception)
        {
            ShowError("无法保存卡牌", exception);
            return false;
        }
    }

    private void DeleteCurrentCard()
    {
        if (database == null || currentCardKey == null) return;
        if (MessageBox.Show(this, $"删除 {currentCardKey}？此操作无法撤销。", "删除卡牌",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;

        try
        {
            database.Delete(currentCardKey);
            currentCardKey = null;
            dirty = false;
            LoadSummaries();
            ClearForm();
            UpdateStatus("卡牌已删除。", false);
        }
        catch (Exception exception)
        {
            ShowError("无法删除卡牌", exception);
        }
    }

    private CardRecord ReadForm()
    {
        var card = new CardRecord
        {
            OriginalCardKey = currentCardKey,
            CardId = decimal.ToInt32(cardIdInput.Value),
            Name = cardNameInput.Text,
            OracleId = oracleIdInput.Text,
            SetCode = setCodeInput.Text,
            CollectorNumber = collectorInput.Text,
            ManaCost = manaCostInput.Text,
            Power = decimal.ToInt32(powerInput.Value),
            Toughness = decimal.ToInt32(toughnessInput.Value),
            ScriptPath = scriptPathInput.Text,
            RulesText = rulesTextInput.Text,
            Enabled = enabledInput.Checked,
            Types = ReadTypes()
        };

        for (int i = 0; i < effectsGrid.Rows.Count; i++)
        {
            DataGridViewRow row = effectsGrid.Rows[i];
            if (row.IsNewRow) continue;
            string key = Convert.ToString(row.Cells["EffectKey"].Value)?.Trim() ?? string.Empty;
            if (key.Length == 0) continue;
            int order = int.TryParse(Convert.ToString(row.Cells["Order"].Value), out int parsedOrder) ? parsedOrder : i;
            card.Effects.Add(new CardEffectRecord
            {
                Order = order,
                Trigger = Convert.ToString(row.Cells["Trigger"].Value)?.Trim() ?? "on_play",
                EffectKey = key,
                ParametersJson = Convert.ToString(row.Cells["Parameters"].Value)?.Trim() is { Length: > 0 } json ? json : "{}"
            });
        }
        return card;
    }

    private void ValidateCurrentDatabase()
    {
        if (database == null) return;
        try
        {
            DatabaseValidationResult result = database.ValidateDatabase();
            string message = $"Schema: {result.SchemaVersion}\n卡牌: {result.CardCount}\n效果: {result.EffectCount}";
            if (result.Errors.Count > 0) message += "\n\n" + string.Join("\n", result.Errors.Take(12));
            MessageBox.Show(this, message, result.IsValid ? "数据库有效" : "数据库存在问题",
                MessageBoxButtons.OK, result.IsValid ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        }
        catch (Exception exception)
        {
            ShowError("无法校验数据库", exception);
        }
    }

    private bool ConfirmDiscardOrSave()
    {
        if (!dirty) return true;
        DialogResult result = MessageBox.Show(this, "当前卡牌有未保存的修改。", "未保存修改",
            MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1);
        return result switch
        {
            DialogResult.Yes => SaveCurrentCard(),
            DialogResult.No => true,
            _ => false
        };
    }

    private void ClearForm()
    {
        loading = true;
        cardIdInput.Value = 1;
        cardNameInput.Clear();
        oracleIdInput.Clear();
        setCodeInput.Clear();
        collectorInput.Clear();
        manaCostInput.Clear();
        powerInput.Value = 0;
        toughnessInput.Value = 0;
        scriptPathInput.Clear();
        rulesTextInput.Clear();
        enabledInput.Checked = true;
        for (int i = 0; i < cardTypesInput.Items.Count; i++) cardTypesInput.SetItemChecked(i, false);
        effectsGrid.Rows.Clear();
        loading = false;
        dirty = false;
    }

    private CardTypeFlags ReadTypes()
    {
        CardTypeFlags result = CardTypeFlags.None;
        foreach (object item in cardTypesInput.CheckedItems) result |= (CardTypeFlags)item;
        return result;
    }

    private int IndexOfType(CardTypeFlags target)
    {
        for (int i = 0; i < cardTypesInput.Items.Count; i++)
            if ((CardTypeFlags)cardTypesInput.Items[i] == target) return i;
        return -1;
    }

    private void MarkDirty()
    {
        if (loading) return;
        dirty = true;
        UpdateStatus(currentCardKey == null ? "新卡牌尚未保存。" : $"{currentCardKey} 有未保存修改。", true);
    }

    private static bool Matches(CardSummary card, string filter)
    {
        if (filter.Length == 0) return true;
        return card.Name.Contains(filter, StringComparison.CurrentCultureIgnoreCase)
               || card.CardId.ToString().Contains(filter, StringComparison.OrdinalIgnoreCase)
               || card.SetCode.Contains(filter, StringComparison.OrdinalIgnoreCase)
               || card.CollectorNumber.Contains(filter, StringComparison.OrdinalIgnoreCase)
               || card.Types.ToString().Contains(filter, StringComparison.OrdinalIgnoreCase);
    }

    private void UpdateStatus(string text, bool isDirty)
    {
        statusLabel.Text = isDirty ? $"● {text}" : text;
        statusLabel.ForeColor = isDirty ? Color.FromArgb(244, 180, 72) : MutedColor;
    }

    private static ToolStripButton CreateToolButton(string text, Action action, bool accent = false, bool danger = false)
    {
        var button = new ToolStripButton(text)
        {
            DisplayStyle = ToolStripItemDisplayStyle.Text,
            ForeColor = danger ? Color.FromArgb(255, 155, 155) : accent ? Color.FromArgb(137, 181, 255) : TextColor,
            Margin = new Padding(3, 0, 3, 0)
        };
        button.Click += (_, _) => action();
        return button;
    }

    private static void AddField(TableLayoutPanel table, string label, Control input, int row, int column, int span = 1)
    {
        var fieldLabel = new Label
        {
            Text = label,
            Dock = DockStyle.Fill,
            ForeColor = MutedColor,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(0, 0, 6, 0),
            AutoEllipsis = true
        };
        input.Dock = DockStyle.Fill;
        input.Margin = new Padding(0, 5, 12, 5);
        table.Controls.Add(fieldLabel, column, row);
        table.Controls.Add(input, column + 1, row);
        if (span > 1) table.SetColumnSpan(input, span);
    }

    private void ApplyTheme(Control control)
    {
        control.Font = Font;
        if (control is TextBoxBase or NumericUpDown or CheckedListBox or ListView)
        {
            control.BackColor = InputColor;
            control.ForeColor = TextColor;
        }
        if (control is Button button)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderColor = BorderColor;
            button.BackColor = PanelColor;
            button.ForeColor = TextColor;
        }
        if (control is DataGridView grid)
        {
            grid.BackgroundColor = InputColor;
            grid.BorderStyle = BorderStyle.FixedSingle;
            grid.GridColor = BorderColor;
            grid.EnableHeadersVisualStyles = false;
            grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = PanelColor,
                ForeColor = TextColor,
                SelectionBackColor = PanelColor,
                Font = new Font(Font, FontStyle.Bold)
            };
            grid.DefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = InputColor,
                ForeColor = TextColor,
                SelectionBackColor = AccentColor,
                SelectionForeColor = Color.White
            };
            grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(30, 36, 45);
        }
        foreach (Control child in control.Controls) ApplyTheme(child);
    }

    private void ShowError(string title, Exception exception)
    {
        MessageBox.Show(this, exception.Message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
        UpdateStatus(exception.Message, true);
    }
}
