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
		public override void StartClientSide(ICoreClientAPI capi){this.capi = capi;}
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
		public Circuit ckt = null;
		//public RealVoltageExport inputExport = null;
		//public RealVoltageExport outputExport = null;
		//public RealCurrentExport currentexport = null;
		public Transient tran = new Transient("tran", 1, 1);
		public Dictionary<string,SpiceSharp.Entities.IEntity> componentlist = new Dictionary<string,SpiceSharp.Entities.IEntity>();
		public Dictionary<string,Export<IBiasingSimulation, double>> ICwatchers = new Dictionary<string,Export<IBiasingSimulation, double>>();
		public int sampleriters = 0;
		
		public void CreateCircuit()
		{
			if(ckt == null){
			ckt = new Circuit
			(new Sampler("sampler",TimePoints, (sender, exargs) =>
                {
						sampleriters++;
						//double dcsetting = ICwatchers[GetNodeNameFromPins("VoltageSource",GetPinNameAtPosition(new BlockPos(10,3,10),new Vec3i(0,0,0)),"0")].Value;
						/*double dcsetting = 100; //for now, hard code it to 100
						double inductorcurrent = ICwatchers[GetNodeNameFromPins("Inductor",GetPinNameAtPosition(new BlockPos(10,3,10),new Vec3i(1,0,0)),GetPinNameAtPosition(new BlockPos(11,3,10),new Vec3i(0,0,0)))].Value;
						double capacitorvoltage = ICwatchers[GetNodeNameFromPins("Capacitor",GetPinNameAtPosition(new BlockPos(11,3,10),new Vec3i(0,0,0)),"0")].Value;
						sapi.BroadcastMessageToAllGroups("DC setting:" + dcsetting,EnumChatType.Notification);
						sapi.BroadcastMessageToAllGroups("inductor:" + inductorcurrent + " capacitor:" + capacitorvoltage,EnumChatType.Notification);*/
						//discontinuing sampler readouts. Get your data from Export event.
				})
			);}
			foreach(KeyValuePair<string,SpiceSharp.Entities.IEntity> entry in componentlist)
			{
				string componentname = entry.Key;
				SpiceSharp.Entities.IEntity component = entry.Value;
				string componenttype = GetNodeTypeFromName(componentname);
				if(ckt.TryGetEntity(componentname, out var throwaway)){break;}//only add to the ckt if it wasn't there before
				switch(componenttype)
				{
					case "VoltageSource":
					//ckt.Add(new VoltageSource(componentname,GetPositivePin(componentname),GetNegativePin(componentname),cvalue));
					ckt.Add(component as VoltageSource);
					break;

					case "Resistor":
					ckt.Add(component as Resistor);
					break;

					case "Inductor":
					ckt.Add(component as Inductor);
					ICwatchers.Add(componentname,new RealCurrentExport(tran,componentname)); //lets us get current later
					break;

					case "Capacitor":
					ckt.Add(component as Capacitor);
					ICwatchers.Add(componentname,new RealVoltageExport(tran,GetPositivePin(componentname))); //lets us get voltage later
					break;

					default:
					break;
				}

			}
		}
		public void OnMNAExport(object sender, ExportDataEventArgs exargs)
		{

			if(sampleriters >= 10)
			{
			string capacitorname = GetNodeNameFromPins("Capacitor",GetPinNameAtPosition(new BlockPos(11,3,10),new Vec3i(0,0,0)),"0");
			string inductorname = GetNodeNameFromPins("Inductor",GetPinNameAtPosition(new BlockPos(10,3,10),new Vec3i(1,0,0)),GetPinNameAtPosition(new BlockPos(11,3,10),new Vec3i(0,0,0)));
			double capacitorreadout = new RealVoltageExport(tran,GetPositivePin(capacitorname)).Value;
			double inductorreadout = new RealCurrentExport(tran,inductorname).Value;
			ckt.TryGetEntity(capacitorname, out SpiceSharp.Entities.IEntity capacitor);
			ckt.TryGetEntity(inductorname, out SpiceSharp.Entities.IEntity inductor);
			capacitor.SetParameter("ic",capacitorreadout);
			inductor.SetParameter("ic",inductorreadout);

			
			sapi.BroadcastMessageToAllGroups("capacitor: "+capacitorreadout+" inductor: "+inductorreadout,EnumChatType.CommandSuccess);
			tran.TimeParameters.StopTime = 0; //stop the simulation
			sampleriters = 0;
			}
		}
		public override void StartServerSide(ICoreServerAPI sapi)
		{
			this.sapi = sapi;
			tran.ExportSimulationData += OnMNAExport;
			
			string voltagename = GetNodeNameFromPins("VoltageSource",GetPinNameAtPosition(new BlockPos(10,3,10),new Vec3i(0,0,0)),"0");
			string resistorname = GetNodeNameFromPins("Resistor",GetPinNameAtPosition(new BlockPos(10,3,10),new Vec3i(0,0,0)),GetPinNameAtPosition(new BlockPos(10,3,10),new Vec3i(1,0,0)));
			string inductorname = GetNodeNameFromPins("Inductor",GetPinNameAtPosition(new BlockPos(10,3,10),new Vec3i(1,0,0)),GetPinNameAtPosition(new BlockPos(11,3,10),new Vec3i(0,0,0)));
			string capacitorname = GetNodeNameFromPins("Capacitor",GetPinNameAtPosition(new BlockPos(11,3,10),new Vec3i(0,0,0)),"0");
			//make some basic components, you know, for testing.
			componentlist.Add(voltagename, new VoltageSource(voltagename,GetPositivePin(voltagename),GetNegativePin(voltagename),1));
			componentlist.Add(resistorname, new Resistor(resistorname,GetPositivePin(resistorname),GetNegativePin(resistorname),1));
			componentlist.Add(inductorname, new Inductor(inductorname,GetPositivePin(inductorname),GetNegativePin(inductorname),1));
			componentlist.Add(capacitorname, new Capacitor(capacitorname,GetPositivePin(capacitorname),GetNegativePin(capacitorname),1));

			sapi.World.RegisterGameTickListener(TickMNA,1000); //tick the MNA every second

			sapi.RegisterCommand("mna","Test the MNA","",(IServerPlayer splayer, int groupId, CmdArgs args) =>
            {
			if(args.Length > 0){
			Double.TryParse(args[0],out double dcsetting);
			ckt.TryGetEntity(GetNodeNameFromPins("VoltageSource",GetPinNameAtPosition(new BlockPos(10,3,10),new Vec3i(0,0,0)),"0"), out SpiceSharp.Entities.IEntity component);
			if(!component.TrySetParameter("dc",dcsetting)){throw new NullReferenceException("MNA command couldn't set the parameter: dc");}
			}


			//try and save this MNA to the world? What could go wrong.
			//sapi.WorldManager.SaveGame.StoreData("ckt",SerializerUtil.Serialize<Circuit>(ckt));
			//sapi.WorldManager.SaveGame.StoreData("tran",SerializerUtil.Serialize<Transient>(tran));
			//everything.

			}, Privilege.chat);
			sapi.RegisterCommand("here","Where am I? answered in text form","",(IServerPlayer splayer, int groupId, CmdArgs args) =>
            {
				BlockPos blockpos = splayer.Entity.Pos.AsBlockPos;
				Vec3i subblockpos = new Vec3i(0,0,0);

				string message = GetPinNameAtPosition(blockpos,subblockpos);
				string nodename = GetNodeNameFromPins("VoltageSource",GetPinNameAtPosition(blockpos,subblockpos),GetPinNameAtPosition(blockpos,subblockpos.AddCopy(1,0,0)));
				string nodetype = GetNodeTypeFromName(nodename);
				string pospinname = GetPositivePin(nodename);
				string negpinname = GetNegativePin(nodename);
				splayer.SendMessage(GlobalConstants.GeneralChatGroup,message,EnumChatType.CommandSuccess);
				splayer.SendMessage(GlobalConstants.GeneralChatGroup,nodename,EnumChatType.CommandSuccess);
				splayer.SendMessage(GlobalConstants.GeneralChatGroup,nodetype,EnumChatType.CommandSuccess);
				splayer.SendMessage(GlobalConstants.GeneralChatGroup,pospinname,EnumChatType.CommandSuccess);
				splayer.SendMessage(GlobalConstants.GeneralChatGroup,negpinname,EnumChatType.CommandSuccess);
			}, Privilege.chat);
		}
		public void TickMNA(float par)
		{
			/*ckt.TryGetEntity(voltagenodename, out var voltagenode); voltagenode.SetParameter("dc",voltagesetting);
			ckt.TryGetEntity(capacitornodename, out var capacitornode); capacitornode.SetParameter("ic",capacitorvoltage);
			ckt.TryGetEntity(inductornodename, out var inductornode); inductornode.SetParameter("ic",inductorcurrent); */
			CreateCircuit();
			tran.TimeParameters.UseIc = true; //ESSENTIAL!!
			tran.TimeParameters.StopTime = 1; // run for this amount of cycles
            tran.Run(ckt);

			//if(cktdata != null){ckt = SerializerUtil.Deserialize<Circuit>(sapi.WorldManager.SaveGame.GetData("ckt"));}
			//if(trandata != null){tran = SerializerUtil.Deserialize<Transient>(sapi.WorldManager.SaveGame.GetData("tran"));}
			//if(cktdata != null){cktdata = SerializerUtil.Serialize<Circuit>(ckt); sapi.WorldManager.SaveGame.StoreData("ckt",cktdata);}
			//if(trandata != null){trandata = SerializerUtil.Serialize<Transient>(tran);sapi.WorldManager.SaveGame.StoreData("tran",trandata);}
		}
		public string GetPinNameAtPosition(BlockPos blockpos, Vec3i subblockpos) //Converts blockpos and subblockpos into a pin
		{
			if(blockpos == null || subblockpos == null){return "0";}
			string returnstring = "" + blockpos + " (" + subblockpos.X + ", " + subblockpos.Y + ", "+ subblockpos.Z + ")";
			return returnstring;
		}
		public string GetNodeNameFromPins(string componenttype, string pinnamepos, string pinnameneg) //Converts a pinpos and a pinneg into a nodename
		{
			string returnstring = componenttype + ":" + pinnamepos + "~" + pinnameneg;
			return returnstring;
		}
		public string GetNodeTypeFromName(string nodename)
		{
			string returnstring = nodename;
			int stopat = returnstring.IndexOf(":");
			return returnstring.Substring(0, stopat);
		}
		public string GetPositivePin(string nodename) //Extracts the positive pin from a node's name.
		{
			string returnstring = nodename;
			int startat = returnstring.IndexOf(":") + 1;
			int stopat = returnstring.IndexOf("~");
			int length = stopat-startat;
			return returnstring.Substring(startat, length);
		}
		public string GetNegativePin(string nodename) //Extracts the negative pin from the node's name.
		{
			string returnstring = nodename;
			int startat = returnstring.IndexOf("~") + 1;
			return returnstring.Substring(startat);
		}
	}
}