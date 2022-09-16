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
		public Dictionary<BlockPos, BEBehaviorElectricalNode> blockmap = new Dictionary<BlockPos, BEBehaviorElectricalNode>();


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
		public Dictionary<string,Export<IBiasingSimulation, double>> ICWatchers = new Dictionary<string,Export<IBiasingSimulation, double>>();
		public int sampleriters = 0;
		
		
		public void CreateCircuit()
		{
			if(ckt == null){
			ckt = new Circuit
			(new Sampler("sampler",TimePoints, (sender, exargs) =>
                {
						sampleriters++;
						//applying KISS principles here
				})
			);}
			foreach(KeyValuePair<string,SpiceSharp.Entities.IEntity> entry in componentlist)
			{
				string componentname = entry.Key;
				SpiceSharp.Entities.IEntity component = entry.Value;
				string componenttype = GetNodeTypeFromName(componentname);
				if(ckt.TryGetEntity(componentname, out var throwaway)){continue;}//only add to the ckt if it wasn't there before
				AddComponent(componentname, component);
				/**/

			}
		}
		public void OnMNAExport(object sender, ExportDataEventArgs exargs)
		{

			if(sampleriters >= 10)
			{
			tran.TimeParameters.StopTime = 0; //stop the simulation
			MNAFinish(); //Tell the MNA to upload the information from this tick into the next so that it can pass on.
			sampleriters = 0;
			}
		}
		public void MNAFinish() //finish the tick then shut down
		{
			foreach(KeyValuePair<string,Export<IBiasingSimulation, double>> entry in ICWatchers)
			{
				string nodename = entry.Key;
				double readout = entry.Value.Value; //awkwaaard
				ckt.TryGetEntity(nodename, out SpiceSharp.Entities.IEntity node);
				node.SetParameter("ic",ICWatchers[nodename].Value);
			}
			castMNAtoblocks(); //sends data from the MNA into the blockents
		}
		public void castMNAtoblocks()
		{
			foreach(KeyValuePair<string,SpiceSharp.Entities.IEntity> entry in componentlist)
			{
				SpiceSharp.Entities.IEntity component = entry.Value;
				string componentname = component.Name;
				string componentlocation = GetPositivePin(componentname);
				BlockPos blockpos = PinToBlockPos(componentlocation,out Vec3i sublocation);
				//Block block = api.World.BlockAccessor.GetBlock(blockpos);
				BlockEntity blockent = api.World.BlockAccessor.GetBlockEntity(blockpos);
				if(blockent == null){continue;} //it's probably unloaded. No need to update it.
				BEBehaviorElectricalNode blockbehavior = blockent.GetBehavior<BEBehaviorElectricalNode>();
				if(blockbehavior == null){continue;} //if it's not a node, don't bother with it.
				blockbehavior.Current = new RealCurrentExport(tran,componentname).Value;
				blockbehavior.Voltage = new RealVoltageExport(tran,GetPositivePin(componentname),GetNegativePin(componentname)).Value;
				
				//sapi.BroadcastMessageToAllGroups(blockbehavior.Voltage + "+"+ blockbehavior.Current,EnumChatType.CommandError);
				blockbehavior.Blockentity.MarkDirty(true);
				//sapi.BroadcastMessageToAllGroups(blockbehavior.Voltage + "+"+ blockbehavior.Current,EnumChatType.CommandError);

			}
		}
		public override void StartServerSide(ICoreServerAPI sapi)
		{
			this.sapi = sapi;
			tran.ExportSimulationData += OnMNAExport;
			
			string voltagename = GetNodeNameFromPins("VoltageSource",GetPinNameAtPosition(new BlockPos(10,3,10),new Vec3i(0,0,0)),"0");
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
			componentlist.Add(capacitorname, new Capacitor(capacitorname,GetPositivePin(capacitorname),GetNegativePin(capacitorname),1));

			sapi.World.RegisterGameTickListener(TickMNA,1000); //tick the MNA every second

			sapi.RegisterCommand("mna","Test the MNA","",(IServerPlayer splayer, int groupId, CmdArgs args) =>
            {
			if(args.Length > 0){
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


			//try and save this MNA to the world? What could go wrong.
			//sapi.WorldManager.SaveGame.StoreData("ckt",SerializerUtil.Serialize<Circuit>(ckt));
			//sapi.WorldManager.SaveGame.StoreData("tran",SerializerUtil.Serialize<Transient>(tran));
			//everything.

			}, Privilege.chat);
			sapi.RegisterCommand("yeet","Try to delete a component and see what happens","",(IServerPlayer splayer, int groupId, CmdArgs args) =>
            {
				ckt.Remove(resistorname);
			}, Privilege.chat);

			sapi.RegisterCommand("here","Where am I? answered in text form","",(IServerPlayer splayer, int groupId, CmdArgs args) =>
            {
				BlockPos blockpos = splayer.Entity.Pos.AsBlockPos;
				Vec3i subblockpos = new Vec3i(0,0,0);

				string message = GetPinNameAtPosition(blockpos,subblockpos);
				string nodename = GetNodeNameFromPins("VoltageSource",GetPinNameAtPosition(blockpos,subblockpos),"0");
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
			CreateCircuit();
			tran.TimeParameters.UseIc = true; //ESSENTIAL!!
			tran.TimeParameters.StopTime = 1; // run for this amount of cycles
            tran.Run(ckt);

			//if(cktdata != null){ckt = SerializerUtil.Deserialize<Circuit>(sapi.WorldManager.SaveGame.GetData("ckt"));}
			//if(trandata != null){tran = SerializerUtil.Deserialize<Transient>(sapi.WorldManager.SaveGame.GetData("tran"));}
			//if(cktdata != null){cktdata = SerializerUtil.Serialize<Circuit>(ckt); sapi.WorldManager.SaveGame.StoreData("ckt",cktdata);}
			//if(trandata != null){trandata = SerializerUtil.Serialize<Transient>(tran);sapi.WorldManager.SaveGame.StoreData("tran",trandata);}
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
		public BlockPos PinToBlockPos(string pinname,out Vec3i sublocation) //converts a pin into a blockpos
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
		public void AddComponent(string nodename,SpiceSharp.Entities.IEntity component)
		{

			//in (paracap) -> pararesistor -> component+ -> component- -> parainduct -> out

			string pos = GetPositivePin(nodename);
			string neg = GetNegativePin(nodename);
			/*Circuit subcircuit = new Circuit(
			//new Resistor (nodename+"PRP",pos,subpos,0.01),
			//new Capacitor(nodename+"PCP",subpos,"0",0.01),
			//new Inductor (nodename+"PI",subneg,neg,0.01),
			//new Capacitor(nodename+"PCN",neg,"0",0.01)
			);*/
			string componenttype = GetNodeTypeFromName(nodename);
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
					ICWatchers.Add(nodename,new RealCurrentExport(tran,nodename)); //lets us get current later
					break;

					case "Capacitor":
					ckt.Add(component as Capacitor);
					ICWatchers.Add(nodename,new RealVoltageExport(tran,GetPositivePin(nodename),GetNegativePin(nodename))); //lets us get voltage later
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
				ckt.Add(new Capacitor(GetNodeNameFromPins("Capacitor",subpos,"0"),subpos,"0",0.01)); //add a capacitor-To-Ground with 0.01 farads
			} 
			}
			if(!neg.EqualsFast("0")){
			string subneg = neg + "UNIDEAL";
			if(!ckt.TryGetEntity(GetNodeNameFromPins("Resistor",neg,subneg),out var throwaway)) //no need to add the component if it already exists
			{
				ckt.Add(new Resistor(GetNodeNameFromPins("Resistor",neg,subneg),neg,subneg,0.01)); //add a resistor with 0.01ohm
				ckt.Add(new Capacitor(GetNodeNameFromPins("Capacitor",subneg,"0"),subneg,"0",0.01)); //add a capacitor-To-Ground with 0.01 farads
			}
			}


		}
		public string Addifnot(string inputstring, string addthis, string ifnotthis)
		{
			string outputstring = inputstring;
			if(!inputstring.EqualsFast(ifnotthis))
			{
				inputstring = inputstring + addthis;
			}
			return outputstring;
		}
	}
}