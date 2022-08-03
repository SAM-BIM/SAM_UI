﻿using System.Collections.Generic;
using System.Linq;

namespace SAM.Core.Mollier.UI
{
    public static partial class Query
    {
        public static double DefaultPressure(IEnumerable<MollierPoint> mollierPoints, IEnumerable<IMollierProcess> mollierProcesses)
        {
            List<double> pressures = new List<double>();
            if (mollierProcesses != null)
            {
                foreach (MollierProcess process in mollierProcesses)
                {
                    pressures.Add(process.Pressure);
                }
            }
            if (mollierPoints != null)
            {
                foreach (MollierPoint point in mollierPoints)
                {
                    pressures.Add(point.Pressure);
                }
            }

            pressures.Sort();
            int count = 1, number = 1;
            double pressure = pressures[0];
            for (int i = 1; i < pressures.Count(); i++)
            {
                if (pressures[i] == pressures[i - 1])
                {
                    count++;
                }
                else
                {
                    if (count > number)
                    {
                        number = count;
                        pressure = pressures[i - 1];
                    }
                    count = 1;
                }
            }
            return pressure;
        }
    }
}
