using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.Config;
using Vintagestory.API.Common.Entities;
using VSSurvivalMod;
using Vintagestory.GameContent;
using Vintagestory.GameContent.Mechanics;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using System.Collections.Generic;
using System.Text;
using SpiceSharp;
using SpiceSharp.Components;
using SpiceSharp.Simulations;
using System.Linq;

[assembly: ModInfo( "ElectricalRevolution",
	Description = "Bringing the electrical revolution to Vintage Story",
	Website     = "",
	Authors     = new []{ "OmegaHaxors" } )]

namespace ElectricalRevolution
{
	public class ELR: ModSystem
	{
		ICoreAPI api = null;
		ICoreServerAPI sapi = null;
		ICoreClientAPI capi = null;

        ///warning: These are only instances of blocks, but I made special handling so that you can act on them directly
        ///It's not flawless. When loading from a save file, the block entity will be null until its BE is loaded.
        ///However, any information that was set while the block was unloaded will instantly apply when it is.
		public Dictionary<BlockPos, BEBehaviorElectricalNode> blockmap = new Dictionary<BlockPos, BEBehaviorElectricalNode>();

        public Dictionary<string,SpiceSharp.Entities.IEntity> componentlist = new Dictionary<string,SpiceSharp.Entities.IEntity>();
        public Dictionary<string,SpiceSharp.Simulations.TimeParameters> TPlist = new Dictionary<string,SpiceSharp.Simulations.TimeParameters>();

        public BlockPos[] leadermap = new BlockPos[0];

		public override void Start(ICoreAPI api)
		{
			this.api = api;
			api.RegisterItemClass("ItemMeter",typeof(ItemMeter));
			api.RegisterBlockClass("LightBlock",typeof(LightBlock));
			api.RegisterBlockClass("CreativeSource",typeof(CreativeSource));
			api.RegisterBlockClass("MPGenerator",typeof(MPGenerator));
			api.RegisterBlockEntityClass("LightBlockEntity",typeof(LightBlockEntity));
			api.RegisterBlockEntityClass("CreativeSourceEntity",typeof(CreativeSourceEntity));
			api.RegisterBlockEntityClass("MPGeneratorBlockEntity",typeof(MPGeneratorBlockEntity));
			api.RegisterBlockEntityBehaviorClass("ElectricalNode",typeof(BEBehaviorElectricalNode));
			api.RegisterBlockEntityBehaviorClass("ElectricalConverter",typeof(BEBehaviorElectricalConverter));
			api.RegisterBlockEntityBehaviorClass("MPGenerator", typeof(BEBehaviorMPGenerator));
			api.RegisterBlockEntityBehaviorClass("CreativeConverter",typeof(BEBehaviorCreativeConverter));
		}

        public override void StartClientSide(ICoreClientAPI capi)
        {
            this.capi = capi;
        }

        public override void StartServerSide(ICoreServerAPI sapi)
        {
            this.sapi = sapi;
            sapi.Event.SaveGameLoaded += OnSaveGameLoading;
            sapi.Event.GameWorldSave += OnSaveGameSaving;
            sapi.World.RegisterGameTickListener(TickMNA, 1000); //tick the MNA every second
            Commands.registerCommands(api, sapi, this);
        }

        /// Read from loaded save data as the world starts up
        private void OnSaveGameLoading()
        {
            //in case of save corruption, uncomment the function below to recover
            //sapi.WorldManager.SaveGame.StoreData("blockmap",null);
            byte[] data = sapi.WorldManager.SaveGame.GetData("blockmap");
            blockmap = data == null ? new Dictionary<BlockPos, BEBehaviorElectricalNode>() : SerializerUtil.Deserialize<Dictionary<BlockPos, BEBehaviorElectricalNode>>(data);
        }

        ///Write save data to the world as it shuts down
        private void OnSaveGameSaving()
        {
            sapi.WorldManager.SaveGame.StoreData("blockmap", SerializerUtil.Serialize(blockmap));
        }

		private static IEnumerable<double> TimePoints
        {
            get
            {
                double time = 0;
                for (var i = 0; i < 10; i++)
                {
                    yield return time;
                    time += 0.1;
                }    
            }
        }

