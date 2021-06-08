using DAGProcessor.Graph;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace DAGProcessor
{

    public class TaskQueue
    {
        private ConcurrentQueue<MockExecutionMachine> machinePool = new ConcurrentQueue<MockExecutionMachine>();
        private ConcurrentDictionary<IDagNode, TaskCompletionSource<int>> pendingNodes;
        private ConcurrentQueue<IDagNode> taskQueue;
        private ConcurrentBag<IDagNode> runningNodes;
        private readonly int sizeOfMachinePool;

        public TaskQueue(int size, int onePerError)
        {
            sizeOfMachinePool = size;
            initMachines(onePerError);
            pendingNodes = new ConcurrentDictionary<IDagNode, TaskCompletionSource<int>>();
            taskQueue = new ConcurrentQueue<IDagNode>();
            runningNodes = new ConcurrentBag<IDagNode>();
        }

        private void initMachines(int onePerError)
        {
            for (int i = 0; i < sizeOfMachinePool; i++)
            {
                machinePool.Enqueue(new MockExecutionMachine(i, onePerError));
            }
        }

        // Add a node to queue.
        public Task<int> AddNode(IDagNode node)
        {
            TaskCompletionSource<int> nodeSubscriber = new TaskCompletionSource<int>();
            try
            {
                if(pendingNodes.TryAdd(node, nodeSubscriber))
                {
                    taskQueue.Enqueue(node);
                    if (machinePool.Count > 0)
                    {
                        executeNextTask();
                    }
                }
            }
            catch(Exception e)
            {
                Console.WriteLine(e.Message);
            }

            return nodeSubscriber.Task;
        }

        // Remove a node by removing it from the pendingNodes.
        // If the node is already running on machine, return false.
        public bool RemoveNode(IDagNode node)
        {
            TaskCompletionSource<int> nodeSubscriber;
            try
            {
                if (pendingNodes.TryRemove(node, out nodeSubscriber))
                {
                    nodeSubscriber.SetCanceled();
                    return true;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }

            return false;
        }


        private void executeNextTask()
        {
            while (machinePool.Count > 0 && taskQueue.Count > 0)
            {
                MockExecutionMachine machine;
                if (machinePool.TryDequeue(out machine))
                {
                    IDagNode workingNode;
                    TaskCompletionSource<int> nodeSubscriber;
                    if (taskQueue.TryDequeue(out workingNode) && pendingNodes.TryRemove(workingNode, out nodeSubscriber))
                    {
                        runningNodes.Add(workingNode);
                        var result = machine.ExecuteAsync(workingNode);
                        try
                        {
                            result.ContinueWith(t =>
                            {
                                try
                                {
                                    nodeSubscriber.SetResult(t.Result);
                                    runningNodes.TryTake(out workingNode);
                                    machinePool.Enqueue(machine);
                                    executeNextTask();
                                }
                                catch (Exception e)
                                {
                                    machinePool.Enqueue(machine);
                                    executeNextTask();
                                    Console.WriteLine(e.Message);
                                }
                            });

                        }
                        catch (Exception e)
                        {
                            machinePool.Enqueue(machine);
                            executeNextTask();
                            Console.WriteLine(e.Message);
                        }
                    }
                    else
                    {
                        machinePool.Enqueue(machine);
                    }
                }
            }
        }
    }
}
