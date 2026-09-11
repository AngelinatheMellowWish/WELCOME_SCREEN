using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using Object1688.Shared;
using Object1688.Shared.Config;
using Object1688.Shared.Ipc;
using Object1688.Shared.Text;
using Object1688.Shared.Ui;
using MatchType = Object1688.Shared.Config.MatchType;
using Color = System.Windows.Media.Color;
using Point = System.Windows.Point;
using ComboBox = System.Windows.Controls.ComboBox;

namespace Object1688.ConfigUI;

/// <summary>
/// 配置窗口主窗体（架构 §5.8 左规则列表 + 右编辑/预览；M4b 最小骨架）。
/// 职责：加载用户配置 → 规则增删/编辑/启停 → 保存（校验 → ConfigSaver 原子写 → ConfigChanged 上报热生效）
/// → 测试显示（TestPlay 经 Main 转 TriggerCommand 下发 Overlay 真实播放）。
/// 关闭窗口 = 隐藏（可再次打开，架构 §2.4）；进程生命周期由 Main 经 IPC Shutdown 指令收尾。
/// </summary>
public partial class MainWindow : Window
{
    /// <summary>规则列表模型（绑定左栏 ListBox：启用开关 + 匹配摘要）。</summary>
    private readonly ObservableCollection<RuleEditModel> _rules = new();

    /// <summary>当前配置中非规则节（global/welcome/manual/dnd/sound 等）：保存时原样保留，仅规则节由 UI 重建。</summary>
    private AppConfig _baseConfig = ConfigLoader.CreateDefault();

    /// <summary>全局默认节（含描边方式；"勿扰/提示音/自启"页的全局描边下拉可改）。</summary>
    private GlobalConfig _globalCfg = ConfigLoader.CreateDefault().Global;

    /// <summary>用户配置路径（与 Main 默认一致：%APPDATA%\Object1688\config.json）。</summary>
    private readonly string _configPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Object1688", "config.json");

    /// <summary>当前选中规则（Editor 各控件的数据来源）。</summary>
    private RuleEditModel? _editing;

    /// <summary>窗口加载标记：防止构造期控件事件在列表填充前触发。</summary>
    private bool _loadGuard;

    /// <summary>是否有未保存修改（AC-38：关闭/切换时提示依据）。</summary>
    private bool _dirty;

    /// <summary>最近一次保存的重叠冲突提示（保存成功后在状态栏展示）。</summary>
    private string _lastOverlapWarn = string.Empty;

    /// <summary>预览拖拽状态（AC-66：拖拽文字设置自定义位置）。</summary>
    private bool _previewDragging;
    private Point _previewDragOffset;

    /// <summary>页签编辑的目标节（欢迎/手动/勿扰/提示音/自启），保存时并入 AppConfig。</summary>
    private WelcomeConfig _welcomeCfg = ConfigLoader.CreateDefault().Welcome;

    private ManualConfig _manualCfg = ConfigLoader.CreateDefault().Manual;

    private DndConfig _dndCfg = ConfigLoader.CreateDefault().Dnd;

    private SoundConfig _soundCfg = ConfigLoader.CreateDefault().Sound;

    private bool _autostartValue = true;

    /// <summary>规则结构操作撤销/重做栈（AC-64：新建/删除/复制/移动/批量；保存/切换时清空由调用方）。</summary>
    private readonly UndoRedoStack<IReadOnlyList<RuleConfig>> _ruleUndo = new(capacity: 30);

    /// <summary>初始化主窗口：装配列表/下拉框数据源并加载用户配置。</summary>
    public MainWindow()
    {
        InitializeComponent();
        RulesListBox.ItemsSource = _rules;

        MatchTypeCombo.ItemsSource = new[] { MatchType.Process, MatchType.WindowTitle };
        MatchModeCombo.ItemsSource = new[] { MatchMode.Exact, MatchMode.Contains, MatchMode.Wildcard };
        PositionCombo.ItemsSource = new[] { "center", "top-center", "custom" };
        PresetCombo.DisplayMemberPath = nameof(StylePreset.DisplayName);
        PresetCombo.ItemsSource = StylePresets.All;

        LoadConfig();
    }

    /// <summary>窗口预览按键：Ctrl+Z / Ctrl+Y 触发规则结构撤销/重做（AC-64）。</summary>
    protected override void OnPreviewKeyDown(System.Windows.Input.KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        if (e.Key == System.Windows.Input.Key.Z && System.Windows.Input.Keyboard.Modifiers.HasFlag(System.Windows.Input.ModifierKeys.Control))
        {
            var restored = _ruleUndo.Undo();
            if (restored is not null)
            {
                RestoreRules(restored);
            }
            else
            {
                StatusText.Text = "无可撤销操作";
            }

            e.Handled = true;
        }
        else if (e.Key == System.Windows.Input.Key.Y && System.Windows.Input.Keyboard.Modifiers.HasFlag(System.Windows.Input.ModifierKeys.Control))
        {
            var next = _ruleUndo.Redo();
            if (next is not null)
            {
                RestoreRules(next);
            }
            else
            {
                StatusText.Text = "无可重做操作";
            }

            e.Handled = true;
        }
    }

    /// <summary>加载用户配置（回退链 CFG-W-1002 提示）；规则节导入编辑模型 + 各页签节回填控件。</summary>
    private void LoadConfig()
    {
        ConfigLoadResult result;
        _loadGuard = true;
        try
        {
            result = ConfigLoader.Load(_configPath);
            var config = result.Config;
            _baseConfig = config;
            _globalCfg = config.Global;

            _rules.Clear();
            foreach (var rule in config.Rules)
            {
                _rules.Add(RuleEditModel.FromConfig(rule));
            }

            _welcomeCfg = config.Welcome;
            _manualCfg = config.Manual;
            _dndCfg = config.Dnd;
            _soundCfg = config.Sound;
            _autostartValue = config.Autostart;
            RefreshTabControlsFromConfig();
        }
        finally
        {
            _loadGuard = false;
        }

        // 选中在守卫释放后设置：SelectionChanged 事件才会触发编辑器回填
        // 规则列表为空 → 空状态（F-28）：不自动新建，预览区提示引导
        RulesListBox.SelectedIndex = _rules.Count > 0 ? 0 : -1;

        _dirty = false;
        var issueText = string.Join("；", result.Issues.Select(i => $"{i.ErrorCode} {i.Message}"));
        StatusText.Text = string.IsNullOrEmpty(issueText) ? $"已加载 {_configPath}" : $"已加载（问题：{issueText}）";
    }

