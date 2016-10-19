using System.Collections.Generic;
using Slamby.API.Helpers;
using Xunit;

namespace Slamby.Tests.Helpers
{
    public class CommonValidatorsTests
    {
        [Theory,
            InlineData(null),
            InlineData(new int[] {  }),
            InlineData(new[] { 0, 1, 2 }),
            InlineData(new[] { 5, 6, 7 }),
            InlineData(new[] { 1, 2, 3, 2 })]
        public void ValidateNGram_ShouldFail_InvalidNGramList(IEnumerable<int> list)
        {
            // Arrange

            // Act
            var result = CommonValidators.ValidateNGramList(list);

            // Assert
            Assert.Equal(true, result.IsFailure);
        }

        [Theory,
            InlineData(new int[] { 3 }),
            InlineData(new int[] { 1, 2, 3, 4, 5, 6 })]
        public void ValidateNGram_ShouldValidate_ValidNGramList(IEnumerable<int> list)
        {
            // Arrange

            // Act
            var result = CommonValidators.ValidateNGramList(list);

            // Assert
            Assert.Equal(true, result.IsSuccess);
        }

        [Theory,
            InlineData(new[] { 1, 2, 3 }, 2)]
        public void ValidateDataSetNGram_ShouldFail_InvalidDataSetNGramCount(IEnumerable<int> list, int dataSetNGramCount)
        {
            // Arrange

            // Act
            var result = CommonValidators.ValidateNGramList(list, dataSetNGramCount);

            // Assert
            Assert.Equal(true, result.IsFailure);
        }

        [Theory,
            InlineData(new int[] { 1, 2, 3 }, 3)]
        public void ValidateDataSetNGram_ShouldValidate_ValidNGramList(IEnumerable<int> list, int dataSetNGramCount)
        {
            // Arrange

            // Act
            var result = CommonValidators.ValidateNGramList(list, dataSetNGramCount);

            // Assert
            Assert.Equal(true, result.IsSuccess);
        }

        [Theory,
            InlineData(new[] { 1, 2 }, new int[] { }),
            InlineData(new[] { 1, 2 }, new[] { 1, 2, 3 }),
            InlineData(new[] { 1, 2 }, new[] { 3, 4 })]
        public void ValidateNGramList_ShouldFail_OutsiderList(IEnumerable<int> baseList, IEnumerable<int> list)
        {
            // Arrange

            // Act
            var result = CommonValidators.ValidateServiceNGramList(baseList, list);

            // Assert
            Assert.Equal(true, result.IsFailure);
        }

        [Theory,
            InlineData(new[] { 1, 2, 3 }, new[] { 1, 3 })]
        public void ValidateNGramList_ShouldValidate_SubsetList(IEnumerable<int> baseList, IEnumerable<int> list)
        {
            // Arrange

            // Act
            var result = CommonValidators.ValidateServiceNGramList(baseList, list);

            // Assert
            Assert.Equal(true, result.IsSuccess);
        }
    }
}
