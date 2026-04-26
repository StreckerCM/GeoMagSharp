/****************************************************************************
 * File:            GeoMagEnums.cs
 * Description:     Enumerations used throughout the GeoMagSharp library
 * Author:          Christopher Strecker
 * Website:         https://github.com/StreckerCM/GeoMagSharp
 ****************************************************************************/

namespace GeoMagSharp
{
    /// <summary>
    /// Coordinate system type for calculations
    /// </summary>
    public enum CoordinateSystem
    {
        /// <summary>
        /// Geodetic coordinates (latitude, longitude, altitude above ellipsoid)
        /// </summary>
        Geodetic = 1,

        /// <summary>
        /// Geocentric coordinates (spherical)
        /// </summary>
        Geocentric = 2
    }

    /// <summary>
    /// Algorithm implementation to use for calculations
    /// </summary>
    public enum Algorithm
    {
        /// <summary>
        /// Default spherical harmonic algorithm
        /// </summary>
        BGS = 1,

        /// <summary>
        /// NOAA algorithm
        /// </summary>
        NOAA = 2,

        /// <summary>
        /// MAGVAR algorithm
        /// </summary>
        MAGVAR = 3
    }

    /// <summary>
    /// Unit for magnetic field intensity values
    /// </summary>
    public enum MagneticFieldUnit
    {
        /// <summary>
        /// NanoTesla (default unit for geomagnetic field)
        /// </summary>
        NanoTesla = 1,

        /// <summary>
        /// Gauss (1 Gauss = 100,000 nT)
        /// </summary>
        Gauss = 2
    }

    /// <summary>
    /// Known magnetic model types
    /// </summary>
    public enum knownModels
    {
        /// <summary>
        /// Unknown or unrecognized model type
        /// </summary>
        NONE = 0,

        /// <summary>
        /// Definitive Geomagnetic Reference Field
        /// </summary>
        DGRF = 1,

        /// <summary>
        /// Enhanced Magnetic Model
        /// </summary>
        EMM = 2,

        /// <summary>
        /// International Geomagnetic Reference Field
        /// </summary>
        IGRF = 3,

        /// <summary>
        /// World Magnetic Model
        /// </summary>
        WMM = 4,

        /// <summary>
        /// World Magnetic Model High Resolution.
        /// Not in ISCWSA Rev5.13 (predates it); classified as HighResolution (HRGM)
        /// based on SH degree (729).
        /// </summary>
        WMMHR = 5,

        /// <summary>
        /// High Definition Geomagnetic Model (NOAA degree-740 crustal field).
        /// Windows-only — requires user-supplied NOAA HDGM DLL at runtime.
        /// HighResolution category per ISCWSA Rev5.13.
        /// </summary>
        HDGM = 6
    }

    /// <summary>
    /// ISCWSA geomagnetic reference model categories for uncertainty estimation.
    /// Categories are defined by spherical harmonic degree range per ISCWSA Rev5.13.
    /// </summary>
    public enum GeomagneticModelCategory
    {
        /// <summary>
        /// Unknown or unrecognized model — uncertainty cannot be auto-determined.
        /// Use ModelCategoryOverride in CalculationOptions to set manually.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// Low Resolution Global Model (ISCWSA LRGM, degree ≤13): IGRF, WMM, DGRF
        /// </summary>
        LowResolution = 1,

        /// <summary>
        /// Standard Resolution Global Model (ISCWSA SRGM, degree ≤133): BGGM pre-2019
        /// </summary>
        StandardResolution = 2,

        /// <summary>
        /// High Resolution Global Model (ISCWSA HRGM, degree ≤720): HDGM, BGGM 2019+, EMM, WMMHR
        /// </summary>
        HighResolution = 3,

        /// <summary>
        /// In-Field Referencing level 1
        /// </summary>
        InFieldReference1 = 4,

        /// <summary>
        /// In-Field Referencing level 2 (with multi-station correction)
        /// </summary>
        InFieldReference2 = 5
    }
}
