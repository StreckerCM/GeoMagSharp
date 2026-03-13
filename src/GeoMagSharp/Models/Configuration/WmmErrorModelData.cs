/****************************************************************************
 * File:            WmmErrorModelData.cs
 * Description:     Internal POCOs for WMM error model JSON deserialization
 * Author:          Christopher Strecker
 * Website:         https://github.com/StreckerCM/GeoMagSharp
 ****************************************************************************/

using System.Collections.Generic;

namespace GeoMagSharp
{
    /// <summary>
    /// Root object for WMM error model JSON data.
    /// Internal — consumers use <see cref="GeomagneticUncertainty"/> instead.
    /// </summary>
    internal class WmmErrorModelData
    {
        public string Source { get; set; }
        public string Reference { get; set; }
        public Dictionary<string, WmmErrorModelEntry> Models { get; set; }
    }

    /// <summary>
    /// Error model constants for a single WMM model epoch.
    /// </summary>
    internal class WmmErrorModelEntry
    {
        public double ValidFrom { get; set; }
        public double ValidTo { get; set; }

        /// <summary>δX — North component uncertainty (nT)</summary>
        public double NorthIntensity { get; set; }

        /// <summary>δY — East component uncertainty (nT)</summary>
        public double EastIntensity { get; set; }

        /// <summary>δZ — Vertical component uncertainty (nT)</summary>
        public double VerticalIntensity { get; set; }

        /// <summary>δH — Horizontal intensity uncertainty (nT)</summary>
        public double HorizontalIntensity { get; set; }

        /// <summary>δF — Total field uncertainty (nT)</summary>
        public double TotalField { get; set; }

        /// <summary>δI — Inclination uncertainty (degrees)</summary>
        public double Inclination { get; set; }

        /// <summary>C₁ — Base declination uncertainty constant (degrees)</summary>
        public double DeclinationBase { get; set; }

        /// <summary>C₂ — Declination H-dependent coefficient (nT·degrees)</summary>
        public double DeclinationCoeff { get; set; }
    }
}
