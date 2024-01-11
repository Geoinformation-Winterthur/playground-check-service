// <copyright company="Vermessungsamt Winterthur">
//      Author: Edgar Butwilowski
//      Copyright (c) Vermessungsamt Winterthur. All rights reserved.
// </copyright>

namespace playground_check_service.Model;

public class DefectPicture {
    public string base64StringPicture { get; set; } = "";
    public string base64StringPictureThumb { get; set; } =  "";
    public bool afterFixing { get; set; } = false;
}