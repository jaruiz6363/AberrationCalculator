using System;
using System.Collections.Generic;

namespace AberrationCalculator.Core.Aberrations;

/// <summary>
/// The twenty seventh-order aberration coefficients tau1..tau20, in the form Robb's
/// polynomial consumes.
///
/// <para>Buchdahl's computing scheme produces T1..T10 and their barred partners; Table II
/// of <i>J. Opt. Soc. Am.</i> <b>48</b>, 747 (1958) converts those into the tau. The
/// conversion is not one-to-one - tau2 mixes Tbar1 with half of T2, tau7 draws on four
/// different T at once - which is why the T are computed first and converted afterwards
/// rather than being produced in Robb's form directly.</para>
///
/// <para><b>Spherical surfaces only</b>, as Buchdahl's scheme is.</para>
/// </summary>
public static class TertiaryCoefficients
{
    /// <summary>
    /// Computes tau1..tau20 for a system. The returned array is indexed 1..20; index 0 is
    /// unused, so the numbering matches the literature rather than being off by one.
    /// </summary>
    public static Scalar[] Compute(
        IReadOnlyList<Models.Surface> surfaces, Scalar[] indices, Scalar efl, Scalar stopParameter,
        IReadOnlyList<Scalar[]>? aspheric = null, Scalar iota = default)
    {
        var rows = BuchdahlTableI.Compute(surfaces, indices, efl, stopParameter, aspheric,
                                          iota: iota);

        var T = new Scalar[11];
        var Tb = new Scalar[11];
        for (int i = 1; i < surfaces.Count - 1; i++)
            for (int m = 1; m <= 10; m++)
            {
                T[m] += rows[i].TertiaryTotal[m];
                Tb[m] += rows[i].TertiaryTotalBar[m];
            }

        var tau = new Scalar[21];
        tau[1] = T[1];
        tau[2] = Tb[1] + T[2] / 2.0;
        tau[3] = T[2] / 2.0;
        tau[4] = Tb[2] + T[3];
        tau[5] = T[3];
        tau[6] = T[4];
        tau[7] = Tb[3] + Tb[4] / 2.0 + T[5] / 2.0 + 3.0 * T[7] / 8.0;
        tau[8] = (Tb[4] + T[5] + T[7]) / 2.0;
        tau[9] = T[5] / 2.0 + T[7] / 4.0;
        tau[10] = T[7] / 8.0;
        tau[11] = Tb[5] + T[6];
        tau[12] = Tb[7] + T[8];
        tau[13] = T[6];
        tau[14] = T[8];
        tau[15] = Tb[6] + Tb[8] / 2.0 + T[9] / 2.0;
        tau[16] = (Tb[8] + T[9]) / 2.0;
        tau[17] = T[9] / 2.0;
        tau[18] = Tb[9] + T[10];
        tau[19] = T[10];
        tau[20] = Tb[10];
        return tau;
    }

    /// <summary>M (13.4)'s iota, 1/l_01 in focal lengths; zero for an object at infinity.</summary>
    private static Scalar IotaOf(Models.OpticalSystem system, RayTrace.ParaxialResult paraxial)
    {
        Scalar t0 = system.Surfaces[0].Thickness;
        return Scalar.IsInfinity(t0) ? 0.0 : -paraxial.Efl / t0;
    }

    /// <summary>
    /// The aperture power of each tau in Robb's polynomial. Index 1..20; they run 7, 6, 6,
    /// 5, 5, 5, 4, 4, 4, 4, 3, 3, 3, 3, 2, 2, 2, 1, 1, 0, and the field power is always
    /// seven minus this.
    /// </summary>
    private static readonly int[] AperturePower =
        { 0, 7, 6, 6, 5, 5, 5, 4, 4, 4, 4, 3, 3, 3, 3, 2, 2, 2, 1, 1, 0 };

