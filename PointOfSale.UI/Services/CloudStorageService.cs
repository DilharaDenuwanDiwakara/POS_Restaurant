using System;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;

namespace PointOfSale.UI.Services
{
    public sealed class CloudStorageService : IDisposable
    {
        private const string AccessKeySettingName = "CloudflareR2AccessKeyId";
        private const string SecretKeySettingName = "CloudflareR2SecretAccessKey";
        private const string ServiceUrlSettingName = "CloudflareR2ServiceUrl";
        private const string BucketNameSettingName = "CloudflareR2BucketName";
        private const string PublicBaseUrlSettingName = "CloudflareR2PublicBaseUrl";
        private const string SignedUrlMinutesSettingName = "CloudflareR2SignedUrlMinutes";

        private readonly AmazonS3Client _s3Client;
        private readonly string _bucketName;
        private readonly string _publicBaseUrl;
        private readonly int _signedUrlMinutes;

        public CloudStorageService()
        {

            var accessKeyId = GetRequiredSetting(AccessKeySettingName);
            var secretAccessKey = GetRequiredSetting(SecretKeySettingName);
            var serviceUrl = GetRequiredSetting(ServiceUrlSettingName).TrimEnd('/');

            _bucketName = GetRequiredSetting(BucketNameSettingName);
            _publicBaseUrl = GetRequiredSetting(PublicBaseUrlSettingName).TrimEnd('/');
            _signedUrlMinutes = GetSignedUrlMinutes();

            var credentials = new BasicAWSCredentials(accessKeyId, secretAccessKey);

            var config = new AmazonS3Config
            {
                ServiceURL = serviceUrl,
                ForcePathStyle = true,
                AuthenticationRegion = "auto"

            };

            _s3Client = new AmazonS3Client(credentials, config);
        }

        public Task<string> UploadFileAsync(string localFilePath, string folderName)
        {
            return UploadFileAsync(localFilePath, folderName, null);
        }

        public async Task<string> UploadFileAsync(string localFilePath, string folderName, string fileName)
        {
            if (string.IsNullOrWhiteSpace(localFilePath))
            {
                throw new ArgumentException("A local file path is required.", nameof(localFilePath));
            }

            if (!File.Exists(localFilePath))
            {
                throw new FileNotFoundException("The selected file could not be found.", localFilePath);
            }

            var safeFileName = SanitizeFileName(string.IsNullOrWhiteSpace(fileName)
                ? Path.GetFileName(localFilePath)
                : fileName);

            var objectKey = BuildObjectKey(folderName, safeFileName);

            using (var stream = File.OpenRead(localFilePath))
            {
                var request = new PutObjectRequest
                {
                    BucketName = _bucketName,
                    Key = objectKey,
                    InputStream = stream,
                    ContentType = GetContentType(safeFileName),
                    AutoCloseStream = false,

                    DisablePayloadSigning = true,
                    UseChunkEncoding = false
                };

                await _s3Client.PutObjectAsync(request).ConfigureAwait(false);
            }

            return objectKey;
        }

        public async Task<string> UploadPublicFileAsync(string localFilePath, string folderName, string fileName)
        {
            if (string.IsNullOrWhiteSpace(localFilePath))
            {
                throw new ArgumentException("A local file path is required.", nameof(localFilePath));
            }

            if (!File.Exists(localFilePath))
            {
                throw new FileNotFoundException("The selected file could not be found.", localFilePath);
            }

            var safeFileName = SanitizeFileName(string.IsNullOrWhiteSpace(fileName)
                ? Path.GetFileName(localFilePath)
                : fileName);

            var objectKey = BuildObjectKey(folderName, safeFileName);

            using (var stream = File.OpenRead(localFilePath))
            {
                var request = new PutObjectRequest
                {
                    BucketName = _bucketName,
                    Key = objectKey,
                    InputStream = stream,
                    ContentType = GetContentType(safeFileName),
                    AutoCloseStream = false,
                    DisablePayloadSigning = true,
                    UseChunkEncoding = false
                };

                await _s3Client.PutObjectAsync(request).ConfigureAwait(false);
            }

            return GetPublicFileUrl(objectKey);
        }

        public string GetPublicFileUrl(string objectKey)
        {
            if (string.IsNullOrWhiteSpace(objectKey))
            {
                return null;
            }

            if (Uri.IsWellFormedUriString(objectKey, UriKind.Absolute))
            {
                return objectKey;
            }

            return $"{_publicBaseUrl}/{NormalizeObjectKey(objectKey)}";
        }

        public string GetSecureFileUrl(string objectKey)
        {
            return GetSecureFileUrl(objectKey, TimeSpan.FromMinutes(_signedUrlMinutes));
        }

        public string GetSecureFileUrl(string objectKey, TimeSpan expiresIn)
        {
            if (string.IsNullOrWhiteSpace(objectKey))
            {
                return null;
            }

            if (Uri.IsWellFormedUriString(objectKey, UriKind.Absolute))
            {
                return objectKey;
            }

            var request = new GetPreSignedUrlRequest
            {
                BucketName = _bucketName,
                Key = NormalizeObjectKey(objectKey),
                Verb = HttpVerb.GET,
                Expires = DateTime.UtcNow.Add(expiresIn)
            };

            return _s3Client.GetPreSignedURL(request);
        }

        public void Dispose()
        {
            _s3Client?.Dispose();
        }

        private static string GetRequiredSetting(string key)
        {
            var value = ConfigurationManager.AppSettings[key];
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ConfigurationErrorsException($"Missing required App.config setting '{key}'.");
            }

            return value.Trim();
        }

        private static int GetSignedUrlMinutes()
        {
            int minutes;
            return int.TryParse(ConfigurationManager.AppSettings[SignedUrlMinutesSettingName], out minutes) && minutes > 0
                ? minutes
                : 15;
        }

        private static string BuildObjectKey(string folderName, string safeFileName)
        {
            var folder = NormalizeObjectKey(folderName);
            var datedName = $"{DateTime.UtcNow:yyyyMMdd}/{Guid.NewGuid():N}-{safeFileName}";

            return string.IsNullOrWhiteSpace(folder)
                ? datedName
                : $"{folder}/{datedName}";
        }

        private static string NormalizeObjectKey(string value)
        {
            return (value ?? string.Empty)
                .Replace('\\', '/')
                .Trim()
                .Trim('/');
        }

        private static string SanitizeFileName(string fileName)
        {
            var invalidCharacters = Path.GetInvalidFileNameChars();
            var safeCharacters = (fileName ?? "file")
                .Select(ch => invalidCharacters.Contains(ch) ? '_' : ch)
                .ToArray();

            var safeFileName = new string(safeCharacters).Trim();
            return string.IsNullOrWhiteSpace(safeFileName) ? "file" : safeFileName;
        }

        private static string GetContentType(string fileName)
        {
            switch ((Path.GetExtension(fileName) ?? string.Empty).ToLowerInvariant())
            {
                case ".jpg":
                case ".jpeg":
                    return "image/jpeg";
                case ".png":
                    return "image/png";
                case ".pdf":
                    return "application/pdf";
                case ".gif":
                    return "image/gif";
                case ".webp":
                    return "image/webp";
                default:
                    return "application/octet-stream";
            }
        }
    }
}
