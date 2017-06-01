#define DEBUGSTEPx
#define DEBUGTTLx
#define DEBUGMETRICx

using System;
using System.Collections.Generic;
using ColossalFramework;
using TrafficManager.TrafficLight;
using TrafficManager.Geometry;
using TrafficManager.Custom.AI;
using TrafficManager.Traffic;
using TrafficManager.Manager;
using TrafficManager.State;
using TrafficManager.Util;
using System.Linq;
using CSUtil.Commons;

namespace TrafficManager.TrafficLight {
	// TODO class should be completely reworked, approx. in version 1.10
	public class TimedTrafficLightsStep : ICustomSegmentLightsManager {
		/// <summary>
		/// The number of time units this traffic light remains in the current state at least
		/// </summary>
		public int minTime;
		/// <summary>
		/// The number of time units this traffic light remains in the current state at most
		/// </summary>
		public int maxTime;
		public uint startFrame;

		/// <summary>
		/// Indicates if the step is done (internal use only)
		/// </summary>
		private bool stepDone;

		/// <summary>
		/// Frame when the GreenToRed phase started
		/// </summary>
		private uint? endTransitionStart;

		/// <summary>
		/// minimum mean "number of cars passing through" / "average segment length"
		/// </summary>
		public float minFlow;
		/// <summary>
		///	maximum mean "number of cars waiting for green" / "average segment length"
		/// </summary>
		public float maxWait;

		public int PreviousStepRefIndex = -1;
		public int NextStepRefIndex = -1;

		public uint lastFlowWaitCalc = 0;

		private TimedTrafficLights timedNode;

		public IDictionary<ushort, CustomSegmentLights> CustomSegmentLights { get; private set; } = new TinyDictionary<ushort, CustomSegmentLights>();
		public LinkedList<CustomSegmentLights> InvalidSegmentLights { get; private set; } = new LinkedList<CustomSegmentLights>();

		public float waitFlowBalance = 1f;

		public override string ToString() {
			return $"[TimedTrafficLightsStep\n" +
				"\t" + $"minTime = {minTime}\n" +
				"\t" + $"maxTime = {maxTime}\n" +
				"\t" + $"startFrame = {startFrame}\n" +
				"\t" + $"stepDone = {stepDone}\n" +
				"\t" + $"endTransitionStart = {endTransitionStart}\n" +
				"\t" + $"minFlow = {minFlow}\n" +
				"\t" + $"maxWait = {maxWait}\n" +
				"\t" + $"PreviousStepRefIndex = {PreviousStepRefIndex}\n" +
				"\t" + $"NextStepRefIndex = {NextStepRefIndex}\n" +
				"\t" + $"lastFlowWaitCalc = {lastFlowWaitCalc}\n" +
				"\t" + $"CustomSegmentLights = {CustomSegmentLights}\n" +
				"\t" + $"InvalidSegmentLights = {InvalidSegmentLights.CollectionToString()}\n" +
				"\t" + $"waitFlowBalance = {waitFlowBalance}\n" +
				"TimedTrafficLightsStep]";
		}

		public TimedTrafficLightsStep(TimedTrafficLights timedNode, int minTime, int maxTime, float waitFlowBalance, bool makeRed=false) {
			this.minTime = minTime;
			this.maxTime = maxTime;
			this.waitFlowBalance = waitFlowBalance;
			this.timedNode = timedNode;

			minFlow = Single.NaN;
			maxWait = Single.NaN;

			endTransitionStart = null;
			stepDone = false;

			NodeGeometry nodeGeometry = NodeGeometry.Get(timedNode.NodeId);

			foreach (SegmentEndGeometry end in nodeGeometry.SegmentEndGeometries) {
				if (end == null)
					continue;
				
				AddSegment(end.SegmentId, end.StartNode, makeRed);
			}
		}

		private TimedTrafficLightsStep() {

		}


        public TimedTrafficLightsStep(TimedTrafficLights timedNode, int minTime, int maxTime, float waitFlowBalance, ushort segID, bool makeRed = false)
        {
            this.minTime = minTime;
            this.maxTime = maxTime;
            this.waitFlowBalance = waitFlowBalance;
            this.timedNode = timedNode;

            minFlow = Single.NaN;
            maxWait = Single.NaN;

            endTransitionStart = null;
            stepDone = false;

            NodeGeometry nodeGeometry = NodeGeometry.Get(timedNode.NodeId);

            foreach (SegmentEndGeometry end in nodeGeometry.SegmentEndGeometries)

            {
                if (end == null || end.OutgoingOneWay)
                {
                    //DebugOutputPanel.AddMessage(PluginManager.MessageType.Message, "end = null");

                    continue;
                }
                //DebugOutputPanel.AddMessage(PluginManager.MessageType.Message, "segment: " + end.SegmentId+ " : " + end.OutgoingOneWay);
                CustomSegmentLights clonedLights = (CustomSegmentLights)CustomSegmentLightsManager.Instance.GetOrLiveSegmentLights(end.SegmentId, end.StartNode).Clone();

                CustomSegmentLights.Add(end.SegmentId, clonedLights);

                if (end.SegmentId == segID)
                {
                    // DebugOutputPanel.AddMessage(PluginManager.MessageType.Message, "segment1: " + end.SegmentId + " segment2: " + segID + " Green");
                    foreach (CustomSegmentLight light in CustomSegmentLights[end.SegmentId].CustomLights.Values)
                    {
                        //DebugOutputPanel.AddMessage(PluginManager.MessageType.Message, "Before Green");
                        light.LightLeft = RoadBaseAI.TrafficLightState.Green;
                        light.LightMain = RoadBaseAI.TrafficLightState.Green;
                        light.LightRight = RoadBaseAI.TrafficLightState.Green;
                        //DebugOutputPanel.AddMessage(PluginManager.MessageType.Message, "After Green");
                    }
                }
                else
                {
                    // DebugOutputPanel.AddMessage(PluginManager.MessageType.Message, "segment1: " + end.SegmentId + " segment2: " + segID + " Red");
                    foreach (CustomSegmentLight light in CustomSegmentLights[end.SegmentId].CustomLights.Values)
                    {
                        light.LightLeft = RoadBaseAI.TrafficLightState.Red;
                        light.LightMain = RoadBaseAI.TrafficLightState.Red;
                        light.LightRight = RoadBaseAI.TrafficLightState.Red;
                    }
                }

                //DebugOutputPanel.AddMessage(PluginManager.MessageType.Message, "Before add");
                AddSegment(end.SegmentId, end.StartNode, makeRed, true);
                // DebugOutputPanel.AddMessage(PluginManager.MessageType.Message, "After add");
            }

        }

