namespace PointOfSale.Core.Exception
{
    public class BatchProductionException : System.Exception
    {
        public BatchProductionException(string message)
            : base(message)
        {
        }

        public BatchProductionException(string message, System.Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
