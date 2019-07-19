﻿using System;
using System.Configuration;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using EPiServer;
using EPiServer.Core;
using EPiServer.Framework;
using EPiServer.Framework.Initialization;
using EPiServer.Logging;
using EPiServer.ServiceLocation;
using EPiServer.Shell;
using Forte.SmartFocalPoint.Business;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision.Models;
using Forte.SmartFocalPoint.Models.Media;
using ImageResizer.Plugins.EPiFocalPoint.SpecializedProperties;
using Microsoft.Rest.ClientRuntime;

namespace Forte.SmartFocalPoint
{
    [ModuleDependency(typeof(ShellInitialization))]
    [InitializableModule]
    public class SmartFocalPointModule : IInitializableModule
    {
	    private const int MaxSize = 1024;
        private static readonly ILogger Logger = LogManager.GetLogger();
        private SmartFocalPointAdminPluginSettings _settings = new SmartFocalPointAdminPluginSettings();

		public void Initialize(InitializationEngine context)
        {
            context.Locate.ContentEvents().PublishingContent += HandlePublishingContent;
        }

        private void HandlePublishingContent(object sender, ContentEventArgs e)
        {
            if (e.Content is FocalImageData imageFile)
            {
                if (_settings.LoadSettings() && !WasEdited(imageFile))
                {
                    using (var stream = ReadBlob(imageFile))
                    {
                        var originalImage = Image.FromStream(stream);

                        var resizedImage = ResizeImage(originalImage, MaxSize);

                        var boundingRect = GetAreaOfInterest(resizedImage) ?? new BoundingRect();

                        double scaleX = 1.0 / (resizedImage.Width / (double) originalImage.Width);
                        double scaleY = 1.0 / (resizedImage.Height / (double) originalImage.Height);

                        var areaOfInterestX = (int) (boundingRect.X * scaleX);
                        var areaOfInterestY = (int) (boundingRect.Y * scaleY);
                        var areaOfInterestWidth = (int) (boundingRect.W * scaleX);
                        var areaOfInterestHeight = (int) (boundingRect.H * scaleY);

                        var middlePointX = areaOfInterestX + areaOfInterestWidth / 2;
                        var middlePointY = areaOfInterestY + areaOfInterestHeight / 2;
                        imageFile.FocalPoint = new FocalPoint()
                        {
                            X = 100 * middlePointX / (double) originalImage.Width,
                            Y = 100 * middlePointY / (double) originalImage.Height
                        };
                    }
                }
            }

        }

        private bool WasEdited(FocalImageData image)
        {
            var contentVersionRepository = ServiceLocator.Current.GetInstance<IContentVersionRepository>();
            var contentRepository = ServiceLocator.Current.GetInstance<IContentRepository>();

            if (ContentReference.IsNullOrEmpty(image.ContentLink)) return false;
            
            var lastVersion = contentVersionRepository
                .List(image.ContentLink)
                .Where(p => p.Status == VersionStatus.PreviouslyPublished)
                .OrderByDescending(p => p.Saved)
                .FirstOrDefault();

            if (lastVersion == null) return false;

            var lastImage = contentRepository.Get<FocalImageData>(lastVersion.ContentLink);
            return lastImage.FocalPoint != image.FocalPoint;
        }

        private static Image ResizeImage(Image originalImage, int maxSize)
        {
	        int w;
	        int h;
	        int desWidth = maxSize;
	        int desHeight = maxSize;

			if (originalImage.Height > originalImage.Width)
	        {
		        w = (originalImage.Width * desHeight) / originalImage.Height;
		        h = desHeight;
		    
	        }
	        else
	        {
		        w = desWidth;
		        h = (originalImage.Height * desWidth) / originalImage.Width;
		       
	        }

			return originalImage.GetThumbnailImage(w, h, null, IntPtr.Zero);
        }

		private static MemoryStream ReadBlob(FocalImageData content)
        {
            using (var stream = content.BinaryData.OpenRead())
            {
                var buffer = new byte[stream.Length];
                stream.Read(buffer, 0, buffer.Length);

                var memoryStream = new MemoryStream(buffer, writable: false);
                return memoryStream;
            }
        }

        private static BoundingRect GetAreaOfInterest(Image image)
        {
	        using (MemoryStream imageStream = new MemoryStream())
	        {
				image.Save(imageStream, ImageFormat.Png);
				imageStream.Seek(0L, SeekOrigin.Begin);

                string key = ConfigurationManager.AppSettings["CognitiveServicesApiKey"];
                string server = ConfigurationManager.AppSettings["CognitiveServicesServer"];

				var client = new ComputerVisionClient(new ApiKeyServiceClientCredentials(key))
		        {
			        Endpoint = server
		        };
                try
                {
                    var result = client.GetAreaOfInterestInStreamWithHttpMessagesAsync(imageStream).Result;
                    return result.Body.AreaOfInterest;
                }
                catch (AggregateException exceptions)
                {
                    exceptions.Handle(ex => HandleException(ex));
                    return null;
                }
            }
        }

        public static bool HandleException(Exception ex)
        {
            if (ex is ComputerVisionErrorException exception)
            {
                Logger.Error(exception.Body.ToString());
                return true;
            }

            return false;
        }

		public void Uninitialize(InitializationEngine context)
        {
            context.Locate.ContentEvents().PublishingContent -= HandlePublishingContent;
        }
    }
}