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
                    selectComm.CommandText = "SELECT fid, nummer, name, geom FROM \"wgr_sp_spielplatz\"";

                    using (NpgsqlDataReader reader = await selectComm.ExecuteReaderAsync())
                    {
                        PlaygroundFeature currentPlayground;
                        while (await reader.ReadAsync())
                        {
                            currentPlayground = new PlaygroundFeature();
                            currentPlayground.properties.fid = reader.IsDBNull(0) ? -1 : reader.GetInt32(0);
                            currentPlayground.properties.nummer = reader.IsDBNull(1) ? -1 : reader.GetInt32(1);
                            currentPlayground.properties.name = reader.IsDBNull(2) ? "" : reader.GetString(2);

                            Point ntsPoint = reader.IsDBNull(3) ? Point.Empty : reader.GetValue(3) as Point;
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
                return new PlaygroundFeature[] {errObj};
            }
        }

        // GET /collections/playgrounds/items/638364
        /// <summary>
        /// Retrieves the public playground of the City of Winterthur
        /// that is operated by the Municipal Green Office for the
        /// given FID.
        /// </summary>
        /// <response code="200">
        /// The data is returned as a feature objects.
        /// </response>
        [Route("/Collections/Playgrounds/Items/{fid}")]
        [HttpGet]
        [ProducesResponseType(typeof(PlaygroundFeature), 200)]
        public async Task<PlaygroundFeature> GetPlaygroundAsFeature(int fid)
        {
            PlaygroundFeature result = new PlaygroundFeature();
            result.properties.fid = fid;

            if (fid < 0)
            {
                result.errorMessage = "Playground with negative fid requested. This is not possible.";
                return result;
            }

            try
            {
                using (NpgsqlConnection pgConn = new NpgsqlConnection(AppConfig.connectionString))
                {
                    await pgConn.OpenAsync();
                    NpgsqlCommand selectComm = pgConn.CreateCommand();
                    selectComm.CommandText = "SELECT nummer, name, geom FROM \"wgr_sp_spielplatz\" WHERE fid=@fid";
                    selectComm.Parameters.AddWithValue("fid", fid);

                    using (NpgsqlDataReader reader = await selectComm.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            result.properties.nummer = reader.IsDBNull(0) ? -1 : reader.GetInt32(0);
                            result.properties.name = reader.IsDBNull(1) ? "" : reader.GetString(1);

                            Point ntsPoint = reader.IsDBNull(2) ? Point.Empty : reader.GetValue(2) as Point;
                            result.geometry = new PlaygroundFeaturePoint(ntsPoint);
                            return result;
                        }
                        else
                        {
                            result.errorMessage = "No playground found for given fid.";
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

        // GET Playground/8262517&inspectiontype=...
        [Route("/Playground/{id}")]
        [HttpGet]
        [Authorize]
        public Playground GetById(int id, string inspectionType)
        {
            return this.readPlaygroundFromDb(id, null, inspectionType);
        }

        // GET Playground/byname?name=...&inspectiontype=Hauptinspektion (HI)
        [Route("/Playground/byname")]
        [HttpGet]
        [Authorize]
        public Playground GetByName(string name, string inspectionType)
        {
            return this.readPlaygroundFromDb(-1, name, inspectionType);
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
                };
            }
            List<Playground> resultTemp = new List<Playground>();

            using (NpgsqlConnection pgConn = new NpgsqlConnection(AppConfig.connectionString))
            {
                pgConn.Open();
                NpgsqlCommand selectComm = pgConn.CreateCommand();
                selectComm.CommandText = "SELECT DISTINCT ON (sp.name) " +
                        "sp.name, insp.datum_inspektion, " +
                        "(SELECT count(*) > 0 " +
                        "FROM \"wgr_sp_insp_mangel\" mangel " +
                        "JOIN \"wgr_sp_insp_bericht\" bericht ON mangel.tid_insp_bericht = bericht.tid " +
                        "JOIN \"gr_v_spielgeraete\" geraete ON bericht.fid_spielgeraet = geraete.fid " +
                        "WHERE geraete.fid_spielplatz = sp.fid " +
                        "AND mangel.fid_erledigung IS NULL) AS geraet_hat_mangel, " +
                        "(SELECT count(*) > 0 " +
                        "FROM \"wgr_sp_insp_mangel\" mangel " +
                        "JOIN \"wgr_sp_insp_bericht\" bericht ON mangel.tid_insp_bericht = bericht.tid " +
                        "JOIN \"wgr_sp_geraetedetail\" detail ON bericht.fid_geraet_detail = detail.fid " +
                        "JOIN \"gr_v_spielgeraete\" geraete ON detail.fid_spielgeraet = geraete.fid " +
                        "WHERE geraete.fid_spielplatz = sp.fid " +
                        "AND mangel.fid_erledigung IS NULL) AS detail_hat_mangel " +
                        "FROM \"wgr_sp_spielplatz\" sp " +
                        "LEFT JOIN \"wgr_sp_inspektion\" insp " +
                        "ON insp.fid_spielplatz = sp.fid " +
                        "ORDER BY sp.name, insp.datum_inspektion DESC";

                if (userMailAddress != null && inspectionType != null &&
                        !inspectionType.Equals("Keine Inspektion"))
                {
                    selectComm.CommandText = "SELECT DISTINCT ON (sp.name) " +
                        "sp.name, insp.datum_inspektion, false, false " +
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
                        resultPlayground.hasOpenDeviceDefects = reader.GetBoolean(2);
                        resultPlayground.hasOpenDeviceDetailDefects = reader.GetBoolean(3);

                        resultTemp.Add(resultPlayground);
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

        private Playground readPlaygroundFromDb(int id, string name, string inspectionType)
        {
            Playground currentPlayground = null;

            using (NpgsqlConnection pgConn = new NpgsqlConnection(AppConfig.connectionString))
            {
                pgConn.Open();
                NpgsqlCommand selectComm = pgConn.CreateCommand();
                if (name != null)
                {
                    selectComm.CommandText = "SELECT fid, name FROM \"wgr_sp_spielplatz\" WHERE name=@name";
                    selectComm.Parameters.AddWithValue("name", name);
                }
                else
                {
                    selectComm.CommandText = "SELECT fid, name FROM \"wgr_sp_spielplatz\" WHERE fid=@id";
                    selectComm.Parameters.AddWithValue("id", id);
                }

                using (NpgsqlDataReader reader = selectComm.ExecuteReader())
                {
                    currentPlayground = new Playground();
                    reader.Read();
                    currentPlayground.id = reader.GetInt32(0);
                    currentPlayground.name = reader.IsDBNull(1) ? "" : reader.GetString(1);
                }
                pgConn.Close();
            }

            if (currentPlayground != null)
            {

                DefectDAO defectDao = new DefectDAO();
                List<string> defectPriorityOptions = defectDao.GetDefectPriorityOptions();
                currentPlayground.defectPriorityOptions = defectPriorityOptions.ToArray();

                currentPlayground.inspectionTypeOptions = InspectionTypesController._GetTypes();

                currentPlayground.playdevices = this._ReadPlaydevicesOfPlayground(currentPlayground.id);

                if (currentPlayground.playdevices != null)
                {
                    this.readDetailsOfPlaydevices(currentPlayground.playdevices, inspectionType);

                    this.readInspectionCriteriaOfPlaydevices(currentPlayground.playdevices, inspectionType);

                    string[] inspectionTypes = InspectionTypesController._GetTypes();

                    this.readReportsOfPlaydevices(currentPlayground.playdevices, inspectionTypes);

                    foreach (PlaydeviceFeature playdevice in currentPlayground.playdevices)
                    {
                        this.readReportsOfPlaydeviceDetail(playdevice.playdeviceDetails, inspectionTypes);
                    }

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
                        "spg.kostenschaetzung, spg.empfohlenes_sanierungsjahr, spg.bemerkung_empf_sanierung, " +
                        "spg.picture_base64 " +
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
                        currentPlaydevice.properties.supplier = reader.IsDBNull(6) ? "" : reader.GetString(6);
                        if (!reader.IsDBNull(7)) currentPlaydevice.properties.costEstimation = reader.GetFloat(7);
                        if (!reader.IsDBNull(8)) currentPlaydevice.properties.recommendedYearOfRenovation = reader.GetInt32(8);
                        currentPlaydevice.properties.commentRecommendedYearOfRenovation = reader.IsDBNull(9) ? "" : reader.GetString(9);

                        byte[] pictureBase64Bytes = reader.IsDBNull(10) ? new byte[0] : (byte[])reader[10];
                        if (pictureBase64Bytes.Length != 0)
                        {
                            currentPlaydevice.properties.pictureBase64String = Encoding.UTF8
                                                .GetString(pictureBase64Bytes, 0, pictureBase64Bytes.Length);
                        }
                        else
                        {
                            currentPlaydevice.properties.pictureBase64String = "";
                        }

                        currentPlaydevice.properties.type = currPlaydeviceType;

                        currentPlaydevices.Add(currentPlaydevice);
                    }
                    result = currentPlaydevices.ToArray();
                }
                pgConn.Close();
            }
            return result;
        }

        private void readReportsOfPlaydevices(PlaydeviceFeature[] playdevices, string[] inspectionTypes)
        {
            using (NpgsqlConnection pgConn = new NpgsqlConnection(AppConfig.connectionString))
            {
                pgConn.Open();
                foreach (PlaydeviceFeature playdevice in playdevices)
                {
                    List<InspectionReport> lastInspectionReports = new List<InspectionReport>();
                    List<InspectionReport> nextToLastInspectionReports = new List<InspectionReport>();

                    foreach (string inspectionType in inspectionTypes)
                    {

                        InspectionCriterionDAO inspectionCriterionDAO = new InspectionCriterionDAO();
                        List<string> inspectionDates = inspectionCriterionDAO
                                .GetInspectionDatesOfPlaydevice(playdevice.properties.fid,
                                            inspectionType, false);

                        NpgsqlCommand selectInspectionComm = pgConn.CreateCommand();
                        Boolean firstRun = true;
                        foreach (string inspectionDate in inspectionDates)
                        {
                            selectInspectionComm.CommandText = "SELECT tid, inspektionsart, datum_inspektion, " +
                                    "kontrolleur, pruefung_text, " +
                                    "pruefung_erledigt, pruefung_kommentar, " +
                                    "wartung_text, wartung_erledigung, " +
                                    "wartung_kommentar, fallschutz " +
                                    "FROM \"wgr_sp_insp_bericht\" " +
                                    "WHERE fid_spielgeraet=" + playdevice.properties.fid + " " +
                                    "AND inspektionsart='" + inspectionType + "' " +
                                    "AND datum_inspektion='" + inspectionDate + "'";

                            using (NpgsqlDataReader reader = selectInspectionComm.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    if (firstRun)
                                    {
                                        InspectionReport lastInspectionReport = readInspectionReport(reader);
                                        lastInspectionReports.Add(lastInspectionReport);

                                    }
                                    else
                                    {
                                        InspectionReport nextToLastInspectionReport = readInspectionReport(reader);
                                        nextToLastInspectionReports.Add(nextToLastInspectionReport);
                                    }
                                }
                            }
                            firstRun = false;
                        }
                    }
                    playdevice.properties.lastInspectionReports = lastInspectionReports.ToArray();
                    playdevice.properties.nextToLastInspectionReports = nextToLastInspectionReports.ToArray();

                    DefectDAO defectDao = new DefectDAO();
                    playdevice.properties.defects = defectDao.Read(playdevice.properties.fid, false);
                }
                pgConn.Close();
            }
        }

        private void readReportsOfPlaydeviceDetail(PlaydeviceDetail[] playdeviceDetails, string[] inspectionTypes)
        {
            using (NpgsqlConnection pgConn = new NpgsqlConnection(AppConfig.connectionString))
            {
                pgConn.Open();
                foreach (PlaydeviceDetail playdeviceDetail in playdeviceDetails)
                {
                    List<InspectionReport> lastInspectionReports = new List<InspectionReport>();
                    List<InspectionReport> nextToLastInspectionReports = new List<InspectionReport>();

                    foreach (string inspectionType in inspectionTypes)
                    {

                        InspectionCriterionDAO inspectionCriterionDAO = new InspectionCriterionDAO();
                        List<string> inspectionDates = inspectionCriterionDAO
                                .GetInspectionDatesOfPlaydevice(playdeviceDetail.properties.fid,
                                            inspectionType, true);

                        Boolean firstRun = true;
                        NpgsqlCommand selectInspRepComm = pgConn.CreateCommand();
                        foreach (string inspectionDate in inspectionDates)
                        {
                            selectInspRepComm.CommandText = "SELECT tid, inspektionsart, datum_inspektion, " +
                                    "kontrolleur, pruefung_text, " +
                                    "pruefung_erledigt, pruefung_kommentar, " +
                                    "wartung_text, wartung_erledigung, " +
                                    "wartung_kommentar, fallschutz " +
                                    "FROM \"wgr_sp_insp_bericht\" " +
                                    "WHERE fid_geraet_detail=" + playdeviceDetail.properties.fid + " " +
                                    "AND inspektionsart='" + inspectionType + "' " +
                                    "AND datum_inspektion='" + inspectionDate + "'";

                            using (NpgsqlDataReader reader = selectInspRepComm.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    if (firstRun)
                                    {
                                        InspectionReport lastInspectionReport = readInspectionReport(reader);
                                        lastInspectionReports.Add(lastInspectionReport);

                                    }
                                    else
                                    {
                                        InspectionReport nextToLastInspectionReport = readInspectionReport(reader);
                                        nextToLastInspectionReports.Add(nextToLastInspectionReport);
                                    }
                                }
                            }
                            firstRun = false;
                        }
                    }
                    playdeviceDetail.properties.lastInspectionReports = lastInspectionReports.ToArray();
                    playdeviceDetail.properties.nextToLastInspectionReports = nextToLastInspectionReports.ToArray();

                    DefectDAO defectDao = new DefectDAO();
                    playdeviceDetail.properties.defects = defectDao.Read(playdeviceDetail.properties.fid, true);
                }
                pgConn.Close();
            }
        }

        private void readDetailsOfPlaydevices(PlaydeviceFeature[] playdevices, string inspectionType)
        {
            using (NpgsqlConnection pgConn = new NpgsqlConnection(AppConfig.connectionString))
            {
                pgConn.Open();
                NpgsqlCommand selectComm = pgConn.CreateCommand();

                List<PlaydeviceDetail> playdevicesDetails = new List<PlaydeviceDetail>();
                List<PlaydeviceDetail> playdevicesDetailsTemp = new List<PlaydeviceDetail>();
                foreach (PlaydeviceFeature currentPlaydevice in playdevices)
                {
                    selectComm.CommandText = "SELECT fid, beschrieb FROM \"wgr_sp_geraetedetail\" " +
                                        "WHERE fid_spielgeraet=" + currentPlaydevice.properties.fid;

                    using (NpgsqlDataReader reader = selectComm.ExecuteReader())
                    {
                        PlaydeviceDetail playdeviceDetail;
                        playdevicesDetailsTemp = new List<PlaydeviceDetail>();
                        while (reader.Read())
                        {
                            playdeviceDetail = new PlaydeviceDetail();
                            playdeviceDetail.properties = new PlaydeviceFeatureProperties();
                            playdeviceDetail.properties.fid = reader.GetInt32(0);
                            playdeviceDetail.properties.type = new PlaydeviceFeatureProperties.Type();
                            playdeviceDetail.properties.type.description = reader.IsDBNull(1) ? "" : reader.GetString(1);
                            playdevicesDetailsTemp.Add(playdeviceDetail);
                            playdevicesDetails.Add(playdeviceDetail);
                        }
                        currentPlaydevice.playdeviceDetails = playdevicesDetailsTemp.ToArray();
                    }
                }
                pgConn.Close();

                this.readInspectionCriteriaOfPlaydeviceDetails(playdevicesDetails.ToArray(), inspectionType);
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

        private void readInspectionCriteriaOfPlaydeviceDetails(PlaydeviceDetail[] playdeviceDetails, string inspectionType)
        {
            if (inspectionType != null && inspectionType.Length > 5)
            {
                inspectionType = inspectionType.Substring(0, inspectionType.Length - 5);
            }

            using (NpgsqlConnection pgConn = new NpgsqlConnection(AppConfig.connectionString))
            {
                pgConn.Open();
                NpgsqlCommand selectComm = pgConn.CreateCommand();

                foreach (PlaydeviceDetail currentPlaydeviceDetail in playdeviceDetails)
                {
                    selectComm.CommandText = "SELECT bereich, pruefung, wartung, " +
                            "inspektionsart, pruefung_kurztext " +
                            "FROM \"wgr_v_sp_gerdet_insp_krit\"" +
                            "WHERE fid_geraet_detail=" + currentPlaydeviceDetail.properties.fid +
                            " AND inspektionsart=@inspektionsart";
                    selectComm.Parameters.AddWithValue("inspektionsart", inspectionType);

                    using (NpgsqlDataReader reader = selectComm.ExecuteReader())
                    {
                        InspectionCriterion inspectionCriterion;
                        List<InspectionCriterion> inspectionCriteria = new List<InspectionCriterion>();
                        while (reader.Read())
                        {
                            inspectionCriterion = new InspectionCriterion();
                            inspectionCriterion.realm = reader.IsDBNull(0) ? "" : reader.GetString(0);
                            inspectionCriterion.check = reader.IsDBNull(1) ? "" : reader.GetString(1);
                            inspectionCriterion.maintenance = reader.IsDBNull(2) ? "" : reader.GetString(2);
                            inspectionCriterion.inspectionType = reader.IsDBNull(3) ? "" : reader.GetString(3);
                            inspectionCriterion.checkShortText = reader.IsDBNull(4) ? "" : reader.GetString(4);
                            inspectionCriteria.Add(inspectionCriterion);
                        }
                        currentPlaydeviceDetail.properties.generalInspectionCriteria = inspectionCriteria.ToArray();
                    }
                }
                pgConn.Close();
            }
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
            return inspectionReport;
        }

    }

}

