using System;
using System.Collections.Generic;
using System.Text;

namespace DAGProcessor
{
    /// <summary>
    /// Class that encapsulates fields related to a DAG request.
    /// </summary>
    public class DagRequest
    {
        /// <summary>
        /// DAG serialized in XML
        /// </summary>
        public string DagXml { get; private set; }
        public int id { get; private set; }

        public DagRequest(string filePath, int id)
        {
            DagXml = filePath;
            this.id = id;
        }
    }

}
