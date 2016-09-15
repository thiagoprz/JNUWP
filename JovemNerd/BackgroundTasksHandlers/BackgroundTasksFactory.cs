using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.Background;

namespace JovemNerd.BackgroundTasksHandlers
{
    public class BackgroundTasksFactory
    {
        public static BackgroundTaskRegistration RegisterBackgroundTask(string taskEntryPoint, string name, IBackgroundTrigger trigger, IBackgroundCondition condition)
        {
            foreach (var cur in BackgroundTaskRegistration.AllTasks)
            {

                if (cur.Value.Name == name)
                    // The task is already registered.
                    return (BackgroundTaskRegistration)(cur.Value);
            }

            //Register new background task:
            var builder = new BackgroundTaskBuilder();

            builder.Name = name;
            builder.TaskEntryPoint = taskEntryPoint;
            builder.SetTrigger(trigger);

            if (condition != null)
            {
                builder.AddCondition(condition);
            }

            BackgroundTaskRegistration task = builder.Register();

            return task;
        }
    }
}
