using CoreSystems.Api;
using Digi.Example_NetworkProtobuf;
using Draygo.API;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using VRage;
using VRage.Collections;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Utils;
using VRageMath;

namespace ThrustBeacon
{
    public partial class Session : MySessionComponentBase
    {
        internal static int Tick;
        internal bool Client;
        internal bool Server;
        internal bool MPActive;
        internal static HudAPIv2 hudAPI;
        internal static WcApi wcAPI;
        public Networking Networking = new Networking(1212); //TODO: Pick a new number based on mod ID
        internal MyStringId symbol = MyStringId.GetOrCompute("FrameSignal");
        internal MyStringId symbolOffscreenArrow = MyStringId.GetOrCompute("ArrowOffset");

        internal MyStringId symbolOffscreen = MyStringId.GetOrCompute("Arrow");
        internal List<MyStringId> symbolList = new List<MyStringId>(){MyStringId.GetOrCompute("IdleSignal"), MyStringId.GetOrCompute("SmallSignal"), MyStringId.GetOrCompute("MediumSignal"),
        MyStringId.GetOrCompute("LargeSignal"), MyStringId.GetOrCompute("HugeSignal"), MyStringId.GetOrCompute("MassiveSignal"), MyStringId.GetOrCompute("MassiveSignal")}; //TODO unique symbol for overheat/shutdown?
        internal List<string> messageList = new List<string>() {"Idle Sig", "Small Sig", "Medium Sig", "Large Sig", "Huge Sig", "Massive Sig", "OVERHEAT - SHUTDOWN"};
        internal static float symbolHeight = 0f;//Leave this as zero, monitor aspect ratio is figured in later
        internal float aspectRatio = 0f;//Leave this as zero, monitor aspect ratio is figured in later
        internal Vector2D offscreenSquish = new Vector2D(0.9, 0.7);//Pulls X in a little, flattens Y to not overlap hotbar
        internal int viewDist = 0;
        internal static float offscreenHeight = 0f;
        private readonly Stack<GridComp> _gridCompPool = new Stack<GridComp>(128);
        private readonly ConcurrentCachingList<MyCubeBlock> _startBlocks = new ConcurrentCachingList<MyCubeBlock>();
        private readonly ConcurrentCachingList<MyCubeGrid> _startGrids = new ConcurrentCachingList<MyCubeGrid>();
        internal readonly ConcurrentCachingList<GridComp> GridList = new ConcurrentCachingList<GridComp>();
        internal static readonly Dictionary<IMyCubeGrid,GridComp> GridListSpecials = new Dictionary<IMyCubeGrid, GridComp>();
        internal static readonly List<MyStringHash> weaponSubtypeIDs = new List<MyStringHash>();
        internal readonly ConcurrentDictionary<IMyCubeGrid, GridComp> GridMap = new ConcurrentDictionary<IMyCubeGrid, GridComp>();
        internal static readonly Dictionary<string, int> SignalProducer = new Dictionary<string, int>();
        internal static readonly Dictionary<MyStringHash, BlockConfig> BlockConfigs = new Dictionary<MyStringHash, BlockConfig>();
        internal List<IMyPlayer> PlayerList = new List<IMyPlayer>();
        internal static ConcurrentDictionary<long, MyTuple<SignalComp, int>> SignalList = new ConcurrentDictionary<long, MyTuple<SignalComp, int>>();
        internal static ConcurrentDictionary<long, MyTuple<SignalComp, int>> NewSignalList = new ConcurrentDictionary<long, MyTuple<SignalComp, int>>();
        internal ICollection<MyTuple<MyEntity, float>> threatList = new List<MyTuple<MyEntity, float>>();
        internal ICollection<MyEntity> obsList = new List<MyEntity>();
        internal List<long> entityIDList = new List<long>();
        internal static List<GridComp> thrustshutdownList = new List<GridComp>();
        internal static List<GridComp> powershutdownList = new List<GridComp>();
        internal static int fadeTimeTicks = 0;
        internal static int stopDisplayTimeTicks = 0;
        internal static int keepTimeTicks = 0;
        internal bool clientActionRegistered = false;
        Random rand = new Random();
        internal string ModName = "[Thrust Beacon]"; //Since I may change the name, this is used in logging

        internal void StartComps()
        {
            try
            {
                _startGrids.ApplyAdditions();
                for (int i = 0; i < _startGrids.Count; i++)
                {
                    var grid = _startGrids[i];

                    if (grid.IsPreview)
                        continue;

                    var gridComp = _gridCompPool.Count > 0 ? _gridCompPool.Pop() : new GridComp();
                    gridComp.Init(grid);

                    GridList.Add(gridComp);
                    GridList.ApplyAdditions();
                    GridMap[grid] = gridComp;
                    grid.OnClose += OnGridClose;
                }
                _startGrids.ClearImmediate();

                _startBlocks.ApplyAdditions();
                for (int i = 0; i < _startBlocks.Count; i++)
                {
                    var block = _startBlocks[i];

                    if (block?.CubeGrid?.Physics == null || block.CubeGrid.IsPreview)
                        continue;

                    GridComp gridComp;
                    if (!GridMap.TryGetValue(block.CubeGrid, out gridComp))
                        continue;

                    var thruster = block as IMyThrust;
                    if (thruster != null)
                    {
                        gridComp.FatBlockAdded(block);
                        continue;
                    }
                }
                _startBlocks.ClearImmediate();
            }
            catch
            { }
        }
        private void Clean()
        {
            _gridCompPool.Clear();
            _startBlocks.ClearImmediate();
            _startGrids.ClearImmediate();
            GridList.ClearImmediate();
            GridMap.Clear();
            BlockConfigs.Clear();
            SignalProducer.Clear();
            weaponSubtypeIDs.Clear();
            GridListSpecials.Clear();
        }
        private void OnEntityCreate(MyEntity entity)
        {
            var grid = entity as MyCubeGrid;
            if (grid != null)
            {
                grid.AddedToScene += addToStart => _startGrids.Add(grid);
            }

            var thruster = entity as MyThrust;
            if (thruster != null)
            {
                entity.AddedToScene += addToStart => _startBlocks.Add(thruster);
            }
        }

        private void OnGridClose(IMyEntity entity)
        {
            var grid = entity as MyCubeGrid;

            GridComp comp;
            if (GridMap.TryRemove(grid, out comp))
            {
                GridList.Remove(comp);
                GridList.ApplyRemovals();
                comp.Clean();
                _gridCompPool.Push(comp);
            }
        }

    }
}
