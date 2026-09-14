using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using AberrationCalculator.Core.Aberrations;
using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.IO;
using AberrationCalculator.Core.RayTrace;
using Xunit;
using Xunit.Abstractions;

namespace AberrationCalculator.Tests;

/// <summary>
/// Which of Buchdahl's symbols each of the scheme's numbered entries IS, established by
/// measurement rather than by inference.
///
/// <para><b>Why this is needed.</b> Paper III's Table I is the condensed iteration of M Secs. 84
/// and 85 in a NUMBERED arrangement, so the monograph's symbols are all present under other
/// names - but the correspondence is nowhere written down, and guessing at it has already cost
/// a whole reading of Sec. 85. A correction was built on the assumption that Buchdahl's
/// accumulated <c>'A_q</c> is the scheme's <c>t20</c>, measured, and reported as neutral; it was
/// not neutral, it was VOID, because the bracket it formed was not the one the equation names.
/// </para>
///
/// <para><b>Why it can be measured.</b> On a SPHERICAL system the scheme is verified - against
/// Buchdahl's own printed numbers, an independent implementation, and Forbes' series trace to
/// 2E-13 at both conjugates. Every printed identity is therefore a NUMERICAL identity there, to
/// roundoff, and a proposed correspondence either satisfies it or does not. Nothing is assumed
/// and nothing is fitted: each line below is an equation from the monograph with the scheme's
/// entries substituted, and it either closes or it does not.</para>
///
/// <para>A residual is reported relative to the larger side, so it can be read as a share.
/// Anything at 1E-12 or below is the identity holding; anything at 1E-2 or above is a different
/// quantity wearing the name.</para>
/// </summary>
public class BuchdahlSymbolDictionaryTests
{
    private readonly ITestOutputHelper _out;
    public BuchdahlSymbolDictionaryTests(ITestOutputHelper output) => _out = output;

    /// <summary>
    /// The spherical fixtures, where the scheme is established and every printed identity must
    /// therefore hold numerically.
    /// </summary>
    private static readonly string[] Spherical =
    {
        "Ladder1_Sphere", "Ladder2_Sphere", "Ladder3_Sphere",
        "CookeTriplet", "KingslakeDG",
    };

    private static BuchdahlTableIRow[] Rows(string name, out int count)
    {
        var catalog = CatalogLocator.LoadBundled();
        var sys = LensFile.Read(Fixtures.Lens(name), catalog);
        var n = IndexResolver.Build(sys, catalog, 0.55, new List<string>());
        double field = 0.0;
        foreach (var f in sys.Fields) if (Math.Abs(f.Y) > Math.Abs(field)) field = f.Y;

        var paraxial = ParaxialTrace.Trace(sys, n, field);
        double objectDistance = sys.Surfaces[0].Thickness;
        double iota = double.IsInfinity(objectDistance) ? 0.0 : -paraxial.Efl / objectDistance;
        int stop = sys.StopSurfaceIndex;
        var scheme = BuchdahlScheme.Compute(sys.Surfaces, n, paraxial.Efl,
                                            sys.Surfaces[stop].SemiDiameter, iota);

        count = sys.Surfaces.Count;
        return BuchdahlTableI.Compute(sys.Surfaces, n, paraxial.Efl, scheme.P, iota: iota);
    }

    /// <summary>One candidate identity: a name, and the two sides to compare.</summary>
    private sealed record Claim(string Name, Func<double[], double> Left,
                                Func<double[], double> Right);

