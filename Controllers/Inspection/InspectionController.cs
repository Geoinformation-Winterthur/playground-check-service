// <copyright company="Vermessungsamt Winterthur">
//      Author: Edgar Butwilowski
//      Copyright (c) Vermessungsamt Winterthur. All rights reserved.
// </copyright>
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using NpgsqlTypes;
using playground_check_service.Configuration;
using playground_check_service.Model;

namespace playground_check_service.Controllers
{
    /// <summary>
    /// This is the controller for inspection data. Inspection data is available
    /// at the /inspection route.
    /// </summary>
    /// <remarks>
    /// This class provides a list of the available types of inspections, as well
    /// as the inspection reports data itself. The route of this controller only
    /// provides read access, no write access.
    /// </remarks>
    [ApiController]
    [Route("[controller]")]
    public class InspectionController : ControllerBase
    {
        private readonly ILogger<InspectionController> _logger;

        public InspectionController(ILogger<InspectionController> logger)
        {
            _logger = logger;
        }

        // POST inspection/
        [HttpPost]
        [Authorize]
        public ActionResult<ErrorMessage> Post([FromBody] InspectionReport[] inspectionReports, bool dryRun = false)
        {
            ErrorMessage result = new ErrorMessage();
            User userFromDb = LoginController.getAuthorizedUser(this.User, dryRun);
            if (userFromDb == null)
            {
                result.errorMessage = "Unauthorized";
                return Unauthorized(result.errorMessage);
                // Sie sind entweder nicht als Kontrolleur in der
                // Spielplatzkontrolle-Datenbank erfasst oder Sie haben keine Zugriffsberechtigung.
            }

            if (inspectionReports == null || inspectionReports.Length == 0)
            {
                result.errorMessage = "SPK-0";
                return Ok(result);  // Es wurden keine Kontrollberichte empfangen.
            }

            string inspectionType = inspectionReports[0].inspectionType;

            Dictionary<int, DateTime> playdeviceDates = new Dictionary<int, DateTime>();
            Dictionary<int, DateTime> playdeviceDetailDates = new Dictionary<int, DateTime>();
            foreach (InspectionReport inspectionReport in inspectionReports)
            {
                if (inspectionReport.inspectionType != inspectionType)
                {
                    result.errorMessage = "SPK-6";
                    return Ok(result);
                    // Die gesendeten Berichte haben variierende Inspektionsarten.
                }

                if (inspectionReport.playdeviceDateOfService == null)
                {
                    result.errorMessage = "SPK-1";
                    return Ok(result);
                    // Es wurden Kontrollberichte ohne Inspektionsdatum geliefert.
                }
                if (inspectionReport.playdeviceFid > 0 && !playdeviceDates.ContainsKey(inspectionReport.playdeviceFid))
                {
                    playdeviceDates.Add(inspectionReport.playdeviceFid,
                                    inspectionReport.playdeviceDateOfService);
                }
                else if (inspectionReport.playdeviceDetailFid > 0 &&
                          !playdeviceDetailDates.ContainsKey(inspectionReport.playdeviceDetailFid))
                {
                    playdeviceDetailDates.Add(inspectionReport.playdeviceDetailFid,
                                    inspectionReport.playdeviceDateOfService);
                }
            }

            using (NpgsqlConnection pgConn = new NpgsqlConnection(AppConfig.connectionString))
            {
                pgConn.Open();
                try
                {
                    NpgsqlCommand beginTrans = pgConn.CreateCommand();
                    beginTrans.CommandText = "BEGIN TRANSACTION";
                    beginTrans.ExecuteNonQuery();

                    NpgsqlCommand lockTable = pgConn.CreateCommand();
                    lockTable.CommandText = "LOCK TABLE \"wgr_sp_insp_bericht\" IN ACCESS EXCLUSIVE MODE";
                    lockTable.ExecuteNonQuery();

                    bool hasExistingInspections = false;
                    NpgsqlCommand selectIfExisting;
                    foreach (KeyValuePair<int, DateTime> playdeviceDate in playdeviceDates)
                    {
                        selectIfExisting = pgConn.CreateCommand();
                        selectIfExisting.CommandText = @"SELECT count(*) FROM ""wgr_sp_insp_bericht"" 
                                    WHERE fid_spielgeraet=@fid_spielgeraet 
                                    AND inspektionsart = @inspektionsart 
                                    AND datum_inspektion = @datum_inspektion";
                        selectIfExisting.Parameters.AddWithValue("fid_spielgeraet", playdeviceDate.Key);
                        NpgsqlDate dateOfService = (NpgsqlDate)playdeviceDate.Value;
                        selectIfExisting.Parameters.AddWithValue("inspektionsart", inspectionType);
                        selectIfExisting.Parameters.AddWithValue("datum_inspektion", dateOfService);

                        using (NpgsqlDataReader reader = selectIfExisting.ExecuteReader())
                        {
                            reader.Read();
                            hasExistingInspections = hasExistingInspections || reader.GetInt32(0) != 0;
                        }
                    }

                    if (!hasExistingInspections)
                    {
                        foreach (KeyValuePair<int, DateTime> playdeviceDetailDate in playdeviceDetailDates)
                        {
                            selectIfExisting = pgConn.CreateCommand();
                            selectIfExisting.CommandText = @"SELECT count(*) FROM ""wgr_sp_insp_bericht"" 
                                        WHERE fid_geraet_detail=@fid_geraet_detail 
                                        AND inspektionsart = @inspektionsart 
                                        AND datum_inspektion = @datum_inspektion";
                            selectIfExisting.Parameters.AddWithValue("fid_geraet_detail", playdeviceDetailDate.Key);
                            NpgsqlDate dateOfService = (NpgsqlDate)playdeviceDetailDate.Value;
                            selectIfExisting.Parameters.AddWithValue("inspektionsart", inspectionType);
                            selectIfExisting.Parameters.AddWithValue("datum_inspektion", dateOfService);

                            using (NpgsqlDataReader reader = selectIfExisting.ExecuteReader())
                            {
                                reader.Read();
                                hasExistingInspections = hasExistingInspections || reader.GetInt32(0) != 0;
                            }
                        }
                    }


                    if (hasExistingInspections)
                    {
                        result.errorMessage = "SPK-2";
                        return Ok(result);
                        // Für diesen Spielplatz ist am selben Tag bereits ein Bericht mit derselben Inspektionsart eingereicht worden.
                    }

                    int inspectionTid = -1;
                    if (inspectionReports.Length != 0)
                    {
                        inspectionTid = InspectionController.writeInspection(inspectionReports[0],
                                        userFromDb, pgConn, dryRun);
                    }

                    NpgsqlCommand selectIfCanBeChecked;
                    bool canBeChecked;
                    foreach (InspectionReport inspectionReport in inspectionReports)
                    {
                        canBeChecked = false;
                        selectIfCanBeChecked = pgConn.CreateCommand();
                        selectIfCanBeChecked.CommandText = @"SELECT nicht_zu_pruefen, nicht_pruefbar
                                        FROM ""gr_v_spielgeraete"" 
                                        WHERE fid=@fid";
                        selectIfCanBeChecked.Parameters.AddWithValue("fid", inspectionReport.playdeviceFid);

                        using (NpgsqlDataReader reader = selectIfCanBeChecked.ExecuteReader())
                        {
                            reader.Read();
                            canBeChecked = (reader.IsDBNull(0) || !reader.GetBoolean(0)) &&
                                    (reader.IsDBNull(1) || !reader.GetBoolean(1));
                        }

                        if (canBeChecked)
                        {
                            InspectionController.writeInspectionReport(inspectionTid, inspectionReport,
                                    userFromDb, pgConn, dryRun);
                        }

                    }

                    NpgsqlCommand commitTrans = pgConn.CreateCommand();
                    commitTrans.CommandText = "COMMIT TRANSACTION";
                    commitTrans.ExecuteNonQuery();

                    return Ok(result);
                }
                catch (Exception ex)
                {
                    NpgsqlCommand rollbackTrans = pgConn.CreateCommand();
                    rollbackTrans.CommandText = "ROLLBACK TRANSACTION";
                    rollbackTrans.ExecuteNonQuery();

                    _logger.LogError(ex.Message);

                    result.errorMessage = "SPK-3";
                    return Ok(result);  // Internal server error
                }

            }

        }

