using Scheduler.Abstraction;
using System;
using System.Threading;

namespace Scheduler.Tasks.Commission
{
    public class CommissionTask : ITask
    {
        public void Execute()
        {
            //TODO:....
            for (int i = 0; i < 100_000; i++)
            {
                var x = Math.Pow(i, 2) / 2;
                x *= 0.001;

                if (DateTime.Now.Minute == 30)
                {
                    throw new InvalidOperationException($"{DateTime.Now}");
                }

                Thread.Sleep(100);
            }
        }
    }
}
