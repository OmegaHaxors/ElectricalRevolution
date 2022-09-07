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
		private IEnumerable<double> TimePoints
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
		public override void StartServerSide(ICoreServerAPI sapi)
		{
			this.sapi = sapi;

			RealVoltageExport inputExport = null;
			RealVoltageExport outputExport = null;
			RealCurrentExport currentexport = null;
			//VoltageSource voltagein = null;
			/*Resistor resistor = null;
			Inductor inductor = null;
			Capacitor capacitor = null; */

			int sampleriters = 0;
			string voltagenodename = GetNodeNameFromPins(GetPinNameAtPosition(new BlockPos(10,3,10),new Vec3i(0,0,0)),"0");
			string resistornodename = GetNodeNameFromPins(GetPinNameAtPosition(new BlockPos(10,3,10),new Vec3i(0,0,0)),GetPinNameAtPosition(new BlockPos(10,3,10),new Vec3i(1,0,0)));
			string inductornodename = GetNodeNameFromPins(GetPinNameAtPosition(new BlockPos(10,3,10),new Vec3i(1,0,0)),GetPinNameAtPosition(new BlockPos(11,3,10),new Vec3i(0,0,0)));
			string capacitornodename = GetNodeNameFromPins(GetPinNameAtPosition(new BlockPos(11,3,10),new Vec3i(0,0,0)),"0");
			double voltagesetting = 0; //todo: save to world
			double capacitorvoltage = 0; //todo: save to world
			double inductorcurrent = 0; //todo: save to world

			var ckt = new Circuit(
   			 new VoltageSource(voltagenodename,GetPositivePin(voltagenodename),GetNegativePin(voltagenodename),0),
   			 new Resistor(resistornodename,GetPositivePin(resistornodename),GetNegativePin(resistornodename),1),
			 new Inductor(inductornodename,GetPositivePin(inductornodename),GetNegativePin(inductornodename),1),
   			 new Capacitor(capacitornodename,GetPositivePin(capacitornodename),GetNegativePin(capacitornodename),1),
			 new Sampler("sampler",TimePoints, (sender, exargs) =>
                {
					inductorcurrent = currentexport.Value;
					capacitorvoltage = outputExport.Value;
					sampleriters++;
					if(sampleriters >= 10)
					{
						sapi.BroadcastMessageToAllGroups("value:" + outputExport.Value + " ticked " + sampleriters,EnumChatType.Notification);
						sapi.BroadcastMessageToAllGroups("inductor:" + inductorcurrent + " capacitor:" + capacitorvoltage,EnumChatType.Notification);
						sampleriters = 0;
					}
				})
			);
			IEnumerator<double> refPoints = TimePoints.GetEnumerator();
            var tran = new Transient("tran", 1, 1);

			// Make the exports
			var inductorprop = new RealPropertyExport(tran,inductornodename,"current");
			inputExport = new RealVoltageExport(tran, GetPositivePin(voltagenodename));
			outputExport = new RealVoltageExport(tran, GetPositivePin(capacitornodename));
			currentexport = new RealCurrentExport(tran,inductornodename);
			int inter = 0;

			tran.ExportSimulationData += (sender, exargs) =>
			{
  			double input = inputExport.Value;
   			double output = outputExport.Value;
			//sapi.BroadcastMessageToAllGroups("in:"+input + " out:" + output + " current" + currentexport.Value + " iter:" + inter,EnumChatType.Notification);
			inter++;
			//ckt.TryGetEntity(voltagenodename, out var voltagenode); voltagenode.SetParameter("dc",voltagesetting);
			tran.TimeParameters.StopTime -= 1;
			if(sampleriters >= 9)
			{
				inter = 0;
				tran.TimeParameters.StopTime = 0;
			}
			};

			sapi.World.RegisterGameTickListener(TickMNA,1000); //tick the MNA every second

			sapi.RegisterCommand("mna","Test the MNA","",(IServerPlayer splayer, int groupId, CmdArgs args) =>
            {
			
			tran.TimeParameters.StopTime = 100;
			if(args.Length > 0){Double.TryParse(args[0],out voltagesetting);}
			tran.TimeParameters.UseIc = true; //ESSENTIAL!!
			ckt.TryGetEntity(voltagenodename, out var voltagenode); voltagenode.SetParameter("dc",voltagesetting);
			ckt.TryGetEntity(capacitornodename, out var capacitornode); capacitornode.SetParameter("ic",capacitorvoltage);
			ckt.TryGetEntity(inductornodename, out var inductornode); inductornode.SetParameter("ic",inductorcurrent);
            tran.Run(ckt);

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
				string nodename = GetNodeNameFromPins(GetPinNameAtPosition(blockpos,subblockpos),GetPinNameAtPosition(blockpos,subblockpos.AddCopy(1,0,0)));
				string pospinname = GetPositivePin(nodename);
				string negpinname = GetNegativePin(nodename);
				splayer.SendMessage(GlobalConstants.GeneralChatGroup,message,EnumChatType.CommandSuccess);
				splayer.SendMessage(GlobalConstants.GeneralChatGroup,nodename,EnumChatType.CommandSuccess);
				splayer.SendMessage(GlobalConstants.GeneralChatGroup,pospinname,EnumChatType.CommandSuccess);
				splayer.SendMessage(GlobalConstants.GeneralChatGroup,negpinname,EnumChatType.CommandSuccess);
			}, Privilege.chat);
		}
		public void TickMNA(float par) //NYI
		{

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
		public string GetNodeNameFromPins(string pinnamepos, string pinnameneg) //Converts a pinpos and a pinneg into a nodename
		{
			string returnstring = pinnamepos + "~" + pinnameneg;
			return returnstring;
		}
		public string GetPositivePin(string nodename) //Extracts the positive pin from a node's name.
		{
			string returnstring = nodename;
			int stopat = returnstring.IndexOf("~");
			return returnstring.Substring(0, stopat);
		}
		public string GetNegativePin(string nodename) //Extracts the negative pin from the node's name.
		{
			string returnstring = nodename;
			int startat = returnstring.IndexOf("~") + 1;
			return returnstring.Substring(startat);
		}
	}
}