// <copyright company="Geoinformation Winterthur">
//      Author: Edgar Butwilowski
//      Copyright (c) Geoinformation Winterthur. All rights reserved.
// </copyright>
namespace playground_check_service.Model;

public class Defect
{
    public int tid { get; set; }
    public int playdeviceFid { get; set; }
    public int priority { get; set; }
    public bool done = false;
    public int[] defectPicsTids { get; set; } = [];
    public int[] defectPicsAfterFixingTids { get; set; } = [];
    public string defectDescription { get; set; } = "";
    public DateTime? dateCreation { get; set; }
    public DateTime? dateDone { get; set; }
    public string defectComment { get; set; } = "";
    public int defectsResponsibleBodyId { get; set; } = -1;
    public string errorMessage { get; set; } = "";
}