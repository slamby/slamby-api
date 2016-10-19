using System.Collections.Generic;
using Swashbuckle.SwaggerGen.Generator;

namespace Slamby.API.Helpers.Swashbuckle
{
    public class SwaggerSchemaDocumentFilter : IDocumentFilter
    {
        readonly string host;
        readonly string basePath;
        readonly IList<string> schemes;

        public SwaggerSchemaDocumentFilter(IList<string> schemes, string host, string basePath)
        {
            this.schemes = schemes;
            this.host = host;
            this.basePath = basePath;
        }

        public void Apply(SwaggerDocument swaggerDoc, DocumentFilterContext context)
        {
            swaggerDoc.Schemes = schemes;
            swaggerDoc.Host = host;
            swaggerDoc.BasePath = basePath;
        }
    }
}
