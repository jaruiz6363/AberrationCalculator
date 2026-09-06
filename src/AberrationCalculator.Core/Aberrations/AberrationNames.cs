using System.Collections.Generic;

namespace AberrationCalculator.Core.Aberrations;

/// <summary>
/// What each coefficient is called in the literature and in other design programs.
///
/// The coefficients this program reports carry Rimmer's names - B, F, C, Pi, B5, M1 and
/// so on - which say nothing about what the aberration IS. A designer looking at a table
/// of eighteen two-character labels cannot see that M1, M2 and M3 are oblique spherical
/// aberration, which is the term Buchdahl singled out as the hardest to control and the
/// usual reason a design predicts badly in the outer field.
///
/// The names come from R. B. Johnson, "Polynomial Ray Aberrations Computed in Various
/// Lens Design Programs," <i>Appl. Opt.</i> <b>12</b>, 2079-2082 (1973), Table I, which
/// tabulates the definition each of six programs used. Robb cites it as the standard
/// nomenclature. Johnson writes in Buchdahl's sigma/mu/tau; the bridge to the Rimmer
/// names used here is the one set out in <see cref="Prms"/>.
///
/// <para><b>Several named aberrations are combinations, not single coefficients.</b>
/// Tangential oblique spherical is mu4 + mu6, which is M1 + M2 + M3; fifth-order linear
/// coma is mu2 + mu3, which is F1 + F2. Where that is so, the name given here identifies
/// the family the coefficient belongs to rather than claiming the coefficient alone is
/// the named quantity. Johnson's own finding is the reason to be careful about this: he
/// compared six programs and found "significant variances in term definitions", so a
/// named aberration is only meaningful alongside its definition.</para>
/// </summary>
public static class AberrationNames
{
    private static readonly Dictionary<string, string> Names = new()
    {
        // Third order (Buchdahl's sigma1..sigma5).
        ["B"]   = "spherical",
        ["F"]   = "linear coma",
        ["C"]   = "astigmatism",
        ["Pi"]  = "Petzval field curvature",
        ["E"]   = "distortion",

        // Fifth order (mu1..mu12).
        ["B5"]  = "spherical",
        ["F1"]  = "linear coma (with F2)",
        ["F2"]  = "linear coma (with F1)",
        ["M1"]  = "oblique spherical, tangential (with M2, M3)",
        ["M2"]  = "oblique spherical, sagittal",
        ["M3"]  = "oblique spherical, tangential (with M1, M2)",
        ["N1"]  = "elliptical coma, tangential (with N2)",
        ["N2"]  = "elliptical coma, tangential (with N1)",
        ["N3"]  = "elliptical coma, oblique",
        ["C5"]  = "astigmatism (with Pi5)",
        ["Pi5"] = "Petzval / astigmatism (with C5)",
        ["E5"]  = "distortion",

        // Seventh order (tau1). The rest of the tertiary set is not computed here.
        ["B7"]  = "spherical",
    };

    private static readonly Dictionary<string, int> Orders = new()
    {
        ["B"] = 3, ["F"] = 3, ["C"] = 3, ["Pi"] = 3, ["E"] = 3,
        ["B5"] = 5, ["F1"] = 5, ["F2"] = 5, ["M1"] = 5, ["M2"] = 5, ["M3"] = 5,
        ["N1"] = 5, ["N2"] = 5, ["N3"] = 5, ["C5"] = 5, ["Pi5"] = 5, ["E5"] = 5,
        ["B7"] = 7,
    };

    /// <summary>The aberration this coefficient belongs to, with its order. Empty if unknown.</summary>
    public static string Describe(string coefficient)
    {
        if (!Names.TryGetValue(coefficient, out var name)) return "";
        string ordinal = Orders.TryGetValue(coefficient, out int o)
            ? o switch { 3 => "3rd", 5 => "5th", 7 => "7th", _ => o + "th" }
            : "";
        return ordinal.Length > 0 ? ordinal + " " + name : name;
    }

    /// <summary>Buchdahl's order for this coefficient: 3, 5 or 7. Zero if unknown.</summary>
    public static int Order(string coefficient) => Orders.TryGetValue(coefficient, out int o) ? o : 0;
}
