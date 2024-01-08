// <copyright company="Geoinformation Winterthur">
//      Author: Edgar Butwilowski
//      Copyright (c) Geoinformation Winterthur. All rights reserved.
// </copyright>
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using playground_check_service.Configuration;

namespace playground_check_service.Controllers
{
    /// <summary>
    /// This is the controller for document data. Document data is available
    /// at the /document route.
    /// </summary>
    [ApiController]
    [Route("Document/")]
    public class DocumentController : ControllerBase
    {

        // GET Document/3736373?type=abnahme
        [Route("/Document/{documentfid}")]
        [HttpGet]
        [Authorize]
        public IActionResult GetDocument(int documentFid, string type)
        {

            if (type == null)
                return BadRequest();

            type = type.Trim().ToLower();

            if (type != "abnahme" && type != "zertifikat")
                return BadRequest();

            using (NpgsqlConnection pgConn = new NpgsqlConnection(AppConfig.connectionString))
            {
                pgConn.Open();

                NpgsqlCommand selectPdfCommand = pgConn.CreateCommand();
                if (type == "abnahme")
                {
                    selectPdfCommand.CommandText = "SELECT abnahmedokument" +
                            " FROM wgr_sp_abnahmen" +
                            " WHERE fid=@fid";
                }
                else
                {
                    selectPdfCommand.CommandText = "SELECT zertifikatsdokument" +
                            " FROM wgr_sp_zertifikat" +
                            " WHERE fid=@fid";
                }
                selectPdfCommand.Parameters.AddWithValue("fid", documentFid);

                NpgsqlDataReader reader = selectPdfCommand.ExecuteReader();
                if (reader.Read())
                {
                    byte[] pdfBytes = (byte[])reader[0];
                    return File(pdfBytes, "application/pdf");
                }
            }

            return BadRequest();
        }

    }
}