        /// <summary>
        /// Checks if the green-to-red (=yellow) phase is finished
        /// </summary>
        /// <returns></returns>
        internal bool IsEndTransitionDone() {
			if (!timedNode.IsMasterNode()) {
				TimedTrafficLights masterLights = timedNode.MasterLights();
				return masterLights.Steps[masterLights.CurrentStep].IsEndTransitionDone();
			}

			bool isStepDone = StepDone(false);
			bool ret = endTransitionStart != null && getCurrentFrame() > endTransitionStart && isStepDone;
#if DEBUGTTL
			if (GlobalConfig.Instance.DebugSwitches[7] && GlobalConfig.Instance.TTLDebugNodeId == timedNode.NodeId)
				Log._Debug($"TimedTrafficLightsStep.isEndTransitionDone() called for master NodeId={timedNode.NodeId}. CurrentStep={timedNode.CurrentStep} getCurrentFrame()={getCurrentFrame()} endTransitionStart={endTransitionStart} isStepDone={isStepDone} ret={ret}");
#endif
			return ret;
		}

		/// <summary>
		/// Checks if the green-to-red (=yellow) phase is currently active
		/// </summary>
		/// <returns></returns>
		internal bool IsInEndTransition() {
			if (!timedNode.IsMasterNode()) {
				TimedTrafficLights masterLights = timedNode.MasterLights();
				return masterLights.Steps[masterLights.CurrentStep].IsInEndTransition();
			}

			bool isStepDone = StepDone(false);
			bool ret = endTransitionStart != null && getCurrentFrame() <= endTransitionStart && isStepDone;
#if DEBUGTTL
			if (GlobalConfig.Instance.DebugSwitches[7] && GlobalConfig.Instance.TTLDebugNodeId == timedNode.NodeId)
				Log._Debug($"TimedTrafficLightsStep.isInEndTransition() called for master NodeId={timedNode.NodeId}. CurrentStep={timedNode.CurrentStep} getCurrentFrame()={getCurrentFrame()} endTransitionStart={endTransitionStart} isStepDone={isStepDone} ret={ret}");
#endif
			return ret;
		}

		internal bool IsInStartTransition() {
			if (!timedNode.IsMasterNode()) {
				TimedTrafficLights masterLights = timedNode.MasterLights();
				return masterLights.Steps[masterLights.CurrentStep].IsInStartTransition();
			}

			bool isStepDone = StepDone(false);
			bool ret = getCurrentFrame() == startFrame && !isStepDone;
#if DEBUGTTL
			if (GlobalConfig.Instance.DebugSwitches[7] && GlobalConfig.Instance.TTLDebugNodeId == timedNode.NodeId)
				Log._Debug($"TimedTrafficLightsStep.isInStartTransition() called for master NodeId={timedNode.NodeId}. CurrentStep={timedNode.CurrentStep} getCurrentFrame()={getCurrentFrame()} startFrame={startFrame} isStepDone={isStepDone} ret={ret}");
#endif

			return ret;
		}

		public RoadBaseAI.TrafficLightState GetLight(ushort segmentId, ExtVehicleType vehicleType, int lightType) {
			CustomSegmentLight segLight = CustomSegmentLights[segmentId].GetCustomLight(vehicleType);
			if (segLight != null) {
				switch (lightType) {
					case 0:
						return segLight.LightMain;
					case 1:
						return segLight.LightLeft;
					case 2:
						return segLight.LightRight;
					case 3:
						RoadBaseAI.TrafficLightState? pedState = CustomSegmentLights[segmentId].PedestrianLightState;
						return pedState == null ? RoadBaseAI.TrafficLightState.Red : (RoadBaseAI.TrafficLightState)pedState;
				}
			}

			return RoadBaseAI.TrafficLightState.Green;
		}

		/// <summary>
		/// Starts the step.
		/// </summary>
		public void Start(int previousStepRefIndex=-1) {
#if DEBUGTTL
			if (GlobalConfig.Instance.DebugSwitches[7] && GlobalConfig.Instance.TTLDebugNodeId == timedNode.NodeId)
				Log._Debug($"TimedTrafficLightsStep.Start: Starting step {timedNode.CurrentStep} @ {timedNode.NodeId}");
#endif

			this.startFrame = getCurrentFrame();
			Reset();
			PreviousStepRefIndex = previousStepRefIndex;

#if DEBUG
			/*if (GlobalConfig.Instance.DebugSwitches[2]) {
				if (timedNode.NodeId == 31605) {
					Log._Debug($"===== Step {timedNode.CurrentStep} @ node {timedNode.NodeId} =====");
					Log._Debug($"minTime: {minTime} maxTime: {maxTime}");
					foreach (KeyValuePair<ushort, CustomSegmentLights> e in segmentLights) {
						Log._Debug($"\tSegment {e.Key}:");
						Log._Debug($"\t{e.Value.ToString()}");
					}
				}
			}*/
#endif
		}

