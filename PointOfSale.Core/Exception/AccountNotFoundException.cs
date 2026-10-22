namespace PointOfSale.Core.Exceptions
{
    public sealed class AccountNotFoundException : System.Exception
    {
        public int AccountId { get; }

        public AccountNotFoundException(int accountId, System.Exception innerException)
            : base($"Account {accountId} does not exist. Please select a valid account.", innerException)
        {
            AccountId = accountId;
        }
    }
}
