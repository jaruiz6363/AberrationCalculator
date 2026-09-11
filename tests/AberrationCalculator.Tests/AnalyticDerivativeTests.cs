using System;
using System.Collections.Generic;
using System.Linq;

using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.IO;
using AberrationCalculator.Core.Models;
using AberrationCalculator.Optimize.Evaluation;
using AberrationCalculator.Optimize.Operands;
using AberrationCalculator.Optimize.Variables;

using Xunit;

namespace AberrationCalculator.Tests;

/// <summary>
/// The claim this optimiser rests on: every derivative it uses is analytic and exact.
///
/// <para>The only way to check that from outside is to compare it against the thing it replaces.
/// A central difference is accurate to about the two-thirds power of machine epsilon - some
/// eleven digits at a well-chosen step - so agreement to five or six digits across every
/// operand and every variable says the analytic derivative is right and the difference quotient
/// is the one carrying the error.</para>
///
/// <para>This is the test that guards the whole dual-number retrofit. If someone edits
/// Buchdahl's scheme, or the series trace, or the paraxial recurrence, and gets the arithmetic
/// right but the differentiation wrong, this is what says so.</para>
/// </summary>
public class AnalyticDerivativeTests
{
    private static (Design Design, MeritFunction Merit) Build(string lens, VariableSet vars,
                                                              IEnumerable<Operand> operands,
                                                              Action<OpticalSystem>? prepare = null)
    {
        var catalog = CatalogLocator.LoadBundled();
        var system = LensFile.Read(Fixtures.Lens(lens), catalog);
        prepare?.Invoke(system);
        var design = new Design(system, catalog, vars);
        var merit = new MeritFunction(design);
        merit.AddRange(operands);
        return (design, merit);
    }

    private static VariableSet TripletVariables() => Spread(new[]
    {
        new Variable { Kind = VariableKind.Curvature, Surface = 1 },
        new Variable { Kind = VariableKind.Curvature, Surface = 2 },
        new Variable { Kind = VariableKind.Curvature, Surface = 4 },
        new Variable { Kind = VariableKind.Thickness, Surface = 2 },
        new Variable { Kind = VariableKind.Thickness, Surface = 6 },
    });

    private static VariableSet Spread(IEnumerable<Variable> vs)
    {
        var set = new VariableSet();
        set.AddRange(vs);
        return set;
    }

    /// <summary>
    /// Every operand this optimiser offers, on a design that exercises all of them.
    /// </summary>
    private static Operand[] EveryOperand() => new[]
    {
        new Operand { Type = OperandType.PRMSA, Target = 0.0, Weight = 1.0 },
        new Operand { Type = OperandType.EFL, Target = 50.0, Weight = 1.0 },
        new Operand { Type = OperandType.TTL, Target = 60.0, Weight = 1.0 },

        new Operand { Type = OperandType.PY, Surface = 4, Py = 0.7, Target = 0.0 },
        new Operand { Type = OperandType.PX, Surface = 3, Px = 0.5, Target = 0.0 },
        new Operand { Type = OperandType.PM, Surface = 5, Py = 1.0, Target = 0.0 },
        new Operand { Type = OperandType.PN, Surface = 5, Py = 1.0, Target = 1.0 },
        new Operand { Type = OperandType.PL, Surface = 4, Px = 0.6, Target = 0.0 },

        new Operand { Type = OperandType.RY, Surface = 7, Py = 1.0, Hy = 1.0, Target = 0.0 },
        new Operand { Type = OperandType.RX, Surface = 4, Px = 0.8, Hy = 0.7, Target = 0.0 },
        new Operand { Type = OperandType.RZ, Surface = 3, Py = 0.9, Target = 0.0 },
        new Operand { Type = OperandType.RM, Surface = 6, Py = 0.7, Target = 0.0 },
        new Operand { Type = OperandType.RN, Surface = 2, Py = 0.5, Target = 1.0 },
        new Operand { Type = OperandType.RL, Surface = 2, Px = 0.5, Target = 0.0 },

        new Operand { Type = OperandType.LCF, Hy = 1.0, Target = 0.0 },
        new Operand { Type = OperandType.AXC, Target = 0.0 },
        new Operand { Type = OperandType.DISTF, Hy = 1.0, Target = 0.0 },

        // Boundary operands are deliberately given limits the design VIOLATES, so that the
        // residual is live and its derivative is not trivially zero. A satisfied boundary has
        // no gradient by design, and testing that would test nothing.
        new Operand { Type = OperandType.EGT, Surface = 1, Surface2 = 6, Min = 50.0 },
        new Operand { Type = OperandType.EAT, Surface = 1, Surface2 = 6, Min = 50.0 },
        new Operand { Type = OperandType.DTRGT, Surface = 1, Surface2 = 6, Max = 0.001 },
    };

    [Fact]
    public void AnalyticJacobianMatchesCentralDifferences_SphericalTriplet()
    {
        // Spherical throughout, which is the only kind this optimiser accepts.
        var vars = TripletVariables();
        var (design, merit) = Build("CookeTriplet", vars, EveryOperand());
        CheckJacobian(design, merit, vars);
    }

