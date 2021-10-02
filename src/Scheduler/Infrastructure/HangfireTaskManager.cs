using Hangfire;
using Hangfire.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Scheduler.Abstraction;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Scheduler.Infrastructure
{
    public class HangfireTaskManager : ITaskManager
    {
        private readonly RecurringJobManager recurringJobManager = new RecurringJobManager();
        private readonly ILogger<HangfireTaskManager> logger;
        private readonly List<ScheduleTaskOptions> scheduleTasks;
        public HangfireTaskManager(ILogger<HangfireTaskManager> logger, IOptions<List<ScheduleTaskOptions>> scheduleTasksOption)
        {
            this.logger = logger;
            scheduleTasks = scheduleTasksOption.Value;
        }

        public void Initialize()
        {
            if (scheduleTasks == null)
                return;

            if (!scheduleTasks.Any())
                return;

            foreach (var taskOptions in scheduleTasks)
            {
                LoadTask(taskOptions);
            }
        }

        private void LoadTask(ScheduleTaskOptions taskOptions)
        {
            try
            {
                var type = Type.GetType(taskOptions.Type) 
                    ?? AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(taskOptions.Type)).FirstOrDefault(t => t != null);

                if (type == null)
                {
                    logger.LogError($"not found type -> Task Options Type:{taskOptions.Type} - Name:{taskOptions.Name}");
                    return;
                }

                var instance = Activator.CreateInstance(type);
                var task = instance as ITask;
                if (task == null || !taskOptions.Enabled)
                {
                    recurringJobManager.RemoveIfExists($"{taskOptions.Type}");
                    logger.LogError($"not found ITask type -> Task Options Type:{taskOptions.Type} - Name:{taskOptions.Name}");
                    return;
                }

                var job = new Job(type, type.GetMethod(nameof(ITask.Execute)));
                recurringJobManager.AddOrUpdate($"{taskOptions.Type}", job, taskOptions.CronExpression);
            }
            catch (Exception ex)
            {
                logger.LogError(ex.Message, ex);
            }
        }

    }
}
