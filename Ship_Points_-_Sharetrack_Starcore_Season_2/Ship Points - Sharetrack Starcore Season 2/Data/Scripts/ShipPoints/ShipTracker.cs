using Draygo.API;
using ProtoBuf;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Text;
using VRage.Game.ModAPI;
using BlendTypeEnum = VRageRender.MyBillboard.BlendTypeEnum;
using VRage.ModAPI;
using VRageMath;
using VRage.Game;
using VRage.Utils;
using SpaceEngineers.Game.ModAPI;
using Sandbox.Common.ObjectBuilders.Definitions;

namespace klime.PointCheck
{

    [ProtoContract]
    public class ShipTracker
    {
        //Instance
        public IMyCubeGrid Grid { get; private set; }
        public IMyPlayer Owner { get; private set; }

        //passable
        [ProtoMember(1)] public string GridName;
        [ProtoMember(2)] public string FactionName;

        [ProtoMember(3)] public int LastUpdate;
        [ProtoMember(4)] public long GridID;
        [ProtoMember(5)] public long OwnerID;

        [ProtoMember(6)] public int BattlePoints;
        [ProtoMember(7)] public float InstalledThrust;
        [ProtoMember(8)] public float Mass;
        [ProtoMember(9)] public float Heavyblocks;
        [ProtoMember(10)] public int BlockCount;
        [ProtoMember(11)] public float TotalShieldStrength;
        [ProtoMember(12)] public float CurrentShieldStrength;
        [ProtoMember(13)] public int PCU;
        [ProtoMember(14)] public float DPS;

        [ProtoMember(16)] public Dictionary<string, int> GunList = new Dictionary<string, int>();
        public Dictionary<string, int> SpecialBlockList = new Dictionary<string, int>();

        [ProtoMember(17)] public string OwnerName;
        [ProtoMember(18)] public bool IsFunctional = false;

        [ProtoMember(19)] public float CurrentIntegrity;
        [ProtoMember(20)] public float OriginalIntegrity = -1;
        [ProtoMember(21)] public int ShieldHeat;
        [ProtoMember(22)] public Vector3 Position;
        [ProtoMember(23)] public Vector3 FactionColor = Vector3.One;



        public ShipTracker() { }

        public ShipTracker(IMyCubeGrid grid)
        {
            this.Grid = grid;
            this.GridID = grid.EntityId;

            grid.OnClose += OnClose;

            Update();
        }

        public void OnClose(IMyEntity e)
        {
            if (MyAPIGateway.Session.IsServer)
            {
                PointCheck.Sending.Remove(e.EntityId);
                PointCheck.Data.Remove(e.EntityId);
                DisposeHud();
            }
            e.OnClose -= OnClose;
        }

