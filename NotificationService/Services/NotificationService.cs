/*
 * Copyright (C) 2024 Stanislav Motsnyi
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program. If not, see <https://www.gnu.org/licenses/>.
 */
using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Shared.Logging;
using LogLevel = Shared.Logging.LogLevel;

namespace NotificationService.Services;

public class NotificationService
{
    private const string recording_was_confirmed_title = "Recording was confirmed";
    private const string recording_was_confirmed_body = "Your recording was confirmed by the admin";
    
    public async Task SendRecordingWasConfirmedNotificationAsync(string deviceToken)
    {
        string messageId = await SendNotificationAsync(deviceToken, recording_was_confirmed_title, recording_was_confirmed_body);
    }
    
    private async Task<string> SendNotificationAsync(string deviceToken, string title, string body)
    {
        
        var message = new Message
        {
            Token = deviceToken,
            Notification = new Notification
            {
                Title = title,
                Body = body
            }
        };

        try
        {
            return await FirebaseMessaging.DefaultInstance.SendAsync(message);
        }
        catch (FirebaseMessagingException e)
        {
            Logger.Log("Error sending message: " + e.Message, LogLevel.Error);
            return string.Empty;
        }
    }
}