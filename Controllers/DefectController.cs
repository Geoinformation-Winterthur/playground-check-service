// <copyright company="Vermessungsamt Winterthur">
//      Author: Edgar Butwilowski
//      Copyright (c) Vermessungsamt Winterthur. All rights reserved.
// </copyright>
using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using playground_check_service.Configuration;
using playground_check_service.Model;

namespace playground_check_service.Controllers
{
    /// <summary>
    /// This is the controller for playdevice defects data. Defects data is available
    /// at the /defect route.
    /// </summary>
    /// <remarks>
    /// This class provides the possibility to store defects data in the database.
    /// </remarks>
    [ApiController]
    [Route("[controller]")]
    public class DefectController : ControllerBase
    {
        private readonly ILogger<DefectController> _logger;

        public DefectController(ILogger<DefectController> logger)
        {
            _logger = logger;
        }

        // GET defect/
        [HttpGet]
        [Authorize]
        public Defect Get(int tid)
        {
            DefectDAO defectDAO = new DefectDAO();
            return defectDAO.Read(tid);
        }


        // POST defect/
        [HttpPost]
        [Authorize]
        public ActionResult<ErrorMessage> Post([FromBody] Defect defect, bool dryRun = false)
        {
            ErrorMessage result = new ErrorMessage();
            User userFromDb = LoginController.getAuthorizedUser(this.User, dryRun);
            if (userFromDb == null || userFromDb.fid == 0)
            {
                return Unauthorized("Sie sind entweder nicht als Kontrolleur in der " +
                    "Spielplatzkontrolle-Datenbank erfasst oder Sie haben keine Zugriffsberechtigung.");
            }

            if (defect != null)
            {
                try
                {
                    DefectDAO defectDao = new DefectDAO();
                    defectDao.Update(defect, userFromDb, dryRun);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex.Message);
                    result.errorMessage = "SPK-3";
                }
            }
            else
            {
                result.errorMessage = "SPK-4";
            }
            return Ok(result);
        }

        // PUT defect/?inspectiontid=...
        [HttpPut]
        [Authorize]
        public ActionResult<ErrorMessage> Put([FromBody] Defect defect, bool dryRun = false)
        {
            ErrorMessage result = new ErrorMessage();
            User userFromDb = LoginController.getAuthorizedUser(this.User, dryRun);
            if (userFromDb == null || userFromDb.fid == 0)
            {
                return Unauthorized("Sie sind entweder nicht als Kontrolleur in der " +
                    "Spielplatzkontrolle-Datenbank erfasst oder Sie haben keine Zugriffsberechtigung.");
            }

            if (defect != null)
            {
                try
                {
                    DefectDAO defectDao = new DefectDAO();
                    defectDao.Update(defect, userFromDb, dryRun);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex.Message);
                    result.errorMessage = "SPK-3";
                }
            }
            else
            {
                result.errorMessage = "SPK-4";
            }
            return Ok(result);
        }

        // GET Defect/Picture/3736373?thumb=true
        [HttpGet]
        [Route("/Defect/Picture/{tid}")]
        public IActionResult GetPicture(int tid, bool thumb, bool dryrun = false)
        {
            try
            {
                byte[]? pictureData = null;
                string mimeType = "image/png"; // Fallback-MIME-Typ

                using var pgConn = new NpgsqlConnection(AppConfig.connectionString);
                pgConn.Open();



                NpgsqlCommand selectDefectsCommand = pgConn.CreateCommand();
                string pictureAttribute = "";
                if (thumb)
                    pictureAttribute = "picture_base64_thumb";
                else
                    pictureAttribute = "picture_base64";

                selectDefectsCommand.CommandText = @$"SELECT {pictureAttribute}
                                FROM ""wgr_sp_insp_mangel_foto""
                                WHERE tid={tid}";

                if (dryrun) return Ok();

                using (NpgsqlDataReader reader = selectDefectsCommand.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        string base64String = reader.IsDBNull(0) ? "" : reader.GetString(0);

                        // Prüfe auf data:image/...-Prefix
                        if (base64String.StartsWith("data:"))
                        {
                            // Beispiel: data:image/jpeg;base64,/9j/4AAQSk...
                            int commaIndex = base64String.IndexOf(',');
                            if (commaIndex > 0)
                            {
                                // MIME-Typ extrahieren
                                int semicolonIndex = base64String.IndexOf(';');
                                if (semicolonIndex > 5)
                                {
                                    mimeType = base64String.Substring(5, semicolonIndex - 5); // z. B. image/jpeg
                                }

                                // Base64-Inhalt extrahieren (alles nach dem Komma)
                                string base64Content = base64String.Substring(commaIndex + 1);
                                pictureData = Convert.FromBase64String(base64Content);
                            }
                        }
                        else
                        {
                            // Falls kein Prefix: Direkt dekodieren
                            pictureData = Convert.FromBase64String(base64String);
                        }

                        if (pictureData == null || pictureData.Length == 0)
                        {
                            return NotFound("Kein Bild vorhanden.");
                        }


                        return File(pictureData, mimeType);
                    }
                }


            }
            catch (FormatException ex)
            {
                return BadRequest($"Fehler beim Dekodieren des Bildes: {ex.Message}");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Fehler beim Laden des Bildes: {ex.Message}");
            }
            return StatusCode(500, $"Unbekannter Fehler beim Laden des Bildes.");
        }

        // PUT Defect/Picture/3736373
        [HttpPut]
        [Route("/Defect/Picture/{defectTid}")]
        public IActionResult PutPicture([FromBody] DefectPicture defectPic, int defectTid, bool dryRun = false)
        {
            if (dryRun) Ok();

            using var pgConn = new NpgsqlConnection(AppConfig.connectionString);
            pgConn.OpenAsync();

            using var insertDefectPicCommand = pgConn.CreateCommand();
            insertDefectPicCommand.CommandText = "INSERT INTO \"wgr_sp_insp_mangel_foto\" " +
                    "(tid, tid_maengel, picture_base64, picture_base64_thumb, zeitpunkt)" +
                    "VALUES (" +
                    "(SELECT CASE WHEN max(tid) IS NULL THEN 1 ELSE max(tid) + 1 END FROM \"wgr_sp_insp_mangel_foto\"), " +
                    "@tid_maengel, @picture_base64, @picture_base64_thumb, @zeitpunkt)";

            insertDefectPicCommand.Parameters.AddWithValue("tid_maengel", defectTid);
            insertDefectPicCommand.Parameters.AddWithValue("picture_base64", defectPic.base64StringPicture);
            insertDefectPicCommand.Parameters.AddWithValue("picture_base64_thumb", defectPic.base64StringPictureThumb);
            insertDefectPicCommand.Parameters.AddWithValue("zeitpunkt", defectPic.afterFixing);
            int rowsAffected = insertDefectPicCommand.ExecuteNonQuery();

            if (rowsAffected == 0)
            {
                return BadRequest();
            }
            return Ok();
        }

    }
}