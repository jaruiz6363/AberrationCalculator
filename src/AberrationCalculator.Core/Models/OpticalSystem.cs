using System;
using System.Collections.Generic;

using AberrationCalculator.Core.Enums;

namespace AberrationCalculator.Core.Models;

/// <summary>A wavelength in the evaluation set.</summary>
public class Wavelength
{
    /// <summary>Wavelength in micrometres.</summary>
    public double Value { get; set; }

    /// <summary>Relative weight when several wavelengths are combined.</summary>
    public double Weight { get; set; } = 1.0;

    /// <summary>True for the reference wavelength.</summary>
    public bool IsPrimary { get; set; }

    public Wavelength() { }

    public Wavelength(double um, double weight = 1.0, bool isPrimary = false)
    {
        Value = um;
        Weight = weight;
        IsPrimary = isPrimary;
    }
}

/// <summary>A field point. Y is the meridional coordinate; X is carried but unused here.</summary>
public class Field
{
    public double Y { get; set; }
    public double X { get; set; }
    public double Weight { get; set; } = 1.0;

    public Field() { }

    public Field(double y, double weight = 1.0)
    {
        Y = y;
        Weight = weight;
    }
}

/// <summary>How the system aperture is specified, and its value.</summary>
public class Aperture
{
    public ApertureType Type { get; set; } = ApertureType.EPD;
    public double Value { get; set; }

    public Aperture() { }

    public Aperture(ApertureType type, double value)
    {
        Type = type;
        Value = value;
    }
}

/// <summary>
/// A surface parameter driven from another surface: target = scale x source + offset.
/// Formats use these to keep a cemented pair's shared surface, or a mirror's radius,
/// tied together. Applied on load so the printed prescription is the resolved lens.
/// </summary>
public class Pickup
{
    public int TargetSurfaceIndex { get; set; }
    public int SourceSurfaceIndex { get; set; }
    public PickupParameter Parameter { get; set; } = PickupParameter.Thickness;
    public double ScaleFactor { get; set; } = 1.0;
    public double Offset { get; set; }

    /// <summary>Which numbered parameter, when <see cref="Parameter"/> addresses one.</summary>
    public int ParameterIndex { get; set; }

    /// <summary>Source configuration for multi-configuration files; -1 = this one.</summary>
    public int SourceConfigurationIndex { get; set; } = -1;
}

/// <summary>
/// A complete lens: surfaces, wavelengths, fields and how the aperture is defined.
///
/// Surface 0 is the object and the last surface is the image, matching every format this
/// program reads, so a surface index means the same thing here as in the source file.
/// </summary>
public class OpticalSystem
{
    public string Title { get; set; } = string.Empty;
    public string Designer { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;

    public List<Surface> Surfaces { get; set; } = new();
    public List<Wavelength> Wavelengths { get; set; } = new();
    public List<Field> Fields { get; set; } = new();

    public Aperture Aperture { get; set; } = new(ApertureType.EPD, 0.0);

    public FieldType FieldType { get; set; } = FieldType.ObjectAngle;

    /// <summary>Catalogs named by the file, in preference order.</summary>
    public List<string> GlassCatalogs { get; set; } = new();

    public List<Pickup> Pickups { get; set; } = new();

    /// <summary>Afocal systems are measured in angle rather than length at the image.</summary>
    public bool IsAfocal { get; set; }

    public bool TelecentricObjectSpace { get; set; }

    /// <summary>
    /// Whether the source file aimed rays at the stop. Carried for the prescription
    /// printout; the aberration coefficients here are computed from the paraxial
    /// pupil and do not depend on it.
    /// </summary>
    public RayAimingMode RayAiming { get; set; } = RayAimingMode.Off;

    /// <summary>How the file said its Auto semi-diameters were derived.</summary>
    public SemiDiameterSolve SemiDiameterSolve { get; set; } = SemiDiameterSolve.RealRay;

    /// <summary>Index of the reference wavelength, or −1 when none is marked.</summary>
    public int PrimaryWavelengthIndex
    {
        get
        {
            for (int i = 0; i < Wavelengths.Count; i++)
                if (Wavelengths[i].IsPrimary) return i;
            return Wavelengths.Count > 0 ? 0 : -1;
        }
    }

    /// <summary>Index of the stop surface, or −1 when no surface is marked.</summary>
    public int StopSurfaceIndex
    {
        get
        {
            for (int i = 0; i < Surfaces.Count; i++)
                if (Surfaces[i].IsStop) return i;
            return -1;
        }
    }

    /// <summary>Largest field magnitude, used to normalise fractional field heights.</summary>
    public double MaxFieldY()
    {
        double m = 0.0;
        foreach (var f in Fields) m = Math.Max(m, Math.Abs(f.Y));
        return m;
    }

    /// <summary>Index of the image surface: the last one in the list.</summary>
    public int ImageSurfaceIndex => Surfaces.Count - 1;

    /// <summary>
    /// Index of the last surface a ray meets before the image plane.
    ///
    /// Note this is NOT the last surface carrying a material: the exit face of the final
    /// element has air after it, so a "last surface with glass" test lands one surface
    /// short and every quantity referred to the final surface - focal length, back focus,
    /// exit pupil - comes out wrong. Trailing dummy surfaces have no power, so counting
    /// back from the image is both simpler and right.
    /// </summary>
    public int LastOpticalSurface() => Math.Max(0, Surfaces.Count - 2);
}
