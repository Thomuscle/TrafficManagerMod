#define QUEUEDSTATSx
#define EXTRAPFx

using System;
using System.Linq;
using ColossalFramework;
using ColossalFramework.UI;
using TrafficManager.Geometry;
using TrafficManager.TrafficLight;
using UnityEngine;
using TrafficManager.State;
using TrafficManager.Custom.PathFinding;
using System.Collections.Generic;
using TrafficManager.Manager;
using CSUtil.Commons;
using ColossalFramework.Plugins;
using TrafficManager.API;
using TrafficManager.Traffic;
using System.Text;
using System.IO;

namespace TrafficManager.UI.MainMenu {

    //This is the UIPanel class for the traffic algorithm application and data recording panel.
    //This class contains functions that are executed when buttons on the panel are clicked. 
	public class AlgorithmPanel : UIPanel {
        private static bool _areAllTrafficLightsRed = false;
        private static List<NodeRecordingObject> nodeDataObjects =new List<NodeRecordingObject>();
        private const int NUM_BUTTONS_PER_ROW = 6;
		private const int NUM_ROWS = 2;

		public const int VSPACING = 5;
		public const int HSPACING = 5;
		public const int TOP_BORDER = 25;
		public const int BUTTON_SIZE = 30;
		public const int MENU_WIDTH = 250;
		public const int MENU_HEIGHT = 250;

		public MenuButton[] Buttons { get; private set; }
		public UILabel VersionLabel { get; private set; }

		public UIDragHandle Drag { get; private set; }
        public static object NodeDataObjects { get; private set; }

        UIButton m_algoButton;
        UIButton m_algoButton2;
        UIButton m_algoButton3;
        UIButton m_testing;
        UIButton m_recording;

        //Sets up the panel visuals and links functions to buttons. 
        public override void Start() {
            this.relativePosition = new Vector3(15f, 120f);
            isVisible = false;
            m_algoButton = this.AddUIComponent<UIButton>();
            m_algoButton.text = "Toggle AWAITS";
            m_algoButton.normalBgSprite = "SubBarButtonBase";
            m_algoButton.hoveredBgSprite = "SubBarButtonBaseHovered";
            m_algoButton.pressedBgSprite = "SubBarButtonBasePressed";
            m_algoButton.width = 220;
            m_algoButton.height = 30;
            m_algoButton.relativePosition = new Vector3(15f, 20f);
            m_algoButton.eventClick += delegate (UIComponent component, UIMouseEventParameter eventParam) {
                clickToggleAllTrafficLightsAWAITS(component, eventParam);
            };
            m_algoButton2 = this.AddUIComponent<UIButton>();
            m_algoButton2.text = "Toggle AWAITS++";
            m_algoButton2.normalBgSprite = "SubBarButtonBase";
            m_algoButton2.hoveredBgSprite = "SubBarButtonBaseHovered";
            m_algoButton2.pressedBgSprite = "SubBarButtonBasePressed";
            m_algoButton2.width = 220;
            m_algoButton2.height = 30;
            m_algoButton2.relativePosition = new Vector3(15f, 60f);
            m_algoButton2.eventClick += delegate (UIComponent component, UIMouseEventParameter eventParam) {
                clickToggleAllTrafficLightsAWAITSPlusPlus(component, eventParam);
            };
            m_algoButton3 = this.AddUIComponent<UIButton>();
            m_algoButton3.text = "Toggle Round Robin";
            m_algoButton3.normalBgSprite = "SubBarButtonBase";
            m_algoButton3.hoveredBgSprite = "SubBarButtonBaseHovered";
            m_algoButton3.pressedBgSprite = "SubBarButtonBasePressed";
            m_algoButton3.width = 220;
            m_algoButton3.height = 30;
            m_algoButton3.relativePosition = new Vector3(15f, 100f);
            m_algoButton3.eventClick += delegate (UIComponent component, UIMouseEventParameter eventParam) {
                clickToggleAllTrafficLightsRR(component, eventParam);
            };
            m_testing = this.AddUIComponent<UIButton>();
            m_testing.text = "Toggle My ATCS";
            m_testing.normalBgSprite = "SubBarButtonBase";
            m_testing.hoveredBgSprite = "SubBarButtonBaseHovered";
            m_testing.pressedBgSprite = "SubBarButtonBasePressed";
            m_testing.width = 220;
            m_testing.height = 30;
            m_testing.relativePosition = new Vector3(15f, 140f);
            m_testing.eventClick += delegate (UIComponent component, UIMouseEventParameter eventParam) {
                toggleMyATCS(component, eventParam);
            };

            m_recording = this.AddUIComponent<UIButton>();
            m_recording.text = "Record";

             
            m_recording.normalBgSprite = "SubBarButtonBase";
            m_recording.hoveredBgSprite = "SubBarButtonBaseHovered";
            m_recording.pressedBgSprite = "SubBarButtonBasePressed";
            m_recording.width = 220;
            m_recording.height = 30;
            m_recording.relativePosition = new Vector3(15f, 180f);
            m_recording.eventClick += delegate (UIComponent component, UIMouseEventParameter eventParam) {
                if (m_recording.text.Equals("Record"))
                {                    
                    startRecording(component, eventParam);
                    m_recording.text = "Stop";
                }
                else
                {                    
                    stopRecording(component, eventParam);
                    m_recording.text = "Record";

                }
                
            };
            backgroundSprite = "GenericPanel";
			color = new Color32(64, 64, 64, 240);
			width = MENU_WIDTH;
			height = MENU_HEIGHT;

			VersionLabel = AddUIComponent<VersionLabel>();

			GlobalConfig config = GlobalConfig.Instance;
			Vector3 pos = new Vector3(config.MainMenuX, config.MainMenuY);
			VectorUtil.ClampPosToScreen(ref pos);
			absolutePosition = pos;

			var dragHandler = new GameObject("TMPE_Menu_DragHandler");
			dragHandler.transform.parent = transform;
			dragHandler.transform.localPosition = Vector3.zero;
			Drag = dragHandler.AddComponent<UIDragHandle>();

			Drag.width = width;
			Drag.height = TOP_BORDER;
			Drag.enabled = !GlobalConfig.Instance.MainMenuPosLocked;
		}
    
