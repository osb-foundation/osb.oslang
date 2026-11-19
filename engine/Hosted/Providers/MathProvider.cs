using System.Text;
using Osb.Engine.Core.Providers;

namespace Osb.Engine.Hosted.Providers;

/// <summary>
/// Hosted BCL implementation of IMathProvider.
/// </summary>
public sealed class MathProvider : IMathProvider
{
    public double PI => System.Math.PI;
    public double E => System.Math.E;

    public double Sqrt(double x) => System.Math.Sqrt(x);
    public double Abs(double x) => System.Math.Abs(x);
    public double Pow(double x, double y) => System.Math.Pow(x, y);
    public double Floor(double x) => System.Math.Floor(x);
    public double Ceil(double x) => System.Math.Ceiling(x);
    public double Round(double x) => System.Math.Round(x); // Banker's rounding
    public double Trunc(double x) => System.Math.Truncate(x);
    public double Sin(double x) => System.Math.Sin(x);
    public double Cos(double x) => System.Math.Cos(x);
    public double Tan(double x) => System.Math.Tan(x);
    public double Asin(double x) => System.Math.Asin(x);
    public double Acos(double x) => System.Math.Acos(x);
    public double Atan(double x) => System.Math.Atan(x);
    public double Log(double x) => System.Math.Log(x);
    public double Log10(double x) => System.Math.Log10(x);
    public double Exp(double x) => System.Math.Exp(x);
    public double Sign(double x) => System.Math.Sign(x);
    public double Min(double x, double y) => System.Math.Min(x, y);
    public double Max(double x, double y) => System.Math.Max(x, y);
    public double Clamp(double x, double min, double max) => System.Math.Clamp(x, min, max);
}