    /// <summary>
    /// A PLANE surface being bent keeps its full gradient.
    ///
    /// <para>This is the case that makes the zero-tests in the chain worth naming rather than
    /// spelling out as <c>== 0.0</c>. A plane has zero curvature, so the conic term of its sag is
    /// skipped - correctly, the term is zero. But when that curvature is a VARIABLE the term has
    /// value nothing and derivative <c>r^2/2</c>, and skipping it would leave the sag right while
    /// the gradient went short: the merit function stays correct and only the direction the
    /// search walks in goes wrong, which is a fault nothing announces.</para>
    ///
    /// <para>Surface 2 of the triplet is flattened here so that its curvature starts at exactly
    /// zero, and then every operand is checked against a central difference as usual. It is
    /// deliberately checked with the SAG-dependent operands present - edge thicknesses and real
    /// rays - because those are the ones that read a sag at all.</para>
    /// </summary>
    [Fact]
    public void ACurvatureVariableStartingAtExactlyZeroKeepsItsGradient()
    {
        var vars = Spread(new[]
        {
            new Variable { Kind = VariableKind.Curvature, Surface = 1 },
            new Variable { Kind = VariableKind.Curvature, Surface = 2 },   // the plane
            new Variable { Kind = VariableKind.Curvature, Surface = 4 },
            new Variable { Kind = VariableKind.Thickness, Surface = 2 },
        });

        var (design, merit) = Build("CookeTriplet", vars, EveryOperand(),
                                    s => s.Surfaces[2].Curvature = 0.0);

        Assert.Equal(0.0, design.System.Surfaces[2].Curvature);
        CheckJacobian(design, merit, vars);
    }

    /// <summary>
    /// A figured design is refused, and told why.
    ///
    /// <para>The coefficients come from Buchdahl's scheme, whose aspheric seventh order is a
    /// reconstruction real rays reject by up to a factor of four. Optimising against that would
    /// not be slow, it would be aimed wrongly - so the run stops before it starts rather than
    /// producing a design that looks reasonable and is not.</para>
    /// </summary>
    [Fact]
    public void AFiguredDesignIsRefusedBeforeAnythingRuns()
    {
        var catalog = CatalogLocator.LoadBundled();
        var system = LensFile.Read(Fixtures.Lens("CookeTriplet"), catalog);
        system.Surfaces[1].Conic = -0.42;

        var ex = Assert.Throws<NotSupportedException>(
            () => new Design(system, catalog, TripletVariables()));

        Assert.Contains("SPHERICAL", ex.Message);
        Assert.Contains("surface 1", ex.Message);
        // It has to say what to do instead, not merely refuse.
        Assert.Contains("--forbes", ex.Message);
    }

    /// <summary>An aspheric coefficient is figuring just as a conic is.</summary>
    [Fact]
    public void AnAsphericTermCountsAsFiguringToo()
    {
        var catalog = CatalogLocator.LoadBundled();
        var system = LensFile.Read(Fixtures.Lens("CookeTriplet"), catalog);
        system.Surfaces[4].AsphericCoefficients[1] = 1.5e-6;

        var ex = Assert.Throws<NotSupportedException>(
            () => new Design(system, catalog, TripletVariables()));
        Assert.Contains("surface 4", ex.Message);
    }

