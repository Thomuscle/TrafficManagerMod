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
    public class APIget
    {
        //returns a list of nodes with traffic lights 
        public List<NetNode> getNodes()
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

        public List<ushort> getSegments(NetNode node)
        {
            List<ushort> segments = new List<ushort>();
            var netManager = Singleton<NetManager>.instance;
            for (ushort i = 0; i < node.CountSegments(); i++)
            {
                segments.Add(node.GetSegment(i));

            }
            return segments;
        }

        public ushort[] getSegmentDirections(ushort nodeID)
        {
            ushort segmentCount = 0;
            Constants.ServiceFactory.NetService.ProcessNode(nodeID, delegate (ushort nId, ref NetNode node)
            {
                segmentCount = (ushort)node.CountSegments();
                return true;
            });
            ushort[] lsrArray = new ushort[segmentCount * 3];
            NodeGeometry nodeGeometry = NodeGeometry.Get(nodeID);
            int counter = 0;
            foreach (SegmentEndGeometry end in nodeGeometry.SegmentEndGeometries)
            {
                if (end.NumLeftSegments > 0)
                {
                    lsrArray[counter * 3] = 1;
                }
                else
                {
                    lsrArray[counter * 3] = 0;
                }
                if (end.NumStraightSegments > 0)
                {
                    lsrArray[counter * 3 + 1] = 1;
                }
                else
                {
                    lsrArray[counter * 3 + 1] = 0;
                }
                if (end.NumRightSegments > 0)
                {
                    lsrArray[counter * 3 + 2] = 1;
                }
                else
                {
                    lsrArray[counter * 3 + 2] = 0;
                }
                counter++;
            }
            return lsrArray;

        }
    }
}