        private static InspectionReport readInspectionReport(NpgsqlDataReader reader)
        {
            InspectionReport inspectionReport = new InspectionReport();
            inspectionReport.inspectionType = reader.IsDBNull(0) ? "" : reader.GetString(0);
            if (!reader.IsDBNull(1))
            {
                NpgsqlDate dateOfInspection = reader.GetDate(1);
                inspectionReport.dateOfService = (DateTime)dateOfInspection;
            }
            inspectionReport.inspector = reader.IsDBNull(2) ? "" : reader.GetString(2);
            inspectionReport.inspectionText = reader.IsDBNull(3) ? "" : reader.GetString(3);
            if (reader.IsDBNull(4))
            {
                inspectionReport.inspectionDone = false;
            }
            else
            {
                int inspectionDoneInt = reader.GetInt32(4);
                if (inspectionDoneInt == 0)
                {
                    inspectionReport.inspectionDone = false;
                }
                else
                {
                    inspectionReport.inspectionDone = true;
                }
            }
            inspectionReport.inspectionComment = reader.IsDBNull(5) ? "" : reader.GetString(5);
            return inspectionReport;
        }

        private static int writeInspection(InspectionReport exampleInspectionReport,
                    User userFromDb, NpgsqlConnection pgConn, bool dryRun)
        {
            int inspectionTid = -1;
            int inspectorFid = -1;
            int playgroundFid = -1;
            int inspectionTypeId = -1;

            NpgsqlCommand selectInspectorFid;
            selectInspectorFid = pgConn.CreateCommand();
            selectInspectorFid.CommandText = "SELECT fid FROM \"wgr_sp_kontrolleur\" " +
                        "WHERE e_mail = @e_mail";
            selectInspectorFid.Parameters.AddWithValue("e_mail", userFromDb.mailAddress);

            using (NpgsqlDataReader reader = selectInspectorFid.ExecuteReader())
            {
                if (reader.Read())
                {
                    inspectorFid = reader.GetInt32(0);
                }
            }

            string inspectionType = exampleInspectionReport.inspectionType;
            if (exampleInspectionReport.inspectionType.Length > 4)
            {
                inspectionType = exampleInspectionReport.inspectionType
                        .Substring(0, exampleInspectionReport.inspectionType.Length - 5);
            }

            NpgsqlCommand selectInspectionTypeId;
            selectInspectionTypeId = pgConn.CreateCommand();
            selectInspectionTypeId.CommandText = "SELECT id FROM \"wgr_sp_inspektionsart_tbd\" " +
                        "WHERE value = @value";
            selectInspectionTypeId.Parameters.AddWithValue("value", inspectionType);

            using (NpgsqlDataReader reader = selectInspectionTypeId.ExecuteReader())
            {
                if (reader.Read())
                {
                    inspectionTypeId = reader.GetInt32(0);
                }
            }


            playgroundFid = InspectionController._GetPlaygroundFid(exampleInspectionReport, pgConn);

            if (dryRun) return -1;

            NpgsqlCommand insertInspectionCommand;
            insertInspectionCommand = pgConn.CreateCommand();
            insertInspectionCommand.CommandText = "INSERT INTO \"wgr_sp_inspektion\" " +
                    "(tid, id_inspektionsart, fid_spielplatz, datum_inspektion, fid_kontrolleur) " +
                    "VALUES (" +
                    "(SELECT CASE WHEN max(tid) IS NULL THEN 1 ELSE max(tid) + 1 END FROM \"wgr_sp_inspektion\"), " +
                    "@id_inspektionsart, @fid_spielplatz, @datum_inspektion, @fid_kontrolleur) RETURNING tid";
            insertInspectionCommand.Parameters.AddWithValue("id_inspektionsart",
                        inspectionTypeId != -1 ? inspectionTypeId : DBNull.Value);
            insertInspectionCommand.Parameters.AddWithValue("fid_spielplatz",
                        playgroundFid != -1 ? playgroundFid : DBNull.Value);
            NpgsqlDate dateOfService = (NpgsqlDate)exampleInspectionReport.playdeviceDateOfService;
            insertInspectionCommand.Parameters.AddWithValue("datum_inspektion", dateOfService);
            insertInspectionCommand.Parameters.AddWithValue("fid_kontrolleur",
                        inspectorFid != -1 ? inspectorFid : DBNull.Value);
            inspectionTid = (int)insertInspectionCommand.ExecuteScalar();

            return inspectionTid;
        }

