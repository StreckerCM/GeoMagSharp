/****************************************************************************
 * File:            HDGMCalculationAdapter.cs
 * Description:     Per-call adapter mapping the NOAA HDGM outData array
 *                  into MagneticCalculations + GeomagneticUncertainty fields
 * Author:          Christopher Strecker
 * Website:         https://github.com/StreckerCM/GeoMagSharp
 ****************************************************************************/

using System;

namespace GeoMagSharp.HDGM
{
    /// <summary>
    /// Per-call adapter that invokes <see cref="INativeHdgmInvoker"/> and maps the 25-element
    /// native outData array to a <see cref="MagneticCalculations"/> result with per-point
    /// uncertainty fields populated.
    /// </summary>
    /// <remarks>
    /// IMPORTANT: outData index 16 carries the NSD high-resolution coverage flag in the
    /// DLL output (HDGM_Sublibrary.c:212 — IsNotCovered: 0 = covered, 1 = fallback).
    /// The CLI variant of NOAA's source (hdgm_file.c:204) overwrites slot 16 with UsePomme;
    /// we use the DLL semantics.
    /// </remarks>
    internal static class HDGMCalculationAdapter
    {
        private const double Sentinel = -99999.0;

        /// <summary>
        /// Calls the native HDGM invoker with the given options and date, and maps
        /// the 25-element outData array to a <see cref="MagneticCalculations"/> result.
        /// </summary>
        /// <param name="opts">Calculation options (lat, lon, elevation).</param>
        /// <param name="intervalDate">Date for which to calculate the field.</param>
        /// <param name="invoker">Native HDGM invoker (real or fake for tests).</param>
        /// <returns>Populated <see cref="MagneticCalculations"/> including per-point uncertainty.</returns>
        /// <exception cref="ArgumentNullException">Thrown if opts or invoker is null.</exception>
        /// <exception cref="GeoMagExceptionOutOfRange">
        /// Thrown when outData[0] == -99999, which indicates the queried location or date
        /// is outside the HDGM model's coverage (e.g., date beyond the loaded DLL's epoch).
        /// </exception>
        public static MagneticCalculations Calculate(
            CalculationOptions opts,
            DateTime intervalDate,
            INativeHdgmInvoker invoker)
        {
            if (opts == null) throw new ArgumentNullException(nameof(opts));
            if (invoker == null) throw new ArgumentNullException(nameof(invoker));

            double depthMeters = opts.DepthInM;       // positive for depth, negative for altitude
            double decimalYear = intervalDate.ToDecimal();

            // Defensive sanitization before crossing the native boundary.
            // NaN/Infinity in any of these inputs can trigger undefined behavior
            // in the NOAA HDGM C code, potentially crashing the hosting process.
            if (double.IsNaN(opts.Latitude) || double.IsInfinity(opts.Latitude) ||
                opts.Latitude < -90.0 || opts.Latitude > 90.0)
            {
                throw new GeoMagExceptionOutOfRange(string.Format(
                    "Error: Latitude {0} is invalid (must be a finite value in [-90, 90]).", opts.Latitude));
            }
            if (double.IsNaN(opts.Longitude) || double.IsInfinity(opts.Longitude) ||
                opts.Longitude < -180.0 || opts.Longitude > 180.0)
            {
                throw new GeoMagExceptionOutOfRange(string.Format(
                    "Error: Longitude {0} is invalid (must be a finite value in [-180, 180]).", opts.Longitude));
            }
            if (double.IsNaN(depthMeters) || double.IsInfinity(depthMeters))
            {
                throw new GeoMagExceptionOutOfRange("Error: depth/elevation must be a finite value.");
            }
            if (double.IsNaN(decimalYear) || double.IsInfinity(decimalYear) ||
                decimalYear < 1.0 || decimalYear > 10000.0)
            {
                throw new GeoMagExceptionOutOfRange(string.Format(
                    "Error: Date {0} (decimal year) is invalid.", decimalYear));
            }

            var outData = new double[25];
            int status = invoker.Calculate(opts.Latitude, opts.Longitude, depthMeters, decimalYear, outData);

            if (status != 0)
            {
                throw new GeoMagExceptionOutOfRange(string.Format(
                    "Error: HDGM native call returned non-zero status {0} for date {1:yyyy-MM-dd} at lat {2}, lon {3}.",
                    status, intervalDate, opts.Latitude, opts.Longitude));
            }

            if (outData[0] == Sentinel)
            {
                throw new GeoMagExceptionOutOfRange(string.Format(
                    "Error: HDGM returned out-of-range result for date {0:yyyy-MM-dd} at lat {1}, lon {2}. " +
                    "The loaded HDGM version may not cover this date or location is invalid.",
                    intervalDate, opts.Latitude, opts.Longitude));
            }

            // outData layout (25 elements):
            //   [0]  D  — Declination (degrees)
            //   [1]  I  — Inclination / dip angle (degrees)
            //   [2]  F  — Total field intensity (nT)
            //   [3]  H  — Horizontal intensity (nT)
            //   [4]  X  — North component (nT)
            //   [5]  Y  — East component (nT)
            //   [6]  Z  — Vertical/down component (nT)
            //   [7]  GV — Grid Variation (degrees) — DISCARDED (not in MagneticCalculations)
            //   [8]  dD/dt  — Secular variation of D (degrees/yr)
            //   [9]  dI/dt  — Secular variation of I (degrees/yr)
            //  [10]  dF/dt  — Secular variation of F (nT/yr)
            //  [11]  dH/dt  — Secular variation of H (nT/yr)
            //  [12]  dX/dt  — Secular variation of X (nT/yr)
            //  [13]  dY/dt  — Secular variation of Y (nT/yr)
            //  [14]  dZ/dt  — Secular variation of Z (nT/yr)
            //  [15]  dGV/dt — Secular variation of GV — DISCARDED
            //  [16]  IsNotCovered (DLL: 0 = high-res NSD covered, 1 = satellite fallback)
            //         NOTE: CLI source hdgm_file.c:204 overwrites this with UsePomme — we use DLL semantics
            //  [17]  σD  — Per-point 1-sigma declination uncertainty (degrees)
            //  [18]  σI  — Per-point 1-sigma inclination uncertainty (degrees)
            //  [19]  σH  — Per-point 1-sigma horizontal intensity uncertainty (nT)
            //  [20]  σX  — Per-point 1-sigma north component uncertainty (nT)
            //  [21]  σY  — Per-point 1-sigma east component uncertainty (nT)
            //  [22]  σZ  — Per-point 1-sigma vertical component uncertainty (nT)
            //  [23]  σF  — Per-point 1-sigma total field uncertainty (nT)
            //  [24]  UsePomme HDGM-RT flag — DISCARDED (out of scope for v1)

            var result = new MagneticCalculations
            {
                Date = intervalDate,
                Declination        = new MagneticValue { Value = outData[0],  ChangePerYear = outData[8]  },
                Inclination        = new MagneticValue { Value = outData[1],  ChangePerYear = outData[9]  },
                TotalField         = new MagneticValue { Value = outData[2],  ChangePerYear = outData[10] },
                HorizontalIntensity = new MagneticValue { Value = outData[3], ChangePerYear = outData[11] },
                NorthComp          = new MagneticValue { Value = outData[4],  ChangePerYear = outData[12] },
                EastComp           = new MagneticValue { Value = outData[5],  ChangePerYear = outData[13] },
                VerticalComp       = new MagneticValue { Value = outData[6],  ChangePerYear = outData[14] },
                Uncertainty = new GeomagneticUncertainty
                {
                    ModelCategory        = GeomagneticModelCategory.HighResolution,
                    // outData[16]: DLL semantics — IsNotCovered (0 = covered, 1 = fallback)
                    HighResolutionCoverage = (outData[16] == 0.0),
                    SigmaD = outData[17],
                    SigmaI = outData[18],
                    SigmaH = outData[19],
                    SigmaX = outData[20],
                    SigmaY = outData[21],
                    SigmaZ = outData[22],
                    SigmaF = outData[23]
                    // outData[24] = UsePomme HDGM-RT flag — out of scope for v1
                }
            };

            return result;
        }
    }
}
