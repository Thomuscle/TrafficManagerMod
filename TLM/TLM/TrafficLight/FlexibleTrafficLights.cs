
#define DEBUGTTLx
using System;
using System.Collections.Generic;
using ColossalFramework;
using TrafficManager.Custom.AI;
using TrafficManager.Geometry;
using TrafficManager.Traffic;
using TrafficManager.Manager;
using System.Linq;
using TrafficManager.Util;
using System.Threading;
using TrafficManager.State;
using GenericGameBridge.Service;
using CSUtil.Commons;


namespace TrafficManager.TrafficLight
{
    // TODO [version 1.10] define FlexibleTrafficLights per node group, not per individual nodes
    // TODO class marked for complete rework in version 1.10
    public class FlexibleTrafficLights : IObserver<NodeGeometry>
    {
        public ushort NodeId
        {
            get; private set;
        }

        /// <summary>
        /// In case the traffic light is set for a group of nodes, the master node decides
        /// if all member steps are done.
        /// </summary>
        internal ushort masterNodeId;

        public List<FlexibleTrafficLightsStep> Steps = new List<FlexibleTrafficLightsStep>();
        public int CurrentStep = 0;

        public List<ushort> NodeGroup;
        private bool testMode = false;

        private bool started = false;

        /// <summary>
        /// Indicates the total amount and direction of rotation that was applied to this timed traffic light
        /// </summary>
        public short RotationOffset { get; private set; } = 0;

        private IDisposable nodeGeometryUnsubscriber = null;
        private object geoLock = new object();

        public IDictionary<ushort, IDictionary<ushort, ArrowDirection>> Directions { get; private set; } = null;

        /// <summary>
        /// Segment ends that were set up for this timed traffic light
        /// </summary>
        private ICollection<SegmentEndId> segmentEndIds = new HashSet<SegmentEndId>();

        public override string ToString()
        {
            return $"[FlexibleTrafficLights\n" +
                "\t" + $"NodeId = {NodeId}\n" +
                "\t" + $"masterNodeId = {masterNodeId}\n" +
                "\t" + $"Steps = {Steps.CollectionToString()}\n" +
                "\t" + $"NodeGroup = {NodeGroup.CollectionToString()}\n" +
                "\t" + $"testMode = {testMode}\n" +
                "\t" + $"started = {started}\n" +
                "\t" + $"Directions = {Directions.DictionaryToString()}\n" +
                "\t" + $"segmentEndIds = {segmentEndIds.CollectionToString()}\n" +
                "FlexibleTrafficLights]";
        }

        public FlexibleTrafficLights(ushort nodeId, IEnumerable<ushort> nodeGroup)
        {
            this.NodeId = nodeId;
            NodeGroup = new List<ushort>(nodeGroup);
            masterNodeId = NodeGroup[0];

            UpdateDirections(NodeGeometry.Get(nodeId));
            UpdateSegmentEnds();
            SubscribeToNodeGeometry();

            started = false;
        }

        private FlexibleTrafficLights()
        {

        }

        

       



        private void UpdateDirections(NodeGeometry nodeGeo)
        {
            Log._Debug($">>>>> FlexibleTrafficLights.UpdateDirections: called for node {NodeId}");
            Directions = new TinyDictionary<ushort, IDictionary<ushort, ArrowDirection>>();
            foreach (SegmentEndGeometry srcSegEndGeo in nodeGeo.SegmentEndGeometries)
            {
                if (srcSegEndGeo == null)
                    continue;
                Log._Debug($"FlexibleTrafficLights.UpdateDirections: Processing source segment {srcSegEndGeo.SegmentId}");

                SegmentGeometry srcSegGeo = srcSegEndGeo.GetSegmentGeometry();
                if (srcSegGeo == null)
                {
                    continue;
                }
                IDictionary<ushort, ArrowDirection> dirs = new TinyDictionary<ushort, ArrowDirection>();
                Directions.Add(srcSegEndGeo.SegmentId, dirs);
                foreach (SegmentEndGeometry trgSegEndGeo in nodeGeo.SegmentEndGeometries)
                {
                    if (trgSegEndGeo == null)
                        continue;

                    ArrowDirection dir = srcSegGeo.GetDirection(trgSegEndGeo.SegmentId, srcSegEndGeo.StartNode);
                    if (dir == ArrowDirection.None)
                    {
                        Log.Error($"FlexibleTrafficLights.UpdateDirections: Processing source segment {srcSegEndGeo.SegmentId}, target segment {trgSegEndGeo.SegmentId}: Invalid direction {dir}");
                        continue;
                    }
                    dirs.Add(trgSegEndGeo.SegmentId, dir);
                    Log._Debug($"FlexibleTrafficLights.UpdateDirections: Processing source segment {srcSegEndGeo.SegmentId}, target segment {trgSegEndGeo.SegmentId}: adding dir {dir}");
                }
            }
            Log._Debug($"<<<<< FlexibleTrafficLights.UpdateDirections: finished for node {NodeId}.");
        }

