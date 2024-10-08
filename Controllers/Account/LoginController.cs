// <copyright company="Vermessungsamt Winterthur">
//      Author: Edgar Butwilowski
//      Copyright (c) Vermessungsamt Winterthur. All rights reserved.
// </copyright>
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using playground_check_service.Model;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.IdentityModel.Tokens.Jwt;
using Npgsql;
using playground_check_service.Configuration;
using playground_check_service.Helper;
using System.Net.Mail;

namespace playground_check_service.Controllers;

[ApiController]
[Route("Account/[controller]")]
public class LoginController : ControllerBase
{
    private readonly ILogger<LoginController> _logger;

    public LoginController(ILogger<LoginController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Authenticates a user and returns a security token string
    /// </summary>
    /// <param name="receivedUser"></param>
    /// <param name="dryRun"></param>
    /// <returns>A security token string for the authenticated user</returns>
    /// <remarks>
    /// Sample request:
    ///     POST /Account/Login
    ///     {
    ///        "mailAddress": "...",
    ///        "passPhrase": "..."
    ///     }
    /// </remarks>
    /// <response code="200">Returns the security token</response>
    /// <response code="400">If user data is missing</response>
    /// <response code="401">If user could not be authenticated</response>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult Login([FromBody] User receivedUser, bool dryRun = false)
    {
        if (receivedUser == null || receivedUser.mailAddress == null)
        {
            // login data is missing something important, thus:
            _logger.LogWarning("No or bad login credentials provided in a login attempt.");
            return BadRequest("Keine oder falsche Login-Daten.");
        }

        receivedUser.mailAddress = receivedUser.mailAddress.ToLower().Trim();

        if (receivedUser.mailAddress.Equals("") || receivedUser.mailAddress.Any(Char.IsWhiteSpace))
        {
            // login data is missing something important, thus:
            _logger.LogWarning("No or bad login credentials provided in a login attempt.");
            return BadRequest("Keine oder falsche Login-Daten.");
        }

        try
        {
            MailAddress mailAddress = new MailAddress(receivedUser.mailAddress);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("No or bad login credentials provided in a login attempt.");
            return BadRequest("Keine oder falsche Login-Daten.");
        }

        _logger.LogInformation("User " + receivedUser.mailAddress + " tries to log in.");

        if (receivedUser.passPhrase == null || receivedUser.passPhrase.Trim().Equals(""))
        {
            // login data is missing something important, thus:
            _logger.LogWarning("No or bad login credentials provided by user.");
            return BadRequest("Keine oder falsche Login-Daten.");
        }

        string hashedPassphrase = HelperFunctions.hashPassphrase(receivedUser.passPhrase);

        _logger.LogInformation("User " + receivedUser.mailAddress + " provided a non-empty password. Now trying to authenticate...");

        // get corresponding user from database:
        User userFromDb = LoginController._getUserFromDatabase(receivedUser.mailAddress, dryRun);

        if (userFromDb != null)
        {
            // if user is already in database:
            _logger.LogInformation("User " + receivedUser.mailAddress + " was found in the database.");
            if (userFromDb.lastLoginAttempt != null)
            {
                // prohibit brute force attack:
                DateTime currentDatabaseTime = (DateTime)userFromDb.databaseTime;
                DateTime lastLoginAttemptTime = (DateTime)userFromDb.lastLoginAttempt;
                double diffInSeconds = (currentDatabaseTime - lastLoginAttemptTime).TotalSeconds;
                if (diffInSeconds < 3)
                {
                    Thread.Sleep(3000);
                }
            }

            LoginController._updateLoginTimestamp(receivedUser.mailAddress, dryRun);

            if (userFromDb.mailAddress != null && userFromDb.passPhrase != null
                && userFromDb.passPhrase.Equals(hashedPassphrase) && userFromDb.active)
            {
                string securityKey = AppConfig.Configuration.GetValue<string>("SecurityKey");
                byte[] securityKeyByteArray = Encoding.UTF8.GetBytes(securityKey);
                SymmetricSecurityKey key = new SymmetricSecurityKey(securityKeyByteArray);
                SigningCredentials signingCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

                List<Claim> userClaims = new()
                {
                    new Claim(ClaimTypes.Email, userFromDb.mailAddress),
                    new Claim(ClaimTypes.GivenName, userFromDb.firstName),
                    new Claim(ClaimTypes.Name, userFromDb.lastName),
                    new Claim(ClaimTypes.Role, userFromDb.role)
                };

                string serviceDomain = AppConfig.Configuration.GetValue<string>("URL:ServiceDomain");
                string serviceBasePath = AppConfig.Configuration.GetValue<string>("URL:ServiceBasePath");

                JwtSecurityToken securityToken = new(
                    issuer: serviceDomain + serviceBasePath,
                    audience: serviceDomain + serviceBasePath,
                    claims: userClaims,
                    signingCredentials: signingCredentials,
                    expires: DateTime.UtcNow.AddDays(2)  // the login expires after 2 days
                );

                string securityTokenString = new JwtSecurityTokenHandler().WriteToken(securityToken);

                _logger.LogInformation("User " + receivedUser.mailAddress + " has logged in.");
                return Ok(new { securityTokenString });
            }
            else
            {
                _logger.LogWarning("The provided credentials of user " + receivedUser.mailAddress +
                        " did not match with the credentials in the database.");
                return Unauthorized("Keine oder falsche Login-Daten.");
            }
        }
        else
        {
            // if user is not already in database:
            _logger.LogWarning("User " + receivedUser.mailAddress + " could not be found in the database.");
            _logger.LogWarning("User " + receivedUser.mailAddress + " is not authenticated.");

            try
            {
                using (NpgsqlConnection pgConn = new NpgsqlConnection(AppConfig.connectionString))
                {
                    pgConn.Open();
                    NpgsqlCommand insertComm = pgConn.CreateCommand();
                    insertComm.CommandText = @"INSERT INTO ""wgr_sp_kontrolleur""
                              (nachname, vorname, e_mail, pwd, rolle, aktiv, is_new)
                       VALUES(@nachname, @vorname, @e_mail, @pwd, 'inspector', false, true)";
                    insertComm.Parameters.AddWithValue("nachname", receivedUser.lastName);
                    insertComm.Parameters.AddWithValue("vorname", receivedUser.firstName);
                    insertComm.Parameters.AddWithValue("e_mail", receivedUser.mailAddress);
                    insertComm.Parameters.AddWithValue("pwd", hashedPassphrase);

                    insertComm.ExecuteNonQuery();

                    pgConn.Close();

                    return Unauthorized("Sie sind entweder nicht als Kontrolleur in der " +
                            "Spielplatzkontrolle-Datenbank erfasst oder Sie haben keine Zugriffsberechtigung." +
                            "Der Administrator wird informiert und wird Ihnen gegebenenfalls den Zugriff gewähren.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                return BadRequest("Ein kritischer Fehler ist aufgetreten. Bitte kontaktieren Sie den Administrator.");
            }
        }
    }


    internal static User getAuthorizedUser(ClaimsPrincipal userFromService, bool dryRun)
    {
        if (dryRun) return null;

        Claim userMailAddressClaim = null;
        foreach (Claim userClaim in userFromService.Claims)
        {
            if (userClaim.Type == ClaimTypes.Email)
            {
                userMailAddressClaim = userClaim;
            }
        }
        string userMailAddress = null;
        if (userMailAddressClaim != null)
        {
            userMailAddress = userMailAddressClaim.Value.Trim().ToLower();
        }
        if (userMailAddress == null || userMailAddress.Equals(""))
        {
            return null;
        }
        User userFromDb = LoginController._getUserFromDatabase(userMailAddress, dryRun);
        return userFromDb;
    }

    private static User _getUserFromDatabase(string eMailAddress, bool dryRun)
    {
        if (dryRun) return null;

        User userFromDb = null;
        // get data of current user from database:
        using (NpgsqlConnection pgConn = new NpgsqlConnection(AppConfig.connectionString))
        {
            pgConn.Open();
            NpgsqlCommand selectComm = pgConn.CreateCommand();
            selectComm.CommandText = "SELECT fid, nachname, vorname, e_mail, pwd, " +
                        "letzter_anmeldeversuch, CURRENT_TIMESTAMP(0)::TIMESTAMP, " +
                        "rolle, aktiv " +
                        "FROM \"wgr_sp_kontrolleur\" WHERE trim(lower(e_mail))=@e_mail";
            selectComm.Parameters.AddWithValue("e_mail", eMailAddress);

            using (NpgsqlDataReader reader = selectComm.ExecuteReader())
            {
                Boolean hasUser = reader.Read();
                if (hasUser)
                {
                    userFromDb = new User();
                    userFromDb.fid = reader.IsDBNull(0) ? -1 : reader.GetInt32(0);
                    userFromDb.mailAddress = reader.GetString(3);
                    userFromDb.passPhrase = reader.GetString(4);

                    userFromDb.lastName = reader.GetString(1);
                    userFromDb.firstName = reader.GetString(2);
                    userFromDb.lastLoginAttempt = !reader.IsDBNull(5) ? reader.GetDateTime(5) : null;
                    userFromDb.databaseTime = !reader.IsDBNull(6) ? reader.GetDateTime(6) : null;
                    userFromDb.role = !reader.IsDBNull(7) ? reader.GetString(7) : "";
                    userFromDb.active = !reader.IsDBNull(8) ? reader.GetBoolean(8) : false;

                    if (userFromDb.lastName == null || userFromDb.lastName.Trim().Equals(""))
                    {
                        userFromDb.lastName = "Nachname unbekannt";
                    }

                    if (userFromDb.firstName == null || userFromDb.firstName.Trim().Equals(""))
                    {
                        userFromDb.firstName = "Vorname unbekannt";
                    }

                }
            }
            pgConn.Close();
        }

        return userFromDb;
    }

    private static void _updateLoginTimestamp(string eMailAddress, bool dryRun)
    {
        if (dryRun) return;

        using (NpgsqlConnection pgConn = new NpgsqlConnection(AppConfig.connectionString))
        {
            pgConn.Open();
            NpgsqlCommand updateTimestampComm = pgConn.CreateCommand();
            updateTimestampComm.CommandText = "UPDATE \"wgr_sp_kontrolleur\"" +
                        " SET letzter_anmeldeversuch=CURRENT_TIMESTAMP " +
                        " WHERE trim(lower(e_mail))=@e_mail";
            updateTimestampComm.Parameters.AddWithValue("e_mail", eMailAddress);
            updateTimestampComm.ExecuteNonQuery();

            pgConn.Close();
        }
    }

}

