// <copyright company="Vermessungsamt Winterthur">
//      Author: Edgar Butwilowski
//      Copyright (c) Vermessungsamt Winterthur. All rights reserved.
// </copyright>
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
    public class PlaydeviceController : ControllerBase
    {
        private readonly ILogger<PlaydeviceController> _logger;

        public PlaydeviceController(ILogger<PlaydeviceController> logger)
        {
            _logger = logger;
        }

        // POST playdevice/
        [HttpPost]
        [Authorize]
        public ActionResult<ErrorMessage> Post([FromBody] PlaydeviceFeature[] playdevices, bool dryRun = false)
        {
            ErrorMessage result = new ErrorMessage();
            User userFromDb = LoginController.getAuthorizedUser(this.User, dryRun);
            if (userFromDb == null || userFromDb.fid == 0)
            {
                return Unauthorized("Sie sind entweder nicht als Kontrolleur in der " +
                    "Spielplatzkontrolle-Datenbank erfasst oder Sie haben keine Zugriffsberechtigung.");
            }

            if (playdevices != null)
            {
                try
                {
                    PlaydeviceFeatureDAO playdeviceDao = new PlaydeviceFeatureDAO();
                    foreach (PlaydeviceFeature playdevice in playdevices)
                    {
                        playdeviceDao.Update(playdevice, userFromDb, dryRun);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex.Message);
                    result.errorMessage = "SPK-3";
                }
            } else {
                result.errorMessage = "SPK-5";
            }
            return Ok(result);
        }

    }
}