        private void UnsubscribeFromNodeGeometry()
        {
            if (nodeGeometryUnsubscriber != null)
            {
                try
                {
                    Monitor.Enter(geoLock);

                    nodeGeometryUnsubscriber.Dispose();
                    nodeGeometryUnsubscriber = null;
                }
                finally
                {
                    Monitor.Exit(geoLock);
                }
            }
        }

        private void SubscribeToNodeGeometry()
        {
            if (nodeGeometryUnsubscriber != null)
            {
                return;
            }

            try
            {
                Monitor.Enter(geoLock);

                nodeGeometryUnsubscriber = NodeGeometry.Get(NodeId).Subscribe(this);
            }
            finally
            {
                Monitor.Exit(geoLock);
            }
        }

        public void OnUpdate(NodeGeometry geometry)
        {
            // not required since TrafficLightSimulation handles this for us: OnGeometryUpdate() is being called.
            // TODO improve
        }

        public bool IsMasterNode()
        {
            return masterNodeId == NodeId;
        }

        //this is our one
        public FlexibleTrafficLightsStep AddStep(ushort[] lightValues, ushort[] segIDs, ushort segID, bool makeRed = false)
        {
            // TODO [version 1.9] currently, this method must be called for each node in the node group individually

            FlexibleTrafficLightsStep step = new FlexibleTrafficLightsStep(this, segID, lightValues, segIDs, makeRed);
            Steps.Add(step);
            return step;
        }
        public void Start()
        {
            // TODO [version 1.10] currently, this method must be called for each node in the node group individually

            /*if (!housekeeping())
				return;*/

            Constants.ServiceFactory.NetService.ProcessNode(NodeId, delegate (ushort nodeId, ref NetNode node) {
                TrafficLightManager.Instance.AddTrafficLight(NodeId, ref node);
                return true;
            });

            foreach (FlexibleTrafficLightsStep step in Steps)
            {
                foreach (KeyValuePair<ushort, CustomSegmentLights> e in step.CustomSegmentLights)
                {
                    e.Value.housekeeping(true, true);
                }
            }

            CheckInvalidPedestrianLights();

            CurrentStep = 0;
            Steps[0].Start();
            Steps[0].UpdateLiveLights();

            started = true;
        }