		internal void Reset() {
			this.endTransitionStart = null;
			minFlow = Single.NaN;
			maxWait = Single.NaN;
			lastFlowWaitCalc = 0;
			PreviousStepRefIndex = -1;
			NextStepRefIndex = -1;
			stepDone = false;
		}

		internal static uint getCurrentFrame() {
			return Constants.ServiceFactory.SimulationService.CurrentFrameIndex >> 6;
		}

		/// <summary>
		/// Updates "real-world" traffic light states according to the timed scripts
		/// </summary>
		public void UpdateLiveLights() {
			UpdateLiveLights(false);
		}
		
		public void UpdateLiveLights(bool noTransition) {
			try {
				CustomSegmentLightsManager customTrafficLightsManager = CustomSegmentLightsManager.Instance;

				bool atEndTransition = !noTransition && (IsInEndTransition() || IsEndTransitionDone()); // = yellow
				bool atStartTransition = !noTransition && !atEndTransition && IsInStartTransition(); // = red + yellow

#if DEBUGTTL
				if (timedNode == null) {
					Log.Error($"TimedTrafficLightsStep: timedNode is null!");
					return;
				}
#endif

				if (PreviousStepRefIndex >= timedNode.NumSteps())
					PreviousStepRefIndex = -1;
				if (NextStepRefIndex >= timedNode.NumSteps())
					NextStepRefIndex = -1;
				TimedTrafficLightsStep previousStep = timedNode.Steps[PreviousStepRefIndex >= 0 ? PreviousStepRefIndex : ((timedNode.CurrentStep + timedNode.Steps.Count - 1) % timedNode.Steps.Count)];
				TimedTrafficLightsStep nextStep = timedNode.Steps[NextStepRefIndex >= 0 ? NextStepRefIndex : ((timedNode.CurrentStep + 1) % timedNode.Steps.Count)];

#if DEBUGTTL
				if (previousStep == null) {
					Log.Error($"TimedTrafficLightsStep: previousStep is null!");
					//return;
				}

				if (nextStep == null) {
					Log.Error($"TimedTrafficLightsStep: nextStep is null!");
					//return;
				}

				if (previousStep.CustomSegmentLights == null) {
					Log.Error($"TimedTrafficLightsStep: previousStep.segmentLights is null!");
					//return;
				}

				if (nextStep.CustomSegmentLights == null) {
					Log.Error($"TimedTrafficLightsStep: nextStep.segmentLights is null!");
					//return;
				}

				if (CustomSegmentLights == null) {
					Log.Error($"TimedTrafficLightsStep: segmentLights is null!");
					//return;
				}
#endif

#if DEBUG
				//Log._Debug($"TimedTrafficLightsStep.SetLights({noTransition}) called for NodeId={timedNode.NodeId}. atStartTransition={atStartTransition} atEndTransition={atEndTransition}");
#endif

				foreach (KeyValuePair<ushort, CustomSegmentLights> e in CustomSegmentLights) {
					var segmentId = e.Key;
					var curStepSegmentLights = e.Value;

#if DEBUG
					//Log._Debug($"TimedTrafficLightsStep.SetLights({noTransition})   -> segmentId={segmentId} @ NodeId={timedNode.NodeId}");
#endif

					CustomSegmentLights prevStepSegmentLights = null;
					if (!previousStep.CustomSegmentLights.TryGetValue(segmentId, out prevStepSegmentLights)) {
#if DEBUGTTL
						if (GlobalConfig.Instance.DebugSwitches[7] && GlobalConfig.Instance.TTLDebugNodeId == timedNode.NodeId)
							Log.Warning($"TimedTrafficLightsStep: previousStep does not contain lights for segment {segmentId}!");
#endif
						continue;
					}

					CustomSegmentLights nextStepSegmentLights = null;
					if (!nextStep.CustomSegmentLights.TryGetValue(segmentId, out nextStepSegmentLights)) {
#if DEBUGTTL
						if (GlobalConfig.Instance.DebugSwitches[7] && GlobalConfig.Instance.TTLDebugNodeId == timedNode.NodeId)
							Log.Warning($"TimedTrafficLightsStep: nextStep does not contain lights for segment {segmentId}!");
#endif
						continue;
					}

					//segLightState.makeRedOrGreen(); // TODO temporary fix

					var liveSegmentLights = customTrafficLightsManager.GetSegmentLights(segmentId, curStepSegmentLights.StartNode, false);
					if (liveSegmentLights == null) {
						continue;
					}

					RoadBaseAI.TrafficLightState pedLightState = calcLightState((RoadBaseAI.TrafficLightState)prevStepSegmentLights.PedestrianLightState, (RoadBaseAI.TrafficLightState)curStepSegmentLights.PedestrianLightState, (RoadBaseAI.TrafficLightState)nextStepSegmentLights.PedestrianLightState, atStartTransition, atEndTransition);
					//Log._Debug($"TimedStep.SetLights: Setting pedestrian light state @ seg. {segmentId} to {pedLightState} {curStepSegmentLights.ManualPedestrianMode}");
                    liveSegmentLights.ManualPedestrianMode = curStepSegmentLights.ManualPedestrianMode;
					liveSegmentLights.PedestrianLightState = liveSegmentLights.AutoPedestrianLightState = pedLightState;
					//Log.Warning($"Step @ {timedNode.NodeId}: Segment {segmentId}: Ped.: {liveSegmentLights.PedestrianLightState.ToString()} / {liveSegmentLights.AutoPedestrianLightState.ToString()}");

#if DEBUGTTL
					if (GlobalConfig.Instance.DebugSwitches[7] && GlobalConfig.Instance.TTLDebugNodeId == timedNode.NodeId)
						if (curStepSegmentLights.VehicleTypes == null) {
							Log.Error($"TimedTrafficLightsStep: curStepSegmentLights.VehicleTypes is null!");
							return;
						}
#endif

					foreach (ExtVehicleType vehicleType in curStepSegmentLights.VehicleTypes) {
#if DEBUG
						//Log._Debug($"TimedTrafficLightsStep.SetLights({noTransition})     -> segmentId={segmentId} @ NodeId={timedNode.NodeId} for vehicle {vehicleType}");
#endif

						CustomSegmentLight liveSegmentLight = liveSegmentLights.GetCustomLight(vehicleType);
						if (liveSegmentLight == null) {
#if DEBUGTTL
							if (GlobalConfig.Instance.DebugSwitches[7] && GlobalConfig.Instance.TTLDebugNodeId == timedNode.NodeId)
								Log._Debug($"Timed step @ seg. {segmentId}, node {timedNode.NodeId} has a traffic light for {vehicleType} but the live segment does not have one.");
#endif
							continue;
						}
						CustomSegmentLight curStepSegmentLight = curStepSegmentLights.GetCustomLight(vehicleType);
						CustomSegmentLight prevStepSegmentLight = prevStepSegmentLights.GetCustomLight(vehicleType);
						CustomSegmentLight nextStepSegmentLight = nextStepSegmentLights.GetCustomLight(vehicleType);
						
#if DEBUGTTL
						if (curStepSegmentLight == null) {
							Log.Error($"TimedTrafficLightsStep: curStepSegmentLight is null!");
							//return;
						}

						if (prevStepSegmentLight == null) {
							Log.Error($"TimedTrafficLightsStep: prevStepSegmentLight is null!");
							//return;
						}

						if (nextStepSegmentLight == null) {
							Log.Error($"TimedTrafficLightsStep: nextStepSegmentLight is null!");
							//return;
						}
#endif

						liveSegmentLight.currentMode = curStepSegmentLight.CurrentMode;
						/*curStepSegmentLight.EnsureModeLights();
						prevStepSegmentLight.EnsureModeLights();
						nextStepSegmentLight.EnsureModeLights();*/

						RoadBaseAI.TrafficLightState mainLight = calcLightState(prevStepSegmentLight.LightMain, curStepSegmentLight.LightMain, nextStepSegmentLight.LightMain, atStartTransition, atEndTransition);
						RoadBaseAI.TrafficLightState leftLight = calcLightState(prevStepSegmentLight.LightLeft, curStepSegmentLight.LightLeft, nextStepSegmentLight.LightLeft, atStartTransition, atEndTransition);
						RoadBaseAI.TrafficLightState rightLight = calcLightState(prevStepSegmentLight.LightRight, curStepSegmentLight.LightRight, nextStepSegmentLight.LightRight, atStartTransition, atEndTransition);
						liveSegmentLight.SetStates(mainLight, leftLight, rightLight, false);

#if DEBUGTTL
						if (GlobalConfig.Instance.DebugSwitches[7] && GlobalConfig.Instance.TTLDebugNodeId == timedNode.NodeId)
							Log._Debug($"TimedTrafficLightsStep.SetLights({noTransition})     -> *SETTING* LightLeft={liveSegmentLight.LightLeft} LightMain={liveSegmentLight.LightMain} LightRight={liveSegmentLight.LightRight} for segmentId={segmentId} @ NodeId={timedNode.NodeId} for vehicle {vehicleType}");
#endif

						//Log._Debug($"Step @ {timedNode.NodeId}: Segment {segmentId} for vehicle type {vehicleType}: L: {liveSegmentLight.LightLeft.ToString()} F: {liveSegmentLight.LightMain.ToString()} R: {liveSegmentLight.LightRight.ToString()}");
					}

					/*if (timedNode.NodeId == 20164) {
						Log._Debug($"Step @ {timedNode.NodeId}: Segment {segmentId}: {segmentLight.LightLeft.ToString()} {segmentLight.LightMain.ToString()} {segmentLight.LightRight.ToString()} {segmentLight.LightPedestrian.ToString()}");
                    }*/

					liveSegmentLights.UpdateVisuals();
				}
			} catch (Exception e) {
				Log.Error($"Exception in TimedTrafficStep.UpdateLiveLights for node {timedNode.NodeId}: {e.ToString()}");
				//invalid = true;
			}
		}

