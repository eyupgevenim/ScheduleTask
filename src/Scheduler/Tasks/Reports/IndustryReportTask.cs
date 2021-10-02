using Scheduler.Abstraction;
using System.Threading;

namespace Scheduler.Tasks.Reports
{
    public class IndustryReportTask : ITask
    {
        public void Execute()
        {
            //TODO:....
            Thread.Sleep(5000);
        }
    }
}
