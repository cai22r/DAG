using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml.Serialization;
using DAGProcessor.Graph;

namespace DAGProcessor
{
    public class XMLParser
    {
        public static DAG ParseDagRequest(DagRequest request)
        {
            using (var fileStream = File.Open(request.DagXml, FileMode.Open))
            {
                XmlSerializer serializer = new XmlSerializer(typeof(DAG));
                var dag = (DAG)serializer.Deserialize(fileStream);
                dag.Id = request.id;
                return dag;
            }
        }
    }
}