		/// <summary>
		/// Adds a new segment to this step. It is cloned from the live custom traffic light.
		/// </summary>
		/// <param name="segmentId"></param>
		internal void AddSegment(ushort segmentId, bool startNode, bool makeRed) {
			CustomSegmentLights clonedLights = CustomSegmentLightsManager.Instance.GetOrLiveSegmentLights(segmentId, startNode).Clone(this);

			CustomSegmentLights.Add(segmentId, clonedLights);
			if (makeRed)
				CustomSegmentLights[segmentId].MakeRed();
			else
				CustomSegmentLights[segmentId].MakeRedOrGreen();
			CustomSegmentLightsManager.Instance.ApplyLightModes(segmentId, startNode, clonedLights);
		}

		private RoadBaseAI.TrafficLightState calcLightState(RoadBaseAI.TrafficLightState previousState, RoadBaseAI.TrafficLightState currentState, RoadBaseAI.TrafficLightState nextState, bool atStartTransition, bool atEndTransition) {
			if (atStartTransition && currentState == RoadBaseAI.TrafficLightState.Green && previousState == RoadBaseAI.TrafficLightState.Red)
				return RoadBaseAI.TrafficLightState.RedToGreen;
			else if (atEndTransition && currentState == RoadBaseAI.TrafficLightState.Green && nextState == RoadBaseAI.TrafficLightState.Red)
				return RoadBaseAI.TrafficLightState.GreenToRed;
			else
				return currentState;
		}

