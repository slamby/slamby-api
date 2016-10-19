using System.Collections.Generic;
using Swashbuckle.SwaggerGen.Generator;

namespace Slamby.API.Helpers.Swashbuckle
{
    public class SwaggerApplyBasicAuthFilter : IOperationFilter
    {
        public string Name { get; private set; }

        public SwaggerApplyBasicAuthFilter()
        {
            Name = "Slamby";
        }

        public void Apply(Operation operation, OperationFilterContext context)
        {
            var basicAuthDict = new Dictionary<string, IEnumerable<string>>();
            basicAuthDict.Add(Name, new List<string>());
            operation.Security = new IDictionary<string, IEnumerable<string>>[] { basicAuthDict };

            // Parameters not initilaized. Swashbuckle BUG?
            operation.Parameters = new List<IParameter>();

            operation.Parameters.Add(new NonBodyParameter()
            {
                Name = "Slamby",
                In =  "header",
                Description = "Http authentication. Ex: Authorization: Slamby <api_secret>",
                Type = "string",
                Required = true
            });
        }
    }
}
