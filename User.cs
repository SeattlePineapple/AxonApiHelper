using System;
using System.Collections.Generic;
using System.Text.Json;

namespace AxonApiHelper
{
    public class User
    {
        public Guid ID;
        public string UserName;
        public string FirstName;
        public string LastName;
        public string Email;
        public string BadgeNumber;
        public string PhoneNumber;
        public string ExternalID;

        public User() { }

        public void Enumerate()
        {
            User copy = JsonHelper.Parse<User>(Endpoint.Get($"users/{ID}", 4), out int _)[0];
            ID = copy.ID;
            UserName = copy.UserName;
            FirstName = copy.FirstName;
            LastName = copy.LastName;
            Email = copy.Email;
            BadgeNumber = copy.BadgeNumber;
            PhoneNumber = copy.PhoneNumber;
            ExternalID = copy.ExternalID;
        }

        public override string ToString() => LastName != null ? $"{LastName}, {FirstName} ({BadgeNumber})" : ID.ToString();

        public static User FromJsonElement(JsonElement je)
        {
            User user = new();
            user.ID = je.TryGetProperty("id", typeof(Guid));
            if (je.TryGetProperty("attributes", out JsonElement att))
            {
                user.UserName = att.TryGetProperty("username", typeof(string));
                user.FirstName = att.TryGetProperty("firstName", typeof(string));
                user.LastName = att.TryGetProperty("lastName", typeof(string));
                user.Email = att.TryGetProperty("email", typeof(string));
                user.BadgeNumber = att.TryGetProperty("badgeNumber", typeof(string));
                user.PhoneNumber = att.TryGetProperty("phoneNumber", typeof(string));
                user.ExternalID = att.TryGetProperty("externalId", typeof(string));
            }
            return user;
        }

        public static List<User> GetAllUsers()
        {
            return JsonHelper.Parse<User>(Endpoint.Get($"users", 4), out int _);
        }
    }
}
