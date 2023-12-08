// <copyright company="Vermessungsamt Winterthur">
//      Author: Edgar Butwilowski
//      Copyright (c) Vermessungsamt Winterthur. All rights reserved.
// </copyright>
using System;

namespace playground_check_service.Model
{
    public class PlaydeviceFeatureProperties
    {
        public int fid { get; set; }
        public string supplier { get; set; } = "";
        public string material { get; set; } = "";
        public int lebensdauer { get; set; }
        public string comment { get; set; } = "";
        public Type type { get; set; } = new Type();
        public DateTime dateOfService { get; set; }
        public InspectionCriterion[] generalInspectionCriteria { get; set; }
                    = new InspectionCriterion[0];
        public InspectionCriterion[] mainFallProtectionInspectionCriteria { get; set; }
                    = new InspectionCriterion[0];
        public InspectionCriterion[] secondaryFallProtectionInspectionCriteria { get; set; }
                    = new InspectionCriterion[0];
        public int recommendedYearOfRenovation { get; set; } = 0;
        public string commentRecommendedYearOfRenovation { get; set; } = "";
        public Defect[] defects { get; set; }
        public InspectionReport[] lastInspectionReports { get; set; }
                    = new InspectionReport[0];
        public InspectionReport[] nextToLastInspectionReports { get; set; }
                    = new InspectionReport[0];

        public string pictureBase64String { get; set; } = "";
        public string mapImageBase64String { get; set; } = "";

        public class Type
        {
            public string name { get; set; } = "";
            public string description { get; set; } = "";
            public string standard { get; set; } = "";
        }
    }
}
