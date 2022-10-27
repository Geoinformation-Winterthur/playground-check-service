// <copyright company="Vermessungsamt Winterthur">
//      Author: Edgar Butwilowski
//      Copyright (c) Vermessungsamt Winterthur. All rights reserved.
// </copyright>
using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

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
        // POST defect/
        [HttpPost]
        [Authorize]
        public IActionResult Post([FromBody] Defect[] defects, bool dryRun = false)
        {
            User userFromDb = LoginController.getAuthorizedUser(this.User);
            if (userFromDb == null || userFromDb.fid == 0)
            {
                return Unauthorized("Sie sind entweder nicht als Kontrolleur in der " +
                    "Spielplatzkontrolle-Datenbank erfasst oder Sie haben keine Zugriffsberechtigung.");
            }

            if (defects != null)
            {
                DefectDAO defectDao = new DefectDAO();
                foreach (Defect defect in defects)
                {
                    defectDao.Update(defect, userFromDb, dryRun);
                }
            }
            return Ok();
        }


        internal static void writeAllDefects(Defect[] defects, int inspectionReportTid, User userFromDb,
                    NpgsqlConnection pgConn, bool dryRun)
        {
            if (defects != null && inspectionReportTid > 0 && userFromDb != null
                    && userFromDb.fid != 0)
            {
                DefectDAO defectDao = new DefectDAO();
                Dictionary<string, int> defectPriorityNames = defectDao.GetDefectPriorityIds();

                foreach (Defect defect in defects)
                {
                    if (defect != null && defect.defectDescription != null &&
                            defect.defectDescription.Trim().Length != 0)
                    {
                        int idPriority = -1;
                        defectPriorityNames.TryGetValue(defect.priority, out idPriority);

                        defectDao.Insert(defect, idPriority, inspectionReportTid,
                                userFromDb, dryRun);
                    }
                }
            }
        }

    }
}