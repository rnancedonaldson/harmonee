namespace Harmonee.Resources;

public static class Constants
{
    public static class Infrastructure
    {
        public static class Cache
        {
            public const string ContainerName = "HarmoneeCache";
        }

        public static class Database
        {
            public const string ServerName = "Harmonee";
            public const string ContainerName = "HarmoneeDatabase";
        }
    }

    public static class Services
    {
        public static class Auth
        {
            public const string DatabaseName = "Auth";
            public const string ContainerName = "HarmoneeAuthService";
        }

        public static class Family
        {
            public const string DatabaseName = "Family";
            public const string ContainerName = "HarmoneeFamilyService";
        }

        public static class Kitchen
        {
            public const string DatabaseName = "Kitchen";
            public const string ContainerName = "HarmoneeKitchenService";
        }

        public static class Presentation
        {
            public const string ContainerName = "HarmoneeUI";
        }

        public static class Schedule
        {
            public const string DatabaseName = "Schedule";
            public const string ContainerName = "HarmoneeScheduleService";
        }

        public static class Migration
        {
            public const string ContainerName = "Migration";
        }
    }
}
