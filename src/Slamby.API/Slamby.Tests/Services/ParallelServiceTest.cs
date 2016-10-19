using System;
using FluentAssertions;
using Microsoft.AspNet.Http;
using Microsoft.AspNet.Server.Kestrel.Http;
using Moq;
using Slamby.Common.Config;
using Slamby.Common.Services;
using Xunit;

namespace Slamby.Tests
{
    public class ParallelServiceTest
    {
        [Fact]
        public void ParallelLimit_ShouldBe_CoreNumberByDefault()
        {
            // Arrange
            var siteConfig = new SiteConfig();
            var requestHeaders = new FrameRequestHeaders();
            var contentAccessorMock = new Mock<IHttpContextAccessor>();

            contentAccessorMock.Setup(hca => hca.HttpContext.Request.Headers).Returns(requestHeaders);

            // Act
            var service = new ParallelService(siteConfig, contentAccessorMock.Object);

            // Assert
            service.ParallelLimit.Should().Be(Environment.ProcessorCount);
        }

        [Theory]
        [InlineData(8, 8, 8)]
        [InlineData(2, 1, 1)]
        public void SiteConfig_ShouldOverride_DefaultLimit(int defaultLimit, int configValue, int parallelLimit)
        {
            // Arrange
            var siteConfig = new SiteConfig
            {
                Parallel = new ParallelConfig
                {
                    ConcurrentTasksLimit = configValue
                }
            };
            var requestHeaders = new FrameRequestHeaders();
            var contentAccessorMock = new Mock<IHttpContextAccessor>();

            contentAccessorMock.Setup(hca => hca.HttpContext.Request.Headers).Returns(requestHeaders);

            // Act
            var service = new ParallelService(siteConfig, contentAccessorMock.Object);
            service.MaximumValue = defaultLimit;

            // Assert
            service.ParallelLimit.Should().Be(parallelLimit);
        }

        [Theory]
        [InlineData(4, 8, 4)]
        [InlineData(2, 0, 2)]
        public void SiteConfig_ShouldNotOverride_DefaultLimit(int defaultLimit, int configValue, int parallelLimit)
        {
            // Arrange
            var siteConfig = new SiteConfig
            {
                Parallel = new ParallelConfig
                {
                    ConcurrentTasksLimit = configValue
                }
            };
            var requestHeaders = new FrameRequestHeaders();
            var contentAccessorMock = new Mock<IHttpContextAccessor>();

            contentAccessorMock.Setup(hca => hca.HttpContext.Request.Headers).Returns(requestHeaders);

            // Act
            var service = new ParallelService(siteConfig, contentAccessorMock.Object);
            service.MaximumValue = defaultLimit;

            // Assert
            service.ParallelLimit.Should().Be(parallelLimit);
        }

        [Theory]
        [InlineData(8, 4, "2", 2)]
        [InlineData(8, 4, "6", 4)]
        [InlineData(8, 0, "2", 2)]
        [InlineData(8, 0, "xyz", 8)]
        [InlineData(8, -1, "-1", 8)]
        public void UserLimitCanOverrideLimit_WithValidValue_IfThereIsNoExplicitLimitDefined(int defaultLimit, int configValue, string userLimit, int parallelLimit)
        {
            // Arrange
            var siteConfig = new SiteConfig
            {
                Parallel = new ParallelConfig
                {
                    ConcurrentTasksLimit = configValue
                }
            };
            var requestHeaders = new FrameRequestHeaders();
            var contentAccessorMock = new Mock<IHttpContextAccessor>();

            contentAccessorMock.Setup(hca => hca.HttpContext.Request.Headers.ContainsKey(
                It.Is<string>(key => key == SDK.Net.Constants.ApiParallelLimitHeader))
                ).Returns(true);
            contentAccessorMock.SetupGet(hca => hca.HttpContext.Request.Headers[
                It.Is<string>(key => key == SDK.Net.Constants.ApiParallelLimitHeader)
                ]).Returns(userLimit);

            // Act
            var service = new ParallelService(siteConfig, contentAccessorMock.Object);
            service.MaximumValue = defaultLimit;

            // Assert
            service.ParallelLimit.Should().Be(parallelLimit);
        }
    }
}
