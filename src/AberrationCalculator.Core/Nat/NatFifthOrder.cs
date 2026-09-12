using System;

namespace AberrationCalculator.Core.Nat;

/// <summary>
/// The fifth-order aberration field of a perturbed system: where the nodes are, and what the
/// wave aberration is at any field point.
///
/// <para>Source: Thompson, K. P., "Multinodal fifth-order optical aberrations of optical systems
/// without rotational symmetry: the comatic aberrations," <i>J. Opt. Soc. Am. A</i> <b>27</b>,
/// 1490 (2010), Appendix B. Equation numbers below are his.</para>
///
/// <para><b>Three of the five fifth-order types need no new machinery.</b> His Eq. (B1) writes
/// the full expansion with <c>W240M</c> and <c>W242</c> already in reduced form,</para>
/// <code>
///     + W240M[(H - a240M).(H - a240M) + b240M](rho.rho)^2
///     + (1/2) W242{[(H - a242)^2 + b242^2].rho^2}(rho.rho)
/// </code>
/// <para>which are the third-order <c>W220M</c> and <c>W222</c> forms exactly - a vertex plus a
/// scalar, and a binodal pair. Eq. (B2) collapses <c>W151</c> to a single node like
/// <c>W131</c>. Only <c>W331M</c> and <c>W333</c> are new, and both are trinodal.</para>
///
/// <para><b>The unnormalised form is the one to compute with.</b> Thompson says so directly of
/// Eq. (B5): it "is complete and, in fact, the most useful form for programming into a simulation
/// environment, the most insightful form for recognizing nodal positions is to normalize this
/// equation". So <see cref="ComaticWave"/> evaluates the unnormalised expansion and the node
/// properties below come from the normalised one - and each is checked against the other, which
/// needs no oracle at all.</para>
/// </summary>
public sealed class NatFifthOrder
{
    /// <summary>Field vectors for field-linear, fifth-order aperture coma.</summary>
    public FieldMoments M151 { get; init; }

    /// <summary>Field vectors for field-cubed, third-order aperture coma, medial.</summary>
    public FieldMoments M331M { get; init; }

    /// <summary>Field vectors for elliptical coma (trefoil).</summary>
    public FieldMoments M333 { get; init; }

    /// <summary>Field vectors for fifth-order medial field curvature.</summary>
    public FieldMoments M240M { get; init; }

    /// <summary>Field vectors for fifth-order astigmatism.</summary>
    public FieldMoments M242 { get; init; }

    /// <summary>Third-order coma, carried because Eqs. (B13-14) modify it.</summary>
    public FieldMoments M131 { get; init; }

    /// <summary>
    /// The single node of field-linear fifth-order coma, Eq. (B9): <c>W = W151(H151.rho)(rho.rho)^2</c>
    /// with <c>H151 = H - a151</c>. It behaves exactly like third-order coma.
    /// </summary>
    public Vec2 Node151 => M151.a;

    /// <summary>
    /// The three COLLINEAR nodes of field-cubed, third-order aperture coma. Eq. (B10) leaves the
    /// field-cubed group as
    /// <code>
    ///     W331M[(H331M^2 + b331M^2) H331M*].rho (rho.rho)
    /// </code>
    /// which vanishes where the conjugate does - at the field centre - and where
    /// <c>H331M^2 = -b331M^2</c>, giving a symmetric pair about it. The three are collinear
    /// because the pair is <c>+/-</c> the same square root.
    /// </summary>
    public Vec2[] Nodes331M
    {
        get
        {
            Vec2 a = M331M.a, half = (-M331M.b2).Sqrt();
            return new[] { a, a + half, a - half };
        }
    }

