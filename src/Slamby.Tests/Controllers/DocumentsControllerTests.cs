using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Slamby.API.Controllers;
using Slamby.API.Models;
using Slamby.API.Services.Interfaces;
using Slamby.Common.Helpers;
using Slamby.Common.Services.Interfaces;
using Slamby.Elastic.Models;
using Slamby.SDK.Net.Models;
using Xunit;

namespace Slamby.Tests.Controllers
{
    public class DocumentsControllerTests
    {
        private DocumentsController SetupController(Action<Mock<IDocumentService>> documentMock = null)
        {
            var documentServiceMock = new Mock<IDocumentService>();
            var globalStoreMock = new Mock<IGlobalStoreManager>();
            var dataSetSelectorMock = new Mock<IDataSetSelector>();

            var dataSetName = "dataset_name";

            globalStoreMock.Setup(s => s.DataSets.Get(It.IsAny<string>()))
                .Returns(new GlobalStoreDataSet(dataSetName, dataSetName, new DataSet { Name = dataSetName }, new List<string> { }, false, false));

            documentMock?.Invoke(documentServiceMock);

            var controller = new DocumentsController(documentServiceMock.Object, dataSetSelectorMock.Object);

            return controller;
        }

        [Fact]
        public void Get_ShouldReturnNotFound_ForNonExistingDocument()
        {
            // Arrange
            var controller = SetupController(ds => ds
                .Setup(s => s.Get("dataset_name","not_found"))
                .Returns<DocumentElastic>(null));

            // Act
            var response = controller.Get("not_found");

            // Assert
            Assert.IsType<HttpStatusCodeWithErrorResult>(response);
            Assert.Equal(StatusCodes.Status404NotFound, (response as ObjectResult).StatusCode);
        }

        [Fact]
        public void Get_ShouldReturnOk_ForExistingDocument()
        {
            // Arrange
            var document = new { };
            var controller = SetupController(ds => ds
                .Setup(s => s.Get(It.IsAny<string>(), "found"))
                .Returns(new DocumentElastic{ DocumentObject = document }));
            
            // Act
            var response = controller.Get("found");

            // Assert
            Assert.IsType<OkObjectResult>(response);
            Assert.Equal(document, (response as OkObjectResult).Value);
        }
    }
}
