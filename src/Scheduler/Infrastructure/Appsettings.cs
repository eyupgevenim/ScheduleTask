namespace Scheduler.Infrastructure
{
    using System.Collections.Generic;

    public class Appsettings
    {
        public ConnectionStringsOptions ConnectionStrings { get; set; }
        public List<ScheduleTaskOptions> ScheduleTasks { get; set; }
    }

    public class ConnectionStringsOptions
    {
        public string DefaultConnection { get; set; }
        public string HangfireConnection { get ; set; }

        public string ConnectionString
        {
            get
            {
                if (!string.IsNullOrEmpty(HangfireConnection))
                    return HangfireConnection;
                return DefaultConnection;
            }
        }
    }

    public class ScheduleTaskOptions
    {
        public string Type { get; set; }
        public string Name { get; set; }
        public string CronExpression { get; set; }
        public bool Enabled { get; set; }
    }

}
