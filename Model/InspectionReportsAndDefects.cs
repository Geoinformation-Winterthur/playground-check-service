// <copyright company="Vermessungsamt Winterthur">
//      Author: Edgar Butwilowski
//      Copyright (c) Vermessungsamt Winterthur. All rights reserved.
// </copyright>

namespace playground_check_service.Model
{
    public class InspectionReportsAndDefects
    {
        public InspectionReport[] inspectionReports { get; set; } = new InspectionReport[0];
        public Defect[] defects { get; set; } = new Defect[0];
    }
}
