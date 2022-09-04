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
                    time += 1;
                }    
            }
        }
		public override void StartServerSide(ICoreServerAPI sapi)
		{
			this.sapi = sapi;
			sapi.RegisterCommand("mna","Test the MNA","",(IServerPlayer splayer, int groupId, CmdArgs args) =>
            {
			// Build the circuit
            var ckt = new Circuit(
   			 new VoltageSource("V1", "in", "0",50),
   			 new Resistor("R1", "in", "-R1", 10000),
			 new Inductor("I1", "-R1", "out", 100),
   			 new Capacitor("C1", "out", "0", 100),
			 new Sampler("sampler1",TimePoints)
			);

			IEnumerator<double> refPoints = TimePoints.GetEnumerator();
            var tran = new Transient("tran", 1, 1);
			tran.TimeParameters.StopTime = 10;
			// Make the exports
			var inputExport = new RealVoltageExport(tran, "in");
			var outputExport = new RealVoltageExport(tran, "out");
			int inter = 0;
			// Simulate
			tran.ExportSimulationData += (sender, exargs) =>
			{
  			var input = inputExport.Value;
   			var output = outputExport.Value;
			splayer.SendMessage(GlobalConstants.GeneralChatGroup,"in:"+input + " out:" + output + " iter:" + inter,EnumChatType.Notification);
			inter++;
			};

            tran.Run(ckt);

			}, Privilege.chat);
		}
	}
}