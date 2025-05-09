// <copyright company="Geoinformation Winterthur">
//      Author: Edgar Butwilowski
//      Copyright (c) Geoinformation Winterthur. All rights reserved.
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

        internal Defect Read(int tid)
        {
            Defect result = null;

            using (NpgsqlConnection pgConn = new(AppConfig.connectionString))
            {
                pgConn.Open();
                NpgsqlCommand selectDefectsComm = this._CreateCommandForSelect(tid, pgConn);

                using (NpgsqlDataReader reader = selectDefectsComm.ExecuteReader())
                {
                    if (reader.Read()) result = _ReadDefect(reader);
                }
                if (result != null)
                {
                    result.defectPicsTids = _ReadAllPictureTids(result.tid, false, pgConn);
                    result.defectPicsAfterFixingTids = _ReadAllPictureTids(result.tid, true, pgConn);
                }
            }
            return result;
        }

        internal Defect[] ReadAllOfPlaydevice(int playdeviceFid)
        {
            List<Defect> result = new();

            using (NpgsqlConnection pgConn = new(AppConfig.connectionString))
            {
                pgConn.Open();
                NpgsqlCommand selectDefectsComm = this._CreateCommandForSelectByPlaydevice(
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
            }
            return result.ToArray();
        }

        internal void Insert(Defect defect, User userFromDb,
                    bool dryRun = false)
        {
            if (!string.IsNullOrWhiteSpace(defect.defectDescription))
            {
                using NpgsqlConnection pgConn = new(AppConfig.connectionString);
                pgConn.Open();

                DbCommand insertDefectCommand = CreateCommandForInsert(defect,
                                pgConn, userFromDb);
                int defectNewTid = -1;
                if (!dryRun) defectNewTid = (int)insertDefectCommand.ExecuteScalar();
                defect.tid = defectNewTid;
            }
        }

        internal void Update(Defect defect, User userFromDb, bool dryRun)
        {
            if (defect != null)
            {
                using (NpgsqlConnection pgConn = new NpgsqlConnection(AppConfig.connectionString))
                {
                    pgConn.Open();
                    DbCommand updateDefectCommand = this._CreateCommandForUpdate(defect, pgConn,
                        userFromDb, dryRun);
                    if (updateDefectCommand != null)
                    {
                        updateDefectCommand.ExecuteNonQuery();
                    }
                }
            }
        }

        private NpgsqlCommand _CreateCommandForSelectByPlaydevice(int playdeviceFid, NpgsqlConnection pgConn)
        {
            NpgsqlCommand selectDefectsCommand = pgConn.CreateCommand();
            selectDefectsCommand.CommandText = "SELECT tid, id_dringlichkeit, beschrieb, " +
                    "datum_erledigung, fid_erledigung, bemerkunng, datum, " +
                    "id_zustaendig_behebung " +
                    "FROM \"wgr_sp_insp_mangel\" " +
                    "WHERE fid_spielgeraet=" + playdeviceFid +
                    " AND datum_erledigung IS NULL";
            return selectDefectsCommand;
        }

        private NpgsqlCommand _CreateCommandForSelectDefectPriorityIds(NpgsqlConnection pgConn)
        {
            NpgsqlCommand selectDefectPriorityIds = pgConn.CreateCommand();
            selectDefectPriorityIds.CommandText =
                        "SELECT id, short_value, value FROM \"wgr_sp_dringlichkeit_tbd\"";
            return selectDefectPriorityIds;
        }
        
        private NpgsqlCommand _CreateCommandForSelect(int tid, NpgsqlConnection pgConn)
        {
            NpgsqlCommand selectDefectCommand = pgConn.CreateCommand();
            selectDefectCommand.CommandText = "SELECT tid, id_dringlichkeit, beschrieb, " +
                    "datum_erledigung, fid_erledigung, bemerkunng, datum, " +
                    "id_zustaendig_behebung " +
                    "FROM \"wgr_sp_insp_mangel\" " +
                    "WHERE tid=" + tid;
            return selectDefectCommand;
        }

        private Defect _ReadDefect(NpgsqlDataReader reader)
        {
            Defect defect = new()
            {
                tid = reader.IsDBNull(0) ? -1 : reader.GetInt32(0)
            };
            int priorityOrdinal = reader.GetOrdinal("id_dringlichkeit");
            defect.priority = reader.IsDBNull(priorityOrdinal) ? -1 : reader.GetInt32(priorityOrdinal);
            int defectDescriptionOrdinal = reader.GetOrdinal("beschrieb");
            defect.defectDescription = reader.IsDBNull(defectDescriptionOrdinal) ? "" :
                        reader.GetString(defectDescriptionOrdinal);
            int dateDoneOrdinal = reader.GetOrdinal("datum_erledigung");
            if (!reader.IsDBNull(dateDoneOrdinal))
            {
                NpgsqlDate dateDone = reader.GetDate(dateDoneOrdinal);
                defect.dateDone = (DateTime)dateDone;
            }
            int defectCommentOrdinal = reader.GetOrdinal("bemerkunng");
            defect.defectComment = reader.IsDBNull(defectCommentOrdinal) ? "" : reader.GetString(defectCommentOrdinal);
            int dateCreationOrdinal = reader.GetOrdinal("datum");
            if (!reader.IsDBNull(dateCreationOrdinal))
            {
                NpgsqlDate dateCreation = reader.GetDate(dateCreationOrdinal);
                defect.dateCreation = (DateTime)dateCreation;
            }
            int defectsResponsibleBodyIdOrdinal = reader.GetOrdinal("id_zustaendig_behebung");            
            if (!reader.IsDBNull(defectsResponsibleBodyIdOrdinal))
                defect.defectsResponsibleBodyId = reader.GetInt32(defectsResponsibleBodyIdOrdinal);
            return defect;
        }


        private static DbCommand CreateCommandForInsert(Defect defect,
                        NpgsqlConnection pgConn, User userFromDb)
        {
            NpgsqlCommand insertDefectCommand = pgConn.CreateCommand();
            insertDefectCommand.CommandText = "INSERT INTO \"wgr_sp_insp_mangel\" " +
                    "(tid, fid_spielgeraet, datum, id_dringlichkeit, beschrieb, bemerkunng, " +
                    "datum_erledigung, fid_erledigung, id_zustaendig_behebung)" +
                    "VALUES (" +
                    "(SELECT CASE WHEN max(tid) IS NULL THEN 1 ELSE max(tid) + 1 END FROM \"wgr_sp_insp_mangel\"), " +
                    "@fid_spielgeraet, CURRENT_TIMESTAMP, @dringlichkeit, @beschrieb, " +
                    "@bemerkung, @datum_erledigung, @fid_erledigung, @id_zustaendig_behebung) RETURNING tid";

            insertDefectCommand.Parameters.AddWithValue("fid_spielgeraet", defect.playdeviceFid);
            insertDefectCommand.Parameters.AddWithValue("dringlichkeit", defect.priority);
            insertDefectCommand.Parameters.AddWithValue("beschrieb", defect.defectDescription ?? "");
            insertDefectCommand.Parameters.AddWithValue("bemerkung", defect.defectComment ?? "");
            insertDefectCommand.Parameters.AddWithValue("id_zustaendig_behebung",
                            defect.defectsResponsibleBodyId > 0 ? defect.defectsResponsibleBodyId : DBNull.Value);

            if (defect.dateDone != null)
            {
                NpgsqlDate dateDone = (NpgsqlDate) DateTime.Now;
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

        private static int[] _ReadAllPictureTids(int defectTid, bool isFixed, NpgsqlConnection pgConn)
        {
            List<int> result = new();

            NpgsqlCommand selectDefectsCommand = pgConn.CreateCommand();
            selectDefectsCommand.CommandText = @$"SELECT tid
                                FROM ""wgr_sp_insp_mangel_foto""
                                WHERE tid_maengel={defectTid} AND zeitpunkt={isFixed}";

            using (NpgsqlDataReader reader = selectDefectsCommand.ExecuteReader())
            {
                while (reader.Read())
                {
                    int tid = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
                    result.Add(tid);
                }
            }
            return result.ToArray();
        }

        private DbCommand _CreateCommandForUpdate(Defect defect, NpgsqlConnection pgConn,
                    User userFromDb, bool dryRun)
        {
            if (dryRun) return null;
            NpgsqlCommand updateDefectCommand = pgConn.CreateCommand();
            updateDefectCommand.CommandText = @"UPDATE ""wgr_sp_insp_mangel""
                    SET id_dringlichkeit = @id_dringlichkeit, beschrieb = @beschrieb,
                    bemerkunng = @bemerkung, datum_erledigung = @datum_erledigung,
                    id_zustaendig_behebung = @id_zustaendig_behebung, fid_erledigung = @fid_erledigung
                    WHERE tid = @tid";
            updateDefectCommand.Parameters.AddWithValue("tid", defect.tid);
            updateDefectCommand.Parameters.AddWithValue("id_dringlichkeit", defect.priority);
            updateDefectCommand.Parameters.AddWithValue("beschrieb", defect.defectDescription);
            updateDefectCommand.Parameters.AddWithValue("bemerkung", defect.defectComment);
            NpgsqlDate? dateDone = null;
            if(defect.dateDone != null) dateDone = (NpgsqlDate)defect.dateDone;
            updateDefectCommand.Parameters.AddWithValue("datum_erledigung", dateDone != null ? dateDone : DBNull.Value);
            updateDefectCommand.Parameters.AddWithValue("id_zustaendig_behebung", defect.defectsResponsibleBodyId);
            updateDefectCommand.Parameters.AddWithValue("fid_erledigung", dateDone != null ? userFromDb.fid : DBNull.Value);
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