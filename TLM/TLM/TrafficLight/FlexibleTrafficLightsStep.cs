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

namespace TrafficManager.TrafficLight
{
    // TODO class should be completely reworked, approx. in version 1.10
    public class FlexibleTrafficLightsStep : ICustomSegmentLightsManager
    {
       
        public uint startFrame;

        /// <summary>
        /// Indicates if the step is done (internal use only)
        /// </summary>
        private bool stepDone;

        /// <summary>
        /// Frame when the GreenToRed phase started
        /// </summary>
        private uint? endTransitionStart;

           

        public int PreviousStepRefIndex = -1;
        public int NextStepRefIndex = -1;

        public uint lastFlowWaitCalc = 0;

        private FlexibleTrafficLights flexibleNode;

        public IDictionary<ushort, CustomSegmentLights> CustomSegmentLights { get; private set; } = new TinyDictionary<ushort, CustomSegmentLights>();
        public LinkedList<CustomSegmentLights> InvalidSegmentLights { get; private set; } = new LinkedList<CustomSegmentLights>();

        

        public override string ToString()
        {
            return $"[FlexibleTrafficLightsStep\n" +                
                "\t" + $"startFrame = {startFrame}\n" +
                "\t" + $"stepDone = {stepDone}\n" +
                "\t" + $"endTransitionStart = {endTransitionStart}\n" +              
                "\t" + $"PreviousStepRefIndex = {PreviousStepRefIndex}\n" +
                "\t" + $"NextStepRefIndex = {NextStepRefIndex}\n" +
                "\t" + $"lastFlowWaitCalc = {lastFlowWaitCalc}\n" +
                "\t" + $"CustomSegmentLights = {CustomSegmentLights}\n" +
                "\t" + $"InvalidSegmentLights = {InvalidSegmentLights.CollectionToString()}\n" +
                
                "FlexibleTrafficLightsStep]";
        }

        public FlexibleTrafficLightsStep(FlexibleTrafficLights flexibleNode,  bool makeRed = false)
        {
            
            
            this.flexibleNode = flexibleNode;
            

            endTransitionStart = null;
            stepDone = false;

            NodeGeometry nodeGeometry = NodeGeometry.Get(flexibleNode.NodeId);

            foreach (SegmentEndGeometry end in nodeGeometry.SegmentEndGeometries)
            {
                if (end == null)
                    continue;

                AddSegment(end.SegmentId, end.StartNode, makeRed);
            }
        }

        private FlexibleTrafficLightsStep()
        {

        }


