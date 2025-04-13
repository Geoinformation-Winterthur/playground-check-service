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
        public ActionResult<ErrorMessage> Post([FromBody] PlaydeviceFeature playdevice, bool dryRun = false)
        {
            ErrorMessage result = new ErrorMessage();
            User userFromDb = LoginController.getAuthorizedUser(this.User, dryRun);
            if (userFromDb == null || userFromDb.fid == 0)
            {
                return Unauthorized("Sie sind entweder nicht als Kontrolleur in der " +
                    "Spielplatzkontrolle-Datenbank erfasst oder Sie haben keine Zugriffsberechtigung.");
            }

            if (playdevice != null)
            {
                try
                {
                    PlaydeviceFeatureDAO playdeviceDao = new PlaydeviceFeatureDAO();

                    bool hasPlaydeviceToBeChecked = playdeviceDao.HasPlaydeviceToBeChecked(playdevice);
                    if (!hasPlaydeviceToBeChecked)
                    {
                        _logger.LogError("Playdevice with FID " + playdevice.properties.fid + " must not be checked " +
                            "but was sent to service.");
                        result.errorMessage = "SPK-8";
                        return result;
                    }

                    playdeviceDao.Update(playdevice, dryRun);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex.Message);
                    result.errorMessage = "SPK-3";
                }
            }
            else
            {
                result.errorMessage = "SPK-5";
            }
            return Ok(result);
        }

        // PUT playdevice/?fid=...
        [HttpPut]
        [Authorize]
        public ActionResult<ErrorMessage> ExchangeImage([FromBody] PictureString picture, int fid, bool dryRun = false)
        {
            ErrorMessage result = new ErrorMessage();

            if (fid < 1)
            {
                _logger.LogWarning("No FID given in playdevice picture update process.");
                result.errorMessage = "SPK-7";
                return Ok(result);
            }

            string pictureData = "";

            if (picture != null && picture.data != null)
            {
                pictureData = picture.data;
            }

            pictureData = pictureData.Trim();

            if (String.Empty == pictureData)
            {
                _logger.LogWarning("User tried to exchange playdevice picture with an empty picture.");
                result.errorMessage = "SPK-6";
                return Ok(result);
            }

            try
            {
                PlaydeviceFeatureDAO playdeviceDao = new PlaydeviceFeatureDAO();
                playdeviceDao.UpdatePicture(fid, pictureData, dryRun);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                result.errorMessage = "SPK-3";
            }

            return Ok(result);
        }

    }
}