    /// <summary>
    /// The three nodes of elliptical coma. Eq. (B11) is
    /// <code>
    ///     W = (1/4) W333 [H333^3 + 3 H333 b333^2 - c333^3].rho^3
    /// </code>
    /// so the nodes are the roots of a depressed cubic <c>x^3 + 3 b^2 x - c^3 = 0</c> in
    /// Thompson's vector algebra, which is Cardano's solution with every product and root taken
    /// vectorially. Writing <c>x = R + S</c> and choosing <c>R S = -b^2</c> leaves
    /// <c>R^3 + S^3 = c^3</c> and <c>R^3 S^3 = -b^6</c>, so <c>R^3</c> and <c>S^3</c> are the
    /// roots of a quadratic. The three cube roots then give
    /// <code>
    ///     2 X,    -X + i sqrt(3) X~,    -X - i sqrt(3) X~
    /// </code>
    /// with <c>X = (R + S)/2</c> and <c>X~ = (R - S)/2</c>, which is the form Thompson's Fig. 10
    /// draws.
    /// </summary>
    public Vec2[] Nodes333
    {
        get
        {
            Vec2 b2 = M333.b2, c3 = M333.c3;

            // A cube root amplifies relative error to the one-third power, so a b^2 or c^3 that
            // ought to be zero and is instead at the round-off floor of the subtraction that
            // produced it - both are differences of nearly equal quantities - comes back as a
            // node splitting of order 1e-6 rather than 1e-16. That is noise reported as physics.
            // Below the floor of their own construction the splitting is not real, and the three
            // nodes coincide at the field centre.
            Scalar w = SMath.Abs(M333.W) > 1e-300 ? SMath.Abs(M333.W) : 1.0;
            Scalar aMag = M333.a.Magnitude;
            Scalar floor2 = 1e-13 * (M333.B2.Magnitude / w + aMag * aMag);
            Scalar floor3 = 1e-13 * (M333.C3.Magnitude / w + aMag * aMag * aMag);
            if (b2.Magnitude <= floor2 && c3.Magnitude <= floor3)
                return new[] { M333.a, M333.a, M333.a };

            Vec2 disc = (c3.Squared + 4.0 * (b2.Squared * b2)).Sqrt();
            Vec2 rCubed = 0.5 * (c3 + disc);
            Vec2 sCubed = 0.5 * (c3 - disc);

            Vec2 r = rCubed.CubeRoot();
            Vec2 s;
            if (r.MagnitudeSquared > 1e-300)
                s = (-b2) / r;              // the pairing R S = -b^2 picks the branch
            else
            {
                r = Vec2.Zero;
                s = sCubed.CubeRoot();
            }

            Vec2 xbar = 0.5 * (r + s), xtilde = 0.5 * (r - s);
            Vec2 turn = SMath.Sqrt(3.0) * xtilde.TimesI;
            Vec2 a = M333.a;
            return new[] { a + 2.0 * xbar, a - xbar + turn, a - xbar - turn };
        }
    }

    /// <summary>The vertex of fifth-order medial field curvature, and its scalar offset.</summary>
    public Vec2 Vertex240M => M240M.a;

    /// <inheritdoc cref="Vertex240M"/>
    public Scalar B240M => M240M.b;

    /// <summary>
    /// The two nodes of fifth-order astigmatism - the same binodal form Shack found at third
    /// order, with fifth-order coefficients.
    /// </summary>
    public Vec2[] Nodes242
    {
        get
        {
            Vec2 a = M242.a, half = (-M242.b2).Sqrt();
            return new[] { a + half, a - half };
        }
    }

    /// <summary>
    /// Eq. (B13). Field-cubed coma generates terms that belong with field-linear coma and change
    /// its MAGNITUDE: <c>W131E = W131 + 2 W331M b331M</c>.
    ///
    /// <para>This is what Thompson means by an induced term - a lower-order term thrown off when
    /// a higher-order one is expanded about its displaced field centre. It is a consequence of
    /// the nodal algebra, not a surface-interaction effect, and it is the reason third-order coma
    /// in a perturbed system is not simply the third-order coma of the aligned one.</para>
    /// </summary>
    public Scalar W131E => M131.W + 2.0 * M331M.W * M331M.b;

    /// <summary>
    /// Eq. (B14). The same generated terms also move the third-order coma NODE:
    /// <code>
    ///     a131E = (1/W131E)[W131 a131 + W331M(c331M - b331M^2 a331M*)]
    /// </code>
    /// </summary>
    public Vec2 Node131E
    {
        get
        {
            Scalar w = W131E;
            if (SMath.Abs(w) < 1e-300) return Vec2.Zero;
            Vec2 inner = M331M.c - M331M.b2 * M331M.a.Conjugate;
            return (1.0 / w) * (M131.W * M131.a + M331M.W * inner);
        }
    }

