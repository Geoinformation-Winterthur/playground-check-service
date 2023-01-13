// <copyright company="Vermessungsamt Winterthur">
//      Author: Edgar Butwilowski
//      Copyright (c) Vermessungsamt Winterthur. All rights reserved.
// </copyright>
namespace playground_check_service.Model
{
    public class InspectionCriterion
    {
        public string realm { get; set; } = ""; // bereich
        public string designation { get; set; } = ""; // bezeichnung
        public string check { get; set; } = ""; // pruefung
        public string checkShortText { get; set; } = ""; // pruefung_kurztext
        public string maintenance { get; set; } = ""; // wartung
        public bool beforeOpening { get; set; } // vor_eroeffnung
        public bool weekly { get; set; } // woechentlich
        public bool monthly { get; set; } // monatlich
        public bool yearly { get; set; } // jaehrlich
        public string inspectionType { get; set; } = ""; // inspektionsart

        public InspectionReport currentInspectionReport { get; set; } = new InspectionReport();

    }
}