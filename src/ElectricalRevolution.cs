﻿using System;
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
		public int sampleriters = 0;
		public int inter = 0;
		//public double voltagesetting = 0; //todo: save to world
		//public double capacitorvoltage = 0; //todo: save to world
		//public double inductorcurrent = 0; //todo: save to world
		public Transient tran = new Transient("tran", 1, 1);
		public Dictionary<string,double> componentlist = new Dictionary<string,double>();
		public Dictionary<string,double> componentvalues = new Dictionary<string,double>();
		public Dictionary<string,RealVoltageExport> voltagewatchers = new Dictionary<string,RealVoltageExport>();
		public Dictionary<string,RealCurrentExport> currentwatchers = new Dictionary<string,RealCurrentExport>();
		
		public void CreateCircuit()
		{
			if(ckt == null){
			ckt = new Circuit
			(new Sampler("sampler",TimePoints, (sender, exargs) =>
                {
					sampleriters++;
					if(sampleriters >= 10)
					{
						double dcsetting = componentvalues[GetNodeNameFromPins("VoltageSource",GetPinNameAtPosition(new BlockPos(10,3,10),new Vec3i(0,0,0)),"0")];
						double inductorcurrent = componentvalues[GetNodeNameFromPins("Inductor",GetPinNameAtPosition(new BlockPos(10,3,10),new Vec3i(1,0,0)),GetPinNameAtPosition(new BlockPos(11,3,10),new Vec3i(0,0,0)))];
						double capacitorvoltage = componentvalues[GetNodeNameFromPins("Capacitor",GetPinNameAtPosition(new BlockPos(11,3,10),new Vec3i(0,0,0)),"0")];
						sapi.BroadcastMessageToAllGroups("DC setting:" + dcsetting + " ticked " + sampleriters,EnumChatType.Notification);
						sapi.BroadcastMessageToAllGroups("inductor:" + inductorcurrent + " capacitor:" + capacitorvoltage,EnumChatType.Notification);
						sampleriters = 0;
					}
				})
			);}
			foreach(KeyValuePair<string,double> entry in componentlist)
			{
				string componentname = entry.Key;
				double cvalue = entry.Value;
				string componenttype = GetNodeTypeFromName(componentname);
				if(ckt.TryGetEntity(componentname, out var throwaway)){return;}//only add to the ckt if it wasn't there before
				switch(componenttype)
				{
					case "VoltageSource":
					ckt.Add(new VoltageSource(componentname,GetPositivePin(componentname),GetNegativePin(componentname),cvalue));
					break;

					case "Resistor":
					ckt.Add(new Resistor(componentname,GetPositivePin(componentname),GetNegativePin(componentname),cvalue));
					break;

					case "Inductor":
					ckt.Add(new Inductor(componentname,GetPositivePin(componentname),GetNegativePin(componentname),cvalue));
					break;

					case "Capacitor":
					ckt.Add(new Capacitor(componentname,GetPositivePin(componentname),GetNegativePin(componentname),cvalue));
					break;

					default:
					break;
				}
				voltagewatchers.Add(componentname,new RealVoltageExport(tran,componentname)); //lets us get voltage later
				currentwatchers.Add(componentname,new RealCurrentExport(tran,componentname)); //lets us get current later
				componentvalues.Add(componentname,0); //initializes the componentvalue

			}
		}
		public void UpdateCircuit() //from the values stored in the dictionary
		{
			foreach(KeyValuePair<string,double> values in componentvalues)
			{
				string nodename = values.Key;
				double nodevalue = values.Value;
				string nodetype = GetNodeTypeFromName(nodename);
				ckt.TryGetEntity(nodename, out var node);
				switch(nodetype)
				{
					case "VoltageSource":
					node.SetParameter("dc",nodevalue);
					break;
					case "Inductor":
					node.SetParameter("ic",nodevalue);
					break;
					case "Capacitor":
					node.SetParameter("ic",nodevalue);
					break;

					default:
					break;
				}
				
			}
		}
		public void OnMNAExport(object sender, ExportDataEventArgs exargs){
			//sapi.BroadcastMessageToAllGroups("in:"+input + " out:" + output + " current" + currentexport.Value + " iter:" + inter,EnumChatType.Notification);
			//ckt.TryGetEntity(voltagenodename, out var voltagenode); voltagenode.SetParameter("dc",voltagesetting);
			//tran.TimeParameters.StopTime -= 1;
			inter++;
			if(sampleriters >= 9)
			{
				inter = 0;
				tran.TimeParameters.StopTime = 0;
				foreach (KeyValuePair<string,double> values in componentlist) //we're working on componentvalues. Since they share keys, this works
				{
					string nodename = values.Key;
					switch(GetNodeTypeFromName(nodename))
					{
						case "Inductor" : //get the current within an inductor
						componentvalues[nodename] = currentwatchers[nodename].Value;
						break;

						case "Capacitor" : //get the voltage within a capacitor
						componentvalues[nodename] = voltagewatchers[nodename].Value;
						break;

						case "VoltageSource" : //get the voltage DC on a VoltageSource
						//componentvalues[nodename] = new RealPropertyExport(tran,nodename,"dc").Value;
						break;

						default:
						break;
					}
				}
				//inductorcurrent = currentexport.Value;
				//capacitorvoltage = outputExport.Value;
			}
		}
		public override void StartServerSide(ICoreServerAPI sapi)
		{
			this.sapi = sapi;

			tran.TimeParameters.UseIc = true; //ESSENTIAL!!
			tran.ExportSimulationData += OnMNAExport;
			
			//make some basic components, you know, for testing.
			componentlist.Add(GetNodeNameFromPins("VoltageSource",GetPinNameAtPosition(new BlockPos(10,3,10),new Vec3i(0,0,0)),"0"),0);
			componentlist.Add(GetNodeNameFromPins("Resistor",GetPinNameAtPosition(new BlockPos(10,3,10),new Vec3i(0,0,0)),GetPinNameAtPosition(new BlockPos(10,3,10),new Vec3i(1,0,0))),1);
			componentlist.Add(GetNodeNameFromPins("Inductor",GetPinNameAtPosition(new BlockPos(10,3,10),new Vec3i(1,0,0)),GetPinNameAtPosition(new BlockPos(11,3,10),new Vec3i(0,0,0))),1);
			componentlist.Add(GetNodeNameFromPins("Capacitor",GetPinNameAtPosition(new BlockPos(11,3,10),new Vec3i(0,0,0)),"0"),1);
    
			// Make the exports
			/*var inductorprop = new RealPropertyExport(tran,inductornodename,"current");
			inputExport = new RealVoltageExport(tran, GetPositivePin(voltagenodename));
			outputExport = new RealVoltageExport(tran, GetPositivePin(capacitornodename));
			currentexport = new RealCurrentExport(tran,inductornodename); */

			sapi.World.RegisterGameTickListener(TickMNA,1000); //tick the MNA every second

			sapi.RegisterCommand("mna","Test the MNA","",(IServerPlayer splayer, int groupId, CmdArgs args) =>
            {
			if(args.Length > 0){
			Double.TryParse(args[0],out double dcsetting);
			componentvalues[GetNodeNameFromPins("VoltageSource",GetPinNameAtPosition(new BlockPos(10,3,10),new Vec3i(0,0,0)),"0")] = dcsetting;
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
			//sapi.BroadcastMessageToAllGroups("",EnumChatType.CommandSuccess);
			
			/*ckt.TryGetEntity(voltagenodename, out var voltagenode); voltagenode.SetParameter("dc",voltagesetting);
			ckt.TryGetEntity(capacitornodename, out var capacitornode); capacitornode.SetParameter("ic",capacitorvoltage);
			ckt.TryGetEntity(inductornodename, out var inductornode); inductornode.SetParameter("ic",inductorcurrent); */
			CreateCircuit();
			UpdateCircuit();
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