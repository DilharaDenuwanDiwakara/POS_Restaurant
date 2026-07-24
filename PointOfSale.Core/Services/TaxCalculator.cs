using System;
using System.Collections.Generic;
using System.Linq;
using PointOfSale.Core.Models.System;

namespace PointOfSale.Core.Services
{
    public static class TaxCalculator
    {
        public static decimal CalculateExclusiveTax(decimal taxableAmount, decimal taxRate)
        {
            if (taxableAmount <= 0m || taxRate <= 0m)
            {
                return 0m;
            }

            return Math.Round(taxableAmount * (taxRate / 100m), 2);
        }

        /// <summary>
        /// Extracts the total per-unit tax embedded in (or added on top of) unitPrice,
        /// applying each tax in CalculationOrder so compounding taxes stack correctly.
        /// </summary>
        public static decimal ExtractUnitTax(decimal unitPrice, IEnumerable<TaxConfiguration> applicableTaxes)
        {
            decimal runningBase = unitPrice;
            decimal totalUnitTax = 0m;

            foreach (var tax in applicableTaxes.Where(t => t.IsActive).OrderBy(t => t.CalculationOrder))
            {
                decimal portion = tax.IsInclusive
                    ? runningBase - (runningBase / (1 + (tax.Rate / 100m)))   // embedded in price
                    : runningBase * (tax.Rate / 100m);                       // added on top

                portion = Math.Round(portion, 2, MidpointRounding.AwayFromZero);
                totalUnitTax += portion;

                if (tax.IsInclusive)
                    runningBase -= portion; // next inclusive tax in the stack applies to the reduced base
            }

            return totalUnitTax;
        }
    }
}
