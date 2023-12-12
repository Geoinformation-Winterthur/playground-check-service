// <copyright company="Vermessungsamt Winterthur">
//      Author: Edgar Butwilowski
//      Copyright (c) Vermessungsamt Winterthur. All rights reserved.
// </copyright>
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using playground_check_service.Configuration;

namespace playground_check_service.Controllers
{
    /// <summary>
    /// This is the controller for renovation type data. Renovation type data
    /// is available at the /inspection/renovationtypes route.
    /// </summary>
    /// <remarks>
    /// This class provides a list of the available types of renovations.
    /// The route of this controller only provides read access, no write access.
    /// </remarks>
    [ApiController]
    [Route("/Inspection/renovationtypes")]
    public class RenovationTypesController : ControllerBase
    {
        private readonly ILogger<RenovationTypesController> _logger;

        public RenovationTypesController(ILogger<RenovationTypesController> logger)
        {
            _logger = logger;
        }

        // GET inspection/renovationtypes
        [HttpGet]
        [Authorize]
        public string[] GetTypes()
        {
            return RenovationTypesController._GetTypes();
        }

        internal static string[] _GetTypes()
        {
            List<string> result = new List<string>();

            using (NpgsqlConnection pgConn = new NpgsqlConnection(AppConfig.connectionString))
            {
                pgConn.Open();
                NpgsqlCommand selectComm = pgConn.CreateCommand();
                selectComm.CommandText = "SELECT value " +
                            "FROM \"wgr_sp_sanierungsart_tbd\"";

                using (NpgsqlDataReader reader = selectComm.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string fullDescription = reader.GetString(0);
                        result.Add(fullDescription);
                    }
                }
                pgConn.Close();
            }
            return result.ToArray();
        }

    }

}