        private static void writeInspectionReport(int inspectionTid, InspectionReport inspectionReport,
                    User userFromDb, NpgsqlConnection pgConn, bool dryRun)
        {
            if (dryRun) return;

            NpgsqlCommand insertInspectionReportCommand;
            insertInspectionReportCommand = pgConn.CreateCommand();
            insertInspectionReportCommand.CommandText = "INSERT INTO \"wgr_sp_insp_bericht\" " +
                    "(tid, tid_inspektion, fid_spielgeraet, fid_geraet_detail, inspektionsart, datum_inspektion, kontrolleur, pruefung_text, " +
                    "pruefung_erledigt, pruefung_kommentar, wartung_text, wartung_erledigung, " +
                    "wartung_kommentar, fallschutz) " +
                    "VALUES (" +
                    "(SELECT CASE WHEN max(tid) IS NULL THEN 1 ELSE max(tid) + 1 END FROM \"wgr_sp_insp_bericht\"), " +
                    "@tid_inspektion, @fid_spielgeraet, @fid_geraet_detail, @inspektionsart, @datum_inspektion, @kontrolleur, @pruefung_text, " +
                    "@pruefung_erledigt, @pruefung_kommentar, @wartung_text, @wartung_erledigung, " +
                    "@wartung_kommentar, @fallschutz) RETURNING tid";
            insertInspectionReportCommand.Parameters.AddWithValue("tid_inspektion",
                inspectionTid != -1 ? inspectionTid : DBNull.Value);
            insertInspectionReportCommand.Parameters.AddWithValue("fid_spielgeraet",
                            inspectionReport.playdeviceFid != 0 ? inspectionReport.playdeviceFid : DBNull.Value);
            insertInspectionReportCommand.Parameters.AddWithValue("fid_geraet_detail",
                            inspectionReport.playdeviceDetailFid != 0 ? inspectionReport.playdeviceDetailFid : DBNull.Value);
            insertInspectionReportCommand.Parameters.AddWithValue("inspektionsart", inspectionReport.inspectionType);
            NpgsqlDate dateOfService = (NpgsqlDate)inspectionReport.playdeviceDateOfService;
            insertInspectionReportCommand.Parameters.AddWithValue("datum_inspektion", dateOfService);
            insertInspectionReportCommand.Parameters.AddWithValue("kontrolleur", userFromDb.firstName + " " + userFromDb.lastName);
            if (inspectionReport.inspectionText == null) inspectionReport.inspectionText = "";
            insertInspectionReportCommand.Parameters.AddWithValue("pruefung_text", inspectionReport.inspectionText);
            int inspectionDone = inspectionReport.inspectionDone ? 1 : 0;
            insertInspectionReportCommand.Parameters.AddWithValue("pruefung_erledigt", inspectionDone);
            if (inspectionReport.inspectionComment == null || inspectionReport.inspectionComment.Trim().Length == 0)
            {
                inspectionReport.inspectionComment = "";
            }
            insertInspectionReportCommand.Parameters.AddWithValue("pruefung_kommentar", inspectionReport.inspectionComment);
            if (inspectionReport.maintenanceText == null) inspectionReport.maintenanceText = "";
            insertInspectionReportCommand.Parameters.AddWithValue("wartung_text", inspectionReport.maintenanceText);
            int maintenanceDone = inspectionReport.maintenanceDone ? 1 : 0;
            insertInspectionReportCommand.Parameters.AddWithValue("wartung_erledigung", maintenanceDone);
            if (inspectionReport.maintenanceComment == null || inspectionReport.maintenanceComment.Trim().Length == 0)
            {
                inspectionReport.maintenanceComment = "";
            }
            insertInspectionReportCommand.Parameters.AddWithValue("wartung_kommentar", inspectionReport.maintenanceComment);
            insertInspectionReportCommand.Parameters.AddWithValue("fallschutz", inspectionReport.fallProtectionType);

            int inspectionReportTid = (int)insertInspectionReportCommand.ExecuteScalar();


            DefectController.writeAllDefects(inspectionReport.defects,
                                    inspectionReportTid, userFromDb, pgConn, dryRun);
        }