    /// <summary>
    /// Compares the analytic Jacobian with a central difference of the same residuals, column
    /// by column, and reports which operand and which variable disagreed rather than only that
    /// something did.
    /// </summary>
    private static void CheckJacobian(Design design, MeritFunction merit, VariableSet vars)
    {
        var x0 = design.Read();
        var analytic = merit.Evaluate(true);
        Assert.True(analytic.Ok, analytic.Failure);

        int m = merit.Operands.Count;
        int n = vars.Count;
        Assert.True(m > 0);

        // Something has to be moving, or the comparison is between two zeros.
        double largest = 0.0;
        for (int i = 0; i < m; i++)
            for (int j = 0; j < n; j++)
                largest = Math.Max(largest, Math.Abs(analytic.Jacobian[i, j]));
        Assert.True(largest > 0.0, "the analytic Jacobian is entirely zero");

        for (int j = 0; j < n; j++)
        {
            // The step has to leave the difference quotient inside its linear regime, and what
            // counts as small depends entirely on the variable. A curvature lives near 0.01, a
            // thickness near 10 and an r^4 aspheric coefficient near 1e-8; one step size cannot
            // serve all three, and stepping A4 by 1e-7 moves the merit by four per cent, which
            // measures a secant and not a derivative.
            //
            // Sizing the step so that the RESIDUALS move by about a part in a million puts every
            // variable in the same regime whatever its units. Taking that size from the analytic
            // column does not bias the comparison: it sets how far to probe, not what to expect
            // there, and an analytic derivative wrong by any factor would still be caught.
            double column = 0.0;
            for (int i = 0; i < m; i++)
                column = Math.Max(column, Math.Abs(analytic.Jacobian[i, j]));

            double magnitude = Math.Max(Math.Abs(x0[j]), 1.0);
            double h = column > 0.0 ? 1e-6 / column : 1e-6;
            h = Math.Min(h, 1e-4 * magnitude);
            h = Math.Max(h, 1e-13 * magnitude);

            var plus = (double[])x0.Clone(); plus[j] += h;
            design.Apply(plus);
            var rp = merit.Evaluate(false);

            var minus = (double[])x0.Clone(); minus[j] -= h;
            design.Apply(minus);
            var rm = merit.Evaluate(false);

            design.Apply((double[])x0.Clone());

            Assert.True(rp.Ok && rm.Ok, "the design could not be evaluated beside the start point");

            for (int i = 0; i < m; i++)
            {
                double numeric = (rp.Residuals[i] - rm.Residuals[i]) / (2.0 * h);
                double exact = analytic.Jacobian[i, j];

                // A central difference of a quantity of size s carries an error of order
                // s * eps^(2/3); the tolerance has to be relative to the column, not to the
                // entry, or an entry that is legitimately near zero is held to an absolute
                // standard nothing could meet.
                double scale = Math.Max(Math.Abs(exact), Math.Abs(numeric));
                double tolerance = 2e-4 * scale + 1e-7 * largest;

                Assert.True(Math.Abs(exact - numeric) <= tolerance,
                    $"d({merit.Operands[i].Label})/d({vars[j].Name}): " +
                    $"analytic {exact:G10}, central difference {numeric:G10}, " +
                    $"differ by {Math.Abs(exact - numeric):G4} (tolerance {tolerance:G4})");
            }
        }
    }

    /// <summary>
    /// A satisfied boundary operand costs nothing and pulls on nothing.
    ///
    /// <para>This is what lets a design carry a dozen manufacturability limits without any of
    /// them bending the solution until one is actually threatened, so it is worth stating as a
    /// test rather than leaving as a remark in a comment.</para>
    /// </summary>
    [Fact]
    public void SatisfiedBoundaryOperandHasNoResidualAndNoGradient()
    {
        var vars = TripletVariables();
        var (design, merit) = Build("CookeTriplet", vars, new[]
        {
            new Operand { Type = OperandType.EGT, Surface = 1, Surface2 = 6, Min = -1000.0 },
            new Operand { Type = OperandType.DTRGT, Surface = 1, Surface2 = 6, Max = 1e9 },
        });

        var r = merit.Evaluate(true);
        Assert.True(r.Ok, r.Failure);
        Assert.True(merit.Operands.Count > 0);

        Assert.All(r.Residuals, v => Assert.Equal(0.0, v));
        for (int i = 0; i < merit.Operands.Count; i++)
            for (int j = 0; j < vars.Count; j++)
                Assert.Equal(0.0, r.Jacobian[i, j]);
        Assert.Equal(0.0, r.SumSquares);
    }

    /// <summary>
    /// PRMSA as the optimiser computes it is PRMSA as the report prints it.
    ///
    /// <para>The optimiser reaches it through the dual-number build of the chain and the report
    /// through the ordinary one. They are the same source compiled twice, so the two ought to
    /// agree to the last bit - and if they ever do not, the optimiser is descending a quantity
    /// the program does not report.</para>
    /// </summary>
    [Fact]
    public void PrmsaAgreesWithTheReport()
    {
        var catalog = CatalogLocator.LoadBundled();
        string path = Fixtures.Lens("CookeTriplet");
        var system = LensFile.Read(path, catalog);

        var writer = new Core.Report.ReportWriter(system, catalog, path);
        string tsv = writer.BuildPrmsTsv();
        string last = tsv.TrimEnd('\n', '\r').Split('\n').Last();
        double reported = double.Parse(last.Split('\t').Last(),
                                       System.Globalization.CultureInfo.InvariantCulture);

        var design = new Design(system, catalog, new VariableSet());
        var merit = new MeritFunction(design);
        merit.Add(new Operand { Type = OperandType.PRMSA, Target = 0.0 });

        var r = merit.Evaluate(false);
        Assert.True(r.Ok, r.Failure);

        // PRMSA is posed to the optimiser as one residual per field and wavelength, so the
        // aggregate the report prints is not any single value - it is what they come to
        // together. Reassembling it here is the whole claim: the quantity being minimised is
        // still, exactly, the number the report shows.
        //
        // residual = sqrt(w)*(v - 0), and the sub-weights were chosen so that
        // sum(residual^2) = W * PRMSA^2. So PRMSA = sqrt(sum(residual^2) / W).
        double squares = 0.0;
        foreach (double residual in r.Residuals) squares += residual * residual;

        double declared = 1.0;                     // the weight the operand was declared with
        Assert.Equal(reported, Math.Sqrt(squares / declared), 12);

        // And every sub-operand is one row of the report's own table.
        Assert.Equal(system.Wavelengths.Count * system.Fields.Count, r.Values.Length);
    }
}
