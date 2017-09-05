
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
using ColossalFramework.Plugins;
using TrafficManager.UI;

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
        public int SelectedAlgorithm = 0;
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

        public FlexibleTrafficLights(ushort nodeId, IEnumerable<ushort> nodeGroup, int selectedAlgorithm)
        {
            this.NodeId = nodeId;
            NodeGroup = new List<ushort>(nodeGroup);
            masterNodeId = NodeGroup[0];

            UpdateDirections(NodeGeometry.Get(nodeId));
            UpdateSegmentEnds();
            SubscribeToNodeGeometry();
            SelectedAlgorithm = selectedAlgorithm;
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
        public FlexibleTrafficLightsStep AddStep(ushort[] lightValues, ushort[] segIDs, bool makeRed = false)
        {
            // TODO [version 1.9] currently, this method must be called for each node in the node group individually
            //Log.Info($"just before the new flexible step");
            FlexibleTrafficLightsStep step = new FlexibleTrafficLightsStep(this, lightValues, segIDs, makeRed);
            //Log.Info($"just before the add");

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

            DebugOutputPanel.AddMessage(PluginManager.MessageType.Message, "Started FTL");

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

        //entry point into the light simulation called from base(kind of)
        public void SimulationStep() {
            
            if (!IsStarted())
            {
                return;
            }

            //DebugOutputPanel.AddMessage(PluginManager.MessageType.Message, "Current Step: " + CurrentStep.ToString());


            SetLights();

            //if (!Steps[CurrentStep].StepDone(true))
            //{
            //    Log.Info($"Current Step in !Steps.stepdone ");

            //    return;
            //}
            // step is done

            


            //this is all timed light logic that happens during a simulationstep (pretty sure this is all about figuring out when to go to the next step and what the next step is) 
            //the way they get vehicle flow info may be useful but all of this will have to change
            //they use minTime, minFlow, maxWait, calcwaitflow for this. 
            //What we need to do is replace all this with api calls that check conditions that determine when the next step occurs and what the next step is.
            //so essentially we are moving this logic(our equivalent of this logic) to api 
            //-jarrod

            TrafficLightSimulationManager tlsMan = TrafficLightSimulationManager.Instance;

            //Log.Info($"outside if <0");
            if (NodeGeometry.Get(NodeId).isMaster && API.APIget.isRecording)
            {
                //Log.Info($"Node id: {this.NodeId}, Master node: {NodeGroup[0]}");
                API.APIget.recordingTime++;
                API.APIget.journeyTimeUpdate();
                UIBase.updateRecordTime();
            }
            if (Steps[CurrentStep].NextStepRefIndex < 0)            {
                int nextStepIndex = 0;
                if (SelectedAlgorithm == 0)
                {
                     nextStepIndex = API.APIget.getNextIndex((CurrentStep) % NumSteps(), NumSteps(), NodeGeometry.Get(NodeId), Steps);
                }else if (SelectedAlgorithm == 1)
                {
                     nextStepIndex = API.APIget.getNextIndexOptimal((CurrentStep) % NumSteps(), NumSteps(), NodeGeometry.Get(NodeId), Steps);
                }else if (SelectedAlgorithm == 2)
                {
                    nextStepIndex = API.APIget.getNextIndexRR((CurrentStep) % NumSteps(), NumSteps(), NodeGeometry.Get(NodeId), Steps);
                }else if (SelectedAlgorithm == 3)
                {
                    nextStepIndex = API.APIget.getNextIndexMyATCS((CurrentStep) % NumSteps(), NumSteps(), NodeGeometry.Get(NodeId), Steps);
                }
                else
                {
                    nextStepIndex = API.APIget.getNextIndex((CurrentStep) % NumSteps(), NumSteps(), NodeGeometry.Get(NodeId), Steps);
                }
                
                API.APIget.incrementWait(NodeGeometry.Get(NodeId));

                
                //DebugOutputPanel.AddMessage(PluginManager.MessageType.Message, "Next Step Index: " + nextStepIndex.ToString());

                //TODO function that returns the next step index (current one or another index) 
                //  int nextStepIndex = APIfuncGetNExtStepIndex()
                if (nextStepIndex == CurrentStep)
                {
                    //DebugOutputPanel.AddMessage(PluginManager.MessageType.Message, "Restarting Current Step ");
                    // restart the current step                    
                    TrafficLightSimulation sim = tlsMan.GetNodeSimulation(NodeId);                 

                    sim.FlexibleLight.Steps[CurrentStep].Start(CurrentStep);
                    sim.FlexibleLight.Steps[CurrentStep].UpdateLiveLights();
                    
                    return;
                }
                else
                {
                    // set next step reference index for assuring a correct end transition              
                    
                    TrafficLightSimulation sim = tlsMan.GetNodeSimulation(NodeId);
                       
                    FlexibleTrafficLights flexibleLightsLocal = sim.FlexibleLight;
                    flexibleLightsLocal.Steps[CurrentStep].NextStepRefIndex = nextStepIndex;
                    
                }
            }

            //SetLights(); // check if this is needed

            
            //if (!Steps[CurrentStep].IsEndTransitionDone())
            //{
            //    Log.Info($"ending");
            //    return;
            //}
            // ending transition (yellow) finished
            // change step
            int newStepIndex = Steps[CurrentStep].NextStepRefIndex;
            int oldStepIndex = CurrentStep;

           
            TrafficLightSimulation slaveSim = tlsMan.GetNodeSimulation(NodeId);
              
            FlexibleTrafficLights flexibleLights = slaveSim.FlexibleLight;
            flexibleLights.CurrentStep = newStepIndex;

            flexibleLights.Steps[oldStepIndex].NextStepRefIndex = -1;
            flexibleLights.Steps[newStepIndex].Start(oldStepIndex);
            flexibleLights.Steps[newStepIndex].UpdateLiveLights();
            
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
                if (slaveSim == null || !slaveSim.IsFlexibleLight())
                {
                    //TrafficLightSimulation.RemoveNodeFromSimulation(slaveNodeId, false); // we iterate over NodeGroup!!
                    continue;
                }
                slaveSim.FlexibleLight.Steps[CurrentStep].UpdateLiveLights(noTransition);
            }
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
            if (masterSim == null || !masterSim.IsFlexibleLight())
                return null;
            return masterSim.FlexibleLight;
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
                //DebugOutputPanel.AddMessage(PluginManager.MessageType.Message, "Segment add or get?");
                SegmentEnd end = SegmentEndManager.Instance.GetSegmentEnd(endGeo.SegmentId, endGeo.StartNode);
                if (end != null)
                {
                    //DebugOutputPanel.AddMessage(PluginManager.MessageType.Message, "retrieved");

                }
                else
                {
                    //DebugOutputPanel.AddMessage(PluginManager.MessageType.Message, "didnt retrieved");
                }
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
