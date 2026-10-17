using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;
using VolumeX.Audio;

namespace VolumeX.Views;

/// <summary>
/// Graphic equalizer: a vertical slider per band with its value above and its frequency below, and the curve
/// through the handles. Pressing anywhere in a band's column and dragging sets that band.
/// </summary>
/// <remarks>
/// The curve is <see cref="EqualizerCurve"/>, the one the sound effect applies, flat beyond the outer bands;
/// the bands sit on a logarithmic frequency axis, so equal columns are equal frequency ratios.
/// </remarks>
public sealed class EqualizerGraph : Control
{
    public static readonly StyledProperty<IReadOnlyList<double>?> ValuesProperty =
        AvaloniaProperty.Register<EqualizerGraph, IReadOnlyList<double>?>(nameof(Values), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<IReadOnlyList<string>?> LabelsProperty =
        AvaloniaProperty.Register<EqualizerGraph, IReadOnlyList<string>?>(nameof(Labels));

    public static readonly StyledProperty<double> MinimumProperty =
        AvaloniaProperty.Register<EqualizerGraph, double>(nameof(Minimum), -12);

    public static readonly StyledProperty<double> MaximumProperty =
        AvaloniaProperty.Register<EqualizerGraph, double>(nameof(Maximum), 12);

    public static readonly StyledProperty<double> StepProperty =
        AvaloniaProperty.Register<EqualizerGraph, double>(nameof(Step), 0.5);

    public static readonly StyledProperty<bool> IsActiveProperty =
        AvaloniaProperty.Register<EqualizerGraph, bool>(nameof(IsActive), true);

    public static readonly StyledProperty<IBrush?> CurveBrushProperty =
        AvaloniaProperty.Register<EqualizerGraph, IBrush?>(nameof(CurveBrush));

    public static readonly StyledProperty<IBrush?> FillBrushProperty =
        AvaloniaProperty.Register<EqualizerGraph, IBrush?>(nameof(FillBrush));

    public static readonly StyledProperty<IBrush?> InactiveBrushProperty =
        AvaloniaProperty.Register<EqualizerGraph, IBrush?>(nameof(InactiveBrush));

    public static readonly StyledProperty<IBrush?> TrackBrushProperty =
        AvaloniaProperty.Register<EqualizerGraph, IBrush?>(nameof(TrackBrush));

    public static readonly StyledProperty<IBrush?> TextBrushProperty =
        AvaloniaProperty.Register<EqualizerGraph, IBrush?>(nameof(TextBrush));

    public static readonly StyledProperty<IBrush?> HandleFillProperty =
        AvaloniaProperty.Register<EqualizerGraph, IBrush?>(nameof(HandleFill));

    private const double LabelHeight = 22;
    private const double HandleRadius = 7;
    private const double DraggedHandleRadius = 9;
    private const double LabelFontSize = 12;

    private int _draggedBand = -1;

    static EqualizerGraph()
    {
        AffectsRender<EqualizerGraph>(
            ValuesProperty, LabelsProperty, MinimumProperty, MaximumProperty, IsActiveProperty,
            CurveBrushProperty, FillBrushProperty, InactiveBrushProperty, TrackBrushProperty, TextBrushProperty, HandleFillProperty);
    }

    /// <summary>
    /// Gain of every band in decibels; replaced with a new list on every change.
    /// </summary>
    public IReadOnlyList<double>? Values
    {
        get => GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    /// <summary>
    /// Frequency label under every band.
    /// </summary>
    public IReadOnlyList<string>? Labels
    {
        get => GetValue(LabelsProperty);
        set => SetValue(LabelsProperty, value);
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
    /// False draws the curve in <see cref="InactiveBrush"/>: the equalizer is switched off but still editable.
    /// </summary>
    public bool IsActive
    {
        get => GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    public IBrush? CurveBrush
    {
        get => GetValue(CurveBrushProperty);
        set => SetValue(CurveBrushProperty, value);
    }

    /// <summary>
    /// Fills the area between the curve and 0 dB while the equalizer is on.
    /// </summary>
    public IBrush? FillBrush
    {
        get => GetValue(FillBrushProperty);
        set => SetValue(FillBrushProperty, value);
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

    public IBrush? TextBrush
    {
        get => GetValue(TextBrushProperty);
        set => SetValue(TextBrushProperty, value);
    }

    public IBrush? HandleFill
    {
        get => GetValue(HandleFillProperty);
        set => SetValue(HandleFillProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        var values = Values;
        if (values is not { Count: > 0 } || Bounds.Width <= 0)
        {
            return;
        }

        var column = Bounds.Width / values.Count;
        var curveBrush = (IsActive ? CurveBrush : InactiveBrush) ?? Brushes.Gray;
        var trackBrush = TrackBrush ?? Brushes.LightGray;
        var textBrush = TextBrush ?? Brushes.Gray;
        var points = values.Select((value, band) => new Point(column * (band + 0.5), ToY(value))).ToArray();
        var tangents = EqualizerCurve.Tangents(values);

        var trackPen = new Pen(trackBrush, 2, lineCap: PenLineCap.Round);
        foreach (var point in points)
        {
            context.DrawLine(trackPen, new Point(point.X, PlotTop), new Point(point.X, PlotBottom));
        }

        var zeroY = ToY(0);
        context.DrawLine(new Pen(trackBrush, 1, new DashStyle([4, 4], 0)), new Point(0, zeroY), new Point(Bounds.Width, zeroY));

        if (IsActive && FillBrush is { } fill)
        {
            context.DrawGeometry(fill, null, Curve(points, tangents, column, zeroY));
        }

        var curvePen = new Pen(curveBrush, 2.5, lineCap: PenLineCap.Round);
        context.DrawGeometry(null, curvePen, Curve(points, tangents, column, null));

        for (var band = 0; band < values.Count; band++)
        {
            var x = points[band].X;
            var isDragged = band == _draggedBand;
            DrawCentred(context, FormatGain(values[band]), x, 0, isDragged ? curveBrush : textBrush, isDragged);
            if (Labels is { } labels && band < labels.Count)
            {
                DrawCentred(context, labels[band], x, Bounds.Height - LabelHeight + 6, textBrush, false);
            }

            var radius = isDragged ? DraggedHandleRadius : HandleRadius;
            context.DrawEllipse(HandleFill ?? Brushes.White, curvePen, points[band], radius, radius);
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var values = Values;
        if (values is not { Count: > 0 } || Bounds.Width <= 0)
        {
            return;
        }

        var position = e.GetPosition(this);
        _draggedBand = Math.Clamp((int)(position.X / (Bounds.Width / values.Count)), 0, values.Count - 1);

        // Vertical drags would otherwise scroll the page.
        e.Pointer.Capture(this);
        e.PreventGestureRecognition();
        e.Handled = true;
        SetDraggedBand(position.Y);
        InvalidateVisual();
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_draggedBand < 0)
        {
            return;
        }

        SetDraggedBand(e.GetPosition(this).Y);
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_draggedBand < 0)
        {
            return;
        }

        _draggedBand = -1;
        e.Pointer.Capture(null);
        e.Handled = true;
        InvalidateVisual();
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _draggedBand = -1;
        InvalidateVisual();
    }

    private double PlotTop => LabelHeight + DraggedHandleRadius;

    private double PlotBottom => Math.Max(PlotTop + 1, Bounds.Height - LabelHeight - DraggedHandleRadius);

    private double ToY(double value)
    {
        var share = (Math.Clamp(value, Minimum, Maximum) - Minimum) / (Maximum - Minimum);
        return PlotBottom - share * (PlotBottom - PlotTop);
    }

    private void SetDraggedBand(double y)
    {
        var values = Values;
        if (values is null || _draggedBand >= values.Count)
        {
            return;
        }

        var raw = Minimum + (PlotBottom - y) / (PlotBottom - PlotTop) * (Maximum - Minimum);
        var value = Math.Clamp(Math.Round(raw / Step, MidpointRounding.AwayFromZero) * Step, Minimum, Maximum);
        if (value.Equals(values[_draggedBand]))
        {
            return;
        }

        var changed = values.ToArray();
        changed[_draggedBand] = value;
        SetCurrentValue(ValuesProperty, changed);
    }

    private static void DrawCentred(DrawingContext context, string text, double x, double y, IBrush brush, bool isBold)
    {
        var typeface = new Typeface(FontFamily.Default, FontStyle.Normal, isBold ? FontWeight.SemiBold : FontWeight.Normal);
        var formatted = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, LabelFontSize, brush);
        context.DrawText(formatted, new Point(x - formatted.Width / 2, y));
    }

    private static string FormatGain(double decibels)
    {
        return decibels.ToString("+0.#;−0.#;0", CultureInfo.CurrentCulture);
    }

    /// <summary>
    /// The equalizer curve as cubic Béziers through the handles, level beyond the outer ones; closed down to
    /// <paramref name="baseY"/> when given, for the fill.
    /// </summary>
    private StreamGeometry Curve(IReadOnlyList<Point> points, IReadOnlyList<double> tangents, double column, double? baseY)
    {
        // A slope of m dB per band moves the curve by m · pixelsPerDecibel per column; a Hermite segment's
        // Bézier control points sit a third of a column along those slopes.
        var pixelsPerDecibel = (PlotBottom - PlotTop) / (Maximum - Minimum);
        var left = new Point(0, points[0].Y);
        var right = new Point(Bounds.Width, points[^1].Y);

        var geometry = new StreamGeometry();
        using var figure = geometry.Open();
        if (baseY is { } closingY)
        {
            figure.BeginFigure(new Point(left.X, closingY), true);
            figure.LineTo(left);
        }
        else
        {
            figure.BeginFigure(left, false);
        }

        figure.LineTo(points[0]);
        for (var i = 0; i < points.Count - 1; i++)
        {
            var start = points[i];
            var end = points[i + 1];
            figure.CubicBezierTo(
                new Point(start.X + column / 3, start.Y - tangents[i] * pixelsPerDecibel / 3),
                new Point(end.X - column / 3, end.Y + tangents[i + 1] * pixelsPerDecibel / 3),
                end);
        }

        figure.LineTo(right);
        if (baseY is { } bottomY)
        {
            figure.LineTo(new Point(right.X, bottomY));
        }

        figure.EndFigure(baseY is not null);
        return geometry;
    }
}
