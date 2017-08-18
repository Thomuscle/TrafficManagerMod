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

namespace TrafficManager.UI.MainMenu {

	public class AlgorithmPanel : UIPanel {
        private static bool _areAllTrafficLightsRed = false;
		
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
        UIButton m_algoButton;
        UIButton m_algoButton2;
        UIButton m_algoButton3;
        UIButton m_testing;
        UIButton m_recording;
        //private UILabel optionsLabel;

        public override void Start() {
            this.relativePosition = new Vector3(15f, 120f);
            isVisible = false;
            m_algoButton = this.AddUIComponent<UIButton>();
            m_algoButton.text = "Toggle Moody Algorithm";
            m_algoButton.normalBgSprite = "SubBarButtonBase";
            m_algoButton.hoveredBgSprite = "SubBarButtonBaseHovered";
            m_algoButton.pressedBgSprite = "SubBarButtonBasePressed";
            m_algoButton.width = 220;
            m_algoButton.height = 30;
            m_algoButton.relativePosition = new Vector3(15f, 20f);
            m_algoButton.eventClick += delegate (UIComponent component, UIMouseEventParameter eventParam) {
                clickToggleAllTrafficLightsMoody(component, eventParam);
            };
            m_algoButton2 = this.AddUIComponent<UIButton>();
            m_algoButton2.text = "Toggle Optimal Algorithm";
            m_algoButton2.normalBgSprite = "SubBarButtonBase";
            m_algoButton2.hoveredBgSprite = "SubBarButtonBaseHovered";
            m_algoButton2.pressedBgSprite = "SubBarButtonBasePressed";
            m_algoButton2.width = 220;
            m_algoButton2.height = 30;
            m_algoButton2.relativePosition = new Vector3(15f, 60f);
            m_algoButton2.eventClick += delegate (UIComponent component, UIMouseEventParameter eventParam) {
                clickToggleAllTrafficLightsMoodyOptimal(component, eventParam);
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
            m_testing.text = "Testing";
            m_testing.normalBgSprite = "SubBarButtonBase";
            m_testing.hoveredBgSprite = "SubBarButtonBaseHovered";
            m_testing.pressedBgSprite = "SubBarButtonBasePressed";
            m_testing.width = 220;
            m_testing.height = 30;
            m_testing.relativePosition = new Vector3(15f, 140f);
            m_testing.eventClick += delegate (UIComponent component, UIMouseEventParameter eventParam) {
                dataRetrievalTesting(component, eventParam);
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
			//optionsLabel = AddUIComponent<OptionsLabel>();

			

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

		internal void SetPosLock(bool lck) {
			Drag.enabled = !lck;
		}

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
        private static void stopRecording(UIComponent component, UIMouseEventParameter eventParam)
        {
                      
            VehicleStateManager vehStateMan = VehicleStateManager.Instance;
            var netManager = Singleton<NetManager>.instance;
            var frame = Singleton<SimulationManager>.instance.m_currentFrameIndex;            
            TrafficLightSimulationManager tlsMan = TrafficLightSimulationManager.Instance;
           
            int totalWaitTime = 0;
            int totalProcessed = 0;
            double avgWaitTime=0.0;
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
                        
                        end.isRecording = false;
                        APIget.isRecording = false;
                        totalWaitTime = totalWaitTime + end.totalWaitTime;
                        totalProcessed = totalProcessed + end.carsProcessed;
                        end.carsProcessed = 0;
                        end.totalWaitTime = 0;

                    }
                    
                }

            }

            if (totalProcessed != 0)
            {
                avgWaitTime = totalWaitTime / totalProcessed;
                DebugOutputPanel.AddMessage(PluginManager.MessageType.Message, " Total Processed: " + totalProcessed);
                DebugOutputPanel.AddMessage(PluginManager.MessageType.Message, " Total wait time: " + totalWaitTime);
                DebugOutputPanel.AddMessage(PluginManager.MessageType.Message, " Average wait time: " + avgWaitTime);
            }
            else
            {
                DebugOutputPanel.AddMessage(PluginManager.MessageType.Message, " ERROR no cars processed");
            }
            APIget.cleanUpJourneyData();
            if(APIget.journeysProcessed != 0)
            {
                DebugOutputPanel.AddMessage(PluginManager.MessageType.Message, " Total Journeys: " + APIget.journeysProcessed);
                DebugOutputPanel.AddMessage(PluginManager.MessageType.Message, " Total Vehicle Journey Time: " + APIget.totalVehicleJourneyTime);
                DebugOutputPanel.AddMessage(PluginManager.MessageType.Message, " Average Journey Time: " + APIget.totalVehicleJourneyTime/ APIget.journeysProcessed);
                DebugOutputPanel.AddMessage(PluginManager.MessageType.Message, " Recording Time: " + APIget.recordingTime);
                APIget.journeysProcessed = 0;
                APIget.totalVehicleJourneyTime = 0;
                APIget.recordingTime = 0;
            }
            else
            {
                DebugOutputPanel.AddMessage(PluginManager.MessageType.Message, " ERROR no journeys processed");
                
                APIget.totalVehicleJourneyTime = 0;
            }
        }
        private static void dataRetrievalTesting(UIComponent component, UIMouseEventParameter eventParam)
        {
            VehicleStateManager vehStateMan = VehicleStateManager.Instance;
            var netManager = Singleton<NetManager>.instance;
            var frame = Singleton<SimulationManager>.instance.m_currentFrameIndex;
            //SegmentEndManager endMan = SegmentEndManager.Instance;
            TrafficLightSimulationManager tlsMan = TrafficLightSimulationManager.Instance;
            // DebugOutputPanel.AddMessage(PluginManager.MessageType.Message, "toggled");
            DebugOutputPanel.AddMessage(PluginManager.MessageType.Message, "Clicked");
            for (ushort i = 0; i < netManager.m_nodes.m_size; i++)
            {
                var node = netManager.m_nodes.m_buffer[i];
                
                var hasLights = ((node.m_flags & NetNode.Flags.TrafficLights) == NetNode.Flags.TrafficLights);

                if (hasLights)
                {

                    NodeGeometry nodeGeometry = NodeGeometry.Get(i);
                    //if (nodeGeometry.NodeId.Equals(20832))
                    //{

                    
                        foreach (SegmentEndGeometry se in nodeGeometry.SegmentEndGeometries)
                        {
                            if (se == null || se.OutgoingOneWay)
                                continue;
                            //DebugOutputPanel.AddMessage(PluginManager.MessageType.Message, "did loop here");
                            SegmentEnd end = SegmentEndManager.Instance.GetSegmentEnd(se.SegmentId, se.StartNode);
                            if (end == null)
                            {
                                //DebugOutputPanel.AddMessage(PluginManager.MessageType.Message, "skip invalid seg");

                                continue; // skip invalid segment
                            }
                            //DebugOutputPanel.AddMessage(PluginManager.MessageType.Message, "DIDNT SKIP");
                            DebugOutputPanel.AddMessage(PluginManager.MessageType.Message, " ID: " + se.SegmentId);
                            string a = end.GetRegisteredVehicleCount().ToString();
                            //DebugOutputPanel.AddMessage(PluginManager.MessageType.Message, "vCount: "+a);
                            if (end.FirstRegisteredVehicleId != 0)
                            {
                                VehicleState state = vehStateMan._GetVehicleState(end.FirstRegisteredVehicleId);
                            if (!state.CheckValidity(ref Singleton<VehicleManager>.instance.m_vehicles.m_buffer[end.FirstRegisteredVehicleId]))
                            {
                                DebugOutputPanel.AddMessage(PluginManager.MessageType.Message, " segID: " + se.SegmentId +" vehID: " + end.FirstRegisteredVehicleId + " type: " + state.VehicleType);
                            }
                                //DebugOutputPanel.AddMessage(PluginManager.MessageType.Message, " segID: " + se.SegmentId);

                            }

                            //DebugOutputPanel.AddMessage(PluginManager.MessageType.Message, "SegmentEndID: "+ se.SegmentId + " RegisteredVehicles: "+ end.GetRegisteredVehicleCount());
                        //}
                    }
                    //APIget.getOrderedSegments(nodeGeometry, out int numSegs);
                }

            }
        }
        private static void clickToggleAllTrafficLights2(UIComponent component, UIMouseEventParameter eventParam)
        {
            var netManager = Singleton<NetManager>.instance;
            var frame = Singleton<SimulationManager>.instance.m_currentFrameIndex;
            RoadBaseAI.TrafficLightState vLightState;
            RoadBaseAI.TrafficLightState pLightState;
            bool vehicles;
            bool pedestrians;
            TrafficLightSimulationManager tlsMan = TrafficLightSimulationManager.Instance;
            // DebugOutputPanel.AddMessage(PluginManager.MessageType.Message, "toggled");
            for (ushort i = 0; i < netManager.m_nodes.m_size; i++)
            {
                var node = netManager.m_nodes.m_buffer[i];
                var hasLights = ((node.m_flags & NetNode.Flags.TrafficLights) == NetNode.Flags.TrafficLights);

                if (hasLights)
                {

                    NodeGeometry nodeGeometry = NodeGeometry.Get(i);

                    //APIget.getOrderedSegments(nodeGeometry, out int numSegs);
                }

            }
        }