        public FlexibleTrafficLightsStep(FlexibleTrafficLights flexibleNode, ushort[] lightValues, ushort[] segIDs, bool makeRed = false)
        {

            string s = "";
            
            for (int q = 0; q < segIDs.Length; q++)
            {
                s = s + " " + segIDs[q].ToString();
            }
           // Log.Info($"segIDs array: {s}");


            //Log.Info($"1");
            this.flexibleNode = flexibleNode;

            endTransitionStart = null;
            stepDone = false;

            NodeGeometry nodeGeometry = NodeGeometry.Get(flexibleNode.NodeId);
            //Log.Info($"2");
            foreach (SegmentEndGeometry end in nodeGeometry.SegmentEndGeometries)

            {
                if (end == null || end.OutgoingOneWay)
                {

                    continue;
                }
                CustomSegmentLights clonedLights = (CustomSegmentLights)CustomSegmentLightsManager.Instance.GetOrLiveSegmentLights(end.SegmentId, end.StartNode).Clone();
                //Log.Info($"3");
                CustomSegmentLights.Add(end.SegmentId, clonedLights);
                //Log.Info($"4");
                short index = 0;
                short i = 0;

                foreach (ushort segIdentifier in segIDs)
                {
                    //Log.Info($"{end.OutgoingOneWay}");

                    if (segIdentifier != 0)
                    {
                        if (SegmentEndGeometry.Get(segIdentifier, true).NodeId().Equals(nodeGeometry.NodeId))
                        {
                            if (!SegmentEndGeometry.Get(segIdentifier, true).OutgoingOneWay)
                            {
                                if (segIdentifier == end.SegmentId)
                                {
                                    index = i;
                                }
                                i++;
                            }
                        }
                        else
                        {
                            if (!SegmentEndGeometry.Get(segIdentifier, false).OutgoingOneWay)
                            {
                                if (segIdentifier == end.SegmentId)
                                {
                                    index = i;
                                }
                                i++;
                            }
                        }

                    }
                }
                
                int rightIndex = index * 3;
                int straightIndex = index * 3 + 1;
                int leftIndex = index * 3 + 2;
                //Log.Info($"right index: {rightIndex}");
               // Log.Info($"straight index: {straightIndex}");
               // Log.Info($"left index: {leftIndex}");
                int ps = 0;
                //CustomSegmentLight light = CustomSegmentLights[end.SegmentId].CustomLights[0];
                foreach (CustomSegmentLight light in CustomSegmentLights[end.SegmentId].CustomLights.Values)

                {
                    Log.Info($"{ps}");
                    ps++;
                    if (light != null)
                    {
                        if (lightValues[leftIndex] == 1)
                        {
                            light.LightLeft = RoadBaseAI.TrafficLightState.Green;
                        }
                        else
                        {
                            light.LightLeft = RoadBaseAI.TrafficLightState.Red;
                        }

                        if (lightValues[straightIndex] == 1)
                        {
                            light.LightMain = RoadBaseAI.TrafficLightState.Green;
                        }
                        else
                        {
                            light.LightMain = RoadBaseAI.TrafficLightState.Red;
                        }

                        if (lightValues[rightIndex] == 1)
                        {
                            light.LightRight = RoadBaseAI.TrafficLightState.Green;
                        }
                        else
                        {
                            light.LightRight = RoadBaseAI.TrafficLightState.Red;
                        }
                    }
                }

                Log.Info($"6");
                AddSegment(end.SegmentId, end.StartNode, makeRed, true);
            }

        }

        /// <summary>
        /// Checks if the green-to-red (=yellow) phase is finished
        /// </summary>
        /// <returns></returns>
        internal bool IsEndTransitionDone()
        {
            if (!flexibleNode.IsMasterNode())
            {
                FlexibleTrafficLights masterLights = flexibleNode.MasterLights();
                return masterLights.Steps[masterLights.CurrentStep].IsEndTransitionDone();
            }

            bool isStepDone = StepDone(false);
            bool ret = endTransitionStart != null && getCurrentFrame() > endTransitionStart && isStepDone;
#if DEBUGTTL
			if (GlobalConfig.Instance.DebugSwitches[7] && GlobalConfig.Instance.TTLDebugNodeId == flexibleNode.NodeId)
				Log._Debug($"FlexibleTrafficLightsStep.isEndTransitionDone() called for master NodeId={flexibleNode.NodeId}. CurrentStep={flexibleNode.CurrentStep} getCurrentFrame()={getCurrentFrame()} endTransitionStart={endTransitionStart} isStepDone={isStepDone} ret={ret}");
#endif
            return ret;
        }

        /// <summary>
        /// Checks if the green-to-red (=yellow) phase is currently active
        /// </summary>
        /// <returns></returns>
        internal bool IsInEndTransition()
        {
            if (!flexibleNode.IsMasterNode())
            {
                FlexibleTrafficLights masterLights = flexibleNode.MasterLights();
                return masterLights.Steps[masterLights.CurrentStep].IsInEndTransition();
            }

            bool isStepDone = StepDone(false);
            bool ret = endTransitionStart != null && getCurrentFrame() <= endTransitionStart && isStepDone;
#if DEBUGTTL
			if (GlobalConfig.Instance.DebugSwitches[7] && GlobalConfig.Instance.TTLDebugNodeId == flexibleNode.NodeId)
				Log._Debug($"FlexibleTrafficLightsStep.isInEndTransition() called for master NodeId={flexibleNode.NodeId}. CurrentStep={flexibleNode.CurrentStep} getCurrentFrame()={getCurrentFrame()} endTransitionStart={endTransitionStart} isStepDone={isStepDone} ret={ret}");
#endif
            return ret;
        }

