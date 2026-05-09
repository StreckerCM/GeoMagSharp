/****************************************************************************
 * File:            CalculationOptions.cs
 * Description:     Options for magnetic field calculations
 * Author:          Christopher Strecker
 * Website:         https://github.com/StreckerCM/GeoMagSharp
 ****************************************************************************/

using System;
using System.Collections.Generic;

namespace GeoMagSharp
{
    /// <summary>
    /// Configuration options for magnetic field calculations
    /// </summary>
    public class CalculationOptions
    {
        #region Constructors

        /// <summary>
        /// Initializes a new instance with default values (origin point, current date, default algorithm).
        /// </summary>
        public CalculationOptions()
        {
            Latitude = 0;
            Longitude = 0;
            StartDate = DateTime.MinValue;
            EndDate = DateTime.MinValue;
            StepInterval = 0;
            SecularVariation = true;
            CalculationMethod = Algorithm.BGS;
            ModelCategoryOverride = null;

            ElevationValue = 0;
            ElevationUnit = Distance.Unit.meter;
            ElevationIsAltitude = true;

            SurveyDepthMeters = null;
            WellboreAzimuthDeg = null;
            WellboreInclinationDeg = null;

            AllowExtrapolation = false;

            UncertaintyPreference = UncertaintyModelPreference.Auto;
        }

        /// <summary>
        /// Initializes a new instance by copying values from another <see cref="CalculationOptions"/>.
        /// </summary>
        /// <param name="other">The source options to copy from.</param>
        public CalculationOptions(CalculationOptions other)
        {
            Latitude = other.Latitude;
            Longitude = other.Longitude;
            StartDate = other.StartDate;
            EndDate = other.EndDate;
            StepInterval = other.StepInterval;
            SecularVariation = other.SecularVariation;
            CalculationMethod = other.CalculationMethod;
            ModelCategoryOverride = other.ModelCategoryOverride;

            ElevationValue = other.ElevationValue;
            ElevationUnit = other.ElevationUnit;
            ElevationIsAltitude = other.ElevationIsAltitude;

            SurveyDepthMeters = other.SurveyDepthMeters;
            WellboreAzimuthDeg = other.WellboreAzimuthDeg;
            WellboreInclinationDeg = other.WellboreInclinationDeg;

            AllowExtrapolation = other.AllowExtrapolation;

            UncertaintyPreference = other.UncertaintyPreference;
        }

        #endregion

        /// <summary>Geographic latitude in decimal degrees (-90 to +90).</summary>
        public double Latitude { get; set; }

        /// <summary>Geographic longitude in decimal degrees (-180 to +180).</summary>
        public double Longitude { get; set; }

        /// <summary>Start date for the calculation range.</summary>
        public DateTime StartDate { get; set; }

        /// <summary>End date for the calculation range. If equal to <see cref="DateTime.MinValue"/>, defaults to <see cref="StartDate"/>.</summary>
        public DateTime EndDate { get; set; }

        /// <summary>Step interval in days between calculations in a date range.</summary>
        public double StepInterval { get; set; }

        /// <summary>Whether to calculate secular variation (annual rate of change).</summary>
        public bool SecularVariation { get; set; }

        /// <summary>The calculation algorithm to use.</summary>
        public Algorithm CalculationMethod { get; set; }

        /// <summary>
        /// Optional override for the geomagnetic model category used in uncertainty estimation.
        /// When null, the category is auto-detected from the loaded model type.
        /// Set this for commercial models (BGGM, HDGM) or IFR corrections.
        /// </summary>
        public GeomagneticModelCategory? ModelCategoryOverride { get; set; }

        /// <summary>
        /// Survey depth below surface (meters, positive downward). Null to skip depth correction.
        /// Used for dipole depth correction per SPE-128217-MS.
        /// </summary>
        public double? SurveyDepthMeters { get; set; }

        /// <summary>
        /// Wellbore magnetic azimuth (degrees, 0-360). Null to skip tool-frame error calculations (Eq 5-8).
        /// </summary>
        public double? WellboreAzimuthDeg { get; set; }

        /// <summary>
        /// Wellbore inclination (degrees, 0-180). Null to skip tool-frame error calculations (Eq 5-8).
        /// </summary>
        public double? WellboreInclinationDeg { get; set; }

