/****************************************************************************
 * File:            UncertaintyData.cs
 * Description:     Internal POCOs for ISCWSA uncertainty JSON deserialization
 * Author:          Christopher Strecker
 * Website:         https://github.com/StreckerCM/GeoMagSharp
 ****************************************************************************/

using System.Collections.Generic;

namespace GeoMagSharp
{
    /// <summary>
    /// Root object for ISCWSA uncertainty JSON data.
    /// Internal — consumers use <see cref="GeomagneticUncertainty"/> instead.
    /// </summary>
    internal class UncertaintyData
    {
        public string Revision { get; set; }
        public string Date { get; set; }
        public string Source { get; set; }
        public Dictionary<string, UncertaintyCategoryData> Categories { get; set; }
    }

    /// <summary>
    /// Uncertainty values for a single model category (JSON shape).
    /// </summary>
    internal class UncertaintyCategoryData
    {
        public double Declination { get; set; }
        public double BhDependentDec { get; set; }
        public double TotalField { get; set; }
        public double DipAngle { get; set; }
    }
}
