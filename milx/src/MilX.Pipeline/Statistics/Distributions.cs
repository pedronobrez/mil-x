namespace MilX.Pipeline.Statistics;

/// <summary>
/// The distributions the tests need, written out rather than taken from a package: the normal,
/// Student's t, F, chi-square and the hypergeometric, through the regularised incomplete beta and
/// gamma functions. Accurate to about twelve digits, which is more than a p-value is worth.
/// </summary>
public static class Distributions
{
    /// <summary>ln Γ(x) by the Lanczos approximation.</summary>
    public static double LogGamma(double x)
    {
        if (x <= 0) return double.NaN;
        double[] c = { 76.18009172947146, -86.50532032941677, 24.01409824083091, -1.231739572450155, 0.1208650973866179e-2, -0.5395239384953e-5 };
        var y = x;
        var t = x + 5.5;
        t -= (x + 0.5) * Math.Log(t);
        var s = 1.000000000190015;
        for (var j = 0; j < 6; j++) s += c[j] / ++y;
        return -t + Math.Log(2.5066282746310005 * s / x);
    }

    /// <summary>The regularised lower incomplete gamma P(a, x).</summary>
    public static double RegularizedGammaP(double a, double x)
    {
        if (x <= 0 || a <= 0) return 0;
        if (x < a + 1)
        {
            // series
            var ap = a;
            var sum = 1.0 / a;
            var del = sum;
            for (var n = 0; n < 500; n++)
            {
                ap += 1;
                del *= x / ap;
                sum += del;
                if (Math.Abs(del) < Math.Abs(sum) * 1e-15) break;
            }
            return sum * Math.Exp(-x + a * Math.Log(x) - LogGamma(a));
        }
        // continued fraction for Q, then 1 - Q
        var b = x + 1 - a;
        var c = 1.0 / 1e-300;
        var d = 1.0 / b;
        var h = d;
        for (var i = 1; i < 500; i++)
        {
            var an = -i * (i - a);
            b += 2;
            d = an * d + b;
            if (Math.Abs(d) < 1e-300) d = 1e-300;
            c = b + an / c;
            if (Math.Abs(c) < 1e-300) c = 1e-300;
            d = 1 / d;
            var delta = d * c;
            h *= delta;
            if (Math.Abs(delta - 1) < 1e-15) break;
        }
        return 1 - Math.Exp(-x + a * Math.Log(x) - LogGamma(a)) * h;
    }

    /// <summary>The regularised incomplete beta I_x(a, b).</summary>
    public static double RegularizedBeta(double x, double a, double b)
    {
        if (x <= 0) return 0;
        if (x >= 1) return 1;
        var lbeta = LogGamma(a + b) - LogGamma(a) - LogGamma(b) + a * Math.Log(x) + b * Math.Log(1 - x);
        var front = Math.Exp(lbeta);
        // the continued fraction converges fast for x < (a+1)/(a+b+2); use the symmetry otherwise
        if (x < (a + 1) / (a + b + 2)) return front * BetaContinuedFraction(x, a, b) / a;
        return 1 - front * BetaContinuedFraction(1 - x, b, a) / b;
    }

    private static double BetaContinuedFraction(double x, double a, double b)
    {
        const double tiny = 1e-300;
        var qab = a + b;
        var qap = a + 1;
        var qam = a - 1;
        var c = 1.0;
        var d = 1 - qab * x / qap;
        if (Math.Abs(d) < tiny) d = tiny;
        d = 1 / d;
        var h = d;
        for (var m = 1; m <= 500; m++)
        {
            var m2 = 2 * m;
            var aa = m * (b - m) * x / ((qam + m2) * (a + m2));
            d = 1 + aa * d;
            if (Math.Abs(d) < tiny) d = tiny;
            c = 1 + aa / c;
            if (Math.Abs(c) < tiny) c = tiny;
            d = 1 / d;
            h *= d * c;
            aa = -(a + m) * (qab + m) * x / ((a + m2) * (qap + m2));
            d = 1 + aa * d;
            if (Math.Abs(d) < tiny) d = tiny;
            c = 1 + aa / c;
            if (Math.Abs(c) < tiny) c = tiny;
            d = 1 / d;
            var del = d * c;
            h *= del;
            if (Math.Abs(del - 1) < 1e-15) break;
        }
        return h;
    }

    /// <summary>Φ(z), the standard normal distribution function.</summary>
    public static double NormalCdf(double z)
    {
        if (double.IsNaN(z)) return double.NaN;
        if (z < -40) return 0;
        if (z > 40) return 1;
        // erf through the incomplete gamma: erf(x) = P(1/2, x²)
        var p = RegularizedGammaP(0.5, z * z / 2);
        return z >= 0 ? 0.5 + 0.5 * p : 0.5 - 0.5 * p;
    }

