using Slamby.Common.Services.Interfaces;

namespace Slamby.Common.Services
{
    public class ExplicitDataSetSelector : IDataSetSelector
    {
        public string DataSetName { get; set; }

        public ExplicitDataSetSelector(string dataSetName)
        {
            DataSetName = dataSetName;
        }
    }
}
