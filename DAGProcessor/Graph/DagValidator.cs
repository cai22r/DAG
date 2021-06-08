using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace DAGProcessor.Graph
{
    public class DagValidator
    {
        public DagValidator()
        {

        }

        private static bool validateOutput(ReadOnlyDictionary<Node, List<Node>> parentList, DAG dag, Node[] completedRunOrder)
        {
            for (int index = 0; index < completedRunOrder.Length; index++)
            {
                var item = completedRunOrder[index];
                List<Node> parentNodes = null;
                if(parentList.TryGetValue(item, out parentNodes))
                {
                    for (int j = index + 1; j < completedRunOrder.Length; j++)
                    {
                        var laterNode = completedRunOrder[j];
                        foreach (var parent in parentNodes)
                        {
                            if (laterNode.Id == parent.Id)
                            {
                                return false;
                            }
                        }



                    }
                }

            }

            return true;
        }

        public static bool isValidDag(DAG dag)
        {
            return !hasInvalidNode(dag) && !hasCycle(dag)  ;
        }

        private static bool hasInvalidNode(DAG dag)
        {
            HashSet<int> nodeIdSet = new HashSet<int>();
            foreach (var node in dag.nodes)
            {
                nodeIdSet.Add(node.Id);
            }

            foreach (var node in dag.nodes)
            {
                foreach (var child in node.dependencyList)
                {
                    if (!nodeIdSet.Contains(child.Id))
                    {
                        return true;
                    }

                }
            }

            return false;
        }

        private static bool hasCycle(DAG dag)
        {
            HashSet<Node> visited = new HashSet<Node>();
            HashSet< Node > path = new HashSet<Node>();
            foreach(var node in dag.nodes)
            {
                if(hasCycleDFS(node, visited, path))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool hasCycleDFS(Node node, HashSet<Node> visited, HashSet<Node> path)
        {
            if(path.Contains(node))
            {
                return true;
            }

            if (visited.Contains(node))
            {
                return false;
            }

            //mark the map for visiting
            visited.Add(node);
            path.Add(node);

            foreach(var child in node.dependencyList)
            {
                if (hasCycleDFS(child, visited, path))
                {
                    return true;
                }

            }

            path.Remove(node);

            return false;

        }
    }
}
