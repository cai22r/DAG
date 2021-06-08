using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DAGProcessor
{
    public class MockExecutionMachine : IDagNodeExecutor
    {
        public int MachineId { get; private set; }
        private readonly int errorChance;
        private int errorCounter = 0;

        public MockExecutionMachine(int id, int oneperN)
        {
            MachineId = id;
            errorChance = oneperN;
        }

        // Mock the execution with a failure rate and randmoized task execution time.
        public Task<int> ExecuteAsync(IDagNode unitOfExecution)
        {
            errorCounter++;
            int result = errorCounter == errorChance ? -1  : 0;
            if(result == -1)
            {
                Interlocked.Exchange(ref errorCounter, 0);
            }

            var fakeTask = Task.Run(async () =>
            {
                var rand = new Random();
                
                await Task.Delay(rand.Next(3000));
                //Console.WriteLine("Machine {0} finished running Node", MachineId);
                return result;

            });

            return fakeTask;
        }
    }
}