        private void CheckInvalidPedestrianLights()
        {
            CustomSegmentLightsManager customTrafficLightsManager = CustomSegmentLightsManager.Instance;
            NodeGeometry nodeGeometry = NodeGeometry.Get(NodeId);

            //Log._Debug($"Checking for invalid pedestrian lights @ {NodeId}.");
            foreach (SegmentEndGeometry end in nodeGeometry.SegmentEndGeometries)
            {
                if (end == null)
                    continue;

                //Log._Debug($"Checking seg. {segmentId} @ {NodeId}.");
                bool needsAlwaysGreenPedestrian = true;
                int i = 0;
                foreach (FlexibleTrafficLightsStep step in Steps)
                {
                    //Log._Debug($"Checking step {i}, seg. {segmentId} @ {NodeId}.");
                    if (!step.CustomSegmentLights.ContainsKey(end.SegmentId))
                    {
                        //Log._Debug($"Step {i} @ {NodeId} does not contain a segment light for seg. {segmentId}.");
                        ++i;
                        continue;
                    }
                    //Log._Debug($"Checking step {i}, seg. {segmentId} @ {NodeId}: {step.segmentLights[segmentId].PedestrianLightState} (pedestrianLightState={step.segmentLights[segmentId].pedestrianLightState}, ManualPedestrianMode={step.segmentLights[segmentId].ManualPedestrianMode}, AutoPedestrianLightState={step.segmentLights[segmentId].AutoPedestrianLightState}");
                    if (step.CustomSegmentLights[end.SegmentId].PedestrianLightState == RoadBaseAI.TrafficLightState.Green)
                    {
                        //Log._Debug($"Step {i} @ {NodeId} has a green ped. light @ seg. {segmentId}.");
                        needsAlwaysGreenPedestrian = false;
                        break;
                    }
                    ++i;
                }
                //Log._Debug($"Setting InvalidPedestrianLight of seg. {segmentId} @ {NodeId} to {needsAlwaysGreenPedestrian}.");
                customTrafficLightsManager.GetSegmentLights(end.SegmentId, end.StartNode).InvalidPedestrianLight = needsAlwaysGreenPedestrian;
            }
        }

        private void ClearInvalidPedestrianLights()
        {
            CustomSegmentLightsManager customTrafficLightsManager = CustomSegmentLightsManager.Instance;

            NodeGeometry nodeGeometry = NodeGeometry.Get(NodeId);

            //Log._Debug($"Checking for invalid pedestrian lights @ {NodeId}.");
            foreach (SegmentEndGeometry end in nodeGeometry.SegmentEndGeometries)
            {
                if (end == null)
                    continue;

                customTrafficLightsManager.GetSegmentLights(end.SegmentId, end.StartNode).InvalidPedestrianLight = false;
            }
        }

        internal void RemoveNodeFromGroup(ushort otherNodeId)
        {
            // TODO [version 1.10] currently, this method must be called for each node in the node group individually

            NodeGroup.Remove(otherNodeId);
            if (NodeGroup.Count <= 0)
            {
                TrafficLightSimulationManager.Instance.RemoveNodeFromSimulation(NodeId, true, false);
                return;
            }
            masterNodeId = NodeGroup[0];
        }

        internal bool housekeeping()
        {
            // TODO [version 1.10] currently, this method must be called for each node in the node group individually
            //Log._Debug($"Housekeeping timed light @ {NodeId}");

            if (NodeGroup == null || NodeGroup.Count <= 0)
            {
                Stop();
                return false;
            }

            //Log.Warning($"Timed housekeeping: Setting master node to {NodeGroup[0]}");
            masterNodeId = NodeGroup[0];

            if (IsStarted())
                CheckInvalidPedestrianLights();

            int i = 0;
            foreach (FlexibleTrafficLightsStep step in Steps)
            {
                foreach (CustomSegmentLights lights in step.CustomSegmentLights.Values)
                {
                    //Log._Debug($"----- Housekeeping timed light at step {i}, seg. {lights.SegmentId} @ {NodeId}");
                    lights.housekeeping(true, true);
                }
                ++i;
            }

            return true;
        }

        public void MoveStep(int oldPos, int newPos)
        {
            // TODO [version 1.10] currently, this method must be called for each node in the node group individually

            var oldStep = Steps[oldPos];

            Steps.RemoveAt(oldPos);
            Steps.Insert(newPos, oldStep);
        }

        public void Stop()
        {
            // TODO [version 1.10] currently, this method must be called for each node in the node group individually

            started = false;
            foreach (FlexibleTrafficLightsStep step in Steps)
            {
                step.Reset();
            }
            ClearInvalidPedestrianLights();
        }

        ~FlexibleTrafficLights()
        {
            Destroy();
        }

        internal void Destroy()
        {
            // TODO [version 1.10] currently, this method must be called for each node in the node group individually

            started = false;
            DestroySegmentEnds();
            Steps = null;
            NodeGroup = null;
            UnsubscribeFromNodeGeometry();
        }

        public bool IsStarted()
        {
            // TODO [version 1.10] currently, this method must be called for each node in the node group individually

            return started;
        }

