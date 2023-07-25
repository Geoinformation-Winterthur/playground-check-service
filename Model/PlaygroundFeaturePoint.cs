// <copyright company="Geoinformation Winterthur">
//      Author: Edgar Butwilowski
//      Copyright (c) Geoinformation Winterthur. All rights reserved.
// </copyright>
using NetTopologySuite.Geometries;

namespace playground_check_service.Model
{
    public class PlaygroundFeaturePoint
    {
        public string type { get; set; } = "Point";
        public double[] coordinates { get; set; } = new double[0];

        public PlaygroundFeaturePoint()
        {
            this.coordinates = new double[0];
        }

        public PlaygroundFeaturePoint(Point coordinates)
        {
            if(!coordinates.IsEmpty)
            {
                this.coordinates = new double[] { coordinates.X, coordinates.Y };
            }
            else
            {
                this.coordinates = new double[0];
            }
        }

    }
}
