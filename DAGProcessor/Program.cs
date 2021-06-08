using System;
using System.Threading;
using System.Threading.Tasks;
using DAGProcessor.Graph;

namespace DAGProcessor
{
    class Program
    {
        static RequestProcessor processor;
        static long requestCompleted = 0;
        static long sentRequests = 0;
        static long succeedRequests = 0;
        static long failedRequests = 0;

        static void Main(string[] args)
        {
            Console.WriteLine("NEW TEST RUN");
            if(args.Length == 0)
            {
                runRequestsFromDifferentThreads(5, 1/*retry once*/);
            }
            else if(args[0] == "cycle")
            {
                runInvalidRequest();
            }
            else if (args[0] == "stress")
            {
                runRequestsFromDifferentThreadsWithLargeRequest(2);
            }
            else if (args[0] == "unit")
            {
                AddNodeAndRemoveNodeFromTaskQueue();
            }
        }


        // Functional test for mutlithreading support
        private static void runRequestsFromDifferentThreads(int n, int retry)
        {
            processor = new RequestProcessor(3/*3 machines*/, 4 /* every 4times error per machine*/, retry);
            requestCompleted = 0;
            sentRequests = n;
            succeedRequests = 0;
            failedRequests = 0;
            for (int i = 0; i < n; i++)
            {
                String path = (i % 2 == 0) ? "test.xml" : "test2.xml";
                Thread myNewThread = new Thread(() => startRequestAsync(path, i));
                Thread.Sleep(1000);
                myNewThread.Start();

            }
        }

        // The input is invalid.
        private static void runInvalidRequest()
        {
            requestCompleted = 0;
            sentRequests = 1;
            succeedRequests = 0;
            failedRequests = 0;
            String path = "cycle.xml";
            Thread thread = new Thread(() => startRequestAsync(path, 0));

            thread.Start();
        }

        // Stree Test
        private static void runRequestsFromDifferentThreadsWithLargeRequest(int retry)
        {
            processor = new RequestProcessor(10/*3 machines*/, 10 /* every 10 times error per machine*/, retry);
            requestCompleted = 0;
            sentRequests = 50;
            succeedRequests = 0;
            failedRequests = 0;
            for (int i = 0; i < 50; i++)
            {
                Thread.Sleep(1000);
                String path =  "test2.xml";
           
                Thread thread = new Thread(() => startRequestAsync(path, i));
                thread.Start();

            }
        }

        // Unit Test
        private static void AddNodeAndRemoveNodeFromTaskQueue()
        {
            TaskQueue queue = new TaskQueue(1,5);
            Node node1 = new Node();
            Node node2 = new Node();
            Node node3 = new Node();
            Node node4 = new Node();
            queue.AddNode(node1);
            queue.AddNode(node2);
            queue.AddNode(node3);
            queue.AddNode(node4);
            bool returnValue = queue.RemoveNode(node4);
            Console.WriteLine("Remove Node which is not running on machine: "+ returnValue.ToString());

            Console.WriteLine("Expected: True ");
        }

        private static async Task MainAsync(String path, int id)
        {
            Console.WriteLine("Thread {0}: ", id);
            DagRequest request = new DagRequest(path, id);
            
            Task<RequestResponse> task = processor.ProcessRequestAsync(request);
            await Task.WhenAll(task);
            Interlocked.Increment(ref requestCompleted);

            Console.WriteLine("Finish Request: {0} on thread {1}", task.Result.Result, id);
            if(task.Result.Result == 0)
            {
                Interlocked.Increment(ref succeedRequests);

            }
            else
            {
                Interlocked.Increment(ref failedRequests);
            }

            int finished = (int)Interlocked.Read(ref requestCompleted);

            if(finished == sentRequests)
            {
                Console.WriteLine("Finished succeed requests: {0}, failed: {1}, total: {2}", succeedRequests, failedRequests, finished);
            }
        }

        private static void startRequestAsync(String xml, int threadId)
        {

            MainAsync(xml, threadId).GetAwaiter().GetResult();
        }
    }
}
