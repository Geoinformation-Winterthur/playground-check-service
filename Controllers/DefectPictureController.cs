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

    }
}