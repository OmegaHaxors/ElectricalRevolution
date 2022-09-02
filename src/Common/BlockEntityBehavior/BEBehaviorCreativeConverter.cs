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
	public class BEBehaviorCreativeConverter: BEBehaviorElectricalConverter
	{
		private float powerSetting;

		public BEBehaviorCreativeConverter(BlockEntity blockentity) : base(blockentity)
		{
			this.powerSetting = 3;
		}
		public override void Initialize(ICoreAPI api, JsonObject properties)
		{base.Initialize(api, properties);}

		internal bool OnInteract(IPlayer byPlayer)
		{
			if(byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack == null){return false;}
			if(!byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack.GetName().Contains("Stick")){return false;}
			int num = (int)this.powerSetting + 1;
			if (num > 10){num = 1;}
			this.powerSetting = num;
			this.Blockentity.MarkDirty(true, null);
			Vec3d pos = this.Blockentity.Pos.ToVec3d().Add(0.5, 0.0, 0.5);
			LightBlockEntity lbe = Blockentity as LightBlockEntity;
			lbe?.SetLightLevelFromVoltage(this.powerSetting);
			this.Api.World.PlaySoundAt(new AssetLocation("game:sounds/toggleswitch"), pos.X, pos.Y, pos.Z, byPlayer, false, 16f, 1f);
			return true;
		}
		public override void GetBlockInfo(IPlayer forPlayer, StringBuilder sb)
		{
			base.GetBlockInfo(forPlayer, sb);
      sb.AppendFormat(Lang.Get("Generator Power: {0}%", new object[]
      {
        10 * this.powerSetting
      }), Array.Empty<object>()).AppendLine();
		}
		public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor world)
		{
			this.powerSetting = tree.GetFloat("powerconverted", 0f);
			if (this.powerSetting > 10 || this.powerSetting < 1)
			{
				this.powerSetting = 3;
			}
			base.FromTreeAttributes(tree, world);
		}
		public override void ToTreeAttributes(ITreeAttribute tree)
		{
			base.Voltage = this.powerSetting;
			base.Powerconverted = this.powerSetting;
			base.ToTreeAttributes(tree);
		}
	}
}