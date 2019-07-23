﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.WebPages;
using EPiServer;
using EPiServer.Core;
using EPiServer.DataAccess;
using EPiServer.Logging;
using EPiServer.PlugIn;
using EPiServer.Scheduler;
using EPiServer.ServiceLocation;
using Forte.SmartFocalPoint.Models.Media;
using SiteDefinition = EPiServer.Web.SiteDefinition;

namespace Forte.SmartFocalPoint
{
    
    public class MediaUpdaterJob : ScheduledJobBase
    {
        private bool _stopSignaled;
        private readonly IContentRepository _contentRepository = ServiceLocator.Current.GetInstance<IContentRepository>();
        private readonly ILogger _logger = LogManager.GetLogger();

        protected bool SetForAll;

        public MediaUpdaterJob()
        {
            IsStoppable = true;
        }

        /// <summary>
        /// Called when a user clicks on Stop for a manually started job, or when ASP.NET shuts down.
        /// </summary>
        public override void Stop()
        {
            _stopSignaled = true;
        }

        /// <summary>
        /// Called when a scheduled job executes
        /// </summary>
        /// <returns>A status message to be stored in the database log and visible from admin mode</returns>
        public override string Execute()
        {
            //Call OnStatusChanged to periodically notify progress of job for manually started jobs
            OnStatusChanged($"Starting execution of {this.GetType()}");

            var assetsRoot = SiteDefinition.Current.GlobalAssetsRoot;
            return UpdateImages(assetsRoot);
            
        }

        private string UpdateImages(ContentReference reference)
        {
            var imagesEnumerable = _contentRepository.GetDescendents(reference)
                .Where(r => _contentRepository.Get<IContent>(r) is ImageData)
                .Select(_contentRepository.Get<ImageData>);
            var images = imagesEnumerable as ImageData[] ?? imagesEnumerable.ToArray();

            var skippedImages = new List<string>();
            var imagesCount = images.Length;
            var updatedCount = 0;


            foreach (var image in images)
            {
                var returnedStatus = UpdateProperties(image);
                updatedCount++;

                if (!returnedStatus.IsEmpty())
                {
                    skippedImages.Add(returnedStatus);
                }

                //For long running jobs periodically check if stop is signaled and if so stop execution
                if (_stopSignaled)
                {
                    return "Stop of job was called.\r\n" + GetStatusMessage(updatedCount, imagesCount, skippedImages);
                }
                
            }
            return "Image files' properties updated.\r\n" + GetStatusMessage(updatedCount, imagesCount, skippedImages);
        }

        private string UpdateProperties(ImageData image)
        {
            
            if(!(image is FocalImageData focalImage))
                return $"{image.Name} is not of type {nameof(FocalImageData)}";

            if (!SetForAll && focalImage.FocalPoint != null)
                return string.Empty;

            //republish image
            var file = _contentRepository.Get<ImageData>(image.ContentLink).CreateWritableClone() as ImageData;
            try
            {
                _contentRepository.Save(file, SaveAction.Publish | SaveAction.ForceCurrentVersion);
            }
            catch (AccessDeniedException ex)
            {
                _logger.Error(ex.Message);
                return $"{image.Name}: {ex.Message}";
            }

            return string.Empty;
        }

        private static string GetStatusMessage(int updatedImagesCount, int allImagesCount, List<string> returnStatuses)
        {
            var message = $"Updated images: {updatedImagesCount} out of {allImagesCount}, " +
                          $"including {returnStatuses.Count} skipped files.\r\n";
            foreach (var statMsg in returnStatuses)
            {
                message = message + statMsg + "\r\n";
            }

            return message;
        }
    }

    [ScheduledPlugIn(DisplayName = "Set FocalPoint For Unset Images", GUID = "DF91149F-796B-441F-A9C0-CF88D38FF58F",
        Description = "Goes over image files and updates focal point properties for unset images")]
    public class UpdateUnsetImages : MediaUpdaterJob
    {

        public UpdateUnsetImages()
        {
            SetForAll = false;
        }

    }

    [ScheduledPlugIn(DisplayName = "Set FocalPoint For All Images", GUID = "515A9EFA-910F-4A19-8B50-DE60678A097E",
        Description = "Goes over image files and updates all of them with focal point properties")]
    public class UpdateAllImages : MediaUpdaterJob
    {

        public UpdateAllImages()
        {
            SetForAll = true;
        }
    }

}
