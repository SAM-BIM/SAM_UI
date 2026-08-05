// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Analytical;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace SAM.Analytical.UI.WPF.Tests
{
    public class ZoneDwellingTests
    {
        //Not set, true and false are three distinct states: TryGetValue must come back false for
        //"not set" and true (with the matching bool out-value) for an explicit true/false. A test
        //that only checked true/false would miss a regression that collapsed "not set" into "false".

        [WpfTheory]
        [InlineData(null)]
        [InlineData(true)]
        [InlineData(false)]
        public void ZoneControl_RoundTripsIsDwelling(bool? isDwelling)
        {
            Zone zone = new Zone("Flat 1");
            if (isDwelling.HasValue)
            {
                zone.SetValue(ZoneParameter.IsDwelling, isDwelling.Value);
            }

            ZoneControl zoneControl = new ZoneControl { Zone = zone };

            Zone result = zoneControl.Zone;

            bool hasValue = result.TryGetValue(ZoneParameter.IsDwelling, out bool value);

            Assert.Equal(isDwelling.HasValue, hasValue);
            if (isDwelling.HasValue)
            {
                Assert.Equal(isDwelling.Value, value);
            }
        }

        [Theory]
        [InlineData(null)]
        [InlineData(true)]
        [InlineData(false)]
        public void Zone_RoundTripsIsDwelling_ThroughSAMFileSerialisation(bool? isDwelling)
        {
            Zone zone = new Zone("Corridor");
            if (isDwelling.HasValue)
            {
                zone.SetValue(ZoneParameter.IsDwelling, isDwelling.Value);
            }

            string path = Path.Combine(Path.GetTempPath(), string.Format("{0}.sam", System.Guid.NewGuid()));
            try
            {
                bool written = Core.Convert.ToFile(zone, path, Core.SAMFileType.SAM);
                Assert.True(written);

                List<Zone> zones = Core.Convert.ToSAM<Zone>(path);
                Zone result = Assert.Single(zones);

                bool hasValue = result.TryGetValue(ZoneParameter.IsDwelling, out bool value);

                Assert.Equal(isDwelling.HasValue, hasValue);
                if (isDwelling.HasValue)
                {
                    Assert.Equal(isDwelling.Value, value);
                }
            }
            finally
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }
    }
}
