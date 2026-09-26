using System.Windows;
using System.Windows.Media;

namespace GLOptimizer.App.Controls;

public class Sparkline : FrameworkElement
{
    public static readonly DependencyProperty ValuesProperty = DependencyProperty.Register(
        nameof(Values),
        typeof(double[]),
        typeof(Sparkline),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public double[]? Values
    {
        get => (double[]?)GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        var values = Values;
        if (values is null || values.Length < 2 || ActualWidth <= 1 || ActualHeight <= 1)
        {
            return;
        }

        var pen = new Pen(Brushes.White, 1.5);
        pen.Freeze();
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            var started = false;
            var width = ActualWidth - 1;
            var height = ActualHeight - 2;
            for (var index = 0; index < values.Length; index++)
            {
                var value = values[index];
                if (double.IsNaN(value) || double.IsInfinity(value))
                {
                    started = false;
                    continue;
                }

                var x = index * width / (values.Length - 1);
                var y = 1 + height - (Math.Clamp(value, 0, 100) / 100d * height);
                var point = new Point(x, y);
                if (!started)
                {
                    context.BeginFigure(point, isFilled: false, isClosed: false);
                    started = true;
                }
                else
                {
                    context.LineTo(point, isStroked: true, isSmoothJoin: false);
                }
            }
        }

        geometry.Freeze();
        drawingContext.DrawGeometry(null, pen, geometry);
    }
}
