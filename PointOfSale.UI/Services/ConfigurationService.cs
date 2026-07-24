using System;
using System.Configuration;
using PointOfSale.Core.Interfaces.Services;

namespace PointOfSale.UI.Services
{
    public class ConfigurationService : IConfigurationService
    {
        public int GetLocalPosRegisterId()
        {
            var rawValue = ConfigurationManager.AppSettings["LocalPOSRegisterId"];

            if (string.IsNullOrWhiteSpace(rawValue))
            {
                throw new InvalidOperationException("AppSetting 'LocalPOSRegisterId' is missing.");
            }

            if (!int.TryParse(rawValue, out var registerId) || registerId <= 0)
            {
                throw new InvalidOperationException("AppSetting 'LocalPOSRegisterId' must be a positive integer.");
            }

            return registerId;
        }

        public string GetLocalPrinterName()
        {
            return ConfigurationManager.AppSettings["LocalPrinterName"] ?? string.Empty;
        }
    }
}
