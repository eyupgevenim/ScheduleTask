using Scheduler.Abstraction;
using System.Threading;

namespace Scheduler.Tasks.Email
{
    public class MailSenderTask : ITask
    {
        public void Execute()
        {
            //TODO:....
            Thread.Sleep(500);
        }
    }
}
