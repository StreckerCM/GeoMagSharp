/****************************************************************************
 * File:            GeomagneticUncertainty.cs
 * Description:     Geomagnetic uncertainty values (ISCWSA or WMM error model)
 * Author:          Christopher Strecker
 * Website:         https://github.com/StreckerCM/GeoMagSharp
 ****************************************************************************/

using System;

namespace GeoMagSharp
{
    /// <summary>
    /// 1-sigma geomagnetic uncertainty values from either ISCWSA Level 1 or WMM native error model.
    /// Instances are created by the library and are effectively immutable to external consumers.
    /// </summary>
    public class GeomagneticUncertainty
    {
        /// <summary>The ISCWSA model category these values apply to.</summary>
        public GeomagneticModelCategory ModelCategory { get; internal set; }

        /// <summary>Identifies which uncertainty model produced these values.</summary>
        public UncertaintySource Source { get; internal set; }

        /// <summary>
        /// Declination uncertainty in degrees, 1-sigma (ISCWSA DEC term / WMM δD).
        /// When the WMM error model is used at H=0 (magnetic dip pole), this is set to 999.0
        /// to indicate that declination is undefined. Check for values >= 999.0 to detect this case.
        /// </summary>
        public double Declination { get; internal set; }

        /// <summary>
        /// Bh-dependent declination uncertainty in deg·nT, 1-sigma (ISCWSA DBH term).
        /// Effective declination error = BhDependentDec / Bh, where Bh is horizontal field intensity.
        /// For WMM error model, this is 0 because declination uncertainty is computed directly.
        /// </summary>
        public double BhDependentDec { get; internal set; }

        /// <summary>Total field intensity uncertainty in nT, 1-sigma (ISCWSA MFI term / WMM δF).</summary>
        public double TotalField { get; internal set; }

        /// <summary>
        /// Inclination uncertainty in degrees, 1-sigma (ISCWSA MDI term / WMM δI).
        /// Same physical quantity as <see cref="MagneticCalculations.Inclination"/>.
        /// </summary>
        public double Inclination { get; internal set; }

        /// <summary>
        /// Dip angle (inclination) uncertainty in degrees, 1-sigma.
        /// </summary>
        [Obsolete("Use Inclination instead. DipAngle will be removed in a future version.")]
        public double DipAngle
        {
            get { return Inclination; }
            set { Inclination = value; }
        }

        /// <summary>North component (X) uncertainty in nT, 1-sigma. Null for ISCWSA source.</summary>
        public double? NorthIntensity { get; internal set; }

        /// <summary>East component (Y) uncertainty in nT, 1-sigma. Null for ISCWSA source.</summary>
        public double? EastIntensity { get; internal set; }

        /// <summary>Vertical component (Z) uncertainty in nT, 1-sigma. Null for ISCWSA source.</summary>
        public double? VerticalIntensity { get; internal set; }

        /// <summary>Horizontal intensity (H) uncertainty in nT, 1-sigma. Null for ISCWSA source.</summary>
        public double? HorizontalIntensity { get; internal set; }

        /// <summary>ISCWSA error model revision (e.g., "Rev5.13") or WMM error model source.</summary>
        public string Revision { get; internal set; }

        /// <summary>
        /// Depth-dependent azimuth uncertainty in degrees, 1-sigma.
        /// From SPE-128217-MS Monte Carlo analysis: sigma_DeltaA = 0.38 deg global average.
        /// Null if depth correction was not applied.
        /// </summary>
        public double? DepthAzimuthUncertainty { get; internal set; }

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
                Inclination = Inclination * scaleFactor,
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