        private List<IMyCubeGrid> connectedGrids = new List<IMyCubeGrid>();
        private List<IMySlimBlock> tmpBlocks = new List<IMySlimBlock>();
        public void Update()
        {
            LastUpdate = 5;
            if (Grid != null && Grid.Physics != null)
            {

                Reset();
                connectedGrids.Clear();
                MyAPIGateway.GridGroups.GetGroup(Grid, GridLinkTypeEnum.Physical, connectedGrids);
                if (connectedGrids.Count > 0)
                {
                    Mass = (Grid as MyCubeGrid).GetCurrentMass();
                    bool hasPower = false, hasCockpit = false;

                    string controller = null;

                    foreach (var grid in connectedGrids)
                    {
                        if (grid != null && grid.Physics != null)
                        {
                            MyCubeGrid subgrid = grid as MyCubeGrid;

                            BlockCount += subgrid.BlocksCount;
                            PCU += subgrid.BlocksPCU;

                            tmpBlocks.Clear();
                            grid.GetBlocks(tmpBlocks);
                            foreach (var block in tmpBlocks)
                            {
								
																		if (block.FatBlock is IMyOxygenGenerator)
										{
											BattlePoints += 20; //flat Point cost for mass blocks
																		
										    string h2o2ID = "H2O2Generator";
                                            if (SpecialBlockList.ContainsKey(h2o2ID))
                                            {
                                                SpecialBlockList[h2o2ID] += 1;
                                            }
                                            else
                                            {
                                                SpecialBlockList.Add(h2o2ID, 1);
                                            }
										}

								//tank block workarounds
										if (block.FatBlock is IMyOxygenTank)
										{
											//BattlePoints += 20; //flat Point cost for mass blocks
																		
										    string tankID = "HydrogenTank";
                                            if (SpecialBlockList.ContainsKey(tankID))
                                            {
                                                SpecialBlockList[tankID] += 1;
                                            }
                                            else
                                            {
                                                SpecialBlockList.Add(tankID, 1);
                                            }
										}	
									    string subtype = block.BlockDefinition.Id.SubtypeName;
	
										if (block.FatBlock is IMyMotorStator && subtype == "SubgridBase")
										{
											//BattlePoints += 20; //flat Point cost for mass blocks
																		
										    string invID = "InvincibleSubgrid";
                                            if (SpecialBlockList.ContainsKey(invID))
                                            {
                                                SpecialBlockList[invID] += 1;
                                            }
                                            else
                                            {
                                                SpecialBlockList.Add(invID, 1);
                                            }
										}	
                                if (block.BlockDefinition != null && !string.IsNullOrEmpty(subtype))
                                {
                                    if (subtype.ToLower().Contains("heavy") && subtype.ToLower().Contains("armor"))
                                    {
                                        Heavyblocks += 1;
                                    }

                                    if (block.FatBlock != null)
                                    {
                                        if (!(block.FatBlock is IMyMotorRotor) && !(block.FatBlock is IMyMotorStator))
                                        {
                                            CurrentIntegrity += block.Integrity;
                                        }
                                    } 
                                    else
                                    {
                                        CurrentIntegrity += block.Integrity;
                                    }
                                }
                            }

                            //fatblocks
                            foreach(var block in subgrid.GetFatBlocks())
                            {
                                //points
                                string id = block?.BlockDefinition?.Id.SubtypeId.ToString();
                                if (!string.IsNullOrEmpty(id))
                                {
                                    if (PointCheck.PointValues.ContainsKey(id))
                                    {
                                        BattlePoints += PointCheck.PointValues[id];
                                    }
                                }
                                else
                                {
                                    if (block is IMyGravityGeneratorBase) //2015 blocks, no ID's
                                    {
                                        BattlePoints += PointCheck.PointValues.GetValueOrDefault("GravityGenerator", 0);
                                    }
                                    else if (block is IMySmallGatlingGun)
                                    {
                                        BattlePoints += PointCheck.PointValues.GetValueOrDefault("SmallGatlingGun", 0);
                                    }
                                    else if (block is IMyLargeGatlingTurret)
                                    {
                                        BattlePoints += PointCheck.PointValues.GetValueOrDefault("LargeGatlingTurret", 0);
                                    }
                                    else if (block is IMySmallMissileLauncher)
                                    {
                                        BattlePoints += PointCheck.PointValues.GetValueOrDefault("SmallMissileLauncher", 0);
                                    }
                                    else if (block is IMyLargeMissileTurret)
                                    {
                                        BattlePoints += PointCheck.PointValues.GetValueOrDefault("LargeMissileTurret", 0);
                                    }
                                }
                                
								

                                //block counts
                                if ((PointCheck.PointValues.ContainsKey(id) && !(block is IMyTerminalBlock)) ||
                                        block is IMyGyro || block is IMyReactor ||
                                        block is IMyBatteryBlock || block is IMyCockpit ||
                                        block is IMyDecoy || block is IMyShipDrill ||
                                        block is IMyGravityGeneratorBase || block is IMyShipWelder ||
                                        block is IMyShipGrinder || block is IMyThrust)
                                {
                                    string typeID = block.BlockDefinition.Id.TypeId.ToString().Replace("MyObjectBuilder_", "");
                                    if (SpecialBlockList.ContainsKey(typeID))
                                    {
                                        SpecialBlockList[typeID] += 1;
                                    }
                                    else
                                    {
                                        SpecialBlockList.Add(typeID, 1);
                                    }

                                    //thrust
                                    if (block is IMyThrust)
                                    {
                                        InstalledThrust += (block as IMyThrust).MaxEffectiveThrust;
                                    }

                                    if (block is IMyCockpit && (block as IMyCockpit).CanControlShip)
                                    {
                                        hasCockpit = true;
                                    }

                                    if (block is IMyReactor || block is IMyBatteryBlock)
                                    {
                                        hasPower = true;
                                    }
                                    if (block is IMyCockpit)
                                    {
                                        //controller = grid.DisplayName;
                                        var p = (block as IMyCockpit).ControllerInfo?.Controller?.ControlledEntity?.Entity;
                                        if (p is IMyCockpit)
                                        {
                                            controller = (p as IMyCockpit).Pilot.DisplayName;
                                        }
                                        //controller = (block.FatBlock as IMyCockpit).ControllerInfo?.Controller?.ControlledEntity?.Entity?.DisplayName;
                                    }
                                }


                                //guns
                                if (block is IMyTerminalBlock)
                                {
                                    IMyTerminalBlock tBlock = block as IMyTerminalBlock;
                                    if (PointCheck.WC_api.HasCoreWeapon(tBlock))
									{
										var tempName = tBlock.DefinitionDisplayNameText;
										var specialCost = 0f; //adds a fixed amount for every additional weapon after the first
                                        var multCost = 0f; //multiplies the cost by the amount of weapons after the first

                                        if (tempName == "Starcore LRM Launcher (45)")
                                        {
                                            tempName = "LRM Launcher";
                                            specialCost = 0f;
                                            multCost = 0.15f;
                                        }
                                        if (tempName == "LRM Launcher")
                                        {
                                            specialCost = 0f;
                                            multCost = 0.15f;
                                        }
                                        if (tempName == "Starcore Arrow-IV Launcher")
                                        {
                                            specialCost = 0f;
                                            multCost = 0.15f;
                                        }
                                        if (tempName == "SRM Streak Launcher")
                                        {
                                            specialCost = 0f;
                                            multCost = 0.15f;
                                        }
                                        if (tempName == "M-1 Torpedo")
                                        {
                                            specialCost = 0f;
                                            multCost = 0.15f;
                                        }
                                        if (tempName == "MA Derecho Missile Storm")
                                        {
                                            specialCost = 0f;
                                            multCost = 0.15f;
                                        }
                                        if (tempName == "Starcore MRM Launcher (45)")
											{
												tempName = "MRM Launcher";
												specialCost = 0f;
                                                multCost = 0.15f;
                                        }
											if (tempName == "MRM Launcher")
											{
												specialCost = 0f;
                                                multCost = 0.15f;
                                            }
											
										if (tempName == "SA Grimlock Launcher")
                                        {
                                            specialCost = 0f;
                                            multCost = 0.15f;
                                        }
										if (tempName == "SA  Grimlock-II Launcher")
                                        {
                                            specialCost = 0f;
                                            multCost = 0.15f;
                                        }
										
										if (GunList.ContainsKey(tempName))
										{
											GunList[tempName] += 1;
                                        }
										else
										{
											GunList.Add(tempName, 1);
										}
                                        if ((specialCost > 0 || multCost > 0) && GunList[tempName] > 1)
                                        {
                                            BattlePoints += (int)(PointCheck.PointValues[id] * (specialCost + ((GunList[tempName] - 1) * multCost))); //Point value of current block being evaluated

                                        }
                                    }
                                }

                            }
                        }
                    }

                    IMyCubeGrid mainGrid = connectedGrids[0];

                    //Owner name
                    FactionName = "None";
                    OwnerName = "Unowned";
                    if (mainGrid.BigOwners != null && mainGrid.BigOwners.Count > 0)
                    {
                        OwnerID = mainGrid.BigOwners[0];
                        Owner = PointCheck.GetOwner(OwnerID);
                        OwnerName = controller ?? (Owner?.DisplayName ?? "Unowned");

                        var f = MyAPIGateway.Session?.Factions?.TryGetPlayerFaction(OwnerID);
                        if (f != null)
                        {
                            FactionName = f.Tag ?? "None";
                            FactionColor = ColorMaskToRGB(f.CustomColor);
                        }
                    }

                    DPS = PointCheck.WC_api.GetConstructEffectiveDps(mainGrid);

                    GridName = Grid.DisplayName;

                    Position = Grid.Physics.CenterOfMassWorld;

                    IsFunctional = hasPower && hasCockpit;

                    //Shield calc for all grids
                    IMyTerminalBlock shield_block = null;
                    foreach(var g in connectedGrids)
                    {
                        if (shield_block == null)
                        {
                            shield_block = PointCheck.SH_api.GetShieldBlock(g);
                            break;
                        }
                    }
                    if (shield_block != null)
                    {
                        TotalShieldStrength = PointCheck.SH_api.GetMaxHpCap(shield_block);
                        CurrentShieldStrength = PointCheck.SH_api.GetShieldPercent(shield_block);
                        ShieldHeat = PointCheck.SH_api.GetShieldHeat(shield_block);
                    }

                    if (OriginalIntegrity == -1)
                    {
                        OriginalIntegrity = CurrentIntegrity;
                    }
                }

            }
        }