    /// <summary>
    /// Every identity tested, each an equation from the monograph with the scheme's entries put
    /// in for the symbols it names.
    /// </summary>
    private static IEnumerable<Claim> Claims()
    {
        // M (68.6): the full first secondary is the intrinsic one plus 3('A_p a-_p - 'A_q a_p).
        // This is the keystone: it involves the two accumulations (68.8) also names, so if it
        // closes, t15 IS 'A_p and t20 IS 'A_q and neither has to be guessed at again.
        yield return new Claim("(68.6)  s1p = s1p^ + 3('A_p a-_p - 'A_q a_p)",
            t => t[41],
            t => t[38] + 3.0 * (t[15] * t[11] - t[20] * t[10]));

        // The scheme's own construction of the barred first secondary, for reference: whatever
        // t31 is, the barred entry is the lift plus a_p times it.
        yield return new Claim("scheme  s-1p = q s1p + a_p t31",
            t => t[42],
            t => t[6] * t[41] + t[10] * t[31]);

        // M (68.8), spherical part, with 'A_q read as t20 - the reading that was used, and the
        // one (68.6) above either vindicates or destroys.
        yield return new Claim("(68.8)  with 'A_q = t20",
            t => t[42],
            t => t[6] * t[41]
               - (t[20] - t[6] * (t[16] + t[20]) + t[6] * t[6] * t[15]) * t[10]);

        // The same with the standalone term read as t21 instead. This is what the scheme's t31
        // amounts to, so it must close if the line above is the scheme's own arrangement.
        yield return new Claim("(68.8)  with the standalone term = t21",
            t => t[42],
            t => t[6] * t[41]
               - (t[21] - t[6] * (t[16] + t[20]) + t[6] * t[6] * t[15]) * t[10]);

        // M (26.2) and (67.1): a-_p = q a_p, b_p = 2 a-_p, b-_p = q b_p, c-_p = q c_p. These fix
        // which of t11..t14 is which, and the figured increments of (67.1) were read off that
        // identification - so if they do not hold, that reading goes too.
        yield return new Claim("(26.2)  a-_p = q a_p",       t => t[11], t => t[6] * t[10]);
        yield return new Claim("(26.2)  b-_p = 2 q a-_p",    t => t[12], t => 2.0 * t[6] * t[11]);
        yield return new Claim("(26.2)  c-_p = q c_p",       t => t[14], t => t[6] * t[13]);

        // M (84.15): the (I) family is q times the accumulated p quantity less the accumulated
        // q one, member by member. These are definitions inside the scheme, so they close by
        // construction - they are here to say WHICH accumulation each entry is, once the entries
        // above have been identified.
        yield return new Claim("(84.15) A_(I)  = q 'A_p - 'A_q",
            t => t[25], t => t[6] * t[15] - t[20]);
        yield return new Claim("(84.15) A-_(I) = q 'A-_p - 'A-_q",
            t => t[26], t => t[6] * t[16] - t[21]);
        yield return new Claim("(84.15) B_(I)  = q 'B_p - 'B_q,  'B_p = 2 'A-_p",
            t => t[27], t => 2.0 * t[6] * t[16] - t[22]);
    }

