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

namespace Company.Function
{
    public static class HttpTrigger1
    {
        [FunctionName("HttpTrigger1")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            // Azure Blob StorageからPDFファイルを取得
            string storageConnectionString = "DefaultEndpointsProtocol=https;AccountName=foremrecogst;AccountKey=ETZHm8E/79sjRFZ9T28cACU0oKzeG3Blxzyf5EXpN0JCsNxSKsVNJ+GIer3CNrO0FZNncHe9Gh8u+AStWMYKUA==;EndpointSuffix=core.windows.net";
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(storageConnectionString);
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
            CloudBlobContainer container = blobClient.GetContainerReference("input");
            CloudBlockBlob blockBlob = container.GetBlockBlobReference("");
            MemoryStream memStream = new MemoryStream();
            await blockBlob.DownloadToStreamAsync(memStream);

            // Azure FormRecognizerでPDFファイルを分析
            string endpoint = "https://komiyasaformrecog.cognitiveservices.azure.com/";
            string key = "cf1b2413eb0e409884794a4790c73a25";
            FormRecognizerClient formRecognizerClient = new FormRecognizerClient(new ApiKeyServiceClientCredentials(key))
            {
                Endpoint = endpoint
            };
            RecognizeContentOptions options = new RecognizeContentOptions() { IncludeFieldElements = true };
            RecognizeContentOperation recognizeContentOperation = await formRecognizerClient.StartRecognizeContentAsync(memStream, options);
            RecognizeContentResult recognizeContentResult = await recognizeContentOperation.WaitForCompletionAsync();

            // 分析結果のJSONをtxtに変換
            foreach (PageResult pageResult in recognizeContentResult.AnalyzeResult.PageResults)
            {
                string jsonString = pageResult.ToJson();
                string txtString = jsonString.Replace("\"", "'");
                byte[] txtBytes = System.Text.Encoding.UTF8.GetBytes(txtString);
                MemoryStream txtStream = new MemoryStream(txtBytes);

                // txtファイルを別のAzure Blob StorageのContainerに保存
                CloudBlobContainer container2 = blobClient.GetContainerReference("output");
                CloudBlockBlob blockBlob2 = container2.GetBlockBlobReference("");
                await blockBlob2.UploadFromStreamAsync(txtStream);
            }

            return new OkObjectResult("success!!");
        }
    }
}
