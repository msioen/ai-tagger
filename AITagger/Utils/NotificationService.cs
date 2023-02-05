using System;
using Foundation;
using UserNotifications;

namespace AITagger.Utils
{
    public class NotificationService : NSObject, INSUserNotificationCenterDelegate
    {
        const string IDENTIFIER = "AITagger";

        static NotificationService _instance;

        #region Properties

        public static NotificationService Instance
        {
            get
            {
                _instance = _instance ?? new NotificationService();
                return _instance;
            }
        }

        #endregion

        #region Constructors

        NotificationService()
        {
            NSUserNotificationCenter.DefaultUserNotificationCenter.ShouldPresentNotification = DefaultUserNotificationCenter_ShouldPresentNotification;
        }

        #endregion

        #region Public

        public void ShowNotification(string title, string text)
        {
            ShowNotificationWithNSUserNotifications(title, text);
        }

        #endregion

        #region Private

        private void ShowNotificationWithNSUserNotifications(string title, string text)
        {
            var notification = new NSUserNotification();
            notification.Title = title;
            notification.InformativeText = text;

            NSUserNotificationCenter.DefaultUserNotificationCenter.DeliverNotification(notification);
        }

        private static bool DefaultUserNotificationCenter_ShouldPresentNotification(NSUserNotificationCenter center, NSUserNotification notification)
        {
            return true;
        }

        #endregion
    }
}

