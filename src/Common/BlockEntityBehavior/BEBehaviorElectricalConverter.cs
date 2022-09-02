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

namespace ElectricalRevolution
{
	public class BEBehaviorElectricalConverter : BEBehaviorElectricalNode
	{
		public float Powerconverted;
		public float Efficiency;
		public BEBehaviorElectricalConverter(BlockEntity blockentity) : base(blockentity){}
		public override void Initialize(ICoreAPI api, JsonObject properties)
		{
			Powerconverted = 0; Efficiency = 1;
			base.Initialize(api, properties);
		}
		public override void GetBlockInfo(IPlayer forPlayer, StringBuilder sb)
		{
			base.GetBlockInfo(forPlayer, sb);
			sb.AppendLine(string.Format(Lang.Get("Power Converted: {0}", new object[]
			{
				Powerconverted
			}), Array.Empty<object>()));
		}
		public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor world)
		{
			this.Powerconverted = tree.GetFloat("powerconverted", 0);
			base.FromTreeAttributes(tree, world);
		}
		public override void ToTreeAttributes(ITreeAttribute tree)
		{
			tree.SetFloat("powerconverted", this.Powerconverted);
			base.Voltage = Powerconverted / 100f;
			base.ToTreeAttributes(tree);
		}
	}
}