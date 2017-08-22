using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TrafficManager.API
{
    class NodeRecordingObject
    {
        public NodeRecordingObject()
        {

        }
        public ushort nodeID;
        public int totalWaitingTime;
        public int totalVehiclesProcessed;
        public double avergaeWaitTime;
        public int noOfSegments;
        public int noOfOutgoingOneWays;
        public int noOFIncomingOneWays;
    }
}
