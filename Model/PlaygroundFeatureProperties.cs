// <copyright company="Vermessungsamt Winterthur">
//      Author: Edgar Butwilowski
//      Copyright (c) Vermessungsamt Winterthur. All rights reserved.
// </copyright>
using System;

namespace playground_check_service.Model
{
    public class PlaygroundFeatureProperties
    {
        public string uuid { get; set; } = "";
        public int nummer { get; set; } = -1;
        public string name { get; set; } = "";
        public string streetName { get; set; } = "";
        public string houseNo { get; set; } = "";
    }
}
