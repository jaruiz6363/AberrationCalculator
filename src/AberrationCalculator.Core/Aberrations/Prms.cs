using System;
using System.Collections.Generic;
using System.Linq;

namespace AberrationCalculator.Core.Aberrations;

/// <summary>
/// The RMS spot radius predicted from the Buchdahl/Rimmer coefficients, with no rays
/// traced - Robb's analytic merit function.
///
/// Robb, P. N., "Analytic merit function based on Buchdahl's aberration coefficients,"
/// <i>JOSA</i> <b>66</b>, 1037 (1976). His Eq. (2) writes the ray's intersection with the
/// Gaussian image plane as polynomials in the pupil radius rho, the pupil azimuth theta and
/// the field H; Eq. (4) is the variance of that intersection over the pupil.
///
/// Every term of eps_y and eps_z has the form  coefficient * rho^a * H^b * f(theta), so the
/// pupil average of a product of two terms separates completely:
///
///     &lt;eps_i eps_j&gt; = c_i c_j * &lt;f_i f_j&gt;_theta * H^(b_i + b_j) * 2/(a_i + a_j + 2)
///
/// The radial factor is the average of rho^(a_i+a_j) over the unit disc with weight
/// 2 rho drho. The theta factor is exact by trapezoid: these are trigonometric polynomials
/// of degree at most six, and the trapezoid rule is spectrally exact for periodic
/// integrands once the sample count exceeds the degree.
///
/// So the whole thing collapses to a quadratic form in the coefficients, evaluated once
/// here rather than carried as a table of magic constants. There are no rings and no arms:
/// the pupil average is analytic, and the only approximation left is the truncation of the
/// series at seventh order.
///
/// <para><b>Referenced to the centroid</b>, as Robb specifies - the variance about the mean
/// intersection, not about the chief ray. That is why the mean of eps_y is subtracted;
/// the mean of eps_z is zero because every one of its terms is odd in theta.</para>
///
/// <para><b>Distortion is deliberately absent.</b> E and E5 displace the whole patch
/// without changing its size, so they cannot appear in a spot radius. Robb's
/// corresponding terms vanish identically for the same reason.</para>
/// </summary>
public static class Prms
{
    /// <summary>One term of the intersection polynomial.</summary>
    private sealed class Term
    {
        public Dictionary<string, double> C = new();   // coefficient name -> multiplier
        public int A;                                  // power of rho
        public int B;                                  // power of H
        public string F = "one";                       // theta function
    }

