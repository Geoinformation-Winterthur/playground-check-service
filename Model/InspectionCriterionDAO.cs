// <copyright company="Vermessungsamt Winterthur">
//      Author: Edgar Butwilowski
//      Copyright (c) Vermessungsamt Winterthur. All rights reserved.
// </copyright>

using System.Collections.Generic;
using Npgsql;
using NpgsqlTypes;
using playground_check_service.Configuration;

namespace playground_check_service.Model
{
    public class InspectionCriterionDAO
    {
        internal List<string> GetInspectionDatesOfPlaydevice(int playdeviceFid,
                        string inspectionType, bool isDetail)
        {
            List<string> result = new List<string>();
            using (NpgsqlConnection pgConn = new NpgsqlConnection(AppConfig.connectionString))
            {
                pgConn.Open();
                NpgsqlCommand selectDatesComm = _CreateSelectInspectionDateCommand(playdeviceFid,
                        inspectionType, isDetail, pgConn);

                using (NpgsqlDataReader reader = selectDatesComm.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        NpgsqlDate inspectionDate = reader.GetDate(0);
                        string inspectionDateString = inspectionDate.Day + "." +
                                inspectionDate.Month + "." + inspectionDate.Year;
                        result.Add(inspectionDateString);
                    }
                }
            }
            return result;
        }

        internal List<InspectionCriterion> Read(NpgsqlDataReader reader)
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
            return inspectionCriteria;
        }

        private NpgsqlCommand _CreateSelectInspectionDateCommand(int playdeviceFid,
                string inspectionType, bool isDetail, NpgsqlConnection pgConn)
        {
            NpgsqlCommand selectDatesComm = pgConn.CreateCommand();
            selectDatesComm.CommandText = "SELECT DISTINCT datum_inspektion " +
                "FROM \"wgr_sp_insp_bericht\" ";
            if (isDetail)
            {
                selectDatesComm.CommandText += "WHERE fid_geraet_detail=" + playdeviceFid;
            }
            else
            {
                selectDatesComm.CommandText += "WHERE fid_spielgeraet=" + playdeviceFid;
            }
            selectDatesComm.CommandText += " AND inspektionsart='" + inspectionType + "' " +
                    "ORDER BY datum_inspektion DESC LIMIT 2";

            return selectDatesComm;
        }

    }
}