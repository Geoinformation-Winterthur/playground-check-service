// <copyright company="Vermessungsamt Winterthur">
//      Author: Edgar Butwilowski
//      Copyright (c) Vermessungsamt Winterthur. All rights reserved.
// </copyright>

using System;

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
        public string picture1Base64String { get; set; } = "";
        public string picture1Base64StringThumb { get; set; } = "";
        public string picture2Base64String { get; set; } = "";
        public string picture2Base64StringThumb { get; set; } = "";
        public string picture3Base64String { get; set; } = "";
        public string picture3Base64StringThumb { get; set; } = "";
    }
}
