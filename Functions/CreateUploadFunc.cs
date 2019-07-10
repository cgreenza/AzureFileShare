using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.Extensions.Configuration;
using System.Net.Http;
using System.Net;
using System.Text;
using System.Security.Cryptography;

namespace Functions
{
    public static class CreateUploadFunc
    {
        private static RNGCryptoServiceProvider rngCsp = new RNGCryptoServiceProvider();

        [FunctionName("CreateUpload")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            ILogger log, ExecutionContext context)
        {
            var devEnvironmentVariable = Environment.GetEnvironmentVariable("NETCORE_ENVIRONMENT");
            var isDevelopment = string.IsNullOrEmpty(devEnvironmentVariable) || devEnvironmentVariable.ToLower() == "development";

            var builder = new ConfigurationBuilder()
                .SetBasePath(context.FunctionAppDirectory)
                .AddJsonFile("settings.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables();

            if (isDevelopment)
                builder.AddUserSecrets(typeof(CreateUploadFunc).Assembly);

            var config = builder.Build();

            log.LogInformation("C# HTTP trigger function processed a request.");

            string filename = req.Query["filename"];
            if (string.IsNullOrEmpty(filename))
                return new BadRequestObjectResult("Please pass a filename on the query string");

            var randomNumber = new byte[9];
            rngCsp.GetBytes(randomNumber);
            var uploadId = Convert.ToBase64String(randomNumber).Replace('+', '-').Replace('/', '_');

            var storageAccount = CloudStorageAccount.Parse(config["StorageAccountConnectionString"]);
            var blobClient = storageAccount.CreateCloudBlobClient();
            var container = blobClient.GetContainerReference(config["ContainerName"]);
            var blob = container.GetBlockBlobReference($"{uploadId}/{filename}");
            var sasToken = blob.GetSharedAccessSignature(new SharedAccessBlobPolicy() {
                SharedAccessStartTime = DateTime.UtcNow.AddMinutes(-5),
                SharedAccessExpiryTime = DateTime.UtcNow.AddHours(2),
                Permissions = SharedAccessBlobPermissions.Write,
            });

            var uploadUrl = $"{blob.Uri}{sasToken}";
            var downloadUrl = $"{blob.Uri}";

            // todo: add entry to table: key by uploadId, store: filename, date/time, user

            return new JsonResult(new {
                uploadUrl = uploadUrl,
                downloadUrl = downloadUrl
            });
        }
    }
}
