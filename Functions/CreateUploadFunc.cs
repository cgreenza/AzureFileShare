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
using Microsoft.WindowsAzure.Storage.Table;
using System.Linq;

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
            var clientPrincipalName = req.Headers["X-MS-CLIENT-PRINCIPAL-NAME"].FirstOrDefault();
            if (string.IsNullOrEmpty(clientPrincipalName))
                return new UnauthorizedResult();

            var devEnvironmentVariable = Environment.GetEnvironmentVariable("NETCORE_ENVIRONMENT");
            var isDevelopment = string.IsNullOrEmpty(devEnvironmentVariable) || devEnvironmentVariable.ToLower() == "development";

            var builder = new ConfigurationBuilder()
                .SetBasePath(context.FunctionAppDirectory)
                .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables();

            if (isDevelopment)
                builder.AddUserSecrets(typeof(CreateUploadFunc).Assembly);

            var config = builder.Build();

            // log.LogInformation("C# HTTP trigger function processed a request.");

            string filename = req.Query["filename"];
            if (string.IsNullOrEmpty(filename))
                return new BadRequestObjectResult("Please pass a filename on the query string");

            var randomNumber = new byte[9];
            rngCsp.GetBytes(randomNumber);
            var uploadId = Convert.ToBase64String(randomNumber).Replace('+', '-').Replace('/', '_').Substring(0, 8);

            var storageAccount = CloudStorageAccount.Parse(config["StorageAccountConnectionString"]);
            var blobClient = storageAccount.CreateCloudBlobClient();
            var container = blobClient.GetContainerReference(config["ContainerName"]);

            // todo: different path prefix for different lifecycle auto-delete e.g. 30d, 60d, 90d
            var blob = container.GetBlockBlobReference($"{uploadId}/{filename}");
            var sasToken = blob.GetSharedAccessSignature(new SharedAccessBlobPolicy() {
                SharedAccessStartTime = DateTime.UtcNow.AddMinutes(-5),
                SharedAccessExpiryTime = DateTime.UtcNow.AddHours(3),
                Permissions = SharedAccessBlobPermissions.Write,
            });

            var uploadUrl = $"{blob.Uri}{sasToken}";
            var downloadUrl = $"{blob.Uri}";

            // save details to table
            var tableClient = storageAccount.CreateCloudTableClient();
            CloudTable uploadsTable = tableClient.GetTableReference("uploads");
            await uploadsTable.CreateIfNotExistsAsync();

            var uploadEntity = new UploadEntity(uploadId) {
                FileName = filename,
                User = clientPrincipalName,
                UploadDate = DateTime.UtcNow,
                DownloadUrl = downloadUrl,
            };
            await uploadsTable.ExecuteAsync(TableOperation.Insert(uploadEntity));

            return new JsonResult(new {
                uploadUrl = uploadUrl,
                downloadUrl = downloadUrl
            });
        }

        public class UploadEntity : TableEntity
        {
            public UploadEntity(string uploadId)
            {
                this.PartitionKey = "uploads";
                this.RowKey = uploadId;
            }

            public UploadEntity() { }

            public string FileName { get; set; }

            public string User { get; set; }

            public DateTime UploadDate { get; set; }

            public string DownloadUrl { get; set; }
        }
    }
}
