
using Nest;

namespace Slamby.Elastic.Models
{
    public class CompressSettingsElastic
    {
        [Number(NumberType.Integer, Name = "compress_level")]
        public int CompressLevel { get; set; }

        [Number(NumberType.Integer, Name = "compress_category_occurence")]
        public int CompressCategoryOccurence { get; set; }
        [Number(NumberType.Integer, Name = "compress_dataset_occurence")]
        public int CompressDataSetOccurence { get; set; }
        [Number(NumberType.Integer, Name = "compress_operator")]
        public int CompressOperator { get; set; }

    }
}