        /// <summary>
        /// When <c>true</c>, <see cref="GeoMag.MagneticCalculations"/> and
        /// <see cref="GeoMag.MagneticCalculationsAsync"/> skip the date-range check
        /// against the loaded model's <c>MinDate</c>/<c>MaxDate</c>. Default is
        /// <c>false</c> — strict; out-of-range dates throw <c>GeoMagExceptionOutOfRange</c>.
        /// Set to <c>true</c> only when raw extrapolation is intentional (research,
        /// model comparison studies). The underlying calculation engine may still
        /// produce nonsense values past the model's validity range; for HDGM the
        /// native sentinel inside <c>HDGMCalculationAdapter</c> remains the last guard.
        /// </summary>
        public bool AllowExtrapolation { get; set; }

        /// <summary>
        /// Selects which uncertainty model to consult when populating
        /// <see cref="MagneticCalculations.Uncertainty"/>. Default is
        /// <see cref="UncertaintyModelPreference.Auto"/>: the model's native
        /// error model is used when available (WMM/WMMHR have one; HDGM provides
        /// per-point sigmas via the DLL), and ISCWSA Level 1 is used otherwise.
        /// Set to <see cref="UncertaintyModelPreference.Iscwsa"/> to force ISCWSA
        /// for all models, or <see cref="UncertaintyModelPreference.Native"/> to
        /// require the native model (throws if the loaded model has none).
        /// </summary>
        public UncertaintyModelPreference UncertaintyPreference { get; set; }

        #region Getters & Setters

        /// <summary>
        /// Sets the elevation value, unit, and type (altitude or depth).
        /// </summary>
        /// <param name="value">The elevation value.</param>
        /// <param name="unit">The unit of measurement.</param>
        /// <param name="isAltitude"><c>true</c> for altitude above sea level; <c>false</c> for depth below sea level.</param>
        public void SetElevation(double value, Distance.Unit unit, bool isAltitude = true)
        {
            ElevationValue = value;
            ElevationUnit = unit;
            ElevationIsAltitude = isAltitude;
        }

        /// <summary>
        /// Gets the elevation converted to depth in meters. Positive for depth, negative for altitude.
        /// </summary>
        public double DepthInM
        {
            get
            {
                double depth = ElevationValue;

                switch (ElevationUnit)
                {
                    case Distance.Unit.kilometer:
                        depth = ElevationValue * 1000;
                        break;

                    case Distance.Unit.foot:
                        depth = ElevationValue * 0.3048;
                        break;

                    case Distance.Unit.mile:
                        depth = ElevationValue * 1609.34;
                        break;
                }

                return ElevationIsAltitude
                                ? -depth
                                : depth;
            }
        }

        /// <summary>
        /// Gets the elevation converted to altitude in kilometers. Positive for altitude, negative for depth.
        /// </summary>
        public double AltitudeInKm
        {
            get
            {
                double altitude = ElevationValue;

                switch (ElevationUnit)
                {
                    case Distance.Unit.meter:
                        altitude = ElevationValue * 0.001;
                        break;

                    case Distance.Unit.foot:
                        altitude = ElevationValue * 0.0003048;
                        break;

                    case Distance.Unit.mile:
                        altitude = ElevationValue * 1.60934;
                        break;
                }

                return ElevationIsAltitude
                                ? altitude
                                : -altitude;
            }
        }

        /// <summary>
        /// Gets the elevation as a list of [label, value, unit abbreviation] for display purposes.
        /// </summary>
        public List<object> GetElevation
        {
            get
            {
                return new List<object>
                {
                    ElevationIsAltitude ? @"Altitude" : @"Depth",
                    ElevationValue,
                    Distance.ToString(ElevationUnit)
                };
            }
        }

        /// <summary>
        /// Gets the geographic co-latitude (90 - latitude) in degrees.
        /// </summary>
        public double CoLatitude
        {
            get
            {
                return 90 - Latitude;
            }
        }

        private double ElevationValue { get; set; }
        private Distance.Unit ElevationUnit { get; set; }
        private bool ElevationIsAltitude { get; set; }

        #endregion
    }
}