    /// <summary>将当前页签目标节写回欢迎/手动/勿扰/提示音/自启控件（加载与恢复默认后调用）。</summary>
    private void RefreshTabControlsFromConfig()
    {
        // 欢迎大字
        WelcomeEnabledCheck.IsChecked = _welcomeCfg.Enabled;
        WelcomeLinesBox.Text = string.Join(Environment.NewLine, _welcomeCfg.DisplayLines.Select(l => l.Text));
        WelcomeFontSizeBox.Text = (_welcomeCfg.DisplayLines.FirstOrDefault(l => l.FontSize > 0)?.FontSize ?? 0)
            .ToString(CultureInfo.InvariantCulture);
        WelcomeDelaySecondsBox.Text = _welcomeCfg.DelaySeconds.ToString(CultureInfo.InvariantCulture);
        WelcomeHoldSecondsBox.Text = _welcomeCfg.HoldSeconds.ToString(CultureInfo.InvariantCulture);

        // 手动大字
        ManualEnabledCheck.IsChecked = _manualCfg.Enabled;
        ManualLinesBox.Text = string.Join(Environment.NewLine, _manualCfg.DisplayLines.Select(l => l.Text));
        ManualFontSizeBox.Text = (_manualCfg.FontSize > 0 ? _manualCfg.FontSize : 0).ToString(CultureInfo.InvariantCulture);
        ManualHoldSecondsBox.Text = _manualCfg.HoldSeconds.ToString(CultureInfo.InvariantCulture);
        ManualShortcutBox.Text = _manualCfg.Shortcut ?? string.Empty;
        SelectOutlineMode(ManualOutlineModeCombo, _manualCfg.OutlineMode);

        // 全局描边方式（架构 §3.3）
        SelectOutlineMode(GlobalOutlineModeCombo, _globalCfg.OutlineMode);

        // 勿扰 / 提示音 / 自启
        DndPausedCheck.IsChecked = _dndCfg.Paused;
        DndScheduleEnabledCheck.IsChecked = _dndCfg.ScheduleEnabled;
        DndPresentationSilenceCheck.IsChecked = _dndCfg.PresentationAutoSilence;
        SoundEnabledCheck.IsChecked = _soundCfg.Enabled;
        SoundVolumeBox.Text = _soundCfg.Volume.ToString(CultureInfo.InvariantCulture);
        SoundCustomPathBox.Text = _soundCfg.CustomPath;
        foreach (var item in SoundSourceCombo.Items)
        {
            if (item is ComboBoxItem cb && string.Equals(cb.Tag as string, _soundCfg.Source, StringComparison.OrdinalIgnoreCase))
            {
                SoundSourceCombo.SelectedItem = item;
                break;
            }
        }

        AutostartCheck.IsChecked = _autostartValue;
    }

