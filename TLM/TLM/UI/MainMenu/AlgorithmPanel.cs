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

namespace TrafficManager.UI.MainMenu {

	public class AlgorithmPanel : UIPanel {
        private static bool _areAllTrafficLightsRed = false;
		
		private const int NUM_BUTTONS_PER_ROW = 6;
		private const int NUM_ROWS = 2;

		public const int VSPACING = 5;
		public const int HSPACING = 5;
		public const int TOP_BORDER = 25;
		public const int BUTTON_SIZE = 30;
		public const int MENU_WIDTH = 215;
		public const int MENU_HEIGHT = 95;

		public MenuButton[] Buttons { get; private set; }
		public UILabel VersionLabel { get; private set; }

		public UIDragHandle Drag { get; private set; }
        UIButton m_algoButton;
        //private UILabel optionsLabel;

        public override void Start() {
			isVisible = false;
            m_algoButton = this.AddUIComponent<UIButton>();
            m_algoButton.text = "Algorithm Button";
            m_algoButton.normalBgSprite = "buttonclose";
            m_algoButton.hoveredBgSprite = "buttonclosehover";
            m_algoButton.pressedBgSprite = "buttonclosepressed";
            m_algoButton.eventClick += delegate (UIComponent component, UIMouseEventParameter eventParam) {
                clickToggleAllTrafficLights(component, eventParam);
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
				Log._Debug($"Menu position changed to {absolutePosition.x}|{absolutePosition.y}");

				config.MainMenuX = (int)absolutePosition.x;
				config.MainMenuY = (int)absolutePosition.y;

				GlobalConfig.WriteConfig();
			}
			base.OnPositionChanged();
		}
        private static void clickToggleAllTrafficLights(UIComponent component, UIMouseEventParameter eventParam)
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