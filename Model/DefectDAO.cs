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

        internal User ReadActiveUserByFid(int userFid)
        {
            User result = null;
            using NpgsqlConnection pgConn = new(AppConfig.connectionString);
            pgConn.Open();
            using NpgsqlCommand command = pgConn.CreateCommand();
            command.CommandText = @"SELECT fid, nachname, vorname, trim(lower(e_mail)), aktiv, rolle, is_new
                        FROM ""wgr_sp_kontrolleur""
                        WHERE fid = @fid AND aktiv = true";
            command.Parameters.AddWithValue("fid", userFid);
            using NpgsqlDataReader reader = command.ExecuteReader();
            if (reader.Read())
            {
                result = new User
                {
                    fid = reader.IsDBNull(0) ? -1 : reader.GetInt32(0),
                    lastName = reader.IsDBNull(1) ? "" : reader.GetString(1).Trim(),
                    firstName = reader.IsDBNull(2) ? "" : reader.GetString(2).Trim(),
                    mailAddress = reader.IsDBNull(3) ? "" : reader.GetString(3).ToLower().Trim(),
                    active = reader.IsDBNull(4) ? false : reader.GetBoolean(4),
                    role = reader.IsDBNull(5) ? "" : reader.GetString(5),
                    isNew = reader.IsDBNull(6) ? false : reader.GetBoolean(6),
                    passPhrase = ""
                };
            }
            return result;
        }


        internal Defect MarkInfoMailSent(int defectTid, bool dryRun = false)
        {
            if (defectTid <= 0) return null;

            using NpgsqlConnection pgConn = new(AppConfig.connectionString);
            pgConn.Open();

            if (!dryRun)
            {
                using NpgsqlCommand command = pgConn.CreateCommand();
                command.CommandText = @"UPDATE ""wgr_sp_insp_mangel"" mangel
                    SET infomail_gesendet_am = CURRENT_TIMESTAMP,
                        infomail_empfaenger = TRIM(CONCAT(kontrolleur.vorname, ' ', kontrolleur.nachname))
                    FROM ""wgr_sp_kontrolleur"" kontrolleur
                    WHERE mangel.tid = @tid
                      AND kontrolleur.fid = mangel.fid_zustaendig_kontrolleur";
                command.Parameters.AddWithValue("tid", defectTid);
                if (command.ExecuteNonQuery() != 1) return null;
            }

            return Read(defectTid);
        }

        internal bool SetAssignmentStatus(int defectTid, User userFromDb, bool accepted, string comment, bool dryRun)
        {
            if (dryRun) return true;
            if (defectTid <= 0 || userFromDb == null || userFromDb.fid <= 0) return false;

            using NpgsqlConnection pgConn = new(AppConfig.connectionString);
            pgConn.Open();
            using NpgsqlCommand command = pgConn.CreateCommand();

            if (accepted)
            {
                command.CommandText = @"UPDATE ""wgr_sp_insp_mangel""
                    SET auftrag_status = 'angenommen',
                        datum_auftrag_angenommen = CURRENT_TIMESTAMP,
                        datum_auftrag_abgelehnt = NULL,
                        bemerkung_auftrag = @bemerkung_auftrag
                    WHERE tid = @tid
                      AND fid_zustaendig_kontrolleur = @fid_kontrolleur";
            }
            else
            {
                command.CommandText = @"UPDATE ""wgr_sp_insp_mangel""
                    SET auftrag_status = 'abgelehnt',
                        datum_auftrag_abgelehnt = CURRENT_TIMESTAMP,
                        bemerkung_auftrag = @bemerkung_auftrag
                    WHERE tid = @tid
                      AND fid_zustaendig_kontrolleur = @fid_kontrolleur";
            }

            command.Parameters.AddWithValue("tid", defectTid);
            command.Parameters.AddWithValue("fid_kontrolleur", userFromDb.fid);
            command.Parameters.AddWithValue("bemerkung_auftrag", string.IsNullOrWhiteSpace(comment) ? DBNull.Value : comment.Trim());
            return command.ExecuteNonQuery() == 1;
        }

        private NpgsqlCommand _CreateCommandForSelectByPlaydevice(int playdeviceFid, NpgsqlConnection pgConn)
        {
            NpgsqlCommand selectDefectsCommand = pgConn.CreateCommand();
            selectDefectsCommand.CommandText = "SELECT tid, fid_spielgeraet, id_dringlichkeit, beschrieb, " +
                    "datum_erledigung, fid_erledigung, bemerkunng, datum, " +
                    "id_zustaendig_behebung, fid_zustaendig_kontrolleur, auftrag_status, " +
                    "datum_auftrag_zugewiesen, datum_auftrag_angenommen, datum_auftrag_abgelehnt, " +
                    "bemerkung_auftrag, infomail_gesendet_am, infomail_empfaenger " +
                    "FROM \"wgr_sp_insp_mangel\" " +
                    "WHERE fid_spielgeraet=@playdeviceFid" +
                    " AND datum_erledigung IS NULL";
            selectDefectsCommand.Parameters.AddWithValue("playdeviceFid", playdeviceFid);
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
            selectDefectCommand.CommandText = "SELECT tid, fid_spielgeraet, id_dringlichkeit, beschrieb, " +
                    "datum_erledigung, fid_erledigung, bemerkunng, datum, " +
                    "id_zustaendig_behebung, fid_zustaendig_kontrolleur, auftrag_status, " +
                    "datum_auftrag_zugewiesen, datum_auftrag_angenommen, datum_auftrag_abgelehnt, " +
                    "bemerkung_auftrag, infomail_gesendet_am, infomail_empfaenger " +
                    "FROM \"wgr_sp_insp_mangel\" " +
                    "WHERE tid=@tid";
            selectDefectCommand.Parameters.AddWithValue("tid", tid);
            return selectDefectCommand;
        }

        private Defect _ReadDefect(NpgsqlDataReader reader)
        {
            Defect defect = new()
            {
                tid = reader.IsDBNull(0) ? -1 : reader.GetInt32(0)
            };
            int playdeviceFidOrdinal = reader.GetOrdinal("fid_spielgeraet");
            defect.playdeviceFid = reader.IsDBNull(playdeviceFidOrdinal) ? 0 : reader.GetInt32(playdeviceFidOrdinal);

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

            int responsibleUserFidOrdinal = reader.GetOrdinal("fid_zustaendig_kontrolleur");
            if (!reader.IsDBNull(responsibleUserFidOrdinal))
                defect.responsibleUserFid = reader.GetInt32(responsibleUserFidOrdinal);

            int assignmentStatusOrdinal = reader.GetOrdinal("auftrag_status");
            defect.assignmentStatus = reader.IsDBNull(assignmentStatusOrdinal) ? "" : reader.GetString(assignmentStatusOrdinal);

            int dateAssignmentCreatedOrdinal = reader.GetOrdinal("datum_auftrag_zugewiesen");
            if (!reader.IsDBNull(dateAssignmentCreatedOrdinal))
                defect.dateAssignmentCreated = reader.GetDateTime(dateAssignmentCreatedOrdinal);

            int dateAssignmentAcceptedOrdinal = reader.GetOrdinal("datum_auftrag_angenommen");
            if (!reader.IsDBNull(dateAssignmentAcceptedOrdinal))
                defect.dateAssignmentAccepted = reader.GetDateTime(dateAssignmentAcceptedOrdinal);

            int dateAssignmentRejectedOrdinal = reader.GetOrdinal("datum_auftrag_abgelehnt");
            if (!reader.IsDBNull(dateAssignmentRejectedOrdinal))
                defect.dateAssignmentRejected = reader.GetDateTime(dateAssignmentRejectedOrdinal);

            int assignmentCommentOrdinal = reader.GetOrdinal("bemerkung_auftrag");
            defect.assignmentComment = reader.IsDBNull(assignmentCommentOrdinal) ? "" : reader.GetString(assignmentCommentOrdinal);

            int infoMailSentAtOrdinal = reader.GetOrdinal("infomail_gesendet_am");
            if (!reader.IsDBNull(infoMailSentAtOrdinal))
                defect.infoMailSentAt = reader.GetDateTime(infoMailSentAtOrdinal);

            int infoMailRecipientNameOrdinal = reader.GetOrdinal("infomail_empfaenger");
            defect.infoMailRecipientName = reader.IsDBNull(infoMailRecipientNameOrdinal) ? "" : reader.GetString(infoMailRecipientNameOrdinal);

            return defect;
        }


        private static DbCommand CreateCommandForInsert(Defect defect,
                        NpgsqlConnection pgConn, User userFromDb)
        {
            NpgsqlCommand insertDefectCommand = pgConn.CreateCommand();
            insertDefectCommand.CommandText = "INSERT INTO \"wgr_sp_insp_mangel\" " +
                    "(tid, fid_spielgeraet, datum, id_dringlichkeit, beschrieb, bemerkunng, " +
                    "datum_erledigung, fid_erledigung, id_zustaendig_behebung, " +
                    "fid_zustaendig_kontrolleur, auftrag_status, datum_auftrag_zugewiesen, " +
                    "bemerkung_auftrag)" +
                    "VALUES (" +
                    "(SELECT CASE WHEN max(tid) IS NULL THEN 1 ELSE max(tid) + 1 END FROM \"wgr_sp_insp_mangel\"), " +
                    "@fid_spielgeraet, CURRENT_TIMESTAMP, @dringlichkeit, @beschrieb, " +
                    "@bemerkung, @datum_erledigung, @fid_erledigung, @id_zustaendig_behebung, " +
                    "@fid_zustaendig_kontrolleur, @auftrag_status, @datum_auftrag_zugewiesen, " +
                    "@bemerkung_auftrag) RETURNING tid";

            insertDefectCommand.Parameters.AddWithValue("fid_spielgeraet", defect.playdeviceFid);
            insertDefectCommand.Parameters.AddWithValue("dringlichkeit", defect.priority);
            insertDefectCommand.Parameters.AddWithValue("beschrieb", defect.defectDescription ?? "");
            insertDefectCommand.Parameters.AddWithValue("bemerkung", defect.defectComment ?? "");
            insertDefectCommand.Parameters.AddWithValue("id_zustaendig_behebung",
                            defect.defectsResponsibleBodyId > 0 ? defect.defectsResponsibleBodyId : DBNull.Value);
            insertDefectCommand.Parameters.AddWithValue("fid_zustaendig_kontrolleur",
                            defect.responsibleUserFid > 0 ? defect.responsibleUserFid : DBNull.Value);
            insertDefectCommand.Parameters.AddWithValue("auftrag_status",
                            defect.responsibleUserFid > 0 ? "zugewiesen" : DBNull.Value);
            insertDefectCommand.Parameters.AddWithValue("datum_auftrag_zugewiesen",
                            defect.responsibleUserFid > 0 ? DateTime.Now : DBNull.Value);
            insertDefectCommand.Parameters.AddWithValue("bemerkung_auftrag",
                            string.IsNullOrWhiteSpace(defect.assignmentComment) ? DBNull.Value : defect.assignmentComment.Trim());

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
            selectDefectsCommand.CommandText = @"SELECT tid
                                FROM ""wgr_sp_insp_mangel_foto""
                                WHERE tid_maengel=@defectTid AND zeitpunkt=@isFixed";
            selectDefectsCommand.Parameters.AddWithValue("defectTid", defectTid);
            selectDefectsCommand.Parameters.AddWithValue("isFixed", isFixed);

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
                    id_zustaendig_behebung = @id_zustaendig_behebung,
                    fid_zustaendig_kontrolleur = @fid_zustaendig_kontrolleur,
                    auftrag_status = @auftrag_status,
                    datum_auftrag_zugewiesen = @datum_auftrag_zugewiesen,
                    bemerkung_auftrag = @bemerkung_auftrag,
                    infomail_gesendet_am = CASE
                        WHEN fid_zustaendig_kontrolleur IS DISTINCT FROM @fid_zustaendig_kontrolleur THEN NULL
                        ELSE infomail_gesendet_am
                    END,
                    infomail_empfaenger = CASE
                        WHEN fid_zustaendig_kontrolleur IS DISTINCT FROM @fid_zustaendig_kontrolleur THEN NULL
                        ELSE infomail_empfaenger
                    END,
                    fid_erledigung = @fid_erledigung
                    WHERE tid = @tid";
            updateDefectCommand.Parameters.AddWithValue("tid", defect.tid);
            updateDefectCommand.Parameters.AddWithValue("id_dringlichkeit", defect.priority);
            updateDefectCommand.Parameters.AddWithValue("beschrieb", defect.defectDescription);
            updateDefectCommand.Parameters.AddWithValue("bemerkung", defect.defectComment);
            NpgsqlDate? dateDone = null;
            if(defect.dateDone != null) dateDone = (NpgsqlDate)defect.dateDone;
            updateDefectCommand.Parameters.AddWithValue("datum_erledigung", dateDone != null ? dateDone : DBNull.Value);
            updateDefectCommand.Parameters.AddWithValue("id_zustaendig_behebung",
                            defect.defectsResponsibleBodyId > 0 ? defect.defectsResponsibleBodyId : DBNull.Value);
            updateDefectCommand.Parameters.AddWithValue("fid_zustaendig_kontrolleur",
                            defect.responsibleUserFid > 0 ? defect.responsibleUserFid : DBNull.Value);
            updateDefectCommand.Parameters.AddWithValue("auftrag_status",
                            defect.responsibleUserFid > 0 ?
                                (string.IsNullOrWhiteSpace(defect.assignmentStatus) ? "zugewiesen" : defect.assignmentStatus) :
                                DBNull.Value);
            updateDefectCommand.Parameters.AddWithValue("datum_auftrag_zugewiesen",
                            defect.responsibleUserFid > 0 ?
                                (defect.dateAssignmentCreated != null ? defect.dateAssignmentCreated : DateTime.Now) :
                                DBNull.Value);
            updateDefectCommand.Parameters.AddWithValue("bemerkung_auftrag",
                            string.IsNullOrWhiteSpace(defect.assignmentComment) ? DBNull.Value : defect.assignmentComment.Trim());
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