        ///Tries to find neighbours and attach them to eachother
		public void UpdateBlockmap(BlockPos thispos)
		{
			BlockPos[] neighbourposes = new BlockPos[6]{thispos.UpCopy(1),thispos.DownCopy(1),thispos.NorthCopy(1),thispos.EastCopy(1),thispos.SouthCopy(1),thispos.WestCopy(1),};
			foreach(BlockPos thatpos in neighbourposes)
			{
				//if(blockmap.ContainsKey(thatpos)){neighbourmap.Add(thatpos,blockmap[thatpos]);}
				if(blockmap.ContainsKey(thatpos))
				{
					bool thisblockisleader = false;
					bool thatblockisleader = false;
					BEBehaviorElectricalNode thisnode = blockmap[thispos];
					BEBehaviorElectricalNode thatnode = blockmap[thatpos];
					BEBehaviorElectricalNode leadernode = blockmap[thatnode.LeaderNode]; //the neighbour block's leader
					//ERROR: leadernode might no longer exist. Will need to act if this is ever not the case
					//leader now signals off if it's ever removed, this should prevent the issue from arising
					if(thisnode.NodeList == null || thatnode.NodeList == null){return;} //oh look, this problem again
					if(thisnode.NodeList.Length > 0){thisblockisleader = true;}
					if(thatnode.NodeList.Length > 0){thatblockisleader = true;}

					if(thisblockisleader)
					{
						if(thatblockisleader)
						{//Take responsiblity, set them as a recruiter and relieve them of their duties.
							//don't do it if the other node is better suited
							if(!NodeTieBreaker(thispos,thatpos)){continue;} //this should bias leaders towards lesser X values.
							thisnode.NodeList = thisnode.NodeList.Union(thatnode.NodeList).ToArray(); //merges the lists, ignoring duplicates
							thatnode.LeaderNode = thispos; //they now point to you
							foreach(BlockPos recruiterpos in (BlockPos[])thatnode.NodeList.Clone()) //work on a clone to avoid racism
							{
								blockmap[recruiterpos].LeaderNode = thispos;
								if(blockmap[recruiterpos].Blockentity!= null){blockmap[recruiterpos].Blockentity.MarkDirty(true);}
								} //tell their recruiters to move to the new leader
							thatnode.NodeList = new BlockPos[0]; //now it's a recruiter
						}else//thisblockisleader, thatblockisrecruiter
						{//Give up your blocklist to their leader and follow their leader
						//but make sure to check you're not just following yourself
						if(thatnode.LeaderNode == thisnode.LeaderNode){continue;} //don't let a leader give up its role to itself
							leadernode.NodeList = leadernode.NodeList.Union(thisnode.NodeList).ToArray(); //give your list to the leader
							thisnode.LeaderNode = thatnode.LeaderNode; //point to the new leader
							foreach(BlockPos recruiterpos in (BlockPos[])thisnode.NodeList.Clone()) //a clone of your list
							{blockmap[recruiterpos].LeaderNode = thatnode.LeaderNode;
							if(blockmap[recruiterpos].Blockentity!= null){blockmap[recruiterpos].Blockentity.MarkDirty(true);}
							} //tell your recruiters to move to the new leader
							thisnode.NodeList = new BlockPos[0]; //now you're a recruiter
						}

					}else{ //thisblockisrecruiter
						leadernode = blockmap[thisnode.LeaderNode]; //you're a recruiter, so your leader matters here

						if(thatblockisleader)
						{//recruit them to your leader and relieve them of their duties
						//but check to make sure they're not your leader first
							if(thisnode.LeaderNode == leadernode.LeaderNode){continue;} //should prevent instant recruit betrayals

							leadernode.NodeList = leadernode.NodeList.Union(thatnode.NodeList).ToArray();
							thatnode.LeaderNode = thispos;
							foreach(BlockPos recruiterpos in (BlockPos[])thatnode.NodeList.Clone()) //work on a clone to avoid racism
							{blockmap[recruiterpos].LeaderNode = thisnode.LeaderNode;
							if(blockmap[recruiterpos].Blockentity!= null){blockmap[recruiterpos].Blockentity.MarkDirty(true);}}
							thatnode.NodeList = new BlockPos[0];

						}else{//neitherblockisleader
							//if they have a different leader, tell them to join yours (merging)
							if(thisnode.LeaderNode != thatnode.LeaderNode)
							{
								leadernode.NodeList = leadernode.NodeList.Union(blockmap[thatnode.LeaderNode].NodeList).ToArray();
								foreach(BlockPos recruiterpos in (BlockPos[])thatnode.NodeList.Clone()) //work on a clone to avoid racism
								{blockmap[recruiterpos].LeaderNode = thisnode.LeaderNode;
								if(blockmap[recruiterpos].Blockentity!= null){blockmap[recruiterpos].Blockentity.MarkDirty(true);}}
								thatnode.LeaderNode = thisnode.LeaderNode;
							}
						}
						
					}
				}
			}
			CreateLeaderMap();
		}

