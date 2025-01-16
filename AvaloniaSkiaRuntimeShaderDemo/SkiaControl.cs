using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Avalonia.Threading;
using SkiaSharp;
using System;

namespace AvaloniaSkiaRuntimeShaderDemo
{
    public class SkiaControl : Control
    {
        public override void Render(DrawingContext context)
        {
            context.Custom(new SkiaControlDrawOp(new Rect(0, 0, Bounds.Width, Bounds.Height)));
            Dispatcher.UIThread.InvokeAsync(InvalidateVisual, DispatcherPriority.Background);
        }
    }

    internal class SkiaControlDrawOp : ICustomDrawOperation
    {
        private SKRuntimeShaderBuilder? _shaderBuilder;
        private SKShader? _noiseImageShader;

        public SkiaControlDrawOp(Rect bounds)
        {
            Bounds = bounds;
            InitShader();
        }

        private void InitShader()
        {
            var sksl = @"
                uniform float2 resolution; 
                uniform shader src;
                uniform shader noise;
                uniform float2 noiseResolution; 
                uniform float  progress;
                uniform float  randomSeed;

                float4 main(float2 coord) {
                    float val = noise.eval(fract(coord / resolution + randomSeed) * noiseResolution).x;

                    if(val < progress)
                    {
                        return src.eval(coord);
                    }
                    else
                    {
                        return float4(0,0,0,0);
                    }
                }
";

            var noiseImage = SKImage.FromEncodedData("noise.png");
            _noiseImageShader = SKShader.CreateImage(noiseImage);

            var effect = SKRuntimeEffect.CreateShader(sksl, out var str);
            if (effect != null)
            {
                _shaderBuilder = new SKRuntimeShaderBuilder(effect);
                _shaderBuilder.Uniforms["randomSeed"] = 0f;
                _shaderBuilder.Uniforms["noiseResolution"] = new SKSize(noiseImage.Width, noiseImage.Height);
            }
        }

        public Rect Bounds { get; }

        public void Dispose()
        {

        }

        public bool Equals(ICustomDrawOperation? other) => false;

        public bool HitTest(Point p) => false;

        public void Render(ImmediateDrawingContext context)
        {
            if (context.TryGetFeature<ISkiaSharpApiLeaseFeature>() is ISkiaSharpApiLeaseFeature leaseFeature)
            {
                using var lease = leaseFeature.Lease();
                var canvas = lease.SkCanvas;
                canvas.Save();

                if (_shaderBuilder != null && _noiseImageShader != null)
                {
                    var progress = Environment.TickCount % 2000 / 2000.0f;
                    _shaderBuilder.Uniforms["progress"] = progress;
                    _shaderBuilder.Uniforms["resolution"] = new SKSize((float)Bounds.Width, (float)Bounds.Height);
                    _shaderBuilder.Children["noise"] = _noiseImageShader;
                    var filter = SKImageFilter.CreateRuntimeShader(_shaderBuilder, 0f, "src", null);
                    using var paint = new SKPaint
                    {
                        ImageFilter = filter,
                    };
                    canvas.SaveLayer(paint);
                }

                if (Bounds.Width > 20 && Bounds.Height > 20)
                {
                    using var paint = new SKPaint
                    {
                        Color = new SKColor(255, 0, 0),
                    };

                    var rect = new SKRect(10, 10, (float)Bounds.Width - 20, (float)Bounds.Height - 20);
                    canvas.DrawRoundRect(rect, 5, 5, paint);
                }

                if (_shaderBuilder != null && _noiseImageShader != null)
                {
                    canvas.Restore();
                }

                canvas.Restore();
            }
            else
            {

            }
        }
    }
}