        //Sets whether the menu is locked in place.
		internal void SetPosLock(bool lck) {
			Drag.enabled = !lck;
		}

        //Updates values when the menu is moved on the screen.
		protected override void OnPositionChanged() {
			GlobalConfig config = GlobalConfig.Instance;

			bool posChanged = (config.MainMenuX != (int)absolutePosition.x || config.MainMenuY != (int)absolutePosition.y);

			if (posChanged) {
				//Log._Debug($"Menu position changed to {absolutePosition.x}|{absolutePosition.y}");

				config.MainMenuX = (int)absolutePosition.x;
				config.MainMenuY = (int)absolutePosition.y;

				GlobalConfig.WriteConfig();
			}
			base.OnPositionChanged();
		}

        //Updates the timer on the recording button. 
        public void updateRecordingTime()
        {
            m_recording.text = "Stop("+(APIget.recordingTime.ToString())+")";
        }
        public static int recording = 0;
        private static void startRecording(UIComponent component, UIMouseEventParameter eventParam)
        {

            APIget.firstIteration = true;
            VehicleStateManager vehStateMan = VehicleStateManager.Instance;
            var netManager = Singleton<NetManager>.instance;
            var frame = Singleton<SimulationManager>.instance.m_currentFrameIndex;
            TrafficLightSimulationManager tlsMan = TrafficLightSimulationManager.Instance;
            
            for (ushort i = 0; i < netManager.m_nodes.m_size; i++)
            {
                var node = netManager.m_nodes.m_buffer[i];
                var hasLights = ((node.m_flags & NetNode.Flags.TrafficLights) == NetNode.Flags.TrafficLights);
                if (hasLights)
                {

                    NodeGeometry nodeGeometry = NodeGeometry.Get(i);
                    if (nodeGeometry.NumSegmentEnds > 4 || nodeGeometry.SegmentEndGeometries[0].NumRightSegments > 1 || nodeGeometry.SegmentEndGeometries[0].NumLeftSegments > 1 || nodeGeometry.SegmentEndGeometries[0].NumStraightSegments > 1)
                    {
                        continue;
                    }
                    foreach (SegmentEndGeometry se in nodeGeometry.SegmentEndGeometries)
                    {
                        if (se == null || se.OutgoingOneWay)
                            continue;

                        SegmentEnd end = SegmentEndManager.Instance.GetSegmentEnd(se.SegmentId, se.StartNode);
                        if (end == null)
                        {
                            continue;
                        }

                        end.isRecording = true;
                        APIget.isRecording = true;
                    }

                }

            }
        }

