// <copyright company="Vermessungsamt Winterthur">
//      Author: Edgar Butwilowski
//      Copyright (c) Vermessungsamt Winterthur. All rights reserved.
// </copyright>
namespace playground_check_service.Model
{
    public class Playground
    {
        public int id { get; set; }
        public string name { get; set; } = "";
        public string address { get; set; } = "";

        public PlaydeviceFeature[] playdevices { get; set; }
                = new PlaydeviceFeature[0];

        public DateTime dateOfLastInspection { get; set; }

        public string[] defectPriorityOptions { get; set; } = new string[0];
        public string[] inspectionTypeOptions { get; set; } = new string[0];
    }
}
