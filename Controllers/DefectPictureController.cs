// <copyright company="Geoinformation Winterthur">
//      Author: Edgar Butwilowski
//      Copyright (c) Geoinformation Winterthur. All rights reserved.
// </copyright>
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using playground_check_service.Configuration;

namespace playground_check_service.Controllers
{
    /// <summary>
    /// This is the controller for playdevice data. Playdevice data is available
    /// at the /playdevice route.
    /// </summary>
    /// <remarks>
    /// This class provides the possibility to store playdevices data in the database.
    /// </remarks>
    [ApiController]
    [Route("Defect/")]
    public class DefectPictureController : ControllerBase
    {



    }
}