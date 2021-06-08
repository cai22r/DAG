using DAGProcessor.Graph;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace DAGProcessor
{
    public class RequestProcessor : IDagExecutor
    {
        private readonly TaskQueue taskQueue;
        private readonly int retry;
        public RequestProcessor(int m, int error,int retryPolicy)
        {
            taskQueue = new TaskQueue(m, error);
            retry = retryPolicy;
        }

        // Implements the IDagEexcutor interface.
        public Task<RequestResponse> ProcessRequestAsync(DagRequest request)
        {
            DAG dag = XMLParser.ParseDagRequest(request);
            dag.setTaskQueue(taskQueue, retry);
            return dag.subscribeToGraph();
        }
    }
}
