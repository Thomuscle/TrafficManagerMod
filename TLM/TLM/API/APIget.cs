using System;
using System.Reflection;
using ColossalFramework;
using System.Collections.Generic;
using ICities;
using TrafficManager.Custom.AI;
using UnityEngine;
using TrafficManager.State;
using TrafficManager.Manager;
using TrafficManager.UI;
using CSUtil.Commons;
using TrafficManager.Geometry;
using ColossalFramework.Plugins;
using System.Collections;
using TrafficManager.Traffic;
using TrafficManager.TrafficLight;

namespace TrafficManager.API
{
    public static class APIget
    {

        //returns a list of nodes with traffic lights 
        public static List<NetNode> getNodes()
        {
            List<NetNode> lightNodes = new List<NetNode>();
            var netManager = Singleton<NetManager>.instance;
            for (ushort i = 0; i < netManager.m_nodes.m_size; i++)
            {
                var node = netManager.m_nodes.m_buffer[i];

                var hasLights = ((node.m_flags & NetNode.Flags.TrafficLights) == NetNode.Flags.TrafficLights);

                if (hasLights)
                {
                    lightNodes.Add(node);
                }
            }
            return lightNodes;
        }
        public static bool isRecording = false;
        public static List<ushort> getSegments(NetNode node)
        {
            List<ushort> segments = new List<ushort>();
            var netManager = Singleton<NetManager>.instance;
            for (ushort i = 0; i < node.CountSegments(); i++)
            {
                segments.Add(node.GetSegment(i));

            }
            return segments;
        }

        //for a nodeid (intersection), get all the possible directions for 
        //each segment(traffic light) and put them into a binary left-straight-right array
        public static ushort[] getSegmentDirections(ushort nodeID)
        {
            ushort segmentCount = 0;

            //get the net node for the node id
            Constants.ServiceFactory.NetService.ProcessNode(nodeID, delegate (ushort nId, ref NetNode node)
            {
                segmentCount = (ushort)node.CountSegments();
                return true;
            });

            //initialise array
            ushort[] lsrArray = new ushort[segmentCount * 3 - 1];
            NodeGeometry nodeGeometry = NodeGeometry.Get(nodeID);
            int counter = 0;

            //loop through each light at intersection
            foreach (SegmentEndGeometry end in nodeGeometry.SegmentEndGeometries)
            {
                if (end.NumLeftSegments > 0)
                { lsrArray[counter * 3] = 1; }
                else
                { lsrArray[counter * 3] = 0; }
                if (end.NumStraightSegments > 0)
                { lsrArray[counter * 3 + 1] = 1; }
                else
                { lsrArray[counter * 3 + 1] = 0; }
                if (end.NumRightSegments > 0)
                { lsrArray[counter * 3 + 2] = 1; }
                else
                { lsrArray[counter * 3 + 2] = 0; }
                counter++;
            }
            return lsrArray;
        }

        public static uint getCurrentFrame()
        {

            return Constants.ServiceFactory.SimulationService.CurrentFrameIndex >> 6;
        }

        //static bool stepHappening = false;
        //static uint startFrame = 0;

        public static int getNextIndexRoundRobin(int currentStep, int noOfSteps, NodeGeometry node)
        {
            if (!node.StepHappening)
            {
                node.StartFrame = getCurrentFrame();
                node.StepHappening = true;
            }

            //Log.Info($"Current frame comparison value: {node.StartFrame} + 5 - {getCurrentFrame()} = {(node.StartFrame + 20) - getCurrentFrame()}");
            if (Math.Max(0, ((int)node.StartFrame + 5) - (int)getCurrentFrame()) == 0)
            {


                node.StepHappening = false;
                return (currentStep + 1) % noOfSteps;
            }
            else
            {
                return currentStep;
            }

        }

