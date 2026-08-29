namespace PointOfSale.Core.Interfaces.Services
{
    public interface IConfigurationService
    {
        int GetLocalPosRegisterId();
        string GetLocalPrinterName();
        string GetConnectionString();
    }
}
