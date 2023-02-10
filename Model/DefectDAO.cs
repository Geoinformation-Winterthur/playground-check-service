// <copyright company="Vermessungsamt Winterthur">
//      Author: Edgar Butwilowski
//      Copyright (c) Vermessungsamt Winterthur. All rights reserved.
// </copyright>

using System.Data.Common;
using Npgsql;
using NpgsqlTypes;
using playground_check_service.Configuration;

namespace playground_check_service.Model
{
    public class DefectDAO
    {

        internal List<string> GetDefectPriorityOptions()
        {
            List<string> result = new List<string>();

            using (NpgsqlConnection pgConn = new NpgsqlConnection(AppConfig.connectionString))
            {
                pgConn.Open();
                NpgsqlCommand selectDefectPriorityIds = this._CreateCommandForSelectDefectPriorityIds(pgConn);
                using (NpgsqlDataReader reader = selectDefectPriorityIds.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        if (!reader.IsDBNull(0))
                        {
                            string shortValue = reader.IsDBNull(1) ? "" : reader.GetString(1);
                            string longValue = reader.IsDBNull(2) ? "" : reader.GetString(2);
                            string finalName = DefectDAO._ConcatPriorityOfDefect(shortValue, longValue);
                            if (finalName.Length != 0)
                            {
                                result.Add(finalName);
                            }
                        }
                    }
                }
            }
            return result;
        }

        internal Dictionary<string, int> GetDefectPriorityIds()
        {
            Dictionary<string, int> result = new Dictionary<string, int>();

            using (NpgsqlConnection pgConn = new NpgsqlConnection(AppConfig.connectionString))
            {
                pgConn.Open();
                NpgsqlCommand selectDefectPriorityIds = this._CreateCommandForSelectDefectPriorityIds(pgConn);
                using (NpgsqlDataReader reader = selectDefectPriorityIds.ExecuteReader())
                {
                    while (reader.Read())
                    {

                        int id = reader.IsDBNull(0) ? -1 : reader.GetInt32(0);
                        string shortValue = reader.IsDBNull(1) ? "" : reader.GetString(1);
                        string longValue = reader.IsDBNull(2) ? "" : reader.GetString(2);

                        if (id >= 0 && shortValue.Length != 0 && longValue.Length != 0)
                        {
                            string finalName = DefectDAO._ConcatPriorityOfDefect(shortValue, longValue);
                            result.Add(finalName, id);
                        }

                    }
                }
            }
            return result;
        }

