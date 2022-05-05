using klime.PointCheck;
using Math0424.ShipPoints;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using VRage.Game.ModAPI;
using static Math0424.Networking.MyEasyNetworkManager;


namespace Math0424.Networking
{
    class MyNetworkHandler : IDisposable
    {

        public MyEasyNetworkManager MyNetwork;
        public static MyNetworkHandler Static;
		private static List<ulong> all_players = new List<ulong>();
        private static List<IMyPlayer> listPlayers = new List<IMyPlayer>();

        public static void Init()
        {
            if (Static == null)
            {
                Static = new MyNetworkHandler();
            }
        }

        public static void Main()
        {
            
        }

        protected MyNetworkHandler()
        {
            MyNetwork = new MyEasyNetworkManager(45674);
            MyNetwork.Register();

            MyNetwork.OnRecievedPacket += PacketIn;
        }

        private void PacketIn(PacketIn e)
        {

            if (e.PacketId == 1)
            {
				
				
				
			//inject for shared list

                    all_players.Clear();
                    listPlayers.Clear();
                    MyAPIGateway.Players.GetPlayers(listPlayers);
                    foreach (var p in listPlayers)
                    {
				    all_players.Add(p.SteamUserId);
                    }
			//end
			
			
			
			
                var packet = e.UnWrap<PacketGridData>();
                if (MyAPIGateway.Multiplayer.IsServer)
                {
                    var x = MyEntities.GetEntityById(packet.id);
                    if (x != null && x is IMyCubeGrid)
                    {
                        if (packet.value == 1 && MyAPIGateway.Session.IsUserAdmin(e.SenderId)) //add
                        {
                            if (PointCheck.Sending.ContainsKey(packet.id))
                            {
                                
								try
								{
								PointCheck.Sending[packet.id].Remove(e.SenderId);
								}
								catch{}
                            }
                            else
                            {
                                PointCheck.Sending.Add(packet.id, new List<ulong>());
                            }
                            
							
							//PointCheck.Sending[packet.id].Add(e.SenderId);
							
							foreach (var p in all_players)
							{
                            PointCheck.Sending[packet.id].Add(p);
							}
							
							/*
							try
							{
							PointCheck.Data.Add(packet.id, packet.tracked);
							}
							catch{}
							*/							
							
							//end
                        }
                        else if (packet.value == 2) //remove
                        {
                            if (PointCheck.Sending.ContainsKey(packet.id))
                            {
                                
								
								//PointCheck.Sending[packet.id].Remove(e.SenderId);
								
                                foreach (var p in all_players)
								{
                                PointCheck.Sending[packet.id].Remove(p);
								}
								
								//end
								
								
								if (PointCheck.Sending[packet.id].Count == 0)
                                {
                                    PointCheck.Sending.Remove(packet.id);

                                    if (PointCheck.Sending.Count == 0)
                                    {
                                        PointCheck.Data[packet.id].DisposeHud();
                                        PointCheck.Data.Remove(packet.id);
                                    }

                                }
                            }
                        }
                    }
                }
                else
                {
					//Inject
					 if (packet.value == 1 && !PointCheck.Tracking.Contains(packet.id))
					{
					PointCheck.Tracking.Add(packet.id);
					PointCheck.Data[packet.id].CreateHud();	
					}
					else if (packet.value == 2 && PointCheck.Tracking.Contains(packet.id))
					{
					PointCheck.Tracking.Remove(packet.id);	
					}
					//end
					
                    packet.tracked.CreateHud();
                    if (PointCheck.Data.ContainsKey(packet.id))
                    {
                        PointCheck.Data[packet.id].DisposeHud();
                        PointCheck.Data[packet.id] = packet.tracked;
                    }
                    else
                    {
                        PointCheck.Data.Add(packet.id, packet.tracked);    
                    }
                }
            }

            if (e.PacketId == 5)
            {
                if (MyAPIGateway.Session.IsUserAdmin(e.SenderId))
                {
                    foreach (var g in MyEntities.GetEntities())
                    {
                        if (g != null && !g.MarkedForClose && g is MyCubeGrid)
                        {
                            var grid = g as MyCubeGrid;
                            var block = PointCheck.SH_api.GetShieldBlock(grid);
                            if (block != null)
                            {
                                PointCheck.SH_api.SetCharge(block, 99999999999);
                            }
                        }
                    }
                    MyAPIGateway.Utilities.ShowMessage("Shields", "Charged");
                    MyAPIGateway.Utilities.ShowNotification("Shields",2000, "White");
                    MyAPIGateway.Utilities.SendMessage("Shields Charged!");
                }
            }
            

            if (e.PacketId == 6)
            {
                PointCheck.broadcaststat =! PointCheck.broadcaststat;
                MyAPIGateway.Utilities.ShowNotification("Go go go!  Capture zone activates in " + PointCheck.delaytime / 3600 + "m, match ends in " + PointCheck.matchtime / 3600 + "m.");
                MyAPIGateway.Utilities.ShowMessage("Match", "Match started");
                MyAPIGateway.Utilities.ShowNotification("Match started", 2000, "White");
                PointCheck.timer = 0;


            }
        }

        public void Dispose()
        {
            MyNetwork.UnRegister();
            MyNetwork = null;
            Static = null;
        }
    }
}
