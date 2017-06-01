﻿#define DEBUGVISUALSx

using System;
using System.Collections.Generic;
using ColossalFramework;
using TrafficManager.Geometry;
using UnityEngine;
using TrafficManager.Custom.AI;
using CSUtil.Commons;
using TrafficManager.State;

namespace TrafficManager.TrafficLight {
	/// <summary>
	/// Represents the traffic light (left, forward, right) at a specific segment end
	/// </summary>
	public class CustomSegmentLight : ICloneable {
		public enum Mode {
			Simple = 1, // <^>
			SingleLeft = 2, // <, ^>
			SingleRight = 3, // <^, >
			All = 4 // <, ^, >
		}

		[Obsolete]
		public ushort NodeId {
			get {
				return lights.NodeId;
			}
		}

		public ushort SegmentId {
			get {
				return lights.SegmentId;
			}
		}

		public bool StartNode {
			get {
				return lights.StartNode;
			}
		}

		public short ClockwiseIndex {
			get {
				return lights.ClockwiseIndex;
			}
		}

		public Mode CurrentMode {
			get { return currentMode; }
			set {
				if (currentMode == value)
					return;

				currentMode = value;
				EnsureModeLights();
			}
		}
		internal Mode currentMode = Mode.Simple;

		internal RoadBaseAI.TrafficLightState leftLight;
		internal RoadBaseAI.TrafficLightState mainLight;
		internal RoadBaseAI.TrafficLightState rightLight;

		public RoadBaseAI.TrafficLightState LightLeft {
			get { return leftLight; }
            set
            {
                if (leftLight == value)
                    return;

                leftLight = value;
                lights.OnChange();
            }
        }

		public RoadBaseAI.TrafficLightState LightMain {
			get { return mainLight; }
            set
            {
                if (mainLight == value)
                    return;

                mainLight = value;
                lights.OnChange();
            }
        }
		public RoadBaseAI.TrafficLightState LightRight {
			get { return rightLight; }
            set
            {
                if (rightLight == value)
                    return;

                rightLight = value;
                lights.OnChange();
            }
        }

		CustomSegmentLights lights;

		public override string ToString() {
			return $"[CustomSegmentLight seg. {SegmentId} @ node {NodeId}\n" +
			"\t" + $"CurrentMode: {CurrentMode}\n" +
			"\t" + $"LightLeft: {LightLeft}\n" +
			"\t" + $"LightMain: {LightMain}\n" +
			"\t" + $"LightRight: {LightRight}\n" +
			"CustomSegmentLight]";
		}

		private void EnsureModeLights() {
			bool changed = false;

			switch (currentMode) {
				case Mode.Simple:
					if (leftLight != LightMain) {
						leftLight = LightMain;
						changed = true;
					}
					if (rightLight != LightMain) {
						rightLight = LightMain;
						changed = true;
					}
					break;
				case Mode.SingleLeft:
					if (rightLight != LightMain) {
						rightLight = LightMain;
						changed = true;
					}
					break;
				case Mode.SingleRight:
					if (leftLight != LightMain) {
						leftLight = LightMain;
						changed = true;
					}
					break;
			}

			if (changed)
				lights.OnChange();
		}

		public CustomSegmentLight(CustomSegmentLights lights, RoadBaseAI.TrafficLightState mainLight) {
			this.lights = lights;

			SetStates(mainLight, leftLight, rightLight);
			UpdateVisuals();
		}

		public CustomSegmentLight(CustomSegmentLights lights, RoadBaseAI.TrafficLightState mainLight, RoadBaseAI.TrafficLightState leftLight, RoadBaseAI.TrafficLightState rightLight/*, RoadBaseAI.TrafficLightState pedestrianLight*/) {
			this.lights = lights;

			SetStates(mainLight, leftLight, rightLight);

			UpdateVisuals();
		}

		public void ToggleMode() {
			SegmentGeometry geometry = SegmentGeometry.Get(SegmentId);

			if (geometry == null) {
				Log.Error($"CustomSegmentLight.ToggleMode: No geometry information available for segment {SegmentId}");
				return;
			}

			bool startNode = lights.StartNode;
			var hasLeftSegment = geometry.HasOutgoingLeftSegment(startNode);
			var hasForwardSegment = geometry.HasOutgoingStraightSegment(startNode);
			var hasRightSegment = geometry.HasOutgoingRightSegment(startNode);

#if DEBUG
			Log._Debug($"ChangeMode. segment {SegmentId} @ node {NodeId}, hasOutgoingLeft={hasLeftSegment}, hasOutgoingStraight={hasForwardSegment}, hasOutgoingRight={hasRightSegment}");
#endif

			Mode newMode = Mode.Simple;
			if (CurrentMode == Mode.Simple) {
				if (!hasLeftSegment) {
					newMode = Mode.SingleRight;
				} else {
					newMode = Mode.SingleLeft;
				}
			} else if (CurrentMode == Mode.SingleLeft) {
				if (!hasForwardSegment || !hasRightSegment) {
					newMode = Mode.Simple;
				} else {
					newMode = Mode.SingleRight;
				}
			} else if (CurrentMode == Mode.SingleRight) {
				if (!hasLeftSegment) {
					newMode = Mode.Simple;
				} else {
					newMode = Mode.All;
				}
			} else {
				newMode = Mode.Simple;
			}

			CurrentMode = newMode;
		}