        public int NumSteps()
        {
            // TODO [version 1.10] currently, this method must be called for each node in the node group individually

            return Steps.Count;
        }

        public FlexibleTrafficLightsStep GetStep(int stepId)
        {
            // TODO [version 1.10] currently, this method must be called for each node in the node group individually

            return Steps[stepId];
        }

        public void SimulationStep()
        {
            // TODO [version 1.10] this method is currently called on each node, but should be called on the master node only

#if DEBUGTTL
			bool debug = GlobalConfig.Instance.DebugSwitches[7] && GlobalConfig.Instance.TTLDebugNodeId == NodeId;
#endif

            if (!IsMasterNode() || !IsStarted())
            {
#if DEBUGTTL
				if (debug)
					Log._Debug($"FlexibleTrafficLights.SimulationStep(): TTL SimStep: *STOP* NodeId={NodeId} isMasterNode={IsMasterNode()} IsStarted={IsStarted()}");
#endif
                return;
            }
            // we are the master node

            /*if (!housekeeping()) {
#if DEBUGTTL
				Log.Warning($"TTL SimStep: *STOP* NodeId={NodeId} Housekeeping detected that this timed traffic light has become invalid: {NodeId}.");
#endif
				Stop();
				return;
			}*/

#if DEBUGTTL
			if (debug)
				Log._Debug($"FlexibleTrafficLights.SimulationStep(): TTL SimStep: NodeId={NodeId} Setting lights (1)");
#endif
            SetLights();

            if (!Steps[CurrentStep].StepDone(true))
            {
#if DEBUGTTL
				if (debug)
					Log._Debug($"FlexibleTrafficLights.SimulationStep(): TTL SimStep: *STOP* NodeId={NodeId} current step ({CurrentStep}) is not done.");
#endif
                return;
            }
            // step is done

#if DEBUGTTL
			if (debug)
				Log._Debug($"FlexibleTrafficLights.SimulationStep(): TTL SimStep: NodeId={NodeId} Setting lights (2)");
#endif

            TrafficLightSimulationManager tlsMan = TrafficLightSimulationManager.Instance;
            if (Steps[CurrentStep].NextStepRefIndex < 0)
            {
#if DEBUGTTL
				if (debug) {
					Log._Debug($"FlexibleTrafficLights.SimulationStep(): Step {CurrentStep} is done at timed light {NodeId}. Determining next step.");
				}
#endif
                // next step has not yet identified yet. check for minTime=0 steps
                int nextStepIndex = (CurrentStep + 1) % NumSteps();
                if (Steps[nextStepIndex].minTime == 0)
                {
                    // next step has minTime=0. calculate flow/wait ratios and compare.
                    int prevStepIndex = CurrentStep;

                    float maxWaitFlowDiff = Steps[CurrentStep].minFlow - Steps[CurrentStep].maxWait;
                    if (float.IsNaN(maxWaitFlowDiff))
                        maxWaitFlowDiff = float.MinValue;
                    int bestNextStepIndex = prevStepIndex;

#if DEBUGTTL
					if (debug) {
						Log._Debug($"FlexibleTrafficLights.SimulationStep(): Next step {nextStepIndex} has minTime = 0 at timed light {NodeId}. Old step {CurrentStep} has waitFlowDiff={maxWaitFlowDiff} (flow={Steps[CurrentStep].minFlow}, wait={Steps[CurrentStep].maxWait}).");
					}
#endif

                    while (nextStepIndex != prevStepIndex)
                    {
                        float wait;
                        float flow;
                        Steps[nextStepIndex].calcWaitFlow(false, nextStepIndex, out wait, out flow);

                        float flowWaitDiff = flow - wait;
                        if (flowWaitDiff > maxWaitFlowDiff)
                        {
                            maxWaitFlowDiff = flowWaitDiff;
                            bestNextStepIndex = nextStepIndex;
                        }

#if DEBUGTTL
						if (debug) {
							Log._Debug($"FlexibleTrafficLights.SimulationStep(): Checking upcoming step {nextStepIndex} @ node {NodeId}: flow={flow} wait={wait} minTime={Steps[nextStepIndex].minTime}. bestWaitFlowDiff={bestNextStepIndex}, bestNextStepIndex={bestNextStepIndex}");
						}
#endif

                        if (Steps[nextStepIndex].minTime != 0)
                        {
                            bestNextStepIndex = (prevStepIndex + 1) % NumSteps();
                            break;
                        }

                        nextStepIndex = (nextStepIndex + 1) % NumSteps();
                    }


                    if (bestNextStepIndex == CurrentStep)
                    {
#if DEBUGTTL
						if (debug) {
							Log._Debug($"FlexibleTrafficLights.SimulationStep(): Best next step {bestNextStepIndex} (wait/flow diff = {maxWaitFlowDiff}) equals CurrentStep @ node {NodeId}.");
						}
#endif

                        // restart the current step
                        foreach (ushort slaveNodeId in NodeGroup)
                        {
                            TrafficLightSimulation slaveSim = tlsMan.GetNodeSimulation(slaveNodeId);
                            if (slaveSim == null || !slaveSim.IsTimedLight())
                            {
                                continue;
                            }

                            slaveSim.TimedLight.Steps[CurrentStep].Start(CurrentStep);
                            slaveSim.TimedLight.Steps[CurrentStep].UpdateLiveLights();
                        }
                        return;
                    }
                    else
                    {
#if DEBUGTTL
						if (debug) {
							Log._Debug($"FlexibleTrafficLights.SimulationStep(): Best next step {bestNextStepIndex} (wait/flow diff = {maxWaitFlowDiff}) does not equal CurrentStep @ node {NodeId}.");
						}
#endif

                        // set next step reference index for assuring a correct end transition
                        foreach (ushort slaveNodeId in NodeGroup)
                        {
                            TrafficLightSimulation slaveSim = tlsMan.GetNodeSimulation(slaveNodeId);
                            if (slaveSim == null || !slaveSim.IsTimedLight())
                            {
                                continue;
                            }
                            FlexibleTrafficLights timedLights = slaveSim.TimedLight;
                            timedLights.Steps[CurrentStep].NextStepRefIndex = bestNextStepIndex;
                        }
                    }
                }
                else
                {
                    Steps[CurrentStep].NextStepRefIndex = nextStepIndex;
                }
            }

            SetLights(); // check if this is needed

            if (!Steps[CurrentStep].IsEndTransitionDone())
            {
#if DEBUGTTL
				if (debug)
					Log._Debug($"FlexibleTrafficLights.SimulationStep(): TTL SimStep: *STOP* NodeId={NodeId} current step ({CurrentStep}): end transition is not done.");
#endif
                return;
            }
            // ending transition (yellow) finished

#if DEBUGTTL
			if (debug)
				Log._Debug($"FlexibleTrafficLights.SimulationStep(): TTL SimStep: NodeId={NodeId} ending transition done. NodeGroup={string.Join(", ", NodeGroup.Select(x => x.ToString()).ToArray())}, nodeId={NodeId}, NumSteps={NumSteps()}");
#endif

            // change step
            int newStepIndex = Steps[CurrentStep].NextStepRefIndex;
            int oldStepIndex = CurrentStep;

            foreach (ushort slaveNodeId in NodeGroup)
            {
                TrafficLightSimulation slaveSim = tlsMan.GetNodeSimulation(slaveNodeId);
                if (slaveSim == null || !slaveSim.IsTimedLight())
                {
                    continue;
                }
                FlexibleTrafficLights timedLights = slaveSim.TimedLight;
                timedLights.CurrentStep = newStepIndex;

#if DEBUGTTL
				if (debug)
					Log._Debug($"FlexibleTrafficLights.SimulationStep(): TTL SimStep: NodeId={slaveNodeId} setting lights of next step: {CurrentStep}");
#endif

                timedLights.Steps[oldStepIndex].NextStepRefIndex = -1;
                timedLights.Steps[newStepIndex].Start(oldStepIndex);
                timedLights.Steps[newStepIndex].UpdateLiveLights();
            }
        }

