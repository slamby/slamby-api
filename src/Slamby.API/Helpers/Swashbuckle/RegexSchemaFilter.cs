using System;
using System.Linq;
using Swashbuckle.Swagger.Model;
using Swashbuckle.SwaggerGen.Generator;

namespace Slamby.API.Helpers.Swashbuckle
{
    /// <summary>
    /// Regex pattern replacer
    /// Swagger likes Perl sytle regex pattern
    /// </summary>
    public class RegexSchemaFilter : ISchemaFilter
    {
        public void Apply(Schema model, SchemaFilterContext context)
        {
            //TODO: check this
            if (model.Properties == null)
                return;
            foreach (var prop in model.Properties.Where(p => !string.IsNullOrEmpty(p.Value.Pattern)))
            {
                prop.Value.Pattern = $"/{prop.Value.Pattern}/";
            }
        }
    }
}