        private static int _GetPlaygroundFid(InspectionReport exampleInspectionReport, NpgsqlConnection pgConn)
        {
            int result = -1;

            int playdeviceFid = exampleInspectionReport.playdeviceFid;

            if (exampleInspectionReport.playdeviceFid == 0 &&
                        exampleInspectionReport.playdeviceDetailFid != 0)
            {
                NpgsqlCommand selectPlaydeviceFid;
                selectPlaydeviceFid = pgConn.CreateCommand();
                selectPlaydeviceFid.CommandText = "SELECT fid_spielgeraet FROM \"wgr_sp_geraetedetail\" " +
                            "WHERE fid = @fid_spielgeraetedetail";
                selectPlaydeviceFid.Parameters
                        .AddWithValue("fid_spielgeraetedetail", exampleInspectionReport.playdeviceDetailFid);

                using (NpgsqlDataReader reader = selectPlaydeviceFid.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        playdeviceFid = reader.GetInt32(0);
                    }
                }

            }

            if (playdeviceFid != 0)
            {
                NpgsqlCommand selectPlaygoundFid;
                selectPlaygoundFid = pgConn.CreateCommand();
                selectPlaygoundFid.CommandText = "SELECT fid_spielplatz FROM \"gr_v_spielgeraete\" " +
                            "WHERE fid = @fid_spielgeraet";
                selectPlaygoundFid.Parameters.AddWithValue("fid_spielgeraet", playdeviceFid);

                using (NpgsqlDataReader reader = selectPlaygoundFid.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        result = reader.GetInt32(0);
                    }
                }
            }
            return result;
        }


    }

}

