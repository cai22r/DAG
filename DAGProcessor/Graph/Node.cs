using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Xml.Serialization;

namespace DAGProcessor.Graph
{
    public class Node : IDagNode
    {
        public enum NodeState
        {
            Waiting,

            Queueing,

            Completed,

            Cancelled,
        }

        [XmlAttribute]
        public int Id { get; set; }

        [XmlArray("dependencies")]
        [XmlArrayItem(ElementName = "Node")]
        public List<Node> dependencyList { get; set; }

        private long pendinDepencyNodeCounter = 0;

        public int retryCounter { get; set; }

        public void SetDependencyCounter()
        {
            Interlocked.Exchange(ref pendinDepencyNodeCounter, dependencyList.Count);
        }

        public void DecrementDependencyCounter()
        {
            Interlocked.Decrement(ref pendinDepencyNodeCounter);
        }

        public Boolean isReadyToQueue()
        {
            return Interlocked.Read(ref pendinDepencyNodeCounter) == 0;
        }
    }
}
