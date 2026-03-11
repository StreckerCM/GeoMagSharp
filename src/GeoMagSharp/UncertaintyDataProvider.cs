/****************************************************************************
 * File:            UncertaintyDataProvider.cs
 * Description:     Loads and provides ISCWSA uncertainty data
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
    /// Provides ISCWSA-based geomagnetic uncertainty values.
    /// Loads data from embedded JSON resource on first access (thread-safe).
    /// </summary>
    public static class UncertaintyDataProvider
    {
        private static readonly Lazy<UncertaintyData> _data = new Lazy<UncertaintyData>(LoadEmbeddedData);

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
        /// </summary>
        /// <param name="model">The detected model type.</param>
        /// <param name="overrideCategory">Optional manual override for model category.</param>
        /// <returns>Uncertainty values, or null if category is Unknown.</returns>
        public static GeomagneticUncertainty GetUncertainty(knownModels model, GeomagneticModelCategory? overrideCategory)
        {
            var category = GetModelCategory(model, overrideCategory);

            if (category == GeomagneticModelCategory.Unknown)
                return null;

            var data = _data.Value;
            var categoryName = category.ToString();

            if (!data.Categories.ContainsKey(categoryName))
                return null;

            var values = data.Categories[categoryName];

            return new GeomagneticUncertainty
            {
                ModelCategory = category,
                Declination = values.Declination,
                BhDependentDec = values.BhDependentDec,
                TotalField = values.TotalField,
                DipAngle = values.DipAngle,
                Revision = data.Revision
            };
        }

        private static UncertaintyData LoadEmbeddedData()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = "GeoMagSharp.Data.iscwsa-uncertainty.json";

            using (var stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                    throw new InvalidOperationException(
                        $"Embedded resource '{resourceName}' not found. Ensure iscwsa-uncertainty.json is set as EmbeddedResource in the project file.");

                using (var reader = new StreamReader(stream))
                {
                    var json = reader.ReadToEnd();
                    return JsonConvert.DeserializeObject<UncertaintyData>(json);
                }
            }
        }
    }
}
