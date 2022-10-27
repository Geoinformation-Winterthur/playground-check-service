// <copyright company="Vermessungsamt Winterthur">
//      Author: Edgar Butwilowski
//      Copyright (c) Vermessungsamt Winterthur. All rights reserved.
// </copyright>
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using playground_check_service.Configuration;
using playground_check_service.Model;

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
    [Route("Playdevice/")]
    public class PlaydevicePictureController : ControllerBase
    {

        // PUT Playdevice/3736373/Picture
        [Route("/Playdevice/{playdevicefid}/Picture")]
        [HttpPut]
        [Authorize]
        public IActionResult PutPicture(int playdeviceFid, [FromBody] Image pictureBase64, bool dryRun = false)
        {
            if(dryRun) return Ok();

            using (NpgsqlConnection pgConn = new NpgsqlConnection(AppConfig.connectionString))
            {
                pgConn.Open();
                NpgsqlCommand updatePictureCommand = pgConn.CreateCommand();
                updatePictureCommand.CommandText = "UPDATE \"gr_v_spielgeraete\" " +
                        "SET picture_base64=@picture_base64 " +
                        "WHERE fid=@fid";
                updatePictureCommand.Parameters.AddWithValue("fid", playdeviceFid);
                updatePictureCommand.Parameters.AddWithValue("picture_base64", pictureBase64.Data);
                updatePictureCommand.ExecuteNonQuery();
            }
            return Ok();
        }

    }
}