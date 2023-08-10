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
    private readonly IWebHostEnvironment _env;

    public HomeController(IWebHostEnvironment env)
    {
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

