using System;

namespace Slamby.API.Helpers.Swashbuckle
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class SwaggerGroupAttribute : Attribute
    {
        public string Group { get; private set; }

        public SwaggerGroupAttribute(string group)
        {
            Group = group;
        }
    }
}