        public static int getNextIndex(int currentStep, int noOfSteps, NodeGeometry nodeGeometry, List<FlexibleTrafficLightsStep> phases)
        {
           

            nodeGeometry.numTicks++;

            if(nodeGeometry.numTicks < 3)
            {
                return currentStep;
            }

            VehicleStateManager vehStateMan = VehicleStateManager.Instance;
            TrafficLightSimulationManager tlsMan = TrafficLightSimulationManager.Instance;

            ushort[] queueLengths = new ushort[phases[0].LightValues.Length];
            int[] directionOrdering = new int[phases[0].LightValues.Length];
            int[] longestWaiting = new int[phases[0].LightValues.Length];

            for(int i = 0; i < directionOrdering.Length; i++)
            {
                directionOrdering[i] = i;
            }
            //Log.Info($"direction ordering initialised ID: {nodeGeometry.NodeId}");
            foreach (SegmentEndGeometry se in nodeGeometry.SegmentEndGeometries)
            {
                if (se == null || se.OutgoingOneWay)
                    continue;
                //Log.Info($"passed outgoing one way ID: {nodeGeometry.NodeId}");
                SegmentEnd end = SegmentEndManager.Instance.GetSegmentEnd(se.SegmentId, se.StartNode);
                if (end == null)
                {
                    continue; // skip invalid segment
                }




                

                ushort importance = 0;
                ushort vehicleId = end.FirstRegisteredVehicleId;
                while (vehicleId != 0)
                {

                    VehicleState v = vehStateMan._GetVehicleState(vehicleId);
                    //hopefully this fixes, checks to see if vehicle is valid before using it
                    if (v.CheckValidity(ref Singleton<VehicleManager>.instance.m_vehicles.m_buffer[vehicleId]))
                    {
                       
                        VehicleState.Direction d = v.direction;
                        int w = v.WaitTime;
                        int index = 0;

                        for (int i = 0; i < 4; i++)
                        {
                            if (phases[0].SegArray[i].Equals(0))
                            {
                                index--;
                                //Log.Info($"No Segment: {index}");
                                continue;
                            }
                            //Log.Info($"1st if. ID: {nodeGeometry.NodeId}");
                            if (SegmentEndGeometry.Get(phases[0].SegArray[i], true).NodeId().Equals(nodeGeometry.NodeId))
                            {
                                //Log.Info($"2nd if. ID: {nodeGeometry.NodeId}");
                                if (SegmentEndGeometry.Get(phases[0].SegArray[i], true).OutgoingOneWay)
                                {
                                    //Log.Info($"3rd if. ID: {nodeGeometry.NodeId}");
                                    index--;
                                    //Log.Info($"Outgoing One Way: {index}");
                                    continue;
                                }
                            }
                            else
                            {
                                //Log.Info($"4th if. ID: {nodeGeometry.NodeId}");
                                if (SegmentEndGeometry.Get(phases[0].SegArray[i], false).OutgoingOneWay)
                                {
                                    //Log.Info($"5th if. ID: {nodeGeometry.NodeId}");
                                    index--;
                                    //Log.Info($"Outgoing One Way: {index}");
                                    continue;
                                }
                            }

                            if (phases[0].SegArray[i].Equals(se.SegmentId))
                            {
                                //Log.Info($"6th if. ID: {nodeGeometry.NodeId}");
                                index = index + i;
                                //Log.Info($"Final Index: {index}");
                                break;
                            }
                        }

                        if (index < 0)
                        {
                            index = 0;
                        }

                        //Log.Info($"queueLengths: {queueLengths.Length}, value: {index * 3 + (int)d} ");
                        queueLengths[index * 3 + (int)d]++;
                        //Log.Info($"incr queue length: {nodeGeometry.NodeId}");

                        if (w > 25)
                        {
                            w = ushort.MaxValue - importance;
                            importance++;
                        }

                        if (w > longestWaiting[index * 3 + (int)d])
                        {
                            longestWaiting[index * 3 + (int)d] = w;
                            //TODO set the wait time to maximum minus vehicle position
                        }
                        //Log.Info($"longest waiting set ID: {nodeGeometry.NodeId}");

                        
                    }
                    vehicleId = vehStateMan._GetVehicleState(vehicleId).NextVehicleIdOnSegment;
                }
              
            }
            //TODO sort out blocked target segments
            //for (int i = 0; i < longestWaiting.Length; i++)
            //{
            //    if(longestWaiting[i] > 25)
            //    {
            //        queueLengths[i] = ushort.MaxValue;
            //    }
            //}

            //if (nodeGeometry.NodeId.Equals(20832))
            //{
            //    for (int p = 0; p< queueLengths.Length; p++)
            //    {
            //        Log.Info($"index: {p} queuelengths: {queueLengths[p]}");

            //    }
            //    //print queueLengths
            //}
            //Log.Info($"queue len waiting set to max. ID: {nodeGeometry.NodeId}");
            Array.Sort(queueLengths, directionOrdering, Comparer.Default);
            Array.Reverse(directionOrdering);
            //Log.Info($"sort and reverse ID: {nodeGeometry.NodeId}");
            List<FlexibleTrafficLightsStep> currentSubList = new List<FlexibleTrafficLightsStep>(phases);
            List<FlexibleTrafficLightsStep> tempSubList = new List<FlexibleTrafficLightsStep>(phases);

            for (int i = 0; i < directionOrdering.Length; i++)
            {
                foreach(FlexibleTrafficLightsStep f in currentSubList)
                {
                    if (f.LightValues[directionOrdering[i]].Equals(0))
                    {
                        tempSubList.Remove(f);
                    }
                }
                //Log.Info($"finding which phases to remove ID: {nodeGeometry.NodeId}");
                if (!tempSubList.Count.Equals(0))
                {
                    currentSubList = new List<FlexibleTrafficLightsStep>(tempSubList);
                }
                else
                {
                    tempSubList = new List<FlexibleTrafficLightsStep>(currentSubList);
                }
            }

            //if (nodeGeometry.NodeId.Equals(20832))
            //{
            //    for (int p= 0; p < currentSubList[0].LightValues.Length; p++)
            //    {
            //        Log.Info($"current sub list light value index {p}: {currentSubList[0].LightValues[p]}");
            //    }

            //    for (int p = 0; p < directionOrdering.Length; p++)
            //    {
            //        Log.Info($"current ordering index {p}: {directionOrdering[p]}");
            //    }

            //}
            if (!phases.IndexOf(currentSubList[0]).Equals(currentStep))
            {
                nodeGeometry.numTicks = 0;
            }
            //Log.Info($"setting the step done ID: {nodeGeometry.NodeId}");
            return phases.IndexOf(currentSubList[0]);
        }

