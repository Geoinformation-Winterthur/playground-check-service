// <copyright company="Vermessungsamt Winterthur">
//      Author: Edgar Butwilowski
//      Copyright (c) Vermessungsamt Winterthur. All rights reserved.
// </copyright>

using System.Data;
using System.Data.Common;
using System.Text;
using Npgsql;
using playground_check_service.Configuration;

namespace playground_check_service.Model
{
    public class PlaydeviceFeatureDAO
    {

        internal bool HasPlaydeviceToBeChecked(PlaydeviceFeature playdevice)
        {
            bool hasPlaydeviceToBeChecked = true;
            if (playdevice != null)
            {
                using (NpgsqlConnection pgConn = new NpgsqlConnection(AppConfig.connectionString))
                {
                    pgConn.Open();
                    NpgsqlCommand selectIfExisting = pgConn.CreateCommand();
                    selectIfExisting.CommandText = @"SELECT nicht_zu_pruefen
                                    FROM ""gr_v_spielgeraete"" 
                                    WHERE fid=@fid";
                    selectIfExisting.Parameters.AddWithValue("fid", playdevice.properties.fid);

                    using (NpgsqlDataReader reader = selectIfExisting.ExecuteReader())
                    {
                        reader.Read();
                        hasPlaydeviceToBeChecked = reader.IsDBNull(0) || !reader.GetBoolean(0);
                    }
                }
            }
            return hasPlaydeviceToBeChecked;
        }

        internal void Update(PlaydeviceFeature playdevice, bool dryRun)
        {
            if (playdevice != null)
            {
                using (NpgsqlConnection pgConn = new NpgsqlConnection(AppConfig.connectionString))
                {
                    pgConn.Open();
                    DbCommand insertPlaydeviceCommand = this.CreateCommandForUpdate(playdevice,
                            pgConn, dryRun);
                    if (insertPlaydeviceCommand != null)
                    {
                        insertPlaydeviceCommand.ExecuteNonQuery();
                    }
                }
            }
        }

        internal void UpdatePicture(int playdeviceFid, string picture, bool dryRun)
        {
            byte[] pictureBytes = Encoding.ASCII.GetBytes(picture);
            using (NpgsqlConnection pgConn = new NpgsqlConnection(AppConfig.connectionString))
            {
                pgConn.Open();
                DbCommand insertPlaydeviceCommand = this.CreateCommandForPictureUpdate(playdeviceFid, pictureBytes,
                        pgConn, dryRun);
                if (insertPlaydeviceCommand != null)
                {
                    insertPlaydeviceCommand.ExecuteNonQuery();
                }
            }
        }

        private DbCommand CreateCommandForUpdate(PlaydeviceFeature playdevice,
                NpgsqlConnection pgConn, bool dryRun)
        {
            if (dryRun) return null;

            NpgsqlCommand updatePlaydeviceCommand = pgConn.CreateCommand();
            updatePlaydeviceCommand.CommandText = "UPDATE \"gr_v_spielgeraete\" SET " +
                    "empfohlenes_sanierungsjahr=@empfohlenes_sanierungsjahr, " +
                    "id_sanierungsart=@id_sanierungsart, " +
                    "bemerkung_empf_sanierung=@bemerkung_empf_sanierung, " +
                    "nicht_pruefbar=@nicht_pruefbar, " +
                    "grund_nicht_pruefbar=@grund_nicht_pruefbar " +
                    "WHERE fid=@fid";
            updatePlaydeviceCommand.Parameters.AddWithValue("fid", playdevice.properties.fid);
            updatePlaydeviceCommand.Parameters.AddWithValue("empfohlenes_sanierungsjahr",
                    playdevice.properties.recommendedYearOfRenovation > 0 ?
                                playdevice.properties.recommendedYearOfRenovation : DBNull.Value);

            if (playdevice.properties.renovationType == "Totalsanierung") {
                updatePlaydeviceCommand.Parameters.AddWithValue("id_sanierungsart", 1);
            } else if (playdevice.properties.renovationType == "Teilsanierung") {
                updatePlaydeviceCommand.Parameters.AddWithValue("id_sanierungsart", 2);
            } else {
                updatePlaydeviceCommand.Parameters.AddWithValue("id_sanierungsart", DBNull.Value);
            }

            if (playdevice.properties.commentRecommendedYearOfRenovation != null &&
                        playdevice.properties.commentRecommendedYearOfRenovation.Length != 0)
            {
                updatePlaydeviceCommand.Parameters.AddWithValue("bemerkung_empf_sanierung",
                        playdevice.properties.commentRecommendedYearOfRenovation);
            }
            else
            {
                updatePlaydeviceCommand.Parameters.AddWithValue("bemerkung_empf_sanierung",
                        DBNull.Value);
            }
            updatePlaydeviceCommand.Parameters.AddWithValue("nicht_pruefbar", playdevice.properties.cannotBeChecked);
            updatePlaydeviceCommand.Parameters.AddWithValue("grund_nicht_pruefbar", playdevice.properties.cannotBeCheckedReason);
            return updatePlaydeviceCommand;
        }

        private DbCommand CreateCommandForPictureUpdate(int playdeviceFid, byte[] pictureBytes,
                NpgsqlConnection pgConn, bool dryRun)
        {
            if (dryRun) return null;
            NpgsqlCommand updatePlaydeviceCommand = pgConn.CreateCommand();
            updatePlaydeviceCommand.CommandText = "UPDATE \"gr_v_spielgeraete\" SET " +
                    "picture_base64=@picture " +
                    "WHERE fid=@fid";
            updatePlaydeviceCommand.Parameters.AddWithValue("picture", pictureBytes);
            updatePlaydeviceCommand.Parameters.AddWithValue("fid", playdeviceFid);
            return updatePlaydeviceCommand;
        }

    }
}