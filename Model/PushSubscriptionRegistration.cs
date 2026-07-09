// <copyright company="Geoinformation Winterthur">
//      Author: Edgar Butwilowski
//      Copyright (c) Geoinformation Winterthur. All rights reserved.
// </copyright>
namespace playground_check_service.Model;

public class PushSubscriptionRegistration
{
    public string endpoint { get; set; } = "";
    public string p256dh { get; set; } = "";
    public string auth { get; set; } = "";
    public string userAgent { get; set; } = "";
}
