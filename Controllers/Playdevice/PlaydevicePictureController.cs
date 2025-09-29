// <copyright company="Vermessungsamt Winterthur">
//      Author: Edgar Butwilowski
//      Copyright (c) Vermessungsamt Winterthur. All rights reserved.
// </copyright>
using System.Text;
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


        // GET Playdevice/3736373/Picture
        [HttpGet]
        [Route("/Playdevice/{playdeviceFid}/Picture")]
        public IActionResult GetPicture(int playdeviceFid, bool dryRun = false)
        {
            try
            {
                if (dryRun) return Ok();

                byte[]? pictureData = null;
                string? mimeType = null;

                using var pgConn = new NpgsqlConnection(AppConfig.connectionString);
                pgConn.Open();

                using var cmd = pgConn.CreateCommand();
                cmd.CommandText = "SELECT picture_base64 FROM \"gr_v_spielgeraete\" WHERE fid=@fid";
                cmd.Parameters.AddWithValue("fid", playdeviceFid);

                using var reader = cmd.ExecuteReader();
                if (reader.Read() && !reader.IsDBNull(0))
                {
                    // Variante A: Spalte ist BYTEA mit ROHEN Bildbytes
                    if (reader.GetFieldType(0) == typeof(byte[]))
                    {
                        var bytes = reader.GetFieldValue<byte[]>(0);

                        // Prüfen, ob das eigentlich Text (Base64) ist
                        // Heuristik: enthält nur Base64-Zeichen?
                        var looksLikeText = bytes.Length > 0 && bytes.Take(Math.Min(bytes.Length, 64)).All(b =>
                            (b >= 0x20 && b <= 0x7E) || b == (byte)'\r' || b == (byte)'\n' || b == (byte)'\t');

                        if (looksLikeText)
                        {
                            var asString = Encoding.UTF8.GetString(bytes);

                            if (asString.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                            {
                                int comma = asString.IndexOf(',');
                                int semi = asString.IndexOf(';');
                                if (comma > 0 && semi > 5)
                                {
                                    mimeType = asString.Substring(5, semi - 5); // z.B. image/jpeg
                                    var base64 = asString.Substring(comma + 1);
                                    pictureData = Convert.FromBase64String(base64);
                                }
                            }
                            else
                            {
                                // Reine Base64-Kodierung ohne Prefix
                                pictureData = Convert.FromBase64String(asString);
                            }
                        }
                        else
                        {
                            // Echte Rohbytes
                            pictureData = bytes;
                        }
                    }
                    else
                    {
                        // Variante B: Spalte ist TEXT/VARCHAR
                        var asString = reader.GetString(0);
                        if (asString.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                        {
                            int comma = asString.IndexOf(',');
                            int semi = asString.IndexOf(';');
                            if (comma > 0 && semi > 5)
                            {
                                mimeType = asString.Substring(5, semi - 5);
                                var base64 = asString.Substring(comma + 1);
                                pictureData = Convert.FromBase64String(base64);
                            }
                        }
                        else
                        {
                            pictureData = Convert.FromBase64String(asString);
                        }
                    }
                }

                if (pictureData == null || pictureData.Length == 0)
                {
                    return NotFound("Kein Bild vorhanden.");
                }

                // MIME-Typ notfalls aus Bytes erkennen
                    mimeType ??= DetectMimeFromHeader(pictureData) ?? "application/octet-stream";

                return File(pictureData, mimeType);
            }
            catch (FormatException ex)
            {
                return BadRequest($"Fehler beim Dekodieren des Bildes: {ex.Message}");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Fehler beim Laden des Bildes: {ex.Message}");
            }
        }

        // Einfache Signaturerkennung (PNG, JPEG, GIF, WebP)
        private static string? DetectMimeFromHeader(byte[] bytes)
        {
            if (bytes.Length >= 8 &&
                bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47 &&
                bytes[4] == 0x0D && bytes[5] == 0x0A && bytes[6] == 0x1A && bytes[7] == 0x0A)
                return "image/png";

            if (bytes.Length >= 3 &&
                bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
                return "image/jpeg";

            if (bytes.Length >= 6 &&
                bytes[0] == 0x47 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x38 &&
                (bytes[4] == 0x39 || bytes[4] == 0x37) && bytes[5] == 0x61)
                return "image/gif";

            if (bytes.Length >= 12 &&
                Encoding.ASCII.GetString(bytes, 0, 4) == "RIFF" &&
                Encoding.ASCII.GetString(bytes, 8, 4) == "WEBP")
                return "image/webp";

            return null;
        }


        // PUT Playdevice/3736373/Picture
        [Route("/Playdevice/{playdevicefid}/Picture")]
        [HttpPut]
        [Authorize]
        public IActionResult PutPicture(int playdeviceFid, [FromBody] Image pictureBase64, bool dryRun = false)
        {
            if (dryRun) return Ok();

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