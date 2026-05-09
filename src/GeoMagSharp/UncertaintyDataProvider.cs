/****************************************************************************
 * File:            UncertaintyDataProvider.cs
 * Description:     Resolves geomagnetic uncertainty values from ISCWSA Level 1
 *                  or the model's native error model (WMM/WMMHR).
 * Author:          Christopher Strecker
 * Website:         https://github.com/StreckerCM/GeoMagSharp
 ****************************************************************************/

using System;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;

namespace GeoMagSharp
{
    /// <summary>
    /// Resolves geomagnetic uncertainty values for a given model — either ISCWSA
    /// Level 1 global constants or the model's native error model (WMM/WMMHR).
    /// HDGM per-point sigmas are populated separately by <c>HDGMCalculationAdapter</c>.
    /// JSON resources are loaded lazily on first access (thread-safe).
    /// </summary>
    public static class UncertaintyDataProvider
    {
        private static readonly Lazy<UncertaintyData> _iscwsaData =
            new Lazy<UncertaintyData>(LoadIscwsaData);
        private static readonly Lazy<WmmErrorModelData> _wmmData =
            new Lazy<WmmErrorModelData>(LoadWmmData);

        /// <summary>
        /// Maps a <see cref="knownModels"/> value to its ISCWSA model category.
        /// </summary>
        /// <param name="model">The detected model type.</param>
        /// <param name="overrideCategory">Optional manual override. If set, returned directly.</param>
        /// <returns>The model category for uncertainty lookup.</returns>
        public static GeomagneticModelCategory GetModelCategory(knownModels model, GeomagneticModelCategory? overrideCategory)
        {
            if (overrideCategory.HasValue)
                return overrideCategory.Value;

            switch (model)
            {
                case knownModels.WMM:
                case knownModels.IGRF:
                case knownModels.DGRF:
                    return GeomagneticModelCategory.LowResolution;

                case knownModels.WMMHR:
                case knownModels.EMM:
                case knownModels.HDGM:
                    return GeomagneticModelCategory.HighResolution;

                default:
                    return GeomagneticModelCategory.Unknown;
            }
        }

        /// <summary>
        /// Backwards-compatible 2-argument overload — always returns ISCWSA Level 1
        /// uncertainty. New consumers should use the 4-argument overload, which dispatches
        /// to the model's native error model when one exists (WMM/WMMHR).
        /// </summary>
        /// <param name="model">The detected model type.</param>
        /// <param name="overrideCategory">Optional manual override for ISCWSA model category.</param>
        /// <returns>ISCWSA uncertainty values, or null if category resolves to Unknown.</returns>
        public static GeomagneticUncertainty GetUncertainty(knownModels model, GeomagneticModelCategory? overrideCategory)
        {
            return GetIscwsaUncertainty(model, overrideCategory);
        }

        /// <summary>
        /// Resolves uncertainty values based on model type, optional category override,
        /// the caller's preference (Auto / Iscwsa / Native), and the location's horizontal
        /// field intensity (used by the WMM error model's location-dependent declination
        /// formula).
        /// </summary>
        /// <param name="model">The detected model type.</param>
        /// <param name="overrideCategory">Optional manual override. When set, ISCWSA is
        /// always used (overrides imply "use the ISCWSA category I told you").</param>
        /// <param name="preference">Which uncertainty source to use.</param>
        /// <param name="horizontalIntensity">Horizontal field intensity (nT) at the
        /// calculation location. Required for the WMM error model; ignored for ISCWSA.</param>
        /// <returns>Uncertainty values, or null if ISCWSA is selected and the category is Unknown.</returns>
        public static GeomagneticUncertainty GetUncertainty(
            knownModels model,
            GeomagneticModelCategory? overrideCategory,
            UncertaintyModelPreference preference,
            double horizontalIntensity)
        {
            // An explicit category override implies the caller wants ISCWSA (e.g. forcing
            // an IFR1/IFR2 category). Native error models don't have category subdivisions.
            if (overrideCategory.HasValue)
                return GetIscwsaUncertainty(model, overrideCategory);

            if (ShouldUseWmmErrorModel(model, preference))
                return GetWmmUncertainty(model, horizontalIntensity);

            return GetIscwsaUncertainty(model, overrideCategory);
        }

        /// <summary>
        /// Returns ISCWSA Level 1 uncertainty for the given model. Component fields
        /// (<see cref="GeomagneticUncertainty.HorizontalIntensity"/>, NorthComp, EastComp,
        /// VerticalComp) stay null — ISCWSA Level 1 doesn't provide them.
        /// </summary>
        internal static GeomagneticUncertainty GetIscwsaUncertainty(knownModels model, GeomagneticModelCategory? overrideCategory)
        {
            var category = GetModelCategory(model, overrideCategory);
            if (category == GeomagneticModelCategory.Unknown) return null;

            var data = _iscwsaData.Value;
            var categoryName = category.ToString();
            if (!data.Categories.ContainsKey(categoryName)) return null;

            var values = data.Categories[categoryName];

            return new GeomagneticUncertainty
            {
                Source = UncertaintySource.Iscwsa,
                ModelCategory = category,
                Revision = data.Revision,
                Declination = values.Declination,
                Inclination = values.DipAngle,   // ISCWSA's DipAngle field is the same physical quantity as Inclination
                TotalField = values.TotalField,
                BhDependentDec = values.BhDependentDec
                // Component fields (Horizontal/North/East/Vertical) remain null — not in ISCWSA L1
            };
        }

