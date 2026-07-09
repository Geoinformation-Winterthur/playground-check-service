// <copyright company="Geoinformation Winterthur">
//      Author: Edgar Butwilowski
//      Copyright (c) Geoinformation Winterthur. All rights reserved.
// </copyright>
using Npgsql;
using playground_check_service.Configuration;

namespace playground_check_service.Model;

public class PushSubscriptionDAO
{
    internal void Register(User userFromDb, PushSubscriptionRegistration subscription, bool dryRun = false)
    {
        if (dryRun) return;
        if (userFromDb == null || userFromDb.fid <= 0) throw new ArgumentException("User is missing.");
        if (subscription == null || string.IsNullOrWhiteSpace(subscription.endpoint) ||
            string.IsNullOrWhiteSpace(subscription.p256dh) || string.IsNullOrWhiteSpace(subscription.auth))
            throw new ArgumentException("Push subscription is incomplete.");

        using NpgsqlConnection pgConn = new(AppConfig.connectionString);
        pgConn.Open();
        using NpgsqlCommand command = pgConn.CreateCommand();
        command.CommandText = @"INSERT INTO ""wgr_sp_push_subscription""
                    (fid_kontrolleur, endpoint, p256dh, auth, user_agent, aktiv,
                     datum_registrierung, datum_letzte_verwendung, datum_deaktivierung)
                VALUES (@fid_kontrolleur, @endpoint, @p256dh, @auth, @user_agent, true,
                     CURRENT_TIMESTAMP, NULL, NULL)
                ON CONFLICT (endpoint)
                DO UPDATE SET fid_kontrolleur = EXCLUDED.fid_kontrolleur,
                    p256dh = EXCLUDED.p256dh,
                    auth = EXCLUDED.auth,
                    user_agent = EXCLUDED.user_agent,
                    aktiv = true,
                    datum_letzte_verwendung = CURRENT_TIMESTAMP,
                    datum_deaktivierung = NULL";
        command.Parameters.AddWithValue("fid_kontrolleur", userFromDb.fid);
        command.Parameters.AddWithValue("endpoint", subscription.endpoint.Trim());
        command.Parameters.AddWithValue("p256dh", subscription.p256dh.Trim());
        command.Parameters.AddWithValue("auth", subscription.auth.Trim());
        command.Parameters.AddWithValue("user_agent", string.IsNullOrWhiteSpace(subscription.userAgent) ? DBNull.Value : subscription.userAgent.Trim());
        command.ExecuteNonQuery();
    }

    internal void Unregister(User userFromDb, string endpoint, bool dryRun = false)
    {
        if (dryRun) return;
        if (userFromDb == null || userFromDb.fid <= 0) throw new ArgumentException("User is missing.");
        if (string.IsNullOrWhiteSpace(endpoint)) throw new ArgumentException("Endpoint is missing.");

        using NpgsqlConnection pgConn = new(AppConfig.connectionString);
        pgConn.Open();
        using NpgsqlCommand command = pgConn.CreateCommand();
        command.CommandText = @"UPDATE ""wgr_sp_push_subscription""
                    SET aktiv = false, datum_deaktivierung = CURRENT_TIMESTAMP
                    WHERE fid_kontrolleur = @fid_kontrolleur AND endpoint = @endpoint";
        command.Parameters.AddWithValue("fid_kontrolleur", userFromDb.fid);
        command.Parameters.AddWithValue("endpoint", endpoint.Trim());
        command.ExecuteNonQuery();
    }

    internal PushSubscriptionRegistration[] ReadActiveSubscriptionsOfUser(int userFid)
    {
        List<PushSubscriptionRegistration> result = new();
        using NpgsqlConnection pgConn = new(AppConfig.connectionString);
        pgConn.Open();
        using NpgsqlCommand command = pgConn.CreateCommand();
        command.CommandText = @"SELECT endpoint, p256dh, auth, COALESCE(user_agent, '')
                    FROM ""wgr_sp_push_subscription""
                    WHERE fid_kontrolleur = @fid_kontrolleur AND aktiv = true";
        command.Parameters.AddWithValue("fid_kontrolleur", userFid);
        using NpgsqlDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new PushSubscriptionRegistration
            {
                endpoint = reader.IsDBNull(0) ? "" : reader.GetString(0),
                p256dh = reader.IsDBNull(1) ? "" : reader.GetString(1),
                auth = reader.IsDBNull(2) ? "" : reader.GetString(2),
                userAgent = reader.IsDBNull(3) ? "" : reader.GetString(3)
            });
        }
        return result.ToArray();
    }

    internal void DeactivateEndpoint(string endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint)) return;
        using NpgsqlConnection pgConn = new(AppConfig.connectionString);
        pgConn.Open();
        using NpgsqlCommand command = pgConn.CreateCommand();
        command.CommandText = @"UPDATE ""wgr_sp_push_subscription""
                    SET aktiv = false, datum_deaktivierung = CURRENT_TIMESTAMP
                    WHERE endpoint = @endpoint";
        command.Parameters.AddWithValue("endpoint", endpoint.Trim());
        command.ExecuteNonQuery();
    }
}
