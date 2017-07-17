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

        public static int getNextIndex(int currentStep, int noOfSteps, NodeGeometry node)
        {
            if (!node.StepHappening)
            {
                node.StartFrame = getCurrentFrame();
                node.StepHappening = true;
            }

            Log.Info($"Current frame comparison value: {node.StartFrame} + 5 - {getCurrentFrame()} = {(node.StartFrame + 20) - getCurrentFrame()}");
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

        public static List<Phase> buildOrderedPhases(NodeGeometry node, out ushort[] segArray)
        {
            int numSegs;
            segArray = getOrderedSegments(node, out numSegs);
            string s = "";
            for (int q = 0; q < segArray.Length; q++)
            {
                s = s + " " + segArray[q];
            }
            Log.Info($"SEGARRAY: {s}");
            List<Phase> phaseList = new List<Phase>();

            phaseList = phaseBuilder(segArray, numSegs);

            return phaseList;

        }

        private static bool[] getTurnPossibilities(ushort[] segArray, int currentIndex)
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

                if (segArray[cycleIndex] != 0 && !SegmentEndGeometry.Get(segArray[cycleIndex], true).IncomingOneWay)
                {
                    turnPossibilities[j] = true;
                }
                else
                {
                    turnPossibilities[j] = false;
                }
            }
            string s = "";
            for (int q = 0; q < turnPossibilities.Length; q++)
            {
                s = s + " " + turnPossibilities[q];
            }
            Log.Info($"TURN POSIBILITIES FOR {segArray[currentIndex]}: {s}");
            return turnPossibilities;
        }

        public static List<Phase> phaseBuilder(ushort[] segArray, int numSegs)
        {
            List<Phase> phaseList = new List<Phase>();
            for (int i = 0; i < 4; i++)
            {
                ushort currentSeg = segArray[i];

                if (currentSeg.Equals(0))
                {
                    continue;
                }

                if (SegmentEndGeometry.Get(currentSeg, true).OutgoingOneWay)
                {
                    numSegs--;
                    Log.Info($"CURRENT SEG IS OUTGOING ONEWAY: {currentSeg}");
                    continue;
                }
            }

            Log.Info($"NO. OF SEGMENTS AFTER CONSIDERING OUTGOING ONEWAYS:  {numSegs}");
            for (int i = 0; i < 4; i++)
            {
                ushort currentSeg = segArray[i];

                if (currentSeg.Equals(0))
                {
                    continue;
                }

                if (SegmentEndGeometry.Get(currentSeg, true).OutgoingOneWay)
                {
                    Log.Info($"CURRENT SEG IS OUTGOING ONEWAY: {currentSeg}");
                    continue;
                }

                bool[] turnPossibilities = getTurnPossibilities(segArray, i);



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
                    string s2 = "";
                    for (int q = 0; q < conflictArray.Length; q++)
                    {
                        s2 = s2 + " "+conflictArray[q];
                    }
                    Log.Info($"initial conflict array before recursive function is called: {s2}");
                    
                    recursiveBuilder(1, segsSeen, phase, conflictArray,segArray,i, numSegs, phaseList);
                }
            }

            return phaseList;
        }

        public static ushort[] getOrderedSegments(NodeGeometry node, out int numSegs)
        {
            ushort[] tempSegArray = new ushort[4];

            tempSegArray[0] = node.SegmentEndGeometries[0].SegmentId;
            Log.Info($"self: {node.SegmentEndGeometries[0].SegmentId}");
            numSegs = 1;

            if (node.SegmentEndGeometries[0].NumRightSegments > 0)
            {
                tempSegArray[1] = node.SegmentEndGeometries[0].RightSegments[0];
                numSegs++;
                Log.Info($"has right segments: {node.SegmentEndGeometries[0].RightSegments[0]}");
            }
            else
            {
                tempSegArray[1] = 0;
                Log.Info($"doesn't have right segments: {node.SegmentEndGeometries[0].RightSegments[0]}");
            }

            if (node.SegmentEndGeometries[0].NumStraightSegments > 0)
            {
                tempSegArray[2] = node.SegmentEndGeometries[0].StraightSegments[0];
                numSegs++;
                Log.Info($"has straight segments: {node.SegmentEndGeometries[0].StraightSegments[0]}");
            }
            else
            {
                tempSegArray[2] = 0;
                Log.Info($"doesn't have straight segments: {node.SegmentEndGeometries[0].StraightSegments[0]}");
            }

            if (node.SegmentEndGeometries[0].NumLeftSegments > 0)
            {
                tempSegArray[3] = node.SegmentEndGeometries[0].LeftSegments[0];
                numSegs++;
                Log.Info($"has left segments: {node.SegmentEndGeometries[0].LeftSegments[0]}");
            }
            else
            {
                tempSegArray[3] = 0;
                Log.Info($"doesnt have left segments: {node.SegmentEndGeometries[0].LeftSegments[0]}");
            }
            Log.Info($"NO. OF SEGMENTS: {numSegs}");
            return tempSegArray;
        }


        public static void recursiveBuilder(int depth, ArrayList segmentsSeen, Phase phase, bool[] conflictArray, ushort[] segArray, int initialSegmentIndex, int numSegs, List<Phase> phases)
        {
            string s = "";
            for (int q = 0; q < conflictArray.Length; q++)
            {
                s = s + " "+ conflictArray[q];
            }
            Log.Info($"initial conflict array: {s} ID: {segArray[initialSegmentIndex]}");
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

                if (SegmentEndGeometry.Get(currentSeg, true).OutgoingOneWay)
                {
                    continue;
                }

                bool[] turnPossibilities = getTurnPossibilities(segArray, i);

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
                    
                    string s2 = "";
                    for (int q = 0; q < recursiveConflictArray.Length; q++)
                    {
                        s2 = s2 + " " + recursiveConflictArray[q];
                    }
                    Log.Info($"recursive conflict array: {s2} ID: {currentSeg}");
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
                    string s3 = "";
                    for (int q = 0; q < rotatedRecursiveConflictArray.Length; q++)
                    {
                        s3 = s3 + " " + rotatedRecursiveConflictArray[q];
                    }
                    Log.Info($"rotated recursive conflict array: {s3} ID: {currentSeg}");
                    for (int k = 0; k < 4; k++)
                    {
                        if(rotatedRecursiveConflictArray[k] && conflictArray[k])
                        {
                            isValid = false;
                            break;
                        }
                    }
                    Log.Info($"depth:  {depth} ID: {currentSeg}");

                    if (isValid)
                    {
                        noMoreValid = false;
                        string s4 = "";
                        for (int q = 0; q < rotatedRecursiveConflictArray.Length; q++)
                        {
                            s4 = s4 + " " + rotatedRecursiveConflictArray[q];
                        }
                        Log.Info($"valid rotated recursive conflict array: {s4} ID: {currentSeg}");
                        bool[] newConflictArray = new bool[4];
                        for (int k = 0; k < 4; k++)
                        {
                            newConflictArray[k] = rotatedRecursiveConflictArray[k] || conflictArray[k];                         
                        }
                        s3 = "";
                        for (int q = 0; q < newConflictArray.Length; q++)
                        {
                            s3 = s3 + " " + newConflictArray[q];
                        }
                        Log.Info($"new conflict array: {s3} ID: {segArray[initialSegmentIndex]}");
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
                                ushort tempSeg = segArray[i];
                                if (tempSeg.Equals(0)|| SegmentEndGeometry.Get(tempSeg, true).OutgoingOneWay)
                                {
                                    continue;
                                }
                                bool isSeenTemp = false;
                                foreach (object o in segmentsSeen)
                                {
                                    if (i == (int)o)
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
                            
                            segmentsSeen.RemoveAt(segmentsSeen.Count - 1);
                            depth--;
                            continue;
                        }
                        else
                        {
                            recursiveBuilder(depth, segmentsSeen, phase, newConflictArray, segArray, initialSegmentIndex, numSegs, phases);
                            segmentsSeen.RemoveAt(segmentsSeen.Count - 1);
                        }
                    }
                }
            }
            if (noMoreValid)
            {
                Log.Info($"no more valid, ID: {segArray[initialSegmentIndex]}");

                int l = segmentsSeen.Count;
                string s4 = "";
                for (int q = 0; q < segArray.Length; q++)
                {
                    s4 = s4 + " " + segArray[q];
                }
                Log.Info($"{s4} ID: {segArray[initialSegmentIndex]}");
                for (int k = 0; k < 4; k++)
                {
                    Log.Info($"L: {l}");

                    ushort tempSeg = segArray[k];
                    if (tempSeg.Equals(0) || SegmentEndGeometry.Get(tempSeg, true).OutgoingOneWay)
                    {
                        Log.Info($"skipped");
                        continue;
                    }
                   
                    bool isSeenTemp = false;
                    foreach (object o in segmentsSeen)
                    {
                        if (k == (int)o)
                        {
                            isSeenTemp = true;
                            Log.Info($"broke");
                            break;
                        }
                    }
                    if (!isSeenTemp)
                    {
                        Log.Info($"added phase");
                        phase.addDirAndSeg(l, tempSeg, (Phase.Directions)0);
                        l++;
                    }
                }

                Log.Info($"a");
                Phase copyPhase = new Phase(numSegs);
                Log.Info($"b");
                copyPhase.copy(phase, numSegs);
                Log.Info($"c");
                phases.Add(copyPhase);
                Log.Info($"d");
                return;

            }


        }

    }
}