		public void ChangeMainLight() {
			var invertedLight = LightMain == RoadBaseAI.TrafficLightState.Green
				? RoadBaseAI.TrafficLightState.Red
				: RoadBaseAI.TrafficLightState.Green;

			if (CurrentMode == Mode.Simple) {
				SetStates(invertedLight, invertedLight, invertedLight);
			} else if (CurrentMode == Mode.SingleLeft) {
				SetStates(invertedLight, null, invertedLight);
			} else if (CurrentMode == Mode.SingleRight) {
				SetStates(invertedLight, invertedLight, null);
			} else {
				//LightMain = invertedLight;
				SetStates(invertedLight, null, null);
			}

			UpdateVisuals();
		}

		public void ChangeLeftLight() {
			var invertedLight = LightLeft == RoadBaseAI.TrafficLightState.Green
				? RoadBaseAI.TrafficLightState.Red
				: RoadBaseAI.TrafficLightState.Green;

			//LightLeft = invertedLight;
			SetStates(null, invertedLight, null);

			UpdateVisuals();
		}

		public void ChangeRightLight() {
			var invertedLight = LightRight == RoadBaseAI.TrafficLightState.Green
				? RoadBaseAI.TrafficLightState.Red
				: RoadBaseAI.TrafficLightState.Green;

			//LightRight = invertedLight;
			SetStates(null, null, invertedLight);

			UpdateVisuals();
		}

		public bool IsAnyGreen() {
			return LightMain == RoadBaseAI.TrafficLightState.Green ||
				LightLeft == RoadBaseAI.TrafficLightState.Green ||
				LightRight == RoadBaseAI.TrafficLightState.Green;
		}

		public bool IsAnyInTransition() {
			return LightMain == RoadBaseAI.TrafficLightState.RedToGreen ||
				LightLeft == RoadBaseAI.TrafficLightState.RedToGreen ||
				LightRight == RoadBaseAI.TrafficLightState.RedToGreen ||
				LightMain == RoadBaseAI.TrafficLightState.GreenToRed ||
				LightLeft == RoadBaseAI.TrafficLightState.GreenToRed ||
				LightRight == RoadBaseAI.TrafficLightState.GreenToRed;
		}

		public bool IsLeftGreen() {
			return LightLeft == RoadBaseAI.TrafficLightState.Green;
		}

		public bool IsMainGreen() {
			return LightMain == RoadBaseAI.TrafficLightState.Green;
		}

		public bool IsRightGreen() {
			return LightRight == RoadBaseAI.TrafficLightState.Green;
		}

		public bool IsLeftRed() {
			return LightLeft == RoadBaseAI.TrafficLightState.Red;
		}

		public bool IsMainRed() {
			return LightMain == RoadBaseAI.TrafficLightState.Red;
		}

		public bool IsRightRed() {
			return LightRight == RoadBaseAI.TrafficLightState.Red;
		}

		public void UpdateVisuals() {
			var instance = Singleton<NetManager>.instance;

			ushort nodeId = lights.NodeId;
			uint currentFrameIndex = Singleton<SimulationManager>.instance.m_currentFrameIndex;
			uint num = (uint)(((int)nodeId << 8) / 32768);

			RoadBaseAI.TrafficLightState vehicleLightState;
			RoadBaseAI.TrafficLightState pedestrianLightState;

			RoadBaseAI.TrafficLightState mainLight = LightMain;
			RoadBaseAI.TrafficLightState leftLight = LightLeft;
			RoadBaseAI.TrafficLightState rightLight = LightRight;

			switch (CurrentMode) {
				case Mode.Simple:
					leftLight = mainLight;
					rightLight = mainLight;
					break;
				case Mode.SingleLeft:
					rightLight = mainLight;
					break;
				case Mode.SingleRight:
					leftLight = mainLight;
					break;
				case Mode.All:
				default:
					break;
			}

			vehicleLightState = GetVisualLightState();
			pedestrianLightState = lights.PedestrianLightState == null ? RoadBaseAI.TrafficLightState.Red : (RoadBaseAI.TrafficLightState)lights.PedestrianLightState;

#if DEBUGVISUALS
			Log._Debug($"Setting visual traffic light state of node {NodeId}, seg. {SegmentId} to vehicleState={vehicleLightState} pedState={pedestrianLightState}");
#endif

			uint now = ((currentFrameIndex - num) >> 8) & 1;
			CustomRoadAI.OriginalSetTrafficLightState(true, nodeId, ref instance.m_segments.m_buffer[SegmentId], now << 8, vehicleLightState, pedestrianLightState, false, false);
			CustomRoadAI.OriginalSetTrafficLightState(true, nodeId, ref instance.m_segments.m_buffer[SegmentId], (1u - now) << 8, vehicleLightState, pedestrianLightState, false, false);
		}