    /// <summary>规则列表选中变化 → 刷新编辑区 + 预览；有未保存修改时先确认（AC-38）。</summary>
    private void OnRuleSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loadGuard)
        {
            return;
        }

        if (_dirty && !ConfirmUnsavedChanges())
        {
            // 取消切换：回退到原选中项（守卫避免递归触发）
            _loadGuard = true;
            RulesListBox.SelectedItem = _editing;
            _loadGuard = false;
            return;
        }

        _editing = RulesListBox.SelectedItem as RuleEditModel;
        RefreshEditorFromModel();
        UpdatePreview();
    }

    /// <summary>ComboBox（匹配类型/匹配模式/位置）选中变化 → 同步编辑模型。</summary>
    private void OnRuleComboSelChanged(object sender, SelectionChangedEventArgs e)
        => SyncEditingFromControls();

    /// <summary>TextBox（匹配值/目标屏/文字行/字号/延时/保持/描边）文本变化 → 同步编辑模型。</summary>
    private void OnRuleTextChanged(object sender, TextChangedEventArgs e)
        => SyncEditingFromControls();

    /// <summary>CheckBox（启用/大小写/全屏）变化 → 同步编辑模型。</summary>
    private void OnRuleCheckedChanged(object sender, RoutedEventArgs e)
        => SyncEditingFromControls();

    /// <summary>将编辑控件当前值回写编辑模型并刷新列表摘要/预览。</summary>
    private void SyncEditingFromControls()
    {
        if (_loadGuard || _editing is null)
        {
            return;
        }

        _editing.MatchType = MatchTypeCombo.SelectedItem is MatchType mt ? mt : MatchType.Process;
        _editing.MatchMode = MatchModeCombo.SelectedItem is MatchMode mm ? mm : MatchMode.Exact;
        _editing.MatchValue = MatchValueBox.Text;
        _editing.Enabled = EnableRuleCheck.IsChecked == true;
        _editing.MatchCaseSensitive = CaseSensitiveCheck.IsChecked == true;
        _editing.IncludeFullscreen = IncludeFullscreenCheck.IsChecked == true;
        _editing.TargetScreen = TargetScreenBox.Text;
        _editing.LinesText = LinesBox.Text;
        _editing.FontSize = ParseDouble(FontSizeBox.Text) ?? 0;
        _editing.DelaySeconds = ParseDouble(DelaySecondsBox.Text) ?? 0;
        _editing.HoldSeconds = ParseDouble(HoldSecondsBox.Text) ?? 0;
        _editing.Position = ResolvePositionSelection(PositionCombo.SelectedItem as string, _editing.Position);
        _editing.OutlineColor = OutlineColorBox.Text;
        UpdateOutlineColorSwatch();
        _editing.OutlineWidth = ParseDouble(OutlineWidthBox.Text) ?? -1;
        _editing.OutlineMode = OutlineModeTag(OutlineModeCombo);
        _editing.Touch(); // 触发 Summary 通知刷新列表

        MarkDirty();
        UpdatePreview();
    }

    /// <summary>常用色块点击（F-78/AC-100）：一键填入描边色。</summary>
    private void OnOutlineSwatchClicked(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string hex })
        {
            OutlineColorBox.Text = hex;
        }
    }

    /// <summary>系统取色器（F-78/AC-100）：ColorDialog 选色后填入 #RRGGBB（含 alpha 时用 #AARRGGBB）。</summary>
    private void OnPickOutlineColorClicked(object sender, RoutedEventArgs e)
    {
        using var dialog = new System.Windows.Forms.ColorDialog
        {
            FullOpen = true,
            AnyColor = true,
        };
        if (TryParseColor(OutlineColorBox.Text, out var current))
        {
            dialog.Color = System.Drawing.Color.FromArgb(current.A, current.R, current.G, current.B);
        }

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            var c = dialog.Color;
            OutlineColorBox.Text = c.A < 255
                ? $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}"
                : $"#{c.R:X2}{c.G:X2}{c.B:X2}";
        }
    }

    /// <summary>「跟随全局 / 清除」（F-78/AC-100）：清空描边色 = 跟随全局默认。</summary>
    private void OnClearOutlineColorClicked(object sender, RoutedEventArgs e)
        => OutlineColorBox.Text = string.Empty;

    /// <summary>更新描边色预览块（F-78/AC-100）：非法 / 空显示透明。</summary>
    private void UpdateOutlineColorSwatch()
    {
        if (OutlineColorSwatch is null)
        {
            return;
        }

        OutlineColorSwatch.Background = TryParseColor(OutlineColorBox.Text, out var color)
            ? new SolidColorBrush(color)
            : Brushes.Transparent;
    }

    /// <summary>解析 #RRGGBB / #AARRGGBB（大小写不敏感，可省 #）；失败返回 false。</summary>
    private static bool TryParseColor(string? text, out Color color)
    {
        color = Colors.Black;
        var s = (text ?? string.Empty).Trim();
        if (s.Length == 0)
        {
            return false;
        }

        if (!s.StartsWith('#'))
        {
            s = "#" + s;
        }

        try
        {
            if (ColorConverter.ConvertFromString(s) is Color parsed)
            {
                color = parsed;
                return true;
            }
        }
        catch (FormatException)
        {
            // 非法格式
        }
        catch (NotSupportedException)
        {
            // 无法转换
        }

        return false;
    }

    /// <summary>新建规则（F-28 引导入口）：追加一条默认规则并选中。</summary>
    private void OnNewRuleClicked(object sender, RoutedEventArgs e)
    {
        PushRuleUndo(); // AC-64：结构变更前压入快照（撤销回到变更前）
        var nextSeq = _rules.Count + 1;
        while (_rules.Any(r => r.RuleId == $"r-{nextSeq:D3}"))
        {
            nextSeq++;
        }

        var model = new RuleEditModel
        {
            RuleId = $"r-{nextSeq:D3}",
            MatchType = MatchType.Process,
            MatchMode = MatchMode.Contains,
            MatchValue = string.Empty,
            Enabled = true,
            LinesText = "新规则触发文字",
            FontSize = 96,
            DelaySeconds = 2,
            HoldSeconds = 4,
            Position = "center",
            OutlineColor = string.Empty,
            OutlineWidth = 0,
        };
        _rules.Add(model);
        ApplySearchFilter();
        RulesListBox.SelectedItem = model;
        StatusText.Text = $"已新建规则 {model.RuleId}（编辑后点保存生效）";
        MatchValueBox.Focus();
    }

    // ============ M4-E：规则列表增强（F-27/AC-56）+ 撤销重做（AC-64） ============

    /// <summary>将当前规则列表整体快照（供撤销）压栈。</summary>
    private void PushRuleUndo()
    {
        var snapshot = SnapshotRules();
        if (snapshot is not null)
        {
            _ruleUndo.Push(snapshot);
        }
    }

    /// <summary>当前规则模型 → RuleConfig 快照（无任何有效规则时返回 null，避免压空栈）。</summary>
    private IReadOnlyList<RuleConfig>? SnapshotRules()
    {
        var list = _rules
            .Select(m => m.ToConfig())
            .Where(c => c is not null)
            .Cast<RuleConfig>()
            .ToList();
        return list.Count == 0 ? null : list;
    }

    /// <summary>用撤销快照重建规则列表（模型 ← 配置；刷新选中与摘要）。</summary>
    private void RestoreRules(IReadOnlyList<RuleConfig> rules)
    {
        _loadGuard = true;
        try
        {
            _rules.Clear();
            foreach (var rule in rules)
            {
                _rules.Add(RuleEditModel.FromConfig(rule));
            }
        }
        finally
        {
            _loadGuard = false;
        }

        ApplySearchFilter();
        RulesListBox.SelectedIndex = _rules.Count > 0 ? 0 : -1;
        MarkDirty();
    }

    /// <summary>搜索过滤：按名称/匹配值子串过滤列表展示（F-27，大小写不敏感）。</summary>
    private void ApplySearchFilter()
    {
        var query = SearchBox?.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(query))
        {
            RulesListBox.ItemsSource = _rules;
            return;
        }

        RulesListBox.ItemsSource = _rules
            .Where(m =>
                m.RuleId.Contains(query, StringComparison.OrdinalIgnoreCase)
                || m.MatchValue.Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_loadGuard)
        {
            return;
        }

        ApplySearchFilter();
    }

    /// <summary>复制选中规则（深拷贝新 ruleId；插入其后；AC-56/F-27）。</summary>
    private void OnCopyRuleClicked(object sender, RoutedEventArgs e)
    {
        if (RulesListBox.SelectedItem is not RuleEditModel model)
        {
            StatusText.Text = "未选中规则，无法复制";
            return;
        }

        if (model.ToConfig() is not { } src)
        {
            StatusText.Text = "规则不完整，无法复制";
            return;
        }

        PushRuleUndo();
        var copy = RuleEditModel.FromConfig(src);
        copy.RuleId = UniqueRuleId();
        copy.MatchValue = src.MatchValue; // FromConfig 已带；此处无变化
        var idx = _rules.IndexOf(model);
        _rules.Insert(idx + 1, copy);
        ApplySearchFilter();
        RulesListBox.SelectedItem = copy;
        StatusText.Text = $"已复制 {src.RuleId} → {copy.RuleId}（点保存生效）";
    }

    private string UniqueRuleId()
    {
        var seq = _rules.Count + 1;
        while (_rules.Any(r => r.RuleId == $"r-{seq:D3}"))
        {
            seq++;
        }

        return $"r-{seq:D3}";
    }

    /// <summary>上移选中规则（顺序即同命中优先级，F-27）。</summary>
    private void OnMoveUpClicked(object sender, RoutedEventArgs e)
    {
        MoveSelected(-1);
    }

    /// <summary>下移选中规则。</summary>
    private void OnMoveDownClicked(object sender, RoutedEventArgs e)
    {
        MoveSelected(+1);
    }

    private void MoveSelected(int delta)
    {
        if (RulesListBox.SelectedItem is not RuleEditModel model)
        {
            StatusText.Text = "未选中规则，无法移动";
            return;
        }

        var idx = _rules.IndexOf(model);
        var target = idx + delta;
        if (target < 0 || target >= _rules.Count)
        {
            return;
        }

        PushRuleUndo();
        _rules.Move(idx, target);
        RulesListBox.SelectedItem = model;
        MarkDirty();
    }

    private void OnEnableAllClicked(object sender, RoutedEventArgs e)
    {
        SetAllEnabled(true);
    }

    private void OnDisableAllClicked(object sender, RoutedEventArgs e)
    {
        SetAllEnabled(false);
    }

    private void SetAllEnabled(bool enabled)
    {
        if (_rules.Count == 0)
        {
            StatusText.Text = "无规则";
            return;
        }

        PushRuleUndo();
        foreach (var m in _rules)
        {
            m.Enabled = enabled;
        }

        MarkDirty();
        StatusText.Text = enabled ? "已全部启用（点保存生效）" : "已全部停用（点保存生效）";
    }

    /// <summary>删除当前选中规则（配置保留在内存，保存后落盘）。</summary>
    private void OnDeleteRuleClicked(object sender, RoutedEventArgs e)
    {
        if (RulesListBox.SelectedItem is not RuleEditModel model)
        {
            StatusText.Text = "未选中规则，无法删除";
            return;
        }

        PushRuleUndo(); // AC-64：删除前压入快照（撤销可恢复）
        _rules.Remove(model);
        _editing = null;
        ClearEditorControls();
        ApplySearchFilter();
        UpdatePreview();
        StatusText.Text = $"已删除规则 {model.RuleId}（点保存生效）";
    }

    /// <summary>恢复默认：以内置默认重建各节（不落盘，需点保存）。未保存修改将被丢弃 → 先确认（AC-38）。</summary>
    private void OnRestoreDefaultsClicked(object sender, RoutedEventArgs e)
    {
        if (_dirty)
        {
            var confirm = MessageBox.Show(
                this,
                "当前有未保存修改，恢复默认将丢弃这些修改。确定继续？",
                "恢复默认",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.OK)
            {
                return;
            }
        }

        var defaults = ConfigLoader.CreateDefault();
        _baseConfig = defaults;
        _welcomeCfg = defaults.Welcome;
        _manualCfg = defaults.Manual;
        _dndCfg = defaults.Dnd;
        _soundCfg = defaults.Sound;
        _autostartValue = defaults.Autostart;

        _loadGuard = true;
        try
        {
            _rules.Clear();
            foreach (var rule in defaults.Rules)
            {
                _rules.Add(RuleEditModel.FromConfig(rule));
            }
        }
        finally
        {
            _loadGuard = false;
        }

        RefreshTabControlsFromConfig();

        // 守卫释放后再选中：触发 SelectionChanged 回填编辑器
        RulesListBox.SelectedIndex = _rules.Count > 0 ? 0 : -1;
        StatusText.Text = "已恢复内置默认（未保存，点保存生效）";
    }

    /// <summary>导出当前配置为 JSON 文件（F-22/AC-21；用户自选路径，写失败记 IO-E-6001）。</summary>
    private void OnExportClicked(object sender, RoutedEventArgs e)
    {
        CommitEditingRule();
        SyncTabsFromControls();
        var config = BuildConfig();
        if (config is null)
        {
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "导出配置",
            Filter = "JSON 配置 (*.json)|*.json|所有文件 (*.*)|*.*",
            FileName = ConfigImportExport.BuildTimestampedName(ConfigImportExport.ExportPrefix),
            InitialDirectory = ConfigImportExport.DefaultExportDirectory,
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            ConfigImportExport.ExportToFile(config, dialog.FileName);
            StatusText.Text = $"已导出配置：{dialog.FileName}";
        }
        catch (IOException ex)
        {
            StatusText.Text = ex.Message;
        }
    }

    /// <summary>从 JSON 文件导入配置（F-22/AC-21）：导入前自动备份 → 宽容解析 → 应用 + 写盘 + 热生效。</summary>
    private async void OnImportClicked(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "导入配置",
            Filter = "JSON 配置 (*.json)|*.json|所有文件 (*.*)|*.*",
            InitialDirectory = ConfigImportExport.DefaultExportDirectory,
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        ConfigLoadResult result;
        try
        {
            try
            {
                ConfigImportExport.BackupCurrent(_configPath);
            }
            catch (IOException ex)
            {
                StatusText.Text = ex.Message; // 备份失败不阻断（CFG-W-1006）
            }

            result = ConfigImportExport.ImportFromFile(dialog.FileName);
        }
        catch (IOException ex)
        {
            StatusText.Text = ex.Message;
            return;
        }

        if (result.Issues.Count > 0)
        {
            var preview = string.Join("；", result.Issues.Take(3).Select(i => i.Message));
            var confirm = MessageBox.Show(
                this,
                $"导入文件包含 {result.Issues.Count} 个问题：\n{preview}\n\n仍要应用可用部分吗？",
                "导入配置",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.OK)
            {
                StatusText.Text = "已取消导入";
                return;
            }
        }

        ApplyImportedConfig(result.Config);

        try
        {
            ConfigSaver.Save(_configPath, result.Config);
        }
        catch (IOException ex)
        {
            StatusText.Text = ex.Message;
            return;
        }

        try
        {
            if (App.Current is App app)
            {
                await app.SendToMainAsync(IpcMessageType.ConfigChanged, result.Config, TimeSpan.FromSeconds(3));
            }
        }
        catch (Exception ex) when (ex is OperationCanceledException or TimeoutException)
        {
            StatusText.Text = $"已导入写盘，但 ConfigChanged 广播超时：{ex.Message}";
            return;
        }

        _dirty = false;
        StatusText.Text = $"已导入并生效：{dialog.FileName}";
    }

    /// <summary>将导入的配置应用到内存与界面（不写盘；由调用方负责保存/广播）。</summary>
    private void ApplyImportedConfig(AppConfig config)
    {
        _baseConfig = config;
        _globalCfg = config.Global;
        _welcomeCfg = config.Welcome;
        _manualCfg = config.Manual;
        _dndCfg = config.Dnd;
        _soundCfg = config.Sound;
        _autostartValue = config.Autostart;

        _loadGuard = true;
        try
        {
            _rules.Clear();
            foreach (var rule in config.Rules)
            {
                _rules.Add(RuleEditModel.FromConfig(rule));
            }
        }
        finally
        {
            _loadGuard = false;
        }

        RefreshTabControlsFromConfig();
        ApplySearchFilter();
        RulesListBox.SelectedIndex = _rules.Count > 0 ? 0 : -1;
    }

    /// <summary>套用样式预设到当前规则（F-23/AC-60）：字号/描边/位置，套用后仍可微调。</summary>
    private void OnApplyPresetClicked(object sender, RoutedEventArgs e)
    {
        if (_editing is null)
        {
            StatusText.Text = "未选中规则，无法套用预设";
            return;
        }

        if (PresetCombo.SelectedItem is not StylePreset preset)
        {
            StatusText.Text = "请先选择样式预设";
            return;
        }

        _editing.FontSize = preset.FontSize;
        _editing.OutlineColor = preset.OutlineColor;
        _editing.OutlineWidth = preset.OutlineWidth;
        _editing.Position = preset.Position;
        RefreshEditorFromModel();
        MarkDirty();
        UpdatePreview();
        StatusText.Text = $"已套用预设 {preset.DisplayName}（可微调后保存）";
    }

    /// <summary>
    /// 保存：重建 AppConfig → 全量校验（CFG-V-1003 拦截）→ 冲突检测（重复拦截/重叠提示）→
    /// ConfigSaver 原子写（CFG-E-1010 失败保留旧文件）→ ConfigChanged 上报 Main（热生效，CFG-I-1004）。
    /// </summary>
    private async void OnSaveClicked(object sender, RoutedEventArgs e)
    {
        var config = SaveToDisk();
        if (config is null)
        {
            return; // 校验/冲突/写盘失败信息已写入状态栏
        }

        // 热生效：ConfigChanged 上报 Main（Main 更新内存并广播 Overlay）
        try
        {
            if (App.Current is App app)
            {
                await app.SendToMainAsync(IpcMessageType.ConfigChanged, config, TimeSpan.FromSeconds(3));
            }
        }
        catch (Exception ex) when (ex is OperationCanceledException or TimeoutException)
        {
            StatusText.Text = $"配置已写盘，但 ConfigChanged 广播超时：{ex.Message}";
            return;
        }

        _dirty = false;
        StatusText.Text = $"已保存并生效（{ErrorCodes.ConfigSaved}）{(_rules.Count == 0 ? "（无规则）" : string.Empty)}"
            + (string.IsNullOrEmpty(_lastOverlapWarn) ? string.Empty : $"；提示：{_lastOverlapWarn}");
    }

    /// <summary>
    /// 同步保存到磁盘：编辑回写 → 重建 AppConfig → 校验/冲突 → ConfigSaver 原子写。
    /// 成功返回配置（供广播热生效）；失败返回 null（状态栏已提示），旧文件保留。
    /// </summary>
    private AppConfig? SaveToDisk()
    {
        CommitEditingRule();
        SyncTabsFromControls();
        var config = BuildConfig();
        if (config is null)
        {
            return null; // 校验失败信息已写入状态栏
        }

        // 冲突/重复检测（F-29）：完全重复 = error 拦截；重叠 = warning 提示不阻断
        var conflicts = ConfigValidator.CheckConflicts(config.Rules);
        var blocking = conflicts.Where(c => c.Message.Contains("完全重复")).ToList();
        if (blocking.Count > 0)
        {
            StatusText.Text = $"保存被拦截：{blocking[0].Message}";
            return null;
        }

        try
        {
            ConfigSaver.Save(_configPath, config);
        }
        catch (IOException ex)
        {
            StatusText.Text = ex.Message;
            return null;
        }

        _lastOverlapWarn = string.Join("；", conflicts.Where(c => !c.Message.Contains("完全重复")).Select(c => c.Message));
        return config;
    }

    /// <summary>未保存修改确认（AC-38：关闭/切换 → 保存 / 放弃 / 取消）。</summary>
    /// <returns>true = 可继续（已保存或已放弃）；false = 用户取消或保存失败（应中止关闭/切换）。</returns>
    private bool ConfirmUnsavedChanges()
    {
        var choice = MessageBox.Show(
            this,
            "当前有未保存的修改。\n\n是（Y）= 保存后继续；否（N）= 放弃修改；取消 = 返回继续编辑。",
            "未保存的修改",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Warning);
        switch (choice)
        {
            case MessageBoxResult.Yes:
                var config = SaveToDisk();
                if (config is null)
                {
                    return false; // 保存失败：不关闭/不切换（状态栏已提示）
                }

                BroadcastConfig(config);
                _dirty = false;
                return true;
            case MessageBoxResult.No:
                _dirty = false; // 先清除，避免 LoadConfig 内选中变化再次触发确认
                LoadConfig();   // 重新从磁盘加载 = 真正放弃内存中的未保存修改
                return true;
            default:
                return false;
        }
    }

    /// <summary>热生效广播（ConfigChanged → Main）；失败仅提示不阻断关闭/切换。</summary>
    private async void BroadcastConfig(AppConfig config)
    {
        try
        {
            if (App.Current is App app)
            {
                await app.SendToMainAsync(IpcMessageType.ConfigChanged, config, TimeSpan.FromSeconds(3));
            }
        }
        catch (Exception ex) when (ex is OperationCanceledException or TimeoutException)
        {
            StatusText.Text = $"配置已写盘，但 ConfigChanged 广播超时：{ex.Message}";
        }
    }

    /// <summary>保存前将各页签控件当前值显式同步回目标节（冗余防御，事件已实时同步）。</summary>
    private void SyncTabsFromControls()
    {
        _welcomeCfg = RebuildWelcome();
        _manualCfg = RebuildManual();
        _globalCfg = RebuildGlobal();
        _dndCfg = new DndConfig
        {
            Paused = DndPausedCheck.IsChecked == true,
            ScheduleEnabled = DndScheduleEnabledCheck.IsChecked == true,
            PresentationAutoSilence = DndPresentationSilenceCheck.IsChecked == true,
            Schedule = _dndCfg.Schedule,
        };
        SyncSound();
        _autostartValue = AutostartCheck.IsChecked == true;
    }

    /// <summary>
    /// 测试显示（F-23）：组装当前选中规则为 BannerRequest（BannerAssembler 合并语义）→ TestPlay 上报 Main
    /// → Main 转 TriggerCommand 下发 Overlay 真实播放；无选中规则时回退欢迎大字。
    /// </summary>
    private async void OnTestPlayClicked(object sender, RoutedEventArgs e)
    {
        CommitEditingRule();
        BannerRequest request;
        if (RulesListBox.SelectedItem is RuleEditModel model)
        {
            var rule = model.ToConfig();
            if (rule is null)
            {
                StatusText.Text = "规则不完整，无法测试显示";
                return;
            }

            request = BannerAssembler.ComposeRule(rule, _globalCfg, _soundCfg, null, DateTimeOffset.UtcNow);
        }
        else if (_welcomeCfg.DisplayLines.Count > 0)
        {
            request = BannerAssembler.ComposeWelcome(_welcomeCfg, _globalCfg, _soundCfg);
        }
        else
        {
            StatusText.Text = "无规则且欢迎大字为空，无法测试显示";
            return;
        }

        try
        {
            if (App.Current is App app)
            {
                await app.SendToMainAsync(IpcMessageType.TestPlay, request, TimeSpan.FromSeconds(3));
            }

            StatusText.Text = "测试显示已下发 Overlay（TestPlay → TriggerCommand）";
        }
        catch (Exception ex) when (ex is OperationCanceledException or TimeoutException)
        {
            StatusText.Text = $"测试显示发送失败：{ex.Message}";
        }
    }

    /// <summary>窗口关闭 → 隐藏（可再次打开，架构 §2.4）；有未保存修改时先确认（AC-38）。</summary>
    protected override void OnClosing(CancelEventArgs e)
    {
        if (_lifetimeCancelled)
        {
            base.OnClosing(e); // App.Shutdown 路径：允许真正关闭
            return;
        }

        e.Cancel = true;
        if (_dirty && !ConfirmUnsavedChanges())
        {
            return; // 用户取消（或保存失败）：保持窗口
        }

        Hide();
    }

    /// <summary>App.Shutdown 已触发（优雅退出）：放行真正关闭。</summary>
    internal bool _lifetimeCancelled { get; set; }

    /// <summary>将当前编辑控件状态回写模型（选中切换/保存/测试前调用）。</summary>
    private void CommitEditingRule()
    {
        if (_editing is not null)
        {
            SyncEditingFromControls();
        }
    }

    /// <summary>编辑模型 → 编辑控件回填。</summary>
    private void RefreshEditorFromModel()
    {
        _loadGuard = true;
        try
        {
            ClearEditorControls();
            if (_editing is null)
            {
                return;
            }

            MatchTypeCombo.SelectedItem = _editing.MatchType;
            MatchModeCombo.SelectedItem = _editing.MatchMode;
            MatchValueBox.Text = _editing.MatchValue;
            EnableRuleCheck.IsChecked = _editing.Enabled;
            CaseSensitiveCheck.IsChecked = _editing.MatchCaseSensitive;
            IncludeFullscreenCheck.IsChecked = _editing.IncludeFullscreen;
            TargetScreenBox.Text = _editing.TargetScreen;
            LinesBox.Text = _editing.LinesText;
            FontSizeBox.Text = _editing.FontSize > 0 ? _editing.FontSize.ToString(CultureInfo.InvariantCulture) : string.Empty;
            DelaySecondsBox.Text = _editing.DelaySeconds.ToString(CultureInfo.InvariantCulture);
            HoldSecondsBox.Text = _editing.HoldSeconds.ToString(CultureInfo.InvariantCulture);
            PositionCombo.SelectedItem = PositionCombo.Items
                .Cast<string>()
                .FirstOrDefault(p => MatchesPositionItem(p, _editing.Position))
                ?? PositionCombo.Items.Cast<string>().First();
            OutlineColorBox.Text = _editing.OutlineColor;
            OutlineWidthBox.Text = _editing.OutlineWidth >= 0
                ? _editing.OutlineWidth.ToString(CultureInfo.InvariantCulture)
                : string.Empty;
            SelectOutlineMode(OutlineModeCombo, _editing.OutlineMode);
        }
        finally
        {
            _loadGuard = false;
        }
    }

    /// <summary>清空编辑控件（删除规则/无选中时）。</summary>
    private void ClearEditorControls()
    {
        MatchTypeCombo.SelectedIndex = -1;
        MatchModeCombo.SelectedIndex = -1;
        MatchValueBox.Text = string.Empty;
        EnableRuleCheck.IsChecked = false;
        CaseSensitiveCheck.IsChecked = false;
        IncludeFullscreenCheck.IsChecked = true;
        TargetScreenBox.Text = string.Empty;
        LinesBox.Text = string.Empty;
        FontSizeBox.Text = string.Empty;
        DelaySecondsBox.Text = string.Empty;
        HoldSecondsBox.Text = string.Empty;
        PositionCombo.SelectedIndex = -1;
        OutlineColorBox.Text = string.Empty;
        OutlineWidthBox.Text = string.Empty;
        OutlineModeCombo.SelectedIndex = 0;
    }

    /// <summary>预览：文字行 + 合并样式（字号/颜色/描边）实时渲染。</summary>
    private void UpdatePreview()
    {
        _loadGuard = true;
        try
        {
            if (RulesListBox.SelectedItem is not RuleEditModel model)
            {
                PreviewTextBlock.Text = $"{(_rules.Count == 0 ? "尚无规则 —— 点击上方“新建规则”开始（F-28）" : "（从左侧选择规则后显示预览）")}";
                PreviewTextBlock.FontSize = 16;
                PreviewTextBlock.Foreground = Brushes.White;
                PositionPreviewBlock();
                return;
            }

            var lines = model.LinesText
                .Split('\n')
                .Select(l => l.TrimEnd('\r'))
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .ToList();
            PreviewTextBlock.Text = lines.Count > 0 ? string.Join(Environment.NewLine, lines) : "（文字行为空）";
            var fontSize = model.FontSize > 0 ? model.FontSize : 96;
            // 预览区缩放：预览区最高约 56 DIP；行数多时等比下调避免超高
            PreviewTextBlock.FontSize = Math.Clamp(fontSize * (lines.Count > 0 ? 0.6 : 1.0), 12, 56);

            // 文字色恒白（BannerAssembler 定稿），描边色仅作提示（真实描边由 Overlay 渲染）
            PreviewTextBlock.Foreground = Brushes.White;
            PositionPreviewBlock();
        }
        finally
        {
            _loadGuard = false;
        }
    }

    /// <summary>从编辑模型列表重建 AppConfig（规则节重建，其余节沿用 _baseConfig）。</summary>
    private AppConfig? BuildConfig()
    {
        var rules = new List<RuleConfig>();
        foreach (var model in _rules)
        {
            var rule = model.ToConfig();
            if (rule is null)
            {
                continue;
            }

            rules.Add(rule);
        }

        var config = new AppConfig
        {
            SchemaVersion = _baseConfig.SchemaVersion,
            Language = _baseConfig.Language,
            Global = _globalCfg,
            Welcome = _welcomeCfg,
            Manual = _manualCfg,
            Rules = rules,
            Dnd = _dndCfg,
            Sound = _soundCfg,
            Autostart = _autostartValue,
            FirstRun = _baseConfig.FirstRun,
            UiState = _baseConfig.UiState,
        };

        var issues = ConfigValidator.Validate(config);
        if (issues.Count > 0)
        {
            StatusText.Text = $"保存被拦截（{ErrorCodes.ConfigValidationFailed}）：{issues[0].Message}";
            return null;
        }

        return config;
    }

    /// <summary>宽松数字解析（未填/非法 → null，保存时由校验拦截）。</summary>
    private static double? ParseDouble(string text)
        => double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : null;

    /// <summary>位置下拉取值解析（AC-66）：选中 custom 且模型已有 custom 坐标时保留坐标，否则用下拉值。</summary>
    private static string ResolvePositionSelection(string? selection, string current)
    {
        var value = string.IsNullOrWhiteSpace(selection) ? "center" : selection;
        if (string.Equals(value, "custom", StringComparison.OrdinalIgnoreCase)
            && current.StartsWith("custom", StringComparison.OrdinalIgnoreCase))
        {
            return current; // 保留拖拽设置的坐标
        }

        return value;
    }

    /// <summary>位置下拉项与模型位置是否匹配（"custom {x,y}" 视为下拉项 "custom"）。</summary>
    private static bool MatchesPositionItem(string item, string position)
    {
        if (string.Equals(item, "custom", StringComparison.OrdinalIgnoreCase))
        {
            return position.StartsWith("custom", StringComparison.OrdinalIgnoreCase);
        }

        return string.Equals(item, position, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>解析 "custom {x,y}" 归一化坐标（AC-66）。</summary>
    private static (double? X, double? Y) ParseCustomPosition(string? position)
    {
        if (string.IsNullOrWhiteSpace(position) || !position.StartsWith("custom", StringComparison.OrdinalIgnoreCase))
        {
            return (null, null);
        }

        var match = System.Text.RegularExpressions.Regex.Match(position, @"([0-9]*\.?[0-9]+)\s*,\s*([0-9]*\.?[0-9]+)");
        if (match.Success
            && double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var x)
            && double.TryParse(match.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var y))
        {
            return (Math.Clamp(x, 0, 1), Math.Clamp(y, 0, 1));
        }

        return (null, null);
    }

    /// <summary>按模型 position 定位预览文字块（AC-66：custom 归一化坐标 / top-center / center）。</summary>
    private void PositionPreviewBlock()
    {
        var w = PreviewCanvas.ActualWidth;
        var h = PreviewCanvas.ActualHeight;
        if (w <= 1 || h <= 1)
        {
            return;
        }

        PreviewTextBlock.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var bw = PreviewTextBlock.DesiredSize.Width;
        var bh = PreviewTextBlock.DesiredSize.Height;
        var position = _editing?.Position ?? "center";
        var (x, y) = ParseCustomPosition(position);

        double left;
        double top;
        if (x is not null && y is not null)
        {
            left = Math.Clamp(x.Value * w - bw / 2, 0, Math.Max(0, w - bw));
            top = Math.Clamp(y.Value * h - bh / 2, 0, Math.Max(0, h - bh));
        }
        else if (string.Equals(position, "top-center", StringComparison.OrdinalIgnoreCase))
        {
            left = Math.Max(0, (w - bw) / 2);
            top = Math.Max(0, h * 0.08);
        }
        else
        {
            left = Math.Max(0, (w - bw) / 2);
            top = Math.Max(0, (h - bh) / 2);
        }

        Canvas.SetLeft(PreviewTextBlock, left);
        Canvas.SetTop(PreviewTextBlock, top);
    }

    private void OnPreviewSizeChanged(object sender, SizeChangedEventArgs e) => PositionPreviewBlock();

    private void OnPreviewMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_editing is null)
        {
            return;
        }

        var pos = e.GetPosition(PreviewCanvas);
        _previewDragOffset = new Point(pos.X - Canvas.GetLeft(PreviewTextBlock), pos.Y - Canvas.GetTop(PreviewTextBlock));
        _previewDragging = true;
        _ = PreviewCanvas.CaptureMouse();
    }

    private void OnPreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_previewDragging || _editing is null)
        {
            return;
        }

        var pos = e.GetPosition(PreviewCanvas);
        var w = PreviewCanvas.ActualWidth;
        var h = PreviewCanvas.ActualHeight;
        var bw = PreviewTextBlock.DesiredSize.Width;
        var bh = PreviewTextBlock.DesiredSize.Height;
        Canvas.SetLeft(PreviewTextBlock, Math.Clamp(pos.X - _previewDragOffset.X, 0, Math.Max(0, w - bw)));
        Canvas.SetTop(PreviewTextBlock, Math.Clamp(pos.Y - _previewDragOffset.Y, 0, Math.Max(0, h - bh)));
    }

    private void OnPreviewMouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (!_previewDragging || _editing is null)
        {
            _previewDragging = false;
            return;
        }

        _previewDragging = false;
        PreviewCanvas.ReleaseMouseCapture();

        var w = PreviewCanvas.ActualWidth;
        var h = PreviewCanvas.ActualHeight;
        if (w <= 1 || h <= 1)
        {
            return;
        }

        var bw = PreviewTextBlock.DesiredSize.Width;
        var bh = PreviewTextBlock.DesiredSize.Height;
        var cx = Math.Clamp((Canvas.GetLeft(PreviewTextBlock) + bw / 2) / w, 0, 1);
        var cy = Math.Clamp((Canvas.GetTop(PreviewTextBlock) + bh / 2) / h, 0, 1);
        _editing.Position = $"custom {{{cx.ToString("0.###", CultureInfo.InvariantCulture)},{cy.ToString("0.###", CultureInfo.InvariantCulture)}}}";

        _loadGuard = true;
        try
        {
            PositionCombo.SelectedItem = "custom";
        }
        finally
        {
            _loadGuard = false;
        }

        MarkDirty();
        StatusText.Text = $"已设置自定义位置（{cx:0.00}, {cy:0.00}）";
    }

    /// <summary>冲突/重复检测（F-29/AC-58）：列出完全重复（error）与匹配重叠（warning）。</summary>
    private void OnConflictCheckClicked(object sender, RoutedEventArgs e)
    {
        CommitEditingRule();
        var rules = _rules.Select(m => m.ToConfig()).Where(c => c is not null).Cast<RuleConfig>().ToList();
        var conflicts = ConfigValidator.CheckConflicts(rules);
        if (conflicts.Count == 0)
        {
            StatusText.Text = "冲突检测：未发现重复或重叠规则";
            MessageBox.Show(this, "未发现重复或重叠规则。", "冲突检测", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var text = string.Join(Environment.NewLine, conflicts.Select(c => "· " + c.Message));
        StatusText.Text = $"冲突检测：发现 {conflicts.Count} 项";
        MessageBox.Show(this, text, "冲突检测", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    /// <summary>分步向导新建规则（F-28/AC-57）：完成后加入列表（结构变更可撤销）。</summary>
    private void OnNewRuleWizardClicked(object sender, RoutedEventArgs e)
    {
        var wizard = new NewRuleWizard { Owner = this };
        if (wizard.ShowDialog() != true || wizard.Result is null)
        {
            return;
        }

        PushRuleUndo();
        var model = RuleEditModel.FromConfig(wizard.Result);
        model.RuleId = UniqueRuleId();
        _rules.Add(model);
        ApplySearchFilter();
        RulesListBox.SelectedItem = model;
        StatusText.Text = $"向导已新建规则 {model.RuleId}（编辑后点保存生效）";
    }

    /// <summary>按取值选中描边方式下拉项（Tag 匹配，大小写不敏感；无匹配回退首项）。</summary>
    private static void SelectOutlineMode(ComboBox combo, string? value)
    {
        foreach (var item in combo.Items)
        {
            if (item is ComboBoxItem cb && string.Equals(cb.Tag as string, value ?? string.Empty, StringComparison.OrdinalIgnoreCase))
            {
                combo.SelectedItem = item;
                return;
            }
        }

        combo.SelectedIndex = 0;
    }

    /// <summary>读取描边方式下拉当前取值（Tag；缺省空串 = 跟随全局）。</summary>
    private static string OutlineModeTag(ComboBox combo)
        => (combo.SelectedItem as ComboBoxItem)?.Tag as string ?? string.Empty;

    // ============ 页签事件（M4-D：欢迎大字 / 手动大字 / 勿扰·提示音·自启） ============

    /// <summary>页签切换：离开"触发规则"时先回写编辑中规则，避免半编辑状态丢失。</summary>
    private void OnMainTabChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loadGuard)
        {
            return;
        }

        CommitEditingRule();
    }

    private void OnWelcomeCheckedChanged(object sender, RoutedEventArgs e)
    {
        if (_loadGuard)
        {
            return;
        }

        _welcomeCfg = RebuildWelcome();
        MarkDirty();
    }

    private void OnWelcomeTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_loadGuard)
        {
            return;
        }

        _welcomeCfg = RebuildWelcome();
        MarkDirty();
    }

    private void OnManualCheckedChanged(object sender, RoutedEventArgs e)
    {
        if (_loadGuard)
        {
            return;
        }

        _manualCfg = RebuildManual();
        MarkDirty();
    }

    private void OnManualTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_loadGuard)
        {
            return;
        }

        _manualCfg = RebuildManual();
        MarkDirty();
    }

    private void OnManualSelChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loadGuard)
        {
            return;
        }

        _manualCfg = RebuildManual();
        MarkDirty();
    }

    private void OnGlobalSelChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loadGuard)
        {
            return;
        }

        _globalCfg = RebuildGlobal();
        MarkDirty();
    }

    private void OnDndCheckedChanged(object sender, RoutedEventArgs e)
    {
        if (_loadGuard)
        {
            return;
        }

        _dndCfg = new DndConfig
        {
            Paused = DndPausedCheck.IsChecked == true,
            ScheduleEnabled = DndScheduleEnabledCheck.IsChecked == true,
            PresentationAutoSilence = DndPresentationSilenceCheck.IsChecked == true,
            Schedule = _dndCfg.Schedule,
        };
        MarkDirty();
    }

    private void OnSoundCheckedChanged(object sender, RoutedEventArgs e)
    {
        if (_loadGuard)
        {
            return;
        }

        SyncSound();
        MarkDirty();
    }

    private void OnSoundSelChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loadGuard)
        {
            return;
        }

        SyncSound();
        MarkDirty();
    }

    private void OnSoundTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_loadGuard)
        {
            return;
        }

        SyncSound();
        MarkDirty();
    }

    private void OnAutostartCheckedChanged(object sender, RoutedEventArgs e)
    {
        if (_loadGuard)
        {
            return;
        }

        _autostartValue = AutostartCheck.IsChecked == true;
        MarkDirty();
    }

    private void SyncSound()
    {
        var source = (SoundSourceCombo.SelectedItem as ComboBoxItem)?.Tag as string ?? "system";
        _soundCfg = new SoundConfig
        {
            Enabled = SoundEnabledCheck.IsChecked == true,
            Source = source,
            CustomPath = SoundCustomPathBox.Text.Trim(),
            Volume = ParseDouble(SoundVolumeBox.Text) is { } v && v is >= 0 and <= 100 ? (int)v : 80,
        };
    }

    /// <summary>由"欢迎大字"页控件重建 WelcomeConfig（保存时生效；未填文字行回退当前）。</summary>
    private WelcomeConfig RebuildWelcome()
    {
        var baseWelcome = _welcomeCfg.DisplayLines.Count > 0 ? _welcomeCfg : ConfigLoader.CreateDefault().Welcome;
        var lines = SplitLines(WelcomeLinesBox.Text);
        return new WelcomeConfig
        {
            Enabled = WelcomeEnabledCheck.IsChecked == true,
            DisplayLines = lines.Count > 0
                ? lines
                : baseWelcome.DisplayLines,
            DelaySeconds = ParseDouble(WelcomeDelaySecondsBox.Text) ?? baseWelcome.DelaySeconds,
            HoldSeconds = ParseDouble(WelcomeHoldSecondsBox.Text) ?? baseWelcome.HoldSeconds,
            WrapStrategy = baseWelcome.WrapStrategy,
        };
    }

    /// <summary>由"手动大字"页控件重建 ManualConfig（未填文字行回退当前）。</summary>
    private ManualConfig RebuildManual()
    {
        var baseManual = _manualCfg.DisplayLines.Count > 0 ? _manualCfg : ConfigLoader.CreateDefault().Manual;
        var lines = SplitLines(ManualLinesBox.Text);
        var fontSize = ParseDouble(ManualFontSizeBox.Text) ?? baseManual.FontSize;
        return new ManualConfig
        {
            Enabled = ManualEnabledCheck.IsChecked == true,
            DisplayLines = lines.Count > 0
                ? lines.Select(l => l.FontSize > 0 ? l : new DisplayLine { Text = l.Text, FontSize = fontSize }).ToList()
                : baseManual.DisplayLines,
            DelaySeconds = baseManual.DelaySeconds,
            HoldSeconds = ParseDouble(ManualHoldSecondsBox.Text) ?? baseManual.HoldSeconds,
            WrapStrategy = baseManual.WrapStrategy,
            Position = baseManual.Position,
            FontSize = fontSize,
            Align = baseManual.Align,
            OutlineColor = baseManual.OutlineColor,
            OutlineWidth = baseManual.OutlineWidth,
            OutlineMode = OutlineModeTag(ManualOutlineModeCombo),
            TargetScreen = baseManual.TargetScreen,
            Shortcut = string.IsNullOrWhiteSpace(ManualShortcutBox.Text) ? null : ManualShortcutBox.Text.Trim(),
        };
    }

    /// <summary>由"勿扰/提示音/自启"页的全局描边下拉重建 GlobalConfig（其余全局字段沿用）。</summary>
    private GlobalConfig RebuildGlobal()
    {
        var g = _globalCfg;
        return new GlobalConfig
        {
            DefaultFontSize = g.DefaultFontSize,
            DefaultPosition = g.DefaultPosition,
            DefaultOutlineColor = g.DefaultOutlineColor,
            DefaultOutlineWidth = g.DefaultOutlineWidth,
            OutlineMode = OutlineModeTag(GlobalOutlineModeCombo) is { Length: > 0 } mode ? mode : "shadow",
            TargetScreen = g.TargetScreen,
            FontFamily = g.FontFamily,
            TimeFormat = g.TimeFormat,
            MonitorPollIntervalMs = g.MonitorPollIntervalMs,
            DedupeWindowSeconds = g.DedupeWindowSeconds,
            MinTriggerIntervalSeconds = g.MinTriggerIntervalSeconds,
            LogLevel = g.LogLevel,
            LogMaxSizeMB = g.LogMaxSizeMB,
            LogRetainCount = g.LogRetainCount,
            DpiAwareness = g.DpiAwareness,
        };
    }

    /// <summary>按行拆分为 DisplayLine（行字号继承块级缺省 0 → 由合并器回退）。</summary>
    private static List<DisplayLine> SplitLines(string text)
        => text.Split('\n')
            .Select(l => l.TrimEnd('\r'))
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Select(l => new DisplayLine { Text = l.Trim(), FontSize = 0 })
            .ToList();

    /// <summary>标记有未保存修改（AC-38）。</summary>
    private void MarkDirty()
    {
        if (_dirty)
        {
            return;
        }

        _dirty = true;
        StatusText.Text = "有未保存修改（AC-38）…";
    }
}