        internal Defect[] Read(int playdeviceFid, bool isDetail)
        {
            List<Defect> result = new List<Defect>();

            using (NpgsqlConnection pgConn = new NpgsqlConnection(AppConfig.connectionString))
            {
                pgConn.Open();
                NpgsqlCommand selectDefectsComm = this._CreateCommandForSelect(playdeviceFid,
                                isDetail, pgConn);

                using (NpgsqlDataReader reader = selectDefectsComm.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        Defect defect = this._ReadDefect(reader);
                        if (defect.tid != -1)
                        {
                            result.Add(defect);
                        }
                    }
                }
            }
            return result.ToArray();
        }

        internal void Insert(Defect defect, int idPriority, int inspectionTid,
                    User userFromDb, bool dryRun)
        {
            if (idPriority != -1)
            {
                using (NpgsqlConnection pgConn = new NpgsqlConnection(AppConfig.connectionString))
                {
                    pgConn.Open();
                    DbCommand insertDefectCommand = this._CreateCommandForInsert(defect, idPriority,
                                    inspectionTid, pgConn, userFromDb, dryRun);
                    if(insertDefectCommand != null)
                    {
                        insertDefectCommand.ExecuteNonQuery();
                    }
                }
            }
        }

        internal void Update(Defect defect, User userFromDb, bool dryRun)
        {
            if (defect != null && defect.dateDone != null)
            {
                using (NpgsqlConnection pgConn = new NpgsqlConnection(AppConfig.connectionString))
                {
                    pgConn.Open();
                    DbCommand insertDefectCommand = this._CreateCommandForUpdate(defect, pgConn,
                        userFromDb, dryRun);
                    if(insertDefectCommand != null)
                    {
                        insertDefectCommand.ExecuteNonQuery();
                    }
                }
            }
        }

        private NpgsqlCommand _CreateCommandForSelectDefectPriorityIds(NpgsqlConnection pgConn)
        {
            NpgsqlCommand selectDefectPriorityIds = pgConn.CreateCommand();
            selectDefectPriorityIds.CommandText =
                        "SELECT id, short_value, value FROM \"wgr_sp_dringlichkeit_tbd\"";
            return selectDefectPriorityIds;
        }

        private NpgsqlCommand _CreateCommandForSelect(int playdeviceFid, bool isDetail,
                    NpgsqlConnection pgConn)
        {
            NpgsqlCommand selectDefectsCommand = pgConn.CreateCommand();
            selectDefectsCommand.CommandText = "SELECT m.tid, d.short_value, d.value, m.beschrieb, " +
                    "m.datum_erledigung, m.fid_erledigung, m.bemerkunng, b.datum_inspektion, " +
                    "m.picture1_base64, m.picture2_base64, m.picture3_base64, " +
                    "m.picture1_base64_thumb, m.picture2_base64_thumb, m.picture3_base64_thumb " +
                    "FROM \"wgr_sp_insp_mangel\" m " +
                    "JOIN \"wgr_sp_insp_bericht\" b ON m.tid_insp_bericht = b.tid " +
                    "LEFT JOIN \"wgr_sp_dringlichkeit_tbd\" d ON m.id_dringlichkeit = d.id ";
            if (isDetail)
            {
                selectDefectsCommand.CommandText += "WHERE b.fid_geraet_detail=" + playdeviceFid;
            }
            else
            {
                selectDefectsCommand.CommandText += "WHERE b.fid_spielgeraet=" + playdeviceFid;
            }
            selectDefectsCommand.CommandText += " AND m.datum_erledigung IS NULL";
            return selectDefectsCommand;
        }

        private Defect _ReadDefect(NpgsqlDataReader reader)
        {
            Defect defect = new Defect();
            defect.tid = reader.IsDBNull(0) ? -1 : reader.GetInt32(0);
            string shortValue = reader.IsDBNull(1) ? "Unbekannt" : reader.GetString(1);
            string longValue = reader.IsDBNull(2) ? "Unbekannt" : reader.GetString(2);
            defect.priority = DefectDAO._ConcatPriorityOfDefect(shortValue, longValue);
            defect.defectDescription = reader.IsDBNull(3) ? "Keine Beschreibung" : reader.GetString(3);
            if (!reader.IsDBNull(4))
            {
                NpgsqlDate dateDone = reader.GetDate(4);
                defect.dateDone = (DateTime)dateDone;
            }
            defect.defectComment = reader.IsDBNull(6) ? "Kein Kommentar" : reader.GetString(6);
            if (!reader.IsDBNull(7))
            {
                NpgsqlDate dateCreation = reader.GetDate(7);
                defect.dateCreation = (DateTime)dateCreation;
            }
            if (!reader.IsDBNull(8))
            {
                defect.picture1Base64String = reader.GetString(8);
            }
            if (!reader.IsDBNull(9))
            {
                defect.picture2Base64String = reader.GetString(9);
            }
            if (!reader.IsDBNull(10))
            {
                defect.picture3Base64String = reader.GetString(10);
            }
            if (!reader.IsDBNull(11))
            {
                defect.picture1Base64StringThumb = reader.GetString(11);
            }
            if (!reader.IsDBNull(12))
            {
                defect.picture2Base64StringThumb = reader.GetString(12);
            }
            if (!reader.IsDBNull(13))
            {
                defect.picture3Base64StringThumb = reader.GetString(13);
            }
            return defect;
        }

        private DbCommand _CreateCommandForInsert(Defect defect, int idPriority,
                    int inspectionTid, NpgsqlConnection pgConn, User userFromDb, bool dryRun)
        {
            if(dryRun) return null;

            NpgsqlCommand insertDefectCommand = pgConn.CreateCommand();
            insertDefectCommand.CommandText = "INSERT INTO \"wgr_sp_insp_mangel\" " +
                    "(tid, tid_insp_bericht, id_dringlichkeit, beschrieb, bemerkunng, " +
                    "picture1_base64, picture2_base64, picture3_base64, " +
                    "picture1_base64_thumb, picture2_base64_thumb, picture3_base64_thumb, " +
                    "datum_erledigung, fid_erledigung)" +
                    "VALUES (" +
                    "(SELECT CASE WHEN max(tid) IS NULL THEN 1 ELSE max(tid) + 1 END FROM \"wgr_sp_insp_mangel\"), "+
                    "@tid_inspektionsbericht, @dringlichkeit, @beschrieb, " +
                    "@bemerkung, @picture1_base64, @picture2_base64, @picture3_base64, " +
                    "@picture1_base64_thumb, @picture2_base64_thumb, @picture3_base64_thumb, " +
                    "@datum_erledigung, @fid_erledigung)";


            insertDefectCommand.Parameters.AddWithValue("tid_inspektionsbericht", inspectionTid);
            insertDefectCommand.Parameters.AddWithValue("dringlichkeit", idPriority);
            insertDefectCommand.Parameters.AddWithValue("beschrieb", defect.defectDescription != null ? defect.defectDescription : "");
            insertDefectCommand.Parameters.AddWithValue("bemerkung", defect.defectComment != null ? defect.defectComment : "");
            insertDefectCommand.Parameters.AddWithValue("picture1_base64",
                                    defect.picture1Base64String != null ? defect.picture1Base64String : "");
            insertDefectCommand.Parameters.AddWithValue("picture2_base64",
                                    defect.picture2Base64String != null ? defect.picture2Base64String : "");
            insertDefectCommand.Parameters.AddWithValue("picture3_base64",
                                    defect.picture3Base64String != null ? defect.picture3Base64String : "");
            insertDefectCommand.Parameters.AddWithValue("picture1_base64_thumb",
                                    defect.picture1Base64StringThumb != null ? defect.picture1Base64StringThumb : "");
            insertDefectCommand.Parameters.AddWithValue("picture2_base64_thumb",
                                    defect.picture2Base64StringThumb != null ? defect.picture2Base64StringThumb : "");
            insertDefectCommand.Parameters.AddWithValue("picture3_base64_thumb",
                                    defect.picture3Base64StringThumb != null ? defect.picture3Base64StringThumb : "");
            if (defect.dateDone != null)
            {
                NpgsqlDate dateDone = (NpgsqlDate)defect.dateDone;
                insertDefectCommand.Parameters.AddWithValue("datum_erledigung", dateDone);
                insertDefectCommand.Parameters.AddWithValue("fid_erledigung", userFromDb.fid);
            }
            else
            {
                insertDefectCommand.Parameters.AddWithValue("datum_erledigung", DBNull.Value);
                insertDefectCommand.Parameters.AddWithValue("fid_erledigung", DBNull.Value);
            }
            return insertDefectCommand;
        }

        private DbCommand _CreateCommandForUpdate(Defect defect, NpgsqlConnection pgConn,
                    User userFromDb, bool dryRun)
        {
            if(dryRun) return null;
            NpgsqlCommand updateDefectCommand = pgConn.CreateCommand();
            updateDefectCommand.CommandText = "UPDATE \"wgr_sp_insp_mangel\" " +
                    "SET datum_erledigung=@datum_erledigung, bemerkunng=@bemerkung, " +
                    "fid_erledigung=@fid_erledigung " +
                    "WHERE tid=@tid";
            updateDefectCommand.Parameters.AddWithValue("tid", defect.tid);
            NpgsqlDate dateDone = (NpgsqlDate)defect.dateDone;
            updateDefectCommand.Parameters.AddWithValue("datum_erledigung", dateDone);
            updateDefectCommand.Parameters.AddWithValue("bemerkung", defect.defectComment);
            updateDefectCommand.Parameters.AddWithValue("fid_erledigung", userFromDb.fid);
            return updateDefectCommand;
        }

        private static string _ConcatPriorityOfDefect(string shortValue, string longValue)
        {
            string result = "";
            if (shortValue != null)
            {
                shortValue = shortValue.Trim();
                result += shortValue;
            }

            if (longValue != null)
            {
                longValue = longValue.Trim();
                if (result.Length != 0)
                {
                    result += " (" + longValue + ")";
                }
                else
                {
                    result += longValue;
                }
            }
            return result;
        }



    }
}