using CSUtil.Commons;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TrafficManager.API
{
    public class Phase
    {
        ushort[] segments;
        Directions[] directions;
        int numSegs;
        public Phase(int numSegs)
        {
            Log.Info($"number of segments in phase: {numSegs}");
            segments = new ushort[numSegs];
            directions = new Directions[numSegs];
            this.numSegs = numSegs;
        }
       
        public enum Directions
        {
            None = 0,
            Right = 1, 
            StraightRight = 2, 
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
            Log.Info($"ADDING SEGMENT AND DIRECTION TO PHASE: {seg} - {dir}");
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

        public ushort[] getRslArray(ushort[] segArray)
        {
            ushort[] rslArray = new ushort[numSegs * 3];

            for (int i = 0; i < numSegs; i++)
            {
                ushort seg = segments[i];
                Directions dir = directions[i];
                int k = 0;
                for (int j = 0; j < 4; j++)
                {
                    if(seg.Equals(segArray[j]))
                    {
                        switch (dir)
                        {
                            case Directions.None:
                                rslArray[k * 3] = 0;
                                rslArray[k * 3 + 1] = 0;
                                rslArray[k * 3 + 2] = 0;
                                break;
                            case Directions.Right:
                                rslArray[k * 3] = 1;
                                rslArray[k * 3 + 1] = 0;
                                rslArray[k * 3 + 2] = 0;
                                break;
                            case Directions.StraightRight:
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
                    }else
                    {
                        if (!(segArray[j].Equals(0) || Geometry.SegmentEndGeometry.Get(segArray[j], true).OutgoingOneWay))
                        {
                            k++;
                        }
                    }
                }
            }

            return rslArray;
        }
    }
}
