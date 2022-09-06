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

			int sampleriters = 0;
			RealVoltageExport inputExport = null;
			RealVoltageExport outputExport = null;
			RealCurrentExport currentexport = null;
			VoltageSource voltagein = null;
			Resistor resistor = null;
			Inductor inductor = null;
			Capacitor capacitor = null;
			float voltagesetting = 0;
			double inductorcurrent = 0;
			double capacitorvoltage = 0;

			var ckt = new Circuit(
   			 voltagein = new VoltageSource("V1", "in", "0",0),
   			 resistor = new Resistor("R1", "in", "-R1", 1),
			 inductor = new Inductor("I1", "-R1", "out", 1),
   			 capacitor = new Capacitor("C1", "out", "0", 1),
			 new Sampler("sampler1",TimePoints, (sender, exargs) =>
                {
					sampleriters++;
					inductorcurrent = currentexport.Value;
					capacitorvoltage = outputExport.Value;
					//sapi.BroadcastMessageToAllGroups("value:" + outputExport.Value + " ticked " + sampleriters,EnumChatType.Notification);
					sapi.BroadcastMessageToAllGroups("inductor:" + inductorcurrent + " capacitor:" + capacitorvoltage,EnumChatType.Notification);

					if(sampleriters >= 10)
					{
						sampleriters = 0;
					}
				})
			);
			IEnumerator<double> refPoints = TimePoints.GetEnumerator();
            var tran = new Transient("tran", 1, 1);

			// Make the exports
			var inductorprop = new RealPropertyExport(tran,"I1","current");
			inputExport = new RealVoltageExport(tran, "in");
			outputExport = new RealVoltageExport(tran, "out");
			currentexport = new RealCurrentExport(tran,"I1");
			
			int inter = 0;
			tran.ExportSimulationData += (sender, exargs) =>
			{
  			double input = inputExport.Value;
   			double output = outputExport.Value;
			sapi.BroadcastMessageToAllGroups("in:"+input + " out:" + output + " current" + currentexport.Value + " iter:" + inter,EnumChatType.Notification);
			inter++;
			voltagein.Parameters.DcValue = voltagesetting;
			tran.TimeParameters.StopTime -= 1;
			if(sampleriters >= 9)
			{
				inter = 0;
				voltagein.Parameters.DcValue = 0;
				tran.TimeParameters.StopTime = 0;
			}
			};

			sapi.RegisterCommand("mna","Test the MNA","",(IServerPlayer splayer, int groupId, CmdArgs args) =>
            {
			
			tran.TimeParameters.StopTime = 100;
			if(args.Length > 0){Single.TryParse(args[0],out voltagesetting);}
			tran.TimeParameters.UseIc = true; //ESSENTIAL!!
			voltagein.Parameters.DcValue = voltagesetting;
			capacitor.Parameters.InitialCondition = capacitorvoltage;
			inductor.Parameters.InitialCondition = inductorcurrent;
            tran.Run(ckt);

			}, Privilege.chat);
		}
	}
}