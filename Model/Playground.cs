// <copyright company="Vermessungsamt Winterthur">
//      Author: Edgar Butwilowski
//      Copyright (c) Vermessungsamt Winterthur. All rights reserved.
// </copyright>
namespace playground_check_service.Model
{
    public class Playground
    {
        public int id { get; set; }
        public string name { get; set; }
        public string address { get; set; }

        public PlaydeviceFeature[] playdevices { get; set; }

        public DateTime dateOfLastInspection { get; set; }

        public string[] defectPriorityOptions { get; set; }
        public string[] inspectionTypeOptions { get; set; }
    }
}