        internal void AddSegment(ushort segmentId, bool startNode, bool makeRed, bool roundRobin)
        {
            CustomSegmentLights clonedLights = CustomSegmentLightsManager.Instance.GetOrLiveSegmentLights(segmentId, startNode).Clone(this);

            if (makeRed)
                CustomSegmentLights[segmentId].MakeRed();
            else
                CustomSegmentLights[segmentId].MakeRedOrGreen();
            CustomSegmentLightsManager.Instance.ApplyLightModes(segmentId, startNode, clonedLights);
        }

      

        /// <summary>
        /// Updates timed segment lights according to "real-world" traffic light states
        /// </summary>
        public void UpdateLights() {
			Log._Debug($"TimedTrafficLightsStep.UpdateLights: Updating lights of timed traffic light step @ {timedNode.NodeId}");
			foreach (KeyValuePair<ushort, CustomSegmentLights> e in CustomSegmentLights) {
				var segmentId = e.Key;
				var segLights = e.Value;

				Log._Debug($"TimedTrafficLightsStep.UpdateLights: Updating lights of timed traffic light step at seg. {e.Key} @ {timedNode.NodeId}");

				//if (segment == 0) continue;
				var liveSegLights = CustomSegmentLightsManager.Instance.GetSegmentLights(segmentId, segLights.StartNode, false);
				if (liveSegLights == null)
					continue;

				segLights.SetLights(liveSegLights);
				Log._Debug($"TimedTrafficLightsStep.UpdateLights: Segment {segmentId} AutoPedState={segLights.AutoPedestrianLightState} live={liveSegLights.AutoPedestrianLightState}");
			}
		}

		/// <summary>
		/// Countdown value for min. time
		/// </summary>
		/// <returns></returns>
		public long MinTimeRemaining() {
			return Math.Max(0, startFrame + minTime - getCurrentFrame());
		}

		/// <summary>
		/// Countdown value for max. time
		/// </summary>
		/// <returns></returns>
		public long MaxTimeRemaining() {
			return Math.Max(0, startFrame + maxTime - getCurrentFrame());
		}

		public void SetStepDone() {
			stepDone = true;
		}

		public bool StepDone(bool updateValues) {
#if DEBUGTTL
			bool debug = GlobalConfig.Instance.DebugSwitches[7] && GlobalConfig.Instance.TTLDebugNodeId == timedNode.NodeId;
			if (debug) {
				Log._Debug($"StepDone: called for node {timedNode.NodeId} @ step {timedNode.CurrentStep}");
			}
#endif

			if (!timedNode.IsMasterNode()) {
				TimedTrafficLights masterLights = timedNode.MasterLights();
				return masterLights.Steps[masterLights.CurrentStep].StepDone(updateValues);
			}
			// we are the master node

			if (timedNode.IsInTestMode()) {
				return false;
			}
			if (stepDone) {
				return true;
			}

			if (getCurrentFrame() >= startFrame + maxTime) {
				// maximum time reached. switch!
#if DEBUGTTL
				if (debug)
					Log._Debug($"StepDone: step finished @ {timedNode.NodeId}");
#endif
				if (!stepDone && updateValues) {
					stepDone = true;
					endTransitionStart = getCurrentFrame();
				}
				return stepDone;
			}

			if (getCurrentFrame() >= startFrame + minTime) {
				
					
				float wait, flow;
				uint curFrame = getCurrentFrame();
				//Log._Debug($"TTL @ {timedNode.NodeId}: curFrame={curFrame} lastFlowWaitCalc={lastFlowWaitCalc}");
				if (lastFlowWaitCalc < curFrame) {
					//Log._Debug($"TTL @ {timedNode.NodeId}: lastFlowWaitCalc<curFrame");
					if (!calcWaitFlow(true, timedNode.CurrentStep, out wait, out flow)) {
						//Log._Debug($"TTL @ {timedNode.NodeId}: calcWaitFlow failed!");
						if (!stepDone && updateValues) {
							//Log._Debug($"TTL @ {timedNode.NodeId}: !stepDone && updateValues");
							stepDone = true;
							endTransitionStart = getCurrentFrame();
						}
						return stepDone;
					} else {
						if (updateValues) {
							lastFlowWaitCalc = curFrame;
							//Log._Debug($"TTL @ {timedNode.NodeId}: updated lastFlowWaitCalc=curFrame={curFrame}");
						}
					}
				} else {
					flow = minFlow;
					wait = maxWait;
					//Log._Debug($"TTL @ {timedNode.NodeId}: lastFlowWaitCalc>=curFrame wait={maxWait} flow={minFlow}");
				}

				float newFlow = minFlow;
				float newWait = maxWait;

#if DEBUGMETRIC
				newFlow = flow;
				newWait = wait;
#else
				if (Single.IsNaN(newFlow))
					newFlow = flow;
				else
					newFlow = 0.1f * newFlow + 0.9f * flow; // some smoothing

				if (Single.IsNaN(newWait))
					newWait = 0;
				else
					newWait = 0.1f * newWait + 0.9f * wait; // some smoothing
#endif

				// if more cars are waiting than flowing, we change the step
				bool done = newWait > 0 && newFlow < newWait;

				//Log._Debug($"TTL @ {timedNode.NodeId}: newWait={newWait} newFlow={newFlow} updateValues={updateValues} stepDone={stepDone} done={done}");

				if (updateValues) {
					minFlow = newFlow;
					maxWait = newWait;
					//Log._Debug($"TTL @ {timedNode.NodeId}: updated minFlow=newFlow={minFlow} maxWait=newWait={maxWait}");
				}
#if DEBUG
				//Log.Message("step finished (2) @ " + nodeId);
#endif
				if (updateValues && !stepDone && done) {
					stepDone = done;
					endTransitionStart = getCurrentFrame();
				}
				return done;
			}

			return false;
		}

