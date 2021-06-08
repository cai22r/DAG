using System;
using System.Collections.Generic;
using System.Text;

namespace DAGProcessor
{
    public class RequestResponse
    {
        //0 if success, < 0 otherwise
        public int Result { get; private set; }

        /// <summary>
        /// Class to store the request response information.
        /// </summary>
        public RequestResponse(int v)
        {
            Result = v;
        }
    }
}
