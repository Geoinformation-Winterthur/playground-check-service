// <copyright company="Vermessungsamt Winterthur">
//      Author: Edgar Butwilowski
//      Copyright (c) Vermessungsamt Winterthur. All rights reserved.
// </copyright>
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using playground_check_service.Model;
using playground_check_service.Configuration;
using playground_check_service.Helper;
using System.Security.Claims;
using System.Net.Mail;

namespace playground_check_service.Controllers;

[ApiController]
[Route("Account/[controller]")]
public class UsersController : ControllerBase
{
    private readonly ILogger<UsersController> _logger;

    public UsersController(ILogger<UsersController> logger)
    {
        _logger = logger;
    }


    // GET /account/users/
    // GET /account/users/?email=...
    // GET /account/users/?uuid=...
    [HttpGet]
    [Authorize(Roles = "administrator")]
    public ActionResult<User[]> GetUsers(string? email)
    {
        List<User> usersFromDb = new List<User>();
        // get data of current user from database:
        using (NpgsqlConnection pgConn = new NpgsqlConnection(AppConfig.connectionString))
        {
            pgConn.Open();
            NpgsqlCommand selectComm = pgConn.CreateCommand();
            selectComm.CommandText = @"SELECT nachname, vorname,
                                    trim(lower(e_mail)), aktiv, rolle
                                FROM ""wgr_sp_kontrolleur""";

            if (email != null)
            {
                email = email.ToLower().Trim();
            }

            if (email != null && email != "")
            {
                selectComm.CommandText += " WHERE trim(lower(e_mail))=@email";
                selectComm.Parameters.AddWithValue("email", email);
            }
            selectComm.CommandText += " ORDER BY vorname, nachname";

            using (NpgsqlDataReader reader = selectComm.ExecuteReader())
            {
                User userFromDb;
                while (reader.Read())
                {
                    userFromDb = new User();
                    userFromDb.mailAddress =
                            reader.IsDBNull(2) ? "" :
                                    reader.GetString(2).ToLower().Trim();
                    if (userFromDb.mailAddress != null && userFromDb.mailAddress != "")
                    {
                        userFromDb.lastName = reader.GetString(0);
                        userFromDb.firstName = reader.GetString(1);

                        if (userFromDb.lastName == null || userFromDb.lastName.Trim().Equals(""))
                        {
                            userFromDb.lastName = "unbekannt";
                        }

                        if (userFromDb.firstName == null || userFromDb.firstName.Trim().Equals(""))
                        {
                            userFromDb.firstName = "unbekannt";
                        }

                        userFromDb.active = reader.GetBoolean(3);
                        userFromDb.role = reader.GetString(4);

                        usersFromDb.Add(userFromDb);
                    }
                }
            }
            pgConn.Close();
        }

        return usersFromDb.ToArray<User>();
    }

    // PUT /account/users/?changepassphrase=false
    [HttpPut]
    [Authorize(Roles = "administrator")]
    public ActionResult<ErrorMessage> UpdateUser([FromBody] User user, bool changePassphrase = false)
    {
        ErrorMessage errorResult = new ErrorMessage();
        try
        {
            string userPassphrase = user.passPhrase;
            user.passPhrase = "";
            if (user == null)
            {
                _logger.LogInformation("No user data provided by user in update user process.");
                errorResult.errorMessage = "SPK-3";
                return Ok(errorResult);
            }

            if (user.mailAddress == null)
            {
                user.mailAddress = "";
            }
            user.mailAddress = user.mailAddress.ToLower().Trim();

            if (user.mailAddress == "")
            {
                _logger.LogWarning("No user data provided by user in update user process.");
                errorResult.errorMessage = "SPK-3";
                return Ok(errorResult);
            }

            try
            {
                MailAddress userMailAddress = new MailAddress(user.mailAddress);
            }
            catch (Exception ex)
            {
                _logger.LogInformation(ex.Message);
                errorResult.errorMessage = "SPK-3";
                return Ok(errorResult);
            }

            if (!User.IsInRole("administrator"))
            {
                _logger.LogWarning("A user who is not an administrator tried to change user data.");
                errorResult.errorMessage = "SPK-3";
                return Ok(errorResult);
            }

            User userInDb = new User();
            ActionResult<User[]> usersInDbResult = this.GetUsers(user.mailAddress);
            User[]? usersInDb = usersInDbResult.Value;
            if (usersInDb == null || usersInDb.Length != 1 || usersInDb[0] == null)
            {
                _logger.LogWarning("Updating user " + user.mailAddress + " is not possible since user is not in the database.");
                errorResult.errorMessage = "SPK-3";
                return Ok(errorResult);
            }

            userInDb = usersInDb[0];
            if (userInDb.role == "administrator")
            {
                int noOfActiveAdmins = _countNumberOfActiveAdmins();
                if (noOfActiveAdmins == 1)
                {
                    if (user.role != "administrator")
                    {
                        _logger.LogWarning("Administrator tried to change role of last administrator. " +
                                "Role cannot be changed since there would be no administrator anymore.");
                        errorResult.errorMessage = "SPK-3";
                        return Ok(errorResult);
                    }

                    if (!user.active)
                    {
                        _logger.LogWarning("Administrator tried to set last administrator inactive. " +
                                "This is not allowed.");
                        errorResult.errorMessage = "SPK-3";
                        return Ok(errorResult);
                    }
                }
            }

            using (NpgsqlConnection pgConn = new NpgsqlConnection(AppConfig.connectionString))
            {
                pgConn.Open();
                NpgsqlCommand updateComm = pgConn.CreateCommand();
                updateComm.CommandText = @"UPDATE ""wgr_sp_kontrolleur""
                            SET nachname=@last_name, vorname=@first_name,
                            rolle=@role, aktiv=@active
                            WHERE e_mail=@e_mail";

                updateComm.Parameters.AddWithValue("last_name", user.lastName);
                updateComm.Parameters.AddWithValue("first_name", user.firstName);
                updateComm.Parameters.AddWithValue("role", user.role);
                updateComm.Parameters.AddWithValue("active", user.active);
                updateComm.Parameters.AddWithValue("e_mail", user.mailAddress);

                int noAffectedRowsStep1 = updateComm.ExecuteNonQuery();

                if (changePassphrase == true)
                {
                    userPassphrase = userPassphrase.Trim();
                    if (userPassphrase.Length < 8)
                    {
                        _logger.LogWarning("Not enough user data provided in update user process.");
                        errorResult.errorMessage = "SPK-3";
                        return Ok(errorResult);
                    }
                }

                int noAffectedRowsStep2 = 0;
                if (changePassphrase == true)
                {
                    string hashedPassphrase = HelperFunctions.hashPassphrase(userPassphrase);
                    updateComm.CommandText = @"UPDATE ""wgr_sp_kontrolleur"" SET
                                    pwd=@pwd WHERE e_mail=@e_mail";
                    updateComm.Parameters.AddWithValue("pwd", hashedPassphrase);
                    updateComm.Parameters.AddWithValue("e_mail", user.mailAddress);
                    noAffectedRowsStep2 = updateComm.ExecuteNonQuery();
                }

                pgConn.Close();

                if (noAffectedRowsStep1 == 1 &&
                    (!changePassphrase || noAffectedRowsStep2 == 1))
                {
                    return Ok(user);
                }

            }

        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message);
            errorResult.errorMessage = "SPK-3";
            return Ok(errorResult);
        }

        _logger.LogError("Fatal error in update user process");
        errorResult.errorMessage = "SPK-3";
        return Ok(errorResult);
    }


    // DELETE /users?email=...
    [HttpDelete]
    [Authorize(Roles = "administrator")]
    public ActionResult<ErrorMessage> DeleteUser(string email)
    {
        ErrorMessage errorResult = new ErrorMessage();

        if (email == null)
        {
            _logger.LogWarning("No user data provided by user in delete user process. " +
                        "Thus process is canceled, no user is deleted.");
            errorResult.errorMessage = "SPK-3";
            return Ok(errorResult);
        }

        email = email.ToLower().Trim();

        if (email == "")
        {
            _logger.LogWarning("No user data provided by user in delete user process. " +
                        "Thus process is canceled, no user is deleted.");
            errorResult.errorMessage = "SPK-3";
            return Ok(errorResult);
        }

        User userInDb = new User();
        ActionResult<User[]> usersInDbResult = this.GetUsers(email);
        User[]? usersInDb = usersInDbResult.Value;
        if (usersInDb == null || usersInDb.Length != 1 || usersInDb[0] == null)
        {
            _logger.LogWarning("User " + email + " cannot be deleted since this user is not in the database.");
            errorResult.errorMessage = "SPK-3";
            return Ok(errorResult);
        }
        else
        {
            userInDb = usersInDb[0];
            if (userInDb.role == "administrator")
            {
                if (_countNumberOfActiveAdmins() == 1)
                {
                    _logger.LogWarning("User tried to delete last administrator. Last administrator cannot be removed.");
                    errorResult.errorMessage = "SPK-3";
                    return Ok(errorResult);
                }
            }
            using (NpgsqlConnection pgConn = new NpgsqlConnection(AppConfig.connectionString))
            {
                pgConn.Open();
                NpgsqlCommand updateComm = pgConn.CreateCommand();
                updateComm.CommandText = @"UPDATE ""wgr_sp_kontrolleur""
                                SET aktiv=false
                                WHERE e_mail=@e_mail";
                updateComm.Parameters.AddWithValue("e_mail", email);

                int noAffectedRows = updateComm.ExecuteNonQuery();

                pgConn.Close();

                if (noAffectedRows == 1)
                {
                    return Ok();
                }
            }
        }

        _logger.LogError("Fatal error.");
        errorResult.errorMessage = "SPK-3";
        return Ok(errorResult);
    }

    private static int _countNumberOfActiveAdmins()
    {
        int count = 0;
        using (NpgsqlConnection pgConn = new NpgsqlConnection(AppConfig.connectionString))
        {
            pgConn.Open();
            NpgsqlCommand selectComm = pgConn.CreateCommand();
            selectComm.CommandText = @"SELECT count(*) 
                            FROM ""wgr_sp_kontrolleur""
                            WHERE aktiv=true AND rolle='administrator'";

            using (NpgsqlDataReader reader = selectComm.ExecuteReader())
            {
                while (reader.Read())
                {
                    count = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
                }
            }
            pgConn.Close();
        }
        return count;
    }

}