        public static int getNextIndexOptimal(int currentStep, int noOfSteps, NodeGeometry nodeGeometry, List<FlexibleTrafficLightsStep> phases)
        {


            nodeGeometry.numTicks++;

            if (nodeGeometry.numTicks < 3)
            {
                return currentStep;
            }

            VehicleStateManager vehStateMan = VehicleStateManager.Instance;
            TrafficLightSimulationManager tlsMan = TrafficLightSimulationManager.Instance;

            ushort[] queueLengths = new ushort[phases[0].LightValues.Length];
            int[] directionOrdering = new int[phases[0].LightValues.Length];
            int[] longestWaiting = new int[phases[0].LightValues.Length];

            for (int i = 0; i < directionOrdering.Length; i++)
            {
                directionOrdering[i] = i;
            }
            //Log.Info($"direction ordering initialised ID: {nodeGeometry.NodeId}");
            foreach (SegmentEndGeometry se in nodeGeometry.SegmentEndGeometries)
            {
                if (se == null || se.OutgoingOneWay)
                    continue;
                //Log.Info($"passed outgoing one way ID: {nodeGeometry.NodeId}");
                SegmentEnd end = SegmentEndManager.Instance.GetSegmentEnd(se.SegmentId, se.StartNode);
                if (end == null)
                {
                    continue; // skip invalid segment
                }






                ushort importance = 0;
                ushort vehicleId = end.FirstRegisteredVehicleId;
                while (vehicleId != 0)
                {

                    VehicleState v = vehStateMan._GetVehicleState(vehicleId);
                    //hopefully this fixes, checks to see if vehicle is valid before using it
                    if (v.CheckValidity(ref Singleton<VehicleManager>.instance.m_vehicles.m_buffer[vehicleId]))
                    {

                        VehicleState.Direction d = v.direction;
                        int w = v.WaitTime;
                        int index = 0;

                        for (int i = 0; i < 4; i++)
                        {
                            if (phases[0].SegArray[i].Equals(0))
                            {
                                index--;
                                //Log.Info($"No Segment: {index}");
                                continue;
                            }
                            //Log.Info($"1st if. ID: {nodeGeometry.NodeId}");
                            if (SegmentEndGeometry.Get(phases[0].SegArray[i], true).NodeId().Equals(nodeGeometry.NodeId))
                            {
                                //Log.Info($"2nd if. ID: {nodeGeometry.NodeId}");
                                if (SegmentEndGeometry.Get(phases[0].SegArray[i], true).OutgoingOneWay)
                                {
                                    //Log.Info($"3rd if. ID: {nodeGeometry.NodeId}");
                                    index--;
                                    //Log.Info($"Outgoing One Way: {index}");
                                    continue;
                                }
                            }
                            else
                            {
                                //Log.Info($"4th if. ID: {nodeGeometry.NodeId}");
                                if (SegmentEndGeometry.Get(phases[0].SegArray[i], false).OutgoingOneWay)
                                {
                                    //Log.Info($"5th if. ID: {nodeGeometry.NodeId}");
                                    index--;
                                    //Log.Info($"Outgoing One Way: {index}");
                                    continue;
                                }
                            }

                            if (phases[0].SegArray[i].Equals(se.SegmentId))
                            {
                                //Log.Info($"6th if. ID: {nodeGeometry.NodeId}");
                                index = index + i;
                                //Log.Info($"Final Index: {index}");
                                break;
                            }
                        }

                        if (index < 0)
                        {
                            index = 0;
                        }

                        //Log.Info($"queueLengths: {queueLengths.Length}, value: {index * 3 + (int)d} ");
                        queueLengths[index * 3 + (int)d]++;
                        //Log.Info($"incr queue length: {nodeGeometry.NodeId}");

                        if (w > 25)
                        {
                            w = ushort.MaxValue - importance;
                            importance++;
                        }

                        if (w > longestWaiting[index * 3 + (int)d])
                        {
                            longestWaiting[index * 3 + (int)d] = w;
                            //TODO set the wait time to maximum minus vehicle position
                        }
                        //Log.Info($"longest waiting set ID: {nodeGeometry.NodeId}");


                    }
                    vehicleId = vehStateMan._GetVehicleState(vehicleId).NextVehicleIdOnSegment;
                }

            }
            
            int bestIndex = 0;
            int bestTotal = 0;
            int bestNumWaitingEx = 0;
            foreach(FlexibleTrafficLightsStep f in phases)
            {
                int total = 0;
                int numWaitingEx = 0;
                for(int i=0; i<f.LightValues.Length; i++)
                {
                    if (f.LightValues[i].Equals(1))
                    {
                        if(longestWaiting[i] > 25)
                        {
                            numWaitingEx++;
                        }
                        total = total + queueLengths[i];
                    }
                }

                if(numWaitingEx > bestNumWaitingEx)
                {
                    bestIndex = phases.IndexOf(f);
                    bestTotal = total;
                    bestNumWaitingEx = numWaitingEx;
                }else if(numWaitingEx == bestNumWaitingEx)
                {
                    if (total > bestTotal)
                    {
                        bestIndex = phases.IndexOf(f);
                        bestTotal = total;
                    }
                }
                
            }

            return bestIndex;
        }