        public void SetLights(bool noTransition = false)
        {
            if (Steps.Count <= 0)
            {
                return;
            }

            TrafficLightSimulationManager tlsMan = TrafficLightSimulationManager.Instance;

            // set lights
            foreach (ushort slaveNodeId in NodeGroup)
            {
                TrafficLightSimulation slaveSim = tlsMan.GetNodeSimulation(slaveNodeId);
                if (slaveSim == null || !slaveSim.IsTimedLight())
                {
                    //TrafficLightSimulation.RemoveNodeFromSimulation(slaveNodeId, false); // we iterate over NodeGroup!!
                    continue;
                }
                slaveSim.TimedLight.Steps[CurrentStep].UpdateLiveLights(noTransition);
            }
        }

        public void SkipStep(bool setLights = true, int prevStepRefIndex = -1)
        {
            if (!IsMasterNode())
                return;

            TrafficLightSimulationManager tlsMan = TrafficLightSimulationManager.Instance;

            var newCurrentStep = (CurrentStep + 1) % NumSteps();
            foreach (ushort slaveNodeId in NodeGroup)
            {
                TrafficLightSimulation slaveSim = tlsMan.GetNodeSimulation(slaveNodeId);
                if (slaveSim == null || !slaveSim.IsTimedLight())
                {
                    continue;
                }

                slaveSim.TimedLight.Steps[CurrentStep].SetStepDone();
                slaveSim.TimedLight.CurrentStep = newCurrentStep;
                slaveSim.TimedLight.Steps[newCurrentStep].Start(prevStepRefIndex);
                if (setLights)
                    slaveSim.TimedLight.Steps[newCurrentStep].UpdateLiveLights();
            }
        }

