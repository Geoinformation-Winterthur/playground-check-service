// <copyright company="Vermessungsamt Winterthur">
//      Author: Edgar Butwilowski
//      Copyright (c) Vermessungsamt Winterthur. All rights reserved.
// </copyright>
namespace playground_check_service.Model
{
    public class PlaydeviceFeature
    {
        public string type { get; set; }
        public PlaydeviceFeatureProperties properties { get; set; }
        public Geometry geometry { get; set; }

        public PlaydeviceDetail[] playdeviceDetails { get; set; }

        public PlaydeviceFeature()
        {
            this.type = "Feature";
            this.properties = new PlaydeviceFeatureProperties();
        }

    }
}
