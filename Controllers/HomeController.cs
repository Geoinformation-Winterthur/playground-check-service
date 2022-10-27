// <copyright company="Vermessungsamt Winterthur">
//      Author: Edgar Butwilowski
//      Copyright (c) Vermessungsamt Winterthur. All rights reserved.
// </copyright>
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace playground_check_service.Controllers;

[ApiController]
[Route("/")]
public class HomeController : ControllerBase
{
    private readonly ILogger<HomeController> _logger;

    public HomeController(ILogger<HomeController> logger)
    {
        _logger = logger;
    }


    [HttpGet]
    public ActionResult<string> Get()
    {
        return JsonSerializer.Serialize<string>("Playground service works.");
    }

}

