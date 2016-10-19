using System.Collections.Generic;
using System.IO;
using Microsoft.AspNet.Hosting;
using Microsoft.Extensions.Logging;
using NJsonSchema;
using NJsonSchema.Validation;
using Slamby.Common.DI;

namespace Slamby.Common.Services
{
    [SingletonDependency]
    public class DataSetSchemaValidatorService
    {
        readonly IHostingEnvironment Env;
        readonly ILogger<DataSetSchemaValidatorService> logger;

        public DataSetSchemaValidatorService(IHostingEnvironment env, ILogger<DataSetSchemaValidatorService> logger)
        {
            this.logger = logger;
            this.Env = env;
        }

        public ICollection<ValidationError> Validate(object datasetSchema)
        {
            var path = Path.Combine(Env.WebRootPath, "schema.json");

            logger.LogInformation($"JsonSchema path used: {path}");

            var schemaJson = File.ReadAllText(path);
            var schema = JsonSchema4.FromJson(schemaJson);
            var token = JTokenHelper.GetToken(datasetSchema);

            return schema.Validate(token);
        }
    }
}