        public static List<Phase> buildOrderedPhases(NodeGeometry node, out ushort[] segArray)
        {
            int numSegs;
            segArray = getOrderedSegments(node, out numSegs);
            
            
            List<Phase> phaseList = new List<Phase>();

            phaseList = phaseBuilder(segArray, numSegs, node);

            return phaseList;

        }

        private static bool[] getTurnPossibilities(ushort[] segArray, int currentIndex, NodeGeometry node)
        {
            bool[] turnPossibilities = new bool[3];
            int cycleIndex = currentIndex;
            for (int j = 0; j < 3; j++)
            {
                cycleIndex++;
                if (cycleIndex > 3)
                {
                    cycleIndex = 0;
                }

                if (segArray[cycleIndex] != 0)
                {
                    if (SegmentEndGeometry.Get(segArray[cycleIndex], true).NodeId().Equals(node.NodeId))
                    {
                        if (!SegmentEndGeometry.Get(segArray[cycleIndex], true).IncomingOneWay)
                        {
                            turnPossibilities[j] = true;
                        }
                        else
                        {
                            turnPossibilities[j] = false;
                        }
                    }
                    else
                    {
                        if (!SegmentEndGeometry.Get(segArray[cycleIndex], false).IncomingOneWay)
                        {
                            turnPossibilities[j] = true;
                        }
                        else
                        {
                            turnPossibilities[j] = false;
                        }
                    }
                    
                }
                else
                {
                    turnPossibilities[j] = false;
                }
            }
           
            
            return turnPossibilities;
        }