		/// <summary>
		/// Calculates the current metrics for flowing and waiting vehicles
		/// </summary>
		/// <param name="wait"></param>
		/// <param name="flow"></param>
		/// <returns>true if the values could be calculated, false otherwise</returns>
		public bool calcWaitFlow(bool countOnlyMovingIfGreen, int stepRefIndex, out float wait, out float flow) {
			uint numFlows = 0;
			uint numWaits = 0;
			float curTotalFlow = 0;
			float curTotalWait = 0;

#if DEBUGTTL
			bool debug = GlobalConfig.Instance.DebugSwitches[7] && GlobalConfig.Instance.TTLDebugNodeId == timedNode.NodeId;
			if (debug) {
				Log._Debug($"calcWaitFlow: called for node {timedNode.NodeId} @ step {stepRefIndex}");
			}
#else
			bool debug = false;
#endif

			// TODO checking agains getCurrentFrame() is only valid if this is the current step
			if (countOnlyMovingIfGreen && getCurrentFrame() <= startFrame + minTime + 1) { // during start phase all vehicles on "green" segments are counted as flowing
				countOnlyMovingIfGreen = false;
			}

			TrafficLightSimulationManager tlsMan = TrafficLightSimulationManager.Instance;
			SegmentEndManager endMan = SegmentEndManager.Instance;
			VehicleRestrictionsManager restrMan = VehicleRestrictionsManager.Instance;

			// loop over all timed traffic lights within the node group
			foreach (ushort timedNodeId in timedNode.NodeGroup) {
				TrafficLightSimulation sim = tlsMan.GetNodeSimulation(timedNodeId);
				if (sim == null || !sim.IsTimedLight())
					continue;
				TimedTrafficLights slaveTimedNode = sim.TimedLight;
				TimedTrafficLightsStep slaveStep = slaveTimedNode.Steps[stepRefIndex];

				// minimum time reached. check traffic! loop over source segments
				uint numNodeFlows = 0;
				uint numNodeWaits = 0;
				float curTotalNodeFlow = 0;
				float curTotalNodeWait = 0;
				foreach (KeyValuePair<ushort, CustomSegmentLights> e in slaveStep.CustomSegmentLights) {
					var sourceSegmentId = e.Key;
					var segLights = e.Value;

					IDictionary<ushort, ArrowDirection> directions = null;
					if (!slaveStep.timedNode.Directions.TryGetValue(sourceSegmentId, out directions)) {
#if DEBUGTTL
						if (debug) {
							Log._Debug($"calcWaitFlow: No arrow directions defined for segment {sourceSegmentId} @ {timedNodeId}");
						}
#endif
						continue;
					}

					// one of the traffic lights at this segment is green: count minimum traffic flowing through
					SegmentEnd sourceSegmentEnd = endMan.GetSegmentEnd(sourceSegmentId, segLights.StartNode);
					if (sourceSegmentEnd == null) {
						Log.Error($"TimedTrafficLightsStep.calcWaitFlow: No segment end @ seg. {sourceSegmentId} found!");
						continue; // skip invalid segment
					}

					bool countOnlyMovingIfGreenOnSegment = countOnlyMovingIfGreen;
					if (countOnlyMovingIfGreenOnSegment) {
						Constants.ServiceFactory.NetService.ProcessSegment(sourceSegmentId, delegate (ushort srcSegId, ref NetSegment segment) {
							if (restrMan.IsRailSegment(segment.Info)) {
								countOnlyMovingIfGreenOnSegment = false;
							}
							return true;
						});
					}

					IDictionary<ushort, uint>[] movingVehiclesMetrics = countOnlyMovingIfGreenOnSegment ? sourceSegmentEnd.MeasureOutgoingVehicles(false, debug) : null;
					IDictionary<ushort, uint>[] allVehiclesMetrics = sourceSegmentEnd.MeasureOutgoingVehicles(true, debug);
					
					ExtVehicleType?[] vehTypeByLaneIndex = segLights.VehicleTypeByLaneIndex;
#if DEBUGTTL
					if (debug) {
						Log._Debug($"calcWaitFlow: Seg. {sourceSegmentId} @ {timedNodeId}, vehTypeByLaneIndex={string.Join(", ", vehTypeByLaneIndex.Select(x => x == null ? "null" : x.ToString()).ToArray())}");
					}
#endif
					uint numSegFlows = 0;
					uint numSegWaits = 0;
					float curTotalSegFlow = 0;
					float curTotalSegWait = 0;
					// loop over source lanes
					for (byte laneIndex = 0; laneIndex < vehTypeByLaneIndex.Length; ++laneIndex) {
						ExtVehicleType? vehicleType = vehTypeByLaneIndex[laneIndex];
						if (vehicleType == null) {
							continue;
						}

						CustomSegmentLight segLight = segLights.GetCustomLight(laneIndex);
						if (segLight == null) {
#if DEBUGTTL
							Log.Warning($"Timed traffic light step: Failed to get custom light for vehicleType {vehicleType} @ seg. {sourceSegmentId}, node {timedNode.NodeId}!");
#endif
							continue;
						}

						IDictionary<ushort, uint> movingVehiclesMetric = countOnlyMovingIfGreenOnSegment ? movingVehiclesMetrics[laneIndex] : null;
						IDictionary<ushort, uint> allVehiclesMetric = allVehiclesMetrics[laneIndex];
						if (allVehiclesMetrics == null) {
#if DEBUGTTL
							if (debug) {
								Log._Debug($"TimedTrafficLightsStep.calcWaitFlow: No cars on lane {laneIndex} @ seg. {sourceSegmentId}. Vehicle types: {vehicleType}");
							}
#endif
							continue;
						}

#if DEBUGTTL
						if (debug)
							Log._Debug($"TimedTrafficLightsStep.calcWaitFlow: Checking lane {laneIndex} @ seg. {sourceSegmentId}. Vehicle types: {vehicleType}");
#endif

						// loop over target segment: calculate waiting/moving traffic
						uint numLaneFlows = 0;
						uint numLaneWaits = 0;
						uint curTotalLaneFlow = 0;
						uint curTotalLaneWait = 0;
						foreach (KeyValuePair<ushort, uint> f in allVehiclesMetric) {
							ushort targetSegmentId = f.Key;
							uint numVehicles = f.Value;

							ArrowDirection dir;
							if (!directions.TryGetValue(targetSegmentId, out dir)) {
								Log._Debug($"TimedTrafficLightsStep.calcWaitFlow: Direction undefined for target segment {targetSegmentId} @ {timedNodeId}");
								continue;
							}

							uint numMovingVehicles = countOnlyMovingIfGreenOnSegment ? movingVehiclesMetric[f.Key] : numVehicles;

#if DEBUGTTL
							if (debug)
								Log._Debug($"TimedTrafficLightsStep.calcWaitFlow: Total num of flowing cars on seg. {sourceSegmentId}, lane {laneIndex} going to seg. {targetSegmentId}: {numMovingVehicles} (all: {numVehicles})");
#endif

							bool addToFlow = false;
							switch (dir) {
								case ArrowDirection.Turn:
									addToFlow = Constants.ServiceFactory.SimulationService.LeftHandDrive ? segLight.IsRightGreen() : segLight.IsLeftGreen();
									break;
								case ArrowDirection.Left:
									addToFlow = segLight.IsLeftGreen();
									break;
								case ArrowDirection.Right:
									addToFlow = segLight.IsRightGreen();
									break;
								case ArrowDirection.Forward:
								default:
									addToFlow = segLight.IsMainGreen();
									break;
							}

							if (addToFlow) {
								curTotalLaneFlow += numMovingVehicles;
								++numLaneFlows;
							} else {
								curTotalLaneWait += numVehicles;
								++numLaneWaits;
							}

#if DEBUGTTL
							if (debug)
								Log._Debug($"TimedTrafficLightsStep.calcWaitFlow: >>>>> Vehicles @ lane {laneIndex}, seg. {sourceSegmentId} going to seg. {targetSegmentId}: curTotalLaneFlow={curTotalLaneFlow}, curTotalLaneWait={curTotalLaneWait}, numLaneFlows={numLaneFlows}, numLaneWaits={numLaneWaits}");
#endif
						} // foreach target segment

						float meanLaneFlow = 0;
						if (numLaneFlows > 0) {
							++numSegFlows;
							meanLaneFlow = (float)curTotalLaneFlow / (float)numLaneFlows;
							curTotalSegFlow += meanLaneFlow;
						}

						float meanLaneWait = 0;
						if (numLaneWaits > 0) {
							++numSegWaits;
							meanLaneWait = (float)curTotalLaneWait / (float)numLaneWaits;
							curTotalSegWait += meanLaneWait;
						}

#if DEBUGTTL
						if (debug)
							Log._Debug($"TimedTrafficLightsStep.calcWaitFlow: >>>> Vehicles @ lane {laneIndex}, seg. {sourceSegmentId}: meanLaneFlow={meanLaneFlow}, meanLaneWait={meanLaneWait} // curTotalSegFlow={curTotalSegFlow}, curTotalSegWait={curTotalSegWait}, numSegFlows={numSegFlows}, numSegWaits={numSegWaits}");
#endif

					} // foreach source lane

					float meanSegFlow = 0;
					if (numSegFlows > 0) {
						++numNodeFlows;
						meanSegFlow = (float)curTotalSegFlow / (float)numSegFlows;
						curTotalNodeFlow += meanSegFlow;
					}

					float meanSegWait = 0;
					if (numSegWaits > 0) {
						++numNodeWaits;
						meanSegWait = (float)curTotalSegWait / (float)numSegWaits;
						curTotalNodeWait += meanSegWait;
					}

#if DEBUGTTL
					if (debug)
						Log._Debug($"TimedTrafficLightsStep.calcWaitFlow: >>> Vehicles @ seg. {sourceSegmentId}: meanSegFlow={meanSegFlow}, meanSegWait={meanSegWait} // curTotalNodeFlow={curTotalNodeFlow}, curTotalNodeWait={curTotalNodeWait}, numNodeFlows={numNodeFlows}, numNodeWaits={numNodeWaits}");
#endif

				} // foreach source segment

				float meanNodeFlow = 0;
				if (numNodeFlows > 0) {
					++numFlows;
					meanNodeFlow = (float)curTotalNodeFlow / (float)numNodeFlows;
					curTotalFlow += meanNodeFlow;
				}

				float meanNodeWait = 0;
				if (numNodeWaits > 0) {
					++numWaits;
					meanNodeWait = (float)curTotalNodeWait / (float)numNodeWaits;
					curTotalWait += meanNodeWait;
				}

#if DEBUGTTL
				if (debug)
					Log._Debug($"TimedTrafficLightsStep.calcWaitFlow: Calculated flow for source node {timedNodeId}: meanNodeFlow={meanNodeFlow} meanNodeWait={meanNodeWait} // curTotalFlow={curTotalFlow}, curTotalWait={curTotalWait}, numFlows={numFlows}, numWaits={numWaits}");
#endif
			} // foreach timed node

			float meanFlow = numFlows > 0 ? (float)curTotalFlow / (float)numFlows : 0;
			float meanWait = numWaits > 0 ? (float)curTotalWait / (float)numWaits : 0;
			meanFlow /= waitFlowBalance; // a value smaller than 1 rewards steady traffic currents

			wait = (float)meanWait;
			flow = meanFlow;

#if DEBUGTTL
			if (debug)
				Log._Debug($"TimedTrafficLightsStep.calcWaitFlow: ***CALCULATION FINISHED*** for master node {timedNode.NodeId}: flow={flow} wait={wait}");
#endif

			return true;
		}