        private static Vector3 ColorMaskToRGB(Vector3 colorMask)
        {
            return MyColorPickerConstants.HSVOffsetToHSV(colorMask).HSVtoColor();
        }

        private HudAPIv2.HUDMessage nametag;
        public void CreateHud()
        {
            nametag = new HudAPIv2.HUDMessage(new StringBuilder(OwnerName), Vector2D.Zero, Blend: BlendTypeEnum.PostPP, HideHud: false, Shadowing: true);
            UpdateHud();
        }

        public void UpdateHud()
        {
            if (nametag != null)
            {
                var e = MyEntities.GetEntityById(GridID);

                Vector3D pos;
                if (e != null && e is IMyCubeGrid)
                {
                    var g = e as IMyCubeGrid;
                    pos = g.Physics.CenterOfMassWorld;
                }
                else
                {
                    pos = Position;
                }

                Vector3D targetHudPos = MyAPIGateway.Session.Camera.WorldToScreen(ref pos);
                Vector2D newOrigin = new Vector2D(targetHudPos.X, targetHudPos.Y);

                
                nametag.InitialColor = new Color(FactionColor);

                Vector3D cameraForward = MyAPIGateway.Session.Camera.WorldMatrix.Forward;
                Vector3D toTarget = pos - MyAPIGateway.Session.Camera.WorldMatrix.Translation;
                float fov = MyAPIGateway.Session.Camera.FieldOfViewAngle;
                var angle = GetAngleBetweenDegree(toTarget, cameraForward);

                bool visible = !(newOrigin.X > 1 || newOrigin.X < -1 || newOrigin.Y > 1 || newOrigin.Y < -1) && angle <= fov;

                var distance = Vector3D.Distance(MyAPIGateway.Session.Camera.WorldMatrix.Translation, pos);
                nametag.Scale = 1 - MathHelper.Clamp(distance / 20000, 0, 1) + (30 / Math.Max(60, angle * angle * angle));
				nametag.Origin = new Vector2D(targetHudPos.X, targetHudPos.Y+(MathHelper.Clamp(-0.000125*distance+0.25, 0.05, 0.25)));
                nametag.Visible = PointCheck.NameplateVisible && visible;
                
                nametag.Message.Clear();
				
				if (PointCheck.viewstat == 0 || PointCheck.viewstat == 2)
				{
				nametag.Message.Append(OwnerName);
                }
				if (PointCheck.viewstat == 1)
				{
				nametag.Message.Append(GridName);
                }
				if (PointCheck.viewstat == 2)
				{
				nametag.Message.Append("\n" + GridName);
                }
                nametag.Offset = -nametag.GetTextLength() / 2;

            }
        }

        private double GetAngleBetweenDegree(Vector3D vectorA, Vector3D vectorB)
        {
            vectorA.Normalize(); vectorB.Normalize();
            return Math.Acos(MathHelper.Clamp(vectorA.Dot(vectorB), -1, 1)) * (180.0 / Math.PI);
        }

        public void DisposeHud()
        {
            if (nametag != null)
            {
                nametag.Visible = false;
                nametag.Message.Clear();
                nametag.DeleteMessage();
            }
            nametag = null;
        }

        private void Reset()
        {
            SpecialBlockList.Clear();
            GunList.Clear();

            BattlePoints = 0;
            InstalledThrust = 0;
            Mass = 0;
            Heavyblocks = 0;
            BlockCount = 0;
            TotalShieldStrength = 0;
            CurrentShieldStrength = 0;
            CurrentIntegrity = 0;
            PCU = 0;
            DPS = 0;
        }

    }
}
