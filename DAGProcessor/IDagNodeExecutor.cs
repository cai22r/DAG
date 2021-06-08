using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace DAGProcessor
{
    /// Execution engine responsible to execute the nodes in the graph.
    /// </summary>
    public interface IDagNodeExecutor
    {
        /// <summary>
        /// Executes a node in the graph.
        /// </summary>
        /// <param name="unitOfExecution">The node to be executed.</param>
        /// <returns>0 if success, < 0 otherwise.</returns>
        public Task<int> ExecuteAsync(IDagNode unitOfExecution);
    }
}
