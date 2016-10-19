using Slamby.Elastic.Models;
using Slamby.SDK.Net.Models.Enums;
using Slamby.SDK.Net.Models.Services;

namespace Slamby.API.Helpers.Services
{
    public static class CompressHelper
    {
        public static CompressSettings ToCompressSettings(this CompressSettingsElastic compressSettingsElastic)
        {
            if (compressSettingsElastic == null) return new CompressSettings { CategoryOccurence = 0, DataSetOccurence = 0, Operator = LogicalOperatorEnum.AND };

            return new CompressSettings {
                CategoryOccurence = compressSettingsElastic.CompressCategoryOccurence,
                DataSetOccurence = compressSettingsElastic.CompressDataSetOccurence,
                Operator = (LogicalOperatorEnum)compressSettingsElastic.CompressOperator
            };
        }

        public static int ToCompressLevel(this CompressSettingsElastic compressSettingsElastic) {
            if (compressSettingsElastic == null) return 0;
            return compressSettingsElastic.CompressLevel;
        }

        public static CompressSettingsElastic ToCompressSettingsElastic(CompressSettings compressSettings, int compressLevel)
        {
            if (compressSettings == null) {
                if (compressLevel == 0)
                    return new CompressSettingsElastic {
                        CompressCategoryOccurence = 0,
                        CompressDataSetOccurence = 0,
                        CompressOperator = (int)LogicalOperatorEnum.AND,
                        CompressLevel = compressLevel
                    };

                if (compressLevel == 1)
                    return new CompressSettingsElastic {
                        CompressCategoryOccurence = 1,
                        CompressDataSetOccurence = 1,
                        CompressOperator = (int)LogicalOperatorEnum.AND,
                        CompressLevel = compressLevel
                    };

                if (compressLevel == 2)
                    return new CompressSettingsElastic {
                        CompressCategoryOccurence = 2,
                        CompressDataSetOccurence = 100000,
                        CompressOperator = (int)LogicalOperatorEnum.AND,
                        CompressLevel = compressLevel
                    };

                throw new Common.Exceptions.SlambyException($"Invalid CompressLevel {compressLevel}!");
            }
            else
            {
                return new CompressSettingsElastic {
                    CompressCategoryOccurence = compressSettings.CategoryOccurence,
                    CompressDataSetOccurence = compressSettings.DataSetOccurence,
                    CompressOperator = (int)compressSettings.Operator,
                    CompressLevel = 0
                };
            }
        }
    }
}
