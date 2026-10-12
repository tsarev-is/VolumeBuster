using System.Globalization;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Rendering;

namespace VolumeBuster.Views;

/// <summary>
/// A round knob: a 270° arc filled from <see cref="Origin"/> to the value, with a handle at the value and an
/// optional scale around it, like the LED ring of an encoder on a mixing desk.
/// </summary>
/// <remarks>
/// Pressing anywhere on the knob grabs it: the handle grows, and moving the finger right or left raises or lowers
/// the value by the distance moved, so touching it never makes the value jump. The handle glides between steps.
/// </remarks>
public sealed class Dial : Control, ICustomHitTest
{
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<Dial, double>(nameof(Value), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<double> MinimumProperty =
        AvaloniaProperty.Register<Dial, double>(nameof(Minimum));

    public static readonly StyledProperty<double> MaximumProperty =
        AvaloniaProperty.Register<Dial, double>(nameof(Maximum), 100);

    public static readonly StyledProperty<double> StepProperty =
        AvaloniaProperty.Register<Dial, double>(nameof(Step), 1);

    public static readonly StyledProperty<double> OriginProperty =
        AvaloniaProperty.Register<Dial, double>(nameof(Origin), double.NaN);

    public static readonly StyledProperty<double> TickFrequencyProperty =
        AvaloniaProperty.Register<Dial, double>(nameof(TickFrequency));

    public static readonly StyledProperty<double> LabelFrequencyProperty =
        AvaloniaProperty.Register<Dial, double>(nameof(LabelFrequency));

    public static readonly StyledProperty<string> LabelFormatProperty =
        AvaloniaProperty.Register<Dial, string>(nameof(LabelFormat), "0.#");

    public static readonly StyledProperty<bool> IsActiveProperty =
        AvaloniaProperty.Register<Dial, bool>(nameof(IsActive), true);

    public static readonly StyledProperty<IBrush?> ActiveBrushProperty =
        AvaloniaProperty.Register<Dial, IBrush?>(nameof(ActiveBrush));

    public static readonly StyledProperty<IBrush?> InactiveBrushProperty =
        AvaloniaProperty.Register<Dial, IBrush?>(nameof(InactiveBrush));

    public static readonly StyledProperty<IBrush?> TrackBrushProperty =
        AvaloniaProperty.Register<Dial, IBrush?>(nameof(TrackBrush));

    public static readonly StyledProperty<IBrush?> HandleFillProperty =
        AvaloniaProperty.Register<Dial, IBrush?>(nameof(HandleFill));

    public static readonly StyledProperty<IBrush?> TextBrushProperty =
        AvaloniaProperty.Register<Dial, IBrush?>(nameof(TextBrush));

    public static readonly DirectProperty<Dial, bool> IsDraggingProperty =
        AvaloniaProperty.RegisterDirect<Dial, bool>(nameof(IsDragging), dial => dial.IsDragging);

    /// <summary>
    /// Where the handle is drawn: follows <see cref="Value"/> through a short transition.
    /// </summary>
    private static readonly StyledProperty<double> ShownValueProperty =
        AvaloniaProperty.Register<Dial, double>("ShownValue");

    /// <summary>
    /// 0 at rest, 1 while held: grows the handle and its halo.
    /// </summary>
    private static readonly StyledProperty<double> GripProperty =
        AvaloniaProperty.Register<Dial, double>("Grip");

    /// <summary>
    /// Where the arc starts, in degrees clockwise from three o'clock: bottom left.
    /// </summary>
    private const double StartAngle = 135;

    private const double SweepAngle = 270;
    private const double Thickness = 10;
    private const double HandleRadius = 9;
    private const double HeldHandleRadius = 13;
    private const double HaloRadius = 21;
    private const double HaloOpacity = 0.22;

    /// <summary>
    /// The finger travel for the whole range, in device-independent pixels: about one and a half knob widths.
    /// </summary>
    private const double DragDistance = 200;

    /// <summary>
    /// How far past the current value, in steps, the finger must go to reach the next one. Above a half step,
    /// so a finger resting on the border between two values does not flicker between them.
    /// </summary>
    private const double SnapThreshold = 0.6;

    private const double TickGap = 2;
    private const double MinorTickLength = 3;
    private const double MajorTickLength = 5;
    private const double LabelGap = 3;
    private const double LabelFontSize = 10;

    /// <summary>
    /// Room for a label beyond the ticks; wider labels may reach into the space around the knob.
    /// </summary>
    private const double LabelBand = 14;

    private static readonly Typeface LabelTypeface = new(FontFamily.Default, FontStyle.Normal, FontWeight.Medium);

    private bool _isDragging;
    private double _lastX;
    private double _unsnapped;

    static Dial()
    {
        AffectsRender<Dial>(
            ShownValueProperty, GripProperty, MinimumProperty, MaximumProperty, OriginProperty, TickFrequencyProperty,
            LabelFrequencyProperty, LabelFormatProperty, IsActiveProperty, ActiveBrushProperty, InactiveBrushProperty,
            TrackBrushProperty, HandleFillProperty, TextBrushProperty);
        FocusableProperty.OverrideDefaultValue<Dial>(true);
    }

    public Dial()
    {
        Transitions = new Transitions
        {
            new DoubleTransition { Property = ShownValueProperty, Duration = TimeSpan.FromMilliseconds(140), Easing = new CubicEaseOut() },
            // A slight overshoot makes the handle pop out under the finger and settle back when let go.
            new DoubleTransition { Property = GripProperty, Duration = TimeSpan.FromMilliseconds(260), Easing = new BackEaseOut() },
        };
    }

    public double Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public double Minimum
    {
        get => GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    public double Maximum
    {
        get => GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    /// <summary>
    /// Values snap to multiples of this.
    /// </summary>
    public double Step
    {
        get => GetValue(StepProperty);
        set => SetValue(StepProperty, value);
    }

    /// <summary>
    /// The value the fill starts from: <see cref="Minimum"/> when not set, 0 for a dial that cuts and raises.
    /// </summary>
    public double Origin
    {
        get => GetValue(OriginProperty);
        set => SetValue(OriginProperty, value);
    }

    /// <summary>
    /// Distance between the small marks of the scale; 0 for no scale.
    /// </summary>
    public double TickFrequency
    {
        get => GetValue(TickFrequencyProperty);
        set => SetValue(TickFrequencyProperty, value);
    }

    /// <summary>
    /// Distance between the long, labelled marks of the scale, a multiple of <see cref="TickFrequency"/>;
    /// 0 for no labels.
    /// </summary>
    public double LabelFrequency
    {
        get => GetValue(LabelFrequencyProperty);
        set => SetValue(LabelFrequencyProperty, value);
    }

    /// <summary>
    /// Number format of the labels, e.g. "+0;−0;0" for a scale around zero.
    /// </summary>
    public string LabelFormat
    {
        get => GetValue(LabelFormatProperty);
        set => SetValue(LabelFormatProperty, value);
    }

    /// <summary>
    /// False fills the arc with <see cref="InactiveBrush"/>: switched off but still adjustable.
    /// </summary>
    public bool IsActive
    {
        get => GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    public IBrush? ActiveBrush
    {
        get => GetValue(ActiveBrushProperty);
        set => SetValue(ActiveBrushProperty, value);
    }

    public IBrush? InactiveBrush
    {
        get => GetValue(InactiveBrushProperty);
        set => SetValue(InactiveBrushProperty, value);
    }

    public IBrush? TrackBrush
    {
        get => GetValue(TrackBrushProperty);
        set => SetValue(TrackBrushProperty, value);
    }

    public IBrush? HandleFill
    {
        get => GetValue(HandleFillProperty);
        set => SetValue(HandleFillProperty, value);
    }

    /// <summary>
    /// The labels and the unlit marks of the scale.
    /// </summary>
    public IBrush? TextBrush
    {
        get => GetValue(TextBrushProperty);
        set => SetValue(TextBrushProperty, value);
    }

    /// <summary>
    /// The knob is held by a finger or the mouse; also the <c>:dragging</c> pseudo-class.
    /// </summary>
    public bool IsDragging
    {
        get => _isDragging;
        private set
        {
            if (SetAndRaise(IsDraggingProperty, ref _isDragging, value))
            {
                PseudoClasses.Set(":dragging", value);
                SetValue(GripProperty, value ? 1.0 : 0.0);
            }
        }
    }

    /// <summary>
    /// The whole circle of the knob and its scale takes touches, not only the arc and the handle that are drawn.
    /// </summary>
    public bool HitTest(Point point)
    {
        var offset = point - Centre;
        var reach = Math.Min(Bounds.Width, Bounds.Height) / 2;
        return offset.X * offset.X + offset.Y * offset.Y <= reach * reach;
    }

    public override void Render(DrawingContext context)
    {
        var radius = Radius;
        if (radius <= 0 || Maximum <= Minimum)
        {
            return;
        }

        var centre = Centre;
        var fillBrush = (IsActive ? ActiveBrush : InactiveBrush) ?? Brushes.Gray;
        var trackBrush = TrackBrush ?? Brushes.LightGray;
        var originValue = double.IsNaN(Origin) ? Minimum : Origin;
        var shownValue = Math.Clamp(GetValue(ShownValueProperty), Minimum, Maximum);

        if (HasScale)
        {
            RenderScale(context, centre, radius, fillBrush, originValue, shownValue);
        }
        else if (!double.IsNaN(Origin) && Origin > Minimum)
        {
            // A notch inside the arc marks the neutral position.
            var inner = PointAt(centre, radius - Thickness / 2 - 8, AngleOf(Origin));
            var outer = PointAt(centre, radius - Thickness / 2 - 3, AngleOf(Origin));
            context.DrawLine(new Pen(trackBrush, 2, lineCap: PenLineCap.Round), inner, outer);
        }

        context.DrawGeometry(null, new Pen(trackBrush, Thickness, lineCap: PenLineCap.Round),
            Arc(centre, radius, StartAngle, StartAngle + SweepAngle));

        var origin = AngleOf(originValue);
        var value = AngleOf(shownValue);
        if (Math.Abs(value - origin) > 0.5)
        {
            context.DrawGeometry(null, new Pen(fillBrush, Thickness, lineCap: PenLineCap.Round),
                Arc(centre, radius, Math.Min(origin, value), Math.Max(origin, value)));
        }

        var grip = GetValue(GripProperty);
        var handle = PointAt(centre, radius, value);
        if (grip > 0)
        {
            using (context.PushOpacity(HaloOpacity * Math.Min(grip, 1)))
            {
                var halo = HaloRadius * grip;
                context.DrawEllipse(fillBrush, null, handle, halo, halo);
            }
        }

        var handleRadius = HandleRadius + (HeldHandleRadius - HandleRadius) * grip;
        context.DrawEllipse(HandleFill ?? Brushes.White, new Pen(fillBrush, 3), handle, handleRadius, handleRadius);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValueProperty)
        {
            SetValue(ShownValueProperty, Value);
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        _unsnapped = Value;
        _lastX = e.GetPosition(this).X;
        IsDragging = true;

        // The drag would otherwise scroll the page.
        e.Pointer.Capture(this);
        e.PreventGestureRecognition();
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!_isDragging)
        {
            return;
        }

        e.Handled = true;
        var x = e.GetPosition(this).X;
        _unsnapped = Math.Clamp(_unsnapped + (x - _lastX) / DragDistance * (Maximum - Minimum), Minimum, Maximum);
        _lastX = x;
        if (Math.Abs(_unsnapped - Value) >= Step * SnapThreshold)
        {
            SetSnapped(_unsnapped);
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_isDragging)
        {
            return;
        }

        IsDragging = false;
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        IsDragging = false;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        var direction = e.Key switch
        {
            Key.Right or Key.Up => 1,
            Key.Left or Key.Down => -1,
            _ => 0,
        };

        if (direction != 0)
        {
            SetSnapped(Math.Clamp(Value + direction * Step, Minimum, Maximum));
            e.Handled = true;
        }
    }

    private bool HasScale => TickFrequency > 0 && (Maximum - Minimum) / TickFrequency <= 100;

    private bool HasLabels => HasScale && LabelFrequency > 0;

    private Point Centre => new(Bounds.Width / 2, Bounds.Height / 2);

    /// <summary>
    /// Radius of the middle of the arc: as large as the held handle, or the scale and its labels, allow.
    /// </summary>
    private double Radius
    {
        get
        {
            var scale = HasScale ? TickGap + MajorTickLength : 0;
            var labels = HasLabels ? LabelGap + LabelBand : 0;
            var margin = Math.Max(HeldHandleRadius, Thickness / 2 + scale + labels) + 1;
            return Math.Min(Bounds.Width, Bounds.Height) / 2 - margin;
        }
    }

    /// <summary>
    /// Marks just outside the arc, lit in the fill colour between the origin and the value, and the labels.
    /// </summary>
    private void RenderScale(DrawingContext context, Point centre, double radius, IBrush fillBrush, double originValue, double shownValue)
    {
        var textBrush = TextBrush ?? Brushes.Gray;
        var litFrom = Math.Min(originValue, shownValue);
        var litTo = Math.Max(originValue, shownValue);
        var tolerance = TickFrequency / 1000;
        var inner = radius + Thickness / 2 + TickGap;
        var count = (int)Math.Round((Maximum - Minimum) / TickFrequency);

        for (var i = 0; i <= count; i++)
        {
            var tick = Math.Min(Minimum + i * TickFrequency, Maximum);
            var isMajor = HasLabels && IsMultiple(tick, LabelFrequency);
            var isLit = tick >= litFrom - tolerance && tick <= litTo + tolerance && litTo - litFrom > tolerance;
            var angle = AngleOf(tick);
            var length = isMajor ? MajorTickLength : MinorTickLength;
            var pen = new Pen(isLit ? fillBrush : textBrush, isMajor ? 2 : 1.5, lineCap: PenLineCap.Round);
            using (context.PushOpacity(isLit || isMajor ? 1 : 0.55))
            {
                context.DrawLine(pen, PointAt(centre, inner, angle), PointAt(centre, inner + length, angle));
            }

            if (isMajor)
            {
                DrawLabel(context, tick, centre, inner + MajorTickLength + LabelGap, angle, textBrush);
            }
        }
    }

    /// <summary>
    /// A label just beyond <paramref name="distance"/> from the centre, wherever on the circle it is.
    /// </summary>
    private void DrawLabel(DrawingContext context, double value, Point centre, double distance, double angle, IBrush brush)
    {
        var text = new FormattedText(value.ToString(LabelFormat, CultureInfo.CurrentCulture), CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, LabelTypeface, LabelFontSize, brush);
        var radians = angle * Math.PI / 180;
        var cos = Math.Cos(radians);
        var sin = Math.Sin(radians);

        // Push the box out until its nearest edge clears the circle: half its width sideways, half its height up or down.
        var reach = distance + Math.Abs(cos) * text.Width / 2 + Math.Abs(sin) * text.Height / 2;
        var middle = new Point(centre.X + reach * cos, centre.Y + reach * sin);
        context.DrawText(text, new Point(middle.X - text.Width / 2, middle.Y - text.Height / 2));
    }

    private void SetSnapped(double value)
    {
        var snapped = Math.Clamp(Math.Round(value / Step, MidpointRounding.AwayFromZero) * Step, Minimum, Maximum);
        if (!snapped.Equals(Value))
        {
            SetCurrentValue(ValueProperty, snapped);
        }
    }

    private double AngleOf(double value)
    {
        var share = (Math.Clamp(value, Minimum, Maximum) - Minimum) / (Maximum - Minimum);
        return StartAngle + share * SweepAngle;
    }

    private static bool IsMultiple(double value, double frequency)
    {
        var ratio = value / frequency;
        return Math.Abs(ratio - Math.Round(ratio)) < 1e-6;
    }

    private static Point PointAt(Point centre, double radius, double degrees)
    {
        var radians = degrees * Math.PI / 180;
        return new Point(centre.X + radius * Math.Cos(radians), centre.Y + radius * Math.Sin(radians));
    }

    private static StreamGeometry Arc(Point centre, double radius, double fromDegrees, double toDegrees)
    {
        var geometry = new StreamGeometry();
        using var figure = geometry.Open();
        figure.BeginFigure(PointAt(centre, radius, fromDegrees), false);
        figure.ArcTo(PointAt(centre, radius, toDegrees), new Size(radius, radius), 0, toDegrees - fromDegrees > 180, SweepDirection.Clockwise);
        figure.EndFigure(false);
        return geometry;
    }
}
