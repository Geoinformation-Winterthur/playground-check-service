// <copyright company="Vermessungsamt Winterthur">
//      Author: Edgar Butwilowski
//      Copyright (c) Vermessungsamt Winterthur. All rights reserved.
// </copyright>
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using NpgsqlTypes;

using playground_check_service.Model;
using System.Text;
using playground_check_service.Configuration;
using NetTopologySuite.Geometries;

namespace playground_check_service.Controllers
{
    /// <summary>
    /// This is the controller for playground data. Playground data is available
    /// at the /playground route.
    /// </summary>
    /// <remarks>
    /// This controller provides a list of the names of all playgrounds in the database.
    /// It also provides single playground objects by id and by name. It is possible to
    /// post a single playground object to this controller. Therefore, the route of this
    /// controller provides read and write access.
    /// </remarks>
    [ApiController]
    [Route("[controller]")]
    public class PlaygroundController : ControllerBase
    {
        private readonly ILogger<PlaygroundController> _logger;

        public PlaygroundController(ILogger<PlaygroundController> logger)
        {
            _logger = logger;
        }

        // GET /collections/playgrounds/items/
        /// <summary>
        /// Retrieves a collection of all public playgrounds of the City
        /// of Winterthur that are operated by the Municipal Green Office.
        /// </summary>
        /// <response code="200">
        /// The data is returned in an array of feature objects.
        /// </response>
        [Route("/Collections/Playgrounds/Items/")]
        [HttpGet]
        [ProducesResponseType(typeof(PlaygroundFeature[]), 200)]
        public async Task<PlaygroundFeature[]> GetFeaturesInCollection()
        {
            List<PlaygroundFeature> result = new List<PlaygroundFeature>();

            try
            {
                using (NpgsqlConnection pgConn = new NpgsqlConnection(AppConfig.connectionString))
                {
                    await pgConn.OpenAsync();
                    NpgsqlCommand selectComm = pgConn.CreateCommand();
                    selectComm.CommandText = "SELECT uuid, nummer, name, strassenname, hausnummer, geom FROM \"wgr_sp_spielplatz\"";

                    using (NpgsqlDataReader reader = await selectComm.ExecuteReaderAsync())
                    {
                        PlaygroundFeature currentPlayground;
                        while (await reader.ReadAsync())
                        {
                            currentPlayground = new PlaygroundFeature();
                            currentPlayground.properties.uuid = reader.IsDBNull(0) ? "" : reader.GetString(0);
                            currentPlayground.properties.nummer = reader.IsDBNull(1) ? -1 : reader.GetInt32(1);
                            currentPlayground.properties.name = reader.IsDBNull(2) ? "" : reader.GetString(2);
                            currentPlayground.properties.streetName = reader.IsDBNull(3) ? "" : reader.GetString(3);
                            currentPlayground.properties.houseNo = reader.IsDBNull(4) ? "" : reader.GetString(4);

                            Point ntsPoint = reader.IsDBNull(5) ? Point.Empty : reader.GetValue(5) as Point;
                            currentPlayground.geometry = new PlaygroundFeaturePoint(ntsPoint);
                            result.Add(currentPlayground);
                        }
                        return result.ToArray();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex.Message);
                PlaygroundFeature errObj = new PlaygroundFeature();
                errObj.errorMessage = "Unknown critical error.";
                return new PlaygroundFeature[] { errObj };
            }
        }

        // GET /collections/playgrounds/items/638364
        /// <summary>
        /// Retrieves the public playground of the City of Winterthur
        /// that is operated by the Municipal Green Office for the
        /// given UUID.
        /// </summary>
        /// <response code="200">
        /// The data is returned as a feature objects.
        /// </response>
        [Route("/Collections/Playgrounds/Items/{uuid}")]
        [HttpGet]
        [ProducesResponseType(typeof(PlaygroundFeature), 200)]
        public async Task<PlaygroundFeature> GetPlaygroundAsFeature(string uuid)
        {
            if (uuid == null)
            {
                uuid = "";
            }
            else
            {
                uuid = uuid.Trim().ToLower();
            }

            PlaygroundFeature result = new PlaygroundFeature();
            if (uuid == "")
            {
                _logger.LogInformation("No valid UUID provided by the user in public GET playground as feature operation");
                result.errorMessage = "No valid UUID provided.";
                return result;
            }
            result.properties.uuid = uuid;

            try
            {
                using (NpgsqlConnection pgConn = new NpgsqlConnection(AppConfig.connectionString))
                {
                    await pgConn.OpenAsync();
                    NpgsqlCommand selectComm = pgConn.CreateCommand();
                    selectComm.CommandText = "SELECT nummer, name, strassenname, hausnummer, geom FROM \"wgr_sp_spielplatz\" WHERE uuid=@uuid";
                    selectComm.Parameters.AddWithValue("uuid", uuid);

                    using (NpgsqlDataReader reader = await selectComm.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            result.properties.nummer = reader.IsDBNull(0) ? -1 : reader.GetInt32(0);
                            result.properties.name = reader.IsDBNull(1) ? "" : reader.GetString(1);
                            result.properties.streetName = reader.IsDBNull(2) ? "" : reader.GetString(2);
                            result.properties.houseNo = reader.IsDBNull(3) ? "" : reader.GetString(3);

                            Point ntsPoint = reader.IsDBNull(4) ? Point.Empty : reader.GetValue(4) as Point;
                            result.geometry = new PlaygroundFeaturePoint(ntsPoint);
                            return result;
                        }
                        else
                        {
                            result.errorMessage = "No playground found for given UUID.";
                            return result;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex.Message);
                result.errorMessage = "Unknown critical error.";
                return result;
            }
        }

        // GET Playground/8262517&inspectiontype=...&withdefects=true&withinspections=true
        [Route("/Playground/{id}")]
        [HttpGet]
        [Authorize]
        public Playground GetById(int id, string inspectionType,
                    bool withdefects = false, bool withinspections = false)
        {
            return this.readPlaygroundFromDb(id, null, inspectionType, withdefects, withinspections);
        }

        // GET Playground/byname?name=...&inspectiontype=Hauptinspektion (HI)
        [Route("/Playground/byname")]
        [HttpGet]
        [Authorize]
        public Playground GetByName(string name, string inspectionType,
                bool withdefects = false, bool withinspections = false)
        {
            return this.readPlaygroundFromDb(-1, name, inspectionType, withdefects, withinspections);
        }

        // GET playground/onlynames?inspectiontype=Hauptinspektion (HI)
        [Route("/Playground/onlynames")]
        [HttpGet]
        [Authorize]
        public IEnumerable<Playground> GetOnlyNames(string inspectionType)
        {
            string userMailAddress = null;
            foreach (Claim claim in this.User.Claims)
            {
                if (claim.Type == ClaimTypes.Email)
                {
                    userMailAddress = claim.Value;
                    break;
                }
                ;
            }
            List<Playground> resultTemp = new List<Playground>();

            using (NpgsqlConnection pgConn = new NpgsqlConnection(AppConfig.connectionString))
            {
                pgConn.Open();
                NpgsqlCommand selectComm = pgConn.CreateCommand();
                selectComm.CommandText = "SELECT DISTINCT ON (sp.name) " +
                        "sp.name, insp.datum_inspektion, sp.inspektion_aussetzen_von, " +
                        "sp.inspektion_aussetzen_bis, " +
                        "(SELECT count(*) > 0 " +
                        "FROM \"wgr_sp_insp_mangel\" mangel " +
                        "JOIN \"gr_v_spielgeraete\" geraete ON mangel.fid_spielgeraet = geraete.fid " +
                        "WHERE geraete.fid_spielplatz = sp.fid " +
                        "AND mangel.fid_erledigung IS NULL) AS geraet_hat_mangel " +
                        "FROM \"wgr_sp_spielplatz\" sp " +
                        "LEFT JOIN \"wgr_sp_inspektion\" insp " +
                        "ON insp.fid_spielplatz = sp.fid " +
                        "ORDER BY sp.name, insp.datum_inspektion DESC";

                if (userMailAddress != null && inspectionType != null &&
                        !inspectionType.Equals("Keine Inspektion"))
                {
                    selectComm.CommandText = "SELECT DISTINCT ON (sp.name) " +
                        "sp.name, insp.datum_inspektion, sp.inspektion_aussetzen_von, " +
                        "sp.inspektion_aussetzen_bis, false " +
                        "FROM \"wgr_sp_spielplatz\" sp " +
                        "JOIN \"wgr_sp_inspart_kontr\" ikt ON sp.fid = ikt.fid_spielplatz " +
                        "JOIN \"wgr_sp_kontrolleur\" kt ON kt.fid = ikt.fid_kontrolleur " +
                        "JOIN \"wgr_sp_inspektionsart_tbd\" ina ON ina.id = ikt.id_inspektionsart " +
                        "LEFT JOIN \"wgr_sp_inspektion\" insp ON insp.fid_spielplatz = sp.fid " +
                        "WHERE kt.e_mail=@e_mail " +
                        "AND ina.value=@inspektionsart " +
                        "ORDER BY sp.name, insp.datum_inspektion DESC";

                    selectComm.Parameters.AddWithValue("e_mail", userMailAddress);
                    inspectionType = inspectionType.Substring(0, inspectionType.Length - 5);
                    selectComm.Parameters.AddWithValue("inspektionsart", inspectionType);
                }

                using (NpgsqlDataReader reader = selectComm.ExecuteReader())
                {
                    Playground resultPlayground;
                    while (reader.Read())
                    {
                        resultPlayground = new Playground();
                        resultPlayground.name = reader.GetString(0);
                        if (!reader.IsDBNull(1))
                        {
                            NpgsqlDate dateOfLastInspection = reader.GetDate(1);
                            resultPlayground.dateOfLastInspection = (DateTime)dateOfLastInspection;
                        }
                        if (!reader.IsDBNull(2))
                        {
                            NpgsqlDate suspendInspectionFrom = reader.GetDate(2);
                            resultPlayground.suspendInspectionFrom = (DateTime)suspendInspectionFrom;
                        }
                        if (!reader.IsDBNull(3))
                        {
                            NpgsqlDate suspendInspectionTo = reader.GetDate(3);
                            resultPlayground.suspendInspectionTo = (DateTime)suspendInspectionTo;
                        }
                        resultPlayground.hasOpenDeviceDefects = reader.GetBoolean(4);

                        _CalculateValueIsInspectionSuspended(resultPlayground);

                        if (inspectionType.Equals("Keine Inspektion"))
                        {
                            resultTemp.Add(resultPlayground);
                        }
                        else if (!resultPlayground.inspectionSuspended)
                        {
                            resultTemp.Add(resultPlayground);
                        }

                    }
                }
                pgConn.Close();
            }

            List<Playground> result = new List<Playground>();
            bool exchanged;
            foreach (Playground playgroundTemp in resultTemp)
            {
                exchanged = false;
                for (int i = 0; i < result.Count; i++)
                {
                    Playground playground = result[i];
                    if (playground.name == playgroundTemp.name)
                        if (playground.dateOfLastInspection != null
                            && playgroundTemp.dateOfLastInspection != null)
                        {
                            result[i] = playground.dateOfLastInspection > playgroundTemp.dateOfLastInspection ?
                                         playground : playgroundTemp;
                            exchanged = true;
                        }
                        else if (playground.dateOfLastInspection == null
                          && playgroundTemp.dateOfLastInspection != null)
                        {
                            result[i] = playgroundTemp;
                            exchanged = true;
                        }
                }
                if (!exchanged)
                {
                    result.Add(playgroundTemp);
                }
            }

            return result;
        }

        // GET playground/mapimage?x=...&y=...
        [Route("/Playground/mapimage")]
        [HttpGet]
        public async Task<string> GetMapImage(double x, double y)
        {
            if (x != 0 && y != 0)
            {
                HttpClient http = new HttpClient();
                string requestUrl = "http://" + AppConfig.wmsUrl + "Spielplatzkarte?" +
                "LAYERS=AV_UEP_Landeskarten,Spielplatz&VERSION=1.1.1&DPI=96&TRANSPARENT=TRUE&FORMAT=image%2Fpng&" +
                "SERVICE=WMS&REQUEST=GetMap&STYLES=&SRS=EPSG%3A2056&BBOX=" + (x - 10) + "," + (y - 5) + "," + (x + 10) + "," +
                (y + 5) + "&WIDTH=800&HEIGHT=400";
                HttpResponseMessage resp =
                    await http.GetAsync(requestUrl);

                byte[] imageBytes = await resp.Content.ReadAsByteArrayAsync();
                string imageBase64 = Convert.ToBase64String(imageBytes);

                return imageBase64;
            }
            else
            {
                return "";
            }

        }

        private Playground readPlaygroundFromDb(int id, string name,
                string inspectionType, bool withdefects = false, bool withinspections = false)
        {
            Playground currentPlayground = null;

            using (NpgsqlConnection pgConn = new NpgsqlConnection(AppConfig.connectionString))
            {
                pgConn.Open();
                NpgsqlCommand selectComm = pgConn.CreateCommand();
                if (name != null)
                {
                    selectComm.CommandText = "SELECT fid, name, inspektion_aussetzen_von, inspektion_aussetzen_bis " +
                        "FROM \"wgr_sp_spielplatz\" WHERE name=@name";
                    selectComm.Parameters.AddWithValue("name", name);
                }
                else
                {
                    selectComm.CommandText = "SELECT fid, name, inspektion_aussetzen_von, inspektion_aussetzen_bis " +
                        "FROM \"wgr_sp_spielplatz\" WHERE fid=@id";
                    selectComm.Parameters.AddWithValue("id", id);
                }

                using (NpgsqlDataReader reader = selectComm.ExecuteReader())
                {
                    currentPlayground = new Playground();
                    reader.Read();
                    currentPlayground.Id = reader.GetInt32(0);
                    currentPlayground.name = reader.IsDBNull(1) ? "" : reader.GetString(1);
                    if (!reader.IsDBNull(2))
                    {
                        NpgsqlDate suspendInspectionFrom = reader.GetDate(2);
                        currentPlayground.suspendInspectionFrom = (DateTime)suspendInspectionFrom;
                    }
                    if (!reader.IsDBNull(3))
                    {
                        NpgsqlDate suspendInspectionTo = reader.GetDate(3);
                        currentPlayground.suspendInspectionTo = (DateTime)suspendInspectionTo;
                    }

                    _CalculateValueIsInspectionSuspended(currentPlayground);

                }
                pgConn.Close();
            }

            if (currentPlayground != null)
            {

                DefectDAO defectDao = new DefectDAO();
                List<string> defectPriorityOptions = defectDao.GetDefectPriorityOptions();
                currentPlayground.defectPriorityOptions = defectPriorityOptions.ToArray();

                currentPlayground.inspectionTypeOptions = InspectionTypesController._GetTypes();

                currentPlayground.renovationTypeOptions = _GetRenovationTypes();

                currentPlayground.defectsResponsibleBodyOptions = _GetDefectsResponsibleBodyTypes();

                currentPlayground.documentsOfAcceptanceFids = _GetAcceptanceDocumentsFids(currentPlayground.Id);
                currentPlayground.certificateDocumentsFids = _GetCertificateDocumentsFids(currentPlayground.Id);

                currentPlayground.playdevices = this._ReadPlaydevicesOfPlayground(currentPlayground.Id);

                if (currentPlayground.playdevices != null)
                {
                    if (withinspections)
                    {
                        this.readInspectionCriteriaOfPlaydevices(currentPlayground.playdevices, inspectionType);
                        string[] inspectionTypes = InspectionTypesController._GetTypes();

                        this.readReportsOfPlaydevices(currentPlayground.playdevices, inspectionTypes);
                    }
                    if (withdefects)
                        this.readDefectsOfPlaydevices(currentPlayground.playdevices);
                }

            }

            return currentPlayground;
        }

        private PlaydeviceFeature[] _ReadPlaydevicesOfPlayground(int playGroundId)
        {
            PlaydeviceFeature[] result = null;
            using (NpgsqlConnection pgConn = new NpgsqlConnection(AppConfig.connectionString))
            {
                pgConn.Open();
                pgConn.TypeMapper.UseNetTopologySuite();
                NpgsqlCommand selectComm = pgConn.CreateCommand();
                selectComm.CommandText = "SELECT spg.fid, spg.bemerkungen, spg.geom, " +
                        "gart.short_value, gart.value, spg.norm, lief.name, " +
                        "spg.empfohlenes_sanierungsjahr, spg.bemerkung_empf_sanierung, " +
                        "spg.nicht_zu_pruefen, spg.nicht_pruefbar, spg.grund_nicht_pruefbar, " +
                        "spg.bau_dat, spg.id_sanierungsart " +
                        "FROM \"gr_v_spielgeraete\" spg " +
                        "LEFT JOIN \"wgr_sp_spielgeraeteart_tbd\" gart ON spg.id_geraeteart = gart.id " +
                        "LEFT JOIN \"wgr_sp_lieferant\" lief ON spg.id_lieferant = lief.fid " +
                        "WHERE spg.fid_spielplatz=" + playGroundId;

                using (NpgsqlDataReader reader = selectComm.ExecuteReader())
                {
                    PlaydeviceFeature currentPlaydevice;
                    PlaydeviceFeatureProperties.Type currPlaydeviceType;
                    List<PlaydeviceFeature> currentPlaydevices = new List<PlaydeviceFeature>();
                    while (reader.Read())
                    {
                        currentPlaydevice = new PlaydeviceFeature();
                        currentPlaydevice.properties.fid = reader.GetInt32(0);
                        currentPlaydevice.properties.comment = reader.IsDBNull(1) ? "" : reader.GetString(1);

                        Point pointFromDb = reader[2] as Point;
                        Model.Geometry geometry
                                    = new Model.Geometry(
                                        Model.Geometry.Type.Point,
                                        new double[] { pointFromDb.Coordinate.X, pointFromDb.Coordinate.Y });
                        currentPlaydevice.geometry = geometry;

                        currPlaydeviceType = new PlaydeviceFeatureProperties.Type();
                        currPlaydeviceType.name = reader.IsDBNull(3) ? "" : reader.GetString(3);
                        currPlaydeviceType.description = reader.IsDBNull(4) ? "" : reader.GetString(4);
                        currPlaydeviceType.standard = reader.IsDBNull(5) ? "" : reader.GetString(5);
                        currentPlaydevice.properties.type = currPlaydeviceType;

                        currentPlaydevice.properties.supplier = reader.IsDBNull(6) ? "" : reader.GetString(6);
                        if (!reader.IsDBNull(7)) currentPlaydevice.properties.recommendedYearOfRenovation = reader.GetInt32(7);
                        currentPlaydevice.properties.commentRecommendedYearOfRenovation = reader.IsDBNull(8) ? "" : reader.GetString(8);

                        currentPlaydevice.properties.notToBeChecked = reader.IsDBNull(9) ? false : reader.GetBoolean(9);
                        currentPlaydevice.properties.cannotBeChecked = reader.IsDBNull(10) ? false : reader.GetBoolean(10);
                        currentPlaydevice.properties.cannotBeCheckedReason = reader.IsDBNull(11) ? "" : reader.GetString(11);

                        if (!reader.IsDBNull(12))
                        {
                            NpgsqlDate constructionDate = reader.GetDate(12);
                            currentPlaydevice.properties.constructionDate = (DateTime)constructionDate;
                        }

                        if (!reader.IsDBNull(13))
                            currentPlaydevice.properties.renovationType = reader.GetInt32(13);

                        currentPlaydevices.Add(currentPlaydevice);
                    }
                    result = currentPlaydevices.ToArray();
                }
                pgConn.Close();
            }
            return result;
        }

        private void readDefectsOfPlaydevices(PlaydeviceFeature[] playdevices)
        {
            using NpgsqlConnection pgConn = new NpgsqlConnection(AppConfig.connectionString);
            pgConn.Open();
            foreach (PlaydeviceFeature playdevice in playdevices)
            {
                DefectDAO defectDao = new();
                playdevice.properties.defects = defectDao.ReadAllOfPlaydevice(playdevice.properties.fid);
            }
        }


        private void readReportsOfPlaydevices(PlaydeviceFeature[] playdevices, string[] inspectionTypes)
        {
            using NpgsqlConnection pgConn = new NpgsqlConnection(AppConfig.connectionString);
            pgConn.Open();
            foreach (PlaydeviceFeature playdevice in playdevices)
            {
                List<InspectionReport> lastInspectionReports = new List<InspectionReport>();
                List<InspectionReport> nextToLastInspectionReports = new List<InspectionReport>();

                foreach (string inspectionType in inspectionTypes)
                {
                    NpgsqlCommand selectInspectionComm = pgConn.CreateCommand();
                    selectInspectionComm.CommandText = $@"SELECT tid, inspektionsart,
                            datum_inspektion, kontrolleur, pruefung_text,
                            pruefung_erledigt, pruefung_kommentar,
                            wartung_text, wartung_erledigung,
                            wartung_kommentar, fallschutz, tid_inspektion
                            FROM ""wgr_sp_insp_bericht""
                            WHERE tid_inspektion IN (
                                SELECT tid_inspektion
                                FROM ""wgr_sp_insp_bericht""
                                WHERE fid_spielgeraet = {playdevice.properties.fid}
                                  AND inspektionsart = '{inspectionType}'
                                GROUP BY tid_inspektion, datum_inspektion
                                ORDER BY datum_inspektion DESC
                                LIMIT 2
                            )
                            AND fid_spielgeraet = {playdevice.properties.fid}
                            AND inspektionsart = '{inspectionType}'
                            ORDER BY datum_inspektion, tid_inspektion DESC;";

                    using (NpgsqlDataReader reader = selectInspectionComm.ExecuteReader())
                    {
                        int currInspectionTid = -1;
                        if(reader.Read()){
                            InspectionReport inspectionReport = readInspectionReport(reader);
                            lastInspectionReports.Add(inspectionReport);
                            currInspectionTid = inspectionReport.tidInspection;
                        }
                        while (reader.Read() && currInspectionTid != -1)
                        {
                            InspectionReport inspectionReport = readInspectionReport(reader);
                            if (inspectionReport.tidInspection == currInspectionTid)
                                lastInspectionReports.Add(inspectionReport);
                            else
                                nextToLastInspectionReports.Add(inspectionReport);
                        }
                    }
                }
                playdevice.properties.lastInspectionReports = lastInspectionReports.ToArray();
                playdevice.properties.nextToLastInspectionReports = nextToLastInspectionReports.ToArray();
            }
        }

        private void readInspectionCriteriaOfPlaydevices(PlaydeviceFeature[] playdevices, string inspectionType)
        {
            if (inspectionType != null && inspectionType.Length > 5)
            {
                inspectionType = inspectionType.Substring(0, inspectionType.Length - 5);
            }

            using (NpgsqlConnection pgConn = new NpgsqlConnection(AppConfig.connectionString))
            {
                pgConn.Open();
                NpgsqlCommand selectComm = pgConn.CreateCommand();

                foreach (PlaydeviceFeature currentPlaydevice in playdevices)
                {
                    selectComm.CommandText = "SELECT bereich, pruefung, wartung, " +
                            "inspektionsart, pruefung_kurztext " +
                            "FROM \"wgr_v_sp_ger_insp_krit\" " +
                            "WHERE fid_spielgeraet=" + currentPlaydevice.properties.fid +
                            " AND inspektionsart=@inspektionsart";
                    selectComm.Parameters.AddWithValue("inspektionsart", inspectionType);
                    using (NpgsqlDataReader reader = selectComm.ExecuteReader())
                    {
                        currentPlaydevice.properties.generalInspectionCriteria = this.readInspectionCriteriaOfPlaydevice(reader);
                    }
                    selectComm.CommandText = "SELECT bereich, pruefung, wartung, " +
                            "insektionsart, pruefung_kurztext " +
                            "FROM \"wgr_v_sp_hfall_insp_krit\" " +
                            "WHERE fid_spielgeraet=" + currentPlaydevice.properties.fid +
                            " AND insektionsart=@insektionsart";
                    selectComm.Parameters.AddWithValue("insektionsart", inspectionType);
                    using (NpgsqlDataReader reader = selectComm.ExecuteReader())
                    {
                        currentPlaydevice.properties.mainFallProtectionInspectionCriteria
                                    = this.readInspectionCriteriaOfPlaydevice(reader);
                    }
                    selectComm.CommandText = "SELECT bereich, pruefung, wartung, " +
                            "insektionsart, pruefung_kurztext " +
                            "FROM \"wgr_v_sp_nfall_insp_krit\" " +
                            "WHERE fid_spielgeraet=" + currentPlaydevice.properties.fid +
                            " AND insektionsart=@insektionsart";
                    selectComm.Parameters.AddWithValue("insektionsart", inspectionType);
                    using (NpgsqlDataReader reader = selectComm.ExecuteReader())
                    {
                        currentPlaydevice.properties.secondaryFallProtectionInspectionCriteria
                                    = this.readInspectionCriteriaOfPlaydevice(reader);
                    }
                }
                pgConn.Close();
            }
        }

        private InspectionCriterion[] readInspectionCriteriaOfPlaydevice(NpgsqlDataReader reader)
        {
            InspectionCriterionDAO inspectionCriterionDAO = new InspectionCriterionDAO();
            return inspectionCriterionDAO.Read(reader).ToArray();
        }

        private InspectionReport readInspectionReport(NpgsqlDataReader reader)
        {
            InspectionReport inspectionReport = new InspectionReport();
            inspectionReport.tid = reader.IsDBNull(0) ? -1 : reader.GetInt32(0);
            inspectionReport.inspectionType = reader.IsDBNull(1) ? "" : reader.GetString(1);
            if (!reader.IsDBNull(2))
            {
                NpgsqlDate dateOfInspection = reader.GetDate(2);
                inspectionReport.dateOfService = (DateTime)dateOfInspection;
            }
            inspectionReport.inspector = reader.IsDBNull(3) ? "" : reader.GetString(3);
            inspectionReport.inspectionText = reader.IsDBNull(4) ? "" : reader.GetString(4);
            inspectionReport.inspectionDone = reader.IsDBNull(5) || reader.GetInt32(5) == 0 ? false : true;
            inspectionReport.inspectionComment = reader.IsDBNull(6) ? "" : reader.GetString(6);
            inspectionReport.maintenanceText = reader.IsDBNull(7) ? "" : reader.GetString(7);
            inspectionReport.maintenanceDone = reader.IsDBNull(8) || reader.GetInt32(8) == 0 ? false : true;
            inspectionReport.maintenanceComment = reader.IsDBNull(9) ? "" : reader.GetString(9);
            inspectionReport.fallProtectionType = reader.IsDBNull(10) ? "" : reader.GetString(10);
            inspectionReport.tidInspection = reader.IsDBNull(11) ? -1 : reader.GetInt32(11);
            return inspectionReport;
        }

        internal static Enumeration[] _GetRenovationTypes()
        {
            List<Enumeration> result = new();

            using (NpgsqlConnection pgConn = new NpgsqlConnection(AppConfig.connectionString))
            {
                pgConn.Open();
                NpgsqlCommand selectComm = pgConn.CreateCommand();
                selectComm.CommandText = "SELECT id, value " +
                            "FROM \"wgr_sp_sanierungsart_tbd\"";

                using (NpgsqlDataReader reader = selectComm.ExecuteReader())
                {
                    Enumeration renovationEnum = new();
                    while (reader.Read())
                    {
                        renovationEnum.Id = reader.GetInt32(0);
                        renovationEnum.Value = reader.GetString(1);
                        result.Add(renovationEnum);
                    }
                }
                pgConn.Close();
            }
            return result.ToArray();
        }

        internal static Enumeration[] _GetDefectsResponsibleBodyTypes()
        {
            List<Enumeration> result = new();

            using (NpgsqlConnection pgConn = new NpgsqlConnection(AppConfig.connectionString))
            {
                pgConn.Open();
                NpgsqlCommand selectComm = pgConn.CreateCommand();
                selectComm.CommandText = "SELECT id, value " +
                            "FROM \"wgr_sp_zust_mangelbeheb_tbd\"";

                using (NpgsqlDataReader reader = selectComm.ExecuteReader())
                {
                    Enumeration defectsResponsibleBodyEnum;
                    while (reader.Read())
                    {
                        defectsResponsibleBodyEnum = new()
                        {
                            Id = reader.GetInt32(0),
                            Value = reader.GetString(1)
                        };
                        result.Add(defectsResponsibleBodyEnum);
                    }
                }
                pgConn.Close();
            }
            return result.ToArray();
        }

        internal static int[] _GetAcceptanceDocumentsFids(int spielplatzFid)
        {
            List<int> result = new();

            using (NpgsqlConnection pgConn = new NpgsqlConnection(AppConfig.connectionString))
            {
                pgConn.Open();
                NpgsqlCommand selectComm = pgConn.CreateCommand();
                selectComm.CommandText = "SELECT fid " +
                            "FROM \"wgr_sp_abnahmen\" " +
                            "WHERE fid_spielplatz=@fid_spielplatz";
                selectComm.Parameters.AddWithValue("fid_spielplatz", spielplatzFid);

                using (NpgsqlDataReader reader = selectComm.ExecuteReader())
                {
                    int acceptanceDocumentFid;
                    while (reader.Read())
                    {
                        acceptanceDocumentFid = reader.GetInt32(0);
                        result.Add(acceptanceDocumentFid);
                    }
                }
                pgConn.Close();
            }
            return result.ToArray();
        }

        internal static int[] _GetCertificateDocumentsFids(int spielplatzFid)
        {
            List<int> result = new();

            using (NpgsqlConnection pgConn = new NpgsqlConnection(AppConfig.connectionString))
            {
                pgConn.Open();
                NpgsqlCommand selectComm = pgConn.CreateCommand();
                selectComm.CommandText = "SELECT fid " +
                            "FROM \"wgr_sp_zertifikat\" " +
                            "WHERE fid_spielplatz=@fid_spielplatz";
                selectComm.Parameters.AddWithValue("fid_spielplatz", spielplatzFid);

                using (NpgsqlDataReader reader = selectComm.ExecuteReader())
                {
                    int certificateDocumentFid;
                    while (reader.Read())
                    {
                        certificateDocumentFid = reader.GetInt32(0);
                        result.Add(certificateDocumentFid);
                    }
                }
                pgConn.Close();
            }
            return result.ToArray();
        }

        private static void _CalculateValueIsInspectionSuspended(Playground playground)
        {
            DateTime today = DateTime.Now.Date;
            playground.inspectionSuspended = true;
            if (playground.suspendInspectionFrom == null && playground.suspendInspectionTo == null)
            {
                playground.inspectionSuspended = false;
            }
            else if (playground.suspendInspectionFrom == null)
            {
                if (today > playground.suspendInspectionTo)
                {
                    playground.inspectionSuspended = false;
                }
            }
            else if (playground.suspendInspectionTo == null)
            {
                if (today < playground.suspendInspectionFrom)
                {
                    playground.inspectionSuspended = false;
                }
            }
            else
            {
                if (today < playground.suspendInspectionFrom || today > playground.suspendInspectionTo)
                {
                    playground.inspectionSuspended = false;
                }
            }
        }

    }

}

