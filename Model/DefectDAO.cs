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

        internal Defect[] Read(int playdeviceFid)
        {
            List<Defect> result = new();

            using (NpgsqlConnection pgConn = new(AppConfig.connectionString))
            {
                pgConn.Open();
                NpgsqlCommand selectDefectsComm = this._CreateCommandForSelect(
                            playdeviceFid, pgConn);

                using (NpgsqlDataReader reader = selectDefectsComm.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        Defect defect = _ReadDefect(reader);
                        if (defect.tid != -1)
                        {
                            result.Add(defect);
                        }
                    }
                }

                foreach(Defect defect in result)
                    defect.pictures = ReadAllPictures(defect.tid, pgConn);
                
            }
            return result.ToArray();
        }

        internal static void Insert(Defect defect, int idPriority, int inspectionTid,
                    User userFromDb, bool dryRun)
        {
            if (idPriority != -1)
            {
                using NpgsqlConnection pgConn = new(AppConfig.connectionString);
                pgConn.Open();

                DbCommand insertDefectCommand = CreateCommandForInsert(defect, idPriority,
                                inspectionTid, pgConn, userFromDb);
                int defectNewTid = -1;
                if (!dryRun) defectNewTid = (int)insertDefectCommand.ExecuteScalar();

                foreach (DefectPicture defectPic in defect.pictures)
                {
                    DbCommand insertDefectPicCommand = CreateCommandForInsertPictures(defectPic,
                            defectNewTid, pgConn);
                    if (!dryRun) insertDefectPicCommand.ExecuteNonQuery();
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
                    if (insertDefectCommand != null)
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

        private NpgsqlCommand _CreateCommandForSelect(int playdeviceFid, NpgsqlConnection pgConn)
        {
            NpgsqlCommand selectDefectsCommand = pgConn.CreateCommand();
            selectDefectsCommand.CommandText = "SELECT m.tid, d.short_value, d.value, m.beschrieb, " +
                    "m.datum_erledigung, m.fid_erledigung, m.bemerkunng, m.datum, " +
                    "m.id_zustaendig_behebung " +
                    "FROM \"wgr_sp_insp_mangel\" m " +
                    "LEFT JOIN \"wgr_sp_dringlichkeit_tbd\" d ON m.id_dringlichkeit = d.id " +
                    "WHERE m.fid_spielgeraet=" + playdeviceFid +
                    " AND m.datum_erledigung IS NULL";
            return selectDefectsCommand;
        }

        private Defect _ReadDefect(NpgsqlDataReader reader)
        {
            Defect defect = new()
            {
                tid = reader.IsDBNull(0) ? -1 : reader.GetInt32(0)
            };
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
                defect.defectsResponsibleBodyId = reader.GetInt32(8);
            }
            return defect;
        }

        private static DefectPicture[] ReadAllPictures(int defectTid,
                    NpgsqlConnection pgConn)
        {
            List<DefectPicture> result = new();

            NpgsqlCommand selectDefectsCommand = pgConn.CreateCommand();
            selectDefectsCommand.CommandText = @$"SELECT picture_base64, picture_base64_thumb,
                                  zeitpunkt
                                FROM ""wgr_sp_insp_mangel_foto""
                                WHERE tid_maengel={defectTid}";

            using (NpgsqlDataReader reader = selectDefectsCommand.ExecuteReader())
            {
                DefectPicture defectPicture;
                while (reader.Read())
                {
                    defectPicture = new()
                    {
                        base64StringPicture = reader.IsDBNull(0) ? "" : reader.GetString(0),
                        base64StringPictureThumb = reader.IsDBNull(1) ? "" : reader.GetString(1),
                        afterFixing = reader.IsDBNull(2) ? false : reader.GetBoolean(2)
                    };
                    result.Add(defectPicture);
                }
            }
            return result.ToArray();
        }

        private static DbCommand CreateCommandForInsert(Defect defect, int idPriority,
                    int inspectionTid, NpgsqlConnection pgConn, User userFromDb)
        {
            NpgsqlCommand insertDefectCommand = pgConn.CreateCommand();
            insertDefectCommand.CommandText = "INSERT INTO \"wgr_sp_insp_mangel\" " +
                    "(tid, fid_spielgeraet, tid_inspektion, id_dringlichkeit, beschrieb, bemerkunng, " +
                    "datum_erledigung, fid_erledigung, id_zustaendig_behebung)" +
                    "VALUES (" +
                    "(SELECT CASE WHEN max(tid) IS NULL THEN 1 ELSE max(tid) + 1 END FROM \"wgr_sp_insp_mangel\"), " +
                    "@fid_spielgeraet, @tid_inspektion, @dringlichkeit, @beschrieb, " +
                    "@bemerkung, @datum_erledigung, @fid_erledigung, @id_zustaendig_behebung) RETURNING tid";

            insertDefectCommand.Parameters.AddWithValue("tid_inspektion", inspectionTid);
            insertDefectCommand.Parameters.AddWithValue("fid_spielgeraet", defect.playdeviceFid);
            insertDefectCommand.Parameters.AddWithValue("dringlichkeit", idPriority);
            insertDefectCommand.Parameters.AddWithValue("beschrieb", defect.defectDescription ?? "");
            insertDefectCommand.Parameters.AddWithValue("bemerkung", defect.defectComment ?? "");
            insertDefectCommand.Parameters.AddWithValue("id_zustaendig_behebung",
                            defect.defectsResponsibleBodyId > 0 ? defect.defectsResponsibleBodyId : DBNull.Value);

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

        private static DbCommand CreateCommandForInsertPictures(DefectPicture defectPic,
                        int defectTid, NpgsqlConnection pgConn)
        {
            NpgsqlCommand insertDefectPicCommand = pgConn.CreateCommand();
            insertDefectPicCommand.CommandText = "INSERT INTO \"wgr_sp_insp_mangel_foto\" " +
                    "(tid, tid_maengel, picture_base64, picture_base64_thumb, zeitpunkt)" +
                    "VALUES (" +
                    "(SELECT CASE WHEN max(tid) IS NULL THEN 1 ELSE max(tid) + 1 END FROM \"wgr_sp_insp_mangel_foto\"), " +
                    "@tid_maengel, @picture_base64, @picture_base64_thumb, @zeitpunkt)";


            insertDefectPicCommand.Parameters.AddWithValue("tid_maengel", defectTid);
            insertDefectPicCommand.Parameters.AddWithValue("picture_base64", defectPic.base64StringPicture);
            insertDefectPicCommand.Parameters.AddWithValue("picture_base64_thumb", defectPic.base64StringPictureThumb);
            insertDefectPicCommand.Parameters.AddWithValue("zeitpunkt", defectPic.afterFixing);

            return insertDefectPicCommand;
        }

        private DbCommand _CreateCommandForUpdate(Defect defect, NpgsqlConnection pgConn,
                    User userFromDb, bool dryRun)
        {
            if (dryRun) return null;
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