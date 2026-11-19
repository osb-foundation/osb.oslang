namespace Osb.Engine.Core.Providers;

/// <summary>
/// Math operations provider. Implemented by hosted BCL or freestanding platform.
/// </summary>
public interface IMathProvider
{
    double PI { get; }
    double E { get; }

    double Sqrt(double x);
    double Abs(double x);
    double Pow(double x, double y);
    double Floor(double x);
    double Ceil(double x);
    double Round(double x); // Banker's rounding
    double Trunc(double x);
    double Sin(double x);
    double Cos(double x);
    double Tan(double x);
    double Asin(double x);
    double Acos(double x);
    double Atan(double x);
    double Log(double x);
    double Log10(double x);
    double Exp(double x);
    double Sign(double x);
    double Min(double x, double y);
    double Max(double x, double y);
    double Clamp(double x, double min, double max);
}