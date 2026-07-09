// <copyright company="Geoinformation Winterthur">
//      Author: Edgar Butwilowski
//      Copyright (c) Geoinformation Winterthur. All rights reserved.
// </copyright>
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using playground_check_service.Model;
using WebPush;

namespace playground_check_service.Services;

public class PushNotificationService
{
    private readonly ILogger<PushNotificationService> _logger;
    private readonly IConfiguration _configuration;

    public PushNotificationService(ILogger<PushNotificationService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    public async Task SendDefectAssignedNotificationAsync(Defect defect, User assignedUser)
    {
        if (defect == null || assignedUser == null || assignedUser.fid <= 0) return;

        string? publicKey = _configuration.GetValue<string>("PushNotifications:VapidPublicKey");
        string? privateKey = _configuration.GetValue<string>("PushNotifications:VapidPrivateKey");
        string? subject = _configuration.GetValue<string>("PushNotifications:VapidSubject");

        if (string.IsNullOrWhiteSpace(publicKey) || string.IsNullOrWhiteSpace(privateKey))
        {
            _logger.LogWarning("Push notification not sent because VAPID keys are missing.");
            return;
        }
        if (string.IsNullOrWhiteSpace(subject)) subject = "mailto:geoinformation@win.ch";

        PushSubscriptionDAO subscriptionDao = new();
        PushSubscriptionRegistration[] subscriptions = subscriptionDao.ReadActiveSubscriptionsOfUser(assignedUser.fid);
        if (subscriptions.Length == 0)
        {
            _logger.LogInformation("No active push subscription found for assigned user {UserFid}.", assignedUser.fid);
            return;
        }

        string appBaseUrl = _configuration.GetValue<string>("PushNotifications:AppBaseUrl")
            ?? "/stadtgruen/spielplatzkontrolle";
        appBaseUrl = appBaseUrl.TrimEnd('/');

        string defectUrl = $"{appBaseUrl}/defect/{defect.playdeviceFid}/{defect.tid}";
        string notificationBody = string.IsNullOrWhiteSpace(defect.defectDescription)
            ? "Dir wurde ein Mangel zur Behebung zugewiesen."
            : defect.defectDescription;

        // Angular Service Worker expects the Web Push payload below the
        // "notification" property. Without this wrapper, Android/Chrome shows
        // only the generic message "Diese Website wurde im Hintergrund aktualisiert".
        string payload = JsonSerializer.Serialize(new
        {
            notification = new
            {
                title = "Neuer Mangel zugewiesen",
                body = notificationBody,
                icon = $"{appBaseUrl}/assets/win_logo.png",
                badge = $"{appBaseUrl}/assets/win_logo.png",
                data = new
                {
                    url = defectUrl,
                    defectTid = defect.tid,
                    playdeviceFid = defect.playdeviceFid,
                    onActionClick = new
                    {
                        @default = new
                        {
                            operation = "navigateLastFocusedOrOpen",
                            url = defectUrl
                        }
                    }
                }
            }
        });

        VapidDetails vapidDetails = new(subject, publicKey, privateKey);
        WebPushClient client = new();

        foreach (PushSubscriptionRegistration subscription in subscriptions)
        {
            try
            {
                PushSubscription webPushSubscription = new(subscription.endpoint, subscription.p256dh, subscription.auth);
                await client.SendNotificationAsync(webPushSubscription, payload, vapidDetails);
            }
            catch (WebPushException ex) when (ex.StatusCode == HttpStatusCode.Gone || ex.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogWarning(ex, "Push subscription is no longer valid and will be deactivated.");
                subscriptionDao.DeactivateEndpoint(subscription.endpoint);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not send push notification for defect {DefectTid}.", defect.tid);
            }
        }
    }
}
