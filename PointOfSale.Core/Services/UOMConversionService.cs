using System;
using System.Linq;
using PointOfSale.Core.Models.Inventory;

namespace PointOfSale.Core.Services
{
    public class UOMConversionService
    {
        public decimal GetConvertedQuantity(Product product, int fromUnitMeasureId, int toUnitMeasureId, decimal quantity)
        {
            if (product == null)
            {
                throw new ArgumentNullException(nameof(product));
            }

            if (fromUnitMeasureId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(fromUnitMeasureId), "Source unit measure is required.");
            }

            if (toUnitMeasureId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(toUnitMeasureId), "Destination unit measure is required.");
            }

            if (fromUnitMeasureId == toUnitMeasureId)
            {
                return quantity;
            }

            var conversion = product.UnitConversions?
                .FirstOrDefault(x =>
                    x.IsActive &&
                    x.ConversionRate > 0m &&
                    ((fromUnitMeasureId == x.TargetUnitMeasureId && toUnitMeasureId == product.UnitMeasureId) ||
                     (fromUnitMeasureId == product.UnitMeasureId && toUnitMeasureId == x.TargetUnitMeasureId)));

            if (conversion == null)
            {
                throw new InvalidOperationException("No active unit conversion was found for the selected product and unit measure.");
            }

            if (fromUnitMeasureId == conversion.TargetUnitMeasureId && toUnitMeasureId == product.UnitMeasureId)
            {
                return conversion.IsMultiply
                    ? quantity * conversion.ConversionRate
                    : quantity / conversion.ConversionRate;
            }

            if (fromUnitMeasureId == product.UnitMeasureId && toUnitMeasureId == conversion.TargetUnitMeasureId)
            {
                return conversion.IsMultiply
                    ? quantity / conversion.ConversionRate
                    : quantity * conversion.ConversionRate;
            }

            throw new InvalidOperationException("The selected unit conversion does not match the requested conversion direction.");
        }
    }
}
