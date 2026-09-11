using System;
using System.Globalization;

namespace AberrationCalculator.Optimize.Variables;

/// <summary>
/// Which construction parameter a variable drives.
///
/// <para><b>There is deliberately no conic and no aspheric member.</b> The coefficients come from
/// Buchdahl's computing scheme, whose aspheric seventh order is a reconstruction real rays
/// reject, so a figured design is refused before anything runs - see
/// <see cref="Evaluation.SphericalOnly"/>. Leaving the kinds out altogether means the refusal
/// cannot be forgotten: there is no way to express the request in the first place, and no test
/// for figuring anywhere in the evaluation loop.</para>
/// </summary>
public enum VariableKind
{
    /// <summary>Surface curvature, 1/radius. The workhorse.</summary>
    Curvature = 0,

    /// <summary>Axial thickness after the surface: a glass centre thickness or an air space.</summary>
    Thickness = 1,
}

/// <summary>
/// One thing the optimiser may change, and how far it may change it.
///
/// <para><b>Bounds are enforced by reflection.</b> A step that would carry the variable past a
/// limit is folded back inside, as light off a mirror, and the variable keeps its physical units
/// throughout. The alternative - mapping the bounded interval onto an unbounded internal
/// coordinate through a sigmoid - has a gradient that vanishes as the bound is approached, so a
/// variable pressed against a limit stops responding to the optimiser and stays stuck there even
/// when the design later wants it back. Under reflection the gradient keeps its magnitude
/// everywhere, which is what a constrained or stochastic search needs. See
/// <see cref="Reflection"/>.</para>
/// </summary>
public sealed class Variable
{
    public VariableKind Kind { get; init; }

    /// <summary>Surface this variable belongs to, indexed as the prescription indexes it.</summary>
    public int Surface { get; init; }

    /// <summary>Lower limit, or negative infinity for none.</summary>
    public double Min { get; init; } = double.NegativeInfinity;

    /// <summary>Upper limit, or positive infinity for none.</summary>
    public double Max { get; init; } = double.PositiveInfinity;

    /// <summary>True when either limit is finite, so reflection has something to fold against.</summary>
    public bool IsBounded => !double.IsNegativeInfinity(Min) || !double.IsPositiveInfinity(Max);

    /// <summary>
    /// A name that reads back as what it is: <c>CV3</c>, <c>TH2</c>. This is what the merit file
    /// writes and what a report column is headed with.
    /// </summary>
    public string Name => Kind switch
    {
        VariableKind.Curvature => "CV" + Surface.ToString(CultureInfo.InvariantCulture),
        VariableKind.Thickness => "TH" + Surface.ToString(CultureInfo.InvariantCulture),
        _ => "?" + Surface.ToString(CultureInfo.InvariantCulture),
    };

    public override string ToString() => Name;
}
