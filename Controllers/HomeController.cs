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
    private readonly Microsoft.AspNetCore.Hosting.IWebHostEnvironment _env;

    public HomeController(ILogger<HomeController> logger,
        Microsoft.AspNetCore.Hosting.IWebHostEnvironment env)
    {
        _logger = logger;
        _env = env;
    }


    [HttpGet]
    public ActionResult<string> Get()
    {
        string environment = "No idea. This means something went wrong.";
        if(_env.IsDevelopment())
        {
            environment = "Development";
        }
        else if(_env.IsProduction())
        {
            environment = "Production";

        } else if(_env.IsStaging())
        {
            environment = "Staging";
        }
        return JsonSerializer.Serialize<string>("Playground service works. "+
                "Environment: " + environment);
    }

}