        public static List<Phase> phaseBuilder(ushort[] segArray, int numSegs, NodeGeometry node)
        {
            List<Phase> phaseList = new List<Phase>();
            for (int i = 0; i < 4; i++)
            {
                ushort currentSeg = segArray[i];

                if (currentSeg.Equals(0))
                {
                    continue;
                }

                if(SegmentEndGeometry.Get(currentSeg, true).NodeId().Equals(node.NodeId))
                {
                    if (SegmentEndGeometry.Get(currentSeg, true).OutgoingOneWay)
                    {
                        
                        numSegs--;
                        continue;
                    }
                }
                else
                {
                    if (SegmentEndGeometry.Get(currentSeg, false).OutgoingOneWay)
                    {
                        
                        numSegs--;
                        continue;
                    }
                }
            }

            
            for (int i = 0; i < 4; i++)
            {
                ushort currentSeg = segArray[i];

                if (currentSeg.Equals(0))
                {
                    continue;
                }

                if (SegmentEndGeometry.Get(currentSeg, true).NodeId().Equals(node.NodeId))
                {
                    if (SegmentEndGeometry.Get(currentSeg, true).OutgoingOneWay)
                    {
                        
                        continue;
                    }
                }
                else
                {
                    if (SegmentEndGeometry.Get(currentSeg, false).OutgoingOneWay)
                    {
                        
                        continue;
                    }
                }

                bool[] turnPossibilities = getTurnPossibilities(segArray, i, node);



                for (int j = 0; j < 3; j++)
                {
                    if (turnPossibilities[j] == false)
                    {
                        continue;
                    }

                    Phase phase = new Phase(numSegs);
                    phase.addDirAndSeg(0, currentSeg, (Phase.Directions)(j+1));

                    ArrayList segsSeen = new ArrayList();
                    segsSeen.Add(i);

                    bool[] conflictArray = new bool[4];

                    switch (j)
                    {
                        case 0:
                            conflictArray[3] = true;
                            break;
                        case 1:
                            conflictArray[1] = true;
                            conflictArray[3] = true;
                            break;
                        case 2:
                            conflictArray[0] = true;                            
                            conflictArray[1] = true;
                            conflictArray[3] = true;
                            break;
                    }
                    
                    
                    
                    recursiveBuilder(1, segsSeen, phase, conflictArray,segArray,i, numSegs, phaseList, node);
                }
            }

            return phaseList;
        }