        public long CheckNextChange(ushort segmentId, bool startNode, ExtVehicleType vehicleType, int lightType)
        {
            var curStep = CurrentStep;
            var nextStep = (CurrentStep + 1) % NumSteps();
            var numFrames = Steps[CurrentStep].MaxTimeRemaining();

            RoadBaseAI.TrafficLightState currentState;
            CustomSegmentLights segmentLights = CustomSegmentLightsManager.Instance.GetSegmentLights(segmentId, startNode, false);
            if (segmentLights == null)
            {
                Log._Debug($"CheckNextChange: No segment lights at node {NodeId}, segment {segmentId}");
                return 99;
            }
            CustomSegmentLight segmentLight = segmentLights.GetCustomLight(vehicleType);
            if (segmentLight == null)
            {
                Log._Debug($"CheckNextChange: No segment light at node {NodeId}, segment {segmentId}");
                return 99;
            }

            if (lightType == 0)
                currentState = segmentLight.LightMain;
            else if (lightType == 1)
                currentState = segmentLight.LightLeft;
            else if (lightType == 2)
                currentState = segmentLight.LightRight;
            else
                currentState = segmentLights.PedestrianLightState == null ? RoadBaseAI.TrafficLightState.Red : (RoadBaseAI.TrafficLightState)segmentLights.PedestrianLightState;


            while (true)
            {
                if (nextStep == curStep)
                {
                    numFrames = 99;
                    break;
                }

                var light = Steps[nextStep].GetLight(segmentId, vehicleType, lightType);

                if (light != currentState)
                {
                    break;
                }
                else
                {
                    numFrames += Steps[nextStep].maxTime;
                }

                nextStep = (nextStep + 1) % NumSteps();
            }

            return numFrames;
        }

        public void ResetSteps()
        {
            Steps.Clear();
        }

        public void RemoveStep(int id)
        {
            Steps.RemoveAt(id);
        }

        internal void OnGeometryUpdate()
        {
            Log._Debug($"FlexibleTrafficLights.OnGeometryUpdate: called for timed traffic light @ {NodeId}");

            NodeGeometry nodeGeometry = NodeGeometry.Get(NodeId);

            UpdateDirections(nodeGeometry);
            UpdateSegmentEnds();

            if (NumSteps() <= 0)
            {
                Log._Debug($"FlexibleTrafficLights.OnGeometryUpdate: no steps @ {NodeId}");
                return;
            }

            BackUpInvalidStepSegments(nodeGeometry);
            HandleNewSegments(nodeGeometry);
        }

