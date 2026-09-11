using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Object1688.Overlay.Rendering;

/// <summary>
/// 大字动画控制器（架构 §3.2：浮现(300~500ms 淡入+缩放) → 保持(N 秒) → 淡出(500ms~1s)）。
/// 单块大字一次性播放：完成/提前结束后内部释放，不可复用。
/// 动画不受 Windows"关闭动画"设置影响（NFR-02 扩展，WPF 动画始终播放）。
/// </summary>
public sealed class BannerAnimation
{
    private readonly FrameworkElement _target;
    private readonly int _fadeInMs;
    private readonly int _fadeOutMs;
    private readonly double _holdSeconds;
    private readonly TaskCompletionSource _completedTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly ScaleTransform _scale;

    private DispatcherTimer? _holdTimer;
    private bool _fadeOutStarted;
    private bool _finished;

    /// <summary>动画创建，并绑定目标元素（其 RenderTransform 被接管用于缩放效果）。</summary>
    /// <param name="target">要动画化的元素（整块大字视觉根）。</param>
    /// <param name="fadeInMs">浮现时长（毫秒，300~500 合理区间）。</param>
    /// <param name="holdSeconds">保持时长（秒）。</param>
    /// <param name="fadeOutMs">淡出时长（毫秒，500~1000 合理区间）。</param>
    public BannerAnimation(FrameworkElement target, int fadeInMs, double holdSeconds, int fadeOutMs)
    {
        _target = target ?? throw new ArgumentNullException(nameof(target));
        _fadeInMs = Math.Clamp(fadeInMs, 300, 500);
        _holdSeconds = Math.Max(0, holdSeconds);
        _fadeOutMs = Math.Clamp(fadeOutMs, 500, 1000);

        _scale = new ScaleTransform(0.95, 0.95);
        target.RenderTransform = _scale;
        target.RenderTransformOrigin = new Point(0.5, 0.5);
    }

    /// <summary>完成信号：浮现→保持→淡出全部结束（或提前结束后淡出结束）。</summary>
    public Task CompletedTask => _completedTcs.Task;

    /// <summary>
    /// 启动播放：浮现 → 保持 → 淡出。
    /// </summary>
    public void Play()
    {
        if (_finished)
        {
            return;
        }

        // 浮现：透明 + 0.95 缩放 → 不透明 + 1.0 缩放
        _target.Opacity = 0;
        BeginFadeIn();
    }

    /// <summary>
    /// 提前结束（F-07）：浮现/保持阶段立即切入淡出，淡出完成后出队信号仍触发。
    /// 幂等：可在任意阶段多次调用。
    /// </summary>
    public void EndEarly()
    {
        if (_finished || _fadeOutStarted)
        {
            return;
        }

        _holdTimer?.Stop();
        _fadeOutStarted = true;
        BeginFadeOut();
    }

    private void BeginFadeIn()
    {
        var opacity = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(_fadeInMs)) { FillBehavior = FillBehavior.HoldEnd };
        var scaleX = new DoubleAnimation(0.95, 1.0, TimeSpan.FromMilliseconds(_fadeInMs)) { FillBehavior = FillBehavior.HoldEnd };
        var scaleY = new DoubleAnimation(0.95, 1.0, TimeSpan.FromMilliseconds(_fadeInMs)) { FillBehavior = FillBehavior.HoldEnd };

        opacity.Completed += (_, _) => StartHold();
        _target.BeginAnimation(UIElement.OpacityProperty, opacity);
        _scale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleX);
        _scale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleY);
    }

    private void StartHold()
    {
        if (_finished || _fadeOutStarted)
        {
            return;
        }

        if (_holdSeconds <= 0)
        {
            EndEarly();
            return;
        }

        _holdTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(_holdSeconds) };
        _holdTimer.Tick += (_, _) => EndEarly();
        _holdTimer.Start();
    }

    private void BeginFadeOut()
    {
        var fadeOut = new DoubleAnimation(_target.Opacity, 0, TimeSpan.FromMilliseconds(_fadeOutMs)) { FillBehavior = FillBehavior.Stop };
        fadeOut.Completed += (_, _) =>
        {
            if (_finished)
            {
                return;
            }

            _finished = true;
            _target.Opacity = 0;
            _target.BeginAnimation(UIElement.OpacityProperty, null);
            _completedTcs.TrySetResult();
        };

        _target.BeginAnimation(UIElement.OpacityProperty, fadeOut);
    }
}