        public static ushort[] getOrderedSegments(NodeGeometry node, out int numSegs)
        {
            ushort[] tempSegArray = new ushort[4];

            tempSegArray[0] = node.SegmentEndGeometries[0].SegmentId;
            
            numSegs = 1;

            if (node.SegmentEndGeometries[0].NumRightSegments > 0)
            {
                tempSegArray[1] = node.SegmentEndGeometries[0].RightSegments[0];
                numSegs++;
                
            }
            else
            {
                tempSegArray[1] = 0;
                //Log.Info($"doesn't have right segments: {node.SegmentEndGeometries[0].RightSegments[0]}");
            }

            if (node.SegmentEndGeometries[0].NumStraightSegments > 0)
            {
                tempSegArray[2] = node.SegmentEndGeometries[0].StraightSegments[0];
                numSegs++;
                //Log.Info($"has straight segments: {node.SegmentEndGeometries[0].StraightSegments[0]}");
            }
            else
            {
                tempSegArray[2] = 0;
                //Log.Info($"doesn't have straight segments: {node.SegmentEndGeometries[0].StraightSegments[0]}");
            }

            if (node.SegmentEndGeometries[0].NumLeftSegments > 0)
            {
                tempSegArray[3] = node.SegmentEndGeometries[0].LeftSegments[0];
                numSegs++;
                //Log.Info($"has left segments: {node.SegmentEndGeometries[0].LeftSegments[0]}");
            }
            else
            {
                tempSegArray[3] = 0;
                //Log.Info($"doesnt have left segments: {node.SegmentEndGeometries[0].LeftSegments[0]}");
            }
            //Log.Info($"NO. OF SEGMENTS: {numSegs}");
            return tempSegArray;
        }