        /// <summary>
        /// Returns WMM/WMMHR native error-model uncertainty with location-dependent declination.
        /// Constants from the WMM 2025-2030 Technical Report Section 3.4.
        /// </summary>
        internal static GeomagneticUncertainty GetWmmUncertainty(knownModels model, double horizontalIntensity)
        {
            string modelKey;
            var entry = ResolveWmmErrorModelEntry(model, out modelKey);
            if (entry == null)
                throw new InvalidOperationException(
                    "No WMM error model data found for model type '" + model + "'. " +
                    "The WMM error model is only available for WMM and WMMHR.");

            double declination = ComputeDeclinationUncertainty(
                entry.DeclinationBase, entry.DeclinationCoeff, horizontalIntensity);

            return new GeomagneticUncertainty
            {
                Source = UncertaintySource.WmmErrorModel,
                ModelCategory = GetModelCategory(model, null),
                Revision = modelKey + "-TR",
                Declination = declination,
                Inclination = entry.Inclination,
                TotalField = entry.TotalField,
                HorizontalIntensity = entry.HorizontalIntensity,
                NorthComp = entry.NorthIntensity,
                EastComp = entry.EastIntensity,
                VerticalComp = entry.VerticalIntensity,
                BhDependentDec = 0  // WMM error model bakes location dependence into Declination directly
            };
        }

        /// <summary>
        /// Computes the WMM error model's location-dependent declination uncertainty:
        /// δD = √(C₁² + (C₂ / H)²). Returns 999.0 at H = 0 (magnetic dip pole) to flag
        /// that declination is undefined; consumers should check for ≥ 999.0.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">If <paramref name="horizontalIntensity"/> is NaN, infinite, or negative.</exception>
        internal static double ComputeDeclinationUncertainty(double declinationBase, double declinationCoeff, double horizontalIntensity)
        {
            if (double.IsNaN(horizontalIntensity) || double.IsInfinity(horizontalIntensity))
                throw new ArgumentOutOfRangeException(nameof(horizontalIntensity),
                    horizontalIntensity, "Horizontal intensity must be a finite number.");
            if (horizontalIntensity < 0)
                throw new ArgumentOutOfRangeException(nameof(horizontalIntensity),
                    horizontalIntensity, "Horizontal intensity cannot be negative.");
            if (horizontalIntensity == 0)
                return 999.0;

            double baseSq = declinationBase * declinationBase;
            double coeffTerm = declinationCoeff / horizontalIntensity;
            double coeffSq = coeffTerm * coeffTerm;
            return Math.Sqrt(baseSq + coeffSq);
        }

        /// <summary>
        /// Decides whether to use the model's native WMM error model based on the model
        /// type and the caller's preference. Native is only available for WMM and WMMHR.
        /// </summary>
        /// <exception cref="InvalidOperationException">If <paramref name="preference"/> is <see cref="UncertaintyModelPreference.Native"/> but the model has no native error model.</exception>
        internal static bool ShouldUseWmmErrorModel(knownModels model, UncertaintyModelPreference preference)
        {
            bool modelHasNativeErrorModel = (model == knownModels.WMM || model == knownModels.WMMHR);

            switch (preference)
            {
                case UncertaintyModelPreference.Auto:
                    return modelHasNativeErrorModel;

                case UncertaintyModelPreference.Native:
                    if (!modelHasNativeErrorModel)
                        throw new InvalidOperationException(
                            "UncertaintyModelPreference.Native was requested but model '" + model +
                            "' has no native error model. Native error models are only available for WMM and WMMHR.");
                    return true;

                case UncertaintyModelPreference.Iscwsa:
                    return false;

                default:
                    return false;
            }
        }

        private static WmmErrorModelEntry ResolveWmmErrorModelEntry(knownModels model, out string modelKey)
        {
            var data = _wmmData.Value;

            switch (model)
            {
                case knownModels.WMM:
                    modelKey = "WMM2025";
                    break;
                case knownModels.WMMHR:
                    modelKey = "WMMHR2025";
                    break;
                default:
                    modelKey = null;
                    return null;
            }

            WmmErrorModelEntry entry;
            if (data.Models == null || !data.Models.TryGetValue(modelKey, out entry))
                return null;
            return entry;
        }

        private static UncertaintyData LoadIscwsaData()
        {
            return LoadEmbeddedJson<UncertaintyData>("GeoMagSharp.Data.iscwsa-uncertainty.json");
        }

        private static WmmErrorModelData LoadWmmData()
        {
            return LoadEmbeddedJson<WmmErrorModelData>("GeoMagSharp.Data.wmm-error-model.json");
        }

        private static T LoadEmbeddedJson<T>(string resourceName)
        {
            var assembly = Assembly.GetExecutingAssembly();
            using (var stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                    throw new InvalidOperationException(
                        "Embedded resource '" + resourceName + "' not found. " +
                        "Ensure it's set as EmbeddedResource in the project file.");
                using (var reader = new StreamReader(stream))
                {
                    var json = reader.ReadToEnd();
                    return JsonConvert.DeserializeObject<T>(json);
                }
            }
        }
    }
}
