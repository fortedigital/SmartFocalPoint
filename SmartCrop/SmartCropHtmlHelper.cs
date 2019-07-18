﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Web.Mvc;
using Forte.SmartCrop.Models.Media;
using EPiServer;
using EPiServer.Core;
using EPiServer.ServiceLocation;
using EPiServer.Web.Routing;

namespace Forte.SmartCrop
{
    public static class SmartCropHtmlHelper
    {
        public static MvcHtmlString ResizedPicture(
            this HtmlHelper helper,
            ContentReference image,
            int? width,
            int? height,
            bool smartCrop)
        {
            if (ContentReference.IsNullOrEmpty(image))
            {
                return MvcHtmlString.Empty;
            }
            string imageBaseUrl = ResolveImageUrl(image);
            ServiceLocator.Current.GetInstance<IContentLoader>().TryGet(image, out FocalImageData imageFile);

            var isCrop = width != null && height != null;

            var parameters = new List<string>();

            if (smartCrop && isCrop)
            {
                parameters.Add("crop=" + CalculateCropBounds(imageFile, width.Value, height.Value));
            }
            else
            {
                if (width != null)
                {
                    parameters.Add("width=" + width.ToString());
                }

                if (height != null)
                {
                    parameters.Add("height=" + height.ToString());
                }
            }

            if (isCrop)
            {
                parameters.Add("mode=crop");
            }


            var separator = imageBaseUrl.Contains("?") ? "&" : "?";

            var imageUrl = imageBaseUrl + separator + string.Join("&", parameters);


            TagBuilder tagBuilder = new TagBuilder("img");
            tagBuilder.Attributes.Add("src", imageUrl);

            return new MvcHtmlString(tagBuilder.ToString());

        }

        private static string CalculateCropBounds(FocalImageData imageFile, int width, int height)
        {
	        using (var stream = ReadBlob(imageFile))
	        {
		        var originalImage = Image.FromStream(stream);
                
				double cropRatio = width / (double)height;
				double originalRatio = originalImage.Width / (double)originalImage.Height;

				var cropQuery = string.Empty;
                var cropX = 0.0;
                var cropY = 0.0;
				if (cropRatio < originalRatio)
				{
					var boundingRectHeight = originalImage.Height;
					var boundingRectWidth = boundingRectHeight * cropRatio;
                    var middlePointX = imageFile.FocalPoint.X * originalImage.Width / 100;

					cropX = middlePointX - boundingRectWidth / 2;
					if (cropX < 0)
						cropX = 0;

					if (cropX + boundingRectWidth > originalImage.Width)
						cropX = originalImage.Width - boundingRectWidth;

                    cropQuery = $"{cropX},{cropY},{boundingRectWidth},{boundingRectHeight}";
				}
				else
				{
					var boundingRectWidth = originalImage.Width;
					var boundingRectHeight = boundingRectWidth / cropRatio;
                    var middlePointY = imageFile.FocalPoint.Y * originalImage.Height / 100;

					cropY = middlePointY - boundingRectHeight / 2;
					if (cropY < 0)
						cropY = 0;

					if (cropY + boundingRectHeight > originalImage.Height)
						cropY = originalImage.Height - boundingRectHeight;

                    cropQuery = $"{cropX},{cropY},{boundingRectWidth},{boundingRectHeight}";
				}

                cropQuery += cropX < width ? $"&width={width + cropX}" : $"&width={width}";
                cropQuery += cropY < height ? $"&height={height + cropY}" : $"&height={height}";
                
                return cropQuery;
	        }
        }

        private static string ResolveImageUrl(ContentReference image)
        {
            return UrlResolver.Current.GetUrl(image);
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
	}



    public class SmartCropCalculator
    {
        public RectangleF CalculateCrop(SizeF imageSize, RectangleF areaOfInterests, SizeF cropSize)
        {
            double cropRatio = cropSize.Width / (double)cropSize.Height;
            double originalRatio = imageSize.Width / (double)imageSize.Height;
            
            var cropX = 0.0;
            var cropY = 0.0;
            var boundingRectHeight = 0.0;
            var boundingRectWidth = 0.0;
            if (cropRatio < originalRatio)
            {
                boundingRectHeight = imageSize.Height;
                boundingRectWidth = boundingRectHeight * cropRatio;

                var xFocalPoint = areaOfInterests.Width / 2 + areaOfInterests.X;
                cropX = xFocalPoint - boundingRectWidth / 2;
                if (cropX < 0)
                    cropX = 0;

                if (cropX + boundingRectWidth > imageSize.Width)
                    cropX = imageSize.Width - boundingRectWidth;
            }
            else
            {
                boundingRectWidth = imageSize.Width;
                boundingRectHeight = boundingRectWidth / cropRatio;

                var yFocalPoint = areaOfInterests.Height / 2 + areaOfInterests.Y;
                cropY = yFocalPoint - boundingRectHeight / 2;
                if (cropY < 0)
                    cropY = 0;

                if (cropY + boundingRectHeight > imageSize.Height)
                    cropY = imageSize.Height - boundingRectHeight;
            }
            return new RectangleF((float)cropX, (float)cropY, (float)boundingRectWidth, (float)boundingRectHeight);
        }

    }
}