        private static void clickToggleAllTrafficLightsMoody(UIComponent component, UIMouseEventParameter eventParam)
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
            // DebugOutputPanel.AddMessage(PluginManager.MessageType.Message, "toggled");
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
                        
                        //DebugOutputPanel.AddMessage(PluginManager.MessageType.Message, "Current Node: " + i);

                        sim.SetupFlexibleTrafficLight(nodeGroup, 0);

                       
                        //NodeGeometry nodeGeometry = NodeGeometry.Get(i);
                        
                        //Instead of next foreach statement API Call to figure out possible steps and add each one of those
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
                                //if (segmentlight.segmentid.equals(28062))
                                //{
                                //    log.info($"here");
                                //    segmentlight.currentmode = customsegmentlight.mode.singleleft;
                                //}
                                sim.FlexibleLight.ChangeLightMode(end.SegmentId, vehicleType, segmentLight.CurrentMode);
                            }

                        }
                        sim.FlexibleLight.Start();
                        Log.Info($"started");
                        

                        //DebugOutputPanel.AddMessage(PluginManager.MessageType.Message, "Is Flexible Light: " + sim.IsFlexibleLight().ToString());


                    }

                }
                else
                {
                    NodeGeometry.Get(i).hasLights = false;
                }

            }
            _areAllTrafficLightsRed = !_areAllTrafficLightsRed;
        }

        private static void clickToggleAllTrafficLightsRR(UIComponent component, UIMouseEventParameter eventParam)
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
            // DebugOutputPanel.AddMessage(PluginManager.MessageType.Message, "toggled");
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

                        //DebugOutputPanel.AddMessage(PluginManager.MessageType.Message, "Current Node: " + i);

                        sim.SetupFlexibleTrafficLight(nodeGroup, 2);


                        //NodeGeometry nodeGeometry = NodeGeometry.Get(i);

                        //Instead of next foreach statement API Call to figure out possible steps and add each one of those
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
                                segmentLight.CurrentMode = CustomSegmentLight.Mode.All;
                                //if (segmentlight.segmentid.equals(28062))
                                //{
                                //    log.info($"here");
                                //    segmentlight.currentmode = customsegmentlight.mode.singleleft;
                                //}
                                sim.FlexibleLight.ChangeLightMode(end.SegmentId, vehicleType, segmentLight.CurrentMode);
                            }

                        }
                        sim.FlexibleLight.Start();
                        Log.Info($"started");


                        //DebugOutputPanel.AddMessage(PluginManager.MessageType.Message, "Is Flexible Light: " + sim.IsFlexibleLight().ToString());


                    }

                }
                else
                {
                    NodeGeometry.Get(i).hasLights = false;
                }

            }
            _areAllTrafficLightsRed = !_areAllTrafficLightsRed;
        }

        private static void clickToggleAllTrafficLightsMoodyOptimal(UIComponent component, UIMouseEventParameter eventParam)
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
            // DebugOutputPanel.AddMessage(PluginManager.MessageType.Message, "toggled");
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

                        //DebugOutputPanel.AddMessage(PluginManager.MessageType.Message, "Current Node: " + i);

                        sim.SetupFlexibleTrafficLight(nodeGroup, 1);


                        //NodeGeometry nodeGeometry = NodeGeometry.Get(i);

                        //Instead of next foreach statement API Call to figure out possible steps and add each one of those
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
                                //if (segmentlight.segmentid.equals(28062))
                                //{
                                //    log.info($"here");
                                //    segmentlight.currentmode = customsegmentlight.mode.singleleft;
                                //}
                                sim.FlexibleLight.ChangeLightMode(end.SegmentId, vehicleType, segmentLight.CurrentMode);
                            }

                        }
                        sim.FlexibleLight.Start();
                        Log.Info($"started");


                        //DebugOutputPanel.AddMessage(PluginManager.MessageType.Message, "Is Flexible Light: " + sim.IsFlexibleLight().ToString());


                    }

                }
                else
                {
                    NodeGeometry.Get(i).hasLights = false;
                }

            }
            _areAllTrafficLightsRed = !_areAllTrafficLightsRed;
        }





        private static void clickToggleAllFlexibleTimedTrafficLights(UIComponent component, UIMouseEventParameter eventParam)
        {
            var netManager = Singleton<NetManager>.instance;
            var frame = Singleton<SimulationManager>.instance.m_currentFrameIndex;
            RoadBaseAI.TrafficLightState vLightState;
            RoadBaseAI.TrafficLightState pLightState;
            bool vehicles;
            bool pedestrians;
            TrafficLightSimulationManager tlsMan = TrafficLightSimulationManager.Instance;
           // DebugOutputPanel.AddMessage(PluginManager.MessageType.Message, "toggled");
            for (ushort i = 0; i < netManager.m_nodes.m_size; i++)
            {
                var node = netManager.m_nodes.m_buffer[i];

                var hasLights = ((node.m_flags & NetNode.Flags.TrafficLights) == NetNode.Flags.TrafficLights);

                if (hasLights)
                {
                    TrafficLightSimulation sim = tlsMan.AddNodeToSimulation(i);
                    if (_areAllTrafficLightsRed)
                    {
                        sim.DestroyFlexibleTrafficLight();
                    }
                    else
                    {

                        List<ushort> nodeGroup = new List<ushort>();
                        nodeGroup.Add(i);
                        //DebugOutputPanel.AddMessage(PluginManager.MessageType.Message, "Current Node: " + i);
                        sim.SetupFlexibleTrafficLight(nodeGroup);
                        NodeGeometry nodeGeometry = NodeGeometry.Get(i);

                        ushort[] segArray = new ushort[nodeGeometry.SegmentEndGeometries.Length];
                        //DebugOutputPanel.AddMessage(PluginManager.MessageType.Message, "length: " + nodeGeometry.SegmentEndGeometries.Length);
                        int i2 = 0;

                        foreach (SegmentEndGeometry end in nodeGeometry.SegmentEndGeometries)
                        {
                            //DebugOutputPanel.AddMessage(PluginManager.MessageType.Message, "got here");
                            if (end == null || end.OutgoingOneWay)
                                continue;
                            
                            segArray[i2] = end.SegmentId;
                            //DebugOutputPanel.AddMessage(PluginManager.MessageType.Message, "self : " + end.SegmentId);
                            //DebugOutputPanel.AddMessage(PluginManager.MessageType.Message, "left segments: " + end.LeftSegments[0]);
                            i2++;
                            
                            
                        }

                        //this doesnt occur, never leaves the above loop
                        //DebugOutputPanel.AddMessage(PluginManager.MessageType.Message, "got out of first for loop");
                        int k = 0;
                        //Instead of next foreach statement API Call to figure out possible steps and add each one of those

                        foreach (SegmentEndGeometry end in nodeGeometry.SegmentEndGeometries)
                        {
                           // DebugOutputPanel.AddMessage(PluginManager.MessageType.Message, "Number of segments: "+ node.CountSegments());
                            if (end == null || end.OutgoingOneWay)
                                continue;
                            ushort[] lsrArray = new ushort[node.CountSegments()*3];
                            
                            for (int j = 0; j <lsrArray.Length; j++)
                            {
                                if(j == k*3 || j == k * 3 + 1 || j == k * 3 + 2)
                                {
                                    lsrArray[j] = 1;
                                }
                                else
                                {
                                    lsrArray[j] = 0;
                                }
                            }
                            //this is not printing
                            //DebugOutputPanel.AddMessage(PluginManager.MessageType.Message, "added a step");
                            sim.FlexibleLight.AddStep(lsrArray, segArray);
                            k++;
                           // DebugOutputPanel.AddMessage(PluginManager.MessageType.Message, lsrArray.ToString());
                        }


                        sim.FlexibleLight.Start();

                        //DebugOutputPanel.AddMessage(PluginManager.MessageType.Message, "Is Flexible Light: " + sim.IsFlexibleLight().ToString());

                    }


                }

            }
            _areAllTrafficLightsRed = !_areAllTrafficLightsRed;
        }
    
        private static void clickToggleAllTimedTrafficLights(UIComponent component, UIMouseEventParameter eventParam)
        {
            
            var netManager = Singleton<NetManager>.instance;
            var frame = Singleton<SimulationManager>.instance.m_currentFrameIndex;
            RoadBaseAI.TrafficLightState vLightState;
            RoadBaseAI.TrafficLightState pLightState;
            bool vehicles;
            bool pedestrians;
            TrafficLightSimulationManager tlsMan = TrafficLightSimulationManager.Instance;
            DebugOutputPanel.AddMessage(PluginManager.MessageType.Message, "start");
            for (ushort i = 0; i < netManager.m_nodes.m_size; i++)
            {
                var node = netManager.m_nodes.m_buffer[i];

                var hasLights = ((node.m_flags & NetNode.Flags.TrafficLights) == NetNode.Flags.TrafficLights);

                if (hasLights)
                {
                    TrafficLightSimulation sim = tlsMan.AddNodeToSimulation(i);
                    if (_areAllTrafficLightsRed)
                    {
                        sim.DestroyTimedTrafficLight();
                    }
                    else
                    {

                        List<ushort> nodeGroup = new List<ushort>();
                        nodeGroup.Add(i);
                        //DebugOutputPanel.AddMessage(PluginManager.MessageType.Message, "Current Node: " + i);
                        sim.SetupTimedTrafficLight(nodeGroup);
                        NodeGeometry nodeGeometry = NodeGeometry.Get(i);


                        foreach (SegmentEndGeometry end in nodeGeometry.SegmentEndGeometries)
                        {
                            if (end == null || end.OutgoingOneWay)
                                continue;

                            sim.TimedLight.AddStep(5, 5, 1f, end.SegmentId);
                        }


                        sim.TimedLight.Start();
                    }
                    

                }

            }
            _areAllTrafficLightsRed = !_areAllTrafficLightsRed;

        }
    }


}
