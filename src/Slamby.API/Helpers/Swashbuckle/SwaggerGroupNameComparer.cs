using System.Collections.Generic;

namespace Slamby.API.Helpers.Swashbuckle
{
    public class SwaggerGroupNameComparer : IComparer<string>
    {
        public int Compare(string x, string y)
        {
            if (x != y)
            {
                if (x == "DataSet")
                {
                    return -1;
                }
                if (y == "DataSet")
                {
                    return 1;
                }
            }

            return -1 * string.CompareOrdinal(x, y);
        }
    }
}