        public static void recursiveBuilder(int depth, ArrayList segmentsSeen, Phase phase, bool[] conflictArray, ushort[] segArray, int initialSegmentIndex, int numSegs, List<Phase> phases, NodeGeometry node)
        {
            
            
            bool noMoreValid = true;
            for (int i = 0; i<4; i++)
            {
                ushort currentSeg = segArray[i];
                if (currentSeg.Equals(0))
                {
                    continue;
                }
                
                bool isSeen = false;
                foreach (object o in segmentsSeen)
                {
                    if (i == (int)o)
                    {
                        isSeen = true;
                        break;
                    }
                }
                if (isSeen)
                {
                    continue;
                }

                if (SegmentEndGeometry.Get(currentSeg, true).NodeId().Equals(node.NodeId))
                {
                    if (SegmentEndGeometry.Get(currentSeg, true).OutgoingOneWay)
                    {
                        continue;
                    }
                }
                else
                {
                    if (SegmentEndGeometry.Get(currentSeg, false).OutgoingOneWay)
                    {
                        continue;
                    }
                }

                bool[] turnPossibilities = getTurnPossibilities(segArray, i, node);

                for (int j=0; j<3; j++)
                {
                    if (!turnPossibilities[j])
                    {
                        continue;
                    }

                    bool[] recursiveConflictArray = new bool[4];

                    switch (j)
                    {
                        case 0:
                            recursiveConflictArray[3] = true;
                            break;
                        case 1:
                            recursiveConflictArray[1] = true;
                            recursiveConflictArray[3] = true;
                            break;
                        case 2:
                            recursiveConflictArray[0] = true;
                            recursiveConflictArray[1] = true;
                            recursiveConflictArray[3] = true;
                            break;
                    }
                    
                    
                    
                    int diff = i - initialSegmentIndex;

                    bool[] rotatedRecursiveConflictArray = recursiveConflictArray;

                    if(diff > 0)
                    {
                        while(diff != 0)
                        {
                            bool[] tempArray = new bool[4];

                            tempArray[0] = rotatedRecursiveConflictArray[1];
                            tempArray[1] = rotatedRecursiveConflictArray[3];
                            tempArray[2] = rotatedRecursiveConflictArray[0];
                            tempArray[3] = rotatedRecursiveConflictArray[2];
                            rotatedRecursiveConflictArray = tempArray;

                            diff--;
                        }
                    } else
                    {
                        while (diff != 0)
                        {
                            bool[] tempArray = new bool[4];

                            tempArray[0] = rotatedRecursiveConflictArray[2];
                            tempArray[1] = rotatedRecursiveConflictArray[0];
                            tempArray[2] = rotatedRecursiveConflictArray[3];
                            tempArray[3] = rotatedRecursiveConflictArray[1];
                            rotatedRecursiveConflictArray = tempArray;

                            diff++;
                        }
                    }

                    bool isValid = true;
                    
                    for (int k = 0; k < 4; k++)
                    {
                        if(rotatedRecursiveConflictArray[k] && conflictArray[k])
                        {
                            isValid = false;
                            break;
                        }
                    }
                    

                    if (isValid)
                    {
                        noMoreValid = false;
                        
                        bool[] newConflictArray = new bool[4];
                        for (int k = 0; k < 4; k++)
                        {
                            newConflictArray[k] = rotatedRecursiveConflictArray[k] || conflictArray[k];                         
                        }
                        
                        segmentsSeen.Add(i);
                        phase.addDirAndSeg(depth, currentSeg, (Phase.Directions)(j+1));
                        depth++;
                        bool isFull = true;
                        for (int k = 0; k < 4; k++)
                        {
                            if (!newConflictArray[k])
                            {
                                isFull = false;
                            }
                            
                        }
                        if (depth == numSegs || isFull)
                        {
                            int l = depth;
                            for (int k = 0; k < 4; k++)
                            {
                                ushort tempSeg = segArray[k];
                                if (tempSeg.Equals(0))
                                {
                                    continue;
                                }
                                if (SegmentEndGeometry.Get(tempSeg, true).NodeId().Equals(node.NodeId))
                                {
                                    if (SegmentEndGeometry.Get(tempSeg, true).OutgoingOneWay)
                                    {
                                        //Log.Info($"outgoing true: {tempSeg}");

                                        continue;
                                    }
                                }
                                else
                                {
                                    if (SegmentEndGeometry.Get(tempSeg, false).OutgoingOneWay)
                                    {
                                        //Log.Info($"outgoing false: {tempSeg} ");
                                        continue;
                                    }
                                }
                                bool isSeenTemp = false;
                                foreach (object o in segmentsSeen)
                                {
                                    if (k == (int)o)
                                    {
                                        //Log.Info($"is seen: {tempSeg} ");
                                        isSeenTemp = true;
                                        break;
                                    }
                                }
                                if (!isSeenTemp)
                                {
                                    phase.addDirAndSeg(l, tempSeg, (Phase.Directions)0);
                                    l++;
                                }
                            }

                            Phase copyPhase = new Phase(numSegs);
                            copyPhase.copy(phase, numSegs);
                            phases.Add(copyPhase);
                            
                            segmentsSeen.RemoveAt(segmentsSeen.Count - 1);
                            depth--;
                            continue;
                        }
                        else
                        {
                            recursiveBuilder(depth, segmentsSeen, phase, newConflictArray, segArray, initialSegmentIndex, numSegs, phases, node);
                            segmentsSeen.RemoveAt(segmentsSeen.Count - 1);
                            depth--;
                        }
                    }
                }
            }
            if (noMoreValid)
            {
                //Log.Info($"no more valid, ID: {segArray[initialSegmentIndex]}");

                int l = segmentsSeen.Count;
                
                for (int k = 0; k < 4; k++)
                {
                    

                    ushort tempSeg = segArray[k];
                    if (tempSeg.Equals(0))
                    {
                        
                        continue;
                    }
                    if (SegmentEndGeometry.Get(tempSeg, true).NodeId().Equals(node.NodeId))
                    {
                        if (SegmentEndGeometry.Get(tempSeg, true).OutgoingOneWay)
                        {
                            
                            continue;
                        }
                    }
                    else
                    {
                        if (SegmentEndGeometry.Get(tempSeg, false).OutgoingOneWay)
                        {
                            
                            continue;
                        }
                    }

                    bool isSeenTemp = false;
                    foreach (object o in segmentsSeen)
                    {
                        if (k == (int)o)
                        {
                            isSeenTemp = true;
                            
                            break;
                        }
                    }
                    if (!isSeenTemp)
                    {
                        
                        phase.addDirAndSeg(l, tempSeg, (Phase.Directions)0);
                        l++;
                    }
                }

                
                Phase copyPhase = new Phase(numSegs);
                
                copyPhase.copy(phase, numSegs);
                
                phases.Add(copyPhase);
                
                return;

            }


        }
        
