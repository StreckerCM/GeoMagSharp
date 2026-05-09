/****************************************************************************
 * File:            GeomagneticUncertainty.cs
 * Description:     1-sigma uncertainty values for a magnetic calculation
 * Author:          Christopher Strecker
 * Website:         https://github.com/StreckerCM/GeoMagSharp
 ****************************************************************************/

using System;

namespace GeoMagSharp
{
    /// <summary>
    /// 1-sigma uncertainty values for a single magnetic-field calculation.
    /// Field names mirror <see cref="MagneticCalculations"/> exactly: the
    /// uncertainty in <c>result.Inclination</c> is <c>result.Uncertainty.Inclination</c>.
    /// The <see cref="Source"/> property identifies which uncertainty model
    /// produced these values (ISCWSA Level 1, WMM native error model, or HDGM
    /// per-point sigmas).
    /// </summary>
    /// <remarks>
    /// Coverage by source:
    /// <list type="bullet">
    /// <item>
    /// <description><b>ISCWSA</b> populates <see cref="Declination"/>, <see cref="Inclination"/>,
    /// <see cref="TotalField"/>, and <see cref="BhDependentDec"/>; component fields stay null.</description>
    /// </item>
    /// <item>
    /// <description><b>WMM error model</b> and <b>HDGM</b> populate all seven field
    /// uncertainties; <see cref="BhDependentDec"/> is 0 (location-dependent declination
    /// is computed directly).</description>
    /// </item>
    /// </list>
    /// Instances are created by the library and are effectively immutable to external
    /// consumers (setters are <c>internal</c>).
    /// </remarks>
    public class GeomagneticUncertainty
    {
        /// <summary>Identifies which uncertainty model produced these values.</summary>
        public UncertaintySource Source { get; internal set; }

        /// <summary>The ISCWSA model category these values apply to (when <see cref="Source"/> is <see cref="UncertaintySource.Iscwsa"/>).</summary>
        public GeomagneticModelCategory ModelCategory { get; internal set; }

        /// <summary>
        /// Source revision/identifier — e.g. <c>"Rev5.13"</c> for ISCWSA,
        /// <c>"WMM2025-TR"</c> for WMM error model, or the HDGM filename for HDGM.
        /// </summary>
        public string Revision { get; internal set; }

        // ─── Field uncertainties (mirror MagneticCalculations field names) ───

        /// <summary>
        /// Declination σ in degrees, 1-sigma. Always populated. For WMM the value is
        /// location-dependent: <c>δD = √(C₁² + (C₂/H)²)</c>. At H=0 (magnetic dip pole)
        /// the WMM error model returns 999.0 to indicate declination is undefined.
        /// </summary>
        public double Declination { get; internal set; }

        /// <summary>Inclination σ in degrees, 1-sigma. Always populated.</summary>
        public double Inclination { get; internal set; }

        /// <summary>Total field σ in nT, 1-sigma. Always populated.</summary>
        public double TotalField { get; internal set; }

        /// <summary>Horizontal intensity (H) σ in nT, 1-sigma. Null when <see cref="Source"/> is <see cref="UncertaintySource.Iscwsa"/> (not provided by ISCWSA Level 1).</summary>
        public double? HorizontalIntensity { get; internal set; }

        /// <summary>North component (X) σ in nT, 1-sigma. Null when <see cref="Source"/> is <see cref="UncertaintySource.Iscwsa"/>.</summary>
        public double? NorthComp { get; internal set; }

        /// <summary>East component (Y) σ in nT, 1-sigma. Null when <see cref="Source"/> is <see cref="UncertaintySource.Iscwsa"/>.</summary>
        public double? EastComp { get; internal set; }

        /// <summary>Vertical component (Z) σ in nT, 1-sigma. Null when <see cref="Source"/> is <see cref="UncertaintySource.Iscwsa"/>.</summary>
        public double? VerticalComp { get; internal set; }

        // ─── Source-specific extras ───

        /// <summary>
        /// ISCWSA Bh-dependent declination coefficient in deg·nT. Effective declination
        /// error per ISCWSA = <c>√(Declination² + (BhDependentDec/H)²)</c>. Zero for
        /// WMM and HDGM (those sources populate <see cref="Declination"/> with the
        /// already location-aware value).
        /// </summary>
        public double BhDependentDec { get; internal set; }

        /// <summary>
        /// True if the queried location is in a high-resolution survey-covered region
        /// (HDGM NSD-covered areas at ~28 km half-wavelength). False for satellite
        /// fallback regions (~150 km half-wavelength). Null when not provided
        /// (only HDGM populates this).
        /// </summary>
        public bool? HighResolutionCoverage { get; internal set; }

        /// <summary>
        /// Depth-dependent azimuth uncertainty in degrees, 1-sigma — from SPE-128217-MS
        /// Monte Carlo analysis (σ_ΔA ≈ 0.38° global average). Null when depth correction
        /// was not requested.
        /// </summary>
        public double? DepthAzimuthUncertainty { get; internal set; }

        // ─── Deprecated bridge ───

        /// <summary>
        /// Deprecated alias for <see cref="Inclination"/>. Use <see cref="Inclination"/>
        /// to match the value-side naming in <see cref="MagneticCalculations.Inclination"/>.
        /// </summary>
        [Obsolete("Use Inclination instead. DipAngle will be removed in 2.0.0.")]
        public double DipAngle
        {
            get { return Inclination; }
            set { Inclination = value; }
        }

        /// <summary>
        /// Returns a new instance with all field uncertainties multiplied by the given
        /// scale factor. <see cref="HighResolutionCoverage"/> (a boolean flag) is copied
        /// unchanged. Note that geomagnetic errors follow a Laplacian (non-Gaussian)
        /// distribution, so scaled values are approximate at sigma levels other than 1.
        /// </summary>
        /// <param name="scaleFactor">Multiplicative scale factor (e.g. 2.0 for ~2-sigma).</param>
        public GeomagneticUncertainty ScaleTo(double scaleFactor)
        {
            return new GeomagneticUncertainty
            {
                Source = Source,
                ModelCategory = ModelCategory,
                Revision = Revision,
                Declination = Declination * scaleFactor,
                Inclination = Inclination * scaleFactor,
                TotalField = TotalField * scaleFactor,
                HorizontalIntensity = HorizontalIntensity.HasValue ? HorizontalIntensity.Value * scaleFactor : (double?)null,
                NorthComp = NorthComp.HasValue ? NorthComp.Value * scaleFactor : (double?)null,
                EastComp = EastComp.HasValue ? EastComp.Value * scaleFactor : (double?)null,
                VerticalComp = VerticalComp.HasValue ? VerticalComp.Value * scaleFactor : (double?)null,
                BhDependentDec = BhDependentDec * scaleFactor,
                HighResolutionCoverage = HighResolutionCoverage,
                DepthAzimuthUncertainty = DepthAzimuthUncertainty.HasValue
                    ? DepthAzimuthUncertainty.Value * scaleFactor
                    : (double?)null
            };
        }
    }
}