    // Robb Eq. (2), written in the Rimmer coefficient names this program reports, with
    // seventh order in his own tau. The notational bridge is: sigma1 = B, sigma2 = F,
    // sigma3 = C, sigma4 = Pi, sigma5 = E; mu1 = B5, mu2 = F1, mu3 = F2, mu4 = M1+M2,
    // mu5 = M2, mu6 = M3, mu7 = N1+N2/2, mu8 = N2/2, mu9 = N3, mu10 = 5*C5+Pi5,
    // mu11 = C5+Pi5, mu12 = E5; tau1 = B7. Verified term by term against Eq. (2), and the
    // seventh-order lines are identical to Buchdahl's own in J. Opt. Soc. Am. 48, 747
    // (1958), p.753 - Robb took them from there.
    private static readonly Term[] Ey =
    {
        new() { C = new() { ["B"] = 1 },                 A = 3, B = 0, F = "cos"   },
        new() { C = new() { ["F"] = 2 },                 A = 2, B = 1, F = "one"   },
        new() { C = new() { ["F"] = 1 },                 A = 2, B = 1, F = "cos2"  },
        new() { C = new() { ["C"] = 3, ["Pi"] = 1 },     A = 1, B = 2, F = "cos"   },
        new() { C = new() { ["B5"] = 1 },                A = 5, B = 0, F = "cos"   },
        new() { C = new() { ["F1"] = 1 },                A = 4, B = 1, F = "one"   },
        new() { C = new() { ["F2"] = 1 },                A = 4, B = 1, F = "cos2"  },
        new() { C = new() { ["M1"] = 1, ["M2"] = 1 },    A = 3, B = 2, F = "cos"   },
        new() { C = new() { ["M3"] = 1 },                A = 3, B = 2, F = "cos3p" },
        new() { C = new() { ["N1"] = 1, ["N2"] = 0.5 },  A = 2, B = 3, F = "one"   },
        new() { C = new() { ["N2"] = 0.5 },              A = 2, B = 3, F = "cos2"  },
        new() { C = new() { ["C5"] = 5, ["Pi5"] = 1 },   A = 1, B = 4, F = "cos"   },
        new() { C = new() { ["B7"] = 1 },                A = 7, B = 0, F = "cos"   },

        // The rest of seventh order. Robb writes it in tau; tau1 is B7 above, and the
        // eighteen here are the ones that affect spot size. tau20 is distortion and is
        // absent for the same reason E and E5 are.
        new() { C = new() { ["Tau2"] = 1 },              A = 6, B = 1, F = "one"   },
        new() { C = new() { ["Tau3"] = 1 },              A = 6, B = 1, F = "cos2"  },
        new() { C = new() { ["Tau4"] = 1 },              A = 5, B = 2, F = "cos"   },
        new() { C = new() { ["Tau6"] = 1 },              A = 5, B = 2, F = "cos3p" },
        new() { C = new() { ["Tau7"] = 1 },              A = 4, B = 3, F = "one"   },
        new() { C = new() { ["Tau8"] = 1 },              A = 4, B = 3, F = "cos2"  },
        new() { C = new() { ["Tau10"] = 1 },             A = 4, B = 3, F = "cos4"  },
        new() { C = new() { ["Tau11"] = 1 },             A = 3, B = 4, F = "cos"   },
        new() { C = new() { ["Tau12"] = 1 },             A = 3, B = 4, F = "cos3p" },
        new() { C = new() { ["Tau15"] = 1 },             A = 2, B = 5, F = "one"   },
        new() { C = new() { ["Tau16"] = 1 },             A = 2, B = 5, F = "cos2"  },
        new() { C = new() { ["Tau18"] = 1 },             A = 1, B = 6, F = "cos"   },
    };

    private static readonly Term[] Ez =
    {
        new() { C = new() { ["B"] = 1 },                 A = 3, B = 0, F = "sin"   },
        new() { C = new() { ["F"] = 1 },                 A = 2, B = 1, F = "sin2"  },
        new() { C = new() { ["C"] = 1, ["Pi"] = 1 },     A = 1, B = 2, F = "sin"   },
        new() { C = new() { ["B5"] = 1 },                A = 5, B = 0, F = "sin"   },
        new() { C = new() { ["F2"] = 1 },                A = 4, B = 1, F = "sin2"  },
        new() { C = new() { ["M2"] = 1 },                A = 3, B = 2, F = "sin"   },
        new() { C = new() { ["M3"] = 1 },                A = 3, B = 2, F = "sin3p" },
        new() { C = new() { ["N3"] = 1 },                A = 2, B = 3, F = "sin2"  },
        new() { C = new() { ["C5"] = 1, ["Pi5"] = 1 },   A = 1, B = 4, F = "sin"   },
        new() { C = new() { ["B7"] = 1 },                A = 7, B = 0, F = "sin"   },

        new() { C = new() { ["Tau3"] = 1 },              A = 6, B = 1, F = "sin2"  },
        new() { C = new() { ["Tau5"] = 1 },              A = 5, B = 2, F = "sin"   },
        new() { C = new() { ["Tau6"] = 1 },              A = 5, B = 2, F = "sin3p" },
        new() { C = new() { ["Tau9"] = 1 },              A = 4, B = 3, F = "sin2"  },
        new() { C = new() { ["Tau10"] = 1 },             A = 4, B = 3, F = "sin4"  },
        new() { C = new() { ["Tau13"] = 1 },             A = 3, B = 4, F = "sin"   },
        new() { C = new() { ["Tau14"] = 1 },             A = 3, B = 4, F = "sin3p" },
        new() { C = new() { ["Tau17"] = 1 },             A = 2, B = 5, F = "sin2"  },
        new() { C = new() { ["Tau19"] = 1 },             A = 1, B = 6, F = "sin"   },
    };

