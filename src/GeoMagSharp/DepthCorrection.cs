/****************************************************************************
 * File:            DepthCorrection.cs
 * Description:     Dipole depth correction per SPE-128217-MS
 * Author:          Christopher Strecker
 * Website:         https://github.com/StreckerCM/GeoMagSharp
 * Notes:           Reference: SPE-128217-MS (Ekseth & Weston, Gyrodata, 2010)
 *                  "Wellbore Positions Obtained While Drilling by the Most
 *                  Advanced Magnetic Surveying Methods May Be Less Accurate
 *                  than Predicted"
 ****************************************************************************/

using System;

namespace GeoMagSharp
{
    /// <summary>
    /// Calculates dipole depth corrections for geomagnetic field values
    /// per SPE-128217-MS (Ekseth &amp; Weston, 2010).
    /// </summary>
    public static class DepthCorrection
    {
        private const double SingularityThreshold = 0.1;
        private const double HorizontalIntensityPoleThreshold = 1.0; // nT
        private const double DegToRad = Math.PI / 180.0;
        private const double RadToDeg = 180.0 / Math.PI;

        /// <summary>
        /// Calculate depth correction from surface field values using dipole approximation.
        /// </summary>
        /// <param name="horizontalIntensityNT">Surface horizontal intensity B_h (nT)</param>
        /// <param name="verticalIntensityNT">Surface vertical intensity B_v (nT, positive downward)</param>
        /// <param name="totalFieldNT">Surface total field F (nT)</param>
        /// <param name="depthMeters">Survey depth below surface (meters, must be >= 0)</param>
        /// <param name="wellboreAzimuthDeg">Magnetic azimuth (degrees, 0-360). Null to skip Eq 5-8.</param>
        /// <param name="wellboreInclinationDeg">Wellbore inclination (degrees, 0-180). Null to skip Eq 5-8.</param>
        /// <param name="earthRadiusKm">Earth radius (km). Default: Constants.EarthsRadiusInKm</param>
        public static DepthCorrectionResult Calculate(
            double horizontalIntensityNT,
            double verticalIntensityNT,
            double totalFieldNT,
            double depthMeters,
            double? wellboreAzimuthDeg = null,
            double? wellboreInclinationDeg = null,
            double earthRadiusKm = Constants.EarthsRadiusInKm)
        {
            if (depthMeters < 0)
                throw new ArgumentOutOfRangeException(nameof(depthMeters), "Depth must be >= 0");
            if (earthRadiusKm <= 0)
                throw new ArgumentOutOfRangeException(nameof(earthRadiusKm), "Earth radius must be > 0");
            if (wellboreInclinationDeg.HasValue && (wellboreInclinationDeg.Value < 0 || wellboreInclinationDeg.Value > 180))
                throw new ArgumentOutOfRangeException(nameof(wellboreInclinationDeg), "Inclination must be 0-180 degrees");

            double earthRadiusM = earthRadiusKm * 1000.0;

            // Geomagnetic latitude: φ = atan(Bv / (2·Bh))
            double geomagLatRad;
            if (Math.Abs(horizontalIntensityNT) < HorizontalIntensityPoleThreshold)
            {
                geomagLatRad = Math.PI / 2.0; // 90° at magnetic pole
            }
            else
            {
                geomagLatRad = Math.Atan2(verticalIntensityNT, 2.0 * horizontalIntensityNT);
            }

            double cosPhi = Math.Cos(geomagLatRad);
            double sinPhi = Math.Sin(geomagLatRad);

            // Equatorial dipole field: B₀ = Bh / cos(φ)
            double B0 = Math.Abs(cosPhi) > 1e-10
                ? horizontalIntensityNT / cosPhi
                : verticalIntensityNT / (2.0 * sinPhi);

            // Dipole scaling factor: R³/(R-D)³
            double scalingFactor = Math.Pow(earthRadiusM / (earthRadiusM - depthMeters), 3);

            // Field at depth (Eq 1-2)
            double bhAtDepth = horizontalIntensityNT * scalingFactor;
            double bvAtDepth = verticalIntensityNT * scalingFactor;
            double fAtDepth = totalFieldNT * scalingFactor;

            // Field errors (Eq 3-4): first-order approximation
            double dOverR = depthMeters / earthRadiusM;
            double deltaH = 3.0 * B0 * cosPhi * dOverR;
            double deltaV = 6.0 * B0 * sinPhi * dOverR;

            var result = new DepthCorrectionResult
            {
                DipoleScalingFactor = scalingFactor,
                HorizontalIntensityAtDepth = bhAtDepth,
                VerticalIntensityAtDepth = bvAtDepth,
                TotalFieldAtDepth = fAtDepth,
                HorizontalError = deltaH,
                VerticalError = deltaV,
                GeomagneticLatitudeDeg = geomagLatRad * RadToDeg,
                DepthMeters = depthMeters,
                Reference = "SPE-128217-MS"
            };

            // Tool-frame errors and azimuth error (Eq 5-8) — requires wellbore geometry
            if (wellboreAzimuthDeg.HasValue && wellboreInclinationDeg.HasValue)
            {
                double A = (wellboreAzimuthDeg.Value % 360.0) * DegToRad;
                double I = wellboreInclinationDeg.Value * DegToRad;

                double cosA = Math.Cos(A);
                double sinA = Math.Sin(A);
                double cosI = Math.Cos(I);
                double sinI = Math.Sin(I);

                // Eq 5: ΔB_H (high-side)
                result.HighSideError = 3.0 * B0 * (cosPhi * cosA * cosI - 2.0 * sinPhi * sinI) * dOverR;

                // Eq 6: ΔB_R (high-side-right)
                result.HighSideRightError = -3.0 * B0 * cosPhi * sinA * dOverR;

                // Eq 7: ΔB_A (along-hole)
                result.AlongHoleError = 3.0 * B0 * (cosPhi * cosA * sinI + 2.0 * sinPhi * cosI) * dOverR;

                // Singularity factor: (1 - sin²A·sin²I)
                double singFactor = 1.0 - sinA * sinA * sinI * sinI;
                result.SingularityFactor = singFactor;
                result.NearSingularity = singFactor < SingularityThreshold;

                // Eq 8: ΔA (azimuth error)
                double sin2A = Math.Sin(2.0 * A);
                double sin2I = Math.Sin(2.0 * I);
                double tanPhi = Math.Abs(cosPhi) > 1e-10 ? sinPhi / cosPhi : 1e10;

                double numerator = (sin2A * sinI * sinI + 2.0 * tanPhi * sinA * sin2I) * 1.5 * dOverR;
                double azErrorRad = Math.Abs(singFactor) > 1e-10
                    ? numerator / singFactor
                    : numerator / 1e-10; // Avoid division by zero

                result.AzimuthErrorDeg = azErrorRad * RadToDeg;
            }

            return result;
        }

        /// <summary>
        /// Convenience overload accepting MagneticCalculations directly.
        /// Extracts HorizontalIntensity, VerticalComp, and TotalField values.
        /// </summary>
        public static DepthCorrectionResult Calculate(
            MagneticCalculations surfaceField,
            double depthMeters,
            double? wellboreAzimuthDeg = null,
            double? wellboreInclinationDeg = null)
        {
            if (surfaceField == null)
                throw new ArgumentNullException(nameof(surfaceField));

            return Calculate(
                surfaceField.HorizontalIntensity.Value,
                surfaceField.VerticalComp.Value,
                surfaceField.TotalField.Value,
                depthMeters,
                wellboreAzimuthDeg,
                wellboreInclinationDeg);
        }
    }
}
