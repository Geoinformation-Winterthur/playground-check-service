// <copyright company="Vermessungsamt Winterthur">
//      Author: Edgar Butwilowski
//      Copyright (c) Vermessungsamt Winterthur. All rights reserved.
// </copyright>
namespace playground_check_service.Model
{
    public class Geometry
    {
        public string type { get; set; } = "";
        public double[] coordinates { get; set; } = new double[0];

        public Geometry(){}

        public Geometry(Type type, double[] coordinates)
        {
            if(type == Type.Point)
            {
                this.type = "Point";
            }
            this.coordinates = coordinates;
        }

        public enum Type
        {
            Point
        }
    }
}
