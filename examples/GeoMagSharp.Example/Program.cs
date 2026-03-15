using System;
using GeoMagSharp;

namespace GeoMagSharp.Example
{
    class Program
    {
        static void Main(string[] args)
        {
            // Find a WMM coefficient file
            string cofFile = FindCoefficientFile("WMM2025.COF");
            if (cofFile == null)
            {
                Console.WriteLine("ERROR: WMM2025.COF not found.");
                Console.WriteLine("Place it in the working directory or pass the path as an argument.");
                Console.WriteLine("  Usage: GeoMagSharp.Example [path-to-COF-file]");
                return;
            }

            if (args.Length > 0 && System.IO.File.Exists(args[0]))
                cofFile = args[0];

            Console.WriteLine("=== GeoMagSharp Uncertainty Example ===");
            Console.WriteLine();

            // ------------------------------------------------------------------
            // 1. Basic calculation with auto-detected uncertainty
            // ------------------------------------------------------------------
            Console.WriteLine("1. WMM spot calculation with ISCWSA uncertainty");
            Console.WriteLine(new string('-', 55));

            var geoMag = new GeoMag();
            geoMag.LoadModel(cofFile);

            var options = new CalculationOptions
            {
                Latitude = 40.0,
                Longitude = -105.0,
                StartDate = new DateTime(2025, 7, 1),
                SecularVariation = true,
                CalculationMethod = Algorithm.BGS
            };
            options.SetElevation(0, Distance.Unit.meter, true);

            geoMag.MagneticCalculations(options);
            var result = geoMag.ResultsOfCalculation[0];

            Console.WriteLine($"  Location:      {options.Latitude:F1}N, {options.Longitude:F1}E");
            Console.WriteLine($"  Date:          {result.Date:yyyy-MM-dd}");
            Console.WriteLine($"  Declination:   {result.Declination.Value,8:F2} deg");
            Console.WriteLine($"  Inclination:   {result.Inclination.Value,8:F2} deg");
            Console.WriteLine($"  Total Field:   {result.TotalField.Value,8:F1} nT");
            Console.WriteLine($"  Horiz. Field:  {result.HorizontalIntensity.Value,8:F1} nT");
            Console.WriteLine();

            if (result.Uncertainty != null)
            {
                var u = result.Uncertainty;
                Console.WriteLine($"  ISCWSA Uncertainty ({u.Revision}, 1-sigma):");
                Console.WriteLine($"    Category:    {u.ModelCategory}");
                Console.WriteLine($"    Declination: +/-{u.Declination:F2} deg");
                Console.WriteLine($"    Bh-dep Dec:  +/-{u.BhDependentDec:F0} deg*nT");
                Console.WriteLine($"    Total Field: +/-{u.TotalField:F0} nT");
                Console.WriteLine($"    Inclination:   +/-{u.Inclination:F2} deg");
            }
            else
            {
                Console.WriteLine("  Uncertainty: not available (unknown model category)");
            }

            Console.WriteLine();

            // ------------------------------------------------------------------
            // 2. Scaling to 2-sigma
            // ------------------------------------------------------------------
            Console.WriteLine("2. Scaled to approximate 2-sigma");
            Console.WriteLine(new string('-', 55));

            if (result.Uncertainty != null)
            {
                var u2 = result.Uncertainty.ScaleTo(2.0);
                Console.WriteLine($"    Declination: +/-{u2.Declination:F2} deg");
                Console.WriteLine($"    Total Field: +/-{u2.TotalField:F0} nT");
                Console.WriteLine($"    Inclination:   +/-{u2.Inclination:F2} deg");
                Console.WriteLine();
                Console.WriteLine("  Note: Geomagnetic errors follow a Laplacian distribution,");
                Console.WriteLine("  so scaled values are approximate at levels other than 1-sigma.");
            }

            Console.WriteLine();

            // ------------------------------------------------------------------
            // 3. Override to IFR category (e.g., for in-field referencing)
            // ------------------------------------------------------------------
            Console.WriteLine("3. ModelCategoryOverride for In-Field Referencing");
            Console.WriteLine(new string('-', 55));

            var ifrOptions = new CalculationOptions
            {
                Latitude = 40.0,
                Longitude = -105.0,
                StartDate = new DateTime(2025, 7, 1),
                ModelCategoryOverride = GeomagneticModelCategory.InFieldReference1
            };
            ifrOptions.SetElevation(0, Distance.Unit.meter, true);

            geoMag.MagneticCalculations(ifrOptions);
            var ifrResult = geoMag.ResultsOfCalculation[0];

            if (ifrResult.Uncertainty != null)
            {
                var u = ifrResult.Uncertainty;
                Console.WriteLine($"  Category:    {u.ModelCategory}");
                Console.WriteLine($"  Declination: +/-{u.Declination:F2} deg  (vs {result.Uncertainty?.Declination:F2} for auto-detect)");
                Console.WriteLine($"  Total Field: +/-{u.TotalField:F0} nT   (vs {result.Uncertainty?.TotalField:F0} for auto-detect)");
                Console.WriteLine();
                Console.WriteLine("  Use ModelCategoryOverride for commercial models (BGGM, HDGM)");
                Console.WriteLine("  or when applying in-field referencing corrections.");
            }

            Console.WriteLine();

            // ------------------------------------------------------------------
            // 4. Date range — all results carry uncertainty
            // ------------------------------------------------------------------
            Console.WriteLine("4. Date range — uncertainty on every result");
            Console.WriteLine(new string('-', 55));

            var rangeOptions = new CalculationOptions
            {
                Latitude = 40.0,
                Longitude = -105.0,
                StartDate = new DateTime(2025, 1, 1),
                EndDate = new DateTime(2025, 7, 1),
                StepInterval = 60
            };
            rangeOptions.SetElevation(0, Distance.Unit.meter, true);

            geoMag.MagneticCalculations(rangeOptions);

            Console.WriteLine($"  {"Date",-12} {"Dec (deg)",10} {"Unc (deg)",10} {"F (nT)",10} {"Unc (nT)",10}");
            foreach (var r in geoMag.ResultsOfCalculation)
            {
                Console.WriteLine($"  {r.Date:yyyy-MM-dd}  {r.Declination.Value,10:F2} {(r.Uncertainty != null ? $"+/-{r.Uncertainty.Declination:F2}" : "n/a"),10} {r.TotalField.Value,10:F1} {(r.Uncertainty != null ? $"+/-{r.Uncertainty.TotalField:F0}" : "n/a"),10}");
            }

            Console.WriteLine();
            Console.WriteLine("=== Done ===");
        }

        private static string FindCoefficientFile(string fileName)
        {
            var searchPaths = new[]
            {
                ".",
                "coefficient",
                System.IO.Path.Combine("..", "coefficient"),
                System.IO.Path.Combine("..", "..", "coefficient"),
                System.IO.Path.Combine("..", "..", "..", "coefficient")
            };

            foreach (var dir in searchPaths)
            {
                var path = System.IO.Path.GetFullPath(System.IO.Path.Combine(dir, fileName));
                if (System.IO.File.Exists(path))
                    return path;
            }
            return null;
        }
    }
}