        //Adds node information to the list of node data objects once recording is finished.
        public static void SetNodeData(NodeGeometry g,int longestWait)
        {
            NodeRecordingObject n = new NodeRecordingObject();
            foreach (SegmentEndGeometry se in g.SegmentEndGeometries)
            {
                if (se == null)
                    continue;
                if (se.OutgoingOneWay)
                {
                    n.noOfOutgoingOneWays++;
                    continue;
                }
                SegmentEnd end = SegmentEndManager.Instance.GetSegmentEnd(se.SegmentId, se.StartNode);
                if (end == null)
                {
                    continue;
                }
                
                n.totalWaitingTime += end.totalWaitTime;
                n.totalVehiclesProcessed += end.carsProcessed;
                if (se.IncomingOneWay)
                {
                    n.noOFIncomingOneWays++;
                }
                end.carsProcessed = 0;
                end.totalWaitTime = 0;

            }

            n.avergaeWaitTime = (double)n.totalWaitingTime / (double)n.totalVehiclesProcessed;

            n.nodeID = g.NodeId;
            n.noOfSegments = g.NumSegmentEnds;
            n.longestWait = longestWait;
            
            nodeDataObjects.Add(n);
        }
        
        //Function that executes when the user toggles recording off.
        private static void stopRecording(UIComponent component, UIMouseEventParameter eventParam)
        {
            StringBuilder csv = new StringBuilder();
            string headings = "Total Wait Time,Cars Processed,Average Wait Time At An Intersection,Number Of Journeys Processed,Total Journey Time,Average Journey Time,Recording Duration";
            String data="";
            csv.AppendLine(headings);
            VehicleStateManager vehStateMan = VehicleStateManager.Instance;
            var netManager = Singleton<NetManager>.instance;
            var frame = Singleton<SimulationManager>.instance.m_currentFrameIndex;            
            TrafficLightSimulationManager tlsMan = TrafficLightSimulationManager.Instance;
            
            int totalWaitTime = 0;
            int totalProcessed = 0;
            double avgWaitTime=0.0;
            for (ushort i = 0; i < netManager.m_nodes.m_size; i++)
            {
                int longestWait = 0;
                int shortestWait = 0;
                var node = netManager.m_nodes.m_buffer[i];
                var hasLights = ((node.m_flags & NetNode.Flags.TrafficLights) == NetNode.Flags.TrafficLights);
                if (hasLights)
                {
                    
                    NodeGeometry nodeGeometry = NodeGeometry.Get(i);
                    if (nodeGeometry.NumSegmentEnds > 4 || nodeGeometry.SegmentEndGeometries[0].NumRightSegments > 1 || nodeGeometry.SegmentEndGeometries[0].NumLeftSegments > 1 || nodeGeometry.SegmentEndGeometries[0].NumStraightSegments > 1)
                    {
                        continue;
                    }
                    
                    foreach (SegmentEndGeometry se in nodeGeometry.SegmentEndGeometries)
                    {
                        if (se == null || se.OutgoingOneWay)
                            
                            continue;
                        
                        SegmentEnd end = SegmentEndManager.Instance.GetSegmentEnd(se.SegmentId, se.StartNode);
                        if (end == null && !end.isRecording)
                        {
                            continue; 
                        }
                        
                        if (longestWait < end.longestWaitingCar)
                        {
                            longestWait = end.longestWaitingCar;
                        }
                        end.GetRegisteredVehicleCount();
                        end.isRecording = false;
                        APIget.isRecording = false;
                        totalWaitTime = totalWaitTime + end.totalWaitTime;
                        totalProcessed = totalProcessed + end.carsProcessed;
                    }

                    if(longestWait> APIget.recordingTime)
                    {
                        longestWait = APIget.recordingTime;
                    }
                    SetNodeData(nodeGeometry,longestWait);

                }

            }

            if (totalProcessed != 0)
            {
                avgWaitTime = ((double)totalWaitTime) / ((double)totalProcessed);
                DebugOutputPanel.AddMessage(PluginManager.MessageType.Message, " Total Processed: " + totalProcessed);
                DebugOutputPanel.AddMessage(PluginManager.MessageType.Message, " Total wait time: " + totalWaitTime);
                DebugOutputPanel.AddMessage(PluginManager.MessageType.Message, " Average wait time: " + avgWaitTime);
                data = totalWaitTime + "," + totalProcessed + "," + avgWaitTime;
            }
            else
            {
                DebugOutputPanel.AddMessage(PluginManager.MessageType.Message, " ERROR no cars processed");
            }
            APIget.cleanUpJourneyData();
            DebugOutputPanel.AddMessage(PluginManager.MessageType.Message, " Recording Time: " + APIget.recordingTime);
            
            if (APIget.journeysProcessed != 0)
            {
                DebugOutputPanel.AddMessage(PluginManager.MessageType.Message, " Total Journeys: " + APIget.journeysProcessed);
                DebugOutputPanel.AddMessage(PluginManager.MessageType.Message, " Total Vehicle Journey Time: " + APIget.totalVehicleJourneyTime);
                DebugOutputPanel.AddMessage(PluginManager.MessageType.Message, " Average Journey Time: " + APIget.totalVehicleJourneyTime/ APIget.journeysProcessed);
                data = data + "," + APIget.journeysProcessed + "," + APIget.totalVehicleJourneyTime + "," + ((double)APIget.totalVehicleJourneyTime / (double)APIget.journeysProcessed)+","+ APIget.recordingTime;
                APIget.journeysProcessed = 0;
                APIget.totalVehicleJourneyTime = 0;
                APIget.recordingTime = 0;
            }
            else
            {
                DebugOutputPanel.AddMessage(PluginManager.MessageType.Message, " ERROR no journeys processed");
                
                APIget.totalVehicleJourneyTime = 0;
            }
            API.APIget.recordingTime = 0;
            string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),"data.csv");
            csv.AppendLine(data);
            csv.AppendLine("");
            
