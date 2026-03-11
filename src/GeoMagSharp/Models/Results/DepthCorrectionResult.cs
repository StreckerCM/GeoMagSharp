/****************************************************************************
 * File:            DepthCorrectionResult.cs
 * Description:     Result class for dipole depth correction (SPE-128217-MS)
 * Author:          Christopher Strecker
 * Website:         https://github.com/StreckerCM/GeoMagSharp
 ****************************************************************************/

namespace GeoMagSharp
{
    /// <summary>
    /// Results of dipole depth correction per SPE-128217-MS (Ekseth &amp; Weston, 2010).
    /// Null tool-frame and azimuth error properties indicate wellbore geometry was not provided.
    /// </summary>
    public class DepthCorrectionResult
    {
        /// <summary>Dipole scaling factor: R³/(R-D)³</summary>
        public double DipoleScalingFactor { get; set; }

        /// <summary>Horizontal intensity at depth (nT), Eq 1</summary>
        public double HorizontalIntensityAtDepth { get; set; }

        /// <summary>Vertical intensity at depth (nT), Eq 2</summary>
        public double VerticalIntensityAtDepth { get; set; }

        /// <summary>Total field at depth (nT), derived from Eq 1-2</summary>
        public double TotalFieldAtDepth { get; set; }

        /// <summary>Horizontal field error from using surface values (nT), Eq 3</summary>
        public double HorizontalError { get; set; }

        /// <summary>Vertical field error from using surface values (nT), Eq 4</summary>
        public double VerticalError { get; set; }

        /// <summary>High-side error component (nT), Eq 5. Null if no wellbore geometry.</summary>
        public double? HighSideError { get; set; }

        /// <summary>High-side-right error component (nT), Eq 6. Null if no wellbore geometry.</summary>
        public double? HighSideRightError { get; set; }

        /// <summary>Along-hole error component (nT), Eq 7. Null if no wellbore geometry.</summary>
        public double? AlongHoleError { get; set; }

        /// <summary>Azimuth error estimate (degrees), Eq 8. Null if no wellbore geometry.</summary>
        public double? AzimuthErrorDeg { get; set; }

        /// <summary>Singularity proximity: (1 - sin²A·sin²I). Values near 0 indicate E-W singularity.</summary>
        public double? SingularityFactor { get; set; }

        /// <summary>True when SingularityFactor &lt; 0.1, indicating Eq 8 is unreliable.</summary>
        public bool? NearSingularity { get; set; }

        /// <summary>Geomagnetic latitude (degrees), derived from field: atan(Bv / 2Bh)</summary>
        public double GeomagneticLatitudeDeg { get; set; }

        /// <summary>Survey depth below surface (meters)</summary>
        public double DepthMeters { get; set; }

        /// <summary>Reference paper identifier</summary>
        public string Reference { get; set; }
    }
}
