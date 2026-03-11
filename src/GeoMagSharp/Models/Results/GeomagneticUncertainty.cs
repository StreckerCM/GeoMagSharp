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
                Revision = Revision
            };
        }
    }
}