        ///this is what compiles the blockmap into many seperate leadermaps so they can be turned into ckts and ultimately trans
        ///unlike the blockmap, the leadermap is not saved, since it's created from the blockmap every tick
		public void CreateLeaderMap()
		{
			leadermap = new BlockPos[0]; //first we wipe it clean to prevent any pollution from last tick
			foreach(KeyValuePair<BlockPos,BEBehaviorElectricalNode> entry in blockmap)
			{
				if(entry.Key == entry.Value.LeaderNode) 
				{//this is a leader
					leadermap = leadermap.Append(entry.Key); //add it to the leadermap
				}
			}
		}

        ///tiebreaker function. Smallest X, then Y, then Z wins. True if thisnode wins. False if thatnode wins.
        /// TODO: Replace with a BlockPos comparison function extension (if that exists in C#) so you can do `poslocal < posremote`
		public bool NodeTieBreaker(BlockPos poslocal, BlockPos posremote)
    	{
      		if(poslocal.X < posremote.X){return true;}
      		if(poslocal.X > posremote.X){return false;}
			//in most cases, it will resolve here, but in the case of a tie...
			if(poslocal.Y < posremote.Y){return true;}
     	 	if(poslocal.Y > posremote.Y){return false;}
    		  //gee golly, still not resolved?
      		if(poslocal.Z < posremote.Z){return true;}
    	 	if(poslocal.Z > posremote.Z){return false;}
    	 	throw new ArgumentException//something clearly went wrong. Throw an exception.
      		("ShouldIBeTheLeader was unable to resolve. It's likely because the local and remote position were the same.");
   		}

		public void CreateCircuit(Transient tran, out Circuit ckt)
		{
			ckt = new Circuit();
			/*(new Sampler("sampler",TimePoints, (sender, exargs) =>
                {
						tran.TimeParameters.StopTime =- 1; //reduce timeparameters by 1 per cycle (it stops at 0)
				})
			); */
			foreach(KeyValuePair<string,SpiceSharp.Entities.IEntity> entry in componentlist)
			{
				string componentname = entry.Key;
				SpiceSharp.Entities.IEntity component = entry.Value;
                string componenttype = PinHelper.GetNodeTypeFromName(componentname);
				if(ckt.TryGetEntity(componentname, out var throwaway)){continue;} //only add to the ckt if it wasn't there before
				AddComponent(tran,ckt,componentname,component);
			}
		}

		public void OnMNAExport(object sender, ExportDataEventArgs exargs)
		{
            Transient tran = (Transient)sender;
            if(tran.TimeParameters.StopTime <= 0)
            {
                MNAFinish(tran); //Tell the MNA to upload the information from this tick into the next so that it can pass on.
            }
		}

        ///finish the tick then shut down
		public void MNAFinish(Transient tran)
		{
			//foreach(KeyValuePair<string,Export<IBiasingSimulation, double>> entry in ICWatchers)
			//{
				//string nodename = entry.Key;
				//double readout = entry.Value.Value; //awkwaaard
				
			TPlist[tran.Name] = tran.TimeParameters; //this saves the TP for later

				//ckt.TryGetEntity(nodename, out SpiceSharp.Entities.IEntity node);
				//node.SetParameter("ic",ICWatchers[nodename].Value);
			//}
			castMNAtoblocks(tran); //sends data from the MNA into the blockents
		}