    /// <summary>
    /// The comatic wave aberration at a field point and pupil point, Eq. (B5), unnormalised - the
    /// form Thompson recommends for computation. The <c>W151</c> term of Eq. (B2) is included.
    ///
    /// <para>The piston and defocus term <c>delta W11</c> of his opening line is not: it is a
    /// focus adjustment rather than an aberration, and this program keeps focus decisions
    /// separate.</para>
    /// </summary>
    public Scalar ComaticWave(Vec2 h, Vec2 rho)
    {
        Scalar rr = Vec2.Dot(rho, rho);
        Scalar hh = Vec2.Dot(h, h);

        // Third-order coma: [(W131 H - A131).rho](rho.rho)
        Vec2 v131 = M131.W * h - M131.A;

        // Field-linear fifth-order coma, Eq. (B2): [(W151 H - A151).rho](rho.rho)^2
        Vec2 v151 = M151.W * h - M151.A;

        // Field-cubed third-order aperture coma, Eq. (B3).
        Vec2 v331 = M331M.W * hh * h
                  - 2.0 * Vec2.Dot(h, M331M.A) * h
                  + 2.0 * M331M.B * h
                  - hh * M331M.A
                  + M331M.B2 * h.Conjugate
                  - M331M.C;

        // Elliptical coma, Eq. (B4): (1/4)[W333 H^3 - 3 H^2 A333 + 3 H B333^2 - C333^3].rho^3
        Vec2 hSq = h.Squared;
        Vec2 v333 = M333.W * (hSq * h)
                  - 3.0 * (hSq * M333.A)
                  + 3.0 * (h * M333.B2)
                  - M333.C3;

        return Vec2.Dot(v131, rho) * rr
             + Vec2.Dot(v151, rho) * rr * rr
             + Vec2.Dot(v331, rho) * rr
             + 0.25 * Vec2.Dot(v333, rho.Squared * rho);
    }

    /// <summary>
    /// The vector whose vanishing defines the elliptical-coma nodes, Eq. (B11)'s bracket. Exposed
    /// so that the analytic nodes can be checked against it rather than against a second program.
    /// </summary>
    public Vec2 TrefoilResidual(Vec2 h)
    {
        Vec2 hn = h - M333.a;
        return hn.Squared * hn + 3.0 * (hn * M333.b2) - M333.c3;
    }

    /// <summary>
    /// The vector whose vanishing defines the field-cubed coma nodes, Eq. (B10)'s field-cubed
    /// group. Exposed for the same reason as <see cref="TrefoilResidual"/>.
    /// </summary>
    public Vec2 FieldCubedComaResidual(Vec2 h)
    {
        Vec2 hn = h - M331M.a;
        return (hn.Squared + M331M.b2) * hn.Conjugate;
    }

    /// <summary>
    /// Build the fifth-order field from the per-surface wave coefficients and sigma vectors.
    /// </summary>
    /// <param name="perSurface">Surface j's wave front coefficients, from <see cref="WaveFront"/>.</param>
    /// <param name="thirdW131">Surface j's third-order coma coefficient.</param>
    /// <param name="sigma">Surface j's sigma vector.</param>
    /// <param name="count">Number of surfaces addressed by the callbacks.</param>
    public static NatFifthOrder Compute(Func<int, Deformation> perSurface,
                                        Func<int, Scalar> thirdW131,
                                        Func<int, Vec2> sigma, int count)
    {
        if (perSurface == null) throw new ArgumentNullException(nameof(perSurface));
        if (sigma == null) throw new ArgumentNullException(nameof(sigma));

        // Eq. (B6): the medial equivalent for coma, as W220M is for field curvature.
        Scalar W331M(int j) => perSurface(j).W331 + 0.75 * perSurface(j).W333;

        return new NatFifthOrder
        {
            M131 = FieldMoments.Accumulate(thirdW131, sigma, count),
            M151 = FieldMoments.Accumulate(j => perSurface(j).W151, sigma, count),
            M331M = FieldMoments.Accumulate(W331M, sigma, count),
            M333 = FieldMoments.Accumulate(j => perSurface(j).W333, sigma, count),
            M240M = FieldMoments.Accumulate(j => perSurface(j).W240 + 0.5 * perSurface(j).W242,
                                            sigma, count),
            M242 = FieldMoments.Accumulate(j => perSurface(j).W242, sigma, count),
        };
    }
}