        internal bool IsInStartTransition()
        {
            if (!flexibleNode.IsMasterNode())
            {
                FlexibleTrafficLights masterLights = flexibleNode.MasterLights();
                return masterLights.Steps[masterLights.CurrentStep].IsInStartTransition();
            }

            bool isStepDone = StepDone(false);
            bool ret = getCurrentFrame() == startFrame && !isStepDone;
#if DEBUGTTL
			if (GlobalConfig.Instance.DebugSwitches[7] && GlobalConfig.Instance.TTLDebugNodeId == flexibleNode.NodeId)
				Log._Debug($"FlexibleTrafficLightsStep.isInStartTransition() called for master NodeId={flexibleNode.NodeId}. CurrentStep={flexibleNode.CurrentStep} getCurrentFrame()={getCurrentFrame()} startFrame={startFrame} isStepDone={isStepDone} ret={ret}");
#endif

            return ret;
        }

        public RoadBaseAI.TrafficLightState GetLight(ushort segmentId, ExtVehicleType vehicleType, int lightType)
        {
            CustomSegmentLight segLight = CustomSegmentLights[segmentId].GetCustomLight(vehicleType);
            if (segLight != null)
            {
                switch (lightType)
                {
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
        public void Start(int previousStepRefIndex = -1)
        {
#if DEBUGTTL
			if (GlobalConfig.Instance.DebugSwitches[7] && GlobalConfig.Instance.TTLDebugNodeId == flexibleNode.NodeId)
				Log._Debug($"FlexibleTrafficLightsStep.Start: Starting step {flexibleNode.CurrentStep} @ {flexibleNode.NodeId}");
#endif

            this.startFrame = getCurrentFrame();
            Reset();
            PreviousStepRefIndex = previousStepRefIndex;

#if DEBUG
			/*if (GlobalConfig.Instance.DebugSwitches[2]) {
				if (flexibleNode.NodeId == 31605) {
					Log._Debug($"===== Step {flexibleNode.CurrentStep} @ node {flexibleNode.NodeId} =====");
					Log._Debug($"minTime: {minTime} maxTime: {maxTime}");
					foreach (KeyValuePair<ushort, CustomSegmentLights> e in segmentLights) {
						Log._Debug($"\tSegment {e.Key}:");
						Log._Debug($"\t{e.Value.ToString()}");
					}
				}
			}*/
#endif
        }

        internal void Reset()
        {
            this.endTransitionStart = null;
            lastFlowWaitCalc = 0;
            PreviousStepRefIndex = -1;
            NextStepRefIndex = -1;
            stepDone = false;
        }

        internal static uint getCurrentFrame()
        {
            return Constants.ServiceFactory.SimulationService.CurrentFrameIndex >> 6;
        }

        /// <summary>
        /// Updates "real-world" traffic light states according to the timed scripts
        /// </summary>
        public void UpdateLiveLights()
        {
            UpdateLiveLights(false);
        }

        public void UpdateLiveLights(bool noTransition)
        {
            try
            {
                CustomSegmentLightsManager customTrafficLightsManager = CustomSegmentLightsManager.Instance;

                bool atEndTransition = !noTransition && (IsInEndTransition() || IsEndTransitionDone()); // = yellow
                bool atStartTransition = !noTransition && !atEndTransition && IsInStartTransition(); // = red + yellow

#if DEBUGTTL
				if (flexibleNode == null) {
					Log.Error($"FlexibleTrafficLightsStep: flexibleNode is null!");
					return;
				}
#endif

                if (PreviousStepRefIndex >= flexibleNode.NumSteps())
                    PreviousStepRefIndex = -1;
                if (NextStepRefIndex >= flexibleNode.NumSteps())
                    NextStepRefIndex = -1;
                FlexibleTrafficLightsStep previousStep = flexibleNode.Steps[PreviousStepRefIndex >= 0 ? PreviousStepRefIndex : ((flexibleNode.CurrentStep + flexibleNode.Steps.Count - 1) % flexibleNode.Steps.Count)];
                FlexibleTrafficLightsStep nextStep = flexibleNode.Steps[NextStepRefIndex >= 0 ? NextStepRefIndex : ((flexibleNode.CurrentStep + 1) % flexibleNode.Steps.Count)];

#if DEBUGTTL
				if (previousStep == null) {
					Log.Error($"FlexibleTrafficLightsStep: previousStep is null!");
					//return;
				}

				if (nextStep == null) {
					Log.Error($"FlexibleTrafficLightsStep: nextStep is null!");
					//return;
				}

				if (previousStep.CustomSegmentLights == null) {
					Log.Error($"FlexibleTrafficLightsStep: previousStep.segmentLights is null!");
					//return;
				}

				if (nextStep.CustomSegmentLights == null) {
					Log.Error($"FlexibleTrafficLightsStep: nextStep.segmentLights is null!");
					//return;
				}

				if (CustomSegmentLights == null) {
					Log.Error($"FlexibleTrafficLightsStep: segmentLights is null!");
					//return;
				}
#endif

#if DEBUG
				//Log._Debug($"FlexibleTrafficLightsStep.SetLights({noTransition}) called for NodeId={flexibleNode.NodeId}. atStartTransition={atStartTransition} atEndTransition={atEndTransition}");
#endif

                foreach (KeyValuePair<ushort, CustomSegmentLights> e in CustomSegmentLights)
                {
                    var segmentId = e.Key;
                    var curStepSegmentLights = e.Value;

#if DEBUG
					//Log._Debug($"FlexibleTrafficLightsStep.SetLights({noTransition})   -> segmentId={segmentId} @ NodeId={flexibleNode.NodeId}");
#endif

                    CustomSegmentLights prevStepSegmentLights = null;
                    if (!previousStep.CustomSegmentLights.TryGetValue(segmentId, out prevStepSegmentLights))
                    {
#if DEBUGTTL
						if (GlobalConfig.Instance.DebugSwitches[7] && GlobalConfig.Instance.TTLDebugNodeId == flexibleNode.NodeId)
							Log.Warning($"FlexibleTrafficLightsStep: previousStep does not contain lights for segment {segmentId}!");
#endif
                        continue;
                    }

                    CustomSegmentLights nextStepSegmentLights = null;
                    if (!nextStep.CustomSegmentLights.TryGetValue(segmentId, out nextStepSegmentLights))
                    {
#if DEBUGTTL
						if (GlobalConfig.Instance.DebugSwitches[7] && GlobalConfig.Instance.TTLDebugNodeId == flexibleNode.NodeId)
							Log.Warning($"FlexibleTrafficLightsStep: nextStep does not contain lights for segment {segmentId}!");
#endif
                        continue;
                    }

                    //segLightState.makeRedOrGreen(); // TODO temporary fix

                    var liveSegmentLights = customTrafficLightsManager.GetSegmentLights(segmentId, curStepSegmentLights.StartNode, false);
                    if (liveSegmentLights == null)
                    {
                        continue;
                    }

                    RoadBaseAI.TrafficLightState pedLightState = calcLightState((RoadBaseAI.TrafficLightState)prevStepSegmentLights.PedestrianLightState, (RoadBaseAI.TrafficLightState)curStepSegmentLights.PedestrianLightState, (RoadBaseAI.TrafficLightState)nextStepSegmentLights.PedestrianLightState, atStartTransition, atEndTransition);
                    //Log._Debug($"TimedStep.SetLights: Setting pedestrian light state @ seg. {segmentId} to {pedLightState} {curStepSegmentLights.ManualPedestrianMode}");
                    liveSegmentLights.ManualPedestrianMode = curStepSegmentLights.ManualPedestrianMode;
                    liveSegmentLights.PedestrianLightState = liveSegmentLights.AutoPedestrianLightState = pedLightState;
                    //Log.Warning($"Step @ {flexibleNode.NodeId}: Segment {segmentId}: Ped.: {liveSegmentLights.PedestrianLightState.ToString()} / {liveSegmentLights.AutoPedestrianLightState.ToString()}");

#if DEBUGTTL
					if (GlobalConfig.Instance.DebugSwitches[7] && GlobalConfig.Instance.TTLDebugNodeId == flexibleNode.NodeId)
						if (curStepSegmentLights.VehicleTypes == null) {
							Log.Error($"FlexibleTrafficLightsStep: curStepSegmentLights.VehicleTypes is null!");
							return;
						}
#endif

                    foreach (ExtVehicleType vehicleType in curStepSegmentLights.VehicleTypes)
                    {
#if DEBUG
						//Log._Debug($"FlexibleTrafficLightsStep.SetLights({noTransition})     -> segmentId={segmentId} @ NodeId={flexibleNode.NodeId} for vehicle {vehicleType}");
#endif

                        CustomSegmentLight liveSegmentLight = liveSegmentLights.GetCustomLight(vehicleType);
                        if (liveSegmentLight == null)
                        {
#if DEBUGTTL
							if (GlobalConfig.Instance.DebugSwitches[7] && GlobalConfig.Instance.TTLDebugNodeId == flexibleNode.NodeId)
								Log._Debug($"Timed step @ seg. {segmentId}, node {flexibleNode.NodeId} has a traffic light for {vehicleType} but the live segment does not have one.");
#endif
                            continue;
                        }
                        CustomSegmentLight curStepSegmentLight = curStepSegmentLights.GetCustomLight(vehicleType);
                        CustomSegmentLight prevStepSegmentLight = prevStepSegmentLights.GetCustomLight(vehicleType);
                        CustomSegmentLight nextStepSegmentLight = nextStepSegmentLights.GetCustomLight(vehicleType);

#if DEBUGTTL
						if (curStepSegmentLight == null) {
							Log.Error($"FlexibleTrafficLightsStep: curStepSegmentLight is null!");
							//return;
						}

						if (prevStepSegmentLight == null) {
							Log.Error($"FlexibleTrafficLightsStep: prevStepSegmentLight is null!");
							//return;
						}

						if (nextStepSegmentLight == null) {
							Log.Error($"FlexibleTrafficLightsStep: nextStepSegmentLight is null!");
							//return;
						}
#endif

                        liveSegmentLight.currentMode = curStepSegmentLight.CurrentMode;
                        /*curStepSegmentLight.EnsureModeLights();
						prevStepSegmentLight.EnsureModeLights();
						nextStepSegmentLight.EnsureModeLights();*/
                        //Log.Info($" mode: {liveSegmentLight.currentMode} for {liveSegmentLight.SegmentId} ");

                        RoadBaseAI.TrafficLightState mainLight = calcLightState(prevStepSegmentLight.LightMain, curStepSegmentLight.LightMain, nextStepSegmentLight.LightMain, atStartTransition, atEndTransition);
                        RoadBaseAI.TrafficLightState leftLight = calcLightState(prevStepSegmentLight.LightLeft, curStepSegmentLight.LightLeft, nextStepSegmentLight.LightLeft, atStartTransition, atEndTransition);
                        RoadBaseAI.TrafficLightState rightLight = calcLightState(prevStepSegmentLight.LightRight, curStepSegmentLight.LightRight, nextStepSegmentLight.LightRight, atStartTransition, atEndTransition);
                        //Log.Info($" main: {mainLight} for {liveSegmentLight.SegmentId} ");
                        //Log.Info($" left: {leftLight} for {liveSegmentLight.SegmentId}");
                        //Log.Info($" right: {rightLight} for {liveSegmentLight.SegmentId}");
                        liveSegmentLight.ChangeLight(rightLight, mainLight, leftLight);

#if DEBUGTTL
						if (GlobalConfig.Instance.DebugSwitches[7] && GlobalConfig.Instance.TTLDebugNodeId == flexibleNode.NodeId)
							Log._Debug($"FlexibleTrafficLightsStep.SetLights({noTransition})     -> *SETTING* LightLeft={liveSegmentLight.LightLeft} LightMain={liveSegmentLight.LightMain} LightRight={liveSegmentLight.LightRight} for segmentId={segmentId} @ NodeId={flexibleNode.NodeId} for vehicle {vehicleType}");
#endif

                        //Log._Debug($"Step @ {flexibleNode.NodeId}: Segment {segmentId} for vehicle type {vehicleType}: L: {liveSegmentLight.LightLeft.ToString()} F: {liveSegmentLight.LightMain.ToString()} R: {liveSegmentLight.LightRight.ToString()}");
                    }

                    /*if (flexibleNode.NodeId == 20164) {
						Log._Debug($"Step @ {flexibleNode.NodeId}: Segment {segmentId}: {segmentLight.LightLeft.ToString()} {segmentLight.LightMain.ToString()} {segmentLight.LightRight.ToString()} {segmentLight.LightPedestrian.ToString()}");
                    }*/

                    liveSegmentLights.UpdateVisuals();
                }
            }
            catch (Exception e)
            {
                Log.Error($"Exception in TimedTrafficStep.UpdateLiveLights for node {flexibleNode.NodeId}: {e.ToString()}");
                //invalid = true;
            }
        }

        /// <summary>
        /// Adds a new segment to this step. It is cloned from the live custom traffic light.
        /// </summary>
        /// <param name="segmentId"></param>
        internal void AddSegment(ushort segmentId, bool startNode, bool makeRed)
        {
            CustomSegmentLights clonedLights = CustomSegmentLightsManager.Instance.GetOrLiveSegmentLights(segmentId, startNode).Clone(this);

            CustomSegmentLights.Add(segmentId, clonedLights);
            if (makeRed)
                CustomSegmentLights[segmentId].MakeRed();
            else
                CustomSegmentLights[segmentId].MakeRedOrGreen();
            CustomSegmentLightsManager.Instance.ApplyLightModes(segmentId, startNode, clonedLights);
        }

        private RoadBaseAI.TrafficLightState calcLightState(RoadBaseAI.TrafficLightState previousState, RoadBaseAI.TrafficLightState currentState, RoadBaseAI.TrafficLightState nextState, bool atStartTransition, bool atEndTransition)
        {
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
            {
               
                CustomSegmentLights[segmentId].MakeRed();
            }
            else
            {
               
                CustomSegmentLights[segmentId].MakeRedOrGreen();
            }
           
            CustomSegmentLightsManager.Instance.ApplyLightModes(segmentId, startNode, clonedLights);
            
        }



        /// <summary>
        /// Updates timed segment lights according to "real-world" traffic light states
        /// </summary>
        public void UpdateLights()
        {
            Log._Debug($"FlexibleTrafficLightsStep.UpdateLights: Updating lights of timed traffic light step @ {flexibleNode.NodeId}");
            foreach (KeyValuePair<ushort, CustomSegmentLights> e in CustomSegmentLights)
            {
                var segmentId = e.Key;
                var segLights = e.Value;

                Log._Debug($"FlexibleTrafficLightsStep.UpdateLights: Updating lights of timed traffic light step at seg. {e.Key} @ {flexibleNode.NodeId}");

                //if (segment == 0) continue;
                var liveSegLights = CustomSegmentLightsManager.Instance.GetSegmentLights(segmentId, segLights.StartNode, false);
                if (liveSegLights == null)
                    continue;

                segLights.SetLights(liveSegLights);
                Log._Debug($"FlexibleTrafficLightsStep.UpdateLights: Segment {segmentId} AutoPedState={segLights.AutoPedestrianLightState} live={liveSegLights.AutoPedestrianLightState}");
            }
        }

        public void SetStepDone()
        {
            stepDone = true;
        }

        public bool StepDone(bool updateValues)
        {
#if DEBUGTTL
			bool debug = GlobalConfig.Instance.DebugSwitches[7] && GlobalConfig.Instance.TTLDebugNodeId == flexibleNode.NodeId;
			if (debug) {
				Log._Debug($"StepDone: called for node {flexibleNode.NodeId} @ step {flexibleNode.CurrentStep}");
			}
#endif

            if (!flexibleNode.IsMasterNode())
            {
                FlexibleTrafficLights masterLights = flexibleNode.MasterLights();
                return masterLights.Steps[masterLights.CurrentStep].StepDone(updateValues);
            }
            // we are the master node

            if (flexibleNode.IsInTestMode())
            {
                return false;
            }
            if (stepDone)
            {
                return true;
            }

            if (getCurrentFrame() >= startFrame + 5)
            {
                // maximum time reached. switch!
#if DEBUGTTL
				if (debug)
					Log._Debug($"StepDone: step finished @ {flexibleNode.NodeId}");
#endif
                if (!stepDone && updateValues)
                {
                    stepDone = true;
                    endTransitionStart = getCurrentFrame();
                }
                return stepDone;
            }

            //TODO: CHECK IF THIS STEP IS DONE USING USER DEFINED FUNCTION


            return false;
        }

        /// <summary>
        /// Calculates the current metrics for flowing and waiting vehicles
        /// </summary>
        /// <param name="wait"></param>
        /// <param name="flow"></param>
        /// <returns>true if the values could be calculated, false otherwise</returns>
        

        internal void ChangeLightMode(ushort segmentId, ExtVehicleType vehicleType, CustomSegmentLight.Mode mode)
        {
            CustomSegmentLight light = CustomSegmentLights[segmentId].GetCustomLight(vehicleType);
            if (light != null)
            {
                light.CurrentMode = mode;
            }
        }

        public CustomSegmentLights RemoveSegmentLights(ushort segmentId)
        {
            CustomSegmentLights ret = null;
            if (CustomSegmentLights.TryGetValue(segmentId, out ret))
            {
                CustomSegmentLights.Remove(segmentId);
            }
            return ret;
        }

        public CustomSegmentLights GetSegmentLights(ushort segmentId)
        {
            return GetSegmentLights(flexibleNode.NodeId, segmentId);
        }

        public CustomSegmentLights GetSegmentLights(ushort nodeId, ushort segmentId)
        {
            if (nodeId != flexibleNode.NodeId)
            {
                Log.Warning($"FlexibleTrafficLightsStep @ node {flexibleNode.NodeId} does not handle custom traffic lights for node {nodeId}");
                return null;
            }

            CustomSegmentLights customLights;
            if (CustomSegmentLights.TryGetValue(segmentId, out customLights))
            {
                return customLights;
            }
            else
            {
                Log.Info($"FlexibleTrafficLightsStep @ node {flexibleNode.NodeId} does not know segment {segmentId}");
                return null;
            }
        }

        public bool RelocateSegmentLights(ushort sourceSegmentId, ushort targetSegmentId)
        {
            CustomSegmentLights sourceLights = null;
            if (!CustomSegmentLights.TryGetValue(sourceSegmentId, out sourceLights))
            {
                Log.Error($"FlexibleTrafficLightsStep.RelocateSegmentLights: Timed traffic light does not know source segment {sourceSegmentId}. Cannot relocate to {targetSegmentId}.");
                return false;
            }

            SegmentGeometry segGeo = SegmentGeometry.Get(targetSegmentId);
            if (segGeo == null)
            {
                Log.Error($"FlexibleTrafficLightsStep.RelocateSegmentLights: No geometry information available for target segment {targetSegmentId}");
                return false;
            }

            if (segGeo.StartNodeId() != flexibleNode.NodeId && segGeo.EndNodeId() != flexibleNode.NodeId)
            {
                Log.Error($"FlexibleTrafficLightsStep.RelocateSegmentLights: Target segment {targetSegmentId} is not connected to node {flexibleNode.NodeId}");
                return false;
            }

            bool startNode = segGeo.StartNodeId() == flexibleNode.NodeId;
            CustomSegmentLights.Remove(sourceSegmentId);
            sourceLights.Relocate(targetSegmentId, startNode, this);
            CustomSegmentLights[targetSegmentId] = sourceLights;

            Log._Debug($"FlexibleTrafficLightsStep.RelocateSegmentLights: Relocated lights: {sourceSegmentId} -> {targetSegmentId} @ node {flexibleNode.NodeId}");
            return true;
        }

        public bool SetSegmentLights(ushort nodeId, ushort segmentId, CustomSegmentLights lights)
        {
            if (nodeId != flexibleNode.NodeId)
            {
                Log.Warning($"FlexibleTrafficLightsStep @ node {flexibleNode.NodeId} does not handle custom traffic lights for node {nodeId}");
                return false;
            }

            return SetSegmentLights(segmentId, lights);
        }

        public bool SetSegmentLights(ushort segmentId, CustomSegmentLights lights)
        {
            SegmentGeometry segGeo = SegmentGeometry.Get(segmentId);
            if (segGeo == null)
            {
                Log.Error($"FlexibleTrafficLightsStep.SetSegmentLights: No geometry information available for target segment {segmentId}");
                return false;
            }

            if (segGeo.StartNodeId() != flexibleNode.NodeId && segGeo.EndNodeId() != flexibleNode.NodeId)
            {
                Log.Error($"FlexibleTrafficLightsStep.RelocateSegmentLights: Segment {segmentId} is not connected to node {flexibleNode.NodeId}");
                return false;
            }

            lights.Relocate(segmentId, segGeo.StartNodeId() == flexibleNode.NodeId, this);
            CustomSegmentLights[segmentId] = lights;
            Log._Debug($"FlexibleTrafficLightsStep.SetSegmentLights: Set lights @ seg. {segmentId}, node {flexibleNode.NodeId}");
            return true;
        }

        public short ClockwiseIndexOfSegmentEnd(SegmentEndId endId)
        {
            SegmentEndGeometry endGeo = SegmentEndGeometry.Get(endId);

            if (endGeo == null)
            {
                Log.Warning($"FlexibleTrafficLightsStep.ClockwiseIndexOfSegmentEnd: @ node {flexibleNode.NodeId}: No segment end geometry found for end id {endId}");
                return -1;
            }

            if (endGeo.NodeId() != flexibleNode.NodeId)
            {
                Log.Warning($"FlexibleTrafficLightsStep.ClockwiseIndexOfSegmentEnd: @ node {flexibleNode.NodeId} does not handle custom traffic lights for node {endGeo.NodeId()}");
                return -1;
            }

            if (CustomSegmentLights.ContainsKey(endId.SegmentId))
            {
                Log.Warning($"FlexibleTrafficLightsStep.ClockwiseIndexOfSegmentEnd: @ node {flexibleNode.NodeId} does not handle custom traffic lights for segment {endId.SegmentId}");
                return -1;
            }

            short index = CustomSegmentLightsManager.Instance.ClockwiseIndexOfSegmentEnd(endId);
            index += flexibleNode.RotationOffset;
            return (short)(index % (endGeo.NumConnectedSegments + 1));
        }
    }
}
