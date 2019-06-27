using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.Azure.Storage.Auth;
using Microsoft.Azure.Storage.Blob;

namespace AzureResourceAccess.Controllers
{
    public class TestController : ApiController
    {
        // make sure you created this storage account
        private const string MyStorageAccountName = "<your storage account name>";


        // these will be created for you
        private const string SampleContainer = "azure-meetup";
        private const string Blob1Txt = "blobster.txt";

        public async Task<string> Get()
        {
            try
            {
                // source https://docs.microsoft.com/en-us/azure/storage/common/storage-auth-aad-msi

                string storageBaseUri = $"https://{MyStorageAccountName}.blob.core.windows.net";

                // Get the initial access token and the interval at which to refresh it.
                AzureServiceTokenProvider azureServiceTokenProvider = new AzureServiceTokenProvider();
                NewTokenAndFrequency tokenAndFrequency = await TokenRenewerAsync(azureServiceTokenProvider,CancellationToken.None);

                // Create storage credentials using the initial token, and connect the callback function 
                // to renew the token just before it expires
                TokenCredential tokenCredential = new TokenCredential(tokenAndFrequency.Token, TokenRenewerAsync, azureServiceTokenProvider, tokenAndFrequency.Frequency.Value);

                StorageCredentials storageCredentials = new StorageCredentials(tokenCredential);

                CloudBlobClient client = new CloudBlobClient(new Uri(storageBaseUri), storageCredentials);
                CloudBlobContainer container = client.GetContainerReference(SampleContainer);

                await container.CreateIfNotExistsAsync();

                CloudBlockBlob blob = container.GetBlockBlobReference(Blob1Txt);

                if (!blob.Exists())
                {
                    // Upload text to the blob.
                    await blob.UploadTextAsync($"That's ma blob! '{blob.Name}'");
                }


                return await blob.DownloadTextAsync();
            }
            catch (Exception e)
            {
                // only for meetup purposes only, don't do this in production since it is a security risk! (information leakage)
                return e.ToString();
            }
        }

        private static async Task<NewTokenAndFrequency> TokenRenewerAsync(object state, CancellationToken cancellationToken)
        {
            // Specify the resource ID for requesting Azure AD tokens for Azure Storage.
            const string StorageResource = "https://storage.azure.com/";

            // Use the same token provider to request a new token.
            AppAuthenticationResult authResult = await ((AzureServiceTokenProvider)state).GetAuthenticationResultAsync(StorageResource, cancellationToken: cancellationToken);

            // Renew the token 5 minutes before it expires.
            var next = (authResult.ExpiresOn - DateTimeOffset.UtcNow) - TimeSpan.FromMinutes(5);
            if (next.Ticks < 0)
            {
                next = default(TimeSpan);
                Debug.WriteLine("Renewing token...");
            }

            // Return the new token and the next refresh time.
            return new NewTokenAndFrequency(authResult.AccessToken, next);
        }
    }
}
