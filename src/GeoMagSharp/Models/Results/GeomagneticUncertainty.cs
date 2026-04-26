/****************************************************************************
 * File:            GeomagneticUncertainty.cs
 * Description:     ISCWSA-based geomagnetic uncertainty values
 * Author:          Christopher Strecker
 * Website:         https://github.com/StreckerCM/GeoMagSharp
 ****************************************************************************/

namespace GeoMagSharp
{
    /// <summary>
    /// ISCWSA-based 1-sigma geomagnetic uncertainty values for a model category.
    /// Values are from CDR-SM-03 Rev 8 (Copsegrove, 2013) Table 2.
    /// </summary>
    public class GeomagneticUncertainty
    {
        /// <summary>The ISCWSA model category these values apply to.</summary>
        public GeomagneticModelCategory ModelCategory { get; set; }

        /// <summary>Declination uncertainty in degrees, 1-sigma (ISCWSA DEC term).</summary>
        public double Declination { get; set; }

        /// <summary>
        /// Bh-dependent declination uncertainty in deg·nT, 1-sigma (ISCWSA DBH term).
        /// Effective declination error = BhDependentDec / Bh, where Bh is horizontal field intensity.
        /// </summary>
        public double BhDependentDec { get; set; }

        /// <summary>Total field intensity uncertainty in nT, 1-sigma (ISCWSA MFI term).</summary>
        public double TotalField { get; set; }

        /// <summary>
        /// Dip angle (inclination) uncertainty in degrees, 1-sigma (ISCWSA MDI term).
        /// Same physical quantity as MagneticCalculations.Inclination.
        /// </summary>
        public double DipAngle { get; set; }

        /// <summary>ISCWSA error model revision (e.g., "Rev5.13").</summary>
        public string Revision { get; set; }

        /// <summary>
        /// Depth-dependent azimuth uncertainty in degrees, 1-sigma.
        /// From SPE-128217-MS Monte Carlo analysis: σ_ΔA ≈ 0.38° global average.
        /// Null if depth correction was not applied.
        /// </summary>
        public double? DepthAzimuthUncertainty { get; set; }

        /// <summary>
        /// Per-point σ for declination in degrees, 1-sigma. Populated by HDGM and other
        /// models that provide location-specific uncertainty. Null if the model only
        /// provides global ISCWSA values (the existing <see cref="Declination"/> field).
        /// </summary>
        public double? SigmaD { get; set; }

        /// <summary>Per-point σ for inclination in degrees, 1-sigma. Null when not provided by the model.</summary>
        public double? SigmaI { get; set; }

        /// <summary>Per-point σ for horizontal intensity in nT, 1-sigma. Null when not provided by the model.</summary>
        public double? SigmaH { get; set; }

        /// <summary>Per-point σ for the X (north) component in nT, 1-sigma. Null when not provided by the model.</summary>
        public double? SigmaX { get; set; }

        /// <summary>Per-point σ for the Y (east) component in nT, 1-sigma. Null when not provided by the model.</summary>
        public double? SigmaY { get; set; }

        /// <summary>Per-point σ for the Z (down) component in nT, 1-sigma. Null when not provided by the model.</summary>
        public double? SigmaZ { get; set; }

        /// <summary>Per-point σ for total field intensity in nT, 1-sigma. Null when not provided by the model.</summary>
        public double? SigmaF { get; set; }

        /// <summary>
        /// True if the queried location is in a high-resolution survey-covered region
        /// (28 km half-wavelength, e.g., HDGM's NSD-covered areas).
        /// False for satellite-only fallback regions (~150 km half-wavelength).
        /// Null if the model does not provide per-location coverage information.
        /// </summary>
        public bool? HighResolutionCoverage { get; set; }

        /// <summary>
        /// Returns a new instance with all uncertainty values multiplied by the given scale factor.
        /// Note: This is a linear approximation. Geomagnetic errors follow a Laplacian
        /// (non-Gaussian) distribution, so scaled values are approximate at levels other than 1-sigma.
        /// </summary>
        /// <param name="scaleFactor">Multiplicative scale factor (e.g., 2.0 for approximate 2-sigma).</param>
        public GeomagneticUncertainty ScaleTo(double scaleFactor)
        {
            return new GeomagneticUncertainty
            {
                ModelCategory = ModelCategory,
                Declination = Declination * scaleFactor,
                BhDependentDec = BhDependentDec * scaleFactor,
                TotalField = TotalField * scaleFactor,
                DipAngle = DipAngle * scaleFactor,
                Revision = Revision,
                DepthAzimuthUncertainty = DepthAzimuthUncertainty.HasValue
                    ? DepthAzimuthUncertainty.Value * scaleFactor
                    : (double?)null,
                SigmaD = SigmaD.HasValue ? SigmaD.Value * scaleFactor : (double?)null,
                SigmaI = SigmaI.HasValue ? SigmaI.Value * scaleFactor : (double?)null,
                SigmaH = SigmaH.HasValue ? SigmaH.Value * scaleFactor : (double?)null,
                SigmaX = SigmaX.HasValue ? SigmaX.Value * scaleFactor : (double?)null,
                SigmaY = SigmaY.HasValue ? SigmaY.Value * scaleFactor : (double?)null,
                SigmaZ = SigmaZ.HasValue ? SigmaZ.Value * scaleFactor : (double?)null,
                SigmaF = SigmaF.HasValue ? SigmaF.Value * scaleFactor : (double?)null,
                HighResolutionCoverage = HighResolutionCoverage  // bool flag — not scaled
            };
        }
    }
}
