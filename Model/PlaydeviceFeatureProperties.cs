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
        public string supplier { get; set; }
        public string material { get; set; }
        public int lebensdauer { get; set; }
        public string comment { get; set; }
        public Type type { get; set; }
        public DateTime dateOfService { get; set; }
        public InspectionCriterion[] generalInspectionCriteria { get; set; }
        public InspectionCriterion[] mainFallProtectionInspectionCriteria { get; set; }
        public InspectionCriterion[] secondaryFallProtectionInspectionCriteria { get; set; }
        public float costEstimation { get; set; }
        public int recommendedYearOfRenovation { get; set; }
        public string commentRecommendedYearOfRenovation { get; set; }
        public Defect[] defects { get; set; }
        public InspectionReport[] lastInspectionReports { get; set; }
        public InspectionReport[] nextToLastInspectionReports { get; set; }

        public string pictureBase64String { get; set; }
        public string mapImageBase64String { get; set; }

        public class Type
        {
            public string name { get; set; }
            public string description { get; set; }
            public string standard { get; set; }
        }
    }
}
