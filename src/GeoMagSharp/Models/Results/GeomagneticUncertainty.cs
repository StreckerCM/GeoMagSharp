/****************************************************************************
 * File:            GeomagneticUncertainty.cs
 * Description:     Geomagnetic uncertainty values (ISCWSA or WMM error model)
 * Author:          Christopher Strecker
 * Website:         https://github.com/StreckerCM/GeoMagSharp
 ****************************************************************************/

namespace GeoMagSharp
{
    /// <summary>
    /// 1-sigma geomagnetic uncertainty values from either ISCWSA Level 1 or WMM native error model.
    /// </summary>
    public class GeomagneticUncertainty
    {
        /// <summary>The ISCWSA model category these values apply to.</summary>
        public GeomagneticModelCategory ModelCategory { get; set; }

        /// <summary>Identifies which uncertainty model produced these values.</summary>
        public UncertaintySource Source { get; set; }

        /// <summary>Declination uncertainty in degrees, 1-sigma (ISCWSA DEC term).</summary>
        public double Declination { get; set; }

        /// <summary>
        /// Bh-dependent declination uncertainty in deg·nT, 1-sigma (ISCWSA DBH term).
        /// Effective declination error = BhDependentDec / Bh, where Bh is horizontal field intensity.
        /// For WMM error model, this is 0 because declination uncertainty is computed directly.
        /// </summary>
        public double BhDependentDec { get; set; }

        /// <summary>Total field intensity uncertainty in nT, 1-sigma (ISCWSA MFI term / WMM δF).</summary>
        public double TotalField { get; set; }

        /// <summary>
        /// Dip angle (inclination) uncertainty in degrees, 1-sigma (ISCWSA MDI term / WMM δI).
        /// Same physical quantity as MagneticCalculations.Inclination.
        /// </summary>
        public double DipAngle { get; set; }

        /// <summary>North component (X) uncertainty in nT, 1-sigma. Null for ISCWSA source.</summary>
        public double? NorthIntensity { get; set; }

        /// <summary>East component (Y) uncertainty in nT, 1-sigma. Null for ISCWSA source.</summary>
        public double? EastIntensity { get; set; }

        /// <summary>Vertical component (Z) uncertainty in nT, 1-sigma. Null for ISCWSA source.</summary>
        public double? VerticalIntensity { get; set; }

        /// <summary>Horizontal intensity (H) uncertainty in nT, 1-sigma. Null for ISCWSA source.</summary>
        public double? HorizontalIntensity { get; set; }

        /// <summary>ISCWSA error model revision (e.g., "Rev5.13") or WMM error model source.</summary>
        public string Revision { get; set; }

        /// <summary>
        /// Depth-dependent azimuth uncertainty in degrees, 1-sigma.
        /// From SPE-128217-MS Monte Carlo analysis: σ_ΔA ≈ 0.38° global average.
        /// Null if depth correction was not applied.
        /// </summary>
        public double? DepthAzimuthUncertainty { get; set; }

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
                Source = Source,
                Declination = Declination * scaleFactor,
                BhDependentDec = BhDependentDec * scaleFactor,
                TotalField = TotalField * scaleFactor,
                DipAngle = DipAngle * scaleFactor,
                NorthIntensity = NorthIntensity.HasValue ? NorthIntensity.Value * scaleFactor : (double?)null,
                EastIntensity = EastIntensity.HasValue ? EastIntensity.Value * scaleFactor : (double?)null,
                VerticalIntensity = VerticalIntensity.HasValue ? VerticalIntensity.Value * scaleFactor : (double?)null,
                HorizontalIntensity = HorizontalIntensity.HasValue ? HorizontalIntensity.Value * scaleFactor : (double?)null,
                Revision = Revision,
                DepthAzimuthUncertainty = DepthAzimuthUncertainty.HasValue
                    ? DepthAzimuthUncertainty.Value * scaleFactor
                    : (double?)null
            };
        }
    }
}