		internal void ChangeLightMode(ushort segmentId, ExtVehicleType vehicleType, CustomSegmentLight.Mode mode) {
			CustomSegmentLight light = CustomSegmentLights[segmentId].GetCustomLight(vehicleType);
			if (light != null) {
				light.CurrentMode = mode;
			}
		}

		public CustomSegmentLights RemoveSegmentLights(ushort segmentId) {
			CustomSegmentLights ret = null;
			if (CustomSegmentLights.TryGetValue(segmentId, out ret)) {
				CustomSegmentLights.Remove(segmentId);
			}
			return ret;
		}

		public CustomSegmentLights GetSegmentLights(ushort segmentId) {
			return GetSegmentLights(timedNode.NodeId, segmentId);
		}

		public CustomSegmentLights GetSegmentLights(ushort nodeId, ushort segmentId) {
			if (nodeId != timedNode.NodeId) {
				Log.Warning($"TimedTrafficLightsStep @ node {timedNode.NodeId} does not handle custom traffic lights for node {nodeId}");
				return null;
			}

			CustomSegmentLights customLights;
			if (CustomSegmentLights.TryGetValue(segmentId, out customLights)) {
				return customLights;
			} else {
				Log.Info($"TimedTrafficLightsStep @ node {timedNode.NodeId} does not know segment {segmentId}");
				return null;
			}
		}

