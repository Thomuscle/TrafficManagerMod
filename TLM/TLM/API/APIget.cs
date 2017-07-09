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
                {lsrArray[counter * 3] = 1;}
                else
                {lsrArray[counter * 3] = 0;}
                if (end.NumStraightSegments > 0)
                {lsrArray[counter * 3 + 1] = 1;}
                else
                {lsrArray[counter * 3 + 1] = 0;}
                if (end.NumRightSegments > 0)
                {lsrArray[counter * 3 + 2] = 1;}
                else
                {lsrArray[counter * 3 + 2] = 0;}
                counter++;
            }
            return lsrArray;
        }

        public static uint getCurrentFrame()
        {
            return Constants.ServiceFactory.SimulationService.CurrentFrameIndex >> 6;
        }

        static bool stepHappening = false;
        static uint startFrame = 0;

        public static int getNextIndex(int currentStep, int noOfSteps)
        {
            if (!stepHappening)
            {
                startFrame = getCurrentFrame();
                stepHappening = true;
            }

            if( Math.Max(0, startFrame + 5 - getCurrentFrame()) == 0)
            {
                stepHappening = false;
                return (currentStep + 1) % noOfSteps;
            }
            else
            {
                return currentStep;
            }

        }
    }
}