    /// <summary>Φ⁻¹(p): Acklam's rational approximation, polished with one Newton step.</summary>
    public static double NormalQuantile(double p)
    {
        if (p <= 0) return double.NegativeInfinity;
        if (p >= 1) return double.PositiveInfinity;
        double[] a = { -3.969683028665376e+01, 2.209460984245205e+02, -2.759285104469687e+02, 1.383577518672690e+02, -3.066479806614716e+01, 2.506628277459239e+00 };
        double[] b = { -5.447609879822406e+01, 1.615858368580409e+02, -1.556989798598866e+02, 6.680131188771972e+01, -1.328068155288572e+01 };
        double[] c = { -7.784894002430293e-03, -3.223964580411365e-01, -2.400758277161838e+00, -2.549732539343734e+00, 4.374664141464968e+00, 2.938163982698783e+00 };
        double[] d = { 7.784695709041462e-03, 3.224671290700398e-01, 2.445134137142996e+00, 3.754408661907416e+00 };
        const double low = 0.02425;
        double x;
        if (p < low)
        {
            var q = Math.Sqrt(-2 * Math.Log(p));
            x = (((((c[0] * q + c[1]) * q + c[2]) * q + c[3]) * q + c[4]) * q + c[5]) / ((((d[0] * q + d[1]) * q + d[2]) * q + d[3]) * q + 1);
        }
        else if (p <= 1 - low)
        {
            var q = p - 0.5;
            var r = q * q;
            x = (((((a[0] * r + a[1]) * r + a[2]) * r + a[3]) * r + a[4]) * r + a[5]) * q / (((((b[0] * r + b[1]) * r + b[2]) * r + b[3]) * r + b[4]) * r + 1);
        }
        else
        {
            var q = Math.Sqrt(-2 * Math.Log(1 - p));
            x = -(((((c[0] * q + c[1]) * q + c[2]) * q + c[3]) * q + c[4]) * q + c[5]) / ((((d[0] * q + d[1]) * q + d[2]) * q + d[3]) * q + 1);
        }
        // one Newton step on the CDF
        var e = NormalCdf(x) - p;
        var u = e * Math.Sqrt(2 * Math.PI) * Math.Exp(x * x / 2);
        return x - u / (1 + x * u / 2);
    }

    /// <summary>Two-sided p-value of a t statistic with the given degrees of freedom.</summary>
    public static double StudentTwoSided(double t, double df)
    {
        if (double.IsNaN(t) || df <= 0) return double.NaN;
        if (double.IsInfinity(t)) return 0;
        var x = df / (df + t * t);
        return RegularizedBeta(x, df / 2, 0.5);
    }

    /// <summary>P(T ≤ t) for Student's t.</summary>
    public static double StudentCdf(double t, double df)
    {
        var two = StudentTwoSided(t, df);
        return t >= 0 ? 1 - two / 2 : two / 2;
    }

    /// <summary>P(F ≥ f) for an F statistic with the given degrees of freedom.</summary>
    public static double FisherUpper(double f, double df1, double df2)
    {
        if (double.IsNaN(f) || f <= 0 || df1 <= 0 || df2 <= 0) return 1;
        return RegularizedBeta(df2 / (df2 + df1 * f), df2 / 2, df1 / 2);
    }

    /// <summary>P(χ² ≥ x) with k degrees of freedom.</summary>
    public static double ChiSquareUpper(double x, double k)
    {
        if (double.IsNaN(x) || x <= 0 || k <= 0) return 1;
        return 1 - RegularizedGammaP(k / 2, x / 2);
    }

    /// <summary>ln C(n, k).</summary>
    public static double LogChoose(int n, int k) => k < 0 || k > n ? double.NegativeInfinity : LogGamma(n + 1) - LogGamma(k + 1) - LogGamma(n - k + 1);

    /// <summary>
    /// The one-sided hypergeometric tail: the probability of drawing at least <paramref name="k"/>
    /// marked items in <paramref name="n"/> draws from a population of <paramref name="total"/>
    /// holding <paramref name="marked"/> of them — the over-representation test.
    /// </summary>
    public static double HypergeometricUpper(int k, int n, int marked, int total)
    {
        if (k <= 0) return 1;
        var max = Math.Min(n, marked);
        if (k > max) return 0;
        var denominator = LogChoose(total, n);
        double sum = 0;
        for (var i = k; i <= max; i++)
        {
            if (n - i > total - marked) continue;
            sum += Math.Exp(LogChoose(marked, i) + LogChoose(total - marked, n - i) - denominator);
        }
        return Math.Min(1, sum);
    }
}

/// <summary>Adjustments for testing many features at once.</summary>
public enum PAdjustment
{
    None,
    /// <summary>Benjamini–Hochberg false discovery rate.</summary>
    FalseDiscoveryRate,
    Bonferroni,
    Holm,
}

public static class MultipleTesting
{
    /// <summary>Adjusted p-values in the order given; NaNs stay NaN and are not counted.</summary>
    public static double[] Adjust(IReadOnlyList<double> p, PAdjustment method)
    {
        var result = new double[p.Count];
        var indices = Enumerable.Range(0, p.Count).Where(i => !double.IsNaN(p[i])).ToList();
        var m = indices.Count;
        for (var i = 0; i < p.Count; i++) result[i] = p[i];
        if (m == 0 || method == PAdjustment.None) return result;
        switch (method)
        {
            case PAdjustment.Bonferroni:
                foreach (var i in indices) result[i] = Math.Min(1, p[i] * m);
                break;
            case PAdjustment.Holm:
            {
                var ordered = indices.OrderBy(i => p[i]).ToList();
                var running = 0.0;
                for (var rank = 0; rank < m; rank++)
                {
                    var value = Math.Min(1, p[ordered[rank]] * (m - rank));
                    running = Math.Max(running, value);
                    result[ordered[rank]] = running;
                }
                break;
            }
            case PAdjustment.FalseDiscoveryRate:
            {
                var ordered = indices.OrderByDescending(i => p[i]).ToList();
                var running = 1.0;
                for (var position = 0; position < m; position++)
                {
                    var rank = m - position;   // 1-based rank in ascending order
                    var value = Math.Min(1, p[ordered[position]] * m / rank);
                    running = Math.Min(running, value);
                    result[ordered[position]] = running;
                }
                break;
            }
        }
        return result;
    }
}