		public bool RelocateSegmentLights(ushort sourceSegmentId, ushort targetSegmentId) {
			CustomSegmentLights sourceLights = null;
			if (! CustomSegmentLights.TryGetValue(sourceSegmentId, out sourceLights)) {
				Log.Error($"TimedTrafficLightsStep.RelocateSegmentLights: Timed traffic light does not know source segment {sourceSegmentId}. Cannot relocate to {targetSegmentId}.");
				return false;
			}

			SegmentGeometry segGeo = SegmentGeometry.Get(targetSegmentId);
			if (segGeo == null) {
				Log.Error($"TimedTrafficLightsStep.RelocateSegmentLights: No geometry information available for target segment {targetSegmentId}");
				return false;
			}

			if (segGeo.StartNodeId() != timedNode.NodeId && segGeo.EndNodeId() != timedNode.NodeId) {
				Log.Error($"TimedTrafficLightsStep.RelocateSegmentLights: Target segment {targetSegmentId} is not connected to node {timedNode.NodeId}");
				return false;
			}

			bool startNode = segGeo.StartNodeId() == timedNode.NodeId;
			CustomSegmentLights.Remove(sourceSegmentId);
			sourceLights.Relocate(targetSegmentId, startNode, this);
			CustomSegmentLights[targetSegmentId] = sourceLights;

			Log._Debug($"TimedTrafficLightsStep.RelocateSegmentLights: Relocated lights: {sourceSegmentId} -> {targetSegmentId} @ node {timedNode.NodeId}");
			return true;
		}

		public bool SetSegmentLights(ushort nodeId, ushort segmentId, CustomSegmentLights lights) {
			if (nodeId != timedNode.NodeId) {
				Log.Warning($"TimedTrafficLightsStep @ node {timedNode.NodeId} does not handle custom traffic lights for node {nodeId}");
				return false;
			}

			return SetSegmentLights(segmentId, lights);
		}

		public bool SetSegmentLights(ushort segmentId, CustomSegmentLights lights) {
			SegmentGeometry segGeo = SegmentGeometry.Get(segmentId);
			if (segGeo == null) {
				Log.Error($"TimedTrafficLightsStep.SetSegmentLights: No geometry information available for target segment {segmentId}");
				return false;
			}

			if (segGeo.StartNodeId() != timedNode.NodeId && segGeo.EndNodeId() != timedNode.NodeId) {
				Log.Error($"TimedTrafficLightsStep.RelocateSegmentLights: Segment {segmentId} is not connected to node {timedNode.NodeId}");
				return false;
			}

			lights.Relocate(segmentId, segGeo.StartNodeId() == timedNode.NodeId, this);
			CustomSegmentLights[segmentId] = lights;
			Log._Debug($"TimedTrafficLightsStep.SetSegmentLights: Set lights @ seg. {segmentId}, node {timedNode.NodeId}");
			return true;
		}

		public short ClockwiseIndexOfSegmentEnd(SegmentEndId endId) {
			SegmentEndGeometry endGeo = SegmentEndGeometry.Get(endId);

			if (endGeo == null) {
				Log.Warning($"TimedTrafficLightsStep.ClockwiseIndexOfSegmentEnd: @ node {timedNode.NodeId}: No segment end geometry found for end id {endId}");
				return -1;
			}

			if (endGeo.NodeId() != timedNode.NodeId) {
				Log.Warning($"TimedTrafficLightsStep.ClockwiseIndexOfSegmentEnd: @ node {timedNode.NodeId} does not handle custom traffic lights for node {endGeo.NodeId()}");
				return -1;
			}

			if (CustomSegmentLights.ContainsKey(endId.SegmentId)) {
				Log.Warning($"TimedTrafficLightsStep.ClockwiseIndexOfSegmentEnd: @ node {timedNode.NodeId} does not handle custom traffic lights for segment {endId.SegmentId}");
				return -1;
			}

			short index = CustomSegmentLightsManager.Instance.ClockwiseIndexOfSegmentEnd(endId);
			index += timedNode.RotationOffset;
			return (short)(index % (endGeo.NumConnectedSegments + 1));
		}
	}
}