		public void castMNAtoblocks(Transient tran)
		{
			foreach(KeyValuePair<string,SpiceSharp.Entities.IEntity> entry in componentlist)
			{
				SpiceSharp.Entities.IEntity component = entry.Value;
				string componentname = component.Name;
				string componentlocation = PinHelper.GetPositivePin(componentname);
                BlockPos blockpos = PinHelper.PinToBlockPos(componentlocation,out Vec3i sublocation);
				//Block block = api.World.BlockAccessor.GetBlock(blockpos);
				BlockEntity blockent = api.World.BlockAccessor.GetBlockEntity(blockpos);
				if(blockent == null){continue;} //it's probably unloaded. No need to update it.
				BEBehaviorElectricalNode blockbehavior = blockent.GetBehavior<BEBehaviorElectricalNode>();
				if(blockbehavior == null){continue;} //if it's not a node, don't bother with it.
				blockbehavior.Current = new RealCurrentExport(tran,componentname).Value;
                blockbehavior.Voltage = new RealVoltageExport(tran,PinHelper.GetPositivePin(componentname),PinHelper.GetNegativePin(componentname)).Value;
				
				//sapi.BroadcastMessageToAllGroups(blockbehavior.Voltage + "+"+ blockbehavior.Current,EnumChatType.CommandError);
				blockbehavior.Blockentity.MarkDirty(true);
				//sapi.BroadcastMessageToAllGroups(blockbehavior.Voltage + "+"+ blockbehavior.Current,EnumChatType.CommandError);

			}
		}

