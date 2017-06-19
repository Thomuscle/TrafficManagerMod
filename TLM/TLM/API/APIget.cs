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
       
    }
}
