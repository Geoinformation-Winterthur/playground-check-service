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
        public ActionResult<Defect> Put([FromBody] Defect defect, bool dryRun = false)
        {
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
                    defectDao.Insert(defect, userFromDb, dryRun);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex.Message);
                    defect.errorMessage = "SPK-3";
                }
            }
            else
            {
                Defect errorMessage = new Defect();
                errorMessage.errorMessage = "SPK-4";
            }
            return Ok(defect);
        }

        // GET Defect/Picture/3736373?thumb=true
        [HttpGet]
        [Route("/Defect/Picture/{tid}")]
        public IActionResult GetPicture(int tid, bool thumb = false, bool dryRun = false)
        {
            try
            {
                if (dryRun) return Ok();

                byte[]? pictureData = null;
                string? mimeType = null;

                using var pgConn = new NpgsqlConnection(AppConfig.connectionString);
                pgConn.Open();

                var column = thumb ? "picture_base64_thumb" : "picture_base64";

                using var cmd = pgConn.CreateCommand();
                cmd.CommandText = $@"SELECT {column}
                             FROM ""wgr_sp_insp_mangel_foto""
                             WHERE tid = @tid";
                cmd.Parameters.AddWithValue("tid", tid);

                using var reader = cmd.ExecuteReader();
                if (!reader.Read() || reader.IsDBNull(0))
                    return NotFound("Kein Bild vorhanden.");

                // 1) BYTEA?
                if (reader.GetFieldType(0) == typeof(byte[]))
                {
                    var bytes = reader.GetFieldValue<byte[]>(0);

                    // a) Sieht nach druckbarem Text aus? (kann Base64, data:-URL ODER Hexstring sein)
                    var looksLikeText = bytes.Length > 0 &&
                        bytes.Take(Math.Min(bytes.Length, 128)).All(b =>
                            (b >= 0x20 && b <= 0x7E) || b is (byte)'\r' or (byte)'\n' or (byte)'\t');

                    if (!looksLikeText)
                    {
                        // → Echte Rohbytes des Bildes
                        pictureData = bytes;
                    }
                    else
                    {
                        // → Text rekonstruieren
                        var text = System.Text.Encoding.UTF8.GetString(bytes).Trim();

                        // b) Falls es ein Hexstring ist (wie im Beispiel "64617461…"): erst aus Hex zu Bytes
                        if (IsLikelyHexString(text))
                        {
                            var hexBytes = HexToBytes(text);
                            // Hex enthielt vermutlich den UTF-8-Text "data:image/...;base64,..."
                            text = System.Text.Encoding.UTF8.GetString(hexBytes).Trim();
                        }

                        // c) Jetzt versuchen: data:-URL → Bildbytes
                        if (TryDecodeDataUrl(text, out var fromDataUrl, out var mt))
                        {
                            pictureData = fromDataUrl;
                            mimeType = mt; // aus data:-Prefix
                        }
                        else
                        {
                            // d) Sonst: purer Base64-String?
                            if (TryBase64Decode(text, out var fromB64))
                                pictureData = fromB64;
                        }
                    }
                }
                else
                {
                    // 2) TEXT/VARCHAR
                    var text = reader.GetString(0)?.Trim() ?? string.Empty;

                    if (IsLikelyHexString(text))
                    {
                        var hexBytes = HexToBytes(text);
                        text = System.Text.Encoding.UTF8.GetString(hexBytes).Trim();
                    }

                    if (TryDecodeDataUrl(text, out var fromDataUrl, out var mt))
                    {
                        pictureData = fromDataUrl;
                        mimeType = mt;
                    }
                    else if (TryBase64Decode(text, out var fromB64))
                    {
                        pictureData = fromB64;
                    }
                }

                if (pictureData == null || pictureData.Length == 0)
                    return NotFound("Kein Bild vorhanden.");

                // Falls kein MIME aus data:-Prefix: versuche Erkennung aus Bytes
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

        // --- Helper ---

        // data:image/<mt>;base64,<payload>
        private static bool TryDecodeDataUrl(string text, out byte[] bytes, out string? mimeType)
        {
            bytes = Array.Empty<byte>();
            mimeType = null;

            if (!text.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                return false;

            var comma = text.IndexOf(',');
            var semi = text.IndexOf(';');
            if (comma <= 0 || semi <= 5) return false;

            mimeType = text.Substring(5, semi - 5).Trim(); // z. B. image/png
            var payload = text[(comma + 1)..].Trim();

            // Manche Ketten verlieren '+' → ' ' (z. B. via WWW-Form-Urlencoded). Reparieren:
            payload = payload.Replace(' ', '+');

            if (!TryBase64Decode(payload, out var data)) return false;
            bytes = data;
            return true;
        }

        private static bool TryBase64Decode(string s, out byte[] data)
        {
            data = Array.Empty<byte>();
            if (string.IsNullOrWhiteSpace(s)) return false;

            // Entferne übliche Noise-Zeichen
            var cleaned = s.Trim();

            // Wenn es wie Hex aussieht, nicht als Base64 versuchen
            if (IsLikelyHexString(cleaned)) return false;

            // Padding tolerant ergänzen
            var mod = cleaned.Length % 4;
            if (mod != 0) cleaned = cleaned.PadRight(cleaned.Length + (4 - mod), '=');

            try
            {
                data = Convert.FromBase64String(cleaned);
                return data.Length > 0;
            }
            catch { return false; }
        }

        private static bool IsLikelyHexString(string s)
        {
            // Sehr lang, nur 0-9A-Fa-f, gerade Länge → wahrscheinlich Hexdump
            if (string.IsNullOrWhiteSpace(s) || (s.Length % 2) != 0) return false;
            if (s.Length < 16) return false;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (!((c >= '0' && c <= '9') ||
                      (c >= 'a' && c <= 'f') ||
                      (c >= 'A' && c <= 'F')))
                    return false;
            }
            return true;
        }

        private static byte[] HexToBytes(string hex)
        {
            var len = hex.Length / 2;
            var bytes = new byte[len];
            for (int i = 0; i < len; i++)
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            return bytes;
        }

        // Signaturen (PNG, JPEG, GIF, WebP)
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
                System.Text.Encoding.ASCII.GetString(bytes, 0, 4) == "RIFF" &&
                System.Text.Encoding.ASCII.GetString(bytes, 8, 4) == "WEBP")
                return "image/webp";

            return null;
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
            insertDefectPicCommand.CommandText = @"INSERT INTO ""wgr_sp_insp_mangel_foto"" 
                (tid, tid_maengel, picture_base64, picture_base64_thumb, zeitpunkt)
                VALUES (
                    (SELECT COALESCE(MAX(tid), 0) + 1 FROM ""wgr_sp_insp_mangel_foto""), 
                    @tid_maengel, @picture_base64, @picture_base64_thumb, @zeitpunkt)
                RETURNING tid;";

            insertDefectPicCommand.Parameters.AddWithValue("tid_maengel", defectTid);
            insertDefectPicCommand.Parameters.AddWithValue("picture_base64", defectPic.base64StringPicture);
            insertDefectPicCommand.Parameters.AddWithValue("picture_base64_thumb", defectPic.base64StringPictureThumb);
            insertDefectPicCommand.Parameters.AddWithValue("zeitpunkt", defectPic.afterFixing);
            var newTid = insertDefectPicCommand.ExecuteScalar();

            if (newTid == null)
                return BadRequest();

            return Ok(new { tid = newTid });
        }

    }
}