		public override void StartServerSide(ICoreServerAPI sapi)
		{
			this.sapi = sapi;

			sapi.Event.SaveGameLoaded += OnSaveGameLoading;
            sapi.Event.GameWorldSave += OnSaveGameSaving;
			
			/*string voltagename = GetNodeNameFromPins("VoltageSource",GetPinNameAtPosition(new BlockPos(10,3,10),new Vec3i(0,0,0)),"0");
			string resistorname = GetNodeNameFromPins("Resistor",GetPinNameAtPosition(new BlockPos(10,3,10),new Vec3i(0,0,0)),GetPinNameAtPosition(new BlockPos(11,3,10),new Vec3i(0,0,0)));
			string diodename = GetNodeNameFromPins("Diode",GetPinNameAtPosition(new BlockPos(11,3,10),new Vec3i(0,0,0)),GetPinNameAtPosition(new BlockPos(12,3,10),new Vec3i(0,0,0)));
			DiodeModel diodemodel = CreateDiodeModel(diodename);
			string diodemodelname = diodemodel.Name;
			string inductorname = GetNodeNameFromPins("Inductor",GetPinNameAtPosition(new BlockPos(12,3,10),new Vec3i(0,0,0)),GetPinNameAtPosition(new BlockPos(13,3,10),new Vec3i(0,0,0)));
			string capacitorname = GetNodeNameFromPins("Capacitor",GetPinNameAtPosition(new BlockPos(13,3,10),new Vec3i(0,0,0)),"0");
			
			//make some basic components with hard-coded locations, you know, for testing.
			componentlist.Add(voltagename, new VoltageSource(voltagename,GetPositivePin(voltagename),GetNegativePin(voltagename),1));
			componentlist.Add(resistorname, new Resistor(resistorname,GetPositivePin(resistorname),GetNegativePin(resistorname),1));
			componentlist.Add(diodename, new Diode(diodename,GetPositivePin(diodename),GetNegativePin(diodename),diodemodelname));
			componentlist.Add(inductorname, new Inductor(inductorname,GetPositivePin(inductorname),GetNegativePin(inductorname),1));
			componentlist.Add(capacitorname, new Capacitor(capacitorname,GetPositivePin(capacitorname),GetNegativePin(capacitorname),1)); */

			sapi.World.RegisterGameTickListener(TickMNA,1000); //tick the MNA every second

			sapi.RegisterCommand("mna","Gets a readout of the leaderlist","",(IServerPlayer splayer, int groupId, CmdArgs args) =>
            {
				string message = "";
				foreach(BlockPos leaderpos in leadermap)
				{
					message = message + leaderpos + "[ ";
					if(!blockmap.ContainsKey(leaderpos))
					{
						sapi.SendMessage(splayer,GlobalConstants.GeneralChatGroup,"No MNA running",EnumChatType.CommandSuccess);
						return;
					}
					BlockPos[] recruiterlist = blockmap[leaderpos].NodeList;
					foreach(BlockPos recruiterpos in recruiterlist)
					{
						message = message + recruiterpos + "  ";
					}
					message = message + "]";
				}
				sapi.SendMessage(splayer,GlobalConstants.GeneralChatGroup,message,EnumChatType.CommandSuccess);
			/*if(args.Length > 0){
			Double.TryParse(args[0],out double dcsetting);
			ckt.TryGetEntity(GetNodeNameFromPins("VoltageSource",GetPinNameAtPosition(new BlockPos(10,3,10),new Vec3i(0,0,0)),"0"), out SpiceSharp.Entities.IEntity component);
			if(!component.TrySetParameter("dc",dcsetting)){throw new NullReferenceException("MNA command couldn't set the parameter: dc");}
			}

			string capacitorname = GetNodeNameFromPins("Capacitor",GetPinNameAtPosition(new BlockPos(13,3,10),new Vec3i(0,0,0)),"0");
			string inductorname = GetNodeNameFromPins("Inductor",GetPinNameAtPosition(new BlockPos(12,3,10),new Vec3i(0,0,0)),GetPinNameAtPosition(new BlockPos(13,3,10),new Vec3i(0,0,0)));
			//double inductorreadout = ICWatchers[inductorname].Value;
			//double capacitorreadout = ICWatchers[capacitorname].Value;
			ckt.TryGetEntity(capacitorname, out SpiceSharp.Entities.IEntity node);
			if(!node.TryGetProperty("ic", out double nodeic)){throw new NullReferenceException("MNA command couldn't get the property: ic");}
			double capacitorreadout = nodeic;
			ckt.TryGetEntity(inductorname, out node);
			if(!node.TryGetProperty("ic", out nodeic)){throw new NullReferenceException("MNA command couldn't get the property: ic");}
			double inductorreadout = nodeic;
			sapi.BroadcastMessageToAllGroups("capacitor: "+capacitorreadout+" inductor: "+inductorreadout,EnumChatType.CommandSuccess);
			*/
			}, Privilege.chat);

			sapi.RegisterCommand("blocklist","Read out the block list","",(IServerPlayer splayer, int groupId, CmdArgs args) =>
            {
				double inputvalue = 0;
				if(args.Length > 0){
				Double.TryParse(args[0],out inputvalue);
				}
				foreach(KeyValuePair<BlockPos,BEBehaviorElectricalNode> entry in blockmap)
				{
					string message = "Block at: " + entry.Key;
					message = message + " Internal hash: " + entry.Value.Blockentity?.GetHashCode().ToString();
					message = message + " External hash: " + api.World.BlockAccessor.GetBlockEntity(entry.Value.Blockentity.Pos)?.GetHashCode().ToString();
					message = message + " Resistance: " + entry.Value.Resistance;
					entry.Value.Resistance = inputvalue;
					sapi.SendMessage(splayer,GlobalConstants.GeneralChatGroup,message,EnumChatType.CommandSuccess);
					entry.Value.Blockentity.MarkDirty(true);
				}
			}, Privilege.chat);


		public void TickMNA(float par)
		{
			if(blockmap.Count < 1){return;}//list is null. Don't start yet.
			if(blockmap.First().Value.NodeList == null){return;}//isn't loaded yet, skip this tick (fucking hate I have to do this)
			foreach(KeyValuePair<BlockPos,BEBehaviorElectricalNode> entry in blockmap)
			{
				UpdateBlockmap(entry.Key);
				if(entry.Value.Blockentity != null){entry.Value.Blockentity.MarkDirty(true);}
			}
			
			foreach(BlockPos leaderpos in leadermap)
			{//build a ckt and then run a tran for each leader
			Transient tran = new Transient("LeaderTran:"+leaderpos,1,1);
				if(TPlist.ContainsKey(tran.Name))
				{//restore TP from the previous tick if it exists
				tran = new Transient("LeaderTran:"+leaderpos,TPlist["LeaderTran:"+leaderpos]);
				}else{
				tran.ExportSimulationData += OnMNAExport; //otherwise register a new event handler
				}
				tran.TimeParameters.UseIc = true;
				tran.TimeParameters.StopTime = 10;
				CreateCircuit(tran, out Circuit ckt);
				ckt.Add(new Sampler("sampler",TimePoints, (sender, exargs) =>
                {tran.TimeParameters.StopTime -= 1; //reduces timeparameters stoptime by 1 per cycle (so it stops at 0)
				}));//add the sampler at the end
				tran.Run(ckt);
			}
		}

		public DiodeModel CreateDiodeModel(string name)
        {
            var diodemodel = new DiodeModel(name + "#diodemodel"); //the parameters of a standard silicon diode
            diodemodel.SetParameter("Is",2.52e-9); //SaturationCurrent
			diodemodel.SetParameter("Rs",0.568); //Resistance
			diodemodel.SetParameter("N",1.752); //emission coefficient
			diodemodel.SetParameter("Cjo",4e-4); //junction capacitance
			diodemodel.SetParameter("M",0.4); //grading coefficient
			diodemodel.SetParameter("tt",20e-9); //transit time
            return diodemodel;
        }

		public static string GetPinNameAtPosition(BlockPos blockpos, Vec3i subblockpos) //Converts blockpos and subblockpos into a pin
		{
			if(blockpos == null || subblockpos == null){return "0";}
			string returnstring = "" + blockpos + " (" + subblockpos.X + ", " + subblockpos.Y + ", "+ subblockpos.Z + ")";
			return returnstring;
		}
		public static string GetNodeNameFromPins(string componenttype, string pinnamepos, string pinnameneg) //Converts a pinpos and a pinneg into a nodename
		{
			string returnstring = componenttype + ":" + pinnamepos + "~" + pinnameneg;
			return returnstring;
		}
		public static string GetNodeTypeFromName(string nodename)
		{
			string returnstring = nodename;
			int stopat = returnstring.IndexOf(":");
			return returnstring.Substring(0, stopat);
		}
		public static string GetPositivePin(string nodename) //Extracts the positive pin from a node's name.
		{
			string returnstring = nodename;
			int startat = returnstring.IndexOf(":") + 1;
			int stopat = returnstring.IndexOf("~");
			int length = stopat-startat;
			return returnstring.Substring(startat, length);
		}
		public static string GetNegativePin(string nodename) //Extracts the negative pin from the node's name.
		{
			string returnstring = nodename;
			int startat = returnstring.IndexOf("~") + 1;
			return returnstring.Substring(startat);
		}
		public static BlockPos PinToBlockPos(string pinname,out Vec3i sublocation) //converts a pin into a blockpos
		{
			string workstring = pinname;
			//the pin name will look like this:
			//10, 3, 10 (0, 0, 0)
			int startat = 0;
			int stopat = workstring.IndexOf(",");
			int length = stopat-startat;
			int x = workstring.Substring(startat,length).ToInt(-666);
			workstring = workstring.Substring(stopat+2); //2 gets rid of the comma and space
			//3, 10 (0, 0, 0)
			stopat = workstring.IndexOf(",");
			length = stopat-startat;
			int y = workstring.Substring(startat,length).ToInt(-666);
			workstring = workstring.Substring(stopat+2); //2 gets rid of the comma and space
			//should be 10 (0, 0, 0)
			stopat = workstring.IndexOf("(")-1;
			length = stopat-startat;
			int z = workstring.Substring(startat,length).ToInt(-666);
			workstring = workstring.Substring(stopat+2); //get rid of the space and )
			//0, 0, 0)
			stopat = workstring.IndexOf(",");
			length = stopat-startat;
			int sex = workstring.Substring(startat,length).ToInt(-666); //sub-x. What the E stands for is as much of a mystery as what the N in ELN stands for.
			workstring = workstring.Substring(stopat+2);
			//should be 0, 0)
			stopat = workstring.IndexOf(",");
			length = stopat-startat;
			int sey = workstring.Substring(startat,length).ToInt(-666);
			workstring = workstring.Substring(stopat+2);
			//should be 0)
			stopat = workstring.IndexOf(")");
			length = stopat-startat;
			int sez = workstring.Substring(startat,length).ToInt(-666);
			workstring = workstring.Substring(stopat+1); //should be empty now

			sublocation = new Vec3i(sex,sey,sez);
			//sapi.BroadcastMessageToAllGroups("x:"+x+"y:"+y+"z:"+z+ ": "+workstring + " subblock:" + sublocation.ToString(),EnumChatType.CommandError);
			//x:11y:3z:10:  subblock:X=1,Y=0,Z=0
			
			return new BlockPos(x,y,z);
		}
		public void AddComponent(Transient tran, Circuit ckt,string nodename,SpiceSharp.Entities.IEntity component)
		{

			//in (paracap) -> pararesistor -> component+ -> component- -> parainduct -> out

			string pos = GetPositivePin(nodename);
			string neg = GetNegativePin(nodename);
			string componenttype = GetNodeTypeFromName(nodename);
			switch(componenttype)
				{
					case "VoltageSource":
					ckt.Add(component as VoltageSource);
					break;


		public void AddComponent(Transient tran, Circuit ckt,string nodename,SpiceSharp.Entities.IEntity component)
        {
            //in (paracap) -> pararesistor -> component+ -> component- -> parainduct -> out
            string pos = PinHelper.GetPositivePin(nodename);
            string neg = PinHelper.GetNegativePin(nodename);
            /*Circuit subcircuit = new Circuit(
            //new Resistor (nodename+"PRP",pos,subpos,0.01),
            //new Capacitor(nodename+"PCP",subpos,"0",0.01),
            //new Inductor (nodename+"PI",subneg,neg,0.01),
            //new Capacitor(nodename+"PCN",neg,"0",0.01)
            );*/
            string componenttype = PinHelper.GetNodeTypeFromName(nodename);
            switch(componenttype)
            {
                case "VoltageSource":
                    ckt.Add(component as VoltageSource);
                    break;

                case "Resistor":
                    ckt.Add(component as Resistor);
                    break;

                case "Inductor":
                    ckt.Add(component as Inductor);
                    break;

                case "Capacitor":
                    ckt.Add(component as Capacitor);
                    break;


                case "Diode":
                    ckt.Add(component as Diode);
                    ckt.Add(CreateDiodeModel(component.Name));
                    break;

					default:
					break;
				}
				//now we check the pos and neg pins to make sure they have unideal components
				//they need to be there to ensure all components are self-sufficient and won't crash
			
			if(!pos.EqualsFast("0"))//prevents the creation of unideal components on earth ground, as they're not needed there
			{
			string subpos = pos + "UNIDEAL";
			if(!ckt.TryGetEntity(GetNodeNameFromPins("Resistor",pos,subpos),out var throwaway)) //no need to add the component if it already exists
			{
				ckt.Add(new Resistor(GetNodeNameFromPins("Resistor",pos,subpos),pos,subpos,0.01)); //add a resistor with 0.01ohm
				ckt.Add(new Resistor(GetNodeNameFromPins("Resistor",pos,"0"),pos,"0",1e12)); //adds a very high resistance to ground
				ckt.Add(new Capacitor(GetNodeNameFromPins("Capacitor",subpos,"0"),subpos,"0",0.01)); //add a capacitor-To-Ground with 0.01 farads
			} 
			}
			if(!neg.EqualsFast("0")){
			string subneg = neg + "UNIDEAL";
			if(!ckt.TryGetEntity(GetNodeNameFromPins("Resistor",neg,subneg),out var throwaway)) //no need to add the component if it already exists
			{
				ckt.Add(new Resistor(GetNodeNameFromPins("Resistor",neg,subneg),neg,subneg,0.01)); //add a resistor with 0.01ohm
				ckt.Add(new Resistor(GetNodeNameFromPins("Resistor",neg,"0"),neg,"0",1e12)); //adds a very high resistance to ground
				ckt.Add(new Capacitor(GetNodeNameFromPins("Capacitor",subneg,"0"),subneg,"0",0.01)); //add a capacitor-To-Ground with 0.01 farads
			}
			}


                default:
                    break;
            }
            //now we check the pos and neg pins to make sure they have unideal components
            //they need to be there to ensure all components are self-sufficient and won't crash

            if(!pos.EqualsFast("0"))//prevents the creation of unideal components on earth ground, as they're not needed there
            {
                string subpos = pos + "UNIDEAL";
                if(!ckt.TryGetEntity(PinHelper.GetNodeNameFromPins("Resistor",pos,subpos),out var throwaway)) //no need to add the component if it already exists
                {
                    ckt.Add(new Resistor(PinHelper.GetNodeNameFromPins("Resistor",pos,subpos),pos,subpos,0.01)); //add a resistor with 0.01ohm
                    ckt.Add(new Capacitor(PinHelper.GetNodeNameFromPins("Capacitor",subpos,"0"),subpos,"0",0.01)); //add a capacitor-To-Ground with 0.01 farads
                }
            }
            if(!neg.EqualsFast("0")){
                string subneg = neg + "UNIDEAL";
                if(!ckt.TryGetEntity(PinHelper.GetNodeNameFromPins("Resistor",neg,subneg),out var throwaway)) //no need to add the component if it already exists
                {
                    ckt.Add(new Resistor(PinHelper.GetNodeNameFromPins("Resistor",neg,subneg),neg,subneg,0.01)); //add a resistor with 0.01ohm
                    ckt.Add(new Capacitor(PinHelper.GetNodeNameFromPins("Capacitor",subneg,"0"),subneg,"0",0.01)); //add a capacitor-To-Ground with 0.01 farads
                }
            }
        }
	}
}