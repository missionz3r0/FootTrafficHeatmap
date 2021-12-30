﻿//using System;
//using System.Collections.Generic;
//using System.Diagnostics;
//using RimWorld;
//using UnityEngine;
//using Verse;

//namespace TrafficHeatmap
//{
//    public class TrafficHeatmap : MapComponent, ICellBoolGiver
//    {
//        public static bool ShowHeatMap, ShowHeatMapCost;
//        readonly Dictionary<Pawn, float[]> pawnToCostMap = new Dictionary<Pawn, float[]>();
//        float[] filteredTotalCostMap;
//        readonly Predicate<Pawn> shouldShowFor = (p) => true;
//        readonly CellBoolDrawer cellBoolDrawer;
//        Gradient gradient;
//        float maxCost = 0.01f;
//        float precision;
//        int windowSizeTicks;
//        int lastUpdatedAt;
//        int numGridCells;
//        Stopwatch sw = new Stopwatch();
//        double cellBoolDrawerUpdateAvgTicks, updateAvgTicks;
//        int cellBoolDrawerUpdateCount, updateCount;

//        public TrafficHeatmap(Map map) : base(map)
//        {
//            this.cellBoolDrawer = new CellBoolDrawer(this, map.Size.x, map.Size.z);
//            this.gradient = this.GetGradient();
//            this.windowSizeTicks = GenDate.TicksPerDay;
//            this.precision = 1f / this.windowSizeTicks;
//            this.numGridCells = this.map.cellIndices.NumGridCells;
//            this.filteredTotalCostMap = new float[this.numGridCells];
//            for (int i = 0; i < this.numGridCells; i++)
//            {
//                this.filteredTotalCostMap[i] = 0;
//            }
//        }

//        public Color Color => Color.white;

//        public bool GetCellBool(int index)
//        {
//            return this.filteredTotalCostMap[index] > this.precision;
//        }

//        public Color GetCellExtraColor(int index)
//        {
//            return this.GetColorForCost(this.filteredTotalCostMap[index]);
//        }

//        public override void MapComponentUpdate()
//        {
//            base.MapComponentUpdate();
//            if (ShowHeatMap)
//            {
//                this.cellBoolDrawer.MarkForDraw();
//                this.sw.Restart();
//                this.cellBoolDrawer.CellBoolDrawerUpdate();
//                this.sw.Stop();
//                var ticks = this.sw.ElapsedTicks;
//                double coefficient = (double)1 / (++this.cellBoolDrawerUpdateCount);
//                this.cellBoolDrawerUpdateAvgTicks = this.cellBoolDrawerUpdateAvgTicks * (1 - coefficient) + coefficient * ticks;
//            }
//        }

//        public override void MapComponentOnGUI()
//        {
//            base.MapComponentOnGUI();
//            if (ShowHeatMapCost)
//            {
//                for (int i = 0; i < this.numGridCells; i++)
//                {
//                    float totalFilteredCost = this.filteredTotalCostMap[i];
//                    if (totalFilteredCost > this.precision)
//                    {// TODO: center text when camera is close, see DebugDrawerOnGUI()
//                        var cell = this.map.cellIndices.IndexToCell(i);
//                        var drawTopLeft = GenMapUI.LabelDrawPosFor(cell);
//                        var labelRect = new Rect(drawTopLeft.x - 20f, drawTopLeft.y - 20f, 40f, 20f);
//                        Widgets.Label(labelRect, (totalFilteredCost / this.maxCost).ToString());
//                    }
//                }
//            }

//            Widgets.Label(new Rect(10, Screen.height * 1 / 3f, 300, 300),
//                       $"{this.GetType().Name} CellBoolDrawerUpdate avg ticks: {this.cellBoolDrawerUpdateAvgTicks:N0}\n" +
//                       $"{this.GetType().Name} Update avg ticks: {this.updateAvgTicks:N0}\n");
//        }

//        private Color GetColorForCost(float cost)
//        {
//            return this.gradient.Evaluate(Math.Min(cost / this.maxCost, 1f));
//        }

//        //private IEnumerable<KeyValuePair<Pawn, float[]>> GetFilteredCostMaps()
//        //{
//        //    return this.pawnToCostMap.Where(kv => this.shouldShowFor(kv.Key));
//        //}

//        //private float GetFilteredTotalCost(int index)
//        //{
//        //    return this.GetFilteredCostMaps().Sum(kv => kv.Value[index]);
//        //}

//        Gradient GetGradient()
//        {
//            var gradient = new Gradient();

//            var colorKey = new GradientColorKey[3];
//            colorKey[0].color = Color.blue;
//            colorKey[0].time = 0f;
//            colorKey[1].color = Color.yellow;
//            colorKey[1].time = 0.5f;
//            colorKey[2].color = Color.red;
//            colorKey[2].time = 1f;

//            var alphaKey = new GradientAlphaKey[2];
//            alphaKey[0].alpha = 1f;
//            alphaKey[0].time = 0f;
//            alphaKey[1].alpha = 1f;
//            alphaKey[1].time = 1f;

//            gradient.SetKeys(colorKey, alphaKey);
//            return gradient;
//        }

//        internal void Update(Pawn pawn, float cost)
//        {
//            this.sw.Restart();
//            int index = this.map.cellIndices.CellToIndex(pawn.Position);
//            int curTick = Find.TickManager.TicksGame;
//            float coefficient = 1f / this.windowSizeTicks;
//            if (this.lastUpdatedAt != curTick)
//            {
//                foreach (var value in this.pawnToCostMap.Values)
//                {
//                    for (int i = 0; i < value.Length; i++)
//                    {
//                        if (value[i] > this.precision)
//                        {
//                            value[i] *= (float)Math.Pow(1 - coefficient, curTick - this.lastUpdatedAt);
//                        }
//                    }
//                }
//                for (int i = 0; i < this.filteredTotalCostMap.Length; i++)
//                {
//                    if (this.filteredTotalCostMap[i] > this.precision)
//                    {
//                        this.filteredTotalCostMap[i] *= (float)Math.Pow(1 - coefficient, curTick - this.lastUpdatedAt);
//                    }
//                }
//            }
//            this.lastUpdatedAt = curTick;
//            float[] mapForCurPawn;
//            if (!this.pawnToCostMap.TryGetValue(pawn, out mapForCurPawn))
//            {
//                mapForCurPawn = new float[this.numGridCells];
//                for (int i = 0; i < this.numGridCells; i++)
//                {
//                    mapForCurPawn[i] = 0;
//                }
//                this.pawnToCostMap.Add(pawn, mapForCurPawn);
//            }
//            mapForCurPawn[index] += cost * coefficient;

//            if (this.shouldShowFor(pawn))
//            {
//                this.filteredTotalCostMap[index] += cost * coefficient;
//            }

//            this.sw.Stop();
//            var ticks = this.sw.ElapsedTicks;
//            double coefficient1 = (double)1 / (++this.updateCount);
//            this.updateAvgTicks = this.updateAvgTicks * (1 - coefficient1) + coefficient1 * ticks;

//            this.cellBoolDrawer.SetDirty();
//        }
//    }
//}
