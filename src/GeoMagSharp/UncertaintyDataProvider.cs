/****************************************************************************
 * File:            UncertaintyDataProvider.cs
 * Description:     Loads and provides geomagnetic uncertainty data
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
    /// Provides geomagnetic uncertainty values from ISCWSA Level 1 or WMM native error model.
    /// Loads data from embedded JSON resources on first access (thread-safe).
    /// </summary>
    public static class UncertaintyDataProvider
    {
        private static readonly Lazy<UncertaintyData> _iscwsaData = new Lazy<UncertaintyData>(LoadIscwsaData);
        private static readonly Lazy<WmmErrorModelData> _wmmData = new Lazy<WmmErrorModelData>(LoadWmmData);

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
                    return GeomagneticModelCategory.HighResolution;

                default:
                    return GeomagneticModelCategory.Unknown;
            }
        }

        /// <summary>
        /// Gets the ISCWSA uncertainty values for the given model and optional category override.
        /// Backward-compatible overload — always uses ISCWSA Level 1.
        /// Use the 4-parameter overload for WMM error model support.
        /// </summary>
        public static GeomagneticUncertainty GetUncertainty(knownModels model, GeomagneticModelCategory? overrideCategory)
        {
            return GetIscwsaUncertainty(model, overrideCategory);
        }

        /// <summary>
        /// Gets uncertainty values based on the model type and uncertainty preference.
        /// </summary>
        /// <param name="model">The detected model type.</param>
        /// <param name="overrideCategory">Optional manual override for ISCWSA model category.</param>
        /// <param name="preference">Which uncertainty model to use (Auto, Iscwsa, or Native).</param>
        /// <param name="horizontalIntensity">Horizontal field intensity (nT) for location-dependent δD. Required for WMM error model.</param>
        /// <returns>Uncertainty values, or null if category is Unknown and ISCWSA is selected.</returns>
        public static GeomagneticUncertainty GetUncertainty(
            knownModels model,
            GeomagneticModelCategory? overrideCategory,
            UncertaintyModelPreference preference,
            double horizontalIntensity)
        {
            // If ModelCategoryOverride is set, the caller explicitly wants ISCWSA
            // for that category (e.g., IFR1), so skip the WMM error model.
            if (overrideCategory.HasValue)
                return GetIscwsaUncertainty(model, overrideCategory);

            bool useWmm = ShouldUseWmmErrorModel(model, preference);

            if (useWmm)
                return GetWmmUncertainty(model, horizontalIntensity);

            return GetIscwsaUncertainty(model, overrideCategory);
        }

        /// <summary>
        /// Gets the ISCWSA uncertainty values for the given model and optional category override.
        /// </summary>
        /// <param name="model">The detected model type.</param>
        /// <param name="overrideCategory">Optional manual override for model category.</param>
        /// <returns>Uncertainty values, or null if category is Unknown.</returns>
        public static GeomagneticUncertainty GetIscwsaUncertainty(knownModels model, GeomagneticModelCategory? overrideCategory)
        {
            var category = GetModelCategory(model, overrideCategory);

            if (category == GeomagneticModelCategory.Unknown)
                return null;

            var data = _iscwsaData.Value;
            var categoryName = category.ToString();

            if (!data.Categories.ContainsKey(categoryName))
                return null;

            var values = data.Categories[categoryName];

            return new GeomagneticUncertainty
            {
                ModelCategory = category,
                Source = UncertaintySource.Iscwsa,
                Declination = values.Declination,
                BhDependentDec = values.BhDependentDec,
                TotalField = values.TotalField,
                DipAngle = values.DipAngle,
                Revision = data.Revision
            };
        }

        /// <summary>
        /// Gets WMM native error model uncertainty values with location-dependent declination.
        /// </summary>
        /// <param name="model">The model type (must be WMM or WMMHR).</param>
        /// <param name="horizontalIntensity">Horizontal field intensity (nT) for δD calculation.</param>
        /// <returns>Uncertainty values with location-dependent declination.</returns>
        public static GeomagneticUncertainty GetWmmUncertainty(knownModels model, double horizontalIntensity)
        {
            var entry = ResolveWmmErrorModelEntry(model);

            if (entry == null)
                throw new InvalidOperationException(
                    $"No WMM error model data found for model type '{model}'. " +
                    "The WMM error model is only available for WMM and WMMHR models.");

            double declination = ComputeDeclinationUncertainty(entry.DeclinationBase, entry.DeclinationCoeff, horizontalIntensity);

            return new GeomagneticUncertainty
            {
                ModelCategory = GetModelCategory(model, null),
                Source = UncertaintySource.WmmErrorModel,
                Declination = declination,
                BhDependentDec = 0,
                TotalField = entry.TotalField,
                DipAngle = entry.Inclination,
                NorthIntensity = entry.NorthIntensity,
                EastIntensity = entry.EastIntensity,
                VerticalIntensity = entry.VerticalIntensity,
                HorizontalIntensity = entry.HorizontalIntensity,
                Revision = "WMM2025-TR"
            };
        }

        /// <summary>
        /// Computes location-dependent declination uncertainty: δD = √(C₁² + (C₂/H)²).
        /// </summary>
        /// <param name="declinationBase">C₁ — base declination uncertainty (degrees).</param>
        /// <param name="declinationCoeff">C₂ — H-dependent coefficient (nT·degrees).</param>
        /// <param name="horizontalIntensity">H — horizontal field intensity (nT).</param>
        /// <returns>Declination uncertainty in degrees.</returns>
        internal static double ComputeDeclinationUncertainty(double declinationBase, double declinationCoeff, double horizontalIntensity)
        {
            if (horizontalIntensity <= 0)
                return 999.0;

            double baseSq = declinationBase * declinationBase;
            double coeffTerm = declinationCoeff / horizontalIntensity;
            double coeffSq = coeffTerm * coeffTerm;

            return Math.Sqrt(baseSq + coeffSq);
        }

        /// <summary>
        /// Determines whether the WMM error model should be used based on model type and preference.
        /// </summary>
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
                            $"UncertaintyModelPreference.Native was requested but model '{model}' has no native error model. " +
                            "Native error models are only available for WMM and WMMHR.");
                    return true;

                case UncertaintyModelPreference.Iscwsa:
                    return false;

                default:
                    return false;
            }
        }

        private static WmmErrorModelEntry ResolveWmmErrorModelEntry(knownModels model)
        {
            var data = _wmmData.Value;

            string key;
            switch (model)
            {
                case knownModels.WMM:
                    key = "WMM2025";
                    break;
                case knownModels.WMMHR:
                    key = "WMMHR2025";
                    break;
                default:
                    return null;
            }

            if (data.Models != null && data.Models.ContainsKey(key))
                return data.Models[key];

            return null;
        }

        private static UncertaintyData LoadIscwsaData()
        {
            return LoadEmbeddedResource<UncertaintyData>("GeoMagSharp.Data.iscwsa-uncertainty.json");
        }

        private static WmmErrorModelData LoadWmmData()
        {
            return LoadEmbeddedResource<WmmErrorModelData>("GeoMagSharp.Data.wmm-error-model.json");
        }

        private static T LoadEmbeddedResource<T>(string resourceName)
        {
            var assembly = Assembly.GetExecutingAssembly();

            using (var stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                    throw new InvalidOperationException(
                        $"Embedded resource '{resourceName}' not found. Ensure the JSON file is set as EmbeddedResource in the project file.");

                using (var reader = new StreamReader(stream))
                {
                    var json = reader.ReadToEnd();
                    return JsonConvert.DeserializeObject<T>(json);
                }
            }
        }
    }
}
