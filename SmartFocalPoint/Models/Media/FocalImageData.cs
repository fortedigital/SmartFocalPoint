﻿using System.ComponentModel.DataAnnotations;
using EPiServer.Core;
using EPiServer.DataAbstraction;
using EPiServer.DataAnnotations;
using ImageResizer.Plugins.EPiFocalPoint;
using ImageResizer.Plugins.EPiFocalPoint.SpecializedProperties;

namespace Forte.SmartFocalPoint.Models.Media
{
    public abstract class FocalImageData : ImageData, IFocalPointData
    {
        
        public virtual bool SmartFocalPointEnabled { get; set; }

        [BackingType(typeof(PropertyFocalPoint))]
        public virtual FocalPoint FocalPoint { get; set; }

        [ScaffoldColumn(false)]
        public virtual int? OriginalWidth { get; set; }

        [ScaffoldColumn(false)]
        public virtual int? OriginalHeight { get; set; }

        public override void SetDefaultValues(ContentType contentType)
        {
            base.SetDefaultValues(contentType);
            this.SmartFocalPointEnabled = true;
            this.FocalPoint = new FocalPoint
            {
                X = 0.5,
                Y = 0.5
            };
        }
    }
}