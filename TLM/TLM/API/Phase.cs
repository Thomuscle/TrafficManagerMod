using CSUtil.Commons;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.Geometry;

namespace TrafficManager.API
{

    //This class represents a phase and is used when building a phase with non-conflicting movements. 
    //It contains a list of segments and directions cars will move from those segments.
    public class Phase
    {
        ushort[] segments;
        Directions[] directions;
        int numSegs;
        public Phase(int numSegs)
        {
            //Log.Info($"number of segments in phase: {numSegs}");
            segments = new ushort[numSegs];
            directions = new Directions[numSegs];
            this.numSegs = numSegs;
        }
       
        public enum Directions
        {
            None = 0,
            InsideTurn = 1, 
            StraightInside = 2, 
            All = 3
        }

        public void getDirAndSeg(int index, out Directions dir, out ushort seg)
        {
            dir = directions[index];
            seg = segments[index];
        }

        public void addDirAndSeg(int index, ushort seg, Directions dir )
        {
            segments[index] = seg;
            directions[index] = dir;
        }

        public int compare(Phase phase, ushort[] segArray, NodeGeometry node)
        {
            ushort[] rslArray1 = getRslArray(segArray, node);
            ushort[] rslArray2 = phase.getRslArray(segArray, node);

            bool rsl1notSuper = false;
            bool rsl2notSuper = false;

            for (int i = 0; i<rslArray1.Length; i++)
            {
                if (rslArray1[i].Equals(rslArray2[i]))
                {
                    continue;

                } else if (rslArray1[i].Equals(0))
                {
                    rsl1notSuper = true;
                } else if (rslArray2[i].Equals(0))
                {
                    rsl2notSuper = true;
                }
            }

            if(rsl1notSuper && !rsl2notSuper)
            {
                return -1;
            }else if (!rsl1notSuper)
            {
                return 1;
            }else
            {
                return 1;
            }
        }

        public void copy(Phase phase, int numSegs)
        {
            for(int i = 0; i < numSegs; i++)
            {
                ushort seg;
                Directions dir;
                phase.getDirAndSeg(i, out dir, out seg);
                addDirAndSeg(i, seg, dir);
            }
        }

        public ushort[] getRslArray(ushort[] segArray, NodeGeometry node)
        {
            
            ushort[] rslArray = new ushort[numSegs * 3];
            
            for (int i = 0; i < numSegs; i++)
            {
                ushort seg = segments[i];
                //Log.Info($"segment: {seg}");

                Directions dir = directions[i];
                int k = 0;
                for (int j = 0; j < 4; j++)
                {
                    if(seg.Equals(segArray[j]))
                    {
                        //Log.Info($"K = {k} for {seg}");

                        bool leftHandDrive = Constants.ServiceFactory.SimulationService.LeftHandDrive;

                        if (leftHandDrive)
                        {
                            switch (dir)
                            {
                                case Directions.None:
                                    rslArray[k * 3] = 0;
                                    rslArray[k * 3 + 1] = 0;
                                    rslArray[k * 3 + 2] = 0;
                                    break;
                                case Directions.InsideTurn:  //Technically Left in this case
                                    rslArray[k * 3] = 0;
                                    rslArray[k * 3 + 1] = 0;
                                    rslArray[k * 3 + 2] = 1;
                                    break;
                                case Directions.StraightInside: //Technically StraightLeft in this case
                                    rslArray[k * 3] = 0;
                                    rslArray[k * 3 + 1] = 1;
                                    rslArray[k * 3 + 2] = 1;
                                    break;
                                case Directions.All: 
                                    rslArray[k * 3] = 1;
                                    rslArray[k * 3 + 1] = 1;
                                    rslArray[k * 3 + 2] = 1;
                                    break;
                            }
                        }
                        else
                        {
                            switch (dir)
                            {
                                case Directions.None:
                                    rslArray[k * 3] = 0;
                                    rslArray[k * 3 + 1] = 0;
                                    rslArray[k * 3 + 2] = 0;
                                    break;
                                case Directions.InsideTurn: //Technically Right in this case
                                    rslArray[k * 3] = 1;
                                    rslArray[k * 3 + 1] = 0;
                                    rslArray[k * 3 + 2] = 0;
                                    break;
                                case Directions.StraightInside: //Technically StraightRight in this case
                                    rslArray[k * 3] = 1;
                                    rslArray[k * 3 + 1] = 1;
                                    rslArray[k * 3 + 2] = 0;
                                    break;
                                case Directions.All:
                                    rslArray[k * 3] = 1;
                                    rslArray[k * 3 + 1] = 1;
                                    rslArray[k * 3 + 2] = 1;
                                    break;
                            }
                        }
                       
                    }else
                    {
                        if (!(segArray[j].Equals(0)))
                        {

                            if (Geometry.SegmentEndGeometry.Get(segArray[j], true).NodeId().Equals(node.NodeId))
                            {
                                if (!Geometry.SegmentEndGeometry.Get(segArray[j], true).OutgoingOneWay)
                                {
                                    k++;
                                }
                            }
                            else
                            {
                                if (!Geometry.SegmentEndGeometry.Get(segArray[j], false).OutgoingOneWay)
                                {
                                    k++;
                                }
                            }
                        }
                        
                        

                        
                    }
                }
            }

            return rslArray;
        }
    }
}