            headings = "Intersection ID,Number Of Segments,Number Of Outgoing One Way Segments,Number Of Incoming One Way Segments,Total Time Waiting At Intersection,Number Of Vehicles Processed,Average Wait Time At Intersection,Longest Wait Time At Intersection";
            data = "";
            csv.AppendLine(headings);
            for (int l = 0; l < nodeDataObjects.Count; l++)
            {
                csv.AppendLine(nodeDataObjects[l].nodeID + "," + nodeDataObjects[l].noOfSegments + "," + nodeDataObjects[l].noOfOutgoingOneWays + "," + nodeDataObjects[l].noOFIncomingOneWays + "," + nodeDataObjects[l].totalWaitingTime + "," + nodeDataObjects[l].totalVehiclesProcessed + "," + nodeDataObjects[l].avergaeWaitTime + "," + nodeDataObjects[l].longestWait);
            }

            File.WriteAllText(path,csv.ToString());
            nodeDataObjects.Clear();
        }
    

        //Function that executes if AWAITS algorithms is toggled.  
        private static void clickToggleAllTrafficLightsAWAITS(UIComponent component, UIMouseEventParameter eventParam)
        {
            var netManager = Singleton<NetManager>.instance;
            var frame = Singleton<SimulationManager>.instance.m_currentFrameIndex;
            CustomSegmentLightsManager customTrafficLightsManager = CustomSegmentLightsManager.Instance;
            bool firstMaster = true;
            TrafficLightSimulationManager tlsMan = TrafficLightSimulationManager.Instance;

            for (ushort i = 0; i < netManager.m_nodes.m_size; i++)
            {
                
                var node = netManager.m_nodes.m_buffer[i];

                var hasLights = ((node.m_flags & NetNode.Flags.TrafficLights) == NetNode.Flags.TrafficLights);

                if (hasLights)
                {
                    NodeGeometry nodeGeometry = NodeGeometry.Get(i);
                    nodeGeometry.hasLights = true;
                    
                    if (nodeGeometry.NumSegmentEnds >4 || nodeGeometry.SegmentEndGeometries[0].NumRightSegments>1 || nodeGeometry.SegmentEndGeometries[0].NumLeftSegments > 1 || nodeGeometry.SegmentEndGeometries[0].NumStraightSegments > 1)
                    {
                        continue;
                    }
                    if (firstMaster)
                    {
                        nodeGeometry.isMaster = true;
                        firstMaster = false;
                    }
                    TrafficLightSimulation sim = tlsMan.AddNodeToSimulation(i);
                    if (_areAllTrafficLightsRed)
                    {
                        sim.DestroyFlexibleTrafficLight();
                    }
                    else
                    {

                        List<ushort> nodeGroup = new List<ushort>();
                        nodeGroup.Add(i);

                        sim.SetupFlexibleTrafficLight(nodeGroup, 0);
                        
                        ushort[] segArray;
                        List<Phase> phases = APIget.buildOrderedPhases(nodeGeometry, out segArray);
                        

                        foreach (Phase phase in phases)
                        {
                            
                            ushort[] rslArray = phase.getRslArray(segArray, nodeGeometry);
                            
                            
                            sim.FlexibleLight.AddStep(rslArray, segArray);
                            
                        }
                        foreach (SegmentEndGeometry end in nodeGeometry.SegmentEndGeometries)
                        {
                            if (end == null || end.OutgoingOneWay)
                                continue;
                            var segmentLights = customTrafficLightsManager.GetSegmentLights(end.SegmentId, end.StartNode);
                            
                            foreach (Traffic.ExtVehicleType vehicleType in segmentLights.VehicleTypes)
                            {
                                CustomSegmentLight segmentLight = segmentLights.GetCustomLight(vehicleType);
                                segmentLight.CurrentMode = CustomSegmentLight.Mode.All;
                                sim.FlexibleLight.ChangeLightMode(end.SegmentId, vehicleType, segmentLight.CurrentMode);
                            }

                        }
                        sim.FlexibleLight.Start();
                        Log.Info($"started");
                    }

                }
                else
                {
                    NodeGeometry.Get(i).hasLights = false;
                }

            }
            _areAllTrafficLightsRed = !_areAllTrafficLightsRed;
        }


        //Function that is executed when Round Robin algorithm is toggled. 
        private static void clickToggleAllTrafficLightsRR(UIComponent component, UIMouseEventParameter eventParam)
        {
            var netManager = Singleton<NetManager>.instance;
            var frame = Singleton<SimulationManager>.instance.m_currentFrameIndex;
            CustomSegmentLightsManager customTrafficLightsManager = CustomSegmentLightsManager.Instance;

            bool firstMaster = true;
            TrafficLightSimulationManager tlsMan = TrafficLightSimulationManager.Instance;
            for (ushort i = 0; i < netManager.m_nodes.m_size; i++)
            {

                var node = netManager.m_nodes.m_buffer[i];

                var hasLights = ((node.m_flags & NetNode.Flags.TrafficLights) == NetNode.Flags.TrafficLights);

                if (hasLights)
                {
                    NodeGeometry nodeGeometry = NodeGeometry.Get(i);
                    nodeGeometry.hasLights = true;
                    if (nodeGeometry.NumSegmentEnds > 4 || nodeGeometry.SegmentEndGeometries[0].NumRightSegments > 1 || nodeGeometry.SegmentEndGeometries[0].NumLeftSegments > 1 || nodeGeometry.SegmentEndGeometries[0].NumStraightSegments > 1)
                    {
                        continue;
                    }
                    if (firstMaster)
                    {
                        nodeGeometry.isMaster = true;
                        firstMaster = false;
                    }
                    TrafficLightSimulation sim = tlsMan.AddNodeToSimulation(i);
                    if (_areAllTrafficLightsRed)
                    {
                        sim.DestroyFlexibleTrafficLight();
                    }
                    else
                    {

                        List<ushort> nodeGroup = new List<ushort>();
                        nodeGroup.Add(i);

                        sim.SetupFlexibleTrafficLight(nodeGroup, 2);

                        ushort[] segArray;
                        List<Phase> phases = APIget.buildPhasesNoRedundancy(nodeGeometry, out segArray);


                        foreach (Phase phase in phases)
                        {

                            ushort[] rslArray = phase.getRslArray(segArray, nodeGeometry);


                            sim.FlexibleLight.AddStep(rslArray, segArray);

                        }
                        foreach (SegmentEndGeometry end in nodeGeometry.SegmentEndGeometries)
                        {
                            if (end == null || end.OutgoingOneWay)
                                continue;
                            var segmentLights = customTrafficLightsManager.GetSegmentLights(end.SegmentId, end.StartNode);

                            foreach (Traffic.ExtVehicleType vehicleType in segmentLights.VehicleTypes)
                            {
                                CustomSegmentLight segmentLight = segmentLights.GetCustomLight(vehicleType);

                                sim.FlexibleLight.ChangeLightMode(end.SegmentId, vehicleType, segmentLight.CurrentMode);
                            }

                        }
                        sim.FlexibleLight.Start();
                        Log.Info($"started");
                    }

                }
                else
                {
                    NodeGeometry.Get(i).hasLights = false;
                }

            }
            _areAllTrafficLightsRed = !_areAllTrafficLightsRed;
        }



        //Function that is executed when AWAITS++ is toggled. 
        private static void clickToggleAllTrafficLightsAWAITSPlusPlus(UIComponent component, UIMouseEventParameter eventParam)
        {
            var netManager = Singleton<NetManager>.instance;
            var frame = Singleton<SimulationManager>.instance.m_currentFrameIndex;
            CustomSegmentLightsManager customTrafficLightsManager = CustomSegmentLightsManager.Instance;
            RoadBaseAI.TrafficLightState vLightState;
            RoadBaseAI.TrafficLightState pLightState;
            bool vehicles;
            bool pedestrians;
            bool firstMaster = true;
            TrafficLightSimulationManager tlsMan = TrafficLightSimulationManager.Instance;
            for (ushort i = 0; i < netManager.m_nodes.m_size; i++)
            {

                var node = netManager.m_nodes.m_buffer[i];

                var hasLights = ((node.m_flags & NetNode.Flags.TrafficLights) == NetNode.Flags.TrafficLights);

                if (hasLights)
                {
                    NodeGeometry nodeGeometry = NodeGeometry.Get(i);
                    nodeGeometry.hasLights = true;
                    if (nodeGeometry.NumSegmentEnds > 4 || nodeGeometry.SegmentEndGeometries[0].NumRightSegments > 1 || nodeGeometry.SegmentEndGeometries[0].NumLeftSegments > 1 || nodeGeometry.SegmentEndGeometries[0].NumStraightSegments > 1)
                    {
                        continue;
                    }
                    if (firstMaster)
                    {
                        nodeGeometry.isMaster = true;
                        firstMaster = false;
                    }
                    TrafficLightSimulation sim = tlsMan.AddNodeToSimulation(i);
                    if (_areAllTrafficLightsRed)
                    {
                        sim.DestroyFlexibleTrafficLight();
                    }
                    else
                    {

                        List<ushort> nodeGroup = new List<ushort>();
                        nodeGroup.Add(i);

                        sim.SetupFlexibleTrafficLight(nodeGroup, 1);
                        
                        ushort[] segArray;
                        List<Phase> phases = APIget.buildOrderedPhases(nodeGeometry, out segArray);


                        foreach (Phase phase in phases)
                        {

                            ushort[] rslArray = phase.getRslArray(segArray, nodeGeometry);


                            sim.FlexibleLight.AddStep(rslArray, segArray);

                        }
                        foreach (SegmentEndGeometry end in nodeGeometry.SegmentEndGeometries)
                        {
                            if (end == null || end.OutgoingOneWay)
                                continue;
                            var segmentLights = customTrafficLightsManager.GetSegmentLights(end.SegmentId, end.StartNode);

                            foreach (Traffic.ExtVehicleType vehicleType in segmentLights.VehicleTypes)
                            {
                                CustomSegmentLight segmentLight = segmentLights.GetCustomLight(vehicleType);
                                segmentLight.CurrentMode = CustomSegmentLight.Mode.All;

                                sim.FlexibleLight.ChangeLightMode(end.SegmentId, vehicleType, segmentLight.CurrentMode);
                            }

                        }
                        sim.FlexibleLight.Start();
                        Log.Info($"started");
                        
                    }

                }
                else
                {
                    NodeGeometry.Get(i).hasLights = false;
                }

            }
            _areAllTrafficLightsRed = !_areAllTrafficLightsRed;
        }



        //Function that executes when the Toggle My ATCS button is clicked. You are most welcome to change this function and use it as
        // a guide to make more buttons. Currently this function transforms every traffic light so that they will call APIget.getNextIndexMyATCS()
        // every second, which currently does nothing, so the traffic lights will not work. 
        private static void toggleMyATCS(UIComponent component, UIMouseEventParameter eventParam)
        {
            var netManager = Singleton<NetManager>.instance;
            var frame = Singleton<SimulationManager>.instance.m_currentFrameIndex;
            CustomSegmentLightsManager customTrafficLightsManager = CustomSegmentLightsManager.Instance;

            bool firstMaster = true;
            TrafficLightSimulationManager tlsMan = TrafficLightSimulationManager.Instance;

            for (ushort i = 0; i < netManager.m_nodes.m_size; i++)
            {

                var node = netManager.m_nodes.m_buffer[i];

                var hasLights = ((node.m_flags & NetNode.Flags.TrafficLights) == NetNode.Flags.TrafficLights);

                if (hasLights)
                {
                    NodeGeometry nodeGeometry = NodeGeometry.Get(i);
                    nodeGeometry.hasLights = true;
                    if (nodeGeometry.NumSegmentEnds > 4 || nodeGeometry.SegmentEndGeometries[0].NumRightSegments > 1 || nodeGeometry.SegmentEndGeometries[0].NumLeftSegments > 1 || nodeGeometry.SegmentEndGeometries[0].NumStraightSegments > 1)
                    {
                        continue;
                    }
                    if (firstMaster)
                    {
                        nodeGeometry.isMaster = true;
                        firstMaster = false;
                    }
                    TrafficLightSimulation sim = tlsMan.AddNodeToSimulation(i);
                    if (_areAllTrafficLightsRed)
                    {
                        sim.DestroyFlexibleTrafficLight();
                    }
                    else
                    {


                        List<ushort> nodeGroup = new List<ushort>();
                        nodeGroup.Add(i);

                        //Selected Algorithm must be 3 for APIget.getNextIndexMyATCS() to be called every second.
                        sim.SetupFlexibleTrafficLight(nodeGroup, 3);


                        ushort[] segArray;

                        //CAN CHANGE!!!
                        //This will generate all the possible phases as a node without redundant phases. If you want your own
                        // list of phases, simply call a new function instead of APIget.buildPhasesNoRedundancy(). 
                        List<Phase> phases = APIget.buildPhasesNoRedundancy(nodeGeometry, out segArray);


                        foreach (Phase phase in phases)
                        {

                            ushort[] rslArray = phase.getRslArray(segArray, nodeGeometry);


                            sim.FlexibleLight.AddStep(rslArray, segArray);

                        }
                        foreach (SegmentEndGeometry end in nodeGeometry.SegmentEndGeometries)
                        {
                            if (end == null || end.OutgoingOneWay)
                                continue;
                            var segmentLights = customTrafficLightsManager.GetSegmentLights(end.SegmentId, end.StartNode);

                            foreach (Traffic.ExtVehicleType vehicleType in segmentLights.VehicleTypes)
                            {
                                CustomSegmentLight segmentLight = segmentLights.GetCustomLight(vehicleType);
                                segmentLight.CurrentMode = CustomSegmentLight.Mode.All;

                                sim.FlexibleLight.ChangeLightMode(end.SegmentId, vehicleType, segmentLight.CurrentMode);
                            }

                        }
                        sim.FlexibleLight.Start();
                        Log.Info($"started");
                    }

                }
                else
                {
                    NodeGeometry.Get(i).hasLights = false;
                }

            }
            _areAllTrafficLightsRed = !_areAllTrafficLightsRed;
        }

    }
}
