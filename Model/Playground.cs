// <copyright company="Vermessungsamt Winterthur">
//      Author: Edgar Butwilowski
//      Copyright (c) Vermessungsamt Winterthur. All rights reserved.
// </copyright>
namespace playground_check_service.Model;

/// <Summary>
/// This is the class that represensts a whole playground.
/// </Summary>
/// <Remarks>
/// This class provides the possibility to access all data of
//// the objecs that are placed on a certain playground.
/// </Remarks>
public class Playground
{
    /// <Summary>Playground-Id</Summary>
    public int Id { get; set; }

    /// <Summary>Playground-Name</Summary>
    public string name { get; set; } = "";
    public string address { get; set; } = "";

    public DateTime dateOfLastInspection { get; set; }
    public DateTime? suspendInspectionFrom { get; set; }
    public DateTime? suspendInspectionTo { get; set; }
    public bool inspectionSuspended { get; set; } = false;
    public bool hasOpenDeviceDefects { get; set; } = false;

    public PlaydeviceFeature[] playdevices { get; set; }
            = new PlaydeviceFeature[0];

    public string[] defectPriorityOptions { get; set; } = Array.Empty<string>();
    public string[] inspectionTypeOptions { get; set; } = Array.Empty<string>();
    public Enumeration[] renovationTypeOptions { get; set; } = Array.Empty<Enumeration>();
    public Enumeration[] defectsResponsibleBodyOptions { get; set; } = Array.Empty<Enumeration>();

    public string chosenTypeOfInspection { get; set; } = "";
}
