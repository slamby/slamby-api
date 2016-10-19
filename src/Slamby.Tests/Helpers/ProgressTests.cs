using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Slamby.API.Helpers;
using Xunit;

namespace Slamby.Tests.Helpers
{
    public class ProgressTests
    {
        [Fact]
        public void Progress_ShouldCalculate_ValidPercentage()
        {
            var progress = new Progress(10, 7);

            Assert.Equal(70, progress.Percent);
        }

        [Theory]
        [InlineData(7, 10, 80)]
        [InlineData(40, 100, 41)]
        [InlineData(0, 8, 12.5)]
        public void Progress_ShouldStep_ToExpectedPercent(int value, int total, double expected)
        {
            var progress = new Progress(total, value);
            progress.Step();

            Assert.Equal(expected, progress.Percent);
        }

        [Theory]
        [InlineData(7, 10, 7, 10, 77)]
        [InlineData(70, 100, 7, 10, 70.7)]
        [InlineData(1, 2, 2, 5, 70)]
        public void Progress_ShouldCalculate_InnerProgress(int value1, int total1, int value2, int total2, double expected)
        {
            // Arrange
            var progress = new Progress(total1, value1);
            var innerProgress = new Progress(total2, value2);

            // Act
            var result = progress.MultiPercent(innerProgress);

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void Progress_ShouldBeThreadSafe_WhileStepping()
        {
            // Arrange
            const int Total = 100;
            const int Threads = 10;

            var progress = new Progress(Total);

            var factory = new TaskFactory();
            var tasks = new List<Task>();

            foreach (var item in Enumerable.Range(1, Threads))
            {
                tasks.Add(factory.StartNew(() =>
                {
                    foreach (var item2 in Enumerable.Range(1, Total / Threads))
                    {
                        progress.Step();
                    }
                }));
            }

            // Act
            Task.WaitAll(tasks.ToArray());

            // Assert
            Assert.Equal(Total, progress.Percent);
        }
    }
}
