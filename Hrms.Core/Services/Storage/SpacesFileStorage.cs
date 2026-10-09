using System.Net;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;

namespace Hrms.Core.Services.Storage;

/// <summary>
/// Files in a DigitalOcean Space, through its S3-compatible API. Every object is private; a
/// browser downloads one through a short-lived signed link. Keys are put under the configured
/// <see cref="SpacesStorageOptions.RootPrefix"/>, which callers never see.
/// </summary>
public class SpacesFileStorage : IFileStorage
{
    private readonly IAmazonS3 _s3;
    private readonly SpacesStorageOptions _options;

    public SpacesFileStorage(IAmazonS3 s3, IOptions<StorageOptions> options)
    {
        _s3 = s3;
        _options = options.Value.Spaces;
    }

    /// <summary>
    /// The S3 client settings Spaces needs. The AWS SDK's newer default request checksums aren't
    /// supported by Spaces and make uploads fail, so they're only sent when an operation requires them.
    /// </summary>
    public static AmazonS3Config ClientConfig(SpacesStorageOptions options) => new()
    {
        ServiceURL = options.Endpoint,
        AuthenticationRegion = options.SigningRegion,
        ForcePathStyle = false,
        RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
        ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
    };

    public static IAmazonS3 CreateClient(SpacesStorageOptions options) =>
        new AmazonS3Client(new BasicAWSCredentials(options.AccessKey, options.SecretKey), ClientConfig(options));

    /// <summary>The object's key in the Space: the caller's key under the root prefix.</summary>
    public string ObjectKey(string key)
    {
        if (!StorageKeys.IsValid(key)) throw new ArgumentException($"Invalid storage key \"{key}\".", nameof(key));
        var prefix = _options.RootPrefix.Trim('/');
        return prefix.Length == 0 ? key : $"{prefix}/{key}";
    }

    public async Task<StoredFile> UploadAsync(FileUpload upload, CancellationToken token = default)
    {
        var request = new PutObjectRequest
        {
            BucketName = _options.Bucket,
            Key = ObjectKey(upload.Key),
            InputStream = upload.Content,
            ContentType = upload.ContentType,
            CannedACL = S3CannedACL.Private,
            AutoCloseStream = false,
        };
        await _s3.PutObjectAsync(request, token);
        var size = upload.Content.CanSeek ? upload.Content.Length : (await GetSizeAsync(request.Key, token) ?? 0);
        return new StoredFile(upload.Key, size, upload.ContentType);
    }

    public async Task<Stream?> OpenReadAsync(string key, CancellationToken token = default)
    {
        try
        {
            var response = await _s3.GetObjectAsync(_options.Bucket, ObjectKey(key), token);
            return response.ResponseStream;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken token = default) =>
        await GetSizeAsync(ObjectKey(key), token) != null;

    public Task DeleteAsync(string key, CancellationToken token = default) =>
        // S3 deletes are idempotent: removing a missing object succeeds.
        _s3.DeleteObjectAsync(_options.Bucket, ObjectKey(key), token);

    public async Task<int> DeleteByPrefixAsync(string prefix, CancellationToken token = default)
    {
        if (!StorageKeys.IsCompanyFolder(prefix))
            throw new ArgumentException($"\"{prefix}\" isn't a company folder.", nameof(prefix));

        var objectPrefix = ObjectKey(prefix.TrimEnd('/')) + "/";
        var deleted = 0;
        string? continuationToken = null;
        do
        {
            var page = await _s3.ListObjectsV2Async(new ListObjectsV2Request
            {
                BucketName = _options.Bucket,
                Prefix = objectPrefix,
                ContinuationToken = continuationToken,
            }, token);

            // DeleteObjects takes up to 1000 keys, the most a listing page returns.
            if (page.S3Objects is { Count: > 0 })
            {
                await _s3.DeleteObjectsAsync(new DeleteObjectsRequest
                {
                    BucketName = _options.Bucket,
                    Objects = page.S3Objects.Select(o => new KeyVersion { Key = o.Key }).ToList(),
                    Quiet = true,
                }, token);
                deleted += page.S3Objects.Count;
            }
            continuationToken = page.IsTruncated == true ? page.NextContinuationToken : null;
        } while (continuationToken != null);
        return deleted;
    }

    public async Task<Uri?> GetDownloadUrlAsync(string key, string? downloadFileName = null, CancellationToken token = default)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _options.Bucket,
            Key = ObjectKey(key),
            Verb = HttpVerb.GET,
            Expires = DateTime.UtcNow.AddMinutes(_options.DownloadUrlMinutes),
        };
        if (!string.IsNullOrWhiteSpace(downloadFileName))
            request.ResponseHeaderOverrides.ContentDisposition =
                $"attachment; filename=\"{StorageKeys.SafeFileName(downloadFileName)}\"";
        return new Uri(await _s3.GetPreSignedURLAsync(request));
    }

    private async Task<long?> GetSizeAsync(string objectKey, CancellationToken token)
    {
        try
        {
            var metadata = await _s3.GetObjectMetadataAsync(_options.Bucket, objectKey, token);
            return metadata.ContentLength;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }
}
