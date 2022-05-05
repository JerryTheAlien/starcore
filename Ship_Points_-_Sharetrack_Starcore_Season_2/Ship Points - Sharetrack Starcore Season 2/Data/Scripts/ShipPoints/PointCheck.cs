using System;
using System.Collections.Generic;
using System.IO;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.Components;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.VisualScripting;
using VRage.Game.Entity;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRageMath;
using Draygo.API;
using WeaponCore.Api;
using System.Text;
using BlendTypeEnum = VRageRender.MyBillboard.BlendTypeEnum;
using VRage.Input;
using DefenseShields;
using Math0424.Networking;
using static Math0424.Networking.MyNetworkHandler;
using Math0424.ShipPoints;
using VRage.Utils;
using RelativeTopSpeed;
using Sandbox.Definitions;
using ShipPoints.Data.Scripts.ShipPoints.Networking;

namespace klime.PointCheck
{
    [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
    public class PointCheck : MySessionComponentBase
    {
        T CastProhibit<T>(T ptr, object val) => (T)val;
        public static Dictionary<string, int> PointValues = new Dictionary<string, int>();

        public static WcApi WC_api { get; private set; }
        public static ShieldApi SH_api { get; private set; }
        public static RtsApi RTS_api { get; private set; }
        public static Vector3 ctrpoint = new Vector3(0, 0, 0);
        public static double capdist = 2500;
        public static Dictionary<long, List<ulong>> Sending = new Dictionary<long, List<ulong>>();
        public static Dictionary<long, ShipTracker> Data = new Dictionary<long, ShipTracker>();
        public static List<long> Tracking = new List<long>();
		public static string capstat = "";
		public static string ZoneControl = "";
        private static Dictionary<long, IMyPlayer> all_players = new Dictionary<long, IMyPlayer>();
        private static List<IMyPlayer> listPlayers = new List<IMyPlayer>();
		public static int  captimer = 0;
        public static int timer = 0;
        public static string zonemsg = "";
        public static string temp_time_msg = "";
        public static string old_time_msg = "0";
        public static string team1 = "";
        public static string team2 = "";
        public enum ViewState { None, InView, GridSwitch, ExitView };
        ViewState vState = ViewState.None;
        HudAPIv2 text_api;
        HudAPIv2.HUDMessage statMessage, integretyMessage, timerMessage;
        public static bool NameplateVisible = true;
		public static bool broadcaststat = false;
		public static String[] viewmode = new string[] {"Player", "Grid", "Grid & Player", "False"}; 
		public static int viewstat = 0;
		public static int wintime = 120;
		public static int decaytime = 180;
		public static int delaytime = 18000;
		public static int matchtime = 72000;
		
		//bubble visual
        private const string SphereModel = "\\Models\\Cubes\\InnerShield.mwm";		
		private MyEntity _sphereEntity;
        
		
		internal MyEntity GetSphereEntity()
        {
			if (MyEntities.EntityNameExists("controlzone")) MyEntities.Remove(MyEntities.GetEntityByName("controlzone"));
            var ent = new MyEntity();
            var model = $"{ModContext.ModPath}{SphereModel}";
			ent.Name = "controlzone";
            ent.Init(null, model, null, null);
            ent.Render.CastShadows = false;
            ent.IsPreview = true;
            ent.Save = false;
            ent.SyncFlag = false;
            ent.NeedsWorldMatrix = false;
            ent.Flags |= EntityFlags.IsNotGamePrunningStructureObject;
            MyEntities.Add(ent);

            var matrix = MatrixD.CreateScale(capdist);
            ent.PositionComp.SetWorldMatrix(ref matrix, null, false, false, false);
            ent.InScene = true;
            ent.Render.UpdateRenderObject(true, false);
            return ent;
        }
		
		
		
		
		
		
		
		
		//end visual
		

        public override void Init(MyObjectBuilder_SessionComponent sessionComponent)
        {
            MyAPIGateway.Utilities.MessageEntered += MessageEntered;
            MyNetworkHandler.Init();
            MyAPIGateway.Utilities.ShowMessage("ShipPoints v2.22 - Control Zone", "Aim at a grid and press Shift+T to show stats, Shift+M to track a grid, Shift+J to cycle nametag style");
			team1="RED";
			team2="BLU";
		
		}

        private void MessageEntered(string messageText, ref bool sendToOthers)
        {
            if (messageText.Contains("/setgps"))
            {

                try
                {
                    ///test GPS:BDCarrillo #1:37004.29:-28096.08:-3884.33:#FF75C9F1:
                    string[] tempgps = messageText.Split(':');

                    ctrpoint = new Vector3(float.Parse(tempgps[2]), float.Parse(tempgps[3]), float.Parse(tempgps[4]));
                    MyAPIGateway.Utilities.ShowNotification("Centerpoint changed to " + tempgps[2]);
                    sendToOthers = true;
					GetSphereEntity();
                }
                catch (Exception)
                {
                    MyAPIGateway.Utilities.ShowNotification("Centerpoint not changed, try pasting a gps coordinate after /setgps");
                }
            }
			            if (messageText.ToLower() == "/shields")
            {

                Static.MyNetwork.TransmitToServer(new BasicPacket(5));
                sendToOthers = false;
            }

 if (messageText.Contains("/setmatchtime"))
            {

                try
                {
                    string[] tempdist = messageText.Split(' ');
  					
                    MyAPIGateway.Utilities.ShowNotification("Match duration changed to " + tempdist[1].ToString() + " minutes.");
					matchtime = int.Parse(tempdist[1]) *60 *60;
                    sendToOthers = true;
                }
                catch (Exception)
                {
                    MyAPIGateway.Utilities.ShowNotification("Win time not changed, try /setmatchtime xxx (in minutes)");
                }
            }

            if (messageText.Contains("/setdist"))
            {
				//MyAPIGateway.Utilities.ShowNotification("Capture distance is temporarily hardcoded at 2500m");


                try
                {
                    string[] tempdist = messageText.Split(' ');

                    capdist = double.Parse(tempdist[1]);
                    MyAPIGateway.Utilities.ShowNotification("Capture distance changed to " + tempdist[1]);
								if (!MyAPIGateway.Utilities.IsDedicated)
                    sendToOthers = true;
					GetSphereEntity();
                }
                catch (Exception)
                {
                    MyAPIGateway.Utilities.ShowNotification("Capture distance not changed, try /setdist ####");
					
               }
  
			}
                   
				   
				   if (messageText.Contains("/setteams"))
            {

                try
                {
                    string[] tempdist = messageText.Split(' ');

                    team1 = tempdist[1].ToUpper();
					team2 = tempdist[2].ToUpper();
                    MyAPIGateway.Utilities.ShowNotification("Teams changed to " + tempdist[1] +" vs " + tempdist[2]);
                    sendToOthers = true;
                }
                catch (Exception)
                {
                    MyAPIGateway.Utilities.ShowNotification("Teams not changed, try /setteams abc xyz");
                }
            }
			                   if (messageText.Contains("/settime"))
            {

                try
                {
                    string[] tempdist = messageText.Split(' ');
  					wintime = int.Parse(tempdist[1]);
                    MyAPIGateway.Utilities.ShowNotification("Win time changed to " + wintime.ToString());
                    sendToOthers = true;
                }
                catch (Exception)
                {
                    MyAPIGateway.Utilities.ShowNotification("Win time not changed, try /settime xxx (in seconds)");
                }
            }
						                   if (messageText.Contains("/setdelay"))
            {

                try
                {
                    string[] tempdist = messageText.Split(' ');
  					delaytime = int.Parse(tempdist[1]);
                    MyAPIGateway.Utilities.ShowNotification("Delay time changed to " + delaytime.ToString() + " minutes.");
					delaytime = delaytime * 60 * 60;
                    sendToOthers = true;
                }
                catch (Exception)
                {
                    MyAPIGateway.Utilities.ShowNotification("Delay time not changed, try /setdelay x (in minutes)");
                }
            }
						                   if (messageText.Contains("/setdecay"))
            {

                try
                {
                    string[] tempdist = messageText.Split(' ');
					decaytime = int.Parse(tempdist[1]);
                    MyAPIGateway.Utilities.ShowNotification("Decay time changed to " + decaytime.ToString());
					decaytime = decaytime * 60;
                    sendToOthers = true;
                }
                catch (Exception)
                {
                    MyAPIGateway.Utilities.ShowNotification("Decay time not changed, try /setdecay xxx (in seconds)");
                }
            }
			
			
			if (messageText.Contains("/start") || messageText.Contains("/broadcast"))
            {
                sendToOthers = true;
                Static.MyNetwork.TransmitToServer(new BasicPacket(6));
                try
                {
                    //int existing = (int)MySessionComponentSafeZones.AllowedActions;
                    //MySessionComponentSafeZones.AllowedActions = CastProhibit(MySessionComponentSafeZones.AllowedActions, existing | 0x1); //damage

                    broadcaststat = !broadcaststat;
                    broadcaststat = true;
                    //timerMessage.Visible = broadcaststat;
                    MyAPIGateway.Utilities.ShowNotification("Go go go!  Capture zone activates in " + delaytime / 3600 + "m, match ends in " + matchtime / 3600 + "m.");
                    MyAPIGateway.Utilities.ShowMessage("Match", "Match started");
                    MyAPIGateway.Utilities.ShowNotification("Match started", 2000, "White");
                    sendToOthers = true;
				foreach (var p in listPlayers)
                            {
                        MyAPIGateway.Utilities.ShowMessage("Match", "Match started");
                        MyAPIGateway.Utilities.ShowNotification("Match started", 2000, "White");
                        Sandbox.Game.MyVisualScriptLogicProvider.SendChatMessage("GO GO GO! Match has begun!", "Zone", p.IdentityId, "White"); //broken
                            }
					timer = 0;
                    sendToOthers = true;
                }
                catch (Exception)
                {
                }
            }



	   }


        public void AddPointValues(object obj)
        {
            string var = MyAPIGateway.Utilities.SerializeFromBinary<string>((byte[])obj);
            if (var != null)
            {
                string[] split = var.Split(';');
                foreach (string s in split)
                {
                    string[] parts = s.Split('@');
                    int value;
                    if (parts.Length == 2 && int.TryParse(parts[1], out value))
                    {
                        string name = parts[0].Trim();
                        if (name.Contains("{LS}"))
                        {
                            PointValues.Remove(name.Replace("{LS}", "Large"));
                            PointValues.Add(name.Replace("{LS}", "Large"), value);

                            PointValues.Remove(name.Replace("{LS}", "Small"));
                            PointValues.Add(name.Replace("{LS}", "Small"), value);
                        } 
                        else
                        {
                            PointValues.Remove(name);
                            PointValues.Add(name, value);
                        }
                    }
                }
            }
        }

        public override void LoadData()
        {
            MyAPIGateway.Utilities.RegisterMessageHandler(2546247, AddPointValues);
        }


        public override void BeforeStart()
        {
			
			if (!MyAPIGateway.Utilities.IsDedicated)
                _sphereEntity = GetSphereEntity();
            if (!MyAPIGateway.Utilities.IsDedicated)
            {
                text_api = new HudAPIv2(HUDRegistered);
            }

            WC_api = new WcApi();
            if (WC_api != null)
            {
                WC_api.Load();
            }

            SH_api = new ShieldApi();
            if (SH_api != null)
            {
                SH_api.Load();
            }

            RTS_api = new RtsApi();
            if (RTS_api != null)
            {
                RTS_api.Load();
            }
        }

        private void HUDRegistered()
        {
            statMessage = new HudAPIv2.HUDMessage(Message: new StringBuilder(""), Origin: new Vector2D(-.99, .99), HideHud: false);
            statMessage.Blend = BlendTypeEnum.PostPP;
            statMessage.Visible = false;
            statMessage.InitialColor = Color.Orange;

            integretyMessage = new HudAPIv2.HUDMessage(Scale: .7f, Font: "monospace", Message: new StringBuilder(""), Origin: new Vector2D(.55, .99), HideHud: false, Blend: BlendTypeEnum.PostPP);
            integretyMessage.Visible = true;
            integretyMessage.InitialColor = Color.Orange;
			
			timerMessage = new HudAPIv2.HUDMessage(Scale: 1.5f, Message: new StringBuilder(""), Origin: new Vector2D(0, .99), HideHud: false, Shadowing: true, Blend: BlendTypeEnum.PostPP);
            timerMessage.Visible = false;
            timerMessage.InitialColor = Color.White;
			timerMessage.ShadowColor = Color.Black;
        }

        public override void UpdateAfterSimulation()
        {
            timer++;
            try
            {
                if (timer % 60 == 0)
                {
                    all_players.Clear();
                    listPlayers.Clear();
                    
                    MyAPIGateway.Multiplayer.Players.GetPlayers(listPlayers);
                    foreach (var p in listPlayers)
                    {
                        all_players.Add(p.IdentityId, p);
                    }

                    if (MyAPIGateway.Session.IsServer)
                    {
                        foreach(var x in Sending.Keys)
                        {
                            if (!Data.ContainsKey(x))
                            {
                                var e = MyEntities.GetEntityById(x);
                                if (e != null && e is IMyCubeGrid && e.Physics != null)
                                {
                                    Data.Add(x, new ShipTracker(e as IMyCubeGrid));
                                    if (!MyAPIGateway.Utilities.IsDedicated)
                                    {
                                        Data[x].CreateHud();
                                    }
                                } 
                                else
                                {
                                    continue;
                                }
                            } 
                            else
                            {
                                Data[x].Update();
                            }

                            foreach(var p in Sending[x])
                            {
                                PacketGridData packet = new PacketGridData
                                {
                                    id = x,
                                    tracked = Data[x]
                                };
                                Static.MyNetwork.TransmitToPlayer(packet, p);
                            }
                        }
                    }
                }
            } catch {}
        }

        public override void Draw()
        {
            if (MyAPIGateway.Utilities.IsDedicated)
            {
                return;
            }

            try
            {
                if (MyAPIGateway.Session?.Camera != null && MyAPIGateway.Session.CameraController != null && !MyAPIGateway.Gui.ChatEntryVisible &&
                        !MyAPIGateway.Gui.IsCursorVisible && MyAPIGateway.Gui.GetCurrentScreen == MyTerminalPageEnum.None)
                {
                    if (MyAPIGateway.Input.IsKeyPress(MyKeys.LeftShift) && MyAPIGateway.Input.IsNewKeyPressed(MyKeys.T))
                    {
                        if (vState == ViewState.None)
                        {
                            vState = ViewState.InView;               
                        }
                        else if (vState == ViewState.InView)
                        {
                            vState = ViewState.ExitView;
                        }
                    }

                    if (MyAPIGateway.Input.IsKeyPress(MyKeys.Shift) && MyAPIGateway.Input.IsNewKeyPressed(MyKeys.M))
                    {
                        if (MyAPIGateway.Session.PromoteLevel >= MyPromoteLevel.Moderator)
                        {
                            var camMat = MyAPIGateway.Session.Camera.WorldMatrix;
                            IHitInfo hits = null;
                            MyAPIGateway.Physics.CastRay(camMat.Translation + camMat.Forward * 0.5, camMat.Translation + camMat.Forward * 500, out hits);
                            if (hits != null && hits.HitEntity is IMyCubeGrid)
                            {
                                PacketGridData packet = new PacketGridData
                                {
                                    id = hits.HitEntity.EntityId,
                                    value = (byte)(Tracking.Contains(hits.HitEntity.EntityId) ? 2 : 1),
                                };
                                Static.MyNetwork.TransmitToServer(packet, true);
                                if (packet.value == 1)
                                {
                                    MyAPIGateway.Utilities.ShowNotification("ShipTracker: Added grid to tracker");
                                    Tracking.Add(hits.HitEntity.EntityId);
									
									if (integretyMessage.Visible == false)
									{
										integretyMessage.Visible = true;
									}

                                    //fix for disappearing nameplates?
                                    Data[hits.HitEntity.EntityId].CreateHud();
                                    //end fix
                                }
                                else
                                {
                                    MyAPIGateway.Utilities.ShowNotification("ShipTracker: Removed grid from tracker");
                                    Tracking.Remove(hits.HitEntity.EntityId);
									Data[hits.HitEntity.EntityId].DisposeHud();
                                }
                            }
                        }
                    }

                    if (MyAPIGateway.Input.IsKeyPress(MyKeys.Shift) && MyAPIGateway.Input.IsNewKeyPressed(MyKeys.N))
                    {
                        if (MyAPIGateway.Session.PromoteLevel >= MyPromoteLevel.Moderator)
                        {
                            integretyMessage.Visible = !integretyMessage.Visible;
							//timerMessage.Visible = !timerMessage.Visible;
                            MyAPIGateway.Utilities.ShowNotification("ShipTracker: Hud visibility set to " + integretyMessage.Visible);
                        }
                    }

                    if (MyAPIGateway.Input.IsKeyPress(MyKeys.Shift) && MyAPIGateway.Input.IsNewKeyPressed(MyKeys.B))
                    {
                        if (MyAPIGateway.Session.PromoteLevel >= MyPromoteLevel.Moderator)
                        {
                            //integretyMessage.Visible = !integretyMessage.Visible;
                            timerMessage.Visible = !timerMessage.Visible;
                            MyAPIGateway.Utilities.ShowNotification("ShipTracker: Timer visibility set to " + timerMessage.Visible);
                        }
                    }

                    if (MyAPIGateway.Input.IsKeyPress(MyKeys.Shift) && MyAPIGateway.Input.IsNewKeyPressed(MyKeys.J))
                    {
                        if (MyAPIGateway.Session.PromoteLevel >= MyPromoteLevel.Moderator)
                        {
							viewstat++;
							if (viewstat==4)
							{
								viewstat=0;
							}
							if (viewstat==3)
							{
							NameplateVisible = false;
							}
							else
							{
							NameplateVisible = true;
							}
							MyAPIGateway.Utilities.ShowNotification("ShipTracker: Nameplate visibility set to " + viewmode[viewstat]);
                        }
                    }
                }

                if (text_api.Heartbeat)
                {
                    foreach (var x in Data.Keys)
                    {
                        if (Tracking.Contains(x))
                        {
                            Data[x].UpdateHud();
                        } 
                        else
                        {
                            Data[x].DisposeHud();
                        }
                    }
                }


                if (vState == ViewState.InView && statMessage != null && text_api.Heartbeat)
                {
                    var camMat = MyAPIGateway.Session.Camera.WorldMatrix;
                    IHitInfo hits = null;
                    MyAPIGateway.Physics.CastRay(camMat.Translation + camMat.Forward * 0.5, camMat.Translation + camMat.Forward * 500, out hits);
                    if (hits != null && hits.HitEntity is IMyCubeGrid)
                    {
                        IMyCubeGrid icubeG = hits.HitEntity as IMyCubeGrid;

                        if (icubeG != null && icubeG.Physics != null)
                        {
                            if (timer % 10 == 0)
                            {
                                ShipTracker tracked = new ShipTracker(icubeG);

                                string total_shield_string = "None";
                                if (tracked.TotalShieldStrength > 100)
                                {
                                    total_shield_string = Math.Round((tracked.TotalShieldStrength / 100f), 2).ToString() + " M";
                                }
                                if (tracked.TotalShieldStrength > 1 && tracked.TotalShieldStrength < 100)
                                {
                                    total_shield_string = Math.Round((tracked.TotalShieldStrength), 0).ToString() + "0 K";
                                }

                                string gunText = "";
                                foreach (var x in tracked.GunList.Keys)
                                {
                                    gunText += "<color=Green>" + tracked.GunList[x] + "<color=White> x " + x + "\n";
                                }

                                string specialBlockText = "";
                                foreach (var x in tracked.SpecialBlockList.Keys)
                                {
                                    specialBlockText += "<color=Green>" + tracked.SpecialBlockList[x] + "<color=White> x " + x + "\n";
									//if (tracked.SpecialBlockList[x] == "
								
								}

                                string massString = tracked.Mass.ToString();
                                if (tracked.Mass > 1000000)
                                {
                                    massString = Math.Round((tracked.Mass / 1000000f), 2).ToString() + "m";
                                }

                                string thrustString = tracked.InstalledThrust.ToString();
                                if (tracked.InstalledThrust > 1000000)
                                {
                                    thrustString = Math.Round((tracked.InstalledThrust / 1000000f), 2).ToString() + " M";
                                }

                                string playerName = tracked.Owner == null ? "Unowned" : tracked.Owner.DisplayName;
                                string factionName = tracked.Owner == null ? "None" : MyAPIGateway.Session?.Factions?.TryGetPlayerFaction(tracked.OwnerID)?.Name;

                                float speed = icubeG.GridSizeEnum == MyCubeSize.Large ? MyDefinitionManager.Static.EnvironmentDefinition.LargeShipMaxSpeed : MyDefinitionManager.Static.EnvironmentDefinition.SmallShipMaxSpeed;
                                if (RTS_api != null && RTS_api.IsReady)
                                {
                                    speed = (float)Math.Round(RTS_api.GetMaxSpeed(icubeG), 2);
                                    
                                    /*if (!speed.IsValid())
                                    {
                                        MyAPIGateway.Utilities.ShowNotification("Speed is NaN! \nDelete reletive speed mod config!", 16 * 10, "Red");
                                    }*/
                                }
								
								
								
							


                                var temp_text =
                                        "----Basic Info----"
                                        + "\n" + "<color=Green>Name<color=White>: " + icubeG.DisplayName
                                        + "\n" + "<color=Green>Owner<color=White>: " + playerName
                                        + "\n" + "<color=Green>Faction<color=White>: " + factionName
                                        + "\n" + "<color=Green>Mass<color=White>: " + massString + " kg"
                                        + "\n" + "<color=Green>Heavy blocks<color=White>: " + tracked.Heavyblocks.ToString()
                                        + "\n" + "<color=Green>Total blocks<color=White>: " + tracked.BlockCount.ToString()
                                        + "\n" + "<color=Green>PCU<color=White>: " + tracked.PCU
                                        + "\n" + "<color=Green>Size<color=White>: " + (icubeG.Max + Vector3.Abs(icubeG.Min)).ToString()
                                        + "\n" + "<color=Green>Max Speed<color=White>: " + speed
                                        + "\n"
                                        + "\n" + "<color=Orange>----Battle Stats----"
                                        + "\n" + "<color=Green>Battle Points<color=White>: " + tracked.BattlePoints.ToString()
                                        + "\n" + "<color=Green>DPS<color=White>: " + ((int)tracked.DPS).ToString()
                                        + "\n" + "<color=Green>Shield Max HP<color=White>: " + total_shield_string + " (" + (int)tracked.CurrentShieldStrength + "%)"
                                        + "\n" + "<color=Green>Thrust<color=White>: " + thrustString + "N"
                                        + "\n"
                                        + "\n" + "<color=Orange>----Blocks----"
                                        + "\n" + specialBlockText
                                        + "\n"
                                        + "\n" + "<color=Orange>----Armament----"
                                        + "\n" + gunText;

                                statMessage.Message.Clear();
                                statMessage.Message.Append(temp_text);
                                statMessage.Visible = true;
                            }

                            
                        }
                    }
                    else
                    {
                        if (statMessage != null && text_api.Heartbeat)
                        {
                            if (statMessage.Visible)
                            {
                                statMessage.Message.Clear();
                                statMessage.Visible = false;
                            }
                        }
                    }
                }
                if (timer % 60 == 0 && integretyMessage != null && text_api.Heartbeat)
                {
                    StringBuilder temp_text = new StringBuilder();

                    Dictionary<string, List<string>> trackedShips = new Dictionary<string, List<string>>();
                    Dictionary<string, double> totalMass = new Dictionary<string, double>();
                    Dictionary<string, int> totalBattlePoints = new Dictionary<string, int>();
                    foreach (var z in Tracking)
                    {
                        if (!Data.ContainsKey(z))
                        {
                            continue;
                        }
                        var data = Data[z];
                        data.LastUpdate--;
                        if (data.LastUpdate <= 0)
                        {
                            Data[z].DisposeHud();
                            Data.Remove(z);
                            continue;
                        }

                        string factionName = data.FactionName;
                        if (!trackedShips.ContainsKey(factionName))
                        {
                            trackedShips.Add(factionName, new List<string>());
                            totalMass.Add(factionName, 0);
                            totalBattlePoints.Add(factionName, 0);
                        }

                        totalMass[factionName] += data.Mass;
                        totalBattlePoints[factionName] += data.BattlePoints;

                        int guns = 0;
                        foreach(int s in data.GunList.Values)
                        {
                            guns += s;
                        }
						
						//center distance info
						
						if (Vector3.Distance(ctrpoint, data.Position) < capdist && data.IsFunctional)
						{
						capstat = "Cap";
							
							if (!ZoneControl.Contains(factionName))
							{
							ZoneControl += factionName.ToUpper();
							}
						}
						else
						{
						capstat = "";
						}
						

						
						
						//end of center zone info
						
                        trackedShips[factionName].Add(string.Format("<color={4}>{0,-12}|HP:<color=orange>{1,3}%<color={4}>|WEPS:<color={6}>{2,3}<color={4}>|SHLD:<color={5}>{3,3}%<color=white>|{7,4}",
                            data.OwnerName?.Substring(0, Math.Min(data.OwnerName.Length, 12)) ?? "Unowned",
                            (int)(data.CurrentIntegrity / data.OriginalIntegrity * 100),
                            guns,
                            (int)data.CurrentShieldStrength,
							data.IsFunctional ? "white" : "red",
                            (int)data.CurrentShieldStrength <= 0 ? "red" : $"{255},{255 - (data.ShieldHeat * 20)},{255 - (data.ShieldHeat * 20)}",
                            guns == 0 ? "red" : "orange",
							capstat));
						
                    }

                    foreach (var x in trackedShips.Keys)
                    {
                        string massStr = Math.Round((totalMass[x] / 1000000f), 2).ToString() + "M";
                        temp_text.Append("<color=white>---------- <color=orange>" + x + " : " + massStr + " : " + totalBattlePoints[x] + "bp<color=white> ----------\n");
                        foreach(var y in trackedShips[x])
                        {
                            temp_text.Append(y + "\n");
                        }
                    }


                    //Timer stuff

                    
                    
					                    //win via match duration
                    if (timer >= matchtime && broadcaststat)

                    {

                        /*
                        if(totalBattlePoints.ContainsKey(team1) && totalBattlePoints.ContainsKey(team2))
                        {
                           string winner = "";
                           if (totalBattlePoints[team1]>totalBattlePoints[team2])
                            {
                                winner = team1.ToString();
                            }
                           else
                            {
                                winner = team2.ToString();
                            }


                            //MyAPIGateway.Multiplayer.Players.GetPlayers(listPlayers);
                            MyAPIGateway.Utilities.ShowNotification(winner.ToString()+" has the highest remaining BP");
                            //sendToOthers = true;
                           
                            
                            foreach (var p in listPlayers)
                            {
                                Sandbox.Game.MyVisualScriptLogicProvider.SendChatMessage(winner.ToString() + " has the highest remaining BP", "Zone", p.IdentityId, "White");
                            }
                            
                        }
                        else
                        {
                        */
                            MyAPIGateway.Multiplayer.Players.GetPlayers(listPlayers);

                            MyAPIGateway.Utilities.ShowNotification("Match time has ended!  Consult the scoreboard to determine a victor.");
                            //sendToOthers = true;
                            
                            foreach (var p in listPlayers)
                            {
                            MyAPIGateway.Utilities.ShowNotification("Match time has ended!  Consult the scoreboard to determine a victor.");
                            Sandbox.Game.MyVisualScriptLogicProvider.SendChatMessage("Match time has ended!  Consult the scoreboard to determine a victor.", "Zone", p.IdentityId, "White");
                            }
                            
                            
                       // }
                        timerMessage.Visible = !broadcaststat;
                        broadcaststat = !broadcaststat;

                    }
                    
					//Start of zone control msg
					if (delaytime-timer == 3600 && broadcaststat == true)							
					{
							MyAPIGateway.Utilities.ShowNotification("Zone activates in 60 seconds.");
							                            foreach (var p in listPlayers)
                            {
                            MyAPIGateway.Utilities.ShowNotification("Zone activates in 60 seconds.");
                            Sandbox.Game.MyVisualScriptLogicProvider.SendChatMessage("Zone activates in 60 seconds.", "Zone", p.IdentityId, "White");
                            }					
					}
					if (delaytime==timer && broadcaststat == true)							
					{
							MyAPIGateway.Utilities.ShowNotification("Zone is active.");
							                            foreach (var p in listPlayers)
                            {
                            MyAPIGateway.Utilities.ShowNotification("Zone is active.");
                            Sandbox.Game.MyVisualScriptLogicProvider.SendChatMessage("Zone is active.", "Zone", p.IdentityId, "White");
                            }					
					}


                    if (matchtime - timer == 18000 && broadcaststat == true)
                    {
                        MyAPIGateway.Utilities.ShowNotification("Five minutes left!");
                        foreach (var p in listPlayers)
                        {
                            MyAPIGateway.Utilities.ShowNotification("Five minutes left!");
                            Sandbox.Game.MyVisualScriptLogicProvider.SendChatMessage("Five minutes left!", "Zone", p.IdentityId, "White");
                        }
                    }

                    if (matchtime - timer == 7200 && broadcaststat == true)
                    {
                        MyAPIGateway.Utilities.ShowNotification("Two minutes left!");
                        foreach (var p in listPlayers)
                        {
                            MyAPIGateway.Utilities.ShowNotification("Two minutes left!");
                            Sandbox.Game.MyVisualScriptLogicProvider.SendChatMessage("Two minutes left!", "Zone", p.IdentityId, "White");
                        }
                    }
                    if (matchtime-timer== 3600 && broadcaststat == true)
                    {
                        MyAPIGateway.Utilities.ShowNotification("One minute left!");
                        foreach (var p in listPlayers)
                        {
                            MyAPIGateway.Utilities.ShowNotification("One minute left!");
                            Sandbox.Game.MyVisualScriptLogicProvider.SendChatMessage("One minute left!", "Zone", p.IdentityId, "White");
                        }
                    }


                    //actual tracking
                    if (timer>=delaytime && broadcaststat == true)
					{
					if (ZoneControl.Contains(team1) && ZoneControl.Contains(team2))
					{
					ZoneControl = "Contested";
                        zonemsg = "Contested";
						if (timer % decaytime == 0 && captimer != 0)
						{
							if (captimer >0)
							{
								captimer--;
							}
							else
							{
								captimer++;
							}
						
						}
						
						
						
                    }
					else
					{
						if (!ZoneControl.Contains(team1) && !ZoneControl.Contains(team2))
						{
						ZoneControl = "Unoccupied";
                            zonemsg = "Unoccupied";
                            captimer = 0;
						}
						else
						{
							if (ZoneControl.Contains(team1) && !ZoneControl.Contains(team2))
							{
							ZoneControl = "<color=tomato>" + team1 + " Occupied";
                                zonemsg = team1 + " Occupied";
							captimer ++;
							}
							else
							{
							ZoneControl = "<color=dodgerblue>" + team2 + " Occupied";
                                zonemsg = team2 + " Occupied";
                                captimer --; 
							}
							
						}
					}
					
					
					if (captimer > 0)
					{
					ZoneControl += "<color=tomato> ";
					}
					else
					{
						if (captimer < 0)
						{
						ZoneControl += "<color=dodgerblue> ";	
						}
						else
						{
						ZoneControl += "<color=white> ";
						}
					}
					int temp_timer = Math.Abs(captimer);
					
                        string minutes = (((matchtime - timer) / 60) / 60).ToString();
                        if (minutes.Length == 1)
                        {
                            minutes = "0" + minutes;
                        }
                        if (minutes.Length == 0)
                        {
                            minutes = "00";
                        }

                        string seconds = ((matchtime-timer)/60-int.Parse(minutes)*60).ToString();
                        if (seconds.Length == 1)
                        {
                            seconds = "0" + seconds;
                        }
                        if (seconds.Length == 0)
                        {
                            seconds = "00";
                        }

                        //var seconds = 0;

                        timerMessage.Message.Clear();
					timerMessage.Message.Append("Match Time " + minutes +":" + seconds +"\n<color=tomato>" + team1 +" <color=white> vs <color=dodgerblue> " + team2 + "<color=white>\n" + ZoneControl + temp_timer.ToString());
					
                    //MyAPIGateway.Utilities.ShowMessage("", ZoneControl);
                    ZoneControl = "";



					if (temp_timer >= wintime && broadcaststat==true)
					{
						broadcaststat = false;
						temp_timer = 0;
						
						
						
						
						if (captimer > 0)
							{
							MyAPIGateway.Utilities.ShowNotification(team1 + " team has captured the point.");
							                            foreach (var p in listPlayers)
                            {
                                Sandbox.Game.MyVisualScriptLogicProvider.SendChatMessage(team1 + " team has captured the point.", "Zone", p.IdentityId, "White");
                            }
							}
						else
							{
							MyAPIGateway.Utilities.ShowNotification(team2 + " team has captured the point.");
							                            foreach (var p in listPlayers)
                            {
                                Sandbox.Game.MyVisualScriptLogicProvider.SendChatMessage(team2 + " team has captured the point.", "Zone", p.IdentityId, "White");
                            }
							}
					timerMessage.Visible = broadcaststat;
					
					}


                    if (timer % 600 == 0 && broadcaststat == true && temp_timer <= wintime)
                    {
                        if (captimer > 0)
                        {
                            temp_time_msg = ",  " + team1 + " " + temp_timer.ToString();
                        }
                        else if (captimer < 0)
                        {
                            temp_time_msg = ",  " + team2 + " " + temp_timer.ToString();
                        }
						else if (captimer == 0)
						{
							temp_time_msg = "";
						}
                        if (zonemsg == "Unoccupied")
                        {
                            temp_time_msg = "";
                        }

                        if (zonemsg != "Unoccupied")
                        {
                                Static.MyNetwork.TransmitToServer(new BasicPacket(6));
                                MyAPIGateway.Utilities.ShowNotification(zonemsg + temp_time_msg, 1000, "White");
                                MyAPIGateway.Utilities.SendMessage(zonemsg + temp_time_msg);
                                MyAPIGateway.Multiplayer.Players.GetPlayers(listPlayers);
                            foreach (var p in listPlayers)
                            {
                                    MyAPIGateway.Utilities.ShowNotification(zonemsg + temp_time_msg, 1000, "White");
                                    MyAPIGateway.Utilities.SendMessage(zonemsg + temp_time_msg);
                                    Sandbox.Game.MyVisualScriptLogicProvider.SendChatMessage(zonemsg + temp_time_msg, "Zone", p.IdentityId, "White");//try here
                            }
                        }

                        old_time_msg = temp_time_msg;
					}

					}
					



                    integretyMessage.Message.Clear();
                    integretyMessage.Message.Append(temp_text);
                }
				
				
				
				

					
					
					
					
					
					
                if (vState == ViewState.ExitView)
                {
                    if (statMessage != null && text_api.Heartbeat)
                    {
                        if (statMessage.Visible)
                        {
                            statMessage.Message.Clear();
                            statMessage.Visible = false;
                        }
                    }
                    vState = ViewState.None;
                }

            }
			


			
            catch (Exception e)
            {
                if (timer % 60 == 0)
                {
                    //MyAPIGateway.Utilities.ShowMessage("", "ShipPoints: " + e.Message);
                }
            }
        }

        public static IMyPlayer GetOwner(long v)
        {
            if (all_players != null && all_players.ContainsKey(v))
            {
                return all_players[v];
            }
            return null;
        }


        protected override void UnloadData()
        {
            if (text_api != null)
            {
                text_api.Unload();
            }
            if (WC_api != null)
            {
                WC_api.Unload();
            }
            if (SH_api != null)
            {
                SH_api.Unload();
            }
            MyAPIGateway.Utilities.MessageEntered -= MessageEntered;
            Static?.Dispose();
            MyAPIGateway.Utilities.UnregisterMessageHandler(2546247, AddPointValues);
        }
    }
}