        /// <summary>
        /// Moves all custom segment lights that are associated with an invalid segment to a special container for later reuse
        /// </summary>
        private void BackUpInvalidStepSegments(NodeGeometry nodeGeo)
        {
            Log._Debug($"FlexibleTrafficLights.BackUpInvalidStepSegments: called for timed traffic light @ {NodeId}");

            ICollection<ushort> validSegments = new HashSet<ushort>();
            foreach (SegmentEndGeometry end in nodeGeo.SegmentEndGeometries)
            {
                if (end == null)
                {
                    continue;
                }

                validSegments.Add(end.SegmentId);
            }

            Log._Debug($"FlexibleTrafficLights.BackUpInvalidStepSegments: valid segments @ {NodeId}: {validSegments.CollectionToString()}");

            int i = 0;
            foreach (FlexibleTrafficLightsStep step in Steps)
            {
                ICollection<ushort> invalidSegmentIds = new HashSet<ushort>();
                foreach (KeyValuePair<ushort, CustomSegmentLights> e in step.CustomSegmentLights)
                {
                    if (!validSegments.Contains(e.Key))
                    {
                        step.InvalidSegmentLights.AddLast(e.Value);
                        invalidSegmentIds.Add(e.Key);
                        Log._Debug($"FlexibleTrafficLights.BackUpInvalidStepSegments: Detected invalid segment @ step {i}, node {NodeId}: {e.Key}");
                    }
                }

                foreach (ushort invalidSegmentId in invalidSegmentIds)
                {
                    Log._Debug($"FlexibleTrafficLights.BackUpInvalidStepSegments: Remvoing invalid segment {invalidSegmentId} from step {i} @ node {NodeId}");
                    step.CustomSegmentLights.Remove(invalidSegmentId);
                }

                ++i;
            }
        }

        /// <summary>
        /// Processes new segments and adds them to the steps. If steps contain a custom light
        /// for an old invalid segment, this light is being reused for the new segment.
        /// </summary>
        /// <param name="nodeGeo"></param>
        private void HandleNewSegments(NodeGeometry nodeGeo)
        {
            CustomSegmentLightsManager customTrafficLightsManager = CustomSegmentLightsManager.Instance;
            TrafficPriorityManager prioMan = TrafficPriorityManager.Instance;

            //Log._Debug($"Checking for invalid pedestrian lights @ {NodeId}.");
            foreach (SegmentEndGeometry end in nodeGeo.SegmentEndGeometries)
            {
                if (end == null)
                {
                    continue;
                }
                Log._Debug($"FlexibleTrafficLights.OnGeometryUpdate: handling existing seg. {end.SegmentId} @ {NodeId}");

                List<ushort> invalidSegmentIds = new List<ushort>();
                if (Steps[0].CustomSegmentLights.ContainsKey(end.SegmentId))
                {
                    continue;
                }

                // segment was created
                RotationOffset = 0;
                Log._Debug($"FlexibleTrafficLights.OnGeometryUpdate: New segment detected: {end.SegmentId} @ {NodeId}");

                int stepIndex = -1;
                foreach (FlexibleTrafficLightsStep step in Steps)
                {
                    ++stepIndex;

                    LinkedListNode<CustomSegmentLights> lightsToReuseNode = step.InvalidSegmentLights.First;
                    if (lightsToReuseNode == null)
                    {
                        // no old segment found: create a fresh custom light
                        Log._Debug($"Adding new segment {end.SegmentId} to node {NodeId} without reusing old segment");
                        step.AddSegment(end.SegmentId, end.StartNode, true);
                    }
                    else
                    {
                        // reuse old lights
                        step.InvalidSegmentLights.RemoveFirst();
                        CustomSegmentLights lightsToReuse = lightsToReuseNode.Value;

                        Log._Debug($"Replacing old segment @ {NodeId} with new segment {end.SegmentId}");
                        lightsToReuse.Relocate(end.SegmentId, end.StartNode);
                        step.SetSegmentLights(end.SegmentId, lightsToReuse);
                    }
                }
            }
        }

