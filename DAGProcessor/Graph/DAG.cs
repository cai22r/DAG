using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;
using System.Collections.ObjectModel;

namespace DAGProcessor.Graph
{
    /// <summary>
    /// Class that encapulates the graph structure fore the request
    /// It handles notify the request result and add/remove nodes in the graph onto the machine.
    /// </summary>
    public class DAG
    {
        private readonly TaskCompletionSource<RequestResponse> taskSubscriberSource = new TaskCompletionSource<RequestResponse>();
        private ConcurrentDictionary<IDagNode, Node.NodeState> nodeStateMap = new ConcurrentDictionary<IDagNode, Node.NodeState>();
        private ReadOnlyDictionary<Node, List<Node>> reverseDependencyMap;

        private TaskQueue taskQueue;
        private long unfinishedNodeCounter;
        // 1 is marked as request aborted.
        private long aborted = 0;
        // number of retry per node.
        private int retryPolicy;

        public int Id { get; set; }

        [XmlArray("Nodes")]
        [XmlArrayItem(ElementName = "Node")]
        public List<Node> nodes { get; set; }

        public Task<RequestResponse> subscribeToGraph()
        {
            return taskSubscriberSource.Task;
        }

        // Initial set up once the DAG get assigned a task queue for executing the nodes.
        public void setTaskQueue(TaskQueue sharedQueue, int retry)
        {
            scrubDag();
            bool valid = DagValidator.isValidDag(this);
            if(!valid)
            {
                Console.WriteLine("{0} is invalid", Id);
                taskSubscriberSource.SetResult(new RequestResponse(-1));
                return;
            }

            taskQueue = sharedQueue;

            Dictionary<Node, List<Node>> map = null;
            initNodes(out map, retry);
            reverseDependencyMap = new ReadOnlyDictionary<Node, List<Node>>(map);
            retryPolicy = retry;
            // Find the initial sets of node to run.
            Node runningNode = null;
            foreach (var node in nodes)
            {
                Node.NodeState currentState = Node.NodeState.Waiting;

                if (node.dependencyList.Count == 0 && nodeStateMap.TryGetValue(node, out currentState))
                {
                    if (currentState == Node.NodeState.Waiting)
                    {
                        runningNode = node;
                        kickOffNodeRun(runningNode);
                    }
                }
            }
            
        }

        // Add the node to taskqueue and attach the node complete call back.
        private void kickOffNodeRun(Node runningNode)
        {
            bool isAborted = Interlocked.Read(ref aborted) == 1;
         
            if (!isAborted && runningNode != null)
            {
                if (nodeStateMap.TryUpdate(runningNode, Node.NodeState.Queueing, Node.NodeState.Waiting))
                {
                    Task<int> nodeListener = taskQueue.AddNode(runningNode);
                    Console.WriteLine("{0}:{1} added to task queue", Id, runningNode.Id);
                    try
                    {
                        nodeListener.ContinueWith(t =>
                        {
                            int result = t.Result;
                            nodeCompleted(runningNode, result);
                        }, TaskContinuationOptions.OnlyOnRanToCompletion);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.Message);
                    }
                }
            }
        }

        // Abort the request if we get a failure in execution.
        private void abortRequest()
        {
            //Only abort once.
            if(Interlocked.Read(ref aborted) == 0)
            {
                Interlocked.Exchange(ref aborted, 1);
                Console.WriteLine("DAG {0} aborted", Id);

                foreach (var node in nodes)
                {
                    Node.NodeState currentState = Node.NodeState.Waiting;

                    if (nodeStateMap.TryGetValue(node, out currentState))
                    {
                        // If we already put a node in the task queue.
                        if (currentState == Node.NodeState.Queueing)
                        {
                            if (taskQueue.RemoveNode(node))
                            {
                                nodeStateMap.TryUpdate(node, Node.NodeState.Cancelled, Node.NodeState.Queueing);
                                Console.WriteLine("{0}:{1} removed from the queue", Id, node.Id);
                            }
                        }
                        // If the node is has not been queue, just set it to cancel.
                        else if (currentState == Node.NodeState.Waiting)
                        {
                            nodeStateMap.TryUpdate(node, Node.NodeState.Cancelled, Node.NodeState.Waiting);
                        }
                    }
                }

                //Notify the subscribers here or wait till the last node running on the machine is done???
                taskSubscriberSource.SetResult(new RequestResponse(-1));
            }
        }

        // Helper method to update mappings once the node work is completed.
        private void nodeCompleted(Node node, int result)
        {
            Console.WriteLine("{0}:{1} result {2}", Id, node.Id, result);

            if (result != 0)
            {
                if(node.retryCounter == 0)
                {
                    //Update the node state to complete
                    nodeStateMap.TryUpdate(node, Node.NodeState.Completed, Node.NodeState.Queueing);
                    abortRequest();
                    return;
                }
                else
                {
                    node.retryCounter = node.retryCounter - 1;
                    //Update the node state to complete
                    nodeStateMap.TryUpdate(node, Node.NodeState.Waiting, Node.NodeState.Queueing);

                    Console.WriteLine("{0}:{1} Retry: {2}", Id, node.Id, retryPolicy - node.retryCounter);
                    kickOffNodeRun(node);
                    return;
                }
            }

            //Update and check the indegree number of nodes which depends on its completion to kick off;
            Interlocked.Decrement(ref unfinishedNodeCounter);
            if (Interlocked.Read(ref unfinishedNodeCounter) == 0)
            {
                taskSubscriberSource.SetResult(new RequestResponse(0));
            }
            else
            {
                List<Node> childNodes;
                if (reverseDependencyMap.TryGetValue(node, out childNodes))
                {
                    foreach(var child in childNodes)
                    {
                        child.DecrementDependencyCounter();
                        if(child.isReadyToQueue())
                        {
                            kickOffNodeRun(child);
                        }
                    }
                }
            }
        }

        // Helper method to update the dependency list in Node to use same object.
        private void scrubDag()
        {
            foreach (var node in nodes)
            {
                List<Node> mappedNode = new List<Node>();
                foreach (var dependency in node.dependencyList)
                {
                    Node resultNode = null;
                    if (getNode(dependency.Id, out resultNode))
                    {
                        mappedNode.Add(resultNode);
                    }
                }

                node.dependencyList = mappedNode;
            }
        }

        // Helper method.
        private bool getNode(int id, out Node resultNode)
        {
            foreach (var node in nodes)
            {
                if (node.Id == id)
                {
                    resultNode = node;
                    return true;
                }
            }

            resultNode = null;
            return false;
        }

        // Initialize the mappings the class to track node, and assign the retry policy.
        private void initNodes(out Dictionary<Node, List<Node>> map, int retry)
        {
            Interlocked.Exchange(ref unfinishedNodeCounter, nodes.Count);
            map = new Dictionary<Node, List<Node>>();
            foreach (var node in nodes)
            {
                nodeStateMap.TryAdd(node, Node.NodeState.Waiting);
                map.Add(node, new List<Node>());
                node.retryCounter = retry;
            }

            foreach (var node in nodes)
            {
                node.SetDependencyCounter();
                foreach (var dependent in node.dependencyList)
                {
                    List<Node> list;
                    if (map.TryGetValue(dependent, out list))
                    {
                        list.Add(node);
                    }
                }
            }
        }

    }
}