    [Fact]
    public void TheDictionary()
    {
        var sb = new StringBuilder();
        sb.AppendLine("identity\tdesign\tsurf\tleft\tright\trelative residual");

        var worst = new Dictionary<string, double>(StringComparer.Ordinal);

        foreach (var claim in Claims())
        {
            worst[claim.Name] = 0.0;
            foreach (string name in Spherical)
            {
                var rows = Rows(name, out int count);
                for (int i = 1; i < count - 1; i++)
                {
                    var t = rows[i].T;

                    // Where the marginal ray meets a surface at zero incidence q is infinite and
                    // the scheme carries the products on the incidences instead; an identity
                    // written with a bare q cannot be evaluated there and says nothing about the
                    // correspondence.
                    if (rows[i].FlatInCollimatedSpace) continue;
                    if (Math.Abs(t[6]) > 1e6) continue;

                    double l = claim.Left(t), r = claim.Right(t);
                    double scale = Math.Max(Math.Abs(l), Math.Abs(r));
                    if (scale < 1e-14) continue;

                    double residual = Math.Abs(l - r) / scale;
                    if (residual > worst[claim.Name]) worst[claim.Name] = residual;

                    sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                        "{0}\t{1}\t{2}\t{3:E6}\t{4:E6}\t{5:E2}",
                        claim.Name, name, i, l, r, residual));
                }
            }
        }

        var summary = new StringBuilder();
        summary.AppendLine();
        summary.AppendLine("WORST RESIDUAL OVER EVERY SPHERICAL SURFACE");
        foreach (var kv in worst)
            summary.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "  {0,-22} {1,-44}", kv.Value.ToString("E2", CultureInfo.InvariantCulture),
                kv.Key));

        string path = Path.Combine(
            Environment.GetEnvironmentVariable("TEMP") ?? ".", "buchdahl-symbols.tsv");
        File.WriteAllText(path, sb.ToString() + summary);
        _out.WriteLine(summary.ToString());

        foreach (var kv in worst)
        {
            // The one that must NOT close - see TheStandaloneTermOf688IsBarred below.
            if (kv.Key.Contains("'A_q = t20", StringComparison.Ordinal)) continue;

            Assert.True(kv.Value < 1e-12,
                $"{kv.Key}: worst residual {kv.Value:E2} over the spherical fixtures. Every "
              + "identity here is an equation from the monograph with the scheme's entries put "
              + "in for the symbols it names, and on a SPHERICAL system the scheme is verified "
              + "against Buchdahl's own printed numbers - so this is either a symbol that means "
              + "something else or an entry that has moved.");
        }
    }

    /// <summary>
    /// <b>M (68.8) is misprinted, and Buchdahl's own Table I says so.</b>
    ///
    /// <para>The equation reads</para>
    /// <code>
    ///   s-_1p = q s_1p - ['A_q - q('A-_p + 'A_q) + q^2 'A_p] a_p + (q~ - q){...}
    /// </code>
    /// <para>and the scheme forms the same barred entry as <c>q s_1p + a_p t31</c>, an identity
    /// that closes to 1.6E-15. So <c>t31</c> is the negative of that bracket. Expanding the
    /// scheme's own definition, <c>t31 = -q t25 + t26</c>, gives</para>
    /// <code>
    ///   -t31 = q^2 t15 - q t16 - q t20 + t21
    /// </code>
    /// <para>which is the printed bracket in every term EXCEPT that the standalone one is
    /// <c>t21</c> where the equation prints <c>'A_q</c>, which is <c>t20</c>.</para>
    ///
    /// <para><b>The identification is not in doubt.</b> M (68.6) - two equations earlier on the
    /// same page, and the one this test suite checks first - reads
    /// <c>s_1p = s_1p^ + 3('A_p a-_p - 'A_q a_p)</c> and closes at 2.3E-16 with
    /// <c>'A_p = t15</c> and <c>'A_q = t20</c>. And t20 and t21 are BOTH in Buchdahl's printed
    /// Table I, at surface 2, as -2.93730 and 0.276263 - different numbers, both reproduced by
    /// this program. So the standalone symbol in (68.8) must be <c>'A-_q</c>, and the overbar
    /// is missing.</para>
    ///
    /// <para>Read at six times magnification the scanned symbol carries no overbar, while the
    /// <c>'A-_p</c> two symbols later carries an unmistakable one - so this is the book's
    /// misprint rather than a faint scan. It is recorded here because it cost a reading of
    /// Sec. 85: a correction was built on the equation as printed, and reported as neutral,
    /// when the bracket it formed was not the one the scheme uses.</para>
    /// </summary>
    [Fact]
    public void TheStandaloneTermOf688IsBarred()
    {
        double widestGap = 0.0;

        foreach (string name in Spherical)
        {
            var rows = Rows(name, out int count);
            for (int i = 1; i < count - 1; i++)
            {
                var t = rows[i].T;
                if (rows[i].FlatInCollimatedSpace || Math.Abs(t[6]) > 1e6) continue;
                if (Math.Abs(t[31]) < 1e-14) continue;

                double q = t[6];
                double barred = t[21] - q * (t[16] + t[20]) + q * q * t[15];
                double printed = t[20] - q * (t[16] + t[20]) + q * q * t[15];

                Assert.True(Math.Abs(barred + t[31]) <= 1e-12 * Math.Abs(t[31]),
                    $"{name} surface {i}: with the standalone term barred, (68.8)'s bracket "
                  + $"should be -t31 = {-t[31]:E8} and is {barred:E8}.");

                double scale = Math.Max(Math.Abs(printed), Math.Abs(barred));
                if (scale > 1e-14)
                    widestGap = Math.Max(widestGap, Math.Abs(printed - barred) / scale);
            }
        }

        // The surfaces where the two readings coincide are the ones with nothing accumulated
        // ahead of them, where t20 and t21 are both zero; they cannot tell the readings apart
        // and no claim is made of them. What must be true is that SOMEWHERE in the set the two
        // differ materially, or the test above would hold for either reading and prove nothing.
        Assert.True(widestGap > 0.1,
            $"the two readings never differ by more than {100 * widestGap:F4} per cent anywhere "
          + "in the spherical fixtures, so nothing here distinguishes them.");
    }
}
