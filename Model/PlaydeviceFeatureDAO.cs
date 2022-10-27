// <copyright company="Vermessungsamt Winterthur">
//      Author: Edgar Butwilowski
//      Copyright (c) Vermessungsamt Winterthur. All rights reserved.
// </copyright>

using System.Data.Common;
using Npgsql;
using playground_check_service.Configuration;

namespace playground_check_service.Model
{
    public class PlaydeviceFeatureDAO
    {

        internal void Update(PlaydeviceFeature playdevice, User userFromDb, bool dryRun)
        {
            if (playdevice != null)
            {
                using (NpgsqlConnection pgConn = new NpgsqlConnection(AppConfig.connectionString))
                {
                    pgConn.Open();
                    DbCommand insertPlaydeviceCommand = this.CreateCommandForUpdate(playdevice, pgConn,
                            userFromDb, dryRun);
                    if(insertPlaydeviceCommand != null)
                    {
                        insertPlaydeviceCommand.ExecuteNonQuery();
                    }
                }
            }
        }

        private DbCommand CreateCommandForUpdate(PlaydeviceFeature playdevice, NpgsqlConnection pgConn,
                User userFromDb, bool dryRun)
        {
            if(dryRun) return null;

            NpgsqlCommand updatePlaydeviceCommand = pgConn.CreateCommand();
            updatePlaydeviceCommand.CommandText = "UPDATE \"gr_v_spielgeraete\" SET " +
                    "kostenschaetzung=@kostenschaetzung, " +
                    "empfohlenes_sanierungsjahr=@empfohlenes_sanierungsjahr, " +
                    "bemerkung_empf_sanierung=@bemerkung_empf_sanierung " +
                    "WHERE fid=@fid";
            updatePlaydeviceCommand.Parameters.AddWithValue("fid", playdevice.properties.fid);
            updatePlaydeviceCommand.Parameters.AddWithValue("kostenschaetzung",
                    playdevice.properties.costEstimation > 0 ? playdevice.properties.costEstimation : DBNull.Value);
            updatePlaydeviceCommand.Parameters.AddWithValue("empfohlenes_sanierungsjahr",
                    playdevice.properties.recommendedYearOfRenovation > 0 ?
                                playdevice.properties.recommendedYearOfRenovation : DBNull.Value);
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
            return updatePlaydeviceCommand;
        }

    }
}