    /// <summary>
    /// Converts Buchdahl's coefficients into the convention the rest of this program uses.
    ///
    /// <para>Buchdahl works in units of the focal length, and his pupil and field variables
    /// are not fractions: his rho is the ray SLOPE, running from zero to the marginal angle
    /// u, and his H is the TANGENT of the field angle. So a coefficient multiplying
    /// rho^a H^b converts by u^a tan(theta)^b, and the whole displacement then scales by the
    /// focal length to reach millimetres.</para>
    ///
    /// <para><b>The aperture and length parts are confirmed; the field part is NOT.</b></para>
    ///
    /// <para>Confirmed: on a Cooke triplet the seventh-order spherical coefficient, which
    /// has a = 7 and b = 0, differs between the two pipelines by exactly EFL u^7 -
    /// 50 x (-0.1)^7 = -5e-6 against a measured -5.0000108e-6. And at fifth order,
    /// Buchdahl's S1p times EFL u^5 reproduces the macro's B5 to a ratio of 1.000. Two
    /// orders, both with b = 0.</para>
    ///
    /// <para>The field factor is confirmed too, but by a different kind of evidence. No
    /// coefficient with a non-zero field power is produced by both pipelines, so it cannot
    /// be calibrated against one the way the aperture factor was. What can be done instead
    /// is to vary it and watch the prediction: tau18 carries H^6, so a five per cent error
    /// in H is a thirty-four per cent error in that term, and the agreement below could not
    /// survive it. Scaling the field factor away from tan(theta) makes the prediction
    /// worse in both directions - see the field-factor note in `docs/verification.md` - which
    /// places it inside about five per cent. The shallow minimum sits a little above 1.00
    /// rather than exactly on it, and that offset is smaller than the truncation error it
    /// would be fitted to, so it is left alone.</para>
    ///
    /// <para><b>Measured on one lens</b> - the all-spherical Cooke triplet, at 0.55 um,
    /// centroid-referenced RMS spot radius at paraxial focus, ray aiming off. Against a
    /// ray trace the seventh-order prediction is within 1.1 per cent from the axis out to
    /// about nine tenths of the field:</para>
    ///
    /// <code>
    ///   H      +5th     +7th     ray trace   err(+7th)
    ///   0.00   0.014337 0.013788 0.013699     +0.6%
    ///   0.30   0.014993 0.014371 0.014286     +0.6%
    ///   0.50   0.017083 0.016373 0.016300     +0.4%
    ///   0.70   0.020539 0.019578 0.019480     +0.5%
    ///   0.85   0.023100 0.020930 0.020697     +1.1%
    ///   0.91   0.023944 0.020844 0.020884     -0.2%
    ///   0.95   0.024438 0.020663 0.021420     -3.5%
    ///   1.00   0.024994 0.020620 0.023603    -12.6%
    /// </code>
    ///
    /// <para>Past about H = 0.9 the truncated series turns over while the traced spot keeps
    /// climbing, and the two part company. That is the ninth order arriving, not a fault in
    /// this conversion: the departure is smooth, it is not moved by ray aiming, and nothing
    /// is being clipped. It does mean the last tenth of the field is outside what seventh
    /// order can describe on this lens.</para>
    ///
    /// <para>One lens is one lens. This triplet has little seventh-order content to begin
    /// with - third order alone overstates its full-field spot by a factor of two, and
    /// fifth order gets within six per cent - so the test shows the conversion is not
    /// wrong rather than showing it works hard. The designs whose full-field error
    /// motivated this work are ASPHERIC, and this scheme handles spherical surfaces only,
    /// so they measure something less than the whole. They are measured anyway in
    /// `docs/verification.md`: even with spherical-only tau, their full-field errors improve
    /// from +85% to +24% and from -34% to -1.5%.</para>
    /// </summary>
    /// <param name="tau">Coefficients in Buchdahl's convention, indexed 1..20.</param>
    /// <param name="efl">The system's focal length.</param>
    /// <param name="marginalAngle">
    /// The marginal ray angle in image space, -1/(2 F#) for a system in air.
    /// </param>
    /// <param name="fieldTangent">Tangent of the maximum field angle.</param>
    /// <param name="sphericalSeventh">
    /// The seventh-order spherical aberration in transverse units, which REPLACES tau1 when
    /// supplied. Pass the fifth-order code's <c>Totals.B7</c>.
    ///
    /// <para>The two routes now AGREE on an asphere - the scheme's own tau1 matches the same
    /// closed form to 1e-5 over conics from -6 to +1, three decades of A4 and sixth-order
    /// figuring up to a 33 um aberration - so this is no longer a repair. It is kept because
    /// B7 remains the better-tested of the two, being checked against a closed-form mirror as
    /// well as a refractor, and because on a multi-surface system the two decompose their
    /// induced parts differently and B7 is the one validated end to end. On the aspheric
    /// triplets the two agree to 1.5% and 0.7%. See
    /// <c>ExactConicSurfaceTests</c> and `docs/verification.md`.</para>
    ///
    /// <para>Both quantities are already transverse here, so the substitution needs no
    /// conversion and cannot introduce one. On a SPHERICAL system it changes nothing: the two
    /// routes agree exactly there, which is what makes it a substitution rather than a fudge.
    /// It corrects tau1 ONLY. The other nineteen still come from the scheme, and their
    /// aspheric parts inherit the same faulty cubics - tau2 most directly, since it draws on
    /// the barred partner of the same coefficient.</para>
    /// </param>
    public static Scalar[] ToTransverse(Scalar[] tau, Scalar efl, Scalar marginalAngle,
                                        Scalar fieldTangent, Scalar? sphericalSeventh = null)
    {
        if (tau == null) throw new ArgumentNullException(nameof(tau));

        var scaled = new Scalar[21];
        for (int n = 1; n <= 20; n++)
        {
            int a = AperturePower[n];
            int b = 7 - a;
            scaled[n] = tau[n] * efl
                      * SMath.Pow(marginalAngle, a)
                      * (b == 0 ? 1.0 : SMath.Pow(fieldTangent, b));
        }
        if (sphericalSeventh.HasValue) scaled[1] = sphericalSeventh.Value;
        return scaled;
    }

