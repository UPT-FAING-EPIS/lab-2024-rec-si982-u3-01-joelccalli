using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.OpenApi.Models;

namespace ShortenFunction
{
    public static class ShortenHttp
    {
        [FunctionName("ShortenHttp")]
        [OpenApiOperation(operationId: "Run", tags: new[] { "General" })]
        [OpenApiParameter(name: "name", In = ParameterLocation.Query, Required = false, Type = typeof(string), Description = "Nombre para saludar")]
        [OpenApiResponseWithBody(statusCode: System.Net.HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(string), Description = "Mensaje de respuesta")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            string name = req.Query["name"];

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            name = name ?? data?.name;

            string responseMessage = string.IsNullOrEmpty(name)
                ? "This HTTP triggered function executed successfully. Pass a name in the query string or in the request body for a personalized response."
                : $"Hello, {name}. This HTTP triggered function executed successfully.";

            return new OkObjectResult(responseMessage);
        }

        [FunctionName("GetAll")]
        [OpenApiOperation(operationId: "GetAll", tags: new[] { "Short URLs" })]
        [OpenApiResponseWithBody(System.Net.HttpStatusCode.OK, "application/json", typeof(UrlMapping[]), Description = "Lista de URLs acortadas")]
        public static async Task<IActionResult> GetShortUrls(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "shorturl")] HttpRequest req, ILogger log)
        {
            log.LogInformation("Getting url list items");
            try
            {
                var context = new ShortenContext();
                var urls = await context.UrlMappings.ToListAsync();
                return new OkObjectResult(urls);
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Error al obtener los datos" + ex.Message);
                return new BadRequestObjectResult("Error al obtener los datos");
            }
        }

        [FunctionName("SwaggerUI")]
        [OpenApiIgnore]
        public static IActionResult SwaggerUI(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "swagger/ui")]
            HttpRequest req)
        {
            return new RedirectResult("/api/swagger/ui");
        }


        [FunctionName("GetById")]
        public static async Task<IActionResult> GetShortUrlById(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "shorturl/{id}")]
            HttpRequest req, ILogger log, int id)
        {
            log.LogInformation("Getting url list item by id");
            var url = await new ShortenContext().UrlMappings.FindAsync(id);
            return new OkObjectResult(url);
        }

        [FunctionName("Create")]
        [OpenApiOperation(operationId: "CreateShortUrl", tags: new[] { "Short URLs" }, Summary = "Crea una URL acortada")]
        [OpenApiRequestBody("application/json", typeof(UrlMappingCreateModel), Description = "Datos de la URL acortada")]
        [OpenApiResponseWithBody(statusCode: System.Net.HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(UrlMapping), Description = "Retorna la URL creada")]
        public static async Task<IActionResult> CreateShortUrl(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "shorturl")]
            HttpRequest req, ILogger log)
        {
            log.LogInformation("Creating a new short URL");
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var input = JsonConvert.DeserializeObject<UrlMappingCreateModel>(requestBody);

            var url = new UrlMapping { OriginalUrl = input.OriginalUrl, ShortenedUrl = input.ShortenedUrl };
            var context = new ShortenContext();
            await context.UrlMappings.AddAsync(url);
            await context.SaveChangesAsync();

            return new OkObjectResult(url);
        }

        [FunctionName("Update")]
        [OpenApiOperation(operationId: "UpdateShortUrl", tags: new[] { "Short URLs" }, Summary = "Actualiza una URL acortada")]
        [OpenApiParameter(name: "id", In = ParameterLocation.Path, Required = true, Type = typeof(int), Description = "ID de la URL a actualizar")]
        [OpenApiRequestBody("application/json", typeof(UrlMappingCreateModel), Description = "Datos actualizados de la URL acortada")]
        [OpenApiResponseWithBody(statusCode: System.Net.HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(UrlMapping), Description = "Retorna la URL actualizada")]
        public static async Task<IActionResult> UpdateShortUrl(
            [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "shorturl/{id}")]
            HttpRequest req, ILogger log, int id)
        {
            log.LogInformation($"Updating URL with ID {id}");
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var input = JsonConvert.DeserializeObject<UrlMappingCreateModel>(requestBody);

            var context = new ShortenContext();
            var url = await context.UrlMappings.FindAsync(id);
            if (url == null)
            {
                log.LogWarning($"Item {id} not found");
                return new NotFoundResult();
            }

            url.OriginalUrl = input.OriginalUrl;
            url.ShortenedUrl = input.ShortenedUrl;
            await context.SaveChangesAsync();

            return new OkObjectResult(url);
        }

        [FunctionName("Delete")]
        [OpenApiOperation(operationId: "DeleteShortUrl", tags: new[] { "Short URLs" }, Summary = "Elimina una URL acortada")]
        [OpenApiParameter(name: "id", In = ParameterLocation.Path, Required = true, Type = typeof(int), Description = "ID de la URL a eliminar")]
        [OpenApiResponseWithBody(statusCode: System.Net.HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(string), Description = "Mensaje de éxito")]
        public static async Task<IActionResult> DeleteShortUrl(
            [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "shorturl/{id}")]
            HttpRequest req, ILogger log, int id)
        {
            log.LogInformation($"Deleting URL with ID {id}");
            var context = new ShortenContext();
            var url = await context.UrlMappings.FindAsync(id);
            if (url == null)
            {
                log.LogWarning($"Item {id} not found");
                return new NotFoundResult();
            }

            context.UrlMappings.Remove(url);
            await context.SaveChangesAsync();
            return new OkObjectResult($"URL with ID {id} deleted successfully.");
        }

    }   

    public class UrlMappingCreateModel
    {
        /// <summary>
        /// Valor original de la url
        /// </summary>
        /// <value>Cadena</value>
        public string OriginalUrl { get; set; } = string.Empty;
        /// <summary>
        /// Valor corto de la url
        /// </summary>
        /// <value>Cadena</value>
        public string ShortenedUrl { get; set; } = string.Empty;
    }
    public class UrlMapping
    {
        /// <summary>
        /// Identificador del mapeo de url
        /// </summary>
        /// <value>Entero</value>
        public int Id { get; set; }
        /// <summary>
        /// Valor original de la url
        /// </summary>
        /// <value>Cadena</value>
        public string OriginalUrl { get; set; } = string.Empty;
        /// <summary>
        /// Valor corto de la url
        /// </summary>
        /// <value>Cadena</value>
        public string ShortenedUrl { get; set; } = string.Empty;
    }

    public class ShortenContext : DbContext
    {
        /// <summary>
        /// Constructor de la clase
        /// </summary>
        static string conexion = new ConfigurationBuilder().AddEnvironmentVariables().AddJsonFile("local.settings.json", optional:  true, reloadOnChange: true).Build().GetConnectionString("ShortenDB");
        public ShortenContext() : base(SqlServerDbContextOptionsExtensions.UseSqlServer(new DbContextOptionsBuilder(), conexion, o => o.CommandTimeout(300)).Options)
        {
        }
        /// <summary>
        /// Propiedad que representa la tabla de mapeo de urls
        /// </summary>
        /// <value>Conjunto de UrlMapping</value>
        public DbSet<UrlMapping> UrlMappings { get; set; }
    }
}