		public RoadBaseAI.TrafficLightState GetVisualLightState() {
			RoadBaseAI.TrafficLightState vehicleLightState;
			// any green?
			if (LightMain == RoadBaseAI.TrafficLightState.Green ||
				LightLeft == RoadBaseAI.TrafficLightState.Green ||
				LightRight == RoadBaseAI.TrafficLightState.Green) {
				vehicleLightState = RoadBaseAI.TrafficLightState.Green;
			} else // all red?
			if (LightMain == RoadBaseAI.TrafficLightState.Red &&
				LightLeft == RoadBaseAI.TrafficLightState.Red &&
				LightRight == RoadBaseAI.TrafficLightState.Red) {
				vehicleLightState = RoadBaseAI.TrafficLightState.Red;
			} else // any red+yellow?
			if (LightMain == RoadBaseAI.TrafficLightState.RedToGreen ||
				LightLeft == RoadBaseAI.TrafficLightState.RedToGreen ||
				LightRight == RoadBaseAI.TrafficLightState.RedToGreen) {
				vehicleLightState = RoadBaseAI.TrafficLightState.RedToGreen;
			} else {
				vehicleLightState = RoadBaseAI.TrafficLightState.GreenToRed;
			}

			return vehicleLightState;
		}

		private RoadBaseAI.TrafficLightState _checkPedestrianLight() {
			if (LightLeft == RoadBaseAI.TrafficLightState.Red && LightMain == RoadBaseAI.TrafficLightState.Red &&
				LightRight == RoadBaseAI.TrafficLightState.Red) {
				return RoadBaseAI.TrafficLightState.Green;
			}
			return RoadBaseAI.TrafficLightState.Red;
		}

		public object Clone() {
			return MemberwiseClone();
		}

		internal void MakeRedOrGreen() {
#if DEBUGTTL
			if (GlobalConfig.Instance.DebugSwitches[7] && GlobalConfig.Instance.TTLDebugNodeId == NodeId)
				Log._Debug($"CustomSegmentLight.MakeRedOrGreen: called for segment {SegmentId} @ {NodeId}");
#endif

			RoadBaseAI.TrafficLightState mainState = RoadBaseAI.TrafficLightState.Green;
			RoadBaseAI.TrafficLightState leftState = RoadBaseAI.TrafficLightState.Green;
			RoadBaseAI.TrafficLightState rightState = RoadBaseAI.TrafficLightState.Green;

			if (LightLeft != RoadBaseAI.TrafficLightState.Green) {
				leftState = RoadBaseAI.TrafficLightState.Red;
			}

			if (LightMain != RoadBaseAI.TrafficLightState.Green) {
				mainState = RoadBaseAI.TrafficLightState.Red;
			}

			if (LightRight != RoadBaseAI.TrafficLightState.Green) {
				rightState = RoadBaseAI.TrafficLightState.Red;
			}

			SetStates(mainState, leftState, rightState);
		}

		internal void MakeRed() {
#if DEBUGTTL
			if (GlobalConfig.Instance.DebugSwitches[7] && GlobalConfig.Instance.TTLDebugNodeId == NodeId)
				Log._Debug($"CustomSegmentLight.MakeRed: called for segment {SegmentId} @ {NodeId}");
#endif

			SetStates(RoadBaseAI.TrafficLightState.Red, RoadBaseAI.TrafficLightState.Red, RoadBaseAI.TrafficLightState.Red);
		}

		public void SetStates(RoadBaseAI.TrafficLightState? mainLight, RoadBaseAI.TrafficLightState? leftLight, RoadBaseAI.TrafficLightState? rightLight, bool calcAutoPedLight=true) {
			if ((mainLight == null || this.mainLight == mainLight) &&
				(leftLight == null || this.leftLight == leftLight) &&
				(rightLight == null || this.rightLight == rightLight))
				return;

			if (mainLight != null)
				this.mainLight = (RoadBaseAI.TrafficLightState)mainLight;
			if (leftLight != null)
				this.leftLight = (RoadBaseAI.TrafficLightState)leftLight;
			if (rightLight != null)
				this.rightLight = (RoadBaseAI.TrafficLightState)rightLight;

#if DEBUGTTL
			if (GlobalConfig.Instance.DebugSwitches[7] && GlobalConfig.Instance.TTLDebugNodeId == NodeId)
				Log._Debug($"CustomSegmentLight.SetStates({mainLight}, {leftLight}, {rightLight}, {calcAutoPedLight}) for segment {SegmentId} @ {NodeId}: {this.mainLight} {this.leftLight} {this.rightLight}");
#endif

			lights.OnChange(calcAutoPedLight);
		}
	}
}
