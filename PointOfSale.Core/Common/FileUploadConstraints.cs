namespace PointOfSale.Core.Common
{
    public static class FileUploadConstraints
    {
        public const long MaxFileSizeBytes = 20L * 1024L * 1024L;
        public const int MaxFileSizeMegabytes = 20;
        public const string MaxFileSizeDisplayText = "20 MB";

        public static string BuildFileTooLargeMessage(string fileName)
        {
            return string.IsNullOrWhiteSpace(fileName)
                ? $"The selected file exceeds the maximum allowed size of {MaxFileSizeDisplayText}."
                : $"'{fileName}' exceeds the maximum allowed file size of {MaxFileSizeDisplayText}. Please choose a smaller file.";
        }
    }
}
