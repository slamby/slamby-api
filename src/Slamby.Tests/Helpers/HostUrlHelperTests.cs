using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Moq;
using Slamby.API.Helpers;
using Xunit;

namespace Slamby.Tests
{
    public class HostUrlHelperTests
    {
        [Fact]
        public void HostUrlHelper_should_return_given_host_if_no_prefix_defined()
        {
            var httpRequestMock = new Mock<HttpRequest>();

            httpRequestMock.Setup(s => s.Scheme).Returns("https");
            httpRequestMock.Setup(s => s.Host).Returns(new HostString("test_api:1234"));

            var hostUrl = HostUrlHelper.GetHostUrl(httpRequestMock.Object, null);

            hostUrl.Should().Be("https://test_api:1234");
        }

        [Fact]
        public void HostUrlHelper_should_return_prefixed_host_if_prefix_defined()
        {
            var httpRequestMock = new Mock<HttpRequest>();

            httpRequestMock.Setup(s => s.Scheme).Returns("http");
            httpRequestMock.Setup(s => s.Host).Returns(new HostString("test_api:1234"));

            var hostUrl = HostUrlHelper.GetHostUrl(httpRequestMock.Object, "https://europe.slamby.com");

            hostUrl.Should().Be("https://europe.slamby.com/test_api");
        }

        [Fact]
        public void HostUrlHelper_should_return_prefixed_dottedhost_if_prefix_defined()
        {
            var httpRequestMock = new Mock<HttpRequest>();

            httpRequestMock.Setup(s => s.Scheme).Returns("http");
            httpRequestMock.Setup(s => s.Host).Returns(new HostString("test_api.msngqf0co2zezf5ihzmkboppdg.ix.internal.cloudapp.net:1234"));

            var hostUrl = HostUrlHelper.GetHostUrl(httpRequestMock.Object, "https://europe.slamby.com");

            hostUrl.Should().Be("https://europe.slamby.com/test_api");
        }
    }
}
