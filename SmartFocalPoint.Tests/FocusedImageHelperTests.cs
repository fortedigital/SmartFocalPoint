﻿using System;
using System.Text;
using System.Collections.Generic;
using System.Web.Mvc;
using EPiServer;
using EPiServer.Core;
using EPiServer.MirroringService.Common;
using EPiServer.ServiceLocation;
using EPiServer.Web;
using EPiServer.Web.Routing;
using Forte.SmartFocalPoint;
using Forte.SmartFocalPoint.Models.Media;
using ImageResizer.Plugins.EPiFocalPoint.SpecializedProperties;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace SmartFocalPointTests
{
    /// <summary>
    /// Summary description for FocusedImageHelperTests
    /// </summary>
    [TestClass]
    public class FocusedImageHelperTests
    {
        private static HtmlHelper _helper;
        private static ContentReference _contentRef;
        private static Mock<IFocalImageData> _imageMock;
        private const string FakeUrl = "test_url";
        //warning: changing these will impact few expected outputs
        private const int OriginalWidth = 800;
        private const int OriginalHeight = 600;

        [ClassInitialize]
        public static void ClassInitializer(TestContext testContext)
        {
            var viewContextMock = new Mock<ViewContext>();
            var viewDataContainerMock = new Mock<IViewDataContainer>();
            _helper = new HtmlHelper(viewContextMock.Object, viewDataContainerMock.Object);
            _contentRef = new ContentReference(1);
            _imageMock = new Mock<IFocalImageData>();
            _imageMock.Setup(x => x.OriginalWidth).Returns(OriginalWidth);
            _imageMock.Setup(x => x.OriginalHeight).Returns(OriginalHeight);
            _imageMock.Setup(x => x.FocalPoint).Returns(new FocalPoint {X = 50.0, Y = 50.0});

            var repository = new Mock<IContentRepository>();
            repository.Setup(x => x.Get<IFocalImageData>(It.IsAny<ContentReference>())).Returns(_imageMock.Object);

            var resolver = new Mock<UrlResolver>();
            resolver.Setup(x => x.GetUrl(_contentRef)).Returns(FakeUrl);

            var loader = new Mock<IContentLoader>();
            loader.Setup(x => x.Get<IFocalImageData>(_contentRef)).Returns(_imageMock.Object);

            var locator = new Mock<IServiceLocator>();
            locator.Setup(x => x.GetInstance<UrlResolver>()).Returns(resolver.Object);
            locator.Setup(x => x.GetInstance<IContentRepository>()).Returns(repository.Object);
            locator.Setup(x => x.GetInstance<IContentLoader>()).Returns(loader.Object);
            ServiceLocator.SetLocator(locator.Object);
        }

        [TestMethod]
        public void FocusedImageTestWithNullReference()
        {
            var expected = MvcHtmlString.Empty;
            var actual = _helper.FocusedImage(null);

            Assert.AreEqual(expected, actual);
        }
        
        [TestMethod]
        public void FocusedImageTestWithNullImageWidthAndHeight()
        {
            _imageMock.Setup(x => x.SmartFocalPointEnabled).Returns(false);

            var expected = MvcHtmlString.Empty;
            var actual = _helper.FocusedImage(_contentRef);

            Assert.AreEqual(expected, actual);
        }

        [DataTestMethod]
        [FocusedImageHelperData]
        public void FocusedImageTestWithParameters(bool smartEnabled, 
            int? width, int? height, string mode, bool zoom, string expectedParams)
        {
            _imageMock.Setup(x => x.SmartFocalPointEnabled).Returns(smartEnabled);

            var expected = "<img src=\"" + FakeUrl + "?" + expectedParams + "\"></img>";
            var actual = _helper.FocusedImage(_contentRef, width, height, mode, zoom).ToString();

            Assert.AreEqual(expected, actual);
        }
    }
}