        internal FlexibleTrafficLights MasterLights()
        {
            TrafficLightSimulation masterSim = TrafficLightSimulationManager.Instance.GetNodeSimulation(masterNodeId);
            if (masterSim == null || !masterSim.IsTimedLight())
                return null;
            return masterSim.TimedLight;
        }

        internal void SetTestMode(bool testMode)
        {
            this.testMode = false;
            if (!IsStarted())
                return;
            this.testMode = testMode;
        }

        internal bool IsInTestMode()
        {
            if (!IsStarted())
                testMode = false;
            return testMode;
        }

        internal void ChangeLightMode(ushort segmentId, ExtVehicleType vehicleType, CustomSegmentLight.Mode mode)
        {
            SegmentGeometry segGeo = SegmentGeometry.Get(segmentId);
            if (segGeo == null)
            {
                Log.Error($"FlexibleTrafficLights.ChangeLightMode: No geometry information available for segment {segmentId}");
                return;
            }

            foreach (FlexibleTrafficLightsStep step in Steps)
            {
                step.ChangeLightMode(segmentId, vehicleType, mode);
            }
            CustomSegmentLightsManager.Instance.SetLightMode(segmentId, segGeo.StartNodeId() == NodeId, vehicleType, mode);
        }

        private void UpdateSegmentEnds()
        {
            Log._Debug($"FlexibleTrafficLights.UpdateSegmentEnds: called for node {NodeId}");

            ICollection<SegmentEndId> segmentEndsToDelete = new HashSet<SegmentEndId>();
            // update currently set segment ends
            foreach (SegmentEndId endId in segmentEndIds)
            {
                Log._Debug($"FlexibleTrafficLights.UpdateSegmentEnds: updating existing segment end {endId} for node {NodeId}");
                if (!SegmentEndManager.Instance.UpdateSegmentEnd(endId))
                {
                    Log._Debug($"FlexibleTrafficLights.UpdateSegmentEnds: segment end {endId} @ node {NodeId} is invalid");
                    segmentEndsToDelete.Add(endId);
                }
            }

            // remove all invalid segment ends
            foreach (SegmentEndId endId in segmentEndsToDelete)
            {
                Log._Debug($"FlexibleTrafficLights.UpdateSegmentEnds: Removing invalid segment end {endId} @ node {NodeId}");
                segmentEndIds.Remove(endId);
            }

            // set up new segment ends
            Log._Debug($"FlexibleTrafficLights.UpdateSegmentEnds: Setting up new segment ends @ node {NodeId}");
            NodeGeometry nodeGeo = NodeGeometry.Get(NodeId);
            foreach (SegmentEndGeometry endGeo in nodeGeo.SegmentEndGeometries)
            {
                if (endGeo == null)
                {
                    continue;
                }

                if (segmentEndIds.Contains(endGeo))
                {
                    Log._Debug($"FlexibleTrafficLights.UpdateSegmentEnds: Node {NodeId} already knows segment {endGeo.SegmentId}");
                    continue;
                }

                Log._Debug($"FlexibleTrafficLights.UpdateSegmentEnds: Adding segment {endGeo.SegmentId} to node {NodeId}");
                segmentEndIds.Add(SegmentEndManager.Instance.GetOrAddSegmentEnd(endGeo.SegmentId, endGeo.StartNode));
            }
            Log._Debug($"FlexibleTrafficLights.UpdateSegmentEnds: finished for node {NodeId}");
        }

        private void DestroySegmentEnds()
        {
            Log._Debug($"FlexibleTrafficLights.DestroySegmentEnds: Destroying segment ends @ node {NodeId}");
            foreach (SegmentEndId endId in segmentEndIds)
            {
                Log._Debug($"FlexibleTrafficLights.DestroySegmentEnds: Destroying segment end {endId} @ node {NodeId}");
                // TODO only remove if no priority sign is located at the segment end (although this is currently not possible)
                SegmentEndManager.Instance.RemoveSegmentEnd(endId);
            }
            segmentEndIds.Clear();
            Log._Debug($"FlexibleTrafficLights.DestroySegmentEnds: finished for node {NodeId}");
        }
    }
}
