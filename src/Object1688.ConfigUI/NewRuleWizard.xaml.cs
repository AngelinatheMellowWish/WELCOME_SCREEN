using System.Globalization;
using System.Windows;
using Object1688.Shared.Config;
using Object1688.Shared.Text;
using MatchType = Object1688.Shared.Config.MatchType;

namespace Object1688.ConfigUI;

/// <summary>
/// 新建规则向导（架构 §5.6 F-28/AC-57）。
/// 三步视图（匹配 → 显示 → 确认）与直接表单共享同一 RuleConfig 构造语义；分步校验，完成后返回 <see cref="Result"/>。
/// </summary>
public partial class NewRuleWizard : Window
{
    private int _step = 1;

    /// <summary>向导完成后的规则（取消时为 null）。</summary>
    public RuleConfig? Result { get; private set; }

    /// <summary>初始化向导。</summary>
    public NewRuleWizard()
    {
        InitializeComponent();
        WizMatchTypeCombo.SelectedIndex = 0;
        WizMatchModeCombo.SelectedIndex = 0;
        UpdateStep();
    }

    private MatchType SelectedMatchType
        => WizMatchTypeCombo.SelectedIndex == 1 ? MatchType.WindowTitle : MatchType.Process;

    private MatchMode SelectedMatchMode
        => WizMatchModeCombo.SelectedIndex switch
        {
            1 => MatchMode.Contains,
            2 => MatchMode.Wildcard,
            _ => MatchMode.Exact,
        };

    private void OnBackClicked(object sender, RoutedEventArgs e)
    {
        if (_step > 1)
        {
            _step--;
            UpdateStep();
        }
    }

    private void OnNextClicked(object sender, RoutedEventArgs e)
    {
        if (!ValidateStep(_step))
        {
            return;
        }

        if (_step < 3)
        {
            _step++;
            UpdateStep();
        }
    }

    private void OnFinishClicked(object sender, RoutedEventArgs e)
    {
        if (!ValidateStep(1) || !ValidateStep(2))
        {
            return;
        }

        Result = BuildResult();
        DialogResult = true;
    }

    private void OnCancelClicked(object sender, RoutedEventArgs e) => DialogResult = false;

    private bool ValidateStep(int step)
    {
        if (step == 1 && string.IsNullOrWhiteSpace(WizMatchValueBox.Text))
        {
            MessageBox.Show(this, "请填写匹配值。", "新建规则向导", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        if (step == 2 && string.IsNullOrWhiteSpace(WizTextLinesBox.Text))
        {
            MessageBox.Show(this, "请填写显示文字。", "新建规则向导", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        return true;
    }

    private void UpdateStep()
    {
        Step1Panel.Visibility = _step == 1 ? Visibility.Visible : Visibility.Collapsed;
        Step2Panel.Visibility = _step == 2 ? Visibility.Visible : Visibility.Collapsed;
        Step3Panel.Visibility = _step == 3 ? Visibility.Visible : Visibility.Collapsed;
        StepHeader.Text = $"第 {_step}/3 步：{(_step == 1 ? "匹配" : _step == 2 ? "显示" : "确认")}";
        WizBackButton.IsEnabled = _step > 1;
        WizNextButton.Visibility = _step < 3 ? Visibility.Visible : Visibility.Collapsed;
        WizFinishButton.Visibility = _step == 3 ? Visibility.Visible : Visibility.Collapsed;

        if (_step == 3)
        {
            WizSummaryText.Text =
                $"匹配类型：{SelectedMatchType}\n匹配模式：{SelectedMatchMode}\n匹配值：{WizMatchValueBox.Text.Trim()}\n" +
                $"文字：{WizTextLinesBox.Text.Replace(Environment.NewLine, " / ")}\n" +
                $"字号：{WizFontSizeBox.Text}  延时：{WizDelayBox.Text}s  保持：{WizHoldBox.Text}s";
        }
    }

    private RuleConfig BuildResult()
    {
        var lines = WizTextLinesBox.Text
            .Split('\n')
            .Select(l => l.TrimEnd('\r'))
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Select(l => new DisplayLine { Text = l.Trim(), FontSize = ParseDouble(WizFontSizeBox.Text) ?? 96 })
            .ToList();

        return new RuleConfig
        {
            RuleId = "r-new",
            MatchType = SelectedMatchType,
            MatchMode = SelectedMatchMode,
            MatchValue = WizMatchValueBox.Text.Trim(),
            Enabled = true,
            DisplayLines = lines,
            DelaySeconds = ParseDouble(WizDelayBox.Text) ?? 2,
            HoldSeconds = ParseDouble(WizHoldBox.Text) ?? 4,
            Position = "center",
        };
    }

    private static double? ParseDouble(string text)
        => double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : null;
}