    /// <summary>
    /// Computes tau2..tau20 for a system and writes them into the coefficient set, in the
    /// transverse convention the rest of the program uses.
    ///
    /// <para><b>Why this exists.</b> Everything above was reachable only by assembling the
    /// scheme, the stop parameter, the aspheric increments and the transverse conversion by
    /// hand, and the only code that ever did so was the test suite. The reported coefficient
    /// set therefore carried tau1 and NINETEEN ZEROS for every design ever run through it,
    /// and <see cref="Prms"/> - which reads eighteen of the twenty - was quietly predicting
    /// spots from a set it was never given. The assembly belongs here, once, where every
    /// caller gets it.</para>
    ///
    /// <para>Systems the scheme cannot describe are left alone rather than half-filled: a
    /// coefficient set of zeros is at least honestly third-and-fifth order, where a partial
    /// one would be neither.</para>
    /// </summary>
    public static void Attach(Models.OpticalSystem system, Scalar[] indices,
                              RayTrace.ParaxialResult paraxial, BuchdahlResult coefficients,
                              Scalar maxField)
    {
        if (system == null) throw new ArgumentNullException(nameof(system));
        if (indices == null) throw new ArgumentNullException(nameof(indices));
        if (paraxial == null) throw new ArgumentNullException(nameof(paraxial));
        if (coefficients == null) throw new ArgumentNullException(nameof(coefficients));

        int stop = system.StopSurfaceIndex;
        if (stop < 0 || stop >= system.Surfaces.Count) return;
        if (SMath.Abs(coefficients.FNumber) < 1e-12) return;

        var scheme = BuchdahlScheme.Compute(system.Surfaces, indices, paraxial.Efl,
                                            system.Surfaces[stop].SemiDiameter,
                                            IotaOf(system, paraxial));

        // The aspheric increments need the all-spherical scheme to difference against, which
        // is the same run the increments are then fed back into. Build returns null when no
        // surface is figured, and the scheme takes that as "spherical throughout".
        var spherical = BuchdahlTableI.Compute(system.Surfaces, indices, paraxial.Efl, scheme.P,
                                               iota: IotaOf(system, paraxial));
        var increments = AsphericSchemeIncrements.Build(coefficients, spherical,
                                                        system.LastOpticalSurface());

        // The conjugate, which enters ONLY through the starting values of the two rays.
        // III says so outright - "the choice of different coordinate systems reflects itself
        // only in the starting values of y_p, v_p, y_q, v_q (cf. M Secs. 12-13)" - and then
        // sends the reader to the monograph for what they are. M (13.4), reduced OT: the p
        // ray leaves at unit height with reduced angle iota = 1/l_01, in focal lengths.
        Scalar objectDistance = system.Surfaces[0].Thickness;
        bool infinite = Scalar.IsInfinity(objectDistance);
        Scalar iota = infinite ? 0.0 : -paraxial.Efl / objectDistance;

        // p is the entrance pupil position in focal lengths at ANY conjugate: by (13.4) the
        // chief ray has S = 0, so its height over its angle at surface one is exactly p. The
        // scheme derives p instead as the q/p ray-height ratio at the stop, which holds only
        // while the two conventions for the q ray differ by p times the p ray - an identity
        // that fails once iota is non-zero. The derived value is kept at infinity so the
        // results validated there stay bit-identical.
        Scalar stopParameter = infinite
            ? scheme.P
            : paraxial.EntrancePupilPosition / paraxial.Efl;

        // The conversion, M (31.11) with the variables of (13.5) and (31.13). Each factor
        // collapses to the one used before when iota is zero, which is the regression guard:
        // v'_pk becomes one, g becomes one, and the object height over the object distance
        // becomes the tangent of the field angle.
        Scalar g = 1.0 - stopParameter * iota;
        Scalar lengthFactor = paraxial.Efl
                            / (paraxial.N[system.LastOpticalSurface()] * scheme.PRayFinalAngle);
        Scalar u = -(0.5 * paraxial.Epd / paraxial.Efl) / g;
        Scalar hmax = infinite
            ? SMath.Tan(maxField * SMath.PI / 180.0)
            : -(paraxial.ParaxialImageHeight / paraxial.Magnification) / objectDistance;

        var tau = ToTransverse(
            Compute(system.Surfaces, indices, paraxial.Efl, stopParameter, increments, iota),
            lengthFactor, u, hmax, coefficients.Totals.B7);

        var t = coefficients.Totals;
        t.Tau2 = tau[2];   t.Tau3 = tau[3];   t.Tau4 = tau[4];   t.Tau5 = tau[5];
        t.Tau6 = tau[6];   t.Tau7 = tau[7];   t.Tau8 = tau[8];   t.Tau9 = tau[9];
        t.Tau10 = tau[10]; t.Tau11 = tau[11]; t.Tau12 = tau[12]; t.Tau13 = tau[13];
        t.Tau14 = tau[14]; t.Tau15 = tau[15]; t.Tau16 = tau[16]; t.Tau17 = tau[17];
        t.Tau18 = tau[18]; t.Tau19 = tau[19]; t.Tau20 = tau[20];
    }
}