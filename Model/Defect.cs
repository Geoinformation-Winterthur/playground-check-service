// <copyright company="Vermessungsamt Winterthur">
//      Author: Edgar Butwilowski
//      Copyright (c) Vermessungsamt Winterthur. All rights reserved.
// </copyright>
namespace playground_check_service.Model
{
    public class Defect
    {
        public int tid { get; set; }
        public int playdeviceFid { get; set; }
        public string priority { get; set; } = "";
        public string defectDescription { get; set; } = "";
        public DateTime? dateCreation { get; set; }
        public DateTime? dateDone { get; set; }
        public string defectComment { get; set; } = "";
        public int defectsResponsibleBodyId { get; set; } = -1;
        public DefectPicture[] pictures { get; set;} = Array.Empty<DefectPicture>();
    }
}
