namespace AberrationCalculator.Optimize.Operands;

/// <summary>
/// What a merit function operand measures.
///
/// <para>Every one of these is differentiated analytically. The paraxial and first-order
/// quantities come from the differentiated paraxial recurrence, the real-ray ones from the
/// differentiated skew trace - which is Feder's differential ray tracing arrived at by carrying
/// dual numbers through the ordinary trace rather than by writing the differentiated equations
/// out by hand - and <see cref="PRMSA"/> from the differentiated Buchdahl chain. There is no
/// finite difference anywhere in this optimiser.</para>
/// </summary>
public enum OperandType
{
    /// <summary>
    /// The predicted RMS spot radius over every field and wavelength at once, weighted.
    ///
    /// <para>Robb's analytic merit function: a quadratic form in the thirty-seven Buchdahl
    /// coefficients, with the weighted mean taken over the mean-SQUARE radii and the root
    /// taken once at the end. No rays are traced to get it. This is the same quantity the
    /// report prints as PRMSA, computed the same way, so optimising it and reading it back
    /// cannot disagree.</para>
    /// </summary>
    PRMSA = 0,

    /// <summary>Total track: the first surface to the image plane, along the axis.</summary>
    TTL,

    /// <summary>Effective focal length.</summary>
    EFL,

    // ── Paraxial ray ────────────────────────────────────────────────────────────────────
    // Where the paraxial ray of a given field and pupil coordinate is at a surface, and which
    // way it is going. X is sagittal, Y meridional, Z along the axis; L, M and N are the
    // corresponding direction cosines.

    /// <summary>Paraxial ray height, sagittal. Zero for a meridional ray.</summary>
    PX,
    /// <summary>Paraxial ray height, meridional.</summary>
    PY,
    /// <summary>Paraxial ray position along the axis at the surface. Zero: the paraxial ray
    /// meets a surface at its vertex plane, which is what makes it paraxial.</summary>
    PZ,
    /// <summary>Paraxial direction cosine, sagittal.</summary>
    PL,
    /// <summary>Paraxial direction cosine, meridional.</summary>
    PM,
    /// <summary>Paraxial direction cosine, axial.</summary>
    PN,

    // ── Real ray ────────────────────────────────────────────────────────────────────────

    /// <summary>Real ray height at the surface, sagittal.</summary>
    RX,
    /// <summary>Real ray height at the surface, meridional.</summary>
    RY,
    /// <summary>Real ray position along the axis at the surface: the sag at the point of
    /// incidence, which is what separates a real ray from a paraxial one.</summary>
    RZ,
    /// <summary>Real direction cosine after refraction, sagittal.</summary>
    RL,
    /// <summary>Real direction cosine after refraction, meridional.</summary>
    RM,
    /// <summary>Real direction cosine after refraction, axial.</summary>
    RN,

    // ── Manufacturability ───────────────────────────────────────────────────────────────

    /// <summary>Edge thickness of a GLASS element, at the clear aperture.</summary>
    EGT,

    /// <summary>Edge thickness of an AIR space, at the clear aperture.</summary>
    EAT,

    /// <summary>
    /// Diameter-to-thickness ratio of an element: clear diameter over centre thickness. A
    /// blank too wide for its thickness will not survive being ground, so this is held under
    /// a limit rather than driven to a target.
    /// </summary>
    DTRGT,

    // ── Chromatic and distortion, on real rays ──────────────────────────────────────────

    /// <summary>
    /// Real lateral colour: the spread in real chief-ray image height between the extreme
    /// wavelengths, at the field asked for.
    /// </summary>
    LCF,

    /// <summary>
    /// Real axial colour: the axial distance between where the real marginal ray crosses the
    /// axis at the extreme wavelengths. A longitudinal quantity, positive when the longer
    /// wavelength focuses further away.
    /// </summary>
    AXC,

    /// <summary>
    /// Real distortion, per cent: the real chief-ray height at the image against the paraxial
    /// height it should have had, as a percentage of that height.
    /// </summary>
    DISTF,
}