        public static void incrementWait(NodeGeometry nodeGeometry)
        {
            VehicleStateManager vehStateMan = VehicleStateManager.Instance;

            foreach (SegmentEndGeometry se in nodeGeometry.SegmentEndGeometries)
            {
                if (se == null || se.OutgoingOneWay)
                    continue;

                SegmentEnd end = SegmentEndManager.Instance.GetSegmentEnd(se.SegmentId, se.StartNode);
                if (end == null)
                {

                    continue; // skip invalid segment
                }

                //ushort vehicleId = end.FirstRegisteredVehicleId;
                //VehicleState firstState = vehStateMan._GetVehicleState(vehicleId);
                //int ret = 0;
                //while (vehicleId != 0)
                //{
                //    ++ret;
                //    VehicleState state = vehStateMan._GetVehicleState(vehicleId);



                //    state.WaitTime++;


                //    vehicleId = vehStateMan._GetVehicleState(vehicleId).NextVehicleIdOnSegment;


                //}

                ushort vehicleId = end.FirstRegisteredVehicleId;
                int numProcessed = 0;
                while (vehicleId != 0)
                {
                    VehicleState state = vehStateMan._GetVehicleState(vehicleId);
                    

                    bool breakLoop = false;

                    
                    state.ProcessCurrentAndNextPathPosition(ref Singleton<VehicleManager>.instance.m_vehicles.m_buffer[vehicleId], delegate (ref Vehicle vehState, ref PathUnit.Position curPos, ref PathUnit.Position nextPos)
                    {
                        if (!state.CheckValidity(ref vehState))
                        {
                            end.RequestCleanup();
                            return;
                        }
                        state.WaitTime++;
                        if (end.isRecording)
                        {
                            end.totalWaitTime++;
                        }
                        
                        //Log.Info($" GetVehicleMetricGoingToSegment: (Segment {end.SegmentId}, Node {end.NodeId}) Checking vehicle {vehicleId}. Coming from seg. {curPos.m_segment}, lane {curPos.m_lane}, going to seg. {nextPos.m_segment}, lane {nextPos.m_lane}");
                        for (int i = 0; i< se.RightSegments.Length; i++)
                        {
                            if (se.RightSegments[i].Equals(nextPos.m_segment))
                            {
                                state.direction = VehicleState.Direction.Right;
                            }
                        }
                        for (int i = 0; i < se.StraightSegments.Length; i++)
                        {
                            if (se.StraightSegments[i].Equals(nextPos.m_segment))
                            {
                                state.direction = VehicleState.Direction.Straight;
                            }
                        }
                        for (int i = 0; i < se.LeftSegments.Length; i++)
                        {
                            if (se.LeftSegments[i].Equals(nextPos.m_segment))
                            {
                                state.direction = VehicleState.Direction.Left;
                            }
                        }
                    });
                    
                    if (breakLoop)
                        break;

                    vehicleId = state.NextVehicleIdOnSegment;
                }
            }
        }

    }
}