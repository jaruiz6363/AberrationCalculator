using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using System.IO;
using System.Text;
using AberrationCalculator.Core.Aberrations;
using AberrationCalculator.Core.Enums;
using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.Models;
using AberrationCalculator.Core.RayTrace;

namespace AberrationCalculator.Core.Report;

/// <summary>
/// Writes what the program found about a lens, in two forms.
///
/// A human reads the report; a script reads the TSV. Producing only one of the two forces
/// the other reader to parse a layout that was never meant for them - column-aligned text
/// is miserable to parse, and a bare TSV is miserable to read - so both come out of the
/// same numbers in one pass, and cannot disagree.
///
/// Tab-separated rather than comma: glass names and comments contain commas, and every
/// spreadsheet opens TSV without a dialogue about delimiters.
/// </summary>
public sealed class ReportWriter
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private readonly OpticalSystem _sys;
    private readonly GlassCatalog _catalog;
    private readonly string _sourcePath;

    /// <summary>Index after each surface, one array per wavelength, in system order.</summary>
    private readonly List<double[]> _indices = new();

    private readonly List<string> _unresolved = new();
    private readonly int _primary;

    public ReportWriter(OpticalSystem system, GlassCatalog catalog, string sourcePath)
    {
        _sys = system;
        _catalog = catalog;
        _sourcePath = sourcePath;

        _primary = system.PrimaryWavelengthIndex < 0 ? 0 : system.PrimaryWavelengthIndex;
        foreach (var w in system.Wavelengths)
            _indices.Add(IndexResolver.Build(system, catalog, w.Value, _unresolved));
        if (_indices.Count == 0)
            _indices.Add(IndexResolver.Build(system, catalog, 0.5875618, _unresolved));
    }

    /// <summary>Materials that no catalog could resolve. Non-empty means the numbers are wrong.</summary>
    public IReadOnlyList<string> Unresolved
    {
        get
        {
            var distinct = new List<string>();
            foreach (var u in _unresolved)
                if (!distinct.Contains(u, StringComparer.OrdinalIgnoreCase)) distinct.Add(u);
            return distinct;
        }
    }

    private double[] PrimaryIndices => _indices[Math.Min(_primary, _indices.Count - 1)];

    /// <summary>
    /// Indices at the shortest and longest wavelengths the system defines - what the
    /// chromatic Seidel terms difference. A monochromatic system has no dispersion to
    /// report, and both come back as the primary set, which correctly gives CL = CT = 0.
    /// </summary>
    private (double[] Short, double[] Long) SpectralExtremes()
    {
        if (_sys.Wavelengths.Count < 2) return (PrimaryIndices, PrimaryIndices);
        int lo = 0, hi = 0;
        for (int i = 1; i < _sys.Wavelengths.Count; i++)
        {
            if (_sys.Wavelengths[i].Value < _sys.Wavelengths[lo].Value) lo = i;
            if (_sys.Wavelengths[i].Value > _sys.Wavelengths[hi].Value) hi = i;
        }
        return (_indices[lo], _indices[hi]);
    }

    /// <summary>
    /// The coefficient set, with the tertiary terms attached. Every route to one goes
    /// through here: tau2..tau20 are assembled separately from the fifth-order code, and
    /// before this existed the reported set carried tau1 and nineteen zeros.
    /// </summary>
    /// <param name="indices">
    /// Indices the trace was made with. Defaults to the primary wavelength; the per-wavelength
    /// callers MUST pass their own, or the tertiary terms would come from a different colour
    /// than the rest of the set.
    /// </param>
    private BuchdahlResult Buchdahl(ParaxialResult p, System.Collections.Generic.List<int>? ignoredR2 = null,
                                    double[]? indices = null)
    {
        var b = BuchdahlCoefficients.Compute(_sys, p, ignoredR2);
        // tau2..tau20 are not produced by the fifth-order code and have to be assembled
        // separately. Every route to a coefficient set goes through here so that none of
        // them can report the nineteen zeros this used to.
        TertiaryCoefficients.Attach(_sys, indices ?? PrimaryIndices, p, b, MaxField());
        return b;
    }

    private SeidelResult Seidel(ParaxialResult p)
    {
        var (nShort, nLong) = SpectralExtremes();
        return SeidelCoefficients.Compute(_sys, PrimaryIndices, nShort, nLong, p);
    }

    private double MaxField()
    {
        double f = 0.0;
        foreach (var x in _sys.Fields) if (Math.Abs(x.Y) > Math.Abs(f)) f = x.Y;
        return f;
    }

    // ── Human-readable report ────────────────────────────────────────────────────────

    public string BuildReport()
    {
        var sb = new StringBuilder();
        var trace = ParaxialTrace.Trace(_sys, PrimaryIndices, MaxField());

        Header(sb);
        Prescription(sb);
        Materials(sb);
        Aspherics(sb);
        FirstOrder(sb, trace);
        SeidelSection(sb, trace);
        BuchdahlSection(sb, trace);
        PrmsSection(sb);
        ContributionSection(sb);
        SurfaceContributionSection(sb);
        Warnings(sb, trace);
        return sb.ToString();
    }

    private void Header(StringBuilder sb)
    {
        string title = string.IsNullOrWhiteSpace(_sys.Title)
            ? Path.GetFileNameWithoutExtension(_sourcePath)
            : _sys.Title;
        sb.AppendLine("================================================================");
        sb.AppendLine(title);
        sb.AppendLine("================================================================");
        sb.AppendLine($"File      : {_sourcePath}");
        sb.AppendLine($"Format    : {Path.GetExtension(_sourcePath).TrimStart('.').ToUpperInvariant()}");
        sb.AppendLine($"Surfaces  : {_sys.Surfaces.Count}   Wavelengths: {_sys.Wavelengths.Count}   Fields: {_sys.Fields.Count}");
        sb.AppendLine($"Aperture  : {_sys.Aperture.Type} = {Num(_sys.Aperture.Value)}");
        sb.AppendLine($"Field type: {_sys.FieldType}");
        sb.AppendLine();
    }

    private void Prescription(StringBuilder sb)
    {
        var n = PrimaryIndices;
        sb.AppendLine("PRESCRIPTION");
        sb.AppendLine("----------------------------------------------------------------");
        sb.AppendLine(string.Format(Inv, "{0,-5} {1,-12} {2,14} {3,12} {4,-12} {5,10} {6,12} {7,10}  {8}",
            "Surf", "Type", "Radius", "Thickness", "Material", "Index", "SemiDiam", "Conic", "Note"));
        for (int i = 0; i < _sys.Surfaces.Count; i++)
        {
            var s = _sys.Surfaces[i];
            string label = i == 0 ? "OBJ" : i == _sys.Surfaces.Count - 1 ? "IMG" : i.ToString(Inv);
            sb.AppendLine(string.Format(Inv, "{0,-5} {1,-12} {2,14} {3,12} {4,-12} {5,10} {6,12} {7,10}  {8}",
                label, Kind(s), Num(s.Radius), Num(s.Thickness), MaterialName(s),
                i < n.Length && Math.Abs(n[i] - 1.0) > 1e-9 ? n[i].ToString("0.000000", Inv) : "",
                s.SemiDiameter > 0 ? Num(s.SemiDiameter) : "",
                Math.Abs(s.Conic) > 1e-15 ? Num(s.Conic) : "",
                Note(s)));
        }
        sb.AppendLine();
    }

    private void Materials(StringBuilder sb)
    {
        sb.AppendLine("REFRACTIVE INDICES");
        sb.AppendLine("----------------------------------------------------------------");
        var head = new StringBuilder(string.Format(Inv, "{0,-16} {1,-14} {2,8}", "Material", "Source", "Vd"));
        foreach (var w in _sys.Wavelengths)
            head.Append(string.Format(Inv, " {0,12}", w.Value.ToString("0.####", Inv) + (w.IsPrimary ? "*" : "")));
        sb.AppendLine(head.ToString());

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < _sys.Surfaces.Count; i++)
        {
            var s = _sys.Surfaces[i];
            string name = MaterialName(s);
            if (string.IsNullOrEmpty(name) || !seen.Add(name)) continue;

            var row = new StringBuilder(string.Format(Inv, "{0,-16} {1,-14} {2,8}", name, Source(s), Vd(s)));
            foreach (var perWave in _indices)
                row.Append(string.Format(Inv, " {0,12}", i < perWave.Length ? perWave[i].ToString("0.000000", Inv) : ""));
            sb.AppendLine(row.ToString());
        }
        sb.AppendLine();
    }

    private void Aspherics(StringBuilder sb)
    {
        var rows = new List<(int Surface, Surface S)>();
        for (int i = 0; i < _sys.Surfaces.Count; i++)
        {
            var s = _sys.Surfaces[i];
            bool any = Math.Abs(s.Conic) > 1e-15;
            foreach (double c in s.AsphericCoefficients) if (c != 0.0) any = true;
            if (any) rows.Add((i, s));
        }
        if (rows.Count == 0) return;

        sb.AppendLine("ASPHERIC SURFACES");
        sb.AppendLine("----------------------------------------------------------------");
        foreach (var (i, s) in rows)
        {
            sb.AppendLine(string.Format(Inv, "Surface {0}   conic = {1}", i, s.Conic.ToString("0.########", Inv)));
            for (int k = 0; k < s.AsphericCoefficients.Length; k++)
            {
                double c = s.AsphericCoefficients[k];
                if (c == 0.0) continue;
                sb.AppendLine(string.Format(Inv, "    r^{0,-3} {1}", 2 * k + 2, c.ToString("0.000000000E+00", Inv)));
            }
        }
        sb.AppendLine();
    }

    private void FirstOrder(StringBuilder sb, ParaxialResult p)
    {
        sb.AppendLine("FIRST ORDER");
        sb.AppendLine("----------------------------------------------------------------");
        foreach (var (name, value) in FirstOrderRows(p))
            sb.AppendLine(string.Format(Inv, "{0,-38} {1}", name, value));
        sb.AppendLine();
    }

    private List<(string Name, string Value)> FirstOrderRows(ParaxialResult p)
    {
        double field = MaxField();
        var rows = new List<(string, string)>
        {
            ("Effective focal length",              Num(p.Efl)),
            ("Back focal length",                   Num(p.Bfl)),
            ("F/number",                            double.IsInfinity(p.FNumber) ? "-" : Num(p.FNumber)),
            ("Entrance pupil diameter",             Num(p.Epd)),
            ("Entrance pupil position (from surf 1)", Num(p.EntrancePupilPosition)),
            ("Exit pupil diameter",                 Num(p.ExitPupilDiameter)),
            ("Exit pupil position (from image)",    Num(p.ExitPupilPosition)),
            ("Field",                               _sys.FieldType == FieldType.ObjectAngle
                                                        ? Num(field) + " deg" : Num(field)),
            ("Image height at image surface",       Num(p.ImageHeight)),
            ("Paraxial focus (from last surface)",  Num(p.ParaxialFocusDistance)),
            ("Image height at paraxial focus",      Num(p.ParaxialImageHeight)),
            ("Magnification",                       p.InfiniteConjugate ? "-" : Num(p.Magnification)),
            ("Conjugate",                           p.InfiniteConjugate ? "infinite" : "finite"),
            ("Lagrange invariant",                  Num(p.LagrangeInvariant)),
            ("Invariant drift",                     p.InvariantDrift.ToString("0.0E+00", Inv)),
        };
        return rows;
    }

    private void SeidelSection(StringBuilder sb, ParaxialResult p)
    {
        var s = Seidel(p);
        sb.AppendLine("SEIDEL (THIRD ORDER) COEFFICIENTS");
        sb.AppendLine("----------------------------------------------------------------");
        sb.AppendLine(string.Format(Inv, "{0,-5} {1,13} {2,13} {3,13} {4,13} {5,13} {6,13} {7,13}",
            "Surf", "S1 sph", "S2 coma", "S3 astig", "S4 Petzval", "S5 distort", "CL axial", "CT lateral"));
        for (int i = 1; i < _sys.Surfaces.Count - 1; i++)
        {
            if (Math.Abs(s.S1[i]) + Math.Abs(s.S4[i]) + Math.Abs(s.CL[i]) < 1e-14) continue;
            sb.AppendLine(string.Format(Inv, "{0,-5} {1,13} {2,13} {3,13} {4,13} {5,13} {6,13} {7,13}",
                i, Sci(s.S1[i]), Sci(s.S2[i]), Sci(s.S3[i]), Sci(s.S4[i]), Sci(s.S5[i]), Sci(s.CL[i]), Sci(s.CT[i])));
        }
        sb.AppendLine(new string('-', 64));
        sb.AppendLine(string.Format(Inv, "{0,-5} {1,13} {2,13} {3,13} {4,13} {5,13} {6,13} {7,13}",
            "TOTAL", Sci(s.TotalS1), Sci(s.TotalS2), Sci(s.TotalS3), Sci(s.TotalS4),
            Sci(s.TotalS5), Sci(s.TotalCL), Sci(s.TotalCT)));
        if (s.DistortionSuppressedAt.Length > 0)
            sb.AppendLine("  note: distortion suppressed at surface(s) "
                + string.Join(", ", s.DistortionSuppressedAt)
                + " - the marginal ray meets them at normal incidence.");
        sb.AppendLine();
    }

    private void BuchdahlSection(StringBuilder sb, ParaxialResult p)
    {
        var b = Buchdahl(p);

        sb.AppendLine("BUCHDAHL / RIMMER COEFFICIENTS (THIRD, FIFTH AND SEVENTH ORDER)");
        sb.AppendLine("----------------------------------------------------------------");
        sb.AppendLine("Transverse coefficients. Per-surface values are the INTRINSIC parts and are");
        sb.AppendLine("unscaled; the totals include the aspheric and induced contributions and carry");
        sb.AppendLine(string.Format(Inv, "the F/number ({0:0.####}), so they are not the column sums.", b.FNumber));
        sb.AppendLine();

        string[] order = BuchdahlTerms.Names;
        var head = new StringBuilder(string.Format(Inv, "{0,-6}", "Surf"));
        foreach (var nme in order) head.Append(string.Format(Inv, " {0,13}", nme));
        sb.AppendLine(head.ToString());

        for (int i = 1; i < _sys.Surfaces.Count - 1; i++)
        {
            var t = b.Intrinsic[i];
            bool any = false;
            foreach (var nme in order) if (Math.Abs(t[nme]) > 1e-15) any = true;
            if (!any) continue;
            var row = new StringBuilder(string.Format(Inv, "{0,-6}", i));
            foreach (var nme in order) row.Append(string.Format(Inv, " {0,13}", Sci(t[nme])));
            sb.AppendLine(row.ToString());

            if (b.Aspheric[i] != null)
            {
                var a = b.Aspheric[i]!;
                var arow = new StringBuilder(string.Format(Inv, "{0,-6}", "  asph"));
                foreach (var nme in order) arow.Append(string.Format(Inv, " {0,13}", Sci(a[nme])));
                sb.AppendLine(arow.ToString());
            }
        }

        sb.AppendLine(new string('-', 64));
        var tot = new StringBuilder(string.Format(Inv, "{0,-6}", "TOTAL"));
        foreach (var nme in order) tot.Append(string.Format(Inv, " {0,13}", Sci(b.Totals[nme])));
        sb.AppendLine(tot.ToString());

        // No r-squared warning: such a term is a curvature change, folded into the vertex
        // curvature by Surface.VertexForm, so the coefficients above account for it.
        sb.AppendLine();
    }

    /// <summary>
    /// Coefficients and field height for every (wavelength, field) the system defines,
    /// with the weight that combination carries in the composite.
    /// </summary>
    private List<(int Wave, int Field, double H, double Weight, BuchdahlTerms Totals)> PrmsCases()
    {
        var list = new List<(int, int, double, double, BuchdahlTerms)>();
        double maxField = 0.0;
        foreach (var f in _sys.Fields) if (Math.Abs(f.Y) > Math.Abs(maxField)) maxField = f.Y;

        for (int wi = 0; wi < Math.Max(1, _sys.Wavelengths.Count); wi++)
        {
            var n = _indices[Math.Min(wi, _indices.Count - 1)];
            var trace = ParaxialTrace.Trace(_sys, n, maxField);
            var totals = Buchdahl(trace, null, n).Totals;
            double ww = wi < _sys.Wavelengths.Count ? _sys.Wavelengths[wi].Weight : 1.0;

            for (int fi = 0; fi < Math.Max(1, _sys.Fields.Count); fi++)
            {
                double fy = fi < _sys.Fields.Count ? _sys.Fields[fi].Y : 0.0;
                double fw = fi < _sys.Fields.Count ? _sys.Fields[fi].Weight : 1.0;
                double h = Math.Abs(maxField) > 1e-15 ? fy / maxField : 0.0;
                list.Add((wi, fi, h, ww * fw, totals));
            }
        }
        return list;
    }

    private void PrmsSection(StringBuilder sb)
    {
        var cases = PrmsCases();
        sb.AppendLine("PREDICTED RMS SPOT RADIUS (PRMS)");
        sb.AppendLine("----------------------------------------------------------------");
        sb.AppendLine("The RMS spot radius implied by the coefficients above, integrated analytically");
        sb.AppendLine("over the pupil with no rays traced. Method: Robb, J. Opt. Soc. Am. 66, 1037 (1976).");
        sb.AppendLine();
        sb.AppendLine("In LENS UNITS, referenced to the PARAXIAL image plane. It cannot see defocus, and");
        sb.AppendLine("it takes no account of any surface's aperture: a beam clipped on axis or off it");
        sb.AppendLine("reads the same as one that passes unobstructed.");
        sb.AppendLine();
        sb.AppendLine(string.Format(Inv, "{0,-12} {1,-10} {2,10} {3,10} {4,16}",
            "Wavelength", "Field", "Hy", "Weight", "PRMS"));
        foreach (var c in cases)
        {
            string wl = c.Wave < _sys.Wavelengths.Count
                ? _sys.Wavelengths[c.Wave].Value.ToString("0.####", Inv)
                  + (_sys.Wavelengths[c.Wave].IsPrimary ? "*" : "")
                : "-";
            string fld = c.Field < _sys.Fields.Count
                ? _sys.Fields[c.Field].Y.ToString("0.####", Inv) : "-";
            sb.AppendLine(string.Format(Inv, "{0,-12} {1,-10} {2,10:0.####} {3,10:0.###} {4,16}",
                wl, fld, c.H, c.Weight, Num6(Prms.Value(c.Totals, c.H))));
        }
        sb.AppendLine(new string('-', 64));
        double composite = Prms.Composite(cases.Select(c => (c.Totals, c.H, c.Weight)));
        sb.AppendLine(string.Format(Inv, "{0,-45}{1,16}", "PRMSA (weighted over all of the above)", Num6(composite)));
        sb.AppendLine();
        sb.AppendLine("ACCURACY FALLS OFF WITH FIELD, NOT WITH APERTURE. The coefficient set is complete");
        sb.AppendLine("through fifth order, but seventh order is represented by spherical aberration (B7)");
        sb.AppendLine("alone - there is no seventh-order coma, astigmatism or field curvature to carry.");
        sb.AppendLine("So on axis nothing is missing and PRMS is at its best, while at the edge of the");
        sb.AppendLine("field the absent terms are at full strength and nothing represents them. Trust the");
        sb.AppendLine("small-Hy rows above furthest, and treat the Hy = 1 row as the weakest number here.");
        sb.AppendLine("How far out it stays usable depends on how much seventh-order field aberration the");
        sb.AppendLine("design actually carries, so check against a ray trace before relying on full field.");
        sb.AppendLine();
    }

    private void ContributionSection(StringBuilder sb)
    {
        var cases = PrmsCases();
        if (cases.Count == 0) return;
        var primary = cases.FirstOrDefault(c => c.Wave == Math.Min(_primary, Math.Max(0, _sys.Wavelengths.Count - 1)));
        var totals = primary.Totals ?? cases[0].Totals;
        const double h = 1.0;

        var rows = ContributionAnalysis.Compute(totals, h);
        double spot = Prms.Value(totals, h);
        double balancing = ContributionAnalysis.BalancingRatio(totals, h);

        sb.AppendLine("WHICH ABERRATION IS COSTING YOU THE SPOT (full field, primary wavelength)");
        sb.AppendLine("----------------------------------------------------------------");
        sb.AppendLine("Two different questions, two different numbers.");
        sb.AppendLine();
        sb.AppendLine("  ISOLATED  the RMS spot this aberration would produce with every other");
        sb.AppendLine("            coefficient set to zero. Re-normalised, so the values ARE");
        sb.AppendLine("            comparable between aberrations - which the raw coefficients are");
        sb.AppendLine("            not. They do not sum to the spot the design actually has.");
        sb.AppendLine("            Method: Rosete-Aguilar and Rayces, Proc. SPIE 2730, 499 (1996).");
        sb.AppendLine();
        sb.AppendLine("  SHARE %   what this aberration contributes to the spot the design DOES");
        sb.AppendLine("            have, cross terms split evenly with their partner. Sums to 100%.");
        sb.AppendLine("            A NEGATIVE share means it is cancelling other aberrations:");
        sb.AppendLine("            remove it and the spot gets WORSE.");
        sb.AppendLine();
        sb.AppendLine(string.Format(Inv, "{0,-6} {1,16} {2,13} {3,10}",
            "Aber", "coefficient", "isolated", "share %") + "  aberration");

        foreach (var r in rows.OrderByDescending(x => Math.Abs(x.Percent)))
        {
            if (Math.Abs(r.Coefficient) < 1e-15 && Math.Abs(r.Percent) < 1e-9) continue;
            sb.AppendLine(string.Format(Inv, "{0,-6} {1,16} {2,13} {3,10:0.0}  {4}",
                r.Name, Sci(r.Coefficient), Num6(r.Isolated), r.Percent,
                AberrationNames.Describe(r.Name)));
        }
        sb.AppendLine(new string('-', 78));
        sb.AppendLine(string.Format(Inv, "{0,-6} {1,16} {2,13} {3,10:0.0}",
            "TOTAL", "", Num6(spot), rows.Sum(x => x.Percent)));

        sb.AppendLine();
        sb.AppendLine("  Names after R. B. Johnson, Appl. Opt. 12, 2079 (1973), Table I. Where a name");
        sb.AppendLine("  reads \"(with ...)\" the named aberration is the COMBINATION of those");
        sb.AppendLine("  coefficients, not this one on its own.");
        sb.AppendLine();
        if (balancing < 0.75)
            sb.AppendLine(string.Format(Inv,
                "  Aberrations are BALANCING here: the spot is {0:0.###}x the mean square it would",
                balancing)
                + Environment.NewLine
                + "  have if each acted alone. Attacking the largest isolated value on its own will"
                + Environment.NewLine
                + "  make the design worse until its partner is corrected too.");
        else
            sb.AppendLine(string.Format(Inv,
                "  Little cancellation here ({0:0.###}x): the aberrations are largely independent,",
                balancing)
                + Environment.NewLine
                + "  so the largest share is the one worth attacking first.");
        sb.AppendLine();
    }

    private void SurfaceContributionSection(StringBuilder sb)
    {
        var trace = ParaxialTrace.Trace(_sys, PrimaryIndices, MaxField());
        var b = Buchdahl(trace);
        var rows = ContributionAnalysis.BySurface(b, 1.0);
        if (rows.Count == 0) return;

        sb.AppendLine("WHICH SURFACE IS COSTING YOU THE SPOT (full field, primary wavelength)");
        sb.AppendLine("----------------------------------------------------------------");
        sb.AppendLine("Share sums to 100%. A negative share is a surface that is CANCELLING the");
        sb.AppendLine("others - which is what a corrector element is for.");
        sb.AppendLine();
        sb.AppendLine("INDUCED tells you where the fix is. A surface generates aberration of its own,");
        sb.AppendLine("and it also generates more by acting on the aberration handed to it by the");
        sb.AppendLine("surfaces ahead. A high induced fraction means this surface is largely reacting");
        sb.AppendLine("to an upstream problem, and correcting it here will not hold.");
        sb.AppendLine();
        sb.AppendLine(string.Format(Inv, "{0,-6} {1,12} {2,12}   {3}",
            "Surf", "share %", "induced", "higher-order coefficients"));
        foreach (var r in rows.OrderByDescending(x => Math.Abs(x.Percent)))
        {
            string bar = r.InducedFraction >= 0.999 ? "all induced"
                       : r.InducedFraction <= 0.001 ? "all its own"
                       : new string('#', (int)Math.Round(r.InducedFraction * 20)).PadRight(20, '.');
            sb.AppendLine(string.Format(Inv, "{0,-6} {1,12:0.0} {2,12:0.0%}   {3}",
                r.Surface, r.Percent, r.InducedFraction, bar));
        }
        sb.AppendLine(new string('-', 64));
        sb.AppendLine(string.Format(Inv, "{0,-6} {1,12:0.0}", "TOTAL", rows.Sum(x => x.Percent)));

        // Individual surfaces routinely contribute far more than the finished design shows,
        // because a corrected lens works by cancellation. Percentages in the hundreds are
        // the normal consequence and mean something specific, so say what.
        double gross = rows.Sum(x => Math.Abs(x.Percent));
        if (gross > 250.0)
        {
            sb.AppendLine();
            sb.AppendLine(string.Format(Inv,
                "  The surfaces cancel heavily: their contributions total {0:0}% in magnitude to",
                gross)
                + Environment.NewLine
                + string.Format(Inv,
                "  leave 100%. Individually they are around {0:0.#}x the finished spot, so a small",
                gross / 200.0)
                + Environment.NewLine
                + "  change to any one of them moves the result far more than its share suggests."
                + Environment.NewLine
                + "  That sensitivity is the design working as intended, not a fault.");
        }
        sb.AppendLine();
    }

    /// <summary>Per surface, per part, every coefficient - the full decomposition.</summary>
    public string BuildSurfaceBreakdownTsv()
    {
        var trace = ParaxialTrace.Trace(_sys, PrimaryIndices, MaxField());
        var b = Buchdahl(trace);
        var sb = new StringBuilder();

        sb.Append("surface	part");
        foreach (var nme in BuchdahlTerms.Names) sb.Append("	" + nme.ToLowerInvariant());
        sb.AppendLine();

        void Row(string surf, string part, BuchdahlTerms t)
        {
            sb.Append(surf + "	" + part);
            foreach (var nme in BuchdahlTerms.Names) sb.Append("	" + Raw(t[nme]));
            sb.AppendLine();
        }

        for (int i = 1; i < _sys.Surfaces.Count - 1; i++)
        {
            Row(i.ToString(Inv), "intrinsic", b.Intrinsic[i]);
            if (b.Aspheric[i] != null) Row(i.ToString(Inv), "aspheric", b.Aspheric[i]!);
            Row(i.ToString(Inv), "induced", b.Induced[i]);
            Row(i.ToString(Inv), "surface_total", b.PerSurface[i]);
        }
        Row("TOTAL", "transverse", b.Totals);

        sb.AppendLine();
        sb.AppendLine("# intrinsic + aspheric + induced = surface_total, per surface, unscaled.");
        sb.AppendLine("# Summing surface_total over the surfaces and multiplying by the F/number");
        sb.AppendLine(string.Format(Inv, "# ({0:R}) reproduces the transverse totals exactly.", b.FNumber));
        return sb.ToString();
    }

    /// <summary>Per-surface share of the spot, with the induced fraction.</summary>
    public string BuildSurfaceShareTsv()
    {
        var sb = new StringBuilder();
        sb.AppendLine("wavelength	hy	surface	share_of_mean_square	percent	induced_fraction");
        foreach (var c in PrmsCases())
        {
            var n = _indices[Math.Min(c.Wave, _indices.Count - 1)];
            var trace = ParaxialTrace.Trace(_sys, n, MaxField());
            var b = Buchdahl(trace, null, n);
            string wl = c.Wave < _sys.Wavelengths.Count ? _sys.Wavelengths[c.Wave].Value.ToString("R", Inv) : "";
            foreach (var r in ContributionAnalysis.BySurface(b, c.H))
                sb.AppendLine(string.Join("	", wl, Raw(c.H), r.Surface.ToString(Inv),
                    Raw(r.Share), Raw(r.Percent), Raw(r.InducedFraction)));
        }
        return sb.ToString();
    }

    public string BuildContributionTsv()
    {
        var cases = PrmsCases();
        var sb = new StringBuilder();
        sb.AppendLine("wavelength	hy	aberration	order	name	coefficient	isolated_rms	share_of_mean_square	percent");
        foreach (var c in cases)
        {
            string wl = c.Wave < _sys.Wavelengths.Count ? _sys.Wavelengths[c.Wave].Value.ToString("R", Inv) : "";
            foreach (var r in ContributionAnalysis.Compute(c.Totals, c.H))
                sb.AppendLine(string.Join("	", wl, Raw(c.H), r.Name,
                    AberrationNames.Order(r.Name).ToString(Inv), AberrationNames.Describe(r.Name),
                    Raw(r.Coefficient), Raw(r.Isolated), Raw(r.Share), Raw(r.Percent)));
        }
        return sb.ToString();
    }

    /// <summary>
    /// Whether this design would test the aspheric tertiary path - see
    /// <see cref="AsphericDiagnostic"/>. Readable text rather than a table: it is a verdict
    /// with reasons, and the reasons are the whole point of it.
    /// </summary>
    public string BuildAsphericScreenText(double h = 1.0)
        => AsphericDiagnostic.Screen(_sys, PrimaryIndices, MaxField(), h).ToString();

    /// <summary>
    /// Distortion predicted from the coefficients against distortion traced, at each of a
    /// ladder of field fractions and at three truncations of the same set. See
    /// <see cref="DistortionPrediction"/> for what is predicted and what is traced.
    ///
    /// <para>Readable text rather than a table, and separate from the report, for the reason
    /// the aspheric screen and the Forbes breakdown are: it traces rays, which nothing else
    /// the report prints does, and it is a measurement of the prediction rather than a
    /// prediction.</para>
    ///
    /// <para>Both mappings are columns of one table rather than two tables with an essay
    /// between them. What the reader is owed is the definition of each column and the numbers;
    /// which mapping their design is specified against, and whether an error of a given size
    /// matters to them, are theirs to know and not this program's to pronounce on.</para>
    /// </summary>
    public string BuildDistortionText()
    {
        double field = MaxField();
        var sb = new StringBuilder();

        sb.AppendLine("DISTORTION FROM THE ABERRATION COEFFICIENTS, AGAINST TRACED RAYS");
        sb.AppendLine("--------------------------------------------------------------------------------");
        sb.AppendLine("Predicted: Robb's polynomial at zero pupil radius, eps_y = E h^3 + E5 h^5 + tau20 h^7.");
        sb.AppendLine("Traced:    the ray through the centre of the paraxial entrance pupil, at paraxial focus.");
        sb.AppendLine("h:         fractional field in the tangent sense, tan(theta) = h tan(theta_max).");
        sb.AppendLine("Ideal:     f tan(theta) for F-tan(th), f theta for F-theta.");
        sb.AppendLine();

        if (Math.Abs(field) < 1e-15)
        {
            sb.AppendLine("This design has no off-axis field, so it has no distortion to measure.");
            return sb.ToString();
        }

        var p = ParaxialTrace.Trace(_sys, PrimaryIndices, field);
        var totals = TertiaryForDistortion(p, field, out string route);
        var rows = DistortionPrediction.Compare(_sys, PrimaryIndices, p, totals, field);

        sb.AppendLine(route);
        sb.AppendLine();
        sb.AppendLine(string.Format(Inv, "  E     {0,12}   3rd order distortion", Sci(totals.E)));
        sb.AppendLine(string.Format(Inv, "  E5    {0,12}   5th", Sci(totals.E5)));
        sb.AppendLine(string.Format(Inv, "  tau20 {0,12}   7th", Sci(totals.Tau20)));
        sb.AppendLine();

        sb.AppendLine("                  traced, per cent      predicted F-tan(th), per cent"
                    + "      error, % of traced");
        sb.AppendLine(string.Format(Inv, Layout, "H", "field", "F-tan(th)", "F-theta",
                                    "3rd", "3rd+5th", "full 7th", "3rd", "3rd+5th", "full 7th"));
        sb.AppendLine("  " + new string('-', 100));

        foreach (var r in rows)
        {
            if (!r.Ok)
            {
                sb.AppendLine(string.Format(Inv, "  {0,4:F2} {1,8:F3}   the chief ray does not get "
                                               + "through at this field", r.H, r.Field));
                continue;
            }
            sb.AppendLine(string.Format(Inv, Layout,
                r.H.ToString("F2", Inv), r.Field.ToString("F3", Inv),
                Pct(r.TracedPercent), Pct(r.TracedPercentFTheta),
                Pct(r.Percent(r.Third)), Pct(r.Percent(r.Fifth)), Pct(r.Percent(r.Seventh)),
                Err(r.RelativeError(3)), Err(r.RelativeError(5)), Err(r.RelativeError(7))));
        }
        sb.AppendLine();

        ImagePlaneNote(sb, p, totals, field, rows);

        sb.AppendLine("  COEFFICIENTS READ BACK OUT OF THE RAYS");
        sb.AppendLine();
        sb.AppendLine(string.Format(Inv, RecoveryLayout, "", "used", "from rays", "  ratio"));
        foreach (var r in DistortionPrediction.Recover(_sys, PrimaryIndices, p, totals, field))
        {
            sb.AppendLine(string.Format(Inv, RecoveryLayout,
                r.Name, Sci(r.Reported), Sci(r.FromRays),
                r.Reliable ? RatioWithUncertainty(r.Ratio, r.Spread) : "   not resolved"));
        }
        sb.AppendLine();
        sb.AppendLine("  The +/- is what the ray measurement is worth, so the ratio means nothing beyond");
        sb.AppendLine("  it. \"not resolved\" means the term is too small for the ray trace to measure at");
        sb.AppendLine("  these fields, not that it disagrees.");
        sb.AppendLine();
        return sb.ToString();
    }

    /// <summary>
    /// The coefficient set to predict distortion from, with the seventh-order term taken by
    /// whichever route is trustworthy for this design, and a line saying which and why.
    ///
    /// <para><b>Figured: Forbes.</b> The Buchdahl scheme needs an aspheric tertiary
    /// arrangement Buchdahl never published, so this program's is a reconstruction, and real
    /// rays say it is wrong - by a factor of two on some designs. There is no reason to show a
    /// designer a number that is known to be wrong, or to make them choose.</para>
    ///
    /// <para><b>Unfigured: either.</b> The two routes agree to roundoff on all twenty tau at
    /// both conjugates, so the choice is empty and the scheme's own value is kept, which
    /// leaves everything validated at infinite conjugate bit-identical.</para>
    ///
    /// <para><b>E and E5 always come from the scheme</b>, at both conjugates and figured or
    /// not. The Forbes inversion produces the tertiary only, and it is not needed: the
    /// aspheric third and fifth orders are established by closed-form conic surfaces, which
    /// is a printed answer rather than a reconstruction. See <c>docs/verification.md</c>.</para>
    /// </summary>
    private BuchdahlTerms TertiaryForDistortion(ParaxialResult p, double field, out string route)
    {
        var totals = Buchdahl(p).Totals;

        bool figured = false;
        for (int i = 1; i <= _sys.LastOpticalSurface(); i++)
            if (_sys.Surfaces[i].IsFigured) { figured = true; break; }

        if (!figured)
        {
            route = "Every surface is spherical: tau20 is Buchdahl's scheme's, and the Forbes series\n"
                  + "trace agrees with it to roundoff.";
            return totals;
        }

        var forbes = Forbes.ForbesCoefficients.Invert(_sys, PrimaryIndices, p, field);
        if (forbes == null)
        {
            route = "This design is FIGURED, so tau20 should be the Forbes series trace's - but the\n"
                  + "trace does not close on it, and the scheme's value is used instead. Its aspheric\n"
                  + "arrangement is a reconstruction that rays reject on some designs, so the\n"
                  + "seventh-order column is unreliable here.";
            return totals;
        }

        var used = totals.Clone();
        used.Tau20 = forbes.Tau[20];
        route = "This design is FIGURED: tau20 is the FORBES series trace's. E and E5 are Buchdahl's\n"
              + "scheme's, as they are at every design. See docs/distortion-prediction.md.";
        return used;
    }

    /// <summary>
    /// What the same lens reads at the image surface the FILE defines, when that is not the
    /// paraxial image plane.
    ///
    /// <para>This is the number a design program prints, and it differs: shifting the plane
    /// scales both the real and the ideal height, and not by quite the same factor. On the
    /// Cooke triplet in this repository the file sits 0.207 lens units inside paraxial focus
    /// and the full-field distortion reads 0.0620 per cent there against 0.0486 at paraxial
    /// focus - a difference of a quarter, entirely from the plane.</para>
    ///
    /// <para>The table cannot simply be moved there. The coefficients are referred to the
    /// paraxial image plane and Robb's polynomial has no defocus term, so a prediction quoted
    /// at any other plane would be comparing two different things. So the comparison stays at
    /// paraxial focus and the design program's figure is given beside it, with the reason.</para>
    /// </summary>
    private void ImagePlaneNote(StringBuilder sb, ParaxialResult p, BuchdahlTerms totals,
                                double field, IReadOnlyList<DistortionPrediction.Row> rows)
    {
        int last = _sys.LastOpticalSurface();
        double lastToImage = 0.0;
        for (int i = last; i < _sys.Surfaces.Count - 1; i++)
        {
            double t = _sys.Surfaces[i].Thickness;
            if (!double.IsInfinity(t) && !double.IsNaN(t)) lastToImage += t;
        }
        double offset = p.ParaxialFocusDistance - lastToImage;
        if (Math.Abs(offset) < 1e-9 || rows.Count == 0) return;

        var atFile = DistortionPrediction.Compare(_sys, PrimaryIndices, p, totals, field,
                                                  new[] { rows[rows.Count - 1].H },
                                                  atParaxialFocus: false);
        if (atFile.Count == 0 || !atFile[0].Ok) return;

        // Only worth saying when the two planes give materially different figures. A design
        // saved AT paraxial focus still lands a fraction of a micron away through rounding,
        // and a paragraph about a tenth of a per cent of a distortion figure would be noise.
        double here = rows[rows.Count - 1].TracedPercent;
        if (Math.Abs(here) < 1e-12) return;
        if (Math.Abs(atFile[0].TracedPercent / here - 1.0) < 0.01) return;

        sb.AppendLine(string.Format(Inv,
            "  The file's image surface is {0:0.0000} lens units {1} paraxial focus. Measured there,",
            Math.Abs(offset), offset > 0 ? "inside" : "beyond"));
        sb.AppendLine(string.Format(Inv,
            "  traced F-tan(th) at H = {0:F2} is {1:F4} % rather than {2:F4} %. The table is at paraxial",
            atFile[0].H, atFile[0].TracedPercent, rows[rows.Count - 1].TracedPercent));
        sb.AppendLine("  focus, where the coefficients are referred and where the polynomial can be");
        sb.AppendLine("  compared with rays at all.");
        sb.AppendLine();
    }

    /// <summary>Column widths of the distortion table, shared by its header and its rows.</summary>
    private const string Layout =
        "  {0,4} {1,8} {2,10} {3,10}   {4,10} {5,10} {6,10}   {7,8} {8,8} {9,8}";


    /// <summary>A relative error as a signed percentage, or a dash when it is not defined.</summary>
    private static string Err(double v) =>
        double.IsNaN(v) || double.IsInfinity(v)
            ? "-"
            : (100.0 * v).ToString("+0.0;-0.0;0.0", Inv) + "%";

    /// <summary>
    /// A ratio with its uncertainty, printed to the precision the uncertainty allows.
    ///
    /// <para>0.9975 +/- 0.0505 claims four digits the measurement has not got, and a reader
    /// who takes the 0.9975 seriously has been misled by the formatting rather than by the
    /// number. So the decimals follow the uncertainty: two of them when it is 5 per cent,
    /// five when the two estimates agreed to a part in a hundred thousand.</para>
    /// </summary>
    private static string RatioWithUncertainty(double ratio, double uncertainty)
    {
        int dp = uncertainty >= 0.05 ? 2
               : uncertainty >= 0.005 ? 3
               : uncertainty >= 0.0005 ? 4 : 5;
        string f = "F" + dp.ToString(Inv);

        // An uncertainty that rounds to zero is not zero, and saying so beats printing a
        // string of noughts that reads as exactness.
        double floor = 0.5 * Math.Pow(10.0, -dp);
        string plusMinus = uncertainty < floor
            ? "<" + floor.ToString(f, Inv)
            : uncertainty.ToString(f, Inv);

        // The value is padded so that the +/- of every row sits in one column: three rows
        // whose signs and widths differ are read down, not across.
        return string.Format(Inv, "{0,7} +/- {1}", ratio.ToString(f, Inv), plusMinus);
    }

    /// <summary>Column widths of the coefficient-recovery table.</summary>
    private const string RecoveryLayout = "  {0,-8} {1,12} {2,13}   {3}";

    /// <summary>An unsigned percentage - for a magnitude, where a leading + would mislead.</summary>
    private static string Mag(double v) =>
        double.IsNaN(v) || double.IsInfinity(v) ? "-" : (100.0 * v).ToString("0.0", Inv) + "%";

    /// <summary>A distortion figure, already in per cent.</summary>
    private static string Pct(double v) =>
        double.IsNaN(v) || double.IsInfinity(v) ? "-" : v.ToString("0.0000", Inv);

    /// <summary>
    /// Third, fifth and seventh order per surface, intrinsic and induced, the seventh by the
    /// Forbes series trace. Null when the coefficients cannot be separated - a system with no
    /// field, or a design the series does not close on.
    /// </summary>
    public string? BuildForbesText(int degree = 3)
    {
        double field = MaxField();
        var trace = ParaxialTrace.Trace(_sys, PrimaryIndices, field);
        return ForbesReport.Build(_sys, PrimaryIndices, trace, field, degree);
    }

    public string BuildPrmsTsv()
    {
        var cases = PrmsCases();
        var sb = new StringBuilder();
        sb.AppendLine("wavelength	field	hy	weight	prms");
        foreach (var c in cases)
        {
            string wl = c.Wave < _sys.Wavelengths.Count
                ? _sys.Wavelengths[c.Wave].Value.ToString("R", Inv) : "";
            string fld = c.Field < _sys.Fields.Count
                ? _sys.Fields[c.Field].Y.ToString("R", Inv) : "";
            sb.AppendLine(string.Join("	", wl, fld, Raw(c.H), Raw(c.Weight), Raw(Prms.Value(c.Totals, c.H))));
        }
        sb.AppendLine(string.Join("	", "ALL", "ALL", "", "",
            Raw(Prms.Composite(cases.Select(c => (c.Totals, c.H, c.Weight))))));
        return sb.ToString();
    }

    private static string Num6(double v) =>
        double.IsNaN(v) || double.IsInfinity(v) ? "-" : v.ToString("0.000000", Inv);

    public string BuildBuchdahlTsv()
    {
        var p = ParaxialTrace.Trace(_sys, PrimaryIndices, MaxField());
        var b = Buchdahl(p);
        var sb = new StringBuilder();
        sb.Append("surface	part");
        foreach (var nme in BuchdahlTerms.Names) sb.Append("	" + nme.ToLowerInvariant());
        sb.AppendLine();
        for (int i = 1; i < _sys.Surfaces.Count - 1; i++)
        {
            sb.Append(i.ToString(Inv) + "	intrinsic");
            foreach (var nme in BuchdahlTerms.Names) sb.Append("	" + Raw(b.Intrinsic[i][nme]));
            sb.AppendLine();
            if (b.Aspheric[i] != null)
            {
                sb.Append(i.ToString(Inv) + "	aspheric");
                foreach (var nme in BuchdahlTerms.Names) sb.Append("	" + Raw(b.Aspheric[i]![nme]));
                sb.AppendLine();
            }
        }
        sb.Append("TOTAL	transverse");
        foreach (var nme in BuchdahlTerms.Names) sb.Append("	" + Raw(b.Totals[nme]));
        sb.AppendLine();
        return sb.ToString();
    }

    public string BuildSeidelTsv()
    {
        var p = ParaxialTrace.Trace(_sys, PrimaryIndices, MaxField());
        var s = Seidel(p);
        var sb = new StringBuilder();
        sb.AppendLine("surface	s1_spherical	s2_coma	s3_astigmatism	s4_petzval	s5_distortion	cl_axial_color	ct_lateral_color");
        for (int i = 1; i < _sys.Surfaces.Count - 1; i++)
            sb.AppendLine(string.Join("	", i.ToString(Inv), Raw(s.S1[i]), Raw(s.S2[i]), Raw(s.S3[i]),
                Raw(s.S4[i]), Raw(s.S5[i]), Raw(s.CL[i]), Raw(s.CT[i])));
        sb.AppendLine(string.Join("	", "TOTAL", Raw(s.TotalS1), Raw(s.TotalS2), Raw(s.TotalS3),
            Raw(s.TotalS4), Raw(s.TotalS5), Raw(s.TotalCL), Raw(s.TotalCT)));
        return sb.ToString();
    }

    private static string Sci(double v) => v.ToString("0.0000E+00", Inv);

    private void Warnings(StringBuilder sb, ParaxialResult p)
    {
        var notes = new List<string>();
        if (Unresolved.Count > 0)
            notes.Add($"No index for {string.Join(", ", Unresolved)} - treated as air, so every "
                    + "number above is wrong for those surfaces.");
        foreach (var s in _sys.Surfaces)
            if (s.Type == SurfaceType.CoordinateBreak)
            {
                notes.Add("This design has a coordinate break. This program analyses rotationally "
                        + "symmetric systems; a tilt or decentre is read but not applied.");
                break;
            }
        if (p.InvariantDrift > 1e-9)
            notes.Add($"Lagrange invariant drifts by {p.InvariantDrift:0.0E+00} - the paraxial trace "
                    + "is not self-consistent on this system.");
        if (notes.Count == 0) return;

        sb.AppendLine("WARNINGS");
        sb.AppendLine("----------------------------------------------------------------");
        foreach (var w in notes) sb.AppendLine("  ! " + w);
        sb.AppendLine();
    }

    // ── Machine-readable ─────────────────────────────────────────────────────────────

    public string BuildPrescriptionTsv()
    {
        var n = PrimaryIndices;
        var sb = new StringBuilder();
        sb.AppendLine("surface\tlabel\ttype\tradius\tthickness\tmaterial\tindex\tsemi_diameter\tconic\tis_stop");
        for (int i = 0; i < _sys.Surfaces.Count; i++)
        {
            var s = _sys.Surfaces[i];
            string label = i == 0 ? "OBJ" : i == _sys.Surfaces.Count - 1 ? "IMG" : i.ToString(Inv);
            sb.AppendLine(string.Join("\t",
                i.ToString(Inv), label, s.Type.ToString(), Raw(s.Radius), Raw(s.Thickness),
                MaterialName(s),
                i < n.Length ? n[i].ToString("R", Inv) : "",
                Raw(s.SemiDiameter), Raw(s.Conic), s.IsStop ? "1" : "0"));
        }
        return sb.ToString();
    }

    public string BuildIndicesTsv()
    {
        var sb = new StringBuilder();
        sb.Append("material\tsource\tvd");
        foreach (var w in _sys.Wavelengths) sb.Append("\tn_" + w.Value.ToString("0.####", Inv));
        sb.AppendLine();

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < _sys.Surfaces.Count; i++)
        {
            var s = _sys.Surfaces[i];
            string name = MaterialName(s);
            if (string.IsNullOrEmpty(name) || !seen.Add(name)) continue;
            sb.Append(string.Join("\t", name, Source(s), Vd(s)));
            foreach (var perWave in _indices)
                sb.Append("\t" + (i < perWave.Length ? perWave[i].ToString("R", Inv) : ""));
            sb.AppendLine();
        }
        return sb.ToString();
    }

    public string BuildFirstOrderTsv()
    {
        var p = ParaxialTrace.Trace(_sys, PrimaryIndices, MaxField());
        var sb = new StringBuilder();
        sb.AppendLine("quantity\tvalue");
        sb.AppendLine("effective_focal_length\t" + Raw(p.Efl));
        sb.AppendLine("back_focal_length\t" + Raw(p.Bfl));
        sb.AppendLine("f_number\t" + Raw(p.FNumber));
        sb.AppendLine("entrance_pupil_diameter\t" + Raw(p.Epd));
        sb.AppendLine("entrance_pupil_position\t" + Raw(p.EntrancePupilPosition));
        sb.AppendLine("exit_pupil_diameter\t" + Raw(p.ExitPupilDiameter));
        sb.AppendLine("exit_pupil_position\t" + Raw(p.ExitPupilPosition));
        sb.AppendLine("image_height_at_image_surface\t" + Raw(p.ImageHeight));
        sb.AppendLine("paraxial_focus_distance\t" + Raw(p.ParaxialFocusDistance));
        sb.AppendLine("paraxial_image_height\t" + Raw(p.ParaxialImageHeight));
        sb.AppendLine("magnification\t" + Raw(p.Magnification));
        sb.AppendLine("infinite_conjugate\t" + (p.InfiniteConjugate ? "1" : "0"));
        sb.AppendLine("lagrange_invariant\t" + Raw(p.LagrangeInvariant));
        sb.AppendLine("invariant_drift\t" + Raw(p.InvariantDrift));
        return sb.ToString();
    }

    /// <summary>Per-surface paraxial ray data - the input the aberration sums will need.</summary>
    public string BuildParaxialRaysTsv()
    {
        var p = ParaxialTrace.Trace(_sys, PrimaryIndices, MaxField());
        var sb = new StringBuilder();
        sb.AppendLine("surface\tn\ty_marginal\tu_marginal\ty_chief\tu_chief");
        for (int i = 0; i < _sys.Surfaces.Count; i++)
            sb.AppendLine(string.Join("\t", i.ToString(Inv),
                Raw(p.N[i]), Raw(p.Y[i]), Raw(p.U[i]), Raw(p.Ybar[i]), Raw(p.Ubar[i])));
        return sb.ToString();
    }

    // ── Shared formatting ────────────────────────────────────────────────────────────

    private static string Kind(Surface s) =>
        s.Type == SurfaceType.EvenAsphere ? "Asphere"
        : s.Type == SurfaceType.Standard ? (Math.Abs(s.Curvature) < 1e-15 ? "Plane" : "Sphere")
        : s.Type.ToString();

    private static string Note(Surface s)
    {
        var bits = new List<string>();
        if (s.IsStop) bits.Add("stop");
        if (s.IsMirror) bits.Add("mirror");
        if (s.HasMarginalRaySolve) bits.Add("solved");
        if (!string.IsNullOrWhiteSpace(s.Comment)) bits.Add(s.Comment!);
        return string.Join(", ", bits);
    }

    private static string MaterialName(Surface s)
    {
        if (!string.IsNullOrWhiteSpace(s.Material)) return s.Material!;
        if (s.ModelIndexEnabled && s.ModelNd > 0.0)
            return $"model {s.ModelNd.ToString("0.####", Inv)}/{s.ModelVd.ToString("0.##", Inv)}";
        return "";
    }

    private string Source(Surface s)
    {
        if (s.ModelIndexEnabled) return "model glass";
        if (GlassCode.TryParse(s.Material, out _, out _)) return "glass code";
        var g = _catalog.GetGlass(s.Material);
        return g != null ? g.Catalog : "NOT FOUND";
    }

    private string Vd(Surface s)
    {
        if (s.ModelIndexEnabled && s.ModelVd > 0) return s.ModelVd.ToString("0.##", Inv);
        if (GlassCode.TryParse(s.Material, out _, out double vd)) return vd.ToString("0.##", Inv);
        var g = _catalog.GetGlass(s.Material);
        return g != null && g.Vd > 0 ? g.Vd.ToString("0.##", Inv) : "";
    }

    /// <summary>Display form: fixed decimals, with infinity spelled out.</summary>
    private static string Num(double v) =>
        double.IsNaN(v) ? "-" : double.IsInfinity(v) ? "infinity" : v.ToString("0.####", Inv);

    /// <summary>Round-trip form for the TSV, so a reader loses no precision.</summary>
    private static string Raw(double v) =>
        double.IsNaN(v) ? "" : double.IsInfinity(v) ? (v > 0 ? "inf" : "-inf") : v.ToString("R", Inv);
}
