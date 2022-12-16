﻿using Newtonsoft.Json.Linq;
using SAM.Geometry.Spatial;
using System;
using System.Collections.Generic;
using SAM.Geometry.UI;

namespace SAM.Analytical.UI
{
    public class AnalyticalTwoDimensionalViewSettings : TwoDimensionalViewSettings, IAnalyticalViewSettings
    {
        public SpaceAppearanceSettings SpaceAppearanceSettings { get; set; }

        public AnalyticalTwoDimensionalViewSettings(Guid guid, string name, Plane plane, AppearanceSettings appearanceSettings, IEnumerable<Type> types)
            :base(guid, name, plane, appearanceSettings, types)
        {

        }

        public AnalyticalTwoDimensionalViewSettings(Plane plane, string name, AppearanceSettings appearanceSettings, IEnumerable<Type> types)
            : base(Guid.NewGuid(), name, plane, appearanceSettings, types)
        {

        }

        public AnalyticalTwoDimensionalViewSettings(TwoDimensionalViewSettings twoDimensionalViewSettings)
            : base(twoDimensionalViewSettings)
        {

        }

        public AnalyticalTwoDimensionalViewSettings(JObject jObject)
            : base(jObject)
        {

        }

        public AnalyticalTwoDimensionalViewSettings(AnalyticalTwoDimensionalViewSettings analyticalTwoDimensionalViewSettings)
            :base(analyticalTwoDimensionalViewSettings)
        {
            if (analyticalTwoDimensionalViewSettings?.SpaceAppearanceSettings != null)
            {
                SpaceAppearanceSettings = new SpaceAppearanceSettings(analyticalTwoDimensionalViewSettings.SpaceAppearanceSettings);
            }
        }

        public AnalyticalTwoDimensionalViewSettings(string name, AnalyticalTwoDimensionalViewSettings analyticalTwoDimensionalViewSettings)
            : base(name, analyticalTwoDimensionalViewSettings)
        {
            if (analyticalTwoDimensionalViewSettings?.SpaceAppearanceSettings != null)
            {
                SpaceAppearanceSettings = new SpaceAppearanceSettings(analyticalTwoDimensionalViewSettings.SpaceAppearanceSettings);
            }
        }

        public AnalyticalTwoDimensionalViewSettings(Guid guid, string name, AnalyticalTwoDimensionalViewSettings analyticalTwoDimensionalViewSettings)
            : base(guid, name, analyticalTwoDimensionalViewSettings)
        {
            if (analyticalTwoDimensionalViewSettings?.SpaceAppearanceSettings != null)
            {
                SpaceAppearanceSettings = new SpaceAppearanceSettings(analyticalTwoDimensionalViewSettings.SpaceAppearanceSettings);
            }
        }

        public override bool FromJObject(JObject jObject)
        {
            if(!base.FromJObject(jObject))
            {
                return false;
            }

            if(jObject.ContainsKey("SpaceAppearanceSettings"))
            {
                SpaceAppearanceSettings = new SpaceAppearanceSettings(jObject.Value<JObject>("SpaceAppearanceSettings"));
            }

            return true;
        }

        public override JObject ToJObject()
        {
            JObject jObject = base.ToJObject();
            if(jObject == null)
            {
                return null;
            }

            if(SpaceAppearanceSettings != null)
            {
                jObject.Add("SpaceAppearanceSettings", SpaceAppearanceSettings.ToJObject());
            }

            return jObject;
        }
    }
}