    private static double Theta(string name, double t) => name switch
    {
        "one" => 1.0,
        "cos" => Math.Cos(t),
        "cos2" => Math.Cos(2 * t),
        "cos3p" => Math.Cos(t) * Math.Cos(t) * Math.Cos(t),
        "sin" => Math.Sin(t),
        "sin2" => Math.Sin(2 * t),
        "sin3p" => Math.Cos(t) * Math.Cos(t) * Math.Sin(t),
        "cos4" => Math.Cos(4 * t),
        "sin4" => Math.Sin(4 * t),
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, "unknown theta function"),
    };

    /// <summary>
    /// Average of f1*f2 over a full turn. Trapezoid is exact here: the integrand is a
    /// trigonometric polynomial of degree at most six and the sample count is far above it.
    /// The result is snapped to a nearby simple rational to clear floating-point dust.
    /// </summary>
    private static double ThetaAverage(string f1, string f2)
    {
        const int n = 4096;
        double s = 0.0;
        for (int k = 0; k < n; k++)
        {
            double t = 2 * Math.PI * k / n;
            s += Theta(f1, t) * Theta(f2, t);
        }
        double v = s / n;
        double snapped = Math.Round(v * 65536.0) / 65536.0;
        return Math.Abs(v - snapped) < 1e-9 ? snapped : v;
    }

    /// <summary>One entry of the assembled quadratic form.</summary>
    private readonly struct Pair
    {
        public readonly string A, B;
        public readonly int HPower;
        public readonly double Factor;
        public Pair(string a, string b, int h, double f) { A = a; B = b; HPower = h; Factor = f; }
    }

    private static readonly Pair[] Form = Build();

    private static Pair[] Build()
    {
        var acc = new Dictionary<(string, string, int), double>();

        void Add(string a, string b, int h, double v)
        {
            // Order the names so a*b and b*a land in the same bucket.
            var key = string.CompareOrdinal(a, b) <= 0 ? (a, b, h) : (b, a, h);
            acc[key] = acc.TryGetValue(key, out var cur) ? cur + v : v;
        }

        void Accumulate(Term[] list)
        {
            foreach (var x in list)
                foreach (var y in list)
                {
                    double th = ThetaAverage(x.F, y.F);
                    if (th == 0.0) continue;
                    double rad = 2.0 / (x.A + y.A + 2);
                    foreach (var (ca, va) in x.C)
                        foreach (var (cb, vb) in y.C)
                            Add(ca, cb, x.B + y.B, va * vb * th * rad);
                }
        }

        Accumulate(Ey);
        Accumulate(Ez);

        // Centroid reference: subtract the square of the mean of eps_y. Only terms whose
        // theta average is non-zero survive, and <eps_z> is zero by symmetry.
        var mean = new List<(string C, double V, int H)>();
        foreach (var t in Ey)
        {
            double m = ThetaAverage(t.F, "one");
            if (Math.Abs(m) < 1e-12) continue;
            foreach (var (c, v) in t.C)
                mean.Add((c, v * m * 2.0 / (t.A + 2), t.B));
        }
        foreach (var mi in mean)
            foreach (var mj in mean)
                Add(mi.C, mj.C, mi.H + mj.H, -mi.V * mj.V);

        return acc.Where(kv => Math.Abs(kv.Value) > 1e-12)
                  .Select(kv => new Pair(kv.Key.Item1, kv.Key.Item2, kv.Key.Item3, kv.Value))
                  .OrderBy(p => p.HPower).ThenBy(p => p.A).ThenBy(p => p.B)
                  .ToArray();
    }

    /// <summary>Number of terms in the assembled quadratic form - a handle for tests.</summary>
    public static int TermCount => Form.Length;

    /// <summary>The assembled form, as (coefficient, coefficient, H power, factor).</summary>
    public static IEnumerable<(string A, string B, int HPower, double Factor)> Terms =>
        Form.Select(p => (p.A, p.B, p.HPower, p.Factor));

    /// <summary>
    /// Robb's Eq. (2) itself - where one ray lands on the Gaussian image plane, rather than
    /// the spread of all of them. <paramref name="rho"/> is the normalised pupil radius,
    /// <paramref name="theta"/> its azimuth measured from the meridian, and
    /// <paramref name="h"/> the field as a fraction of tan(theta_max). The result is in the
    /// same length units as the coefficients, measured from the Gaussian image point.
    ///
    /// <para>The RMS spot below is the pupil average of this squared, so the two cannot
    /// disagree. It is exposed separately because a ray fan says something an RMS radius
    /// cannot: the SHAPE of the residual against a traced fan shows which order is missing,
    /// where a single number only says how much is.</para>
    ///
    /// <para><paramref name="order"/> truncates the series - 3, 5 or 7 - so that the orders
    /// can be compared against each other. Terms are selected by total degree a+b.</para>
    /// </summary>
    public static (double Y, double Z) Transverse(BuchdahlTerms totals, double rho,
                                                  double theta, double h, int order = 7)
    {
        if (totals == null) throw new ArgumentNullException(nameof(totals));

        double Sum(Term[] list)
        {
            double s = 0.0;
            foreach (var t in list)
            {
                if (t.A + t.B > order) continue;
                double v = 0.0;
                foreach (var (name, mult) in t.C) v += mult * totals[name];
                s += v * Math.Pow(rho, t.A) * (t.B == 0 ? 1.0 : Math.Pow(h, t.B))
                   * Theta(t.F, theta);
            }
            return s;
        }
        return (Sum(Ey), Sum(Ez));
    }

    /// <summary>
    /// Mean square spot radius at fractional field height <paramref name="h"/>, from the
    /// system's transverse coefficients. Zero on axis for a design with no spherical
    /// aberration; never negative.
    /// </summary>
    public static double MeanSquare(BuchdahlTerms totals, double h)
    {
        if (totals == null) throw new ArgumentNullException(nameof(totals));

        double sum = 0.0;
        foreach (var p in Form)
        {
            double hp = p.HPower == 0 ? 1.0 : Math.Pow(h, p.HPower);
            sum += p.Factor * totals[p.A] * totals[p.B] * hp;
        }
        return sum;
    }

    /// <summary>
    /// RMS spot radius at fractional field height <paramref name="h"/>.
    ///
    /// Clamped at zero: the series is a truncation, and on a well-corrected design the
    /// surviving terms can cancel to a very small negative number that is numerical dust
    /// rather than a real quantity.
    /// </summary>
    public static double Value(BuchdahlTerms totals, double h)
    {
        double ms = MeanSquare(totals, h);
        return ms > 0.0 ? Math.Sqrt(ms) : 0.0;
    }

    /// <summary>
    /// The composite over every field and wavelength - PRMSA.
    ///
    /// Robb's spectrally weighted average: the weighted mean of the mean-square radii,
    /// square-rooted once at the end. Averaging the RMS values directly instead would
    /// weight a design's worst field differently and is not the same quantity.
    /// </summary>
    /// <param name="cases">
    /// One entry per (wavelength, field) combination: the coefficients at that wavelength,
    /// the fractional field height, and the product of the field and wavelength weights.
    /// </param>
    public static double Composite(IEnumerable<(BuchdahlTerms Totals, double H, double Weight)> cases)
    {
        double num = 0.0, den = 0.0;
        foreach (var (totals, h, w) in cases)
        {
            if (w <= 0.0) continue;
            double ms = MeanSquare(totals, h);
            num += w * (ms > 0.0 ? ms : 0.0);
            den += w;
        }
        if (den <= 0.0) return 0.0;
        double mean = num / den;
        return mean > 0.0 ? Math.Sqrt(mean) : 0.0;
    }
}
