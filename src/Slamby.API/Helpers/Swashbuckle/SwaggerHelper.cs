using System.Linq;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Swashbuckle.SwaggerGen.Generator;

namespace Slamby.API.Helpers.Swashbuckle
{
    public static class SwaggerHelper
    {
        public static string GetActionGroup(ApiDescription desc)
        {
            var groupAttribute = desc
                .GetControllerAttributes()
                .OfType<SwaggerGroupAttribute>()
                .FirstOrDefault();

            if (groupAttribute != null)
            {
                return groupAttribute.Group;
            }

            var split = desc.RelativePath.Split('/');
            var group = split[1];

            return group;
        }
    }
}
