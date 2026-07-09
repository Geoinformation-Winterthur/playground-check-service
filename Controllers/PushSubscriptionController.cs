// <copyright company="Geoinformation Winterthur">
//      Author: Edgar Butwilowski
//      Copyright (c) Geoinformation Winterthur. All rights reserved.
// </copyright>
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using playground_check_service.Model;

namespace playground_check_service.Controllers;

[ApiController]
[Route("[controller]")]
public class PushSubscriptionController : ControllerBase
{
    private readonly ILogger<PushSubscriptionController> _logger;

    public PushSubscriptionController(ILogger<PushSubscriptionController> logger)
    {
        _logger = logger;
    }

    // POST /PushSubscription/Register
    [HttpPost]
    [Route("Register")]
    [Authorize]
    public ActionResult<ErrorMessage> Register([FromBody] PushSubscriptionRegistration subscription, bool dryRun = false)
    {
        ErrorMessage result = new();
        User userFromDb = LoginController.getAuthorizedUser(this.User, dryRun);
        if (userFromDb == null || userFromDb.fid <= 0)
        {
            return Unauthorized("Sie sind entweder nicht als Kontrolleur in der " +
                "Spielplatzkontrolle-Datenbank erfasst oder Sie haben keine Zugriffsberechtigung.");
        }

        try
        {
            if (string.IsNullOrWhiteSpace(subscription.userAgent))
                subscription.userAgent = Request.Headers["User-Agent"].ToString();

            PushSubscriptionDAO dao = new();
            dao.Register(userFromDb, subscription, dryRun);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not register push subscription.");
            result.errorMessage = "SPK-3";
        }
        return Ok(result);
    }

    // DELETE /PushSubscription/Unregister
    [HttpDelete]
    [Route("Unregister")]
    [Authorize]
    public ActionResult<ErrorMessage> Unregister([FromBody] PushSubscriptionRegistration subscription, bool dryRun = false)
    {
        ErrorMessage result = new();
        User userFromDb = LoginController.getAuthorizedUser(this.User, dryRun);
        if (userFromDb == null || userFromDb.fid <= 0)
        {
            return Unauthorized("Sie sind entweder nicht als Kontrolleur in der " +
                "Spielplatzkontrolle-Datenbank erfasst oder Sie haben keine Zugriffsberechtigung.");
        }

        try
        {
            PushSubscriptionDAO dao = new();
            dao.Unregister(userFromDb, subscription.endpoint, dryRun);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not unregister push subscription.");
            result.errorMessage = "SPK-3";
        }
        return Ok(result);
    }

    // GET /PushSubscription/Me
    [HttpGet]
    [Route("Me")]
    [Authorize]
    public ActionResult<PushSubscriptionRegistration[]> ReadOwnSubscriptions(bool dryRun = false)
    {
        User userFromDb = LoginController.getAuthorizedUser(this.User, dryRun);
        if (userFromDb == null || userFromDb.fid <= 0)
        {
            return Unauthorized("Sie sind entweder nicht als Kontrolleur in der " +
                "Spielplatzkontrolle-Datenbank erfasst oder Sie haben keine Zugriffsberechtigung.");
        }

        PushSubscriptionDAO dao = new();
        return Ok(dao.ReadActiveSubscriptionsOfUser(userFromDb.fid));
    }
}
