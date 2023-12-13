using CoreSystems.Api;
using DefenseShields;
using Digi.Example_NetworkProtobuf;
using Draygo.API;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using VRage;
using VRage.Collections;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Groups;
using VRage.ModAPI;
using VRage.Utils;
using VRageMath;

namespace ThrustBeacon
{
    public partial class Session : MySessionComponentBase
    {
        private void GridGroupsOnOnGridGroupCreated(IMyGridGroupData group)
        {
            if (group.LinkType != GridLinkTypeEnum.Mechanical)
                return;
            GroupComp gComp = new GroupComp();
            gComp.iMyGroup = group;
            GroupDict.Add(group, gComp);
            gComp.InitGrids();

            group.OnGridAdded += gComp.OnGridAdded;
            group.OnGridRemoved += gComp.OnGridRemoved;
        }
        private void GridGroupsOnOnGridGroupDestroyed(IMyGridGroupData group)
        {
            if (group.LinkType != GridLinkTypeEnum.Mechanical)
                return;
            GroupComp gComp;
            if(GroupDict.TryGetValue(group, out gComp))
            {
                group.OnGridAdded -= gComp.OnGridAdded;
                group.OnGridRemoved -= gComp.OnGridRemoved;
                gComp.Clean();
                GroupDict.Remove(group);
            }
        }
    }
}