/// <summary>
/// 规则编辑模型（UI 可变层 → 保存时转换 RuleConfig；ListBox 绑定枚举开关 + 匹配摘要）。
/// </summary>
public sealed class RuleEditModel : INotifyPropertyChanged
{
    private string _matchValue = string.Empty;
    private bool _enabled = true;

    /// <summary>规则标识。</summary>
    public string RuleId { get; set; } = string.Empty;

    /// <summary>匹配类型（进程名 / 窗口标题）。</summary>
    public MatchType MatchType { get; set; } = MatchType.Process;

    /// <summary>匹配模式（exact / contains / wildcard）。</summary>
    public MatchMode MatchMode { get; set; } = MatchMode.Exact;

    /// <summary>匹配值（进程名 / 窗口标题文本 / 通配模式）。</summary>
    public string MatchValue
    {
        get => _matchValue;
        set
        {
            _matchValue = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Summary));
        }
    }

    /// <summary>规则是否启用。</summary>
    public bool Enabled
    {
        get => _enabled;
        set
        {
            _enabled = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Summary)); // 列表摘要前缀 ☑/☐ 同步
        }
    }

    /// <summary>匹配大小写敏感。</summary>
    public bool MatchCaseSensitive { get; set; }

    /// <summary>是否含全屏应用命中。</summary>
    public bool IncludeFullscreen { get; set; } = true;

    /// <summary>目标屏（空 = 主屏）。</summary>
    public string TargetScreen { get; set; } = string.Empty;

    /// <summary>文字行文本（多行，每行一条；保存时拆分 DisplayLines）。</summary>
    public string LinesText { get; set; } = string.Empty;

    /// <summary>字号（&gt;0 生效）。</summary>
    public double FontSize { get; set; }

    /// <summary>浮现延时（秒）。</summary>
    public double DelaySeconds { get; set; }

    /// <summary>保持时长（秒）。</summary>
    public double HoldSeconds { get; set; }

    /// <summary>显示位置（center / top-center / custom）。</summary>
    public string Position { get; set; } = "center";

    /// <summary>描边色（#RRGGBB / #AARRGGBB，空 = 默认）。</summary>
    public string OutlineColor { get; set; } = string.Empty;

    /// <summary>描边宽度（&lt;0 = 默认）。</summary>
    public double OutlineWidth { get; set; } = -1;

    /// <summary>描边方式（shadow/stroke；空串 = 跟随全局）。</summary>
    public string OutlineMode { get; set; } = string.Empty;

    /// <summary>列表摘要（匹配摘要 + 禁用标记，列表只读展示）。</summary>
    public string Summary => $"{(Enabled ? "☑" : "☐")} [{MatchType}/{MatchMode}] {MatchValue}";

    /// <summary>从共享 RuleConfig 构建编辑模型。</summary>
    public static RuleEditModel FromConfig(RuleConfig rule) => new()
    {
        RuleId = rule.RuleId,
        MatchType = rule.MatchType,
        MatchMode = rule.MatchMode,
        MatchValue = rule.MatchValue,
        Enabled = rule.Enabled,
        MatchCaseSensitive = rule.MatchCaseSensitive,
        IncludeFullscreen = rule.IncludeFullscreen,
        TargetScreen = rule.TargetScreen,
        LinesText = string.Join(Environment.NewLine, rule.DisplayLines.Select(l => l.Text)),
        FontSize = rule.DisplayLines.FirstOrDefault(l => l.FontSize > 0)?.FontSize ?? 0,
        DelaySeconds = rule.DelaySeconds,
        HoldSeconds = rule.HoldSeconds,
        Position = rule.Position,
        OutlineColor = rule.OutlineColor,
        OutlineWidth = rule.OutlineWidth,
        OutlineMode = rule.OutlineMode,
    };

    /// <summary>转换为共享 RuleConfig（文字行拆分；空规则返回 null，校验错误由 ConfigValidator 承担）。</summary>
    public RuleConfig? ToConfig()
    {
        if (string.IsNullOrWhiteSpace(RuleId))
        {
            return null;
        }

        var lines = LinesText
            .Split('\n')
            .Select(l => l.TrimEnd('\r'))
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Select(l => new DisplayLine { Text = l.Trim(), FontSize = FontSize > 0 ? FontSize : 0 })
            .ToList();
        if (lines.Count == 0)
        {
            return null;
        }

        return new RuleConfig
        {
            RuleId = RuleId,
            MatchType = MatchType,
            MatchValue = MatchValue,
            MatchMode = MatchMode,
            Enabled = Enabled,
            MatchCaseSensitive = MatchCaseSensitive,
            IncludeFullscreen = IncludeFullscreen,
            TargetScreen = TargetScreen,
            DisplayLines = lines,
            DelaySeconds = DelaySeconds,
            HoldSeconds = HoldSeconds,
            Position = Position,
            OutlineColor = OutlineColor,
            OutlineWidth = OutlineWidth,
            OutlineMode = OutlineMode,
        };
    }

    /// <summary>通知列表刷新（编辑区控件写回模型后调用）。</summary>
    public void Touch()
    {
        OnPropertyChanged(nameof(Summary));
    }

    /// <summary>属性变更通知（列表/编辑区绑定刷新）。</summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}