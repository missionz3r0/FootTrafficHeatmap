﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace TrafficHeatmap
{
    public class FootTrafficHeatmap : MapComponent, ICellBoolGiver, ISettingsObserver
    {
        public static bool ShowHeatMap, ShowHeatMapCost;
        private CellBoolDrawer cellBoolDrawer;
        private CellCostGrid cellCostGridToDisplay;
        private float coefficient;
        private bool disposedValue;
        private Gradient gradient;
        private int lastGlobalDecayTick;
        private CellCostGrid multiPawnsCellCostGrid;
        private HashSet<Pawn> multiPawnsToDisplayFor;
        private int numGridCells;
        private Dictionary<Pawn, CellCostGrid> pawnToCellCostGridMap;
        private int sampleInterval;
        private float threshold;
        private List<CellCostGrid> tmpCellCostGrids;
        private List<Pawn> tmpPawns;

        public FootTrafficHeatmap(Map map) : base(map)
        {
            Log.Message($"TrafficHeatmap ctor {this.GetHashCode()}");
            // Only init fields that are needed before ExposeData here
        }

        public Color Color => Color.white;

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            this.Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Log.Message($"TrafficHeatmap ExposeData {this.GetHashCode()}, Scribe mode = {Scribe.mode}");
            Scribe_Collections.Look(ref this.pawnToCellCostGridMap, "pawnToCellCostGridMap", LookMode.Reference, LookMode.Deep, ref this.tmpPawns, ref this.tmpCellCostGrids);
        }

        public override void FinalizeInit()
        {
            // Prefer initing fields here, except the ones that are needed before ExposeData
            base.FinalizeInit();
            Log.Message($"TrafficHeatmap FinalizeInit {this.GetHashCode()}");
            this.cellBoolDrawer = new CellBoolDrawer(this, this.map.Size.x, this.map.Size.z);
            var mod = LoadedModManager.GetMod<FootTrafficHeatmapMod>();
            mod.Subscribe(this);
            this.UpdateFromSettings(mod.GetSettings<TrafficHeatmapModSettings>());
            this.gradient = this.GetGradient();
            this.numGridCells = this.map.cellIndices.NumGridCells;
            if (this.pawnToCellCostGridMap == null)
            {
                this.pawnToCellCostGridMap = new Dictionary<Pawn, CellCostGrid>();
            }
            if (this.multiPawnsCellCostGrid == null)
            {
                this.multiPawnsCellCostGrid = new CellCostGrid(this.map);
            }
            if (this.multiPawnsToDisplayFor == null)
            {
                this.multiPawnsToDisplayFor = new HashSet<Pawn>();
            }
            this.cellCostGridToDisplay = this.multiPawnsCellCostGrid;
        }

        public bool GetCellBool(int index)
        {
            return this.cellCostGridToDisplay.GetRawCost(index) > this.threshold;
        }

        public Color GetCellExtraColor(int index)
        {
            return this.GetColorForNormalizedCost(this.cellCostGridToDisplay.GetNormalizedCost(index));
        }

        public override void MapComponentTick()
        {
            base.MapComponentTick();
            int curTick = Find.TickManager.TicksGame;
            if (curTick >= this.lastGlobalDecayTick + this.sampleInterval)
            {
                this.RemoveInvalidPawns();
                this.GlobalDecay();
                this.lastGlobalDecayTick = curTick;
            }
        }

        public override void MapComponentUpdate()
        {
            base.MapComponentUpdate();

            if (ShowHeatMap)
            {
                this.SetCellCostGridToDisplay();
                this.cellBoolDrawer.MarkForDraw();
                this.cellBoolDrawer.CellBoolDrawerUpdate();
            }
        }

        public override void MapRemoved()
        {
            base.MapRemoved();
            this.Dispose();
        }

        public void OnSettingsChanged(TrafficHeatmapModSettings settings)
        {
            this.UpdateFromSettings(settings);
        }

        public void Update(Pawn pawn, float cost)
        {
            int index = this.map.cellIndices.CellToIndex(pawn.Position);
            if (!this.pawnToCellCostGridMap.TryGetValue(pawn, out CellCostGrid gridForCurPawn))
            {
                gridForCurPawn = new CellCostGrid(this.map);
                this.pawnToCellCostGridMap.Add(pawn, gridForCurPawn);
            }
            float costToAdd = cost / this.sampleInterval * this.coefficient;
            gridForCurPawn.AddRawCost(index, costToAdd);
            if (this.multiPawnsToDisplayFor.Contains(pawn))
            {
                this.multiPawnsCellCostGrid.AddRawCost(index, costToAdd);
            }

            this.cellBoolDrawer.SetDirty();
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposedValue)
            {
                if (disposing)
                {
                    LoadedModManager.GetMod<FootTrafficHeatmapMod>().Unsubscribe(this);
                    foreach (var grid in this.pawnToCellCostGridMap.Values)
                    {
                        grid.Dispose();
                    }
                    this.pawnToCellCostGridMap.Clear();
                    this.multiPawnsCellCostGrid.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                this.disposedValue = true;
            }
        }

#if DEBUG
        public override void MapComponentOnGUI()
        {
            base.MapComponentOnGUI();
            IntVec3 pos = UI.MouseCell();
            int index = map.cellIndices.CellToIndex(pos);
            TerrainDef terrain = pos.GetTerrain(map);
            string gridToDisplay = cellCostGridToDisplay == multiPawnsCellCostGrid ? "Multi" : "SelectedPawn";
            Widgets.Label(new Rect(10, Screen.height * 1 / 3f, 300, 300),
                           $"GridToDisplay: {gridToDisplay}\n" +
                           $"Normalizer: {cellCostGridToDisplay.Normalizer.GetDebugString()}\n");
            if (ShowHeatMapCost)
            {
                for (int i = 0; i < this.numGridCells; i++)
                {
                    if (this.GetCellBool(i))
                    {
                        float cost = this.cellCostGridToDisplay.GetNormalizedCost(i);
                        // TODO: center text when camera is close, see DebugDrawerOnGUI()
                        var cell = this.map.cellIndices.IndexToCell(i);
                        var drawTopLeft = GenMapUI.LabelDrawPosFor(cell);
                        var labelRect = new Rect(drawTopLeft.x - 20f, drawTopLeft.y - 20f, 40f, 20f);
                        Widgets.Label(labelRect, cost.ToString());
                        var labelRectBot = new Rect(drawTopLeft.x - 20f, drawTopLeft.y, 40f, 20f);
                        Widgets.Label(labelRectBot, cellCostGridToDisplay.GetRawCost(i).ToString());
                    }
                }
            }
        }
#endif

        private Color GetColorForNormalizedCost(float cost)
        {
            return this.gradient.Evaluate(cost);
        }

        private Gradient GetGradient()
        {
            Gradient localGradient = new Gradient();

            var colorKey = new GradientColorKey[4];
            colorKey[0].color = Color.blue;
            colorKey[0].time = 0f;
            colorKey[1].color = Color.green;
            colorKey[1].time = 1f / 3f;
            colorKey[2].color = Color.yellow;
            colorKey[2].time = 2f / 3f;
            colorKey[3].color = Color.red;
            colorKey[3].time = 1f;

            var alphaKey = new GradientAlphaKey[2];
            alphaKey[0].alpha = 1f;
            alphaKey[0].time = 0f;
            alphaKey[1].alpha = 1f;
            alphaKey[1].time = 1f;

            localGradient.SetKeys(colorKey, alphaKey);
            return localGradient;
        }

        private void GlobalDecay()
        {
            float decayCoefficient = 1 - this.coefficient;
            IEnumerable<CellCostGrid> grids = this.pawnToCellCostGridMap.Values.Concat(this.multiPawnsCellCostGrid);
            Parallel.ForEach(grids, grid =>
            {
                grid.Decay(decayCoefficient);
            });
            this.cellBoolDrawer.SetDirty();
        }

        private void RemoveInvalidPawns()
        {
            IEnumerable<Pawn> toRemove = this.pawnToCellCostGridMap.Keys.Where(pawn => !pawn.IsColonist || pawn.Dead);

            if (toRemove.Any())
            {
                foreach (var pawn in toRemove)
                {
                    if (this.pawnToCellCostGridMap.TryGetValue(pawn, out var grid))
                    {
                        grid.Dispose();
                        this.pawnToCellCostGridMap.Remove(pawn);
                    }
                    this.multiPawnsToDisplayFor.Remove(pawn);
                }
                this.cellBoolDrawer.SetDirty();
            }
        }

        private void SetCellCostGridToDisplay()
        {
            List<Pawn> selectedPawns = Find.Selector.SelectedPawns;
            var previousCostGrid = this.cellCostGridToDisplay;
            if (selectedPawns.Count == 0 || !this.pawnToCellCostGridMap.ContainsKey(selectedPawns[0]))
            {
                this.UpdatePawnMultiSelection(this.pawnToCellCostGridMap.Keys);
                this.cellCostGridToDisplay = this.multiPawnsCellCostGrid;
            }
            else if (selectedPawns.Count == 1)
            {
                this.cellCostGridToDisplay = this.pawnToCellCostGridMap[selectedPawns[0]];
            }
            else
            {
                this.UpdatePawnMultiSelection(selectedPawns.Intersect(this.pawnToCellCostGridMap.Keys));
                this.cellCostGridToDisplay = this.multiPawnsCellCostGrid;
            }
            if (previousCostGrid != this.cellCostGridToDisplay)
            {
                this.cellBoolDrawer.SetDirty();
            }
        }

        private void UpdateFromSettings(TrafficHeatmapModSettings settings)
        {
            // Use exponential moving average to calculate average cost over the moving window, see https://en.wikipedia.org/wiki/Moving_average#Application_to_measuring_computer_performance
            this.coefficient = settings.coefficient;
            this.threshold = settings.minThreshold;
            this.sampleInterval = settings.sampleInterval;
            this.cellBoolDrawer.SetDirty();
        }

        private void UpdatePawnMultiSelection(IEnumerable<Pawn> selected)
        {
            if (!this.multiPawnsToDisplayFor.SetEquals(selected))
            {
                this.multiPawnsToDisplayFor = selected.ToHashSet();
                this.multiPawnsCellCostGrid.Clear();
                foreach (Pawn pawn in selected)
                {
                    for (int i = 0; i < this.numGridCells; i++)
                    {
                        this.multiPawnsCellCostGrid.AddRawCost(i, this.pawnToCellCostGridMap[pawn].GetRawCost(i));
                    }
                }
                this.cellBoolDrawer.SetDirty();
            }
        }
    }
}