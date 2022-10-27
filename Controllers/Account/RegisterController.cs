// <copyright company="Vermessungsamt Winterthur">
//      Author: Edgar Butwilowski
//      Copyright (c) Vermessungsamt Winterthur. All rights reserved.
// </copyright>
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using playground_check_service.Configuration;
using playground_check_service.Helper;

namespace playground_check_service.Controllers;

[ApiController]
[Route("Account/[controller]")]
public class RegisterController : ControllerBase
{
    private readonly ILogger<RegisterController> _logger;

    public RegisterController(ILogger<RegisterController> logger)
    {
        _logger = logger;
    }

    // GET Account/Register?uuid=3736373
    [HttpGet]
    public ContentResult Get(string uuid)
    {
        string content = "<html><body><h1>Passwort setzen</h1>";

        if (uuid == null || uuid.Length != 36)
        {
            content += "<p style=\"color:red;\">Bitte geben Sie in der URL eine g&uuml;ltige UUID an, in der Form: \".../?uuid=...\".</p>";
        }

        content += "<form action=\"?uuid=" + uuid + "\" method=\"post\" enctype=\"application/x-www-form-urlencoded\">" +
        "Passwort (min 8 Zeichen):&nbsp;<input type=\"password\" name=\"password1\" minlength=\"8\" required><br><br>" +
        "Passwort wiederholen:&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;<input type=\"password\" name=\"password2\" minlength=\"8\" required><br><br>" +
        "<input type=\"submit\"></form>";

        content += "</body></html>";

        return base.Content(content, "text/html");
    }


    // POST Account/Register?uuid=3736373
    [HttpPost]
    public ContentResult Post(string uuid, [FromForm] string password1, [FromForm] string password2, bool dryRun = false)
    {

        Thread.Sleep(1000);
        // make brute force costly

        if (uuid == null || uuid.Length != 36)
        {
            _logger.LogWarning("Passwort konnte nicht gesetzt werden. UUID ist fehlerhaft.");
            string error = "<html><body><h1>Passwort konnte nicht gesetzt werden</h1><p>UUID ist fehlerhaft.</p></body></html>";
            return base.Content(error, "text/html");
        }

        if (password1 == null || password1.Length < 8 ||
            password2 == null || password2.Length < 8)
        {
            _logger.LogWarning("Passwort konnte nicht gesetzt werden. Das Passwort erfüllt nicht die Mindestanforderungen.");
            string error = "<html><body><h1>Passwort konnte nicht gesetzt werden</h1><p>Das Passwort erf&uuml;llt nicht die Mindestanforderungen.</p></body></html>";
            return base.Content(error, "text/html");
        }

        if (password1 != password2)
        {
            _logger.LogWarning("Passwort konnte nicht gesetzt werden. Die beiden eingegebenen Passwörter sind nicht gleich.");
            string error = "<html><body><h1>Passwort konnte nicht gesetzt werden</h1><p>Die beiden eingegebenen Passw&ouml;rter sind nicht gleich.</p></body></html>";
            return base.Content(error, "text/html");
        }

        if(dryRun) return base.Content("", "text/html");

        try
        {

            string hashedPassphrase = HelperFunctions.hashPassphrase(password1);

            using (NpgsqlConnection pgConn = new NpgsqlConnection(AppConfig.connectionString))
            {
                pgConn.Open();

                NpgsqlCommand updatePasswordCommand = pgConn.CreateCommand();
                updatePasswordCommand.CommandText = "UPDATE wgr_sp_kontrolleur" +
                        " SET pwd=@hashed_password, registrierung_uuid=NULL" +
                        " WHERE registrierung_uuid=@uuid";
                updatePasswordCommand.Parameters.AddWithValue("hashed_password", hashedPassphrase);
                updatePasswordCommand.Parameters.AddWithValue("uuid", uuid);

                int affectedRows = updatePasswordCommand.ExecuteNonQuery();
                if (affectedRows == 0)
                {
                    _logger.LogWarning("Passwort konnte nicht gesetzt werden. Ursache: Einmalige UUID des Registrierungslinks ist nicht gültig."+
                            " Eventuell wurde der Link schon verbraucht oder der Link enthält einen Fehler.");
                    string error = "<html><body><h1>Passwort konnte nicht gesetzt werden</h1>" +
                            "<p>Es ist ein Fehler aufgetreten.</p>" +
                            "<p>Bitte wenden Sie sich an den Applikationsverantwortlichen.</p></body></html>";
                    return base.Content(error, "text/html");
                }
                else if(affectedRows > 1)
                {
                    _logger.LogError("Eine UUID für die Benutzer-Registrierung lag in der Datenbank mehrfah vor." +
                            " Dies darf eigentlich nie vorkommen. Hier scheint ein Applikationsfehler vorzuliegen. Bitte prüfen.");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("Passwort konnte nicht gesetzt werden. Ursache: " + ex.Message);
            string error = "<html><body><h1>Passwort konnte nicht gesetzt werden</h1>" +
                    "<p>Es ist ein Fehler aufgetreten.</p>" +
                    "<p>Bitte wenden Sie sich an den Applikationsverantwortlichen.</p></body></html>";
            return base.Content(error, "text/html");
        }

        _logger.LogInformation("Passwort eines Benutzers wurde neu gesetzt.");
        string success = "<html><body><h1>Passwort gesetzt</h1><p>Vielen Dank.</p></body></html>";
        return base.Content